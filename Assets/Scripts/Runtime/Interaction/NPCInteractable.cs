using UnityEngine;

/// <summary>
/// An NPC you can talk to. Holds the NPC's identity and makes its head look at
/// the player while focused or in conversation.
/// Hook your dialogue system into <see cref="InteractableBase.onInteract"/>
/// (or override Interact) to start the conversation.
/// </summary>
public class NPCInteractable : InteractableBase
{
    [Header("NPC")]
    [SerializeField] private string speaker = "NPC";
    [SerializeField] private DialogueGraphSO dialogue;
    

    private bool inConversation;

    private void Awake()
    {
        prompt = $"Talk to {speaker}";
    }

    public override void Interact(PlayerInteractor interactor)
    {
        base.Interact(interactor);
        inConversation = true;
        DialogueManager.Instance.StartDialogue(dialogue);
    }

    /// <summary>Call this from your dialogue system when the conversation ends.</summary>
    public void EndConversation()
    {
        inConversation = false;
    }

    public override void OnFocus(PlayerInteractor interactor)
    {
        base.OnFocus(interactor);
    }

    public override void OnUnfocus(PlayerInteractor interactor)
    {
        base.OnUnfocus(interactor);
    }

}