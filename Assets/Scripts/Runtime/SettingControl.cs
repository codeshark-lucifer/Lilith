using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Binds a Slider or Toggle to a setting key. Add one per row in the settings page,
/// set the key, assign the slider or toggle. Nothing else to code for a new setting.
/// Set the slider's min/max in the Inspector (volume 0-1, sensitivity 0.1-3, ...).
/// </summary>
public class SettingControl : MonoBehaviour
{
    [SerializeField] private string settingKey = SettingKeys.MasterVolume;
    [SerializeField] private Slider slider;
    [SerializeField] private Toggle toggle;
    [SerializeField] private TMP_Text valueLabel; // optional
    [SerializeField] private bool showAsPercent = true;

    private void Awake()
    {
        if (slider != null) slider.onValueChanged.AddListener(OnSliderChanged);
        if (toggle != null) toggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnEnable()
    {
        SettingsManager settings = SettingsManager.Instance;
        if (settings == null) return;

        settings.SettingChanged += OnSettingChanged;
        Refresh();
    }

    private void OnDisable()
    {
        SettingsManager settings = SettingsManager.Instance;
        if (settings != null) settings.SettingChanged -= OnSettingChanged;
    }

    private void OnSettingChanged(string key, float value)
    {
        if (key == settingKey) Refresh();
    }

    private void Refresh()
    {
        SettingsManager settings = SettingsManager.Instance;
        if (settings == null) return;

        float value = settings.GetFloat(settingKey);

        if (slider != null) slider.SetValueWithoutNotify(value);
        if (toggle != null) toggle.SetIsOnWithoutNotify(value >= 0.5f);

        if (valueLabel != null && slider != null)
            valueLabel.text = showAsPercent ? $"{Mathf.RoundToInt(value * 100f)}%" : value.ToString("0.##");
    }

    private void OnSliderChanged(float value)
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.SetFloat(settingKey, value);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (SettingsManager.Instance != null) SettingsManager.Instance.SetBool(settingKey, isOn);
    }
}