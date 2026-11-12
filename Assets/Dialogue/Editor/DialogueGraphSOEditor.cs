using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DialogueGraphSO))]
public class DialogueGraphSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var graph = (DialogueGraphSO)target;

        int starts = graph.nodes.Count(n => n is DialogueStart);
        int lines = graph.nodes.Count(n => n is DialogueLine);
        int choices = graph.nodes.Count(n => n is DialogueChoice);

        EditorGUILayout.HelpBox(
            $"{graph.nodes.Count} nodes   ({starts} start, {lines} lines, {choices} choices)",
            MessageType.None);

        if (starts == 0)
            EditorGUILayout.HelpBox("This graph has no Start node, so it can never begin.", MessageType.Warning);

        if (GUILayout.Button("Open Graph Editor", GUILayout.Height(30)))
            DialogueGraphWindow.Open(graph);
    }
}
