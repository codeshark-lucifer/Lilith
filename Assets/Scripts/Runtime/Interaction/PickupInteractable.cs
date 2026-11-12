using UnityEngine;

/// <summary>
/// A world item the player can pick up. Replace the TODO with a call to your
/// inventory system once you have one.
/// </summary>
public class PickupInteractable : InteractableBase
{
    [Header("Item")]
    public ItemDataSO data;
    public int amount = 1;

    private void Awake()
    {
        singleUse = true;
        if (data != null) prompt = $"Pick up {data.displayName}";
    }

    public void Setup(ItemDataSO newData, int newAmount)
    {
        data = newData;
        amount = newAmount;
        prompt = $"Pick up {data.displayName}";
    }

    public override void Interact(PlayerInteractor interactor)
    {
        PlayerInventory inventory = interactor != null
            ? interactor.GetComponentInParent<PlayerInventory>()
            : null;
        if (inventory == null) return;

        int leftover = inventory.Add(data, amount);
        int added = amount - leftover;

        if (added <= 0)
        {
            Debug.Log("Inventory full");   // hook up a UI message here
            return;
        }

        if (leftover > 0)
        {
            // Partial pickup: take what fit, leave the rest in the world
            amount = leftover;
            Debug.Log($"Picked up {added}x {data.displayName}, {leftover} left behind");
            return;
        }

        base.Interact(interactor);
        // Debug.Log($"Picked up {added}x {data.displayName} ({data.Id})");
        Destroy(gameObject);
    }
}