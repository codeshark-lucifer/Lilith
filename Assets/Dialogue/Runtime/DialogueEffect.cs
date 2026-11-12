using System;
using System.Collections.Generic;

public enum EffectKind { SetFlag, SetNumber, AddNumber }

/// <summary>
/// One change to the saved state, applied when the player reaches a line / picks an option / hits an End node, e.g.
///   set flag   helped_blacksmith = true
///   add number affection.mira += 1
/// </summary>
[Serializable]
public class DialogueEffect
{
    public string key;
    public EffectKind kind = EffectKind.SetFlag;
    public bool flagValue = true;
    public int numberValue = 1;

    public void Apply(DialogueState state)
    {
        if (state == null || string.IsNullOrEmpty(key)) return;

        switch (kind)
        {
            case EffectKind.SetFlag:   state.SetFlag(key, flagValue); break;
            case EffectKind.SetNumber: state.SetNumber(key, numberValue); break;
            case EffectKind.AddNumber: state.AddNumber(key, numberValue); break;
        }
    }

    public static void ApplyAll(List<DialogueEffect> list, DialogueState state)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
            list[i]?.Apply(state);
    }
}
