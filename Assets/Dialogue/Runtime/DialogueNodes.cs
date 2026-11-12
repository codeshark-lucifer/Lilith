using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base class of every node in a dialogue graph.
/// A node's connections are stored as the guid of the node it leads to ("next"),
/// one per output slot. Slot 0 is the first output port in the editor.
/// </summary>
[Serializable]
public abstract class DialogueNode
{
    public string guid;
    public Vector2 position;                 // only used by the graph editor

    public virtual int OutputCount => 1;
    public abstract string GetNext(int slot);
    public abstract void SetNext(int slot, string targetGuid);
}

/// <summary>
/// Entry point. A graph can have several; the one whose conditions pass
/// (highest priority wins) is where the conversation begins.
/// e.g. "met_mira == true" -> "Welcome back"; no conditions -> "Nice to meet you".
/// </summary>
[Serializable]
public class DialogueStart : DialogueNode
{
    public int priority;
    public List<DialogueCondition> conditions = new List<DialogueCondition>();
    public string nextGuid;

    public override string GetNext(int slot) => nextGuid;
    public override void SetNext(int slot, string targetGuid) => nextGuid = targetGuid;
}

/// <summary>A speaker says some text, then the conversation moves on.</summary>
[Serializable]
public class DialogueLine : DialogueNode
{
    public string speaker;
    [TextArea(2, 6)] public string text;
    public List<DialogueEffect> effects = new List<DialogueEffect>();   // applied when this line is reached
    public string nextGuid;

    public override string GetNext(int slot) => nextGuid;
    public override void SetNext(int slot, string targetGuid) => nextGuid = targetGuid;
}

/// <summary>One answer the player can pick inside a Choice node.</summary>
[Serializable]
public class DialogueChoiceOption
{
    public string text = "New option";
    public List<DialogueCondition> conditions = new List<DialogueCondition>();  // shown only if all pass
    public bool hideWhenUnavailable = true;                                     // false = show greyed out
    public List<DialogueEffect> effects = new List<DialogueEffect>();           // applied when picked
    public string nextGuid;
}

/// <summary>Shows options to the player; each option leads to a different node.</summary>
[Serializable]
public class DialogueChoice : DialogueNode
{
    public List<DialogueChoiceOption> options = new List<DialogueChoiceOption>();

    public override int OutputCount => options.Count;

    public override string GetNext(int slot) =>
        slot >= 0 && slot < options.Count ? options[slot].nextGuid : null;

    public override void SetNext(int slot, string targetGuid)
    {
        if (slot >= 0 && slot < options.Count) options[slot].nextGuid = targetGuid;
    }
}

/// <summary>
/// Invisible automatic branch: if all conditions pass go "True", otherwise go "False".
/// Use it to route the conversation without asking the player.
/// </summary>
[Serializable]
public class DialogueConditionNode : DialogueNode
{
    public List<DialogueCondition> conditions = new List<DialogueCondition>();
    public string trueGuid;
    public string falseGuid;

    public override int OutputCount => 2;
    public override string GetNext(int slot) => slot == 0 ? trueGuid : falseGuid;

    public override void SetNext(int slot, string targetGuid)
    {
        if (slot == 0) trueGuid = targetGuid;
        else falseGuid = targetGuid;
    }
}

/// <summary>
/// Closes the conversation. Can change state and raise a named event
/// (e.g. "open_shop", "give_quest") for the rest of your game to react to.
/// </summary>
[Serializable]
public class DialogueEnd : DialogueNode
{
    public string eventId;
    public List<DialogueEffect> effects = new List<DialogueEffect>();

    public override int OutputCount => 0;
    public override string GetNext(int slot) => null;
    public override void SetNext(int slot, string targetGuid) { }
}
