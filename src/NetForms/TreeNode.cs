using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum TreeViewAction
{
    Unknown = 0,
    ByKeyboard = 1,
    ByMouse = 2,
    Collapse = 3,
    Expand = 4,
}

public enum TreeViewDrawMode
{
    Normal = 0,
    OwnerDrawText = 1,
    OwnerDrawAll = 2,
}

[Flags]
public enum TreeViewHitTestLocations
{
    None = 1,
    Image = 2,
    Label = 4,
    Indent = 8,
    PlusMinus = 16,
    RightOfLabel = 32,
    StateImage = 64,
    AboveClientArea = 256,
    BelowClientArea = 512,
    LeftOfClientArea = 1024,
    RightOfClientArea = 2048,
}

/// <summary>One node of a <see cref="TreeView"/>: a label, an optional image and its children.</summary>
[DefaultProperty(nameof(Text))]
public class TreeNode : ICloneable
{
    private string _text = string.Empty;
    private bool _expanded;
    private bool _checked;
    private Color _foreColor = Color.Empty;
    private Color _backColor = Color.Empty;

    public TreeNode()
    {
        Nodes = new TreeNodeCollection(this);
    }

    public TreeNode(string? text) : this() => _text = text ?? string.Empty;

    public TreeNode(string? text, TreeNode[] children) : this(text)
    {
        if (children != null) Nodes.AddRange(children);
    }

    public TreeNode(string? text, int imageIndex, int selectedImageIndex) : this(text)
    {
        ImageIndex = imageIndex;
        SelectedImageIndex = selectedImageIndex;
    }

    public TreeNode(string? text, int imageIndex, int selectedImageIndex, TreeNode[] children)
        : this(text, imageIndex, selectedImageIndex)
    {
        if (children != null) Nodes.AddRange(children);
    }

    [Category("Appearance")]
    [Description("The text displayed in the label of the tree node.")]
    [Localizable(true)]
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            TreeView?.NodesChanged();
        }
    }

    [Category("Appearance")]
    [Description("The name of the object.")]
    public string Name { get; set; } = string.Empty;

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [Category("Appearance")]
    [Description("The ToolTip text that will be displayed when the mouse hovers over the node.")]
    [DefaultValue("")]
    public string ToolTipText { get; set; } = string.Empty;

    [Browsable(false)]
    public TreeNodeCollection Nodes { get; }

    [Browsable(false)]
    public TreeNode? Parent { get; internal set; }

    [Description("Displays a hierarchical collection of labeled items to the user that optionally contain an image.")]
    [Browsable(false)]
    public TreeView? TreeView { get; internal set; }

    [Category("Behavior")]
    [Description("The ImageList index value of the image displayed when the tree node is in the unselected state.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int ImageIndex { get; set; } = -1;

    [Category("Behavior")]
    [Description("Identifies the image to display on the node when it is not selected.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string ImageKey { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("The key in the image-list value that represents the image to display when the tree node is in the selected state.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int SelectedImageIndex { get; set; } = -1;

    [Category("Behavior")]
    [Description("The key in the ImageList value that represents the image to display when the tree node is in the selected state.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string SelectedImageKey { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("The index in the StateImageList displayed when CheckBoxes is set to false on the TreeView.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int StateImageIndex { get; set; } = -1;

    [Category("Appearance")]
    [Description("The font used to display the text on the tree node's label.")]
    [DefaultValue(null)]
    [Localizable(true)]
    public Font? NodeFont { get; set; }

    [Category("Appearance")]
    [Description("The foreground color of tree node.")]
    public Color ForeColor
    {
        get => !_foreColor.IsEmpty ? _foreColor : TreeView?.ForeColor ?? Control.DefaultForeColor;
        set { _foreColor = value; TreeView?.Invalidate(); }
    }

    [Category("Appearance")]
    [Description("The background color of tree node.")]
    public Color BackColor
    {
        get => !_backColor.IsEmpty ? _backColor : TreeView?.BackColor ?? Control.DefaultBackColor;
        set { _backColor = value; TreeView?.Invalidate(); }
    }

    internal bool IsBackColorSet => !_backColor.IsEmpty;

    internal bool ShouldSerializeForeColor() => !_foreColor.IsEmpty;

    internal bool ShouldSerializeBackColor() => !_backColor.IsEmpty;

    /// <summary>Position among the siblings.</summary>
    [Category("Behavior")]
    [Description("The position of the tree node in the tree node collection.")]
    public int Index => Parent?.Nodes.IndexOf(this) ?? TreeView?.Nodes.IndexOf(this) ?? -1;

    /// <summary>Zero for a root node, one for its children, and so on.</summary>
    [Browsable(false)]
    public int Level
    {
        get
        {
            int level = 0;
            for (var p = Parent; p != null; p = p.Parent) level++;
            return level;
        }
    }

    [Browsable(false)]
    public string FullPath
    {
        get
        {
            string separator = TreeView?.PathSeparator ?? "\\";
            var parts = new List<string>();
            for (TreeNode? n = this; n != null; n = n.Parent) parts.Insert(0, n.Text);
            return string.Join(separator, parts);
        }
    }

    [Browsable(false)]
    public bool IsExpanded => _expanded;

    [Browsable(false)]
    public bool IsSelected => TreeView?.SelectedNode == this;

    [Browsable(false)]
    public bool IsEditing => TreeView?.EditingNode == this;

    /// <summary>True when every ancestor is expanded and the node is inside the visible area.</summary>
    [Browsable(false)]
    public bool IsVisible
    {
        get
        {
            for (var p = Parent; p != null; p = p.Parent)
            {
                if (!p.IsExpanded) return false;
            }
            return TreeView != null;
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the tree node is in a checked state.")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            if (TreeView != null) TreeView.SetNodeChecked(this, value, TreeViewAction.Unknown);
            else _checked = value;
        }
    }

    internal void SetCheckedCore(bool value) => _checked = value;

    internal void SetExpandedCore(bool value) => _expanded = value;

    [Browsable(false)]
    public TreeNode? FirstNode => Nodes.Count > 0 ? Nodes[0] : null;

    [Browsable(false)]
    public TreeNode? LastNode => Nodes.Count > 0 ? Nodes[Nodes.Count - 1] : null;

    [Browsable(false)]
    public TreeNode? NextNode
    {
        get
        {
            var siblings = SiblingCollection;
            int i = siblings?.IndexOf(this) ?? -1;
            return siblings != null && i >= 0 && i + 1 < siblings.Count ? siblings[i + 1] : null;
        }
    }

    [Browsable(false)]
    public TreeNode? PrevNode
    {
        get
        {
            var siblings = SiblingCollection;
            int i = siblings?.IndexOf(this) ?? -1;
            return siblings != null && i > 0 ? siblings[i - 1] : null;
        }
    }

    private TreeNodeCollection? SiblingCollection => Parent?.Nodes ?? TreeView?.Nodes;

    [Browsable(false)]
    public TreeNode? NextVisibleNode => TreeView?.GetNextVisible(this);

    [Browsable(false)]
    public TreeNode? PrevVisibleNode => TreeView?.GetPrevVisible(this);

    [Browsable(false)]
    public Rectangle Bounds => TreeView?.GetNodeBounds(this) ?? Rectangle.Empty;

    public void Expand()
    {
        TreeView?.ExpandNode(this, TreeViewAction.Unknown);
        if (TreeView == null) _expanded = true;
    }

    public void Collapse()
    {
        TreeView?.CollapseNode(this, TreeViewAction.Unknown);
        if (TreeView == null) _expanded = false;
    }

    public void Toggle()
    {
        if (_expanded) Collapse();
        else Expand();
    }

    public void ExpandAll()
    {
        Expand();
        foreach (TreeNode child in Nodes) child.ExpandAll();
    }

    public void Collapse(bool ignoreChildren)
    {
        Collapse();
        if (ignoreChildren) return;
        foreach (TreeNode child in Nodes) child.Collapse(false);
    }

    public void EnsureVisible()
    {
        for (var p = Parent; p != null; p = p.Parent) p.Expand();
        TreeView?.EnsureNodeVisible(this);
    }

    public void Remove()
    {
        (Parent?.Nodes ?? TreeView?.Nodes)?.Remove(this);
    }

    public void BeginEdit() => TreeView?.BeginNodeEdit(this);

    public void EndEdit(bool cancel) => TreeView?.EndNodeEdit(!cancel);

    public int GetNodeCount(bool includeSubTrees)
    {
        int count = Nodes.Count;
        if (includeSubTrees)
        {
            foreach (TreeNode child in Nodes) count += child.GetNodeCount(true);
        }
        return count;
    }

    public object Clone()
    {
        var copy = new TreeNode(_text)
        {
            Name = Name,
            Tag = Tag,
            ImageIndex = ImageIndex,
            SelectedImageIndex = SelectedImageIndex,
            NodeFont = NodeFont,
            _foreColor = _foreColor,
            _backColor = _backColor,
            _checked = _checked,
        };
        foreach (TreeNode child in Nodes) copy.Nodes.Add((TreeNode)child.Clone());
        return copy;
    }

    public override string ToString() => "TreeNode: " + _text;
}

public class TreeNodeCollection : IList, IList<TreeNode>
{
    private readonly TreeNode? _ownerNode;
    private readonly TreeView? _ownerTree;
    private readonly List<TreeNode> _nodes = new();

    internal TreeNodeCollection(TreeNode owner) => _ownerNode = owner;

    internal TreeNodeCollection(TreeView owner) => _ownerTree = owner;

    private TreeView? Tree => _ownerTree ?? _ownerNode?.TreeView;

    public int Count => _nodes.Count;
    public bool IsReadOnly => false;

    public virtual TreeNode this[int index]
    {
        get => _nodes[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Detach(_nodes[index]);
            Attach(value);
            _nodes[index] = value;
            Tree?.NodesChanged();
        }
    }

    public virtual TreeNode? this[string? key]
    {
        get
        {
            int i = IndexOfKey(key);
            return i >= 0 ? _nodes[i] : null;
        }
    }

    public virtual TreeNode Add(string? text)
    {
        var node = new TreeNode(text);
        Add(node);
        return node;
    }

    public virtual TreeNode Add(string? key, string? text)
    {
        var node = new TreeNode(text) { Name = key ?? string.Empty };
        Add(node);
        return node;
    }

    public virtual TreeNode Add(string? key, string? text, int imageIndex)
    {
        var node = Add(key, text);
        node.ImageIndex = imageIndex;
        return node;
    }

    public virtual TreeNode Add(string? key, string? text, int imageIndex, int selectedImageIndex)
    {
        var node = Add(key, text, imageIndex);
        node.SelectedImageIndex = selectedImageIndex;
        return node;
    }

    public virtual int Add(TreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Attach(node);
        _nodes.Add(node);
        Tree?.NodesChanged();
        return _nodes.Count - 1;
    }

    void ICollection<TreeNode>.Add(TreeNode item) => Add(item);

    public virtual void AddRange(TreeNode[] nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        Tree?.BeginUpdate();
        foreach (var n in nodes) Add(n);
        Tree?.EndUpdate();
    }

    public virtual void Clear()
    {
        foreach (var node in _nodes.ToArray()) Detach(node);
        _nodes.Clear();
        Tree?.NodesChanged();
    }

    public bool Contains(TreeNode node) => _nodes.Contains(node);
    public virtual bool ContainsKey(string? key) => IndexOfKey(key) >= 0;
    public int IndexOf(TreeNode node) => _nodes.IndexOf(node);

    public virtual int IndexOfKey(string? key)
    {
        if (string.IsNullOrEmpty(key)) return -1;
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (string.Equals(_nodes[i].Name, key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    public virtual void Insert(int index, TreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Attach(node);
        _nodes.Insert(Math.Clamp(index, 0, _nodes.Count), node);
        Tree?.NodesChanged();
    }

    public virtual TreeNode Insert(int index, string? text)
    {
        var node = new TreeNode(text);
        Insert(index, node);
        return node;
    }

    public bool Remove(TreeNode node)
    {
        if (node == null || !_nodes.Remove(node)) return false;
        Detach(node);
        Tree?.NodesChanged();
        return true;
    }

    public virtual void RemoveAt(int index)
    {
        Detach(_nodes[index]);
        _nodes.RemoveAt(index);
        Tree?.NodesChanged();
    }

    public virtual void RemoveByKey(string? key)
    {
        int i = IndexOfKey(key);
        if (i >= 0) RemoveAt(i);
    }

    public TreeNode[] Find(string key, bool searchAllChildren)
    {
        var result = new List<TreeNode>();
        FindInto(this, key, searchAllChildren, result);
        return result.ToArray();
    }

    private static void FindInto(TreeNodeCollection nodes, string key, bool deep, List<TreeNode> into)
    {
        foreach (var node in nodes._nodes)
        {
            if (string.Equals(node.Name, key, StringComparison.OrdinalIgnoreCase)) into.Add(node);
            if (deep) FindInto(node.Nodes, key, true, into);
        }
    }

    private void Attach(TreeNode node)
    {
        (node.Parent?.Nodes ?? node.TreeView?.Nodes)?.Remove(node);
        node.Parent = _ownerNode;
        SetTree(node, Tree);
    }

    private void Detach(TreeNode node)
    {
        node.Parent = null;
        SetTree(node, null);
    }

    internal static void SetTree(TreeNode node, TreeView? tree)
    {
        node.TreeView = tree;
        foreach (TreeNode child in node.Nodes) SetTree(child, tree);
    }

    /// <summary>Re-stamp the owner tree on every node; used when a subtree is grafted onto a TreeView.</summary>
    internal void SetTreeRecursive(TreeView? tree)
    {
        foreach (var node in _nodes) SetTree(node, tree);
    }

    internal void SortCore(IComparer comparer, bool recursive)
    {
        _nodes.Sort((a, b) => comparer.Compare(a, b));
        if (!recursive) return;
        foreach (var node in _nodes) node.Nodes.SortCore(comparer, true);
    }

    public void CopyTo(TreeNode[] array, int index) => _nodes.CopyTo(array, index);
    public IEnumerator<TreeNode> GetEnumerator() => _nodes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _nodes.GetEnumerator();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _nodes[index]; set => this[index] = (TreeNode)value!; }
    int IList.Add(object? value) => Add((TreeNode)value!);
    bool IList.Contains(object? value) => value is TreeNode n && Contains(n);
    int IList.IndexOf(object? value) => value is TreeNode n ? IndexOf(n) : -1;
    void IList.Insert(int index, object? value) => Insert(index, (TreeNode)value!);
    void IList.Remove(object? value) { if (value is TreeNode n) Remove(n); }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_nodes).CopyTo(array, index);
}

// --- event args ---------------------------------------------------------------------------

public delegate void TreeViewEventHandler(object? sender, TreeViewEventArgs e);
public delegate void TreeViewCancelEventHandler(object? sender, TreeViewCancelEventArgs e);
public delegate void NodeLabelEditEventHandler(object? sender, NodeLabelEditEventArgs e);
public delegate void TreeNodeMouseClickEventHandler(object? sender, TreeNodeMouseClickEventArgs e);
public delegate void DrawTreeNodeEventHandler(object? sender, DrawTreeNodeEventArgs e);

public class TreeViewEventArgs : EventArgs
{
    public TreeViewEventArgs(TreeNode? node) : this(node, TreeViewAction.Unknown) { }

    public TreeViewEventArgs(TreeNode? node, TreeViewAction action)
    {
        Node = node;
        Action = action;
    }

    public TreeNode? Node { get; }
    public TreeViewAction Action { get; }
}

public class TreeViewCancelEventArgs : CancelEventArgs
{
    public TreeViewCancelEventArgs(TreeNode? node, bool cancel, TreeViewAction action) : base(cancel)
    {
        Node = node;
        Action = action;
    }

    public TreeNode? Node { get; }
    public TreeViewAction Action { get; }
}

public class NodeLabelEditEventArgs : EventArgs
{
    public NodeLabelEditEventArgs(TreeNode? node) : this(node, null) { }

    public NodeLabelEditEventArgs(TreeNode? node, string? label)
    {
        Node = node;
        Label = label;
    }

    public TreeNode? Node { get; }
    public string? Label { get; }
    public bool CancelEdit { get; set; }
}

public class TreeNodeMouseClickEventArgs : MouseEventArgs
{
    public TreeNodeMouseClickEventArgs(TreeNode node, MouseButtons button, int clicks, int x, int y)
        : base(button, clicks, x, y, 0) => Node = node;

    public TreeNode Node { get; }
}

public class TreeViewHitTestInfo
{
    public TreeViewHitTestInfo(TreeNode? hitNode, TreeViewHitTestLocations hitLocation)
    {
        Node = hitNode;
        Location = hitLocation;
    }

    public TreeNode? Node { get; }
    public TreeViewHitTestLocations Location { get; }
}

[Flags]
public enum TreeNodeStates
{
    Checked = 8,
    Default = 32,
    Focused = 16,
    Grayed = 2,
    Hot = 64,
    Indeterminate = 256,
    Marked = 128,
    Selected = 1,
    ShowKeyboardCues = 512,
}

public class DrawTreeNodeEventArgs : EventArgs
{
    public DrawTreeNodeEventArgs(Graphics graphics, TreeNode? node, Rectangle bounds, TreeNodeStates state)
    {
        Graphics = graphics;
        Node = node;
        Bounds = bounds;
        State = state;
    }

    public Graphics Graphics { get; }
    public TreeNode? Node { get; }
    public Rectangle Bounds { get; }
    public TreeNodeStates State { get; }
    public bool DrawDefault { get; set; }
}
