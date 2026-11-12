using UnityEditor;
using UnityEngine;

/// <summary>Handy testing shortcuts under Tools > Dialogue.</summary>
public static class DialogueEditorMenu
{
    [MenuItem("Tools/Dialogue/Open Save Folder")]
    private static void OpenSaveFolder()
    {
        EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    [MenuItem("Tools/Dialogue/Delete Saved State")]
    private static void DeleteSavedState()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Delete saved dialogue state?",
            "This removes dialogue_state.json, so every flag and number the player has set is forgotten.",
            "Delete", "Cancel");

        if (confirmed) DialogueStateStore.Delete();
    }
}
