using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class ObjectiveView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image iconImage; // optional

    [Header("Timing")]
    [SerializeField] private float fadeInTime = 0.3f;
    [SerializeField] private float visibleTime = 4f;
    [SerializeField] private float fadeOutTime = 0.6f;

    [Header("Behaviour")]
    [SerializeField] private bool showWhenObjectiveChanges = true;

    private CanvasGroup group;
    private Coroutine routine;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Start()
    {
        // Subscribe in Start so the singletons are guaranteed to exist.
        if (ObjectiveManager.Instance != null)
        {
            ObjectiveManager.Instance.ObjectiveChanged += OnObjectiveChanged;
            Refresh(ObjectiveManager.Instance.Current);
        }
    }

    private void OnDestroy()
    {
        if (ObjectiveManager.Instance != null)
            ObjectiveManager.Instance.ObjectiveChanged -= OnObjectiveChanged;
    }

    private void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.ObjectivePressed)
            Show();
    }

    private void OnObjectiveChanged(ObjectiveData objective)
    {
        Refresh(objective);
        if (showWhenObjectiveChanges && objective != null) Show();
    }

    private void Refresh(ObjectiveData objective)
    {
        if (titleText) titleText.text = objective != null ? objective.title : "No active objective";
        if (descriptionText) descriptionText.text = objective != null ? objective.description : string.Empty;

        if (iconImage)
        {
            bool hasIcon = objective != null && objective.icon != null;
            iconImage.enabled = hasIcon;
            if (hasIcon) iconImage.sprite = objective.icon;
        }
    }

    public void Show()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowThenHide());
    }

    private IEnumerator ShowThenHide()
    {
        yield return Fade(group.alpha, 1f, fadeInTime);
        yield return new WaitForSecondsRealtime(visibleTime);
        yield return Fade(1f, 0f, fadeOutTime);
        routine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }
}