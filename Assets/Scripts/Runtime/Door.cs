using System.Collections;
using UnityEngine;

public class Door : InteractableBase
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float speed = 2f;
    [SerializeField] private bool startLocked = false;

    [Header("Key Lock")]
    [Tooltip("If set, the door can only open when the player has this item in inventory.")]
    [SerializeField] private ItemDataSO requiredKeyItem;
    [Tooltip("If true, consuming one key unlocks the door permanently.")]
    [SerializeField] private bool consumeKeyOnUnlock = true;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [SerializeField] private AudioClip lockedClip;

    [Header("Floating Prompt (in front of door)")]
    [Tooltip("How far in front of the door surface the prompt floats.")]
    [SerializeField] private float promptDistance = 0.6f;
    [Tooltip("Vertical shift from the door's center (e.g. 0 = middle, 0.1 = near handle).")]
    [SerializeField] private float promptHeightOffset = 0f;

    private bool isOpen = false;
    private bool isLocked;
    private bool isAnimating = false;
    private Quaternion closedRotation;
    private Coroutine animationCoroutine;

    // Cached while the door is closed, so the prompt stays put when the door swings
    private Vector3 closedCenter;
    private Vector3 closedNormal;
    private Transform focusedPlayer;

    public bool IsOpen => isOpen;
    public bool IsLocked => isLocked;
    public string MessageInteract => isLocked ? "Locked" : isOpen ? "Close" : "Open";

    public Sprite Icon => null;

    private void Awake()
    {
        prompt = "Open Door";

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;

        closedRotation = transform.rotation;
        isLocked = startLocked;

        // Door center (not the hinge pivot) and the direction its face points
        Collider col = GetComponentInChildren<Collider>();
        closedCenter = col != null ? col.bounds.center : transform.position;
        closedNormal = transform.forward;
    }

    // Show Open / Close / Locked automatically
    public override string GetPrompt(PlayerInteractor interactor) => MessageInteract;

    public override Vector3 GetPromptWorldPosition()
    {
        Vector3 center = closedCenter + Vector3.up * promptHeightOffset;

        // Pick the door face the player is standing on
        float side = 1f;
        if (focusedPlayer != null)
        {
            Vector3 toPlayer = focusedPlayer.position - closedCenter;
            side = Vector3.Dot(closedNormal, toPlayer) >= 0f ? 1f : -1f;
        }

        return center + closedNormal * side * promptDistance;
    }

    public override void Interact(PlayerInteractor interactor)
    {
        base.Interact(interactor);

        if (isAnimating) return;

        PlayerInventory inventory = interactor != null
            ? interactor.GetComponentInParent<PlayerInventory>()
            : null;
        if (isLocked)
        {
            if (requiredKeyItem != null)
            {
                if (inventory != null && inventory.Inventory != null && inventory.Inventory.HasItem(requiredKeyItem))
                {
                    if (consumeKeyOnUnlock)
                        inventory.Inventory.TryConsumeItem(requiredKeyItem, 1);

                    isLocked = false;
                }
                else
                {
                    PlaySound(lockedClip);
                    return;
                }
            }
            else
            {
                PlaySound(lockedClip);
                return;
            }
        }

        var animator = interactor.GetComponentInChildren<Animator>();
        if (animator != null) animator.SetTrigger("OpenDoor");

        // Open/Close sound is played inside OpenDoor() / CloseDoor()
        if (isOpen) CloseDoor(interactor.transform.position);
        else OpenDoor(interactor.transform.position);
    }

    public override void OnFocus(PlayerInteractor interactor)
    {
        base.OnFocus(interactor);
        focusedPlayer = interactor.transform;
    }

    public override void OnUnfocus(PlayerInteractor interactor)
    {
        base.OnUnfocus(interactor);
        focusedPlayer = null;
    }

    public void Unlock() => isLocked = false;
    public void Lock() => isLocked = true;

    private void PlaySound(AudioClip clip)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    public void OpenDoor(Vector3 userPos)
    {
        if (isOpen || isAnimating) return;

        Vector3 toUser = userPos - transform.position;
        float side = Vector3.Dot(transform.forward, toUser) >= 0 ? -1f : 1f;
        Quaternion target = closedRotation * Quaternion.Euler(0f, openAngle * side, 0f);

        isOpen = true;
        PlaySound(openClip);
        Animate(target);
    }

    public void CloseDoor(Vector3 userPos)
    {
        if (!isOpen || isAnimating) return;

        isOpen = false;
        PlaySound(closeClip);
        Animate(closedRotation);
    }

    private void Animate(Quaternion target)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(RotateTo(target));
    }

    private IEnumerator RotateTo(Quaternion target)
    {
        isAnimating = true;
        Quaternion from = transform.rotation;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * speed;
            transform.rotation = Quaternion.Slerp(from, target, t);
            yield return null;
        }

        transform.rotation = target;
        isAnimating = false;
    }
}