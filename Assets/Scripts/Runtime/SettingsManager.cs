using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Setting keys used by the game. To add a setting: add a key here, add a matching
/// definition on SettingsManager, then add a SettingControl in the UI.
/// </summary>
public static class SettingKeys
{
    public const string MasterVolume = "masterVolume";
    public const string MusicVolume = "musicVolume";
    public const string SfxVolume = "sfxVolume";
    public const string MouseSensitivity = "mouseSensitivity";
    public const string InvertY = "invertY";
}

/// <summary>
/// Stores every setting as a float under a string key (bool = 0/1, int = rounded float).
/// Read anywhere with SettingsManager.Instance.GetFloat(SettingKeys.MouseSensitivity).
/// </summary>
public class SettingsManager : Singleton<SettingsManager>
{
    [Serializable]
    public class SettingDefinition
    {
        public string key;
        public float defaultValue;
        [Tooltip("Optional. Exposed AudioMixer parameter driven by this setting (0-1 value -> dB).")]
        public string mixerParameter;
    }

    private const string PrefsPrefix = "setting.";

    [SerializeField] private AudioMixer mixer;
    [SerializeField] private SettingDefinition[] definitions =
    {
        new SettingDefinition { key = SettingKeys.MasterVolume, defaultValue = 1f, mixerParameter = "MasterVolume" },
        new SettingDefinition { key = SettingKeys.MusicVolume, defaultValue = 1f, mixerParameter = "MusicVolume" },
        new SettingDefinition { key = SettingKeys.SfxVolume, defaultValue = 1f, mixerParameter = "SfxVolume" },
        new SettingDefinition { key = SettingKeys.MouseSensitivity, defaultValue = 1f },
        new SettingDefinition { key = SettingKeys.InvertY, defaultValue = 0f },
    };

    private Dictionary<string, SettingDefinition> lookup;

    /// <summary>Fired whenever a setting changes (key, new value).</summary>
    public event Action<string, float> SettingChanged;

    private Dictionary<string, SettingDefinition> Lookup
    {
        get
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, SettingDefinition>();
                foreach (SettingDefinition def in definitions) lookup[def.key] = def;
            }
            return lookup;
        }
    }

    private void Start()
    {
        if (Instance != this) return;
        ApplyAll(); // mixer values are applied in Start because the mixer may not be ready in Awake
    }

    public float GetDefault(string key)
    {
        return Lookup.TryGetValue(key, out SettingDefinition def) ? def.defaultValue : 0f;
    }

    public float GetFloat(string key) => PlayerPrefs.GetFloat(PrefsPrefix + key, GetDefault(key));
    public int GetInt(string key) => Mathf.RoundToInt(GetFloat(key));
    public bool GetBool(string key) => GetFloat(key) >= 0.5f;

    public void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(PrefsPrefix + key, value);
        Apply(key, value);
        SettingChanged?.Invoke(key, value);
    }

    public void SetBool(string key, bool value) => SetFloat(key, value ? 1f : 0f);

    /// <summary>Writes to disk. The settings menu calls this when it closes.</summary>
    public void Save() => PlayerPrefs.Save();

    public void ResetToDefaults()
    {
        foreach (SettingDefinition def in definitions) SetFloat(def.key, def.defaultValue);
        Save();
    }

    private void ApplyAll()
    {
        foreach (SettingDefinition def in definitions) Apply(def.key, GetFloat(def.key));
    }

    private void Apply(string key, float value)
    {
        if (mixer == null) return;
        if (!Lookup.TryGetValue(key, out SettingDefinition def)) return;
        if (string.IsNullOrEmpty(def.mixerParameter)) return;

        float db = Mathf.Log10(Mathf.Max(value, 0.0001f)) * 20f; // 0 -> -80 dB, 1 -> 0 dB
        mixer.SetFloat(def.mixerParameter, db);
    }
}