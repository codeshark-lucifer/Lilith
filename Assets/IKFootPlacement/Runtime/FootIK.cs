using UnityEngine;
using UnityEngine.Events;

namespace RealStep
{
    [AddComponentMenu("RealStep/Foot IK")]
    [RequireComponent(typeof(Animator))]
    public class FootIK : MonoBehaviour
    {
        [Tooltip("Global IK weight. Set to 0 to completely disable foot IK.")]
        [Range(0, 1)] public float Weight = 1f;

        [Tooltip("Maximum step height the foot can adapt to (up and down).")]
        public float MaxStep = 0.75f;

        [Tooltip("Automatically calculate MaxStep from character height.")]
        public bool AutoMaxStep;

        [Tooltip("Radius for ground detection (used to probe past step risers).")]
        public float FootRadius = 0.15f;

        [Tooltip("Layer mask used for ground detection raycasting.")]
        public LayerMask Ground = 1;

        [Tooltip("Extra clearance between the sole and the ground. 0 is correct for most rigs; raise it if feet clip.")]
        public float Offset = 0f;

        [Tooltip("Height of the ankle bone above the sole. 0 = measured automatically from the pose at Start.")]
        public float AnkleHeight = 0f;

        [Tooltip("Height above the ground at which a swinging foot is no longer pulled toward the ground.")]
        public float SwingFadeHeight = 0.25f;

        [Tooltip("Distance from the ankle to the tip of the toes. 0 = measured automatically from the toe bone.")]
        public float ToeLength = 0f;

        [Tooltip("Distance from the ankle back to the heel. 0 = estimated automatically.")]
        public float HeelLength = 0f;

        [Tooltip("Responsiveness of pelvis height adjustment (higher = snappier).")]
        public float HipsPositionSpeed = 8f;

        [Tooltip("Responsiveness of foot height adjustment (higher = snappier). Raising is always 2x faster to avoid clipping.")]
        public float FeetPositionSpeed = 14f;

        [Tooltip("Speed of foot rotation adjustment in degrees per second.")]
        public float FeetRotationSpeed = 360f;

        [Tooltip("Weight of pelvis height adjustment.")]
        [Range(0, 1)] public float HipsWeight = 0.8f;

        [Tooltip("Weight of left foot IK position and rotation.")]
        [Range(0, 1)] public float LeftFootWeight = 1f;

        [Tooltip("Weight of right foot IK position and rotation.")]
        [Range(0, 1)] public float RightFootWeight = 1f;

        [Tooltip("Enable body tilt to match ground slope.")]
        public bool EnableBodyTilt = true;

        [Tooltip("Amount of body tilt applied on slopes.")]
        [Range(0, 1)] public float TiltAmount = 0.5f;

        [Tooltip("Responsiveness of body tilt (higher = snappier).")]
        public float TiltSpeed = 10f;

        [Tooltip("Invert the body tilt direction.")]
        public bool InvertTilt = true;

        // ---------------- Predictive scan ----------------
        [Tooltip("Scan the ground ahead of each foot along the movement direction to anticipate steps, ledges and landings.")]
        public bool PredictiveScan = true;

        [Tooltip("How far into the future (seconds) the scan looks along the current velocity.")]
        public float LookAheadTime = 0.25f;

        [Tooltip("Maximum scan distance in metres (caps the look-ahead at sprint speed).")]
        public float MaxLookAhead = 0.8f;

        [Tooltip("Number of ground samples taken along the scan path.")]
        [Range(2, 8)] public int ScanSamples = 4;

        [Tooltip("Extra height a swinging foot keeps above upcoming ground (prevents toe stubbing).")]
        public float SwingClearance = 0.04f;

        [Tooltip("While falling, how far below the character to look for the landing surface.")]
        public float LandingScanDistance = 2f;

        [Tooltip("Distance above the landing surface at which the legs start reaching for the ground.")]
        public float LandingReachDistance = 0.8f;

        [Tooltip("Toggle debug gizmos in Scene view.")]
        public bool ShowDebug;

