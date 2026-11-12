using System;
using UnityEngine;

/// <summary>ScriptableObject event channel. Scene objects listen to it, effects raise it.</summary>
[CreateAssetMenu(menuName = "Game/Event Channel", fileName = "NewGameEvent")]
public class GameEventSO : ScriptableObject
{
    public event Action Raised;
    public void Raise() => Raised?.Invoke();
}

/// <summary>Plays a 2D sound (objective chime, etc).</summary>
[CreateAssetMenu(menuName = "Game/Objective Effects/Play Sound", fileName = "PlaySoundEffect")]
public class PlaySoundEffect : ObjectiveEffect
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;

    public override void Execute(ObjectiveData objective)
    {
        if (clip == null || Camera.main == null) return;
        AudioSource.PlayClipAtPoint(clip, Camera.main.transform.position, volume);
    }
}

/// <summary>
/// Raises an event channel. Anything in the scene (a door, spawner, light, etc.)
/// can subscribe to the same GameEventSO and react. This is how SOs affect scene objects.
/// </summary>
[CreateAssetMenu(menuName = "Game/Objective Effects/Raise Event", fileName = "RaiseEventEffect")]
public class RaiseEventEffect : ObjectiveEffect
{
    public GameEventSO gameEvent;

    public override void Execute(ObjectiveData objective)
    {
        if (gameEvent != null) gameEvent.Raise();
    }
}