using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Animation for one choice button. DialogueUI adds it automatically, or you can put it on
/// your choice button prefab to tweak the numbers.
///
/// - Entrance: fades in and pops up (with a little overshoot), after a stagger delay set by DialogueUI.
/// - Hover / keyboard-gamepad selection: grows slightly.
/// - Press: shrinks slightly.
/// - Locked (button not interactable): dimmed, no hover effect.
/// - When the player picks one: the picked button grows, the others shrink and fade out.
///
/// It animates alpha and scale only, never position, so it works with Layout Groups.
/// Everything uses unscaled time, so it still plays if you pause the game during dialogue.
/// Can also be added to the Continue button (the entrance delay is then 0).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class DialogueChoiceButton : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    private enum VisualState { Normal, Chosen, Dismissed }

    [Header("Entrance")]
    [SerializeField] private float entranceDuration = 0.28f;
    [Range(0.1f, 1f)]
    [SerializeField] private float entranceStartScale = 0.8f;

    [Header("Hover / press")]
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressedScale = 0.95f;
    [Tooltip("Higher = snappier.")]
    [SerializeField] private float scaleSpeed = 18f;
    [Range(0f, 1f)]
    [SerializeField] private float disabledAlpha = 0.45f;

    [Header("When an option is picked")]
    [SerializeField] private float chosenScale = 1.1f;
    [SerializeField] private float dismissedScale = 0.9f;

    [Header("Events")]
    [Tooltip("Raised when the pointer enters a usable button. Hook a hover sound here.")]
    public UnityEvent onHover = new UnityEvent();

    private CanvasGroup group;
    private Selectable selectable;
    private Vector3 baseScale = Vector3.one;

    private float entranceDelay;
    private float timer;
    private float interactionScale = 1f;
    private float stateAlpha = 1f;

    private bool hovered;
    private bool pressed;
    private bool selected;
    private VisualState state = VisualState.Normal;

    private bool IsUsable => selectable == null || selectable.interactable;

    // ---- API used by DialogueUI ----------------------------------------------------

    /// <summary>Seconds to wait before this button's entrance starts.</summary>
    public void SetEntranceDelay(float seconds)
    {
        entranceDelay = Mathf.Max(0f, seconds);
    }

    /// <summary>This is the picked option: grow and stay fully visible.</summary>
    public void PlayChosen()
    {
        state = VisualState.Chosen;
    }

    /// <summary>Another option was picked: shrink and fade out.</summary>
    public void PlayDismissed()
    {
        state = VisualState.Dismissed;
    }

    // ---- lifecycle -----------------------------------------------------------------

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        selectable = GetComponent<Selectable>();
        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        timer = 0f;
        interactionScale = 1f;
        hovered = pressed = selected = false;
        state = VisualState.Normal;
        stateAlpha = IsUsable ? 1f : disabledAlpha;

        Tick(0f);       // first frame already shows the start pose, so there is no flicker
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
        if (group != null) group.alpha = 1f;
    }

    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    private void Tick(float dt)
    {
        timer += dt;

        float progress = entranceDuration <= 0f
            ? 1f
            : Mathf.Clamp01((timer - entranceDelay) / entranceDuration);

        bool usable = IsUsable;
        float targetScale = 1f;
        float targetAlpha;

        switch (state)
        {
            case VisualState.Chosen:
                targetScale = chosenScale;
                targetAlpha = 1f;
                break;

            case VisualState.Dismissed:
                targetScale = dismissedScale;
                targetAlpha = 0f;
                break;

            default:
                targetAlpha = usable ? 1f : disabledAlpha;
                if (usable)
                {
                    if (pressed) targetScale = pressedScale;
                    else if (hovered || selected) targetScale = hoverScale;
                }
                break;
        }

        float blend = 1f - Mathf.Exp(-scaleSpeed * dt);
        interactionScale = Mathf.Lerp(interactionScale, targetScale, blend);
        stateAlpha = Mathf.Lerp(stateAlpha, targetAlpha, blend);

        float entranceScale = Mathf.LerpUnclamped(entranceStartScale, 1f, EaseOutBack(progress));
        float entranceAlpha = 1f - (1f - progress) * (1f - progress);

        transform.localScale = baseScale * (entranceScale * interactionScale);
        group.alpha = entranceAlpha * stateAlpha;
        group.blocksRaycasts = progress >= 0.5f;     // can't be clicked while still mostly invisible
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // ---- pointer / selection -------------------------------------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (IsUsable && state == VisualState.Normal) onHover?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    public void OnSelect(BaseEventData eventData) { selected = true; }
    public void OnDeselect(BaseEventData eventData) { selected = false; }
}