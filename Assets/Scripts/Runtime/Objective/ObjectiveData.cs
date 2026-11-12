using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Objective", fileName = "NewObjective")]
public class ObjectiveData : ScriptableObject
{
    [Header("Display")]
    public string title = "New Objective";
    [TextArea(2, 5)] public string description;
    public Sprite icon; // optional

    [Header("Flow")]
    [Tooltip("Set automatically after this objective is completed. Leave empty to end the chain.")]
    public ObjectiveData nextObjective;

    [Header("Effects")]
    public List<ObjectiveEffect> onStartEffects = new();
    public List<ObjectiveEffect> onCompleteEffects = new();
}