        [Tooltip("Fired when the foot touches the ground (use for footstep sounds/particles).")]
        public UnityEvent OnLeftFootGrounded;
        public UnityEvent OnLeftFootLifted;
        public UnityEvent OnRightFootGrounded;
        public UnityEvent OnRightFootLifted;

        const float NormalUpThreshold = 0.7f;
        const float ContactEnter = 0.03f;
        const float ContactExit = 0.06f;
        const int IKLayer = 0;              // Animator layer that has "IK Pass" enabled
        const float NoGround = -1e9f;

        class Foot
        {
            public AvatarIKGoal goal;
            public bool hit, contact, prevContact;
            public float minY;
            public float targetDelta;
            public float delta;
            public float weight;
            public float blend;         // narrow: 1 when the animated foot is planted (rotation, contact, floor)
            public float reachBlend;    // wide: starts adapting to the predicted ground before touchdown
            public float pathY;         // smoothed highest ground along the scan path
            public Vector3 normal = Vector3.up;
            public Vector3 smoothNormal = Vector3.up;
            public Quaternion smoothRot = Quaternion.identity;
            public Vector3 animPos;
            public Quaternion animRot;

            // Foot geometry, stored in the foot bone's local space (measured at Start)
            public Transform bone;
            public Vector3 soleLocal, toeLocal, heelLocal;
            // Animated (pre-IK) sole/toe/heel offsets from the ankle, refreshed every frame
            public Vector3 vSole, vToe, vHeel;

            public void Reset()
            {
                hit = contact = prevContact = false;
                minY = targetDelta = delta = weight = blend = reachBlend = pathY = 0f;
                normal = smoothNormal = Vector3.up;
                smoothRot = Quaternion.identity;
            }
        }

        struct PathScan
        {
            public bool valid;
            public float maxY;
            public float endY;
        }

        readonly Foot left = new Foot { goal = AvatarIKGoal.LeftFoot };
        readonly Foot right = new Foot { goal = AvatarIKGoal.RightFoot };

        Animator anim;
        CharacterController cc;
        bool ready;
        float characterHeight = 1f;
        float autoAnkle = 0.1f;
        float pelvisOffset;
        Quaternion currentTilt = Quaternion.identity;
        bool isDisabling;
        bool isGrounded;

        // Motion input (from PlayerLocomotion via SetMotion, or estimated as a fallback)
        Vector3 motionVelocity;
        bool motionGrounded = true;
        float lastMotionTime = -10f;
        Vector3 lastRootPos;
        bool hasLastRootPos;
        float fallbackGroundedTimer;
        bool falling;
        float landingReach;

        // These names are read by FootIKEditor through reflection - keep them.
        bool leftGrounded, rightGrounded;
        float falloffWeight;

        float Ankle => AnkleHeight > 0f ? AnkleHeight : autoAnkle;

        static float Damp(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);

        /// <summary>
        /// Call every frame from your locomotion script so the IK knows where the character is going.
        /// If it is never called, velocity/grounded are estimated from the transform and CharacterController.
        /// </summary>
        public void SetMotion(Vector3 worldVelocity, bool grounded)
        {
            motionVelocity = worldVelocity;
            motionGrounded = grounded;
            lastMotionTime = Time.unscaledTime;
        }

        void Awake()
        {
            anim = GetComponent<Animator>();
            cc = GetComponentInParent<CharacterController>();
        }

        void Start()
        {
            if (!anim) anim = GetComponent<Animator>();

            Transform l = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
            Transform r = anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.RightFoot) : null;

            if (l == null || r == null)
            {
                Debug.LogWarning("[RealStep] Humanoid foot bones not found.", this);
                enabled = false;
                return;
            }

            characterHeight = anim.humanScale;

            float h = (l.position.y + r.position.y) * 0.5f - transform.position.y;
            autoAnkle = Mathf.Clamp(h, 0.03f, 0.3f);

            MeasureFoot(left, l, anim.GetBoneTransform(HumanBodyBones.LeftToes));
            MeasureFoot(right, r, anim.GetBoneTransform(HumanBodyBones.RightToes));

            ApplyAutoMaxStep();
            ready = true;
        }

