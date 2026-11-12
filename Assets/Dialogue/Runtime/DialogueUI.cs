using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Dialogue UI controller.
/// Features:
/// - Popup scale and fade transitions.
/// - Typewriter dialogue text.
/// - Animated choice buttons (see DialogueChoiceButton) with staggered entrances.
/// - Click feedback: the picked option grows, the others fade, then the dialogue continues.
/// - Automatic choice visibility after typing.
/// - Continue button support.
/// - Unscaled time for animations and typing.
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("Dialogue Panel")]
    [Tooltip("CanvasGroup attached to the dialogue panel.")]
    [SerializeField] private CanvasGroup panel;

    [Tooltip("RectTransform to scale during popup transitions. Defaults to the panel.")]
    [SerializeField] private RectTransform popupTransform;

    [Tooltip("Duration of the popup opening and closing animation.")]
    [SerializeField] private float popupDuration = 0.22f;

    [Tooltip("Initial scale when opening or closing the popup.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float popupStartScale = 0.85f;

    [Tooltip("Final scale relative to the original panel scale.")]
    [SerializeField] private float popupEndScale = 1f;

    [Header("Dialogue Text")]
    [SerializeField] private TMP_Text speakerText;
    [SerializeField] private TMP_Text bodyText;

    [Tooltip("Characters displayed per second. Set to 0 to disable typing.")]
    [SerializeField] private float charactersPerSecond = 45f;

    [Header("Continue Button")]
    [SerializeField] private Button continueButton;

    [Header("Dialogue Choices")]
    [Tooltip("Parent RectTransform for dynamically created choice buttons.")]
    [SerializeField] private RectTransform choicesRoot;

    [Tooltip("Button prefab containing a TMP_Text and optionally DialogueChoiceButton.")]
    [SerializeField] private Button choiceButtonPrefab;

    [Tooltip("Delay between the entrance animations of consecutive choices.")]
    [SerializeField] private float choiceStaggerDelay = 0.06f;

    [Header("Choice Animation")]
    [Tooltip("Show choices only after the dialogue text finishes typing.")]
    [SerializeField] private bool waitForTypingToFinish = true;

    [Tooltip("Add a DialogueChoiceButton to choice buttons that don't have one.")]
    [SerializeField] private bool autoAddChoiceAnimation = true;

    [Tooltip("Seconds the picked option is shown (grow + others fade) before the dialogue continues. 0 = continue instantly.")]
    [SerializeField] private float chosenFeedbackDuration = 0.25f;

    [Tooltip("Select the first available choice so keyboard / gamepad navigation works right away.")]
    [SerializeField] private bool autoSelectFirstChoice = false;

    private DialogueManager manager;

    private Coroutine typingCoroutine;
    private Coroutine popupCoroutine;
    private Coroutine chooseCoroutine;

    private Vector3 originalPopupScale;

    private bool isTyping;
    private bool choicesPending;
    private bool choiceLocked;

    private readonly List<GameObject> spawnedChoices =
        new List<GameObject>();

    private void Awake()
    {
        if (popupTransform == null && panel != null)
            popupTransform = panel.GetComponent<RectTransform>();

        if (popupTransform != null)
            originalPopupScale = popupTransform.localScale;
    }

    private void OnEnable()
    {
        manager = DialogueManager.Instance;

        if (manager == null)
        {
            Debug.LogError(
                "[Dialogue] DialogueUI requires a DialogueManager in the scene."
            );

            enabled = false;
            return;
        }

        manager.OnDialogueStarted += HandleStarted;
        manager.OnLine += HandleLine;
        manager.OnChoices += HandleChoices;
        manager.OnDialogueEnded += HandleEnded;

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        // Keep the panel synchronized with the dialogue manager.
        SetPanelVisible(manager.IsRunning, true);
    }

    private void OnDisable()
    {
        if (manager != null)
        {
            manager.OnDialogueStarted -= HandleStarted;
            manager.OnLine -= HandleLine;
            manager.OnChoices -= HandleChoices;
            manager.OnDialogueEnded -= HandleEnded;
        }

        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinuePressed);
        }

        StopTyping();
        StopPopup();
        StopChoose();

        // Avoid leaving dynamically created buttons behind.
        ClearChoices();
    }

    // =========================================================
    // DIALOGUE MANAGER EVENTS
    // =========================================================

    private void HandleStarted(DialogueGraphSO graph)
    {
        ClearChoices();

        choicesPending = false;
        isTyping = false;

        SetPanelVisible(true);
    }

    private void HandleLine(DialogueLine line)
    {
        ClearChoices();

        choicesPending = false;

        if (speakerText != null)
        {
            speakerText.text = line.speaker;

            speakerText.gameObject.SetActive(
                !string.IsNullOrEmpty(line.speaker)
            );
        }

        if (continueButton != null)
            continueButton.gameObject.SetActive(true);

        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(false);

        ShowText(line.text);
    }

    private void HandleChoices(List<DialogueChoiceView> views)
    {
        ClearChoices();

        choicesPending = true;

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(false);

        if (choicesRoot == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning(
                "[Dialogue] Choices cannot be displayed. " +
                "Assign Choices Root and Choice Button Prefab."
            );

            return;
        }

        int index = 0;

        foreach (DialogueChoiceView view in views)
        {
            Button button = Instantiate(
                choiceButtonPrefab,
                choicesRoot
            );

            button.gameObject.SetActive(true);

            // Assign choice text.
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();

            if (label != null)
                label.text = view.Text;

            // Unavailable choices cannot be selected.
            button.interactable = view.Available;

            // Staggered entrance animation.
            DialogueChoiceButton choiceAnimation =
                button.GetComponent<DialogueChoiceButton>();

            if (choiceAnimation == null && autoAddChoiceAnimation)
                choiceAnimation = button.gameObject.AddComponent<DialogueChoiceButton>();

            if (choiceAnimation != null)
            {
                choiceAnimation.SetEntranceDelay(
                    index * Mathf.Max(0f, choiceStaggerDelay)
                );
            }

            // Capture this particular choice.
            int optionIndex = view.Index;
            GameObject buttonObject = button.gameObject;

            button.onClick.AddListener(
                () => OnChoiceClicked(optionIndex, buttonObject)
            );

            spawnedChoices.Add(buttonObject);

            index++;
        }

        // If no typing animation is active, show the choices immediately.
        if (!waitForTypingToFinish || !isTyping)
        {
            ShowChoices();
        }
    }

    private void HandleEnded()
    {
        StopTyping();
        StopChoose();

        isTyping = false;
        choicesPending = false;

        ClearChoices();

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(false);

        SetPanelVisible(false);
    }

    // =========================================================
    // BUTTON EVENTS
    // =========================================================

    private void OnContinuePressed()
    {
        if (manager == null)
            return;

        // First click completes the typewriter animation.
        if (isTyping)
        {
            FinishTyping();
            return;
        }

        manager.Advance();
    }

    private void OnChoiceClicked(int optionIndex, GameObject clicked)
    {
        if (manager == null || choiceLocked)
            return;

        if (chosenFeedbackDuration <= 0f)
        {
            manager.Choose(optionIndex);
            return;
        }

        chooseCoroutine = StartCoroutine(ChooseRoutine(optionIndex, clicked));
    }

    /// <summary>Plays the picked / dismissed animation, then tells the manager.</summary>
    private IEnumerator ChooseRoutine(int optionIndex, GameObject clicked)
    {
        choiceLocked = true;     // ignore further clicks while the feedback plays

        foreach (GameObject choice in spawnedChoices)
        {
            if (choice == null)
                continue;

            DialogueChoiceButton animation = choice.GetComponent<DialogueChoiceButton>();
            if (animation == null)
                continue;

            if (choice == clicked) animation.PlayChosen();
            else animation.PlayDismissed();
        }

        yield return new WaitForSecondsRealtime(chosenFeedbackDuration);

        chooseCoroutine = null;
        choiceLocked = false;

        manager.Choose(optionIndex);
    }

    private void StopChoose()
    {
        if (chooseCoroutine != null)
        {
            StopCoroutine(chooseCoroutine);
            chooseCoroutine = null;
        }

        choiceLocked = false;
    }

    // =========================================================
    // PANEL POPUP ANIMATION
    // =========================================================

    private void SetPanelVisible(bool visible, bool instant = false)
    {
        if (panel == null)
            return;

        StopPopup();

        float targetAlpha = visible ? 1f : 0f;

        // Prevent interaction with an invisible panel.
        panel.blocksRaycasts = visible;
        panel.interactable = visible;

        if (instant || popupDuration <= 0f || !isActiveAndEnabled)
        {
            panel.alpha = targetAlpha;

            if (popupTransform != null)
            {
                popupTransform.localScale = visible
                    ? originalPopupScale * popupEndScale
                    : originalPopupScale * popupStartScale;
            }

            return;
        }

        popupCoroutine = StartCoroutine(
            PopupRoutine(visible)
        );
    }

    private IEnumerator PopupRoutine(bool visible)
    {
        float duration = Mathf.Max(0.001f, popupDuration);

        float startAlpha = panel.alpha;
        float targetAlpha = visible ? 1f : 0f;

        Vector3 startScale = popupTransform != null
            ? popupTransform.localScale
            : Vector3.one;

        Vector3 targetScale = popupTransform != null
            ? originalPopupScale *
              (visible ? popupEndScale : popupStartScale)
            : Vector3.one;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            // Ease out cubic.
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            panel.alpha = Mathf.Lerp(
                startAlpha,
                targetAlpha,
                eased
            );

            if (popupTransform != null)
            {
                popupTransform.localScale = Vector3.Lerp(
                    startScale,
                    targetScale,
                    eased
                );
            }

            yield return null;
        }

        panel.alpha = targetAlpha;

        if (popupTransform != null)
            popupTransform.localScale = targetScale;

        popupCoroutine = null;
    }

    private void StopPopup()
    {
        if (popupCoroutine == null)
            return;

        StopCoroutine(popupCoroutine);
        popupCoroutine = null;
    }

    // =========================================================
    // TYPEWRITER ANIMATION
    // =========================================================

    private void ShowText(string text)
    {
        StopTyping();

        isTyping = false;

        if (bodyText == null)
            return;

        bodyText.text = text ?? string.Empty;
        bodyText.maxVisibleCharacters = 0;

        bodyText.ForceMeshUpdate();

        if (charactersPerSecond <= 0f)
        {
            bodyText.maxVisibleCharacters = int.MaxValue;

            if (choicesPending)
                ShowChoices();

            return;
        }

        typingCoroutine = StartCoroutine(TypeRoutine());
    }

    private IEnumerator TypeRoutine()
    {
        isTyping = true;

        bodyText.maxVisibleCharacters = 0;
        bodyText.ForceMeshUpdate();

        int totalCharacters = bodyText.textInfo.characterCount;

        float visibleCharacters = 0f;

        while (visibleCharacters < totalCharacters)
        {
            visibleCharacters +=
                charactersPerSecond * Time.unscaledDeltaTime;

            bodyText.maxVisibleCharacters = Mathf.Min(
                totalCharacters,
                Mathf.FloorToInt(visibleCharacters)
            );

            yield return null;
        }

        bodyText.maxVisibleCharacters = int.MaxValue;

        typingCoroutine = null;
        isTyping = false;

        if (choicesPending)
            ShowChoices();
    }

    private void FinishTyping()
    {
        StopTyping();

        isTyping = false;

        if (bodyText != null)
            bodyText.maxVisibleCharacters = int.MaxValue;

        if (choicesPending)
            ShowChoices();
    }

    private void StopTyping()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        isTyping = false;
    }

    // =========================================================
    // CHOICE VISIBILITY
    // =========================================================

    private void ShowChoices()
    {
        if (!choicesPending)
            return;

        choicesPending = false;

        if (choicesRoot != null)
            choicesRoot.gameObject.SetActive(true);

        if (autoSelectFirstChoice)
            SelectFirstAvailableChoice();
    }

    private void SelectFirstAvailableChoice()
    {
        if (EventSystem.current == null)
            return;

        foreach (GameObject choice in spawnedChoices)
        {
            if (choice == null)
                continue;

            Button button = choice.GetComponent<Button>();

            if (button != null && button.interactable)
            {
                EventSystem.current.SetSelectedGameObject(choice);
                return;
            }
        }
    }

    private void ClearChoices()
    {
        foreach (GameObject choice in spawnedChoices)
        {
            if (choice != null)
                Destroy(choice);
        }

        spawnedChoices.Clear();
        choiceLocked = false;
    }
}