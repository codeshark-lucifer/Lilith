using UnityEngine;

/// <summary>
/// Full-Body First-Person (FBFP) controller.
///
/// Responsibilities:
///  1. Dynamically adjusts eye height between standing (1.65m) and crouching (0.95m).
///  2. Modulates spine/head IK rig weight based on camera pitch so the character
///     naturally bends down to inspect objects and look at their feet.
///  3. Bridges the real CharacterController velocity (including falling/jumping vertical speed)
///     and grounded state into FootIK.SetMotion() so foot IK accurately predicts ledges,
///     slopes, steps, and landing impacts.
/// </summary>
[RequireComponent(typeof(PlayerControls))]
public class FirstPersonBody : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────

    [Header("Camera & Pivot")]
    [Tooltip("The CameraRot transform driven by CameraControls (pitch pivot).")]
    [SerializeField] private Transform cameraRot;

    [Tooltip("CameraControls script that owns the pitch value.")]
    [SerializeField] private CameraControls cameraControls;

    [Header("Foot IK")]
    [Tooltip("FootIK component on the character (auto-found in children if null).")]
    [SerializeField] private RealStep.FootIK footIK;

    [Header("Neck / Eye Offset")]
    [Tooltip("Local Y offset applied to cameraRot when standing (eye height above character root).")]
    [SerializeField] private float standEyeHeight = 1.65f;

    [Tooltip("Local Y offset applied to cameraRot when crouching.")]
    [SerializeField] private float crouchEyeHeight = 0.95f;

    [Tooltip("How fast the camera Y position lerps between stand and crouch heights.")]
    [SerializeField] private float eyeHeightSpeed = 10f;

    [Header("Spine IK Blending")]
    [Tooltip("Rig weight when looking straight ahead (pitch ≈ 0).")]
    [SerializeField, Range(0f, 1f)] private float spineWeightNeutral = 0.6f;

    [Tooltip("Rig weight when looking fully up or down (|pitch| ≈ maxPitch).")]
    [SerializeField, Range(0f, 1f)] private float spineWeightExtreme = 1.0f;

    [Tooltip("Maximum camera pitch reference for weight interpolation (degrees).")]
    [SerializeField] private float maxPitchRef = 85f;

    // ─── Private state ────────────────────────────────────────────────────────

    private PlayerControls playerControls;
    private CharacterController characterController;
    private UnityEngine.Animations.Rigging.Rig rig;
    private float currentEyeHeight;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        playerControls = GetComponent<PlayerControls>();
        characterController = GetComponent<CharacterController>();

        if (cameraControls == null)
            cameraControls = GetComponentInChildren<CameraControls>();

        if (cameraRot == null && cameraControls != null)
            cameraRot = cameraControls.transform;

        if (footIK == null)
            footIK = GetComponentInChildren<RealStep.FootIK>();

        if (rig == null)
            rig = GetComponentInChildren<UnityEngine.Animations.Rigging.Rig>();

        currentEyeHeight = standEyeHeight;
        if (cameraRot != null)
        {
            Vector3 p = cameraRot.localPosition;
            p.y = currentEyeHeight;
            cameraRot.localPosition = p;
        }
    }

    private void LateUpdate()
    {
        UpdateEyeHeight();
        UpdateSpineIKWeight();
        BridgeFootIK();
    }

    // ─── Eye height (crouch smooth transition) ────────────────────────────────

    private void UpdateEyeHeight()
    {
        if (cameraRot == null || playerControls == null) return;

        float targetHeight = playerControls.IsCrouching ? crouchEyeHeight : standEyeHeight;
        currentEyeHeight = Mathf.Lerp(currentEyeHeight, targetHeight, eyeHeightSpeed * Time.deltaTime);

        Vector3 p = cameraRot.localPosition;
        p.y = currentEyeHeight;
        cameraRot.localPosition = p;
    }

    // ─── Spine IK weight driven by pitch ─────────────────────────────────────

    private void UpdateSpineIKWeight()
    {
        if (rig == null || cameraControls == null) return;

        float absPitch = Mathf.Abs(cameraControls.Pitch);
        float t = Mathf.Clamp01(absPitch / Mathf.Max(maxPitchRef, 1f));
        rig.weight = Mathf.Lerp(spineWeightNeutral, spineWeightExtreme, t);
    }

    // ─── Foot IK bridge ───────────────────────────────────────────────────────

    private void BridgeFootIK()
    {
        if (footIK == null || playerControls == null) return;

        // Use the real CharacterController velocity so FootIK receives vertical velocity (falling/landing)
        Vector3 worldVel = characterController != null ? characterController.velocity : Vector3.zero;

        // Fallback to move direction if CC velocity is very small (e.g. at start of frame)
        if (worldVel.sqrMagnitude < 0.01f && playerControls.MoveDirection.sqrMagnitude > 0.01f)
        {
            Vector2 md = playerControls.MoveDirection;
            float speed = playerControls.IsCrouching ? 1.3f : (playerControls.IsSprinting ? 5.0f : 2.0f);
            worldVel = transform.TransformDirection(new Vector3(md.x, 0f, md.y) * speed);
        }

        footIK.SetMotion(worldVel, playerControls.IsGrounded);
    }
}
