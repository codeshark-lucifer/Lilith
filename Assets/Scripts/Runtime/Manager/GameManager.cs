using System;
using UnityEngine;

public enum GameState
{
    Playing,
    Paused,
    Inventory,
    Dialogue,
    ItemPreview,
    Cutscene
}
public class GameManager : Singleton<GameManager>
{
    [SerializeField] private GameState defaultGameState;
    public GameState CurrentGameState { get; private set; }
    public event Action<GameState> OnGameStateChanged;
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        SetGameState(defaultGameState);
    }

    public void SetGameState(GameState state)
    {
        CurrentGameState = state;
        OnGameStateChanged?.Invoke(state);
        switch (state)
        {
            case GameState.Playing:
            case GameState.Cutscene:
                SetCursorLocked(true);
                break;
            case GameState.Paused:
            case GameState.Inventory:
            case GameState.Dialogue:
            case GameState.ItemPreview:
                SetCursorLocked(false);
                break;
            default:
                break;
        }
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}