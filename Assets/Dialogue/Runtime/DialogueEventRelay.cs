using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Turns the event id of a Dialogue End node into UnityEvents you can wire in the Inspector.
/// Example: eventId "open_shop" -> ShopUI.Open().  Put it on any always-active object.
/// </summary>
public class DialogueEventRelay : MonoBehaviour
{
    [Serializable]
    public class Binding
    {
        public string eventId;
        public UnityEvent onFired;
    }

    [SerializeField] private List<Binding> bindings = new List<Binding>();

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

        manager.OnDialogueEvent += Handle;
        subscribed = true;
    }

    private void OnDisable()
    {
        if (subscribed && manager != null) manager.OnDialogueEvent -= Handle;
        subscribed = false;
        manager = null;
    }

    private void Handle(string eventId)
    {
        foreach (Binding binding in bindings)
            if (binding.eventId == eventId) binding.onFired?.Invoke();
    }
}
