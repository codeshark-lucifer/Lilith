using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Game/Player Data")]
public class PlayerDataSO : ScriptableObject
{
    [Header("Speed")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.0f;
    public float crouchSpeed = 1.3f;
    [Tooltip("How fast you reach target speed when pressing a direction (units/sec²).")]
    public float acceleration = 50f;
    [Tooltip("How fast you stop when releasing input (units/sec²).")]
    public float deceleration = 60f;

    [Header("Input")]
    [Tooltip("Move input below this magnitude is treated as idle.")]
    public float moveDeadzone = 0.1f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.2f;
    [Tooltip("Seconds after landing before the player can jump again.")]
    public float jumpCooldown = 0.4f;
    public float gravity = -20f;

    [Header("Crouch")]
    [Tooltip("On: press once to crouch, press again to stand. Off: crouch while held.")]
    public bool crouchToggle = false;
    public float standHeight = 1.8f;
    public float crouchHeight = 1.0f;
    public float crouchTransitionSpeed = 10f;
    [Tooltip("Layers that block standing up. Do NOT include the Player's own layer.")]
    public LayerMask ceilingMask = ~0;

    [Header("Ground Surface (Physics Material)")]
    [Tooltip("Layers the player can stand on. Do NOT include the Player's own layer.")]
    public LayerMask groundMask = ~0;
    [Tooltip("How far below the feet to look for the surface material.")]
    public float groundProbeDistance = 0.3f;
    [Tooltip("Dynamic friction of a 'normal' surface. Unity's default material is 0.6. " +
             "A surface with this friction uses acceleration/deceleration as-is.")]
    public float referenceFriction = 0.6f;
    [Tooltip("Limits how much a surface can scale acceleration/deceleration (x = min, y = max).")]
    public Vector2 frictionMultiplierRange = new Vector2(0.05f, 2f);
    [Tooltip("Surfaces with bounciness below this value are not bouncy.")]
    [Range(0f, 1f)] public float minBounciness = 0.05f;
    [Tooltip("Landing speed (units/sec) below this value does not bounce.")]
    public float minBounceSpeed = 3f;

    private void OnValidate()
    {
        referenceFriction = Mathf.Max(0.01f, referenceFriction);
        groundProbeDistance = Mathf.Max(0.01f, groundProbeDistance);

        frictionMultiplierRange.x = Mathf.Max(0.01f, frictionMultiplierRange.x);
        frictionMultiplierRange.y = Mathf.Max(frictionMultiplierRange.x, frictionMultiplierRange.y);

        minBounceSpeed = Mathf.Max(0f, minBounceSpeed);
    }
}