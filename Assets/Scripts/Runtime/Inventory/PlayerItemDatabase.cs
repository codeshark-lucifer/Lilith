using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerItemDatabase
{
    private readonly List<ItemDataSO> items = new List<ItemDataSO>();

    public bool HasItem(ItemDataSO item)
    {
        return item != null && items.Contains(item);
    }

    public void AddItem(ItemDataSO item)
    {
        if (item != null && !items.Contains(item))
        {
            items.Add(item);
        }
    }       
}
