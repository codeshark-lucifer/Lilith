using TMPro;
using UnityEngine;

/// <summary>
/// Floating "[E] Talk to Bob" prompt that pops up in the world above the focused
/// interactable and always faces the camera.
///
/// Setup: create a Canvas (Render Mode = World Space) with a CanvasGroup and a
/// TMP text child. Put this script on the Canvas root. This script manages
/// position, rotation, size and the pop animation, so no Animator is needed.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class InteractionPromptUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInteractor interactor;
    [SerializeField] private CanvasGroup promptCanvasGroup;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string keyLabel = "E";

    [Header("Pop Animation")]
    [SerializeField] private float popInDuration = 0.25f;
    [SerializeField] private float popOutDuration = 0.12f;
    [Tooltip("How far the prompt overshoots its final size when popping in. 0 = no bounce.")]
    [SerializeField] private float overshoot = 1.7f;
    [Tooltip("World distance the prompt rises during pop-in.")]
    [SerializeField] private float riseDistance = 0.15f;

    [Header("Follow")]
    [Tooltip("Smoothing when the target moves. 0 = snap.")]
    [SerializeField] private float followSpeed = 20f;

    [Header("Size")]
    [Tooltip("Keep the prompt the same apparent size on screen regardless of distance.")]
    [SerializeField] private bool constantScreenSize = true;
    [SerializeField] private float baseScale = 0.0015f;       // scale per metre when constantScreenSize = true
    [SerializeField] private float minScale = 0.003f;
    [SerializeField] private float maxScale = 0.01f;
    [SerializeField] private float fixedScale = 0.005f;       // used when constantScreenSize = false

    private IInteractable shownTarget;   // target the prompt is currently attached to
    private Vector3 anchorPos;           // smoothed world position (without the rise offset)
    private float popT;                  // 0 = hidden, 1 = fully shown
    private bool snapPosition;
    private Camera cam;

    private void Awake()
    {
        if (interactor == null) interactor = FindFirstObjectByType<PlayerInteractor>();
        if (promptCanvasGroup == null) promptCanvasGroup = GetComponent<CanvasGroup>();

        GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        promptCanvasGroup.alpha = 0f;
        promptCanvasGroup.blocksRaycasts = false;
        promptCanvasGroup.interactable = false;
    }

    private void OnEnable()
    {
        if (interactor != null) interactor.FocusChanged += OnFocusChanged;
    }

    private void OnDisable()
    {
        if (interactor != null) interactor.FocusChanged -= OnFocusChanged;
    }

    private void OnFocusChanged(IInteractable target)
    {
        // Looking away: keep the old target so we can animate out where it was.
        if (!PlayerInteractor.IsAlive(target)) return;

        // New target: attach to it and replay the pop.
        if (!ReferenceEquals(target, shownTarget))
        {
            shownTarget = target;
            snapPosition = true;
            popT = 0f;
        }
    }

    private void LateUpdate()
    {
        if (cam == null) cam = interactor != null ? interactor.PlayerCamera : Camera.main;
        if (cam == null) return;

        // A destroyed interactable counts as "nothing focused".
        IInteractable focused = interactor != null ? interactor.Current : null;
        if (!PlayerInteractor.IsAlive(focused)) focused = null;

        // The remembered target may have been destroyed while we were fading out.
        if (!PlayerInteractor.IsAlive(shownTarget)) shownTarget = null;

        bool wantVisible = focused != null && shownTarget != null;

        // --- Animate popT ---
        if (wantVisible)
            popT = Mathf.MoveTowards(popT, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, popInDuration));
        else
            popT = Mathf.MoveTowards(popT, 0f, Time.unscaledDeltaTime / Mathf.Max(0.01f, popOutDuration));

        if (popT <= 0f && !wantVisible)
        {
            promptCanvasGroup.alpha = 0f;
            shownTarget = null;
            return;
        }

        // --- Text & position (only while a live target is focused) ---
        if (wantVisible)
        {
            promptText.text = $"[{keyLabel}] {shownTarget.GetPrompt(interactor)}";

            Vector3 wanted = shownTarget.GetPromptWorldPosition();
            anchorPos = (snapPosition || followSpeed <= 0f)
                ? wanted
                : Vector3.Lerp(anchorPos, wanted, 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime));
            snapPosition = false;
        }

        // --- Easing: bounce in, quick ease-in shrink out ---
        float eased = wantVisible ? EaseOutBack(popT, overshoot) : popT * popT;

        // --- Transform ---
        transform.rotation = cam.transform.rotation;   // never tilted or mirrored
        transform.position = anchorPos + cam.transform.up * (riseDistance * (1f - Mathf.Clamp01(eased)));

        float baseSize = fixedScale;
        if (constantScreenSize)
        {
            float dist = Vector3.Distance(cam.transform.position, anchorPos);
            baseSize = Mathf.Clamp(dist * baseScale, minScale, maxScale);
        }
        transform.localScale = Vector3.one * (baseSize * Mathf.Max(0f, eased));

        // --- Fade (faster than the scale so it doesn't look washed out mid-pop) ---
        promptCanvasGroup.alpha = Mathf.Clamp01(popT * 2.5f);
    }

    /// <summary>Ease-out with overshoot (0 -> 1, peaking above 1 before settling).</summary>
    private static float EaseOutBack(float t, float s)
    {
        float c1 = s;
        float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}