using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Full-Body First-Person (FBFP) controller.
///
/// Responsibilities:
///  1. Keeps the player body visible in first-person by assigning the mesh to a
///     dedicated "Body" layer and rendering it with a second camera that has a
///     tiny near-clip plane (prevents the body from vanishing through walls).
///  2. Bridges the camera pitch from CameraControls into the spine/head IK rig
///     via the existing Rig 1 / MultiAimConstraint chain.
///  3. Calls FootIK.SetMotion() every frame so the foot-IK has accurate velocity
///     and grounded data (instead of falling back to the slower transform-delta estimate).
///  4. Optionally offsets the camera pivot (neck) down when crouching.
///
/// Setup:
///  - Attach this component to the Player root (same GameObject as PlayerControls).
///  - Assign all serialized fields in the Inspector.
///  - Create a "Body" layer in Project Settings → Tags & Layers and set the
///    character@olia GameObject's layer (and all its children) to Body.
/// </summary>
[RequireComponent(typeof(PlayerControls))]
public class FirstPersonBody : MonoBehaviour
{
    // ─── References ──────────────────────────────────────────────────────────

    [Header("Camera")]
    [Tooltip("The CinemachineCamera (or any Camera) that the player looks through.")]
    [SerializeField] private Camera mainCamera;

    [Tooltip("The CameraRot transform driven by CameraControls (pitch pivot).")]
    [SerializeField] private Transform cameraRot;

    [Tooltip("CameraControls script that owns the pitch value.")]
    [SerializeField] private CameraControls cameraControls;

    [Header("Body")]
    [Tooltip("Root of the character mesh prefab (character@olia). " +
             "All renderers in this hierarchy will be moved to the Body layer.")]
    [SerializeField] private Transform characterRoot;

    [Tooltip("Layer index assigned to the body mesh. " +
             "Create a 'Body' layer in Project Settings → Tags & Layers.")]
    [SerializeField] private int bodyLayer = 8;

    [Tooltip("If true, spawns a second camera that renders ONLY the Body layer " +
             "with a near-clip of 0.01, preventing the body from clipping walls.")]
    [SerializeField] private bool useBodyCamera = true;

    [Header("Foot IK")]
    [Tooltip("FootIK component on the character@olia object. " +
             "Will receive SetMotion() every frame.")]
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
    [SerializeField, Range(0f, 1f)] private float spineWeightExtreme = 1f;

    [Tooltip("Maximum camera pitch reference for weight interpolation (degrees).")]
    [SerializeField] private float maxPitchRef = 85f;

    // ─── Private state ────────────────────────────────────────────────────────

    private PlayerControls playerControls;
    private Camera bodyCam;

    // Cached Rig (Unity.Animations.Rigging) for weight control
    private UnityEngine.Animations.Rigging.Rig rig;

    private float currentEyeHeight;

    // ─── Unity lifecycle ──────────────────────────────────────────────────────

    private void Awake()
    {
        playerControls = GetComponent<PlayerControls>();

        if (cameraControls == null)
            cameraControls = GetComponentInChildren<CameraControls>();

        if (footIK == null && characterRoot != null)
            footIK = characterRoot.GetComponentInChildren<RealStep.FootIK>();

        // Set body layer on all renderers
        if (characterRoot != null)
            SetLayerRecursive(characterRoot, bodyLayer);

        // Grab rig for weight blending
        if (characterRoot != null)
            rig = characterRoot.GetComponentInChildren<UnityEngine.Animations.Rigging.Rig>();

        // Initialise eye height
        currentEyeHeight = standEyeHeight;
        if (cameraRot != null)
        {
            Vector3 p = cameraRot.localPosition;
            p.y = currentEyeHeight;
            cameraRot.localPosition = p;
        }

        // Spawn body camera
        if (useBodyCamera && mainCamera != null)
            SetupBodyCamera();
    }

    private void LateUpdate()
    {
        UpdateEyeHeight();
        UpdateSpineIKWeight();
        BridgeFootIK();
    }

    // ─── Eye height (crouch bob) ──────────────────────────────────────────────

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

        // Reconstruct the world velocity from the CharacterController move direction + speed.
        // PlayerControls exposes MoveDirection (local XZ) and IsGrounded.
        Vector2 md = playerControls.MoveDirection;
        float speed = playerControls.IsCrouching
            ? GetDataSpeed("crouchSpeed")
            : (playerControls.IsSprinting ? GetDataSpeed("runSpeed") : GetDataSpeed("walkSpeed"));

        Vector3 localVel = new Vector3(md.x, 0f, md.y) * speed;
        Vector3 worldVel = transform.TransformDirection(localVel);

        footIK.SetMotion(worldVel, playerControls.IsGrounded);
    }

    /// <summary>
    /// Reads a speed value from the character controller's velocity via reflection fallback.
    /// We approximate it rather than making PlayerDataSO public, since we can read it from
    /// the CharacterController's actual velocity for accuracy.
    /// </summary>
    private float GetDataSpeed(string fieldName)
    {
        // Best source: the actual character controller velocity magnitude (XZ only).
        CharacterController cc = playerControls.GetComponent<CharacterController>();
        if (cc != null)
        {
            Vector3 flat = new Vector3(cc.velocity.x, 0f, cc.velocity.z);
            return flat.magnitude;
        }
        return 2f; // safe fallback
    }

    // ─── Body camera setup ───────────────────────────────────────────────────

    private void SetupBodyCamera()
    {
        // Create a child camera that renders only the Body layer
        // on top of the main camera, with a near-clip of 0.01 so the
        // body mesh doesn't clip through nearby geometry.

        GameObject camGO = new GameObject("BodyCamera");
        camGO.transform.SetParent(mainCamera.transform, false);
        camGO.layer = gameObject.layer;

        bodyCam = camGO.AddComponent<Camera>();
        bodyCam.clearFlags = CameraClearFlags.Depth;
        bodyCam.cullingMask = 1 << bodyLayer;
        bodyCam.depth = mainCamera.depth + 1;
        bodyCam.nearClipPlane = 0.01f;
        bodyCam.farClipPlane = mainCamera.farClipPlane;
        bodyCam.fieldOfView = mainCamera.fieldOfView;
        bodyCam.renderingPath = mainCamera.renderingPath;
        bodyCam.allowHDR = mainCamera.allowHDR;
        bodyCam.allowMSAA = mainCamera.allowMSAA;

        // Exclude Body layer from main camera so it only renders via the body cam
        mainCamera.cullingMask &= ~(1 << bodyLayer);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
            SetLayerRecursive(child, layer);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Warn if the body layer hasn't been configured
        if (bodyLayer <= 0 || bodyLayer > 31)
            Debug.LogWarning("[FirstPersonBody] bodyLayer must be between 1 and 31. " +
                             "Create a 'Body' layer in Project Settings → Tags & Layers.", this);
    }
#endif
}
