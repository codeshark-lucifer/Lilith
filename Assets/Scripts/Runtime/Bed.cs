using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.Audio;

public class Bed : InteractableBase
{
    private void Awake()
    {
        prompt = "Sleep";
    }

    public override void Interact(PlayerInteractor interactor)
    {
        base.Interact(interactor);
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
