using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

/// <summary>
/// Small UI Toolkit builders that edit lists of conditions / effects inside graph nodes.
/// They change the data objects directly and call "changed" so the asset gets marked dirty.
/// </summary>
public static class DialogueRuleUI
{
    private static readonly List<string> ConditionKinds = new List<string> { "flag", "number" };
    private static readonly List<string> Ops = new List<string> { "==", "!=", ">", ">=", "<", "<=" };
    private static readonly List<string> EffectKinds = new List<string> { "set flag", "set number", "add number" };

    private const string KeyTooltip = "Name of a flag or number, e.g. helped_blacksmith or affection.mira";

    // ---- conditions ------------------------------------------------------------------

    public static VisualElement ConditionList(string header, List<DialogueCondition> list, Action changed)
    {
        VisualElement root = Section(header);
        var rows = new VisualElement();
        root.Add(rows);

        void Rebuild()
        {
            rows.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                rows.Add(ConditionRow(
                    list[index],
                    () => { list.RemoveAt(index); changed(); Rebuild(); },
                    changed,
                    Rebuild));
            }
        }

        root.Add(SmallButton("+ condition", () => { list.Add(new DialogueCondition()); changed(); Rebuild(); }));
        Rebuild();
        return root;
    }

    private static VisualElement ConditionRow(DialogueCondition c, Action remove, Action changed, Action rebuild)
    {
        VisualElement row = Row();

        var key = new TextField { value = c.key, tooltip = KeyTooltip };
        key.style.flexGrow = 1;
        key.style.minWidth = 60;
        key.RegisterValueChangedCallback(ev => { c.key = ev.newValue; changed(); });
        row.Add(key);

        var kind = new PopupField<string>(ConditionKinds, (int)c.kind);
        kind.style.width = 64;
        kind.RegisterValueChangedCallback(ev =>
        {
            c.kind = (ConditionKind)ConditionKinds.IndexOf(ev.newValue);
            if (c.kind == ConditionKind.Flag) c.op = ConditionOp.Equal;
            changed();
            row.schedule.Execute(rebuild);
        });
        row.Add(kind);

        if (c.kind == ConditionKind.Number)
        {
            var op = new PopupField<string>(Ops, (int)c.op);
            op.style.width = 46;
            op.RegisterValueChangedCallback(ev => { c.op = (ConditionOp)Ops.IndexOf(ev.newValue); changed(); });
            row.Add(op);

            var number = new IntegerField { value = c.numberValue };
            number.style.width = 48;
            number.RegisterValueChangedCallback(ev => { c.numberValue = ev.newValue; changed(); });
            row.Add(number);
        }
        else
        {
            var toggle = new Toggle { value = c.flagValue, tooltip = "Checked = the flag must be true. Unchecked = must be false." };
            toggle.RegisterValueChangedCallback(ev => { c.flagValue = ev.newValue; changed(); });
            row.Add(toggle);
        }

        row.Add(RemoveButton(remove));
        return row;
    }

    // ---- effects ---------------------------------------------------------------------

    public static VisualElement EffectList(string header, List<DialogueEffect> list, Action changed)
    {
        VisualElement root = Section(header);
        var rows = new VisualElement();
        root.Add(rows);

        void Rebuild()
        {
            rows.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                rows.Add(EffectRow(
                    list[index],
                    () => { list.RemoveAt(index); changed(); Rebuild(); },
                    changed,
                    Rebuild));
            }
        }

        root.Add(SmallButton("+ effect", () => { list.Add(new DialogueEffect()); changed(); Rebuild(); }));
        Rebuild();
        return root;
    }

    private static VisualElement EffectRow(DialogueEffect fx, Action remove, Action changed, Action rebuild)
    {
        VisualElement row = Row();

        var key = new TextField { value = fx.key, tooltip = KeyTooltip };
        key.style.flexGrow = 1;
        key.style.minWidth = 60;
        key.RegisterValueChangedCallback(ev => { fx.key = ev.newValue; changed(); });
        row.Add(key);

        var kind = new PopupField<string>(EffectKinds, (int)fx.kind);
        kind.style.width = 94;
        kind.RegisterValueChangedCallback(ev =>
        {
            fx.kind = (EffectKind)EffectKinds.IndexOf(ev.newValue);
            changed();
            row.schedule.Execute(rebuild);
        });
        row.Add(kind);

        if (fx.kind == EffectKind.SetFlag)
        {
            var toggle = new Toggle { value = fx.flagValue, tooltip = "Checked = set the flag to true. Unchecked = set it to false." };
            toggle.RegisterValueChangedCallback(ev => { fx.flagValue = ev.newValue; changed(); });
            row.Add(toggle);
        }
        else
        {
            var number = new IntegerField { value = fx.numberValue };
            number.style.width = 48;
            number.RegisterValueChangedCallback(ev => { fx.numberValue = ev.newValue; changed(); });
            row.Add(number);
        }

        row.Add(RemoveButton(remove));
        return row;
    }

    // ---- small widgets -----------------------------------------------------------------

    private static VisualElement Section(string header)
    {
        var root = new VisualElement();
        root.style.marginTop = 4;

        var label = new Label(header);
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.fontSize = 10;
        label.style.opacity = 0.75f;
        root.Add(label);
        return root;
    }

    private static VisualElement Row()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 2;
        return row;
    }

    private static Button SmallButton(string text, Action onClick)
    {
        var button = new Button(onClick) { text = text };
        button.style.alignSelf = Align.FlexStart;
        button.style.fontSize = 10;
        return button;
    }

    private static Button RemoveButton(Action onClick)
    {
        var button = new Button(onClick) { text = "x", tooltip = "Remove" };
        button.style.width = 20;
        button.style.marginLeft = 2;
        return button;
    }
}
