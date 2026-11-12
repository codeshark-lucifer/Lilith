using UnityEngine;

/// <summary>
/// Drives first-person camera look by rotating:
///   • The player body (yaw — left/right, full 360°)
///   • This transform (pitch — up/down, clamped)
///
/// For FBFP this GameObject should be the "CameraRot" pivot sitting at eye-height
/// inside the Player root. The CinemachineCamera (CM) must be a direct child of
/// this transform so camera orientation and IK aim share the same space.
/// </summary>
public class CameraControls : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player root. It rotates left/right (yaw); this camera rotates up/down (pitch).")]
    [SerializeField] private Transform playerBody;

    [Tooltip("Optional: assign the Unity Camera that the Cinemachine Brain outputs to. " +
             "Used by FirstPersonBody and PlayerInteractor so they share the same camera reference.")]
    [SerializeField] private Camera playerCamera;

    [Header("Look Settings")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private bool invertY = false;

    private float pitch;
    private bool isGamePaused;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Current look pitch in degrees (positive = looking up when invertY=false).</summary>
    public float Pitch => pitch;

    /// <summary>The camera that this controller drives (set via Inspector or auto-found).</summary>
    public Camera PlayerCamera => playerCamera;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-find the camera on the child CM if not assigned
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void HandleGameStateChanged(GameState state)
    {
        isGamePaused = state != GameState.Playing;
        if (state == GameState.Dialogue) ResetRotation();
    }

    private void LateUpdate()
    {
        InputManager input = InputManager.Instance;
        if (input == null || playerBody == null) return;
        if (isGamePaused) return;

        // Mouse delta is already per-frame, so don't multiply by Time.deltaTime.
        Vector2 look = input.LookInput * sensitivity;

        // Yaw: rotate the whole player body.
        playerBody.Rotate(Vector3.up * look.x);

        // Pitch: rotate only this transform (camera pivot), clamped.
        pitch += invertY ? look.y : -look.y;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void ResetRotation()
    {
        pitch = 0f;
        transform.localRotation = Quaternion.identity;
        // Small downward tilt for dialogue — avoids jarring snap to identity
        transform.localRotation = Quaternion.Euler(10f, 0f, 0f);
    }
}
