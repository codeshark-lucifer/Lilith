using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The visual block for one DialogueNode in the graph editor.
/// It edits the data object directly; links are written back by DialogueGraphView.SyncToAsset().
/// Each output port carries its slot number in port.userData.
/// </summary>
public class DialogueEditorNode : Node
{
    public readonly DialogueNode Data;
    public Port Input { get; private set; }
    public readonly List<Port> Outputs = new List<Port>();

    private readonly DialogueGraphView view;
    private VisualElement optionsRoot;      // Choice nodes only

    public DialogueEditorNode(DialogueNode data, DialogueGraphView graphView)
    {
        Data = data;
        view = graphView;

        style.width = 340;
        title = TitleFor(data);
        titleContainer.style.backgroundColor = ColorFor(data);

        if (!(data is DialogueStart))
        {
            Input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            Input.portName = "In";
            inputContainer.Add(Input);
        }

        RebuildOutputs();

        extensionContainer.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f, 0.95f);
        extensionContainer.style.paddingLeft = 6;
        extensionContainer.style.paddingRight = 6;
        extensionContainer.style.paddingBottom = 6;
        BuildBody();

        RefreshExpandedState();
        RefreshPorts();
    }

    // ---- ports -----------------------------------------------------------------------------

    /// <summary>(Re)creates one output port per output slot. Edges must be reconnected by the caller.</summary>
    public void RebuildOutputs()
    {
        outputContainer.Clear();
        Outputs.Clear();

        for (int slot = 0; slot < Data.OutputCount; slot++)
        {
            Port port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            port.userData = slot;
            port.portName = OutputLabel(slot);
            outputContainer.Add(port);
            Outputs.Add(port);
        }

        RefreshPorts();
        RefreshExpandedState();
    }

    private string OutputLabel(int slot)
    {
        switch (Data)
        {
            case DialogueChoice choice: return Shorten(choice.options[slot].text);
            case DialogueConditionNode _: return slot == 0 ? "True" : "False";
            default: return "Next";
        }
    }

    private static string Shorten(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "(empty)";
        text = text.Replace("\n", " ");
        return text.Length <= 22 ? text : text.Substring(0, 21) + "...";
    }

    // ---- body ------------------------------------------------------------------------------

    private void BuildBody()
    {
        switch (Data)
        {
            case DialogueStart start:
            {
                var priority = new IntegerField("Priority")
                {
                    value = start.priority,
                    tooltip = "When several Start nodes have passing conditions, the highest priority wins."
                };
                priority.RegisterValueChangedCallback(ev => { start.priority = ev.newValue; view.NotifyChanged(); });
                extensionContainer.Add(priority);
                extensionContainer.Add(DialogueRuleUI.ConditionList(
                    "BEGIN HERE ONLY IF (all must pass)", start.conditions, view.NotifyChanged));
                break;
            }

            case DialogueLine line:
            {
                var speaker = new TextField("Speaker") { value = line.speaker };
                speaker.RegisterValueChangedCallback(ev => { line.speaker = ev.newValue; view.NotifyChanged(); });
                extensionContainer.Add(speaker);

                var text = new TextField { value = line.text, multiline = true };
                text.style.minHeight = 60;
                text.style.whiteSpace = WhiteSpace.Normal;
                text.RegisterValueChangedCallback(ev => { line.text = ev.newValue; view.NotifyChanged(); });
                extensionContainer.Add(text);

                extensionContainer.Add(DialogueRuleUI.EffectList(
                    "WHEN REACHED, CHANGE", line.effects, view.NotifyChanged));
                break;
            }

            case DialogueChoice choice:
                BuildChoiceBody(choice);
                break;

            case DialogueConditionNode branch:
                extensionContainer.Add(DialogueRuleUI.ConditionList(
                    "GO \"TRUE\" IF (all must pass), ELSE \"FALSE\"", branch.conditions, view.NotifyChanged));
                break;

            case DialogueEnd end:
            {
                var eventField = new TextField("Event id")
                {
                    value = end.eventId,
                    tooltip = "Optional. Raised when the conversation ends, e.g. open_shop or give_quest."
                };
                eventField.RegisterValueChangedCallback(ev => { end.eventId = ev.newValue; view.NotifyChanged(); });
                extensionContainer.Add(eventField);

                extensionContainer.Add(DialogueRuleUI.EffectList(
                    "WHEN REACHED, CHANGE", end.effects, view.NotifyChanged));
                break;
            }
        }
    }

    private void BuildChoiceBody(DialogueChoice choice)
    {
        optionsRoot = new VisualElement();
        extensionContainer.Add(optionsRoot);

        var add = new Button(() =>
        {
            view.SyncToAsset();                                   // make sure links are saved before the ports change
            choice.options.Add(new DialogueChoiceOption { text = "New option" });
            RebuildOptionsUI(choice);
            view.RefreshNode(this);
        }) { text = "+ Add option" };
        add.style.marginTop = 4;
        extensionContainer.Add(add);

        RebuildOptionsUI(choice);
    }

    private void RebuildOptionsUI(DialogueChoice choice)
    {
        optionsRoot.Clear();

        for (int i = 0; i < choice.options.Count; i++)
        {
            int index = i;
            DialogueChoiceOption option = choice.options[i];

            var box = new VisualElement();
            box.style.marginTop = 4;
            box.style.paddingBottom = 3;
            box.style.borderBottomWidth = 1;
            box.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var number = new Label((i + 1) + ".");
            number.style.width = 18;
            row.Add(number);

            var text = new TextField { value = option.text };
            text.style.flexGrow = 1;
            text.RegisterValueChangedCallback(ev =>
            {
                option.text = ev.newValue;
                if (index < Outputs.Count) Outputs[index].portName = Shorten(option.text);
                view.NotifyChanged();
            });
            row.Add(text);

            var remove = new Button(() =>
            {
                // run after the click finishes, because this rebuilds the very button being clicked
                schedule.Execute(() =>
                {
                    view.SyncToAsset();
                    choice.options.RemoveAt(index);
                    RebuildOptionsUI(choice);
                    view.RefreshNode(this);
                });
            }) { text = "x", tooltip = "Remove this option" };
            remove.style.width = 20;
            remove.style.marginLeft = 2;
            row.Add(remove);

            box.Add(row);

            var rules = new Foldout { text = "Conditions & effects", value = false };
            rules.Add(DialogueRuleUI.ConditionList("SHOW THIS OPTION ONLY IF", option.conditions, view.NotifyChanged));

            var hide = new Toggle("Hide if locked")
            {
                value = option.hideWhenUnavailable,
                tooltip = "Off = the option stays visible but greyed out when its conditions fail."
            };
            hide.RegisterValueChangedCallback(ev => { option.hideWhenUnavailable = ev.newValue; view.NotifyChanged(); });
            rules.Add(hide);

            rules.Add(DialogueRuleUI.EffectList("WHEN PICKED, CHANGE", option.effects, view.NotifyChanged));
            box.Add(rules);

            optionsRoot.Add(box);
        }
    }

    // ---- look ------------------------------------------------------------------------------

    private static string TitleFor(DialogueNode data)
    {
        switch (data)
        {
            case DialogueStart _:         return "Start";
            case DialogueLine _:          return "Line";
            case DialogueChoice _:        return "Choice";
            case DialogueConditionNode _: return "Condition";
            case DialogueEnd _:           return "End";
            default:                      return data.GetType().Name;
        }
    }

    private static Color ColorFor(DialogueNode data)
    {
        switch (data)
        {
            case DialogueStart _:         return new Color(0.20f, 0.45f, 0.25f);
            case DialogueLine _:          return new Color(0.22f, 0.35f, 0.55f);
            case DialogueChoice _:        return new Color(0.45f, 0.30f, 0.55f);
            case DialogueConditionNode _: return new Color(0.60f, 0.40f, 0.15f);
            case DialogueEnd _:           return new Color(0.55f, 0.22f, 0.22f);
            default:                      return new Color(0.3f, 0.3f, 0.3f);
        }
    }
}
