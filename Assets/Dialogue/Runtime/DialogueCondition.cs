using System;
using System.Collections.Generic;

public enum ConditionKind { Flag, Number }
public enum ConditionOp { Equal, NotEqual, Greater, GreaterOrEqual, Less, LessOrEqual }

/// <summary>
/// One yes/no question about the saved state, e.g.
///   flag   helped_blacksmith == true
///   number affection.mira    >= 3
/// A condition with an empty key is ignored (counts as met).
/// </summary>
[Serializable]
public class DialogueCondition
{
    public string key;
    public ConditionKind kind = ConditionKind.Flag;
    public ConditionOp op = ConditionOp.Equal;
    public bool flagValue = true;
    public int numberValue;

    public bool Evaluate(DialogueState state)
    {
        if (state == null || string.IsNullOrEmpty(key)) return true;

        if (kind == ConditionKind.Flag)
        {
            bool actual = state.GetFlag(key);
            return op == ConditionOp.NotEqual ? actual != flagValue : actual == flagValue;
        }

        int n = state.GetNumber(key);
        switch (op)
        {
            case ConditionOp.Equal:          return n == numberValue;
            case ConditionOp.NotEqual:       return n != numberValue;
            case ConditionOp.Greater:        return n > numberValue;
            case ConditionOp.GreaterOrEqual: return n >= numberValue;
            case ConditionOp.Less:           return n < numberValue;
            case ConditionOp.LessOrEqual:    return n <= numberValue;
            default:                         return true;
        }
    }

    /// <summary>True only if every condition passes (an empty or null list passes).</summary>
    public static bool AllMet(List<DialogueCondition> list, DialogueState state)
    {
        if (list == null) return true;
        for (int i = 0; i < list.Count; i++)
            if (list[i] != null && !list[i].Evaluate(state)) return false;
        return true;
    }
}
