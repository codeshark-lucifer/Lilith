using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class InputManager : Singleton<InputManager>
{
    [Header("Action Names (must match your Input Actions asset)")]
    [SerializeField] private string moveActionName = "Move";
    [SerializeField] private string lookActionName = "Look";
    [SerializeField] private string sprintActionName = "Sprint";
    [SerializeField] private string crouchActionName = "Crouch";
    [SerializeField] private string jumpActionName = "Jump";
    [SerializeField] private string interactActionName = "Interact";
    [SerializeField] private string inventoryActionName = "Inventory";
    [SerializeField] private string pauseActionName = "Pause";
    [SerializeField] private string objectiveActionName = "Objective"; // NEW

    private PlayerInput input;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction sprintAction;
    private InputAction crouchAction;
    private InputAction jumpAction;
    private InputAction interactAction;
    private InputAction inventoryAction;
    private InputAction pauseAction;
    private InputAction objectiveAction; // NEW

    /// <summary>The runtime actions used by this PlayerInput. Rebinding must go through these.</summary>
    public InputActionAsset Actions => input != null ? input.actions : null;

    // Continuous values
    public Vector2 MoveInput => moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public Vector2 LookInput => lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

    // Held state
    public bool SprintHeld => sprintAction != null && sprintAction.IsPressed();
    public bool CrouchHeld => crouchAction != null && crouchAction.IsPressed();
    public bool JumpHeld => jumpAction != null && jumpAction.IsPressed();

    // One-frame events (read these from Update)
    public bool JumpPressed => jumpAction != null && jumpAction.WasPressedThisFrame();
    public bool JumpReleased => jumpAction != null && jumpAction.WasReleasedThisFrame();
    public bool CrouchPressed => crouchAction != null && crouchAction.WasPressedThisFrame();
    public bool CrouchReleased => crouchAction != null && crouchAction.WasReleasedThisFrame();
    public bool SprintPressed => sprintAction != null && sprintAction.WasPressedThisFrame();
    public bool SprintReleased => sprintAction != null && sprintAction.WasReleasedThisFrame();

    public bool InteractPressed => interactAction != null && interactAction.WasPressedThisFrame();
    public bool InteractReleased => interactAction != null && interactAction.WasReleasedThisFrame();
    public bool InventoryPressed => inventoryAction != null && inventoryAction.WasPressedThisFrame();
    public bool InventoryReleased => inventoryAction != null && inventoryAction.WasReleasedThisFrame();

    // NEW: ignored while rebinding so typing "O" into a rebind doesn't pop the objective.
    public bool ObjectivePressed => !InputRebinder.IsRebinding && objectiveAction != null && objectiveAction.WasPressedThisFrame();
    public bool ObjectiveReleased => objectiveAction != null && objectiveAction.WasReleasedThisFrame();

    // Ignored while rebinding, so the Escape that cancels a rebind doesn't also toggle pause.
    public bool PausePressed => !InputRebinder.IsRebinding && pauseAction != null && pauseAction.WasPressedThisFrame();
    public bool PauseReleased => pauseAction != null && pauseAction.WasReleasedThisFrame();

    protected override void Awake()
    {
        base.Awake();

        // If this was a duplicate, base.Awake destroyed it, so don't continue.
        if (Instance != this) return;

        input = GetComponent<PlayerInput>();

        moveAction = input.actions[moveActionName];
        lookAction = input.actions[lookActionName];
        sprintAction = input.actions[sprintActionName];
        crouchAction = input.actions[crouchActionName];
        jumpAction = input.actions[jumpActionName];
        interactAction = input.actions[interactActionName];
        inventoryAction = input.actions[inventoryActionName];
        pauseAction = input.actions[pauseActionName];
        objectiveAction = input.actions[objectiveActionName]; // NEW

        // Apply the player's saved key bindings.
        InputRebinder.LoadSaved(input.actions);
    }
}