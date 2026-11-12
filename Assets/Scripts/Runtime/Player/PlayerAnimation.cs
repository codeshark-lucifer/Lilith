using UnityEngine;

public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerControls controls;
    [SerializeField] private Animator animator;

    [Header("Blend Tree Distances (from the center)")]
    [Tooltip("walk_f / walk_b / walk_sr / walk_sl sit at this distance. Diagonals land at ~0.35 automatically.")]
    [SerializeField] private float walkBlend = 0.5f;
    [Tooltip("run_f sits at this distance.")]
    [SerializeField] private float runBlend = 0.75f;
    [Tooltip("run_fr / run_fl sit at this X. Run sideways is clamped to it.")]
    [SerializeField] private float runSideLimit = 0.5f;
    [SerializeField] private float crouchBlend = 0.5f;

    [Header("Smoothing")]
    [SerializeField] private float dampTime = 0.1f;

    // Animator parameters (rename here if yours differ)
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int IsCrouchHash = Animator.StringToHash("IsCrouch");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private void Awake()
    {
        if (controls == null) controls = GetComponentInParent<PlayerControls>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        if (controls != null) controls.Jumped += OnJumped;
    }

    private void OnDisable()
    {
        if (controls != null) controls.Jumped -= OnJumped;
    }

    private void Update()
    {
        if (controls == null || animator == null) return;

        Vector2 blend = GetBlendPosition();

        animator.SetFloat(MoveXHash, blend.x, dampTime, Time.deltaTime);
        animator.SetFloat(MoveYHash, blend.y, dampTime, Time.deltaTime);

        animator.SetBool(IsCrouchHash, controls.IsCrouching);
        animator.SetBool(IsGroundedHash, controls.IsGrounded);
    }

    private void OnJumped()
    {
        animator.SetTrigger(JumpHash);
    }

    // The blend tree is a map of positions: each clip sits at a point, and the animator
    // blends toward the point we give it. So we send the move direction scaled by how far
    // out that state's clips are (walk 0.5, run 0.75, crouch 0.5).
    private Vector2 GetBlendPosition()
    {
        Vector2 move = controls.MoveDirection; // already deadzoned, clamped, and crouch-snapped
        float strength = move.magnitude;       // 0..1, lower for light stick tilt

        if (strength < 0.001f) return Vector2.zero;

        Vector2 direction = move / strength;

        if (controls.IsCrouching)
        {
            return direction * (crouchBlend * strength);
        }

        if (controls.IsSprinting)
        {
            Vector2 run = direction * (runBlend * strength);
            run.x = Mathf.Clamp(run.x, -runSideLimit, runSideLimit);
            return run;
        }

        return direction * (walkBlend * strength);
    }
}