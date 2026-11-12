using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class Item3DViewer : MonoBehaviour, IDragHandler
{
    [Header("Preview Configuration")]
    [SerializeField] private ItemDataSO defaultItem;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Camera previewCamera;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 0.2f;
    [SerializeField] private bool invertVertical = false;

    [SerializeField] private CanvasGroup canvasGroup;

    public GameObject CurrentItemPrefab { get; private set; }

    private void Start()
    {
        if (defaultItem != null)
        {
            Display(defaultItem);
        }
    }

    /// <summary>
    /// Call this to switch displayed items at runtime (e.g., UI list clicks).
    /// </summary>
    public void Display(ItemDataSO itemData)
    {
        if (itemData == null || itemData.worldPrefab == null)
        {
            ClearCurrentItem();
            return;
        }

        ClearCurrentItem();

        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : new Vector3(1000f, 1000f, 1000f);
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        CurrentItemPrefab = Instantiate(itemData.worldPrefab, spawnPosition, spawnRotation);

        // Optional: Parent under spawnPoint for cleaner Hierarchy organization
        if (spawnPoint != null)
        {
            CurrentItemPrefab.transform.SetParent(spawnPoint);
        }

        PreparePrefabForPreview(CurrentItemPrefab);
    }

    public void ClearCurrentItem()
    {
        if (CurrentItemPrefab != null)
        {
            Destroy(CurrentItemPrefab);
            CurrentItemPrefab = null;
        }
    }

    private void PreparePrefabForPreview(GameObject prefab)
    {
        // 1. Disable Physics Rigidbodies
        if (prefab.TryGetComponent(out Rigidbody rb))
        {
            rb.isKinematic = true;
        }

        // 2. Disable all colliders on prefab and its children
        Collider[] colliders = prefab.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (CurrentItemPrefab == null) return;

        float deltaX = eventData.delta.x * rotationSpeed;
        float deltaY = eventData.delta.y * rotationSpeed * (invertVertical ? -1f : 1f);

        // Determine reference axes based on previewCamera or World axes
        Vector3 upAxis = previewCamera != null ? previewCamera.transform.up : Vector3.up;
        Vector3 rightAxis = previewCamera != null ? previewCamera.transform.right : Vector3.right;

        // Yaw (Horizontal drag rotates around Up axis)
        CurrentItemPrefab.transform.Rotate(upAxis, -deltaX, Space.World);

        // Pitch (Vertical drag rotates around Camera's Right axis)
        CurrentItemPrefab.transform.Rotate(rightAxis, deltaY, Space.World);
    }

    public void ShowItem(ItemDataSO data)
    {
        GameManager.Instance.SetGameState(GameState.ItemPreview);
        Display(data);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void ClosePreview()
    {
        GameManager.Instance.SetGameState(GameState.Playing);
        ClearCurrentItem();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}