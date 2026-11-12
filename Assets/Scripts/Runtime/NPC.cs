using UnityEngine;

/// <summary>
/// NPC that looks at the player when the player enters its front detection range.
///   Far  state (between nearRange and detectRange): only the HEAD (and eyes) turns.
///   Near state (inside nearRange):                    the BODY helps rotate too.
///
/// Special check when an object tagged "Player" enters range:
///   1) Look for a Cinemachine camera inside the player (children)  -> use it as the look target
///   2) else look for a Camera component inside the player          -> use it as the look target
///   3) else fall back to the player root (+ targetHeightOffset)
/// So the NPC appears to face the player's "eyes"/view.
///
/// Requires: Humanoid Animator + "IK Pass" enabled on the Animator Controller layer.
/// Works with Cinemachine 2.x (CinemachineVirtualCamera / FreeLook) and 3.x (CinemachineCamera)
/// without needing a Cinemachine reference (detected by type name).
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPC : MonoBehaviour
{
    public enum LookState { None, Far, Near }

    [Header("Target")]
    [Tooltip("Tag of the object that triggers the special camera check.")]
    public string playerTag = "Player";
    [Tooltip("Optional manual override. If set, the special check is skipped.")]
    public Transform manualTarget;
    [Tooltip("Used only when NO camera is found in the player (fallback to player root).")]
    public float targetHeightOffset = 1.6f;

    [Header("Special Check (what to look at)")]
    public bool preferCinemachine = true;
    [Tooltip("Include inactive child objects when searching for cameras.")]
    public bool includeInactive = true;

    [Header("Detection")]
    [Tooltip("Max distance at which the NPC notices the player (head only).")]
    public float detectRange = 10f;
    [Tooltip("Inside this distance the body also turns.")]
    public float nearRange = 4f;
    [Tooltip("Total field of view in FRONT of the NPC (degrees).")]
    [Range(10f, 360f)] public float viewAngle = 140f;
    [Tooltip("Extra distance before the NPC stops looking (prevents flickering at the edge).")]
    public float loseSensitivity = 1f;

    [Header("Pitch Limits (prevents leaning backward)")]
    [Range(0f, 60f)] public float maxLookUp = 12f;
    [Range(0f, 60f)] public float maxLookDown = 25f;

    [Header("Look Weights")]
    [Range(0f, 1f)] public float farHeadWeight = 1f;
    [Range(0f, 1f)] public float farBodyWeight = 0f;
    [Range(0f, 1f)] public float nearHeadWeight = 1f;
    [Range(0f, 1f)] public float nearBodyWeight = 0.3f;
    [Range(0f, 1f)] public float eyesWeight = 1f;
    [Range(0f, 1f)] public float clampWeight = 0.7f;

    [Header("Smoothing")]
    public float weightSpeed = 4f;
    public float lookSpeed = 6f;

    [Header("Debug (read only)")]
    public LookState CurrentState = LookState.None;
    public string resolvedTargetInfo = "-";

    private Animator animator;
    private Transform playerRoot;      // object with the Player tag
    private Transform lookTarget;      // what we actually look at (camera or player root)
    private bool lookTargetIsCamera;
    private bool playerWasInRange;

    private float currentMasterWeight;
    private float currentHeadWeight;
    private float currentBodyWeight;
    private Vector3 currentLookPos;
    private bool hasLookPos;

    void Start()
    {
        animator = GetComponent<Animator>();
        FindPlayerRoot();
    }

    void Update()
    {
        if (playerRoot == null) FindPlayerRoot(); // player may spawn later

        CurrentState = EvaluateState();
    }

    private void FindPlayerRoot()
    {
        if (string.IsNullOrEmpty(playerTag)) return;
        GameObject found = GameObject.FindGameObjectWithTag(playerTag);
        if (found != null) playerRoot = found.transform;
    }

    // ------------------------------------------------------------------
    //  State detection + special check on "Player enters range"
    // ------------------------------------------------------------------
    private LookState EvaluateState()
    {
        if (manualTarget != null)
        {
            lookTarget = manualTarget;
            lookTargetIsCamera = true; // no height offset on a manual target
            resolvedTargetInfo = "Manual: " + manualTarget.name;
            return StateFromTransform(manualTarget, ref playerWasInRange);
        }

        if (playerRoot == null) return LookState.None;

        LookState state = StateFromTransform(playerRoot, ref playerWasInRange, out bool justEntered);

        // Player just entered the range -> do the special check
        if (justEntered || (state != LookState.None && lookTarget == null))
            ResolveLookTarget();

        return state;
    }

    private LookState StateFromTransform(Transform t, ref bool wasInRange)
    {
        return StateFromTransform(t, ref wasInRange, out _);
    }

    private LookState StateFromTransform(Transform t, ref bool wasInRange, out bool justEntered)
    {
        justEntered = false;

        Vector3 toTarget = t.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;

        // Hysteresis so it doesn't flicker at the edge
        float maxRange = wasInRange ? detectRange + loseSensitivity : detectRange;
        bool inRange = dist <= maxRange;

        // Must be in FRONT of the NPC
        if (inRange && dist > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, toTarget);
            if (angle > viewAngle * 0.5f) inRange = false;
        }

        if (inRange && !wasInRange) justEntered = true;
        wasInRange = inRange;

        if (!inRange) return LookState.None;
        return dist <= nearRange ? LookState.Near : LookState.Far;
    }

    // ------------------------------------------------------------------
    //  SPECIAL CHECK: Cinemachine camera first, then Camera component
    // ------------------------------------------------------------------
    private void ResolveLookTarget()
    {
        lookTarget = null;
        lookTargetIsCamera = false;

        if (playerRoot == null) return;

        Transform found = null;
        string info = "";

        if (preferCinemachine)
        {
            found = FindCinemachineCamera(playerRoot, out info);
            if (found == null) found = FindUnityCamera(playerRoot, out info);
        }
        else
        {
            found = FindUnityCamera(playerRoot, out info);
            if (found == null) found = FindCinemachineCamera(playerRoot, out info);
        }

        if (found != null)
        {
            lookTarget = found;
            lookTargetIsCamera = true;
            resolvedTargetInfo = info;
        }
        else
        {
            // Nothing found -> fall back to the player root
            lookTarget = playerRoot;
            lookTargetIsCamera = false;
            resolvedTargetInfo = "Fallback: Player root";
        }
    }

    private Transform FindCinemachineCamera(Transform root, out string info)
    {
        info = "";
        Component[] all = root.GetComponentsInChildren<Component>(includeInactive);
        foreach (Component c in all)
        {
            if (c == null) continue;
            if (IsCinemachineVirtualCamera(c.GetType()))
            {
                info = "Cinemachine: " + c.gameObject.name + " (" + c.GetType().Name + ")";
                return c.transform;
            }
        }
        return null;
    }

    private Transform FindUnityCamera(Transform root, out string info)
    {
        info = "";
        Camera cam = root.GetComponentInChildren<Camera>(includeInactive);
        if (cam != null)
        {
            info = "Camera: " + cam.gameObject.name;
            return cam.transform;
        }
        return null;
    }

    // Detect Cinemachine virtual cameras by type name, so no Cinemachine package reference is needed.
    // v2: CinemachineVirtualCamera / CinemachineFreeLook ...  v3: CinemachineCamera
    // All of them derive from CinemachineVirtualCameraBase.
    private static bool IsCinemachineVirtualCamera(System.Type type)
    {
        while (type != null && type != typeof(object))
        {
            string n = type.Name;
            if (n == "CinemachineVirtualCameraBase" || n == "CinemachineCamera" || n == "CinemachineVirtualCamera")
                return true;
            type = type.BaseType;
        }
        return false;
    }

    // ------------------------------------------------------------------
    //  IK
    // ------------------------------------------------------------------
    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        float targetMaster = 0f;
        float targetHead = 0f;
        float targetBody = 0f;

        switch (CurrentState)
        {
            case LookState.Far:
                targetMaster = 1f;
                targetHead = farHeadWeight;
                targetBody = farBodyWeight;
                break;
            case LookState.Near:
                targetMaster = 1f;
                targetHead = nearHeadWeight;
                targetBody = nearBodyWeight;
                break;
        }

        float t = Time.deltaTime * weightSpeed;
        currentMasterWeight = Mathf.Lerp(currentMasterWeight, targetMaster, t);
        currentHeadWeight = Mathf.Lerp(currentHeadWeight, targetHead, t);
        currentBodyWeight = Mathf.Lerp(currentBodyWeight, targetBody, t);

        if (lookTarget != null)
        {
            Vector3 raw = lookTarget.position;
            if (!lookTargetIsCamera) raw += Vector3.up * targetHeightOffset;

            Vector3 goal = ClampPitch(raw);

            if (!hasLookPos)
            {
                currentLookPos = goal;
                hasLookPos = true;
            }
            else
            {
                currentLookPos = Vector3.Lerp(currentLookPos, goal, Time.deltaTime * lookSpeed);
            }
        }

        if (currentMasterWeight > 0.001f && hasLookPos)
        {
            animator.SetLookAtWeight(currentMasterWeight, currentBodyWeight, currentHeadWeight, eyesWeight, clampWeight);
            animator.SetLookAtPosition(currentLookPos);
        }
        else
        {
            animator.SetLookAtWeight(0f);
        }
    }

    /// <summary>Limits the vertical angle so the spine doesn't bend backward.</summary>
    private Vector3 ClampPitch(Vector3 goal)
    {
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 origin = head != null ? head.position : transform.position + Vector3.up * 1.6f;

        Vector3 dir = goal - origin;
        float flatDist = new Vector2(dir.x, dir.z).magnitude;
        if (flatDist < 0.01f) flatDist = 0.01f;

        float pitch = Mathf.Atan2(dir.y, flatDist) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, -maxLookDown, maxLookUp);

        float newY = Mathf.Tan(pitch * Mathf.Deg2Rad) * flatDist;
        return origin + new Vector3(dir.x, newY, dir.z);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 pos = transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, detectRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos, nearRange);

        Gizmos.color = Color.cyan;
        Vector3 left = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 right = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawRay(pos, left * detectRange);
        Gizmos.DrawRay(pos, right * detectRange);

        if (Application.isPlaying && lookTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pos + Vector3.up * 1.6f, currentLookPos);
        }
    }
}