using System;
using System.Collections;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Header("Drop")]
    [SerializeField] private Transform dropPoint;      // e.g. empty child in front of the camera
    [SerializeField] private float dropForce = 3f;

    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private float fadeDuration = 0.25f; // Duration of fade in/out
    [SerializeField] private int slotSize = 18;
    [SerializeField] private Item3DViewer item3DViewer;

    public Inventory Inventory { get; private set; }

    private CanvasGroup canvasGroup;
    private bool IsInventoryOpened = false;
    private Coroutine fadeRoutine;
    private PlayerItemDatabase itemDatabase;
    private System.Action<GameState> gameStateChangedHandler;

    void Awake()
    {
        itemDatabase = new PlayerItemDatabase();
        Inventory = new Inventory(slotSize);
        Inventory.ItemDropped += SpawnDrop;

        if (inventoryUI != null)
        {
            inventoryUI.Initialize(Inventory);
        }

        if (inventoryUI != null && inventoryUI.TryGetComponent<CanvasGroup>(out CanvasGroup canvas))
        {
            canvasGroup = canvas;
            // Set initial state to hidden
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        else
        {
            Debug.LogWarning("InventoryUI is missing a CanvasGroup component!", this);
        }
    }

    private void OnDestroy()
    {
        if (Inventory != null) Inventory.ItemDropped -= SpawnDrop;
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            gameStateChangedHandler = state =>
            {
                if (state != GameState.Inventory)
                    CloseInventory();
            };
            GameManager.Instance.OnGameStateChanged += gameStateChangedHandler;
        }
    }

    void Update()
    {
        // Toggle inventory properly on button press
        if (InputManager.Instance != null && InputManager.Instance.InventoryPressed)
        {
            if (IsInventoryOpened)
            {
                CloseInventory();
                GameManager.Instance.SetGameState(GameState.Playing);
            }
            else
            {
                OpenInventory();
                GameManager.Instance.SetGameState(GameState.Inventory);
            }
        }
    }

    private void OpenInventory()
    {
        IsInventoryOpened = true;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            StartFade(1f);
        }
    }

    private void CloseInventory()
    {
        IsInventoryOpened = false;

        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            StartFade(0f);
        }
    }

    private void StartFade(float targetAlpha)
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }
        fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
    }

    private IEnumerator FadeRoutine(float targetAlpha)
    {
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);

            // Optional: If you want to use your EaseOutBack for scaling or smoothing, 
            // you can apply it here, but a linear/smoothstep works great for alpha fading.
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }

    // Call this whenever you pick up an item
    public void GiveItem(ItemDataSO item, int amount)
    {
        Inventory.AddItem(item, amount);

        if (inventoryUI != null)
        {
            inventoryUI.RefreshUI(); // Refresh UI after changes
        }
    }

    // Call this whenever you drop or use an item
    public void TakeItem(ItemDataSO item, int amount)
    {
        Inventory.RemoveItem(item, amount);

        if (inventoryUI != null)
        {
            inventoryUI.RefreshUI();
        }
    }

    private static float EaseOutBack(float t, float s)
    {
        float c1 = s;
        float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    /// <summary>Returns how many items did NOT fit (0 = all added).</summary>
    public int Add(ItemDataSO data, int amount)
    {
        if (Inventory == null) return amount;
        if (data == null || amount <= 0) return amount;

        int leftover = Inventory.AddItem(data, amount);
        int added = amount - leftover;

        // Show the item preview only the first time this item is successfully added.
        // Do not mark it as discovered when the inventory was full.
        if (added > 0 && !itemDatabase.HasItem(data))
        {
            itemDatabase.AddItem(data);
            if (item3DViewer != null)
                item3DViewer.ShowItem(data);
        }

        return leftover;
    }

    private void SpawnDrop(ItemDataSO data, int amount)
    {
        if (data.worldPrefab == null)
        {
            Debug.LogWarning($"{data.displayName} has no worldPrefab, item was destroyed.");
            return;
        }

        Transform origin = dropPoint != null ? dropPoint : transform;
        GameObject obj = Instantiate(data.worldPrefab, origin.position, Quaternion.identity);

        if (obj.TryGetComponent(out PickupInteractable pickup)) pickup.Setup(data, amount);
        if (obj.TryGetComponent(out Rigidbody rb)) rb.AddForce(origin.forward * dropForce, ForceMode.Impulse);
    }
}