        // Records where the sole, toe tip and heel are relative to the foot bone, from the starting pose.
        void MeasureFoot(Foot f, Transform footBone, Transform toes)
        {
            f.bone = footBone;

            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 ankleW = footBone.position;
            float soleY = AnkleHeight > 0f ? ankleW.y - AnkleHeight : transform.position.y;
            Vector3 soleW = new Vector3(ankleW.x, soleY, ankleW.z);

            float toeLen;
            if (ToeLength > 0f) toeLen = ToeLength;
            else if (toes != null)
            {
                Vector3 d = toes.position - ankleW;
                d.y = 0f;
                toeLen = d.magnitude + 0.04f;   // ball of the foot -> tip of the toes
            }
            else toeLen = 0.2f;
            toeLen = Mathf.Clamp(toeLen, 0.08f, 0.4f);

            float heelLen = HeelLength > 0f ? HeelLength : Mathf.Clamp(toeLen * 0.35f, 0.04f, 0.1f);

            f.soleLocal = footBone.InverseTransformPoint(soleW);
            f.toeLocal = footBone.InverseTransformPoint(soleW + fwd * toeLen);
            f.heelLocal = footBone.InverseTransformPoint(soleW - fwd * heelLen);
        }

        void OnValidate()
        {
            if (anim == null) anim = GetComponent<Animator>();
            if (anim && anim.isHuman)
                characterHeight = anim.humanScale;
            ApplyAutoMaxStep();
        }

        void ApplyAutoMaxStep()
        {
            if (AutoMaxStep)
                MaxStep = Mathf.Max(characterHeight * 0.35f, 0.2f);
        }

        void OnEnable()
        {
            isDisabling = false;
            hasLastRootPos = false;
        }

        void OnDisable()
        {
            falloffWeight = 0f;
            pelvisOffset = 0f;
            landingReach = 0f;
            falling = false;
            currentTilt = Quaternion.identity;
            left.Reset();
            right.Reset();
        }

        public void DisableSmooth()
        {
            isDisabling = true;
        }

        void ReleaseGoals()
        {
            if (!anim) return;
            anim.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            anim.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            anim.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != IKLayer || !ready || !anim) return;

            if (Weight <= 0f)
            {
                ReleaseGoals();
                return;
            }

            float dt;
            switch ((int)anim.updateMode)
            {
                case 1:  dt = Time.fixedDeltaTime; break;      // AnimatePhysics / Fixed
                case 2:  dt = Time.unscaledDeltaTime; break;   // UnscaledTime
                default: dt = Time.deltaTime; break;           // Normal
            }

            UpdateMotionFallback(dt);
            ScanLanding();

            SolveFoot(left, dt);
            SolveFoot(right, dt);

            UpdateGroundedState(dt);
            if (!enabled) return;

            ApplyPelvis(dt);
            if (EnableBodyTilt) ApplyBodyTilt(dt);

