using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The "choice memory" half of the dialogue system.
/// Holds named true/false flags (helped_blacksmith) and named numbers (affection.mira).
/// It is global: any conversation can read what an earlier conversation wrote.
/// Unity cannot serialize Dictionary, so entries are copied to lists when saving.
/// </summary>
[Serializable]
public class DialogueState : ISerializationCallbackReceiver
{
    [Serializable] public struct FlagEntry { public string key; public bool value; }
    [Serializable] public struct NumberEntry { public string key; public int value; }

    [SerializeField] private List<FlagEntry> flagEntries = new List<FlagEntry>();
    [SerializeField] private List<NumberEntry> numberEntries = new List<NumberEntry>();

    private Dictionary<string, bool> flags = new Dictionary<string, bool>();
    private Dictionary<string, int> numbers = new Dictionary<string, int>();

    /// <summary>Raised whenever any value changes (handy for quest logs, debug UI, autosave).</summary>
    public event Action Changed;

    public IReadOnlyDictionary<string, bool> Flags => flags;
    public IReadOnlyDictionary<string, int> Numbers => numbers;

    // ---- flags ----------------------------------------------------------------

    public bool GetFlag(string key, bool defaultValue = false)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return flags.TryGetValue(key, out bool value) ? value : defaultValue;
    }

    public void SetFlag(string key, bool value = true)
    {
        if (string.IsNullOrEmpty(key)) return;
        flags[key] = value;
        Changed?.Invoke();
    }

    // ---- numbers --------------------------------------------------------------

    public int GetNumber(string key, int defaultValue = 0)
    {
        if (string.IsNullOrEmpty(key)) return defaultValue;
        return numbers.TryGetValue(key, out int value) ? value : defaultValue;
    }

    public void SetNumber(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        numbers[key] = value;
        Changed?.Invoke();
    }

    public void AddNumber(string key, int amount)
    {
        if (string.IsNullOrEmpty(key)) return;
        numbers[key] = GetNumber(key) + amount;
        Changed?.Invoke();
    }

    // ---- housekeeping ---------------------------------------------------------

    public void Clear()
    {
        flags.Clear();
        numbers.Clear();
        Changed?.Invoke();
    }

    // ---- ISerializationCallbackReceiver (Dictionary <-> list) ------------------

    public void OnBeforeSerialize()
    {
        flagEntries = new List<FlagEntry>(flags.Count);
        foreach (var pair in flags)
            flagEntries.Add(new FlagEntry { key = pair.Key, value = pair.Value });

        numberEntries = new List<NumberEntry>(numbers.Count);
        foreach (var pair in numbers)
            numberEntries.Add(new NumberEntry { key = pair.Key, value = pair.Value });
    }

    public void OnAfterDeserialize()
    {
        flags = new Dictionary<string, bool>();
        numbers = new Dictionary<string, int>();

        if (flagEntries != null)
            foreach (var entry in flagEntries)
                if (!string.IsNullOrEmpty(entry.key)) flags[entry.key] = entry.value;

        if (numberEntries != null)
            foreach (var entry in numberEntries)
                if (!string.IsNullOrEmpty(entry.key)) numbers[entry.key] = entry.value;
    }
}
