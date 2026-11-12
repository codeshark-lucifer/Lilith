using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One conversation (the "tree" half of the system).
/// It only holds structure. What the player did lives in DialogueState, which is shared by all conversations.
/// </summary>
[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue/Dialogue Graph")]
public class DialogueGraphSO : ScriptableObject
{
    [SerializeReference, HideInInspector]
    public List<DialogueNode> nodes = new List<DialogueNode>();

    public DialogueNode GetNode(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && nodes[i].guid == guid) return nodes[i];
        return null;
    }

    /// <summary>
    /// Picks the Start node to begin from: among Start nodes whose conditions pass,
    /// the highest priority wins (ties: the one created first). Null if none pass.
    /// </summary>
    public DialogueStart FindEntry(DialogueState state)
    {
        DialogueStart best = null;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (!(nodes[i] is DialogueStart start)) continue;
            if (!DialogueCondition.AllMet(start.conditions, state)) continue;
            if (best == null || start.priority > best.priority) best = start;
        }
        return best;
    }
}
