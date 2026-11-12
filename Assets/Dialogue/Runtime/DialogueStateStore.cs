using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Save / load for DialogueState as a JSON file in Application.persistentDataPath.
/// Call these from your own save system if you already have one
/// (JsonUtility.ToJson(state) gives you the same JSON string to embed anywhere).
/// </summary>
public static class DialogueStateStore
{
    public const string FileName = "dialogue_state.json";

    public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

    public static void Save(DialogueState state, string path = null)
    {
        if (state == null) return;
        path ??= DefaultPath;
        try
        {
            File.WriteAllText(path, JsonUtility.ToJson(state, true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Dialogue] Could not save state to '{path}': {e.Message}");
        }
    }

    /// <summary>Returns the saved state, or a fresh empty one if nothing is saved yet.</summary>
    public static DialogueState Load(string path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var state = JsonUtility.FromJson<DialogueState>(File.ReadAllText(path));
                if (state != null) return state;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Dialogue] Could not load state from '{path}': {e.Message}");
        }
        return new DialogueState();
    }

    public static void Delete(string path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Dialogue] Could not delete '{path}': {e.Message}");
        }
    }
}
