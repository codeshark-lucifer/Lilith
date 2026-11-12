using UnityEngine;

public class TestDialogueEvent : MonoBehaviour
{
    [SerializeField] private string targetEventId = "test_event";
    [SerializeField] private string numberKey = "relationship"; // the key name from your DialogueEffect
    private DialogueManager manager;
    private bool subscribed;

    private void OnEnable()
    {
        Subscribe();
    }

    private void Start()
    {
        Subscribe();
    }

    private void Subscribe()
    {
        if (subscribed) return;

        manager = DialogueManager.Instance;
        if (manager == null) return;

        manager.OnDialogueEvent += HandleDialogueEvent;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && manager != null)
            manager.OnDialogueEvent -= HandleDialogueEvent;

        subscribed = false;
        manager = null;
    }

    private void HandleDialogueEvent(string eventId)
    {
        if (!string.Equals(eventId, targetEventId, System.StringComparison.Ordinal))
            return;

        if (manager == null || manager.State == null) return;

        int value = manager.State.GetNumber(numberKey);
        Debug.Log($"[TestDialogueEvent] Event: {eventId}, {numberKey} = {value}");
    }
}