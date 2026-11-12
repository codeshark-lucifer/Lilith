using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One option as the UI should show it.</summary>
public readonly struct DialogueChoiceView
{
    /// <summary>Pass this to DialogueManager.Choose().</summary>
    public readonly int Index;
    public readonly string Text;
    /// <summary>False when the option's conditions fail but it is set to show greyed out.</summary>
    public readonly bool Available;

    public DialogueChoiceView(int index, string text, bool available)
    {
        Index = index;
        Text = text;
        Available = available;
    }
}

/// <summary>
/// Runs a DialogueGraphSO against the shared DialogueState and tells the UI what to show.
/// Put one in your first scene. Other code reaches it through DialogueManager.Instance.
/// </summary>
[DefaultExecutionOrder(-100)]
public class DialogueManager : MonoBehaviour
{
    private const int MaxAutomaticSteps = 500;

    public static DialogueManager Instance { get; private set; }

    [Header("State")]
    [Tooltip("Read dialogue_state.json when the game starts.")]
    [SerializeField] private bool autoLoadState = true;
    [Tooltip("Write dialogue_state.json every time a conversation ends.")]
    [SerializeField] private bool autoSaveOnEnd = true;

    [Header("Behaviour")]
    [Tooltip("When a Line leads straight into a Choice, show the options together with the line.")]
    [SerializeField] private bool showChoicesWithLine = true;
    [Tooltip("Keep this object between scenes (it must be a root object).")]
    [SerializeField] private bool persistAcrossScenes = true;

    // ---- what the UI listens to ------------------------------------------------
    public event Action<DialogueGraphSO> OnDialogueStarted;
    public event Action<DialogueLine> OnLine;
    public event Action<List<DialogueChoiceView>> OnChoices;
    public event Action OnDialogueEnded;
    /// <summary>Raised after the conversation closes when its End node has an event id (open_shop, give_quest...).</summary>
    public event Action<string> OnDialogueEvent;

    public DialogueState State { get; private set; }
    public DialogueGraphSO CurrentGraph { get; private set; }
    public DialogueNode CurrentNode { get; private set; }
    public bool IsRunning => CurrentGraph != null;

    private enum WaitMode { None, Line, Choice }
    private WaitMode waiting = WaitMode.None;

    // ---------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistAcrossScenes && transform.parent == null) DontDestroyOnLoad(gameObject);

        State = autoLoadState ? DialogueStateStore.Load() : new DialogueState();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---- public API --------------------------------------------------------------

    /// <summary>Starts a conversation. Returns false if one is already running or no Start node's conditions pass.</summary>
    public bool StartDialogue(DialogueGraphSO graph)
    {
        if (graph == null)
        {
            Debug.LogWarning("[Dialogue] StartDialogue was called without a graph.");
            return false;
        }
        if (IsRunning)
        {
            Debug.LogWarning("[Dialogue] A conversation is already running.");
            return false;
        }

        DialogueStart entry = graph.FindEntry(State);
        if (entry == null)
        {
            Debug.LogWarning($"[Dialogue] '{graph.name}' has no Start node whose conditions are met.");
            return false;
        }

        CurrentGraph = graph;
        waiting = WaitMode.None;
        OnDialogueStarted?.Invoke(graph);
        Process(entry);
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameState(GameState.Dialogue);
        return true;
    }

    /// <summary>The player pressed "continue" on a line.</summary>
    public void Advance()
    {
        if (!IsRunning || waiting != WaitMode.Line) return;

        DialogueNode next = Follow(CurrentNode, 0);
        if (next == null) Finish(null);
        else Process(next);
    }

    /// <summary>The player picked an option. Use DialogueChoiceView.Index.</summary>
    public void Choose(int optionIndex)
    {
        if (!IsRunning || waiting != WaitMode.Choice) return;
        if (!(CurrentNode is DialogueChoice choice)) return;
        if (optionIndex < 0 || optionIndex >= choice.options.Count) return;

        DialogueChoiceOption option = choice.options[optionIndex];
        if (!DialogueCondition.AllMet(option.conditions, State)) return;   // locked option

        DialogueEffect.ApplyAll(option.effects, State);

        DialogueNode next = CurrentGraph.GetNode(option.nextGuid);
        if (next == null) Finish(null);
        else Process(next);
    }

    /// <summary>Close the conversation right now (player walked away, cutscene, etc.).</summary>
    public void EndDialogue() => Finish(null);

    public void Save() => DialogueStateStore.Save(State);

    public void Load() => State = DialogueStateStore.Load();

    /// <summary>Forget everything the player did, in memory and on disk.</summary>
    public void ResetState()
    {
        State.Clear();
        DialogueStateStore.Delete();
    }

    // ---- the engine --------------------------------------------------------------

    private DialogueNode Follow(DialogueNode node, int slot)
    {
        if (node == null || CurrentGraph == null) return null;
        return CurrentGraph.GetNode(node.GetNext(slot));
    }

    /// <summary>Walks the graph from this node until it reaches something that needs the player (a Line or a Choice) or the end.</summary>
    private void Process(DialogueNode node)
    {
        int steps = 0;

        while (node != null)
        {
            if (++steps > MaxAutomaticSteps)
            {
                Debug.LogError("[Dialogue] Too many automatic steps. Is there a loop with no Line or Choice in it?");
                Finish(null);
                return;
            }

            CurrentNode = node;

            switch (node)
            {
                case DialogueStart start:
                    node = Follow(start, 0);
                    break;

                case DialogueConditionNode branch:
                    node = Follow(branch, DialogueCondition.AllMet(branch.conditions, State) ? 0 : 1);
                    break;

                case DialogueLine line:
                {
                    DialogueEffect.ApplyAll(line.effects, State);
                    waiting = WaitMode.Line;
                    OnLine?.Invoke(line);
                    if (!IsRunning) return;     // a listener ended the conversation

                    DialogueNode following = Follow(line, 0);
                    if (showChoicesWithLine && following is DialogueChoice)
                    {
                        node = following;
                        break;
                    }
                    return;                     // wait for Advance()
                }

                case DialogueChoice choice:
                    ShowChoices(choice);
                    return;                     // wait for Choose()

                case DialogueEnd end:
                    DialogueEffect.ApplyAll(end.effects, State);
                    Finish(end.eventId);
                    return;

                default:
                    Finish(null);
                    return;
            }
        }

        // A node had nothing connected to its output: treat it as the end.
        Finish(null);
    }

    private void ShowChoices(DialogueChoice choice)
    {
        var views = new List<DialogueChoiceView>();
        bool anyAvailable = false;

        for (int i = 0; i < choice.options.Count; i++)
        {
            DialogueChoiceOption option = choice.options[i];
            bool available = DialogueCondition.AllMet(option.conditions, State);

            if (!available && option.hideWhenUnavailable) continue;

            anyAvailable |= available;
            views.Add(new DialogueChoiceView(i, option.text, available));
        }

        if (!anyAvailable)
        {
            Debug.LogWarning("[Dialogue] A Choice node has no options the player can pick. Ending the conversation.");
            Finish(null);
            return;
        }

        waiting = WaitMode.Choice;
        OnChoices?.Invoke(views);
    }

    private void Finish(string eventId)
    {
        if (!IsRunning) return;

        waiting = WaitMode.None;
        CurrentGraph = null;
        CurrentNode = null;

        if (autoSaveOnEnd) Save();

        OnDialogueEnded?.Invoke();
        if (!string.IsNullOrEmpty(eventId)) OnDialogueEvent?.Invoke(eventId);
        
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameState(GameState.Playing);
    }
}
