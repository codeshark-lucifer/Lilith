using UnityEngine;

public class ItemContainer
{
    public int Quantity;
    public ItemDataSO Data;

    public void AddItem(ItemDataSO data, int amount)
    {
        // If the slot is empty, set the item data
        if (Data == null)
        {
            Data = data;
        }

        // Optional safety check: ensure we are adding the correct item type
        if (Data != data)
        {
            Debug.LogWarning("Trying to add a different item type to this container!");
            return;
        }

        // Calculate how much space is left in the stack
        int availableSpace = Data.stackSize - Quantity;

        // Add only as much as we can fit up to the stack size
        int addAmount = Mathf.Min(amount, availableSpace);

        Quantity += addAmount;

        // If you need to handle overflow (e.g., returning the leftover amount), 
        // you can calculate: int overflow = amount - addAmount;
    }

    public void Clear()
    {
        Data = null;
        Quantity = 0;
    }
}