using UnityEngine;

/// <summary>
/// Base class for anything that can happen when an objective starts or completes.
/// Create new effects by inheriting from this and adding a CreateAssetMenu attribute.
/// </summary>
public abstract class ObjectiveEffect : ScriptableObject
{
    public abstract void Execute(ObjectiveData objective);
}