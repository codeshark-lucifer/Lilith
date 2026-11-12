using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectiveManager : Singleton<ObjectiveManager>
{
    [SerializeField] private ObjectiveData startingObjective;

    public ObjectiveData Current { get; private set; }

    /// <summary>Fired when the active objective changes (null = no objective).</summary>
    public event Action<ObjectiveData> ObjectiveChanged;
    public event Action<ObjectiveData> ObjectiveCompleted;

    private void Start()
    {
        if (startingObjective != null) SetObjective(startingObjective);
    }

    public void SetObjective(ObjectiveData objective)
    {
        Current = objective;
        if (objective != null) RunEffects(objective.onStartEffects, objective);
        ObjectiveChanged?.Invoke(objective);
    }

    /// <summary>Call this from gameplay (trigger zones, quest items, etc).</summary>
    public void CompleteCurrent()
    {
        if (Current == null) return;

        ObjectiveData done = Current;
        RunEffects(done.onCompleteEffects, done);
        ObjectiveCompleted?.Invoke(done);

        SetObjective(done.nextObjective); // null clears it
    }

    private static void RunEffects(List<ObjectiveEffect> effects, ObjectiveData objective)
    {
        foreach (var effect in effects)
            if (effect != null) effect.Execute(objective);
    }
}