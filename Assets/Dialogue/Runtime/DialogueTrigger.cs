using UnityEngine;

/// <summary>
/// Put on an NPC / object. Call Begin() from your interaction code, a UnityEvent or a UI button,
/// or tick "Start On Trigger Enter" and give the object a trigger collider.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueGraphSO dialogue;

    [Header("Optional automatic start")]
    [SerializeField] private bool startOnTriggerEnter;
    [SerializeField] private string requiredTag = "Player";

    public void Begin()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogError("[Dialogue] No DialogueManager in the scene.");
            return;
        }
        DialogueManager.Instance.StartDialogue(dialogue);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (startOnTriggerEnter && other.CompareTag(requiredTag)) Begin();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (startOnTriggerEnter && other.CompareTag(requiredTag)) Begin();
    }
}