            ApplyFoot(left, LeftFootWeight);
            ApplyFoot(right, RightFootWeight);
        }

        // ------------------------------------------------------------------
        // Motion input
        // ------------------------------------------------------------------
        void UpdateMotionFallback(float dt)
        {
            Vector3 p = anim.transform.position;

            if (Time.unscaledTime - lastMotionTime > 0.25f)
            {
                if (hasLastRootPos && dt > 0f)
                    motionVelocity = (p - lastRootPos) / dt;

                if (cc)
                {
                    // CharacterController.isGrounded flickers on slopes: add a short grace period.
                    if (cc.isGrounded) fallbackGroundedTimer = 0.15f;
                    else fallbackGroundedTimer -= dt;
                    motionGrounded = fallbackGroundedTimer > 0f;
                }
                else
                {
                    motionGrounded = true;
                }
            }

            lastRootPos = p;
            hasLastRootPos = true;
        }

        // While falling, look straight down to find the landing surface so the legs can reach for it.
        void ScanLanding()
        {
            landingReach = 0f;
            falling = PredictiveScan && !motionGrounded && motionVelocity.y < -0.5f;
            if (!falling) return;

            Vector3 root = anim.transform.position;
            if (Physics.Raycast(root + Vector3.up * 0.1f, Vector3.down, out RaycastHit h,
                    LandingScanDistance, Ground, QueryTriggerInteraction.Ignore)
                && h.normal.y > NormalUpThreshold)
            {
                float dist = Mathf.Max(0f, root.y - h.point.y);
                float t = 1f - Mathf.Clamp01(dist / Mathf.Max(LandingReachDistance, 0.01f));
                landingReach = t * t * (3f - 2f * t);

                if (ShowDebug)
                    Debug.DrawLine(root, h.point, Color.magenta);
            }
        }

        // ------------------------------------------------------------------
        // Local area scan: sample the ground along the foot's predicted path.
        // ------------------------------------------------------------------
        PathScan ScanPath(Vector3 animPos, float rootY, float ankle, float underY, float startOffset)
        {
            PathScan s = new PathScan { valid = false, maxY = underY, endY = underY };

            Vector3 flatVel = new Vector3(motionVelocity.x, 0f, motionVelocity.z);
            float speed = flatVel.magnitude;
            if (!PredictiveScan || speed < 0.2f) return s;

            float dist = Mathf.Min(speed * LookAheadTime, MaxLookAhead);
            Vector3 dir = flatVel / speed;

            float originY = Mathf.Max(animPos.y, rootY + ankle) + Mathf.Min(MaxStep, 0.4f);
            float reach = originY - (rootY - MaxStep);
            int n = Mathf.Max(2, ScanSamples);

            for (int i = 1; i <= n; i++)
            {
                Vector3 p = new Vector3(animPos.x, originY, animPos.z) + dir * (startOffset + dist * i / n);

                if (Physics.Raycast(p, Vector3.down, out RaycastHit hh, reach, Ground,
                        QueryTriggerInteraction.Ignore)
                    && hh.normal.y > NormalUpThreshold
                    && hh.point.y - rootY <= MaxStep)
                {
                    s.valid = true;
                    s.maxY = Mathf.Max(s.maxY, hh.point.y);
                    s.endY = hh.point.y;

                    if (ShowDebug) Debug.DrawLine(p, hh.point, Color.cyan);
                }
                else if (ShowDebug)
                {
                    Debug.DrawLine(p, p + Vector3.down * reach, Color.gray);
                }
            }

            return s;
        }

        // Ground height under a single probe point, or NoGround if nothing usable is there.
        float SampleY(Vector3 point, float originY, float reach, float rootY)
        {
            if (Physics.Raycast(new Vector3(point.x, originY, point.z), Vector3.down, out RaycastHit h,
                    reach, Ground, QueryTriggerInteraction.Ignore)
                && h.normal.y > 0.3f
                && h.point.y - rootY <= MaxStep)
            {
                if (ShowDebug) Debug.DrawLine(new Vector3(point.x, originY, point.z), h.point, Color.yellow);
                return h.point.y;
            }
            return NoGround;
        }

        void SolveFoot(Foot f, float dt)
        {
            f.animPos = anim.GetIKPosition(f.goal);
            f.animRot = anim.GetIKRotation(f.goal);

            // Animated (pre-IK) sole / toe / heel points relative to the ankle.
            Vector3 ankleW = f.bone.position;
            f.vSole = f.bone.TransformPoint(f.soleLocal) - ankleW;
            f.vToe = f.bone.TransformPoint(f.toeLocal) - ankleW;
            f.vHeel = f.bone.TransformPoint(f.heelLocal) - ankleW;
            Vector3 toeW = f.animPos + f.vToe;
            Vector3 heelW = f.animPos + f.vHeel;

            float rootY = anim.transform.position.y;
            float ankle = Ankle;

            float castUp = Mathf.Min(MaxStep, 0.4f);
            Vector3 origin = f.animPos + Vector3.up * castUp;
            float lowLimit = falling ? Mathf.Max(LandingScanDistance, MaxStep) : MaxStep;
            float castDist = Mathf.Max(0.05f, origin.y - (rootY - lowLimit));

            if (ShowDebug)
                Debug.DrawLine(origin, origin + Vector3.down * castDist, Color.yellow);

            bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit rh, castDist,
                Ground, QueryTriggerInteraction.Ignore);

            if (hit && rh.normal.y <= NormalUpThreshold)
            {
                Vector3 flat = new Vector3(rh.normal.x, 0f, rh.normal.z).normalized;
                Vector3 probe = origin - flat * FootRadius;
                hit = Physics.Raycast(probe, Vector3.down, out rh, castDist, Ground,
                          QueryTriggerInteraction.Ignore)
                      && rh.normal.y > NormalUpThreshold;
            }

            if (hit && rh.point.y - rootY > MaxStep) hit = false;

            bool wasHit = f.hit;
            f.hit = hit;

            // How close is the animated foot to the flat floor the clip was authored on?
            float planeClearance = f.animPos.y - rootY - ankle;
            float fade = Mathf.Max(SwingFadeHeight, 0.01f);

            float t = Mathf.Clamp01(planeClearance / fade);
            f.blend = 1f - t * t * (3f - 2f * t);

            float tw = Mathf.Clamp01(planeClearance / (PredictiveScan ? fade * 2f : fade));
            f.reachBlend = 1f - tw * tw * (3f - 2f * tw);

            if (falling)
            {
                f.blend = Mathf.Max(f.blend, landingReach);
                f.reachBlend = Mathf.Max(f.reachBlend, landingReach);
            }

            if (!hit)
            {
                f.blend = 0f;
                f.reachBlend = 0f;
            }

            f.normal = hit ? rh.normal : Vector3.up;

            // Rotation first: the toe/heel floor below depends on how the foot is actually rotated.
            f.smoothNormal = Vector3.Slerp(f.smoothNormal, f.normal, Damp(12f, dt)).normalized;
            Quaternion targetRot = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(Vector3.up, f.smoothNormal), f.blend);
            f.smoothRot = Quaternion.RotateTowards(f.smoothRot, targetRot, FeetRotationSpeed * dt);

            if (hit)
            {
                float underY = rh.point.y;

                // 1) Look ahead along the movement path (starting beyond the toe tip).
                Vector3 toeFlat = f.vToe; toeFlat.y = 0f;
                PathScan sc = ScanPath(f.animPos, rootY, ankle, underY, toeFlat.magnitude);
                float pathTarget = sc.valid ? sc.maxY : underY;
                if (!wasHit) f.pathY = underY;
                f.pathY = Mathf.Lerp(f.pathY, pathTarget, Damp(18f, dt));

                // 2) Swinging foot aims at the ground it is about to touch.
                float swing = 1f - f.blend;
                float aimY = Mathf.Lerp(underY, sc.endY, swing);

                // 3) Floor: sole, toe tip and heel must all stay above the ground under THEM,
                //    using the foot's real (rotated) orientation. This is what stops toe clipping.
                Quaternion R = f.smoothRot;
                float toeY = SampleY(toeW, origin.y, castDist, rootY);
                float heelY = SampleY(heelW, origin.y, castDist, rootY);

                float ownFloor = underY + Offset - (R * f.vSole).y;
                if (toeY > NoGround) ownFloor = Mathf.Max(ownFloor, toeY + Offset - (R * f.vToe).y);
                if (heelY > NoGround) ownFloor = Mathf.Max(ownFloor, heelY + Offset - (R * f.vHeel).y);

                // 4) Swinging foot also clears the highest ground on its predicted path.
                float lowestLocalY = Mathf.Min((R * f.vSole).y, Mathf.Min((R * f.vToe).y, (R * f.vHeel).y));
                float pathFloor = f.pathY + Offset + SwingClearance - lowestLocalY;
                f.minY = PredictiveScan
                    ? Mathf.Max(ownFloor, Mathf.Lerp(pathFloor, ownFloor, f.blend))
                    : ownFloor;

                float desiredY = Mathf.Max(f.animPos.y + (aimY - rootY) * f.reachBlend, f.minY);
                f.targetDelta = Mathf.Clamp(desiredY - f.animPos.y, -MaxStep, MaxStep);

                float clearance = desiredY - (underY + ankle);
                bool planted = f.blend > 0.5f;
                f.contact = planted && (f.contact ? clearance < ContactExit : clearance < ContactEnter);

                if (ShowDebug)
                    Debug.DrawRay(rh.point, rh.normal * 0.3f, Color.blue);
            }
            else
            {
                f.targetDelta = 0f;
                f.contact = false;

                if (ShowDebug && Physics.Raycast(origin, Vector3.down, out RaycastHit bad, castDist,
                        Ground, QueryTriggerInteraction.Ignore))
                    Debug.DrawRay(bad.point, bad.normal * 0.3f, Color.red);
            }

            float rate = f.targetDelta > f.delta ? FeetPositionSpeed * 2f : FeetPositionSpeed;
            f.delta = Mathf.Lerp(f.delta, f.targetDelta, Damp(rate, dt));
            f.weight = Mathf.MoveTowards(f.weight, hit ? 1f : 0f, dt * 8f);
        }

        void UpdateGroundedState(float dt)
        {
            bool ikActive = left.hit || right.hit;
            isGrounded = ikActive;
            leftGrounded = left.hit;
            rightGrounded = right.hit;

            // In the air IK is only active while a landing is approaching.
            float target = isDisabling ? 0f : (ikActive ? (motionGrounded ? 1f : landingReach) : 0f);
            float speed = target > falloffWeight ? 10f : 15f;
            falloffWeight = Mathf.MoveTowards(falloffWeight, target, speed * dt);

            if (isDisabling && falloffWeight < 0.01f)
            {
                enabled = false;
                return;
            }

            FireEvents(left, OnLeftFootGrounded, OnLeftFootLifted);
            FireEvents(right, OnRightFootGrounded, OnRightFootLifted);
        }

        static void FireEvents(Foot f, UnityEvent grounded, UnityEvent lifted)
        {
            if (f.contact && !f.prevContact) grounded?.Invoke();
            if (!f.contact && f.prevContact) lifted?.Invoke();
            f.prevContact = f.contact;
        }

        void ApplyPelvis(float dt)
        {
            float lowest = 0f;
            if (left.hit) lowest = Mathf.Min(lowest, left.delta);
            if (right.hit) lowest = Mathf.Min(lowest, right.delta);

            // Never drop the pelvis while airborne; only the legs reach for the ground.
            float target = motionGrounded ? lowest * HipsWeight * falloffWeight * Weight : 0f;
            float rate = target < pelvisOffset ? HipsPositionSpeed * 1.5f : HipsPositionSpeed;
            pelvisOffset = Mathf.Lerp(pelvisOffset, target, Damp(rate, dt));

            Vector3 p = anim.bodyPosition;
            p.y += pelvisOffset;
            anim.bodyPosition = p;
        }

        void ApplyBodyTilt(float dt)
        {
            Vector3 n = Vector3.zero;
            if (left.hit) n += left.smoothNormal;
            if (right.hit) n += right.smoothNormal;
            if (n.sqrMagnitude < 0.01f) n = Vector3.up;
            n.Normalize();

            Quaternion slope = Quaternion.FromToRotation(Vector3.up, n);
            if (InvertTilt) slope = Quaternion.Inverse(slope);

            Quaternion target = Quaternion.Slerp(Quaternion.identity, slope,
                TiltAmount * falloffWeight * Weight);

            currentTilt = Quaternion.Slerp(currentTilt, target, Damp(TiltSpeed, dt));
            anim.bodyRotation = currentTilt * anim.bodyRotation;
        }

        void ApplyFoot(Foot f, float footWeight)
        {
            Vector3 pos = f.animPos;
            pos.y += f.delta;
            if (f.hit) pos.y = Mathf.Max(pos.y, f.minY);

            Quaternion rot = f.smoothRot * f.animRot;
            float w = Weight * falloffWeight * footWeight * f.weight;

            anim.SetIKPosition(f.goal, pos);
            anim.SetIKPositionWeight(f.goal, w);
            anim.SetIKRotation(f.goal, rot);
            anim.SetIKRotationWeight(f.goal, w);

            if (ShowDebug)
            {
                Debug.DrawLine(f.animPos, pos, f.contact ? Color.green : Color.white);
                Debug.DrawLine(pos + f.smoothRot * f.vHeel, pos + f.smoothRot * f.vToe, new Color(1f, 0.5f, 0f));
            }
        }
    }
}