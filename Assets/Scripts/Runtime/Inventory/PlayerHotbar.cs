using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Shows a range of the player's inventory (startIndex .. startIndex + slotCount - 1)
/// as a hotbar. Keys 1-4 (and optionally the scroll wheel) select the active slot,
/// and the selection frame slides smoothly between slots.
/// The whole hotbar fades out when idle and fades back in on any selection input.
/// </summary>
public class PlayerHotbar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private Transform slotParent;

    [Header("Range")]
    [SerializeField] private int startIndex = 0;
    [Tooltip("Max 9 when using number keys.")]
    [SerializeField][Range(1, 9)] private int slotCount = 4;

    [Header("Selection")]
    [Tooltip("An Image (RectTransform) used as the selection frame. Must NOT be inside the layout group.")]
    [SerializeField] private RectTransform selectionHighlight;
    [Tooltip("How fast the frame slides. Higher = snappier. 0 = instant.")]
    [SerializeField] private float moveSpeed = 18f;
    [SerializeField] private bool useScrollWheel = true;

    [Header("Idle Fade")]
    [Tooltip("CanvasGroup on an object that contains BOTH the slots and the selection frame. " +
             "If empty, one is found/added on the slot parent's parent.")]
    [SerializeField] private CanvasGroup hotbarGroup;
    [SerializeField] private bool fadeWhenIdle = true;
    [Tooltip("Seconds without input before the hotbar starts fading out.")]
    [SerializeField] private float idleDelay = 2f;
    [Tooltip("Alpha while idle. 0 = fully hidden.")]
    [SerializeField, Range(0f, 1f)] private float idleAlpha = 0f;
    [Tooltip("Alpha units per second when fading in.")]
    [SerializeField] private float fadeInSpeed = 8f;
    [Tooltip("Alpha units per second when fading out.")]
    [SerializeField] private float fadeOutSpeed = 2f;
    [Tooltip("Also wake the hotbar when an item in the hotbar range changes (pickup, use, etc.).")]
    [SerializeField] private bool wakeOnInventoryChange = true;

    private readonly List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();
    private readonly List<InputAction> numberActions = new List<InputAction>();
    private InputAction scrollAction;

    private Inventory inventory;
    private int selectedIndex;
    private bool snapHighlight = true;
    private float lastInputTime;

    public int SelectedIndex => selectedIndex;
    public ItemContainer SelectedContainer => inventory != null && startIndex >= 0 && selectedIndex >= 0 &&
        startIndex + selectedIndex < inventory.containers.Count
        ? inventory.containers[startIndex + selectedIndex]
        : null;

    /// <summary>Fired when the selected hotbar slot changes (0-based index).</summary>
    public event System.Action<int> SelectionChanged;

    private void Awake()
    {
        for (int i = 0; i < slotCount; i++)
        {
            int slot = i;
            var action = new InputAction($"Hotbar{i + 1}", InputActionType.Button, $"<Keyboard>/{i + 1}");
            action.performed += _ => Select(slot);
            numberActions.Add(action);
        }

        if (useScrollWheel)
        {
            scrollAction = new InputAction("HotbarScroll", InputActionType.Value, "<Mouse>/scroll/y");
            scrollAction.performed += ctx =>
            {
                float v = ctx.ReadValue<float>();
                if (v > 0.01f) Cycle(-1);
                else if (v < -0.01f) Cycle(1);
            };
        }

        SetupHighlight();
        SetupFadeGroup();
    }

    private void OnEnable()
    {
        foreach (var a in numberActions) a.Enable();
        scrollAction?.Enable();
    }

    private void OnDisable()
    {
        foreach (var a in numberActions) a.Disable();
        scrollAction?.Disable();
    }

    private void Start()
    {
        if (playerInventory == null || playerInventory.Inventory == null || slotParent == null || slotPrefab == null)
        {
            Debug.LogError("PlayerHotbar requires PlayerInventory, slot parent, and slot prefab references.", this);
            enabled = false;
            return;
        }

        inventory = playerInventory.Inventory;
        startIndex = Mathf.Clamp(startIndex, 0, inventory.containers.Count);
        slotCount = Mathf.Clamp(slotCount, 0, inventory.containers.Count - startIndex);

        foreach (Transform child in slotParent) Destroy(child.gameObject);
        slotUIs.Clear();

        for (int i = 0; i < slotCount; i++)
        {
            InventorySlotUI slot = Instantiate(slotPrefab, slotParent);
            slot.Initialize(inventory, startIndex + i);
            slotUIs.Add(slot);
        }

        inventory.OnChanged += OnInventoryChanged;
        Refresh();

        // Make slot positions valid right now so the frame can snap on the first frame
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)slotParent);

        Select(0);
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= OnInventoryChanged;

        foreach (var a in numberActions) a.Dispose();
        scrollAction?.Dispose();
    }

    private void SetupHighlight()
    {
        if (selectionHighlight == null || slotParent == null) return;

        // The frame must not be controlled by the layout group: move it next to the group
        if (selectionHighlight.parent == slotParent)
        {
            selectionHighlight.SetParent(slotParent.parent, false);
        }

        // Center anchors + pivot so localPosition / sizeDelta map directly to the slot center and size
        selectionHighlight.anchorMin = selectionHighlight.anchorMax = new Vector2(0.5f, 0.5f);
        selectionHighlight.pivot = new Vector2(0.5f, 0.5f);
        selectionHighlight.SetAsLastSibling(); // draw above the slots

        var graphic = selectionHighlight.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = false;
    }

    private void SetupFadeGroup()
    {
        if (slotParent == null)
        {
            Debug.LogError("PlayerHotbar requires a slot parent reference.", this);
            enabled = false;
            return;
        }

        if (hotbarGroup == null)
        {
            // The highlight is moved next to the slot group, so the slot parent's parent
            // contains both the slots and the frame.
            GameObject root = slotParent.parent != null ? slotParent.parent.gameObject : slotParent.gameObject;
            hotbarGroup = root.GetComponent<CanvasGroup>();
            if (hotbarGroup == null) hotbarGroup = root.AddComponent<CanvasGroup>();
        }

        hotbarGroup.alpha = 1f;
        lastInputTime = Time.unscaledTime;
    }

    private void OnInventoryChanged()
    {
        Refresh();
        if (wakeOnInventoryChange) Wake();
    }

    public void Refresh()
    {
        if (inventory == null) return;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            int inventoryIndex = startIndex + i;
            if (inventoryIndex < inventory.containers.Count)
                slotUIs[i].UpdateSlot(inventory.containers[inventoryIndex]);
        }
    }

    /// <summary>Shows the hotbar and restarts the idle timer.</summary>
    public void Wake()
    {
        lastInputTime = Time.unscaledTime;
    }

    /// <summary>Select a hotbar slot (0-based within the hotbar).</summary>
    public void Select(int index)
    {
        if (slotUIs.Count == 0) return;

        // Any selection input wakes the hotbar, even if the slot didn't change
        Wake();

        int newIndex = Mathf.Clamp(index, 0, slotUIs.Count - 1);
        bool changed = newIndex != selectedIndex;
        selectedIndex = newIndex;

        if (selectionHighlight != null) selectionHighlight.gameObject.SetActive(true);

        if (changed) SelectionChanged?.Invoke(selectedIndex);
    }

    public void Cycle(int direction)
    {
        if (slotUIs.Count == 0) return;
        Select((selectedIndex + direction + slotUIs.Count) % slotUIs.Count);
    }

    private void UpdateIdleFade()
    {
        if (hotbarGroup == null) return;

        float targetAlpha = 1f;
        float speed = fadeInSpeed;

        if (fadeWhenIdle && Time.unscaledTime - lastInputTime > idleDelay)
        {
            targetAlpha = idleAlpha;
            speed = fadeOutSpeed;
        }

        hotbarGroup.alpha = Mathf.MoveTowards(hotbarGroup.alpha, targetAlpha, speed * Time.unscaledDeltaTime);
    }

    private void LateUpdate()
    {
        UpdateIdleFade();

        if (selectionHighlight == null || slotUIs.Count == 0) return;

        var slotRect = (RectTransform)slotUIs[selectedIndex].transform;
        var parent = (RectTransform)selectionHighlight.parent;

        // Target center, in the frame's parent space
        Vector3 worldCenter = slotRect.TransformPoint(slotRect.rect.center);
        Vector3 targetPos = parent.InverseTransformPoint(worldCenter);

        // Target size, converted between possibly different canvas scales
        float scaleRatio = slotRect.lossyScale.x / Mathf.Max(0.0001f, parent.lossyScale.x);
        Vector2 targetSize = slotRect.rect.size * scaleRatio;

        if (snapHighlight || moveSpeed <= 0f)
        {
            selectionHighlight.localPosition = targetPos;
            selectionHighlight.sizeDelta = targetSize;
            snapHighlight = false;
            return;
        }

        // Frame-rate independent exponential smoothing
        float t = 1f - Mathf.Exp(-moveSpeed * Time.unscaledDeltaTime);
        selectionHighlight.localPosition = Vector3.Lerp(selectionHighlight.localPosition, targetPos, t);
        selectionHighlight.sizeDelta = Vector2.Lerp(selectionHighlight.sizeDelta, targetSize, t);
    }
}