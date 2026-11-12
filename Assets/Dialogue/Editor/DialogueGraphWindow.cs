using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class DialogueGraphWindow : EditorWindow
{
    [SerializeField] private DialogueGraphSO current;   // survives script recompiles

    private DialogueGraphView view;
    private ObjectField assetField;

    [MenuItem("Window/Dialogue Graph Editor")]
    public static void OpenWindow() => GetWindow<DialogueGraphWindow>("Dialogue Graph");

    public static void Open(DialogueGraphSO asset)
    {
        var window = GetWindow<DialogueGraphWindow>("Dialogue Graph");
        window.SetAsset(asset);
    }

    private void OnEnable()
    {
        BuildUI();
        rootVisualElement.focusable = true;
        rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown);
        if (current != null) view.Load(current);
    }

    private void OnKeyDown(KeyDownEvent evt)
    {
        if (!evt.ctrlKey && !evt.commandKey) return;

        if (evt.keyCode == KeyCode.Z && !evt.shiftKey)
        {
            Undo.PerformUndo();
            evt.StopPropagation();
            evt.PreventDefault();
            return;
        }

        if (evt.keyCode == KeyCode.Y || (evt.keyCode == KeyCode.Z && evt.shiftKey))
        {
            Undo.PerformRedo();
            evt.StopPropagation();
            evt.PreventDefault();
        }
    }

    private void BuildUI()
    {
        VisualElement root = rootVisualElement;
        root.Clear();

        var toolbar = new Toolbar();

        assetField = new ObjectField { objectType = typeof(DialogueGraphSO), allowSceneObjects = false };
        assetField.style.minWidth = 280;
        assetField.SetValueWithoutNotify(current);
        assetField.RegisterValueChangedCallback(ev => SetAsset(ev.newValue as DialogueGraphSO));
        toolbar.Add(assetField);

        toolbar.Add(new ToolbarButton(SaveAsset) { text = "Save" });
        toolbar.Add(new ToolbarButton(() => view.FrameAll()) { text = "Frame All" });
        root.Add(toolbar);

        view = new DialogueGraphView();
        root.Add(view);
    }

    private void SetAsset(DialogueGraphSO asset)
    {
        current = asset;
        assetField.SetValueWithoutNotify(asset);
        view.Load(asset);
    }

    private void SaveAsset()
    {
        if (current == null) return;
        view.SyncToAsset();
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent("Saved"));
    }
}
