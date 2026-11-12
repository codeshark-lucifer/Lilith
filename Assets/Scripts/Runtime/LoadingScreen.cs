using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent loading screen. Call LoadingScreen.Instance.LoadScene("SceneName").
/// Loads the scene in the background, then activates it while the screen
/// is still covering everything.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text statusText;

    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.3f;
    [SerializeField] private float minShowTime = 0.5f;
    [SerializeField] private float barSpeed = 1.5f;

    private bool isLoading;
    private float displayedProgress;
    private float targetProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // keep this object at the root of the hierarchy

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    private void Update()
    {
        if (!isLoading) return;

        // Smooth the bar so it doesn't jump. Unscaled time so it works while paused.
        displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, barSpeed * Time.unscaledDeltaTime);
        progressBar.value = displayedProgress;
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        isLoading = true;
        displayedProgress = 0f;
        targetProgress = 0f;
        progressBar.value = 0f;

        yield return Fade(1f);
        float startTime = Time.unscaledTime;

        // 1) Load the scene in the background, but don't activate it yet.
        SetStatus("Loading...");
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) // Unity stops at 0.9 until activation is allowed
        {
            targetProgress = op.progress; // 0 -> 0.9
            yield return null;
        }
        targetProgress = 0.9f;

        // Let the bar catch up and keep the screen up for a minimum time (avoids a flash).
        while (displayedProgress < 0.89f || Time.unscaledTime - startTime < minShowTime)
            yield return null;

        // 2) Activate the scene. Awake/Start of scene objects run now, still hidden behind the screen.
        SetStatus("Starting...");
        targetProgress = 1f;
        op.allowSceneActivation = true;

        while (!op.isDone) yield return null;
        yield return null; // one extra frame so Start() methods finish

        while (displayedProgress < 0.99f) yield return null;

        yield return Fade(0f);
        isLoading = false;
    }

    private IEnumerator Fade(float target)
    {
        canvasGroup.blocksRaycasts = true;
        float start = canvasGroup.alpha;

        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = target;
        canvasGroup.blocksRaycasts = target > 0f;
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }
}