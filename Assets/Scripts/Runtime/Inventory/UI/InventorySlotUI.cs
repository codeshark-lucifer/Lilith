using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private CanvasGroup iconCanvasGroup;
    [SerializeField] private TextMeshProUGUI quantityText;

    private Inventory inventory;
    private int index;
    private bool hasItem;

    private GameObject dragGhost;
    private RectTransform canvasRect;
    private Canvas rootCanvas;

    public void Initialize(Inventory _inventory, int _index)
    {
        inventory = _inventory;
        index = _index;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
        if (rootCanvas == null || iconImage == null || iconCanvasGroup == null || quantityText == null)
        {
            Debug.LogError("InventorySlotUI requires a parent Canvas, icon Image, icon CanvasGroup, and quantity Text.", this);
            return;
        }

        canvasRect = rootCanvas.transform as RectTransform;
        iconImage.raycastTarget = false; // let the slot root receive pointer events
        ClearSlot();
    }

    public void UpdateSlot(ItemContainer container)
    {
        if (container.Data != null && container.Quantity > 0)
        {
            hasItem = true;
            iconImage.sprite = container.Data.icon;
            iconImage.gameObject.SetActive(true);
            iconCanvasGroup.alpha = 1f;
            quantityText.text = container.Quantity > 1 ? container.Quantity.ToString() : "";
        }
        else
        {
            ClearSlot();
        }
    }

    public void ClearSlot()
    {
        hasItem = false;
        iconImage.sprite = null;
        iconImage.gameObject.SetActive(false);
        iconCanvasGroup.alpha = 1f;
        quantityText.text = "";
    }

    #region Drag and Drop

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!hasItem || eventData.button != PointerEventData.InputButton.Left) return;

        // Ghost icon that follows the cursor
        dragGhost = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        dragGhost.transform.SetParent(rootCanvas.transform, false);
        dragGhost.transform.SetAsLastSibling(); // draw on top of everything

        var ghostImage = dragGhost.GetComponent<Image>();
        ghostImage.sprite = iconImage.sprite;
        ghostImage.raycastTarget = false;
        dragGhost.GetComponent<CanvasGroup>().blocksRaycasts = false;

        var ghostRect = dragGhost.GetComponent<RectTransform>();
        ghostRect.sizeDelta = iconImage.rectTransform.rect.size;

        iconCanvasGroup.alpha = 0.4f; // dim the original while dragging
        MoveGhost(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        if (dragGhost == null) return;
        MoveGhost(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragGhost != null) Destroy(dragGhost);
        dragGhost = null;

        // Restore visuals (if a move happened, OnChanged already refreshed us)
        if (hasItem) iconCanvasGroup.alpha = 1f;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (inventory == null) return;

        var source = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<InventorySlotUI>()
            : null;
        if (source == null) return;

        inventory.MoveItem(source.index, index);
    }

    private void MoveGhost(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            canvasRect, eventData.position, eventData.pressEventCamera, out Vector3 worldPos);
        dragGhost.transform.position = worldPos;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right || !hasItem) return;

        ItemContainer c = inventory.containers[index];
        var options = new List<(string, Action)>();

        if (c.Quantity > 1)
            options.Add(("Split", () => inventory.SplitStack(index, c.Quantity / 2)));

        options.Add(("Drop One", () => inventory.DropFromSlot(index, 1)));

        if (c.Quantity > 1)
            options.Add(("Drop All", () => inventory.DropFromSlot(index, c.Quantity)));

        if (InventoryContextMenu.Instance != null)
            InventoryContextMenu.Instance.Show(options, eventData.position);
    }

    #endregion
}