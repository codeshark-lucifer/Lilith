using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The canvas. The DialogueGraphSO is the source of truth: node fields are edited in place,
/// and SyncToAsset() copies positions, the node list and the connections (edges) back into it.
/// Sync runs automatically after every change to the graph.
/// </summary>
public class DialogueGraphView : GraphView
{
    private DialogueGraphSO asset;
    private bool rebuilding;        // true while the canvas is being (re)built, so we don't sync half-built state

    public DialogueGraphView()
    {
        style.flexGrow = 1;

        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());

        var grid = new GridBackground();
        Insert(0, grid);
        grid.StretchToParentSize();

        graphViewChanged = OnGraphViewChanged;
    }

    // ---- which ports may connect -------------------------------------------------------------

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        var compatible = new List<Port>();
        foreach (Port port in ports.ToList())
        {
            if (port == startPort) continue;
            if (port.node == startPort.node) continue;               // no connecting a node to itself
            if (port.direction == startPort.direction) continue;     // output <-> input only
            compatible.Add(port);
        }
        return compatible;
    }

    // ---- right-click menu ----------------------------------------------------------------------

    public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
    {
        base.BuildContextualMenu(evt);
        if (asset == null) return;

        Vector2 pos = contentViewContainer.WorldToLocal(evt.mousePosition);

        evt.menu.AppendAction("Add Node/Start", a => AddNewNode(new DialogueStart(), pos));
        evt.menu.AppendAction("Add Node/Line", a => AddNewNode(new DialogueLine(), pos));
        evt.menu.AppendAction("Add Node/Choice", a => AddNewNode(NewChoice(), pos));
        evt.menu.AppendAction("Add Node/Condition", a => AddNewNode(new DialogueConditionNode(), pos));
        evt.menu.AppendAction("Add Node/End", a => AddNewNode(new DialogueEnd(), pos));
    }

    private static DialogueChoice NewChoice()
    {
        var choice = new DialogueChoice();
        choice.options.Add(new DialogueChoiceOption { text = "Option 1" });
        choice.options.Add(new DialogueChoiceOption { text = "Option 2" });
        return choice;
    }

    private void AddNewNode(DialogueNode data, Vector2 position)
    {
        if (asset == null) return;

        data.guid = Guid.NewGuid().ToString();
        data.position = position;
        AddElement(CreateEditorNode(data));
        SyncToAsset();
    }

    private DialogueEditorNode CreateEditorNode(DialogueNode data)
    {
        var node = new DialogueEditorNode(data, this);
        node.SetPosition(new Rect(data.position, Vector2.zero));
        return node;
    }

    // ---- load -----------------------------------------------------------------------------------

    public void Load(DialogueGraphSO so)
    {
        asset = so;
        rebuilding = true;

        DeleteElements(graphElements.ToList());

        bool createdDefaults = false;
        if (so != null)
        {
            if (so.nodes == null) so.nodes = new List<DialogueNode>();

            if (so.nodes.Count == 0)
            {
                so.nodes.Add(new DialogueStart { guid = Guid.NewGuid().ToString(), position = new Vector2(80, 160) });
                createdDefaults = true;
            }

            var byGuid = new Dictionary<string, DialogueEditorNode>();
            foreach (DialogueNode data in so.nodes)
            {
                if (data == null) continue;
                DialogueEditorNode node = CreateEditorNode(data);
                byGuid[data.guid] = node;
                AddElement(node);
            }

            foreach (DialogueEditorNode node in byGuid.Values)
                ConnectFromData(node, byGuid);
        }

        rebuilding = false;

        if (createdDefaults) SyncToAsset();
        schedule.Execute(() => FrameAll()).StartingIn(100);
    }

    private void ConnectFromData(DialogueEditorNode node, Dictionary<string, DialogueEditorNode> byGuid)
    {
        for (int slot = 0; slot < node.Outputs.Count; slot++)
        {
            string targetGuid = node.Data.GetNext(slot);
            if (string.IsNullOrEmpty(targetGuid)) continue;
            if (!byGuid.TryGetValue(targetGuid, out DialogueEditorNode target) || target.Input == null) continue;

            AddElement(node.Outputs[slot].ConnectTo(target.Input));
        }
    }

    // ---- keep the asset up to date ---------------------------------------------------------------

    private GraphViewChange OnGraphViewChanged(GraphViewChange change)
    {
        // The change has not been applied yet when this callback runs, so read the graph one tick later.
        if (!rebuilding) schedule.Execute(SyncToAsset);
        return change;
    }

    /// <summary>Marks the asset as changed (call after editing a field of a node).</summary>
    public void NotifyChanged()
    {
        if (asset != null && !rebuilding)
        {
            Undo.RegisterCompleteObjectUndo(asset, "Dialogue Graph Edit");
            EditorUtility.SetDirty(asset);
        }
    }

    /// <summary>Copies positions, nodes and connections from the canvas into the asset.</summary>
    public void SyncToAsset()
    {
        if (asset == null || rebuilding) return;

        Undo.RegisterCompleteObjectUndo(asset, "Dialogue Graph Edit");

        List<DialogueEditorNode> editorNodes = nodes.ToList().OfType<DialogueEditorNode>().ToList();

        foreach (DialogueEditorNode node in editorNodes)
        {
            node.Data.position = node.GetPosition().position;
            for (int slot = 0; slot < node.Data.OutputCount; slot++)
                node.Data.SetNext(slot, null);
        }

        foreach (Edge edge in edges.ToList())
        {
            var from = edge.output?.node as DialogueEditorNode;
            var to = edge.input?.node as DialogueEditorNode;
            if (from == null || to == null || !(edge.output.userData is int slot)) continue;

            from.Data.SetNext(slot, to.Data.guid);
        }

        asset.nodes = editorNodes.Select(n => n.Data).ToList();
        EditorUtility.SetDirty(asset);
    }

    /// <summary>
    /// Rebuilds a node's output ports (after its options changed) and redraws its edges from the data.
    /// Call SyncToAsset() BEFORE changing the data, so the links are already stored in it.
    /// </summary>
    public void RefreshNode(DialogueEditorNode node)
    {
        rebuilding = true;

        foreach (Port port in node.Outputs)
        {
            foreach (Edge edge in port.connections.ToList())
            {
                edge.input?.Disconnect(edge);
                edge.output?.Disconnect(edge);
                RemoveElement(edge);
            }
        }

        node.RebuildOutputs();

        var byGuid = nodes.ToList().OfType<DialogueEditorNode>().ToDictionary(n => n.Data.guid);
        ConnectFromData(node, byGuid);

        rebuilding = false;
        SyncToAsset();
    }
}
