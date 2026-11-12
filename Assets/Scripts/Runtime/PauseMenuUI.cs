using System;
using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [SerializeField] private Transform pauseMenuUI;
    [SerializeField] private Button resumeButton;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        // Ensure the CanvasGroup exists on the target transform
        if (pauseMenuUI != null)
        {
            canvasGroup = pauseMenuUI.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = pauseMenuUI.gameObject.AddComponent<CanvasGroup>();
            }
        }
        HideUI();
    }


    void OnEnable()
    {
        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);
    }

    void OnDisable()
    {
        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeGame);
    }

    void Update()
    {
        // Use a key press (e.g., Escape) to toggle the pause state
        if (InputManager.Instance == null || GameManager.Instance == null) return;

        if (InputManager.Instance.PausePressed)
        {
            if (GameManager.Instance.CurrentGameState == GameState.Paused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    public void PauseGame()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetGameState(GameState.Paused);

        // Freeze time
        // Time.timeScale = 0f;

        // Show UI and enable interaction
        ShowUI();
    }

    public void ResumeGame()
    {
        if (GameManager.Instance == null) return;
        GameManager.Instance.SetGameState(GameState.Playing);

        // Resume time
        Time.timeScale = 1f;

        // Hide UI and disable interaction
        HideUI();
    }

    void HideUI()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    void ShowUI()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
}