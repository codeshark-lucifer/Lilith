using UnityEngine;

// Unity 6 renamed PhysicMaterial to PhysicsMaterial. This alias keeps the script working on both.
#if UNITY_6000_0_OR_NEWER
using PhysMat = UnityEngine.PhysicsMaterial;
#else
using PhysMat = UnityEngine.PhysicMaterial;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerControls : MonoBehaviour
{
    [SerializeField] private PlayerDataSO data;

    private CharacterController controller;
    private Vector3 horizontalVelocity;
    private float verticalVelocity;
    private bool isCrouching;
    private bool isGamePaused;
    private bool wantsCrouch;
    private bool wasGrounded = true; // true so spawning doesn't trigger a landing
    private float nextJumpTime;
    private PhysMat groundMaterial;  // null = default material (or nothing found)

    private readonly Collider[] ceilingHits = new Collider[8];
    private readonly RaycastHit[] groundHits = new RaycastHit[8];

    public bool IsCrouching => isCrouching;
    public bool IsGrounded => controller.isGrounded;
    public bool IsSprinting { get; private set; }

    /// <summary>Material of the surface the player last stood on. Null means default / none.</summary>
    public PhysMat GroundMaterial => groundMaterial;

    /// <summary>
    /// Move input after processing (deadzone, magnitude clamp, and crouch limited to
    /// 4 directions). Both movement and animation use this, so they always agree.
    /// </summary>
    public Vector2 MoveDirection { get; private set; }

    /// <summary>Fired the frame a jump actually starts.</summary>
    public event System.Action Jumped;

    /// <summary>Fired the frame the player touches ground after being airborne (not when it bounces). Passes the downward speed at impact.</summary>
    public event System.Action<float> Landed;

    /// <summary>Fired instead of Landed when the surface bounces the player. Passes the impact speed.</summary>
    public event System.Action<float> Bounced;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (data == null)
        {
            Debug.LogError($"{nameof(PlayerControls)} on {name} has no PlayerDataSO assigned.", this);
            enabled = false;
            return;
        }

        ApplyHeight(data.standHeight);
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
    }

    private void Update()
    {
        InputManager input = InputManager.Instance;
        if (input == null) return;

        HandleJumpAndGravity(input);
        HandleCrouch(input);
        HandleMovement(input);

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        CheckLanding();
    }

    // ---------------------------------------------------------------- Ground surface

    /// <summary>
    /// Finds the physics material under the player. Called after Move(), when isGrounded is
    /// up to date. While airborne the last surface is kept.
    /// </summary>
    private void UpdateGroundMaterial()
    {
        if (!controller.isGrounded) return;

        float r = controller.radius * 0.9f;
        Vector3 origin = transform.position + Vector3.up * (r + controller.skinWidth);
        float distance = controller.skinWidth + data.groundProbeDistance;

        int count = Physics.SphereCastNonAlloc(origin, r, Vector3.down, groundHits, distance,
                                               data.groundMask, QueryTriggerInteraction.Ignore);

        // Pick the closest hit that isn't the player.
        float best = float.MaxValue;
        Collider found = null;
        for (int i = 0; i < count; i++)
        {
            Collider col = groundHits[i].collider;
            if (col == controller || col.transform.IsChildOf(transform)) continue;

            if (groundHits[i].distance < best)
            {
                best = groundHits[i].distance;
                found = col;
            }
        }

        groundMaterial = found != null ? found.sharedMaterial : null;
    }

    /// <summary>Scales acceleration/deceleration by how grippy the current surface is.</summary>
    private float GetFrictionMultiplier()
    {
        // No surface control in the air.
        if (!controller.isGrounded) return 1f;

        float friction = groundMaterial != null ? groundMaterial.dynamicFriction : data.referenceFriction;
        float multiplier = friction / data.referenceFriction;
        return Mathf.Clamp(multiplier, data.frictionMultiplierRange.x, data.frictionMultiplierRange.y);
    }

    // ---------------------------------------------------------------- Landing / bounce

    private void CheckLanding()
    {
        bool grounded = controller.isGrounded;

        if (grounded)
            UpdateGroundMaterial(); // runs before the landing logic so it uses the surface we just hit

        if (grounded && !wasGrounded)
        {
            // verticalVelocity still holds the pre-impact value here (it's only snapped to -2 next frame).
            float impact = -verticalVelocity;

            bool bouncy = groundMaterial != null
                          && groundMaterial.bounciness > data.minBounciness
                          && impact > data.minBounceSpeed;

            if (bouncy)
            {
                verticalVelocity = impact * groundMaterial.bounciness;
                Bounced?.Invoke(impact);
                // No jump cooldown on a bounce, the player is leaving the ground again.
            }
            else
            {
                Landed?.Invoke(impact);

                // The jump delay starts counting from the moment the player lands,
                // so it can't be used up while still in the air.
                nextJumpTime = Time.time + data.jumpCooldown;
            }
        }

        wasGrounded = grounded;
    }

    // ---------------------------------------------------------------- Crouch

    private void HandleCrouch(InputManager input)
    {
        if (data.crouchToggle)
        {
            if (input.CrouchPressed && !isGamePaused) wantsCrouch = !wantsCrouch;
        }
        else
        {
            wantsCrouch = input.CrouchHeld;
        }

        if (wantsCrouch)
        {
            isCrouching = true;
        }
        else if (isCrouching && CanStandUp())
        {
            isCrouching = false;
        }

        float targetHeight = isCrouching ? data.crouchHeight : data.standHeight;
        float newHeight = Mathf.Lerp(controller.height, targetHeight, data.crouchTransitionSpeed * Time.deltaTime);
        ApplyHeight(newHeight);
    }

    private void ApplyHeight(float height)
    {
        // Assumes the player pivot is at the feet. The camera follows the head bone,
        // so only the collider needs to change here.
        controller.height = height;
        controller.center = new Vector3(0f, height * 0.5f, 0f);
    }

    private bool CanStandUp()
    {
        float r = controller.radius * 0.95f;
        Vector3 origin = transform.position + Vector3.up * (data.standHeight - controller.radius);

        int count = Physics.OverlapSphereNonAlloc(origin, r, ceilingHits, data.ceilingMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider hit = ceilingHits[i];

            // Ignore the player's own collider(s); otherwise we could never stand back up.
            if (hit == controller || hit.transform.IsChildOf(transform)) continue;

            return false;
        }

        return true;
    }

    // ---------------------------------------------------------------- Movement

    private void HandleMovement(InputManager input)
    {
        MoveDirection = ProcessMoveInput(input.MoveInput);

        if (isGamePaused)
        {
            MoveDirection = Vector2.zero;
        }

        // Run only when moving forward (straight or diagonal), never backward or pure sideways.
        IsSprinting = input.SprintHeld && !isCrouching && MoveDirection.y > 0.1f;

        float targetSpeed = isCrouching ? data.crouchSpeed : (IsSprinting ? data.runSpeed : data.walkSpeed);

        Vector3 direction = transform.right * MoveDirection.x + transform.forward * MoveDirection.y;
        Vector3 targetVelocity = direction * targetSpeed;

        float rate = MoveDirection.sqrMagnitude > 0.001f ? data.acceleration : data.deceleration;
        rate *= GetFrictionMultiplier(); // ice = slow to start and stop, grippy = snappy

        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * Time.deltaTime);
    }

    private Vector2 ProcessMoveInput(Vector2 raw)
    {
        Vector2 move = Vector2.ClampMagnitude(raw, 1f);
        float magnitude = move.magnitude;

        if (magnitude < data.moveDeadzone) return Vector2.zero;

        if (isCrouching)
        {
            // Crouch only has forward, backward, left, right. Keep the dominant axis
            // (ties go to forward/backward) and keep the same strength.
            move = Mathf.Abs(move.x) > Mathf.Abs(move.y)
                ? new Vector2(Mathf.Sign(move.x) * magnitude, 0f)
                : new Vector2(0f, Mathf.Sign(move.y) * magnitude);
        }

        return move;
    }

    // ---------------------------------------------------------------- Jump & gravity

    private void HandleJumpAndGravity(InputManager input)
    {
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f; // keeps the controller snapped to the ground
        }

        bool canJump = controller.isGrounded
                       && !isCrouching
                       && !isGamePaused
                       && Time.time >= nextJumpTime;

        if (input.JumpPressed && canJump)
        {
            verticalVelocity = Mathf.Sqrt(data.jumpHeight * -2f * data.gravity);
            nextJumpTime = Time.time + data.jumpCooldown; // covers jumps that start without a landing event
            Jumped?.Invoke();
        }

        verticalVelocity += data.gravity * Time.deltaTime;
    }
}