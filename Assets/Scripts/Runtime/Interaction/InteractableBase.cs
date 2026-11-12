using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Base class for interactables. Inherit from this and override what you need,
/// or use it directly and wire everything up with the UnityEvents in the Inspector.
/// </summary>
public class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [SerializeField] protected string prompt = "Interact";

    [Header("Floating Prompt")]
    [Tooltip("Empty child placed where the prompt should float (e.g. above the NPC's head). If empty, the collider's top is used.")]
    [SerializeField] protected Transform promptAnchor;
    [SerializeField] protected Vector3 promptOffset = new Vector3(0f, 0.3f, 0f);

    [Header("Behaviour")]
    [Tooltip("If true, can only be used once.")]
    [SerializeField] protected bool singleUse = false;

    [Header("Events")]
    public UnityEvent<PlayerInteractor> onInteract;
    public UnityEvent onFocus;
    public UnityEvent onUnfocus;

    protected bool used;
    public bool IsFocused { get; private set; }

    public virtual string GetPrompt(PlayerInteractor interactor) => prompt;

    public virtual Vector3 GetPromptWorldPosition()
    {
        if (promptAnchor != null) return promptAnchor.position + promptOffset;

        // Fallback: top-center of the first collider we can find
        Collider col = GetComponentInChildren<Collider>();
        Vector3 basePos = col != null
            ? new Vector3(col.bounds.center.x, col.bounds.max.y, col.bounds.center.z)
            : transform.position;
        return basePos + promptOffset;
    }

    public virtual bool CanInteract(PlayerInteractor interactor) => !(singleUse && used);

    public virtual void Interact(PlayerInteractor interactor)
    {
        used = true;
        onInteract?.Invoke(interactor);
    }

    public virtual void OnFocus(PlayerInteractor interactor)
    {
        IsFocused = true;
        onFocus?.Invoke();
    }

    public virtual void OnUnfocus(PlayerInteractor interactor)
    {
        IsFocused = false;
        onUnfocus?.Invoke();
    }
}