using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Put on a full-screen UI object at the root of your Canvas.
/// Children: a transparent full-screen Button ("blocker") and a Panel ("panel")
/// with a VerticalLayoutGroup + ContentSizeFitter (Vertical = Preferred Size).
/// </summary>
public class InventoryContextMenu : MonoBehaviour
{
    public static InventoryContextMenu Instance { get; private set; }

    [SerializeField] private RectTransform panel;
    [SerializeField] private Button blocker;        // click outside = close
    [SerializeField] private Button buttonPrefab;   // Button with a TMP_Text child

    private readonly List<GameObject> spawned = new List<GameObject>();
    private RectTransform rootRect;
    private Canvas rootCanvas;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rootRect = (RectTransform)transform;
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;

        if (panel == null || blocker == null || buttonPrefab == null || rootCanvas == null)
        {
            Debug.LogError("InventoryContextMenu requires a Canvas, panel, blocker, and button prefab.", this);
            return;
        }

        blocker.onClick.AddListener(Hide);
        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(List<(string label, Action action)> options, Vector2 screenPos)
    {
        if (panel == null || blocker == null || rootCanvas == null || options == null) return;
        Clear();
        if (options.Count == 0) return;

        transform.SetAsLastSibling(); // draw above everything else

        foreach (var (label, action) in options)
        {
            Button btn = Instantiate(buttonPrefab, panel);
            btn.GetComponentInChildren<TMP_Text>().text = label;
            btn.onClick.AddListener(() =>
            {
                Hide();
                action?.Invoke();
            });
            spawned.Add(btn.gameObject);
        }

        blocker.gameObject.SetActive(true);
        panel.gameObject.SetActive(true);

        // Place at the cursor (works for Overlay and Camera canvases)
        Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
        RectTransformUtility.ScreenPointToWorldPointInRectangle(rootRect, screenPos, cam, out Vector3 world);
        panel.pivot = new Vector2(0f, 1f); // top-left corner at the cursor
        panel.position = world;

        // Keep fully on screen
        LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
        ClampToRoot();
    }

    public void Hide()
    {
        Clear();
        panel.gameObject.SetActive(false);
        blocker.gameObject.SetActive(false);
    }

    private void Clear()
    {
        foreach (var go in spawned) Destroy(go);
        spawned.Clear();
    }

    private void ClampToRoot()
    {
        var rootCorners = new Vector3[4];
        var panelCorners = new Vector3[4];
        rootRect.GetWorldCorners(rootCorners);
        panel.GetWorldCorners(panelCorners);

        Vector3 shift = Vector3.zero;
        if (panelCorners[2].x > rootCorners[2].x) shift.x = rootCorners[2].x - panelCorners[2].x; // right edge
        if (panelCorners[0].y < rootCorners[0].y) shift.y = rootCorners[0].y - panelCorners[0].y; // bottom edge
        panel.position += shift;
    }
}