using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory
{

    public List<ItemContainer> containers;
    public int capacity;
    public event Action OnChanged;
    public event Action<ItemDataSO, int> ItemDropped;


    public Inventory(int size = 64)
    {
        capacity = size;
        containers = new List<ItemContainer>(capacity);

        // Initialize the inventory with empty containers up to capacity
        for (int i = 0; i < capacity; i++)
        {
            containers.Add(new ItemContainer());
        }
    }

    public int AddItem(ItemDataSO data, int amount)
    {
        int remainingAmount = amount;

        // Step 1: Fill existing stacks of the same item first
        foreach (var container in containers)
        {
            if (remainingAmount <= 0) break;

            if (container.Data == data && container.Quantity < data.stackSize)
            {
                int spaceInSlot = data.stackSize - container.Quantity;
                int addAmount = Mathf.Min(remainingAmount, spaceInSlot);

                container.Quantity += addAmount;
                remainingAmount -= addAmount;
            }
        }

        // Step 2: If there's still items left, put them into empty slots
        foreach (var container in containers)
        {
            if (remainingAmount <= 0) break;

            if (container.Data == null)
            {
                container.Data = data;
                int addAmount = Mathf.Min(remainingAmount, data.stackSize);

                container.Quantity = addAmount;
                remainingAmount -= addAmount;
            }
        }

        if (remainingAmount > 0)
        {
            Debug.LogWarning($"Inventory is full! Could not add {remainingAmount} of {data.displayName}.");
        }

        if (remainingAmount < amount) OnChanged?.Invoke();
        return remainingAmount; // 0 means everything fit
    }


    public void RemoveItem(ItemDataSO data, int amount)
    {
        int remainingToRemove = amount;

        // Iterate backwards so we remove from the last slots first (common convention)
        for (int i = containers.Count - 1; i >= 0; i--)
        {
            if (remainingToRemove <= 0) break;

            var container = containers[i];
            if (container.Data == data)
            {
                int removeAmount = Mathf.Min(remainingToRemove, container.Quantity);
                container.Quantity -= removeAmount;
                remainingToRemove -= removeAmount;

                // Clear slot data if the quantity hits zero
                if (container.Quantity <= 0)
                {
                    container.Data = null;
                    container.Quantity = 0;
                }
            }
        }

        if (remainingToRemove > 0)
        {
            Debug.LogWarning($"Tried to remove more {data.name} than available in inventory.");
        }

        OnChanged?.Invoke();
    }

    public bool HasItem(ItemDataSO data, int amount = 1)
    {
        if (data == null || amount <= 0) return false;

        int total = 0;
        foreach (ItemContainer container in containers)
        {
            if (container.Data == data)
                total += container.Quantity;
        }

        return total >= amount;
    }

    public bool TryConsumeItem(ItemDataSO data, int amount = 1)
    {
        if (!HasItem(data, amount)) return false;

        int remaining = amount;
        for (int i = containers.Count - 1; i >= 0 && remaining > 0; i--)
        {
            ItemContainer container = containers[i];
            if (container.Data != data) continue;

            int taken = Mathf.Min(container.Quantity, remaining);
            container.Quantity -= taken;
            remaining -= taken;

            if (container.Quantity <= 0)
            {
                container.Data = null;
                container.Quantity = 0;
            }
        }

        OnChanged?.Invoke();
        return true;
    }

    // At the end of AddItem and RemoveItem, add:
    //     OnChanged?.Invoke();

    public bool Contains(ItemDataSO data) => HasItem(data, 1);

    public void MoveItem(int from, int to)
    {
        if (from == to) return;
        if (from < 0 || to < 0 || from >= containers.Count || to >= containers.Count) return;

        var source = containers[from];
        var target = containers[to];
        if (source.Data == null) return;

        if (target.Data == source.Data)
        {
            // Same item: merge as much as fits
            int space = target.Data.stackSize - target.Quantity;
            int moved = Mathf.Min(space, source.Quantity);
            target.Quantity += moved;
            source.Quantity -= moved;
            if (source.Quantity <= 0) source.Clear();
        }
        else
        {
            // Empty target or different item: swap
            (source.Data, target.Data) = (target.Data, source.Data);
            (source.Quantity, target.Quantity) = (target.Quantity, source.Quantity);
        }

        OnChanged?.Invoke();
    }


    /// <summary>Moves 'amount' from this slot into the first empty slot.</summary>
    public bool SplitStack(int index, int amount)
    {
        var src = containers[index];
        if (src.Data == null || amount <= 0 || amount >= src.Quantity) return false;

        int empty = containers.FindIndex(c => c.Data == null);
        if (empty < 0) return false; // no free slot

        containers[empty].Data = src.Data;
        containers[empty].Quantity = amount;
        src.Quantity -= amount;

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Removes items from a slot and announces them so the world can spawn them.</summary>
    public void DropFromSlot(int index, int amount)
    {
        var c = containers[index];
        if (c.Data == null || amount <= 0) return;

        amount = Mathf.Min(amount, c.Quantity);
        ItemDataSO data = c.Data;

        c.Quantity -= amount;
        if (c.Quantity <= 0) c.Clear();

        ItemDropped?.Invoke(data, amount);
        OnChanged?.Invoke();
    }
}