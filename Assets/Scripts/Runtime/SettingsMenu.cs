using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings panel with tabs (Gameplay, Controls, ...). Use the same prefab in the main menu
/// and in the in-game pause menu: call Open() to show it, and listen to Closed to go back.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Serializable]
    private class Tab
    {
        public string name;
        public Button button;
        public GameObject page;
    }

    [SerializeField] private Tab[] tabs;
    [SerializeField] private Button backButton;
    [SerializeField] private Button resetSettingsButton; // on the Gameplay page
    [SerializeField] private Button resetControlsButton; // on the Controls page

    public event Action Closed;
    public bool IsOpen => gameObject.activeSelf;

    private void Awake()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            tabs[i].button.onClick.AddListener(() => ShowTab(index));
        }

        backButton.onClick.AddListener(Close);
        if (resetSettingsButton != null) resetSettingsButton.onClick.AddListener(ResetSettings);
        if (resetControlsButton != null) resetControlsButton.onClick.AddListener(ResetControls);
    }

    private void OnDisable()
    {
        InputRebinder.Cancel();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        ShowTab(0);
    }

    public void Close()
    {
        InputRebinder.Cancel();

        if (SettingsManager.Instance != null) SettingsManager.Instance.Save();

        gameObject.SetActive(false);
        Closed?.Invoke();
    }

    public void ShowTab(int index)
    {
        InputRebinder.Cancel();

        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].page.SetActive(i == index);
            tabs[i].button.interactable = i != index; // the open tab looks "selected"
        }
    }

    private void ResetSettings()
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.ResetToDefaults();
    }

    private void ResetControls()
    {
        InputManager input = InputManager.Instance;
        if (input != null) InputRebinder.ResetAll(input.Actions);
    }
}