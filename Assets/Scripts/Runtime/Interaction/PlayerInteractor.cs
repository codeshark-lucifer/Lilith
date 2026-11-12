using System;
using UnityEngine;

/// <summary>
/// Put this on the Player (or the camera). Every frame it casts a ray from the
/// center of the camera, finds an IInteractable, and lets the player use it.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Settings")]
    [SerializeField] private float interactRange = 3f;
    [Tooltip("Radius of the sphere cast. 0 = thin ray. A small radius (0.1) makes small objects easier to target.")]
    [SerializeField] private float aimRadius = 0.1f;
    [SerializeField] private LayerMask interactMask = ~0;

    /// <summary>Currently focused interactable (null if none).</summary>
    public IInteractable Current { get; private set; }

    /// <summary>The camera used for aiming. Handy for NPC head look-at.</summary>
    public Camera PlayerCamera => playerCamera;

    /// <summary>When true, interaction is paused (dialogue open, inventory open, etc.).</summary>
    public bool InputLocked { get; set; }

    /// <summary>Fired whenever the focused interactable changes (can be null).</summary>
    public event Action<IInteractable> FocusChanged;

    /// <summary>Fired after a successful interaction.</summary>
    public event Action<IInteractable> Interacted;

    private void Reset()
    {
        playerCamera = GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        if (playerCamera == null)
        {
            // Prefer the camera exposed by CameraControls (tracks the head pivot correctly)
            CameraControls cc = GetComponentInChildren<CameraControls>();
            if (cc != null) playerCamera = cc.PlayerCamera;
        }

        if (playerCamera == null) playerCamera = Camera.main;
    }

    private void Update()
    {
        if (InputManager.Instance == null || GameManager.Instance == null ||
            InputLocked || GameManager.Instance.CurrentGameState != GameState.Playing)
        {
            SetCurrent(null);
            return;
        }

        SetCurrent(FindInteractable());

        if (Current != null && InputManager.Instance.InteractPressed)
        {
            IInteractable used = Current;
            used.Interact(this);
            Interacted?.Invoke(used);

            // Single-use / consumed objects: release focus now, before they get
            // destroyed, so nothing touches a dead reference in LateUpdate.
            if (!IsAlive(used) || !used.CanInteract(this))
            {
                SetCurrent(null);
            }
        }
    }

    private IInteractable FindInteractable()
    {
        Transform cam = playerCamera.transform;
        Ray ray = new Ray(cam.position, cam.forward);

        bool hit = aimRadius > 0f
            ? Physics.SphereCast(ray, aimRadius, out RaycastHit info, interactRange, interactMask, QueryTriggerInteraction.Collide)
            : Physics.Raycast(ray, out info, interactRange, interactMask, QueryTriggerInteraction.Collide);

        if (!hit) return null;

        IInteractable found = info.collider.GetComponentInParent<IInteractable>();
        return (found != null && found.CanInteract(this)) ? found : null;
    }

    private void SetCurrent(IInteractable next)
    {
        if (ReferenceEquals(next, Current)) return;

        // Don't call into an object that was destroyed while it was focused.
        if (IsAlive(Current)) Current.OnUnfocus(this);

        Current = next;
        if (IsAlive(Current)) Current.OnFocus(this);

        FocusChanged?.Invoke(Current);
    }

    /// <summary>
    /// Null check that also catches destroyed UnityEngine.Objects hiding behind an
    /// interface reference (where a plain "== null" can't see the destruction).
    /// </summary>
    public static bool IsAlive(IInteractable interactable)
    {
        if (interactable == null) return false;
        if (interactable is UnityEngine.Object obj && obj == null) return false;
        return true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactRange);
    }
#endif
}