using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Set by the main menu and read by the game scene: false = new game, true = load the save.
/// </summary>
public static class GameSession
{
    public static bool LoadSaveOnStart;
}

public class MainMenu : MonoBehaviour
{
    [Serializable]
    private struct SocialLink
    {
        public Button button;
        public string url;
    }

    [Header("Game")]
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string saveFileName = "save.json";

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject creditsPanel;
    [SerializeField] private GameObject followPanel;
    [SerializeField] private SettingsMenu settingsMenu;

    [Header("Main buttons")]
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button followButton;

    [Header("Back buttons (credits and follow panels)")]
    [SerializeField] private Button[] backButtons;

    [Header("Social links (Follow Us panel)")]
    [SerializeField] private SocialLink[] socialLinks;

    private void Awake()
    {
        newGameButton.onClick.AddListener(() => StartGame(false));
        loadGameButton.onClick.AddListener(() => StartGame(true));
        settingsButton.onClick.AddListener(OpenSettings);
        creditsButton.onClick.AddListener(() => ShowPanel(creditsPanel));
        followButton.onClick.AddListener(() => ShowPanel(followPanel));
        quitButton.onClick.AddListener(Quit);

        foreach (Button back in backButtons)
            back.onClick.AddListener(ShowMain);

        foreach (SocialLink link in socialLinks)
        {
            string url = link.url;
            if (link.button != null && !string.IsNullOrEmpty(url))
                link.button.onClick.AddListener(() => Application.OpenURL(url));
        }

        settingsMenu.Closed += ShowMain;
    }

    private void OnDestroy()
    {
        if (settingsMenu != null) settingsMenu.Closed -= ShowMain;
    }

    private void Start()
    {
        // Coming back from a paused game: make sure time runs and the cursor is free.
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ShowMain();
    }

    private void ShowMain()
    {
        ShowPanel(mainPanel);
        loadGameButton.interactable = HasSave();
    }

    private void ShowPanel(GameObject target)
    {
        if (mainPanel != null) mainPanel.SetActive(target == mainPanel);
        if (creditsPanel != null) creditsPanel.SetActive(target == creditsPanel);
        if (followPanel != null) followPanel.SetActive(target == followPanel);
    }

    private void OpenSettings()
    {
        ShowPanel(null); // hide the other panels
        settingsMenu.Open();
    }

    private void StartGame(bool loadSave)
    {
        GameSession.LoadSaveOnStart = loadSave;

        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.LoadScene(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);
    }

    // Replace with a call to your save system once you have one.
    private bool HasSave()
    {
        return File.Exists(Path.Combine(Application.persistentDataPath, saveFileName));
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}