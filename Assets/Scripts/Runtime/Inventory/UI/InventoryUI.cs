using System.Collections.Generic;
using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private Inventory inventory;

    public void Initialize(Inventory playerInventory)
    {
        if (inventory != null) inventory.OnChanged -= RefreshUI;
        inventory = playerInventory;
        if (inventory == null || slotParent == null || slotPrefab == null) return;
        inventory.OnChanged += RefreshUI;

        foreach (Transform child in slotParent) Destroy(child.gameObject);
        slotUIs.Clear();

        for (int i = 0; i < inventory.containers.Count; i++)
        {
            var slotObj = Instantiate(slotPrefab, slotParent);
            var slotUI = slotObj.GetComponent<InventorySlotUI>();
            slotUI.Initialize(inventory, i);
            slotUIs.Add(slotUI);
        }

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= RefreshUI;
    }

    public void RefreshUI()
    {
        if (inventory == null) return;

        for (int i = 0; i < inventory.containers.Count; i++)
        {
            if (i < slotUIs.Count)
                slotUIs[i].UpdateSlot(inventory.containers[i]);
        }
    }
}