using UnityEngine;

/// <summary>
/// Anything the player can look at and press the interact key on:
/// NPCs, pickups, bugs/viruses, doors, terminals...
/// </summary>
public interface IInteractable
{
    /// <summary>Text shown in the HUD, e.g. "Talk to Bob".</summary>
    string GetPrompt(PlayerInteractor interactor);

    /// <summary>World position where the floating prompt should appear.</summary>
    Vector3 GetPromptWorldPosition();

    /// <summary>False = prompt is hidden and Interact() is ignored.</summary>
    bool CanInteract(PlayerInteractor interactor);

    /// <summary>Called when the player presses the interact key.</summary>
    void Interact(PlayerInteractor interactor);

    /// <summary>Player's crosshair started pointing at this object.</summary>
    void OnFocus(PlayerInteractor interactor);

    /// <summary>Player's crosshair stopped pointing at this object.</summary>
    void OnUnfocus(PlayerInteractor interactor);
}