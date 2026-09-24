using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The hierarchical list. Visible nodes are flattened into rows on every layout pass, so hit-testing,
/// keyboard navigation and scrolling all work on row indices while the tree itself stays a tree.
/// </summary>
[DefaultProperty(nameof(Nodes))]
[DefaultEvent(nameof(AfterSelect))]
public class TreeView : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    private readonly TreeNodeCollection _nodes;
    private readonly List<TreeNode> _rows = new();
    private readonly ScrollBarCore _vscroll;
    private readonly ScrollBarCore _hscroll;
    private bool _vVisible;
    private bool _hVisible;
    private Point _scroll;
    private Size _content;
    private bool _layoutDirty = true;
    private int _updateCount;

    private TreeNode? _selected;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private int _indent = 19;
    private int _itemHeight = -1;
    private bool _checkBoxes;
    private bool _showLines = true;
    private bool _showPlusMinus = true;
    private bool _showRootLines = true;
    private bool _fullRowSelect;
    private bool _sorted;
    private TextBox? _editor;
    private TreeNode? _editing;

    public TreeView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        _nodes = new TreeNodeCollection(this);
        _vscroll = new ScrollBarCore(this, vertical: true, (v, _) => ScrollTo(_scroll.X, v));
        _hscroll = new ScrollBarCore(this, vertical: false, (v, _) => ScrollTo(v, _scroll.Y));
    }

    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Window;
        set => base.BackColor = value;
    }

    protected override Size DefaultSize => new Size(121, 97);

    [Category("Behavior")]
    [Description("The root nodes in the TreeView control.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public TreeNodeCollection Nodes => _nodes;

    // --- appearance -------------------------------------------------------------------------

    [Category("Appearance")]
    [Description("The border style of the control.")]
    [DefaultValue(BorderStyle.Fixed3D)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            InvalidateLayout();
        }
    }

    [Category("Behavior")]
    [Description("The indentation width of child nodes in pixels.")]
    [Localizable(true)]
    public int Indent
    {
        get => _indent;
        set
        {
            if (value < 0 || value > 32000) throw new ArgumentOutOfRangeException(nameof(value));
            if (_indent == value) return;
            _indent = value;
            InvalidateLayout();
        }
    }

    internal bool ShouldSerializeIndent() => _indent != 19;

    [Category("Appearance")]
    [Description("The height of every node in the TreeView.")]
    public int ItemHeight
    {
        get => _itemHeight >= 0 ? _itemHeight : Font.Height + 3;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
            if (_itemHeight == value) return;
            _itemHeight = value;
            InvalidateLayout();
        }
    }

    internal bool ShouldSerializeItemHeight() => _itemHeight >= 0;

    [Category("Appearance")]
    [Description("Indicates whether check boxes are displayed beside nodes.")]
    [DefaultValue(false)]
    public bool CheckBoxes
    {
        get => _checkBoxes;
        set
        {
            if (_checkBoxes == value) return;
            _checkBoxes = value;
            InvalidateLayout();
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether lines are displayed between sibling nodes and between parent and child nodes.")]
    [DefaultValue(true)]
    public bool ShowLines
    {
        get => _showLines;
        set { _showLines = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Indicates if plus/minus buttons are shown next to parent nodes.")]
    [DefaultValue(true)]
    public bool ShowPlusMinus
    {
        get => _showPlusMinus;
        set { _showPlusMinus = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Indicates whether lines are displayed between root nodes.")]
    [DefaultValue(true)]
    public bool ShowRootLines
    {
        get => _showRootLines;
        set
        {
            if (_showRootLines == value) return;
            _showRootLines = value;
            InvalidateLayout();
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the highlight spans the width of the TreeView.")]
    [DefaultValue(false)]
    public bool FullRowSelect
    {
        get => _fullRowSelect;
        set { _fullRowSelect = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Removes highlight from the selected node when control does not have focus.")]
    [DefaultValue(true)]
    public bool HideSelection { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether nodes give feedback when the mouse is moved over them.")]
    [DefaultValue(false)]
    public bool HotTracking { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the user can edit the label text of nodes.")]
    [DefaultValue(false)]
    public bool LabelEdit { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the control will display scroll bars if it contains more nodes than can fit in the visible area.")]
    [DefaultValue(true)]
    public bool Scrollable { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether ToolTips will be displayed on the nodes.")]
    [DefaultValue(false)]
    public bool ShowNodeToolTips { get; set; }

    [Category("Behavior")]
    [Description("The color of the lines that connect the nodes of the TreeView.")]
    [DefaultValue(typeof(Color), "Black")]
    // Empty until set, as in WinForms; the painter falls back to EffectiveLineColor.
    public Color LineColor { get; set; } = Color.Empty;

    private static readonly Color DefaultLineColor = Color.FromArgb(0x80, 0x80, 0x80);

    private Color EffectiveLineColor => LineColor.IsEmpty ? DefaultLineColor : LineColor;

    [Category("Behavior")]
    [Description("The string delimiter used for the path returned by a node's FullPath property.")]
    [DefaultValue("\\")]
    public string PathSeparator { get; set; } = "\\";

    [Category("Behavior")]
    [Description("Controls whether the system or the user paints the nodes.")]
    [DefaultValue(TreeViewDrawMode.Normal)]
    public TreeViewDrawMode DrawMode { get; set; } = TreeViewDrawMode.Normal;

    [Category("Behavior")]
    [Description("The ImageList control from which node images are taken.")]
    [DefaultValue(null)]
    public ImageList? ImageList { get; set; }

    [Category("Behavior")]
    [Description("The ImageList control used by the TreeView for custom states.")]
    [DefaultValue(null)]
    public ImageList? StateImageList { get; set; }

    [Category("Behavior")]
    [Description("The default image index for nodes.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int ImageIndex { get; set; } = -1;

    [Category("Behavior")]
    [Description("The default image key for nodes.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string ImageKey { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("The default image index for selected nodes.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int SelectedImageIndex { get; set; } = -1;

    [Category("Behavior")]
    [Description("The default image for selected nodes.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string SelectedImageKey { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("Indicates whether nodes are sorted.")]
    [DefaultValue(false)]
    [Browsable(false)]
    public bool Sorted
    {
        get => _sorted;
        set
        {
            if (_sorted == value) return;
            _sorted = value;
            if (value) Sort();
        }
    }

    [Category("Behavior")]
    [Description("The sorting comparer for the TreeView.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IComparer? TreeViewNodeSorter { get; set; }

    // --- state ------------------------------------------------------------------------------

    [Category("Appearance")]
    [Description("The currently selected node, or null if no node is selected.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeNode? SelectedNode
    {
        get => _selected;
        set => SelectNode(value, TreeViewAction.Unknown);
    }

    [Category("Appearance")]
    [Description("The first visible node in the TreeView.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TreeNode? TopNode
    {
        get
        {
            EnsureLayout();
            int row = _scroll.Y / Math.Max(1, ItemHeight);
            return row >= 0 && row < _rows.Count ? _rows[row] : null;
        }
        set
        {
            if (value != null) EnsureNodeVisible(value);
        }
    }

    [Category("Appearance")]
    [Description("The number of visible nodes in the TreeView.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int VisibleCount => Math.Max(0, ViewportRectangle.Height / Math.Max(1, ItemHeight));

    internal TreeNode? EditingNode => _editing;

    // --- events ------------------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs when the selection has been changed.")]
    public event TreeViewEventHandler? AfterSelect;

    [Category("Behavior")]
    [Description("Occurs when the selection is about to change.")]
    public event TreeViewCancelEventHandler? BeforeSelect;

    [Category("Behavior")]
    [Description("Occurs when a node has been expanded.")]
    public event TreeViewEventHandler? AfterExpand;

    [Category("Behavior")]
    [Description("Occurs when a node is about to be expanded.")]
    public event TreeViewCancelEventHandler? BeforeExpand;

    [Category("Behavior")]
    [Description("Occurs when a node has been collapsed.")]
    public event TreeViewEventHandler? AfterCollapse;

    [Category("Behavior")]
    [Description("Occurs when a node is about to be collapsed.")]
    public event TreeViewCancelEventHandler? BeforeCollapse;

    [Category("Behavior")]
    [Description("Occurs when a check box on a tree node has been checked or unchecked.")]
    public event TreeViewEventHandler? AfterCheck;

    [Category("Behavior")]
    [Description("Occurs when a check box on a tree node is about to be checked or unchecked.")]
    public event TreeViewCancelEventHandler? BeforeCheck;

    [Category("Behavior")]
    [Description("Occurs when the text of a node has been edited by the user.")]
    public event NodeLabelEditEventHandler? AfterLabelEdit;

    [Category("Behavior")]
    [Description("Occurs when the text of a node is about to be edited by the user.")]
    public event NodeLabelEditEventHandler? BeforeLabelEdit;

    [Category("Behavior")]
    [Description("Occurs when a node is clicked with the mouse.")]
    public event TreeNodeMouseClickEventHandler? NodeMouseClick;

    [Category("Behavior")]
    [Description("Occurs when a node is double-clicked with the mouse.")]
    public event TreeNodeMouseClickEventHandler? NodeMouseDoubleClick;

    [Category("Behavior")]
    [Description("Occurs in owner-draw mode, when a node needs to be drawn.")]
    public event DrawTreeNodeEventHandler? DrawNode;

    protected virtual void OnAfterSelect(TreeViewEventArgs e) => AfterSelect?.Invoke(this, e);
    protected virtual void OnBeforeSelect(TreeViewCancelEventArgs e) => BeforeSelect?.Invoke(this, e);
    protected virtual void OnAfterExpand(TreeViewEventArgs e) => AfterExpand?.Invoke(this, e);
    protected virtual void OnBeforeExpand(TreeViewCancelEventArgs e) => BeforeExpand?.Invoke(this, e);
    protected virtual void OnAfterCollapse(TreeViewEventArgs e) => AfterCollapse?.Invoke(this, e);
    protected virtual void OnBeforeCollapse(TreeViewCancelEventArgs e) => BeforeCollapse?.Invoke(this, e);
    protected virtual void OnAfterCheck(TreeViewEventArgs e) => AfterCheck?.Invoke(this, e);
    protected virtual void OnBeforeCheck(TreeViewCancelEventArgs e) => BeforeCheck?.Invoke(this, e);
    protected virtual void OnAfterLabelEdit(NodeLabelEditEventArgs e) => AfterLabelEdit?.Invoke(this, e);
    protected virtual void OnBeforeLabelEdit(NodeLabelEditEventArgs e) => BeforeLabelEdit?.Invoke(this, e);
    protected virtual void OnNodeMouseClick(TreeNodeMouseClickEventArgs e) => NodeMouseClick?.Invoke(this, e);
    protected virtual void OnNodeMouseDoubleClick(TreeNodeMouseClickEventArgs e) => NodeMouseDoubleClick?.Invoke(this, e);
    protected virtual void OnDrawNode(DrawTreeNodeEventArgs e) => DrawNode?.Invoke(this, e);

    // --- bulk updates ---------------------------------------------------------------------------

    public void BeginUpdate() => _updateCount++;

    public void EndUpdate()
    {
        if (_updateCount > 0) _updateCount--;
        if (_updateCount == 0) InvalidateLayout();
    }

    internal void NodesChanged()
    {
        if (_selected != null && _selected.TreeView != this) _selected = null;
        InvalidateLayout();
    }

    private void InvalidateLayout()
    {
        _layoutDirty = true;
        if (_updateCount == 0)
        {
            PerformRowLayout();
            Invalidate();
        }
    }

    // --- geometry ---------------------------------------------------------------------------------

    private int BorderSize => _borderStyle switch { BorderStyle.None => 0, BorderStyle.FixedSingle => 1, _ => 2 };

    private Rectangle InnerRectangle
    {
        get
        {
            int b = BorderSize;
            return new Rectangle(b, b, Math.Max(0, Width - 2 * b), Math.Max(0, Height - 2 * b));
        }
    }

    private Rectangle ViewportRectangle
    {
        get
        {
            var r = InnerRectangle;
            if (_vVisible) r.Width = Math.Max(0, r.Width - ScrollBarCore.Thickness);
            if (_hVisible) r.Height = Math.Max(0, r.Height - ScrollBarCore.Thickness);
            return r;
        }
    }

    /// <summary>Horizontal offset of a node's glyph column; root nodes are indented only when root lines show.</summary>
    private int NodeIndent(TreeNode node) => (node.Level + (_showRootLines ? 1 : 0)) * _indent;

    private int CheckWidth => _checkBoxes ? 16 : 0;

    private int ImageWidth => ImageList != null ? ImageList.ImageSize.Width + 2 : 0;

    internal void EnsureLayout()
    {
        if (_layoutDirty) PerformRowLayout();
    }

    /// <summary>Flattens the expanded part of the tree into <see cref="_rows"/> and sizes the content.</summary>
    private void PerformRowLayout()
    {
        _layoutDirty = false;
        _rows.Clear();
        Flatten(_nodes);

        int width = 0;
        foreach (var node in _rows)
        {
            width = Math.Max(width, NodeTextRight(node));
        }
        _content = new Size(width, _rows.Count * ItemHeight);
        UpdateScrollBars();
    }

    private void Flatten(TreeNodeCollection nodes)
    {
        foreach (TreeNode node in nodes)
        {
            _rows.Add(node);
            if (node.IsExpanded && node.Nodes.Count > 0) Flatten(node.Nodes);
        }
    }

    private int NodeTextRight(TreeNode node) =>
        NodeIndent(node) + CheckWidth + ImageWidth + TextRenderer.MeasureText(node.Text, node.NodeFont ?? Font).Width + 4;

    private void UpdateScrollBars()
    {
        if (!Scrollable)
        {
            _vVisible = _hVisible = false;
            return;
        }

        var inner = InnerRectangle;
        bool v = _content.Height > inner.Height;
        bool h = _content.Width > inner.Width - (v ? ScrollBarCore.Thickness : 0);
        if (h && !v) v = _content.Height > inner.Height - ScrollBarCore.Thickness;
        _vVisible = v;
        _hVisible = h;

        var viewport = ViewportRectangle;
        _vscroll.Minimum = 0;
        _vscroll.Maximum = Math.Max(0, _content.Height - 1);
        _vscroll.LargeChange = Math.Max(1, viewport.Height);
        _vscroll.SmallChange = Math.Max(1, ItemHeight);
        _vscroll.Bounds = new Rectangle(inner.Right - ScrollBarCore.Thickness, inner.Y, ScrollBarCore.Thickness, Math.Max(0, viewport.Height));
        _vscroll.Enabled = Enabled;

        _hscroll.Minimum = 0;
        _hscroll.Maximum = Math.Max(0, _content.Width - 1);
        _hscroll.LargeChange = Math.Max(1, viewport.Width);
        _hscroll.SmallChange = Math.Max(1, _indent);
        _hscroll.Bounds = new Rectangle(inner.X, inner.Bottom - ScrollBarCore.Thickness, Math.Max(0, viewport.Width), ScrollBarCore.Thickness);
        _hscroll.Enabled = Enabled;

        int maxX = Math.Max(0, _content.Width - viewport.Width);
        int maxY = Math.Max(0, _content.Height - viewport.Height);
        _scroll = new Point(Math.Clamp(_scroll.X, 0, maxX), Math.Clamp(_scroll.Y, 0, maxY));
        _vscroll.Value = _scroll.Y;
        _hscroll.Value = _scroll.X;
    }

    private void ScrollTo(int x, int y)
    {
        var viewport = ViewportRectangle;
        int maxX = Math.Max(0, _content.Width - viewport.Width);
        int maxY = Math.Max(0, _content.Height - viewport.Height);
        var next = new Point(Math.Clamp(x, 0, maxX), Math.Clamp(y, 0, maxY));
        if (next == _scroll) return;
        _scroll = next;
        _vscroll.Value = next.Y;
        _hscroll.Value = next.X;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        InvalidateLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        InvalidateLayout();
    }

    // --- node geometry -------------------------------------------------------------------------------

    internal Rectangle GetNodeBounds(TreeNode node)
    {
        EnsureLayout();
        int row = _rows.IndexOf(node);
        if (row < 0) return Rectangle.Empty;
        var viewport = ViewportRectangle;
        int y = viewport.Y + row * ItemHeight - _scroll.Y;
        int left = viewport.X + NodeIndent(node) + CheckWidth + ImageWidth - _scroll.X;
        int width = TextRenderer.MeasureText(node.Text, node.NodeFont ?? Font).Width + 3;
        return new Rectangle(left, y, width, ItemHeight);
    }

    private Rectangle RowBounds(int row)
    {
        var viewport = ViewportRectangle;
        return new Rectangle(viewport.X, viewport.Y + row * ItemHeight - _scroll.Y, viewport.Width, ItemHeight);
    }

    private Rectangle GlyphBounds(TreeNode node, Rectangle row)
    {
        int x = row.X + NodeIndent(node) - _scroll.X - _indent + (_indent - 9) / 2;
        return new Rectangle(x, row.Y + (row.Height - 9) / 2, 9, 9);
    }

    private Rectangle CheckBounds(TreeNode node, Rectangle row)
    {
        int x = row.X + NodeIndent(node) - _scroll.X;
        return new Rectangle(x + 1, row.Y + (row.Height - 13) / 2, 13, 13);
    }

    public TreeNode? GetNodeAt(Point pt) => GetNodeAt(pt.X, pt.Y);

    public TreeNode? GetNodeAt(int x, int y)
    {
        EnsureLayout();
        var viewport = ViewportRectangle;
        if (!viewport.Contains(x, y)) return null;
        int row = (y - viewport.Y + _scroll.Y) / Math.Max(1, ItemHeight);
        return row >= 0 && row < _rows.Count ? _rows[row] : null;
    }

    public TreeViewHitTestInfo HitTest(Point pt) => HitTest(pt.X, pt.Y);

    public TreeViewHitTestInfo HitTest(int x, int y)
    {
        var node = GetNodeAt(x, y);
        if (node == null) return new TreeViewHitTestInfo(null, TreeViewHitTestLocations.None);

        int row = _rows.IndexOf(node);
        var bounds = RowBounds(row);
        if (_showPlusMinus && node.Nodes.Count > 0 && GlyphBounds(node, bounds).Contains(x, y))
            return new TreeViewHitTestInfo(node, TreeViewHitTestLocations.PlusMinus);
        if (_checkBoxes && CheckBounds(node, bounds).Contains(x, y))
            return new TreeViewHitTestInfo(node, TreeViewHitTestLocations.StateImage);

        var label = GetNodeBounds(node);
        if (label.Contains(x, y)) return new TreeViewHitTestInfo(node, TreeViewHitTestLocations.Label);
        if (x < label.Left) return new TreeViewHitTestInfo(node, TreeViewHitTestLocations.Indent);
        return new TreeViewHitTestInfo(node, TreeViewHitTestLocations.RightOfLabel);
    }

    internal TreeNode? GetNextVisible(TreeNode node)
    {
        EnsureLayout();
        int i = _rows.IndexOf(node);
        return i >= 0 && i + 1 < _rows.Count ? _rows[i + 1] : null;
    }

    internal TreeNode? GetPrevVisible(TreeNode node)
    {
        EnsureLayout();
        int i = _rows.IndexOf(node);
        return i > 0 ? _rows[i - 1] : null;
    }

    internal void EnsureNodeVisible(TreeNode node)
    {
        EnsureLayout();
        int row = _rows.IndexOf(node);
        if (row < 0) return;
        var viewport = ViewportRectangle;
        int top = row * ItemHeight;
        int y = _scroll.Y;
        if (top < y) y = top;
        else if (top + ItemHeight > y + viewport.Height) y = top + ItemHeight - viewport.Height;
        ScrollTo(_scroll.X, y);
    }

    // --- expand, collapse, select, check --------------------------------------------------------------

    internal void ExpandNode(TreeNode node, TreeViewAction action)
    {
        if (node.IsExpanded) return;
        var e = new TreeViewCancelEventArgs(node, false, action);
        OnBeforeExpand(e);
        if (e.Cancel) return;
        node.SetExpandedCore(true);
        OnAfterExpand(new TreeViewEventArgs(node, action));
        InvalidateLayout();
    }

    internal void CollapseNode(TreeNode node, TreeViewAction action)
    {
        if (!node.IsExpanded) return;
        var e = new TreeViewCancelEventArgs(node, false, action);
        OnBeforeCollapse(e);
        if (e.Cancel) return;
        node.SetExpandedCore(false);
        OnAfterCollapse(new TreeViewEventArgs(node, action));
        // The selection may have been inside the collapsed branch; WinForms moves it to the parent.
        if (_selected != null && IsDescendantOf(_selected, node)) SelectNode(node, action);
        InvalidateLayout();
    }

    private static bool IsDescendantOf(TreeNode node, TreeNode ancestor)
    {
        for (var p = node.Parent; p != null; p = p.Parent)
        {
            if (p == ancestor) return true;
        }
        return false;
    }

    public void ExpandAll()
    {
        BeginUpdate();
        foreach (TreeNode node in _nodes) node.ExpandAll();
        EndUpdate();
    }

    public void CollapseAll()
    {
        BeginUpdate();
        foreach (TreeNode node in _nodes) node.Collapse(false);
        EndUpdate();
    }

    internal void SelectNode(TreeNode? node, TreeViewAction action)
    {
        if (_selected == node) return;
        var e = new TreeViewCancelEventArgs(node, false, action);
        OnBeforeSelect(e);
        if (e.Cancel) return;
        _selected = node;
        if (node != null) EnsureNodeVisible(node);
        OnAfterSelect(new TreeViewEventArgs(node, action));
        Invalidate();
    }

    internal void SetNodeChecked(TreeNode node, bool value, TreeViewAction action)
    {
        if (node.Checked == value) return;
        var e = new TreeViewCancelEventArgs(node, false, action);
        OnBeforeCheck(e);
        if (e.Cancel) return;
        node.SetCheckedCore(value);
        OnAfterCheck(new TreeViewEventArgs(node, action));
        Invalidate();
    }

    public int GetNodeCount(bool includeSubTrees)
    {
        int count = _nodes.Count;
        if (includeSubTrees)
        {
            foreach (TreeNode node in _nodes) count += node.GetNodeCount(true);
        }
        return count;
    }

    public void Sort()
    {
        var comparer = TreeViewNodeSorter ?? (IComparer)TextComparer.Instance;
        _nodes.SortCore(comparer, recursive: true);
        InvalidateLayout();
    }

    private sealed class TextComparer : IComparer
    {
        public static readonly TextComparer Instance = new();

        public int Compare(object? x, object? y) =>
            string.Compare(((TreeNode)x!).Text, ((TreeNode)y!).Text, StringComparison.CurrentCulture);
    }

    // --- label editing -------------------------------------------------------------------------------

    internal void BeginNodeEdit(TreeNode node)
    {
        if (!LabelEdit || node.TreeView != this) return;
        var e = new NodeLabelEditEventArgs(node);
        OnBeforeLabelEdit(e);
        if (e.CancelEdit) return;

        EnsureNodeVisible(node);
        var bounds = GetNodeBounds(node);
        _editing = node;
        _editor ??= CreateEditor();
        _editor.Bounds = new Rectangle(bounds.X, bounds.Y, Math.Max(60, bounds.Width + 20), Math.Max(_editor.PreferredHeight, bounds.Height));
        _editor.Text = node.Text;
        _editor.Visible = true;
        _editor.Focus();
        _editor.SelectAll();
    }

    private TextBox CreateEditor()
    {
        var editor = new TextBox { BorderStyle = BorderStyle.FixedSingle, Visible = false };
        editor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Return) { EndNodeEdit(commit: true); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { EndNodeEdit(commit: false); e.Handled = true; }
        };
        editor.LostFocus += (_, _) => EndNodeEdit(commit: true);
        Controls.Add(editor);
        return editor;
    }

    internal void EndNodeEdit(bool commit)
    {
        if (_editing == null || _editor == null) return;
        var node = _editing;
        _editing = null;
        string text = _editor.Text;
        _editor.Visible = false;

        var e = new NodeLabelEditEventArgs(node, commit ? text : null);
        OnAfterLabelEdit(e);
        if (commit && !e.CancelEdit) node.Text = text;
        if (CanFocus) Focus();
    }

    // --- input ------------------------------------------------------------------------------------------

    internal override bool IsOverlayPoint(Point p) =>
        (_vVisible && _vscroll.Bounds.Contains(p)) || (_hVisible && _hscroll.Bounds.Contains(p));

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (CanFocus) Focus();

        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible && _vscroll.Bounds.Contains(e.Location)) { _vscroll.MouseDown(e.Location); base.OnMouseDown(e); return; }
            if (_hVisible && _hscroll.Bounds.Contains(e.Location)) { _hscroll.MouseDown(e.Location); base.OnMouseDown(e); return; }
        }

        var hit = HitTest(e.Location);
        if (hit.Node != null)
        {
            switch (hit.Location)
            {
                case TreeViewHitTestLocations.PlusMinus:
                    if (hit.Node.IsExpanded) CollapseNode(hit.Node, TreeViewAction.Collapse);
                    else ExpandNode(hit.Node, TreeViewAction.Expand);
                    break;
                case TreeViewHitTestLocations.StateImage:
                    SetNodeChecked(hit.Node, !hit.Node.Checked, TreeViewAction.ByMouse);
                    break;
                case TreeViewHitTestLocations.Label:
                    SelectNode(hit.Node, TreeViewAction.ByMouse);
                    break;
                default:
                    if (_fullRowSelect) SelectNode(hit.Node, TreeViewAction.ByMouse);
                    break;
            }
            OnNodeMouseClick(new TreeNodeMouseClickEventArgs(hit.Node, e.Button, e.Clicks, e.X, e.Y));
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool left = (e.Button & MouseButtons.Left) != 0;
        if (_vVisible) _vscroll.MouseMove(e.Location, left);
        if (_hVisible) _hscroll.MouseMove(e.Location, left);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible) _vscroll.MouseUp(e.Location);
            if (_hVisible) _hscroll.MouseUp(e.Location);
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vscroll.MouseLeave();
        _hscroll.MouseLeave();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int notches = e.Delta / 120;
        if (notches == 0) notches = Math.Sign(e.Delta);
        if (_vVisible) ScrollTo(_scroll.X, _scroll.Y - notches * ItemHeight * 3);
        base.OnMouseWheel(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        // A double click on the label toggles the branch, as Explorer does.
        var point = PointToClient(MousePosition);
        var hit = HitTest(point);
        if (hit.Node != null && hit.Location == TreeViewHitTestLocations.Label)
        {
            hit.Node.Toggle();
            OnNodeMouseDoubleClick(new TreeNodeMouseClickEventArgs(hit.Node, MouseButtons.Left, 2, point.X, point.Y));
        }
        base.OnDoubleClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        EnsureLayout();
        var node = _selected ?? (_rows.Count > 0 ? _rows[0] : null);
        if (node != null)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    SelectNode(GetPrevVisible(node) ?? node, TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.Down:
                    SelectNode(GetNextVisible(node) ?? node, TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.Left:
                    if (node.IsExpanded) CollapseNode(node, TreeViewAction.ByKeyboard);
                    else if (node.Parent != null) SelectNode(node.Parent, TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.Right:
                    if (node.Nodes.Count > 0 && !node.IsExpanded) ExpandNode(node, TreeViewAction.ByKeyboard);
                    else if (node.Nodes.Count > 0) SelectNode(node.Nodes[0], TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.Home:
                    if (_rows.Count > 0) SelectNode(_rows[0], TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.End:
                    if (_rows.Count > 0) SelectNode(_rows[^1], TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.PageUp:
                case Keys.PageDown:
                    {
                        int step = Math.Max(1, VisibleCount - 1) * (e.KeyCode == Keys.PageDown ? 1 : -1);
                        int target = Math.Clamp(_rows.IndexOf(node) + step, 0, _rows.Count - 1);
                        SelectNode(_rows[target], TreeViewAction.ByKeyboard);
                        e.Handled = true;
                        break;
                    }
                case Keys.Space:
                    if (_checkBoxes)
                    {
                        SetNodeChecked(node, !node.Checked, TreeViewAction.ByKeyboard);
                        e.Handled = true;
                    }
                    break;
                case Keys.Add:
                    ExpandNode(node, TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.Subtract:
                    CollapseNode(node, TreeViewAction.ByKeyboard);
                    e.Handled = true;
                    break;
                case Keys.Multiply:
                    node.ExpandAll();
                    e.Handled = true;
                    break;
                case Keys.F2:
                    if (LabelEdit)
                    {
                        BeginNodeEdit(node);
                        e.Handled = true;
                    }
                    break;
            }
        }
        base.OnKeyDown(e);
    }

    // --- painting ----------------------------------------------------------------------------------------

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureLayout();
        var g = e.Graphics;
        var viewport = ViewportRectangle;

        var state = g.Save();
        g.IntersectClip(viewport);
        for (int row = 0; row < _rows.Count; row++)
        {
            var bounds = RowBounds(row);
            if (bounds.Bottom < viewport.Top || bounds.Top > viewport.Bottom) continue;
            PaintNode(g, _rows[row], bounds);
        }
        g.Restore(state);

        if (_borderStyle != BorderStyle.None)
        {
            using var pen = new Pen(_borderStyle == BorderStyle.Fixed3D ? Theme.WindowBorder : Theme.ButtonBorder);
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
        base.OnPaint(e);
    }

    internal override void OnPaintOverlay(Graphics g)
    {
        if (_vVisible) _vscroll.Paint(g);
        if (_hVisible) _hscroll.Paint(g);
        base.OnPaintOverlay(g);
    }

    private void PaintNode(Graphics g, TreeNode node, Rectangle row)
    {
        bool selected = _selected == node;
        bool showSelection = selected && (!HideSelection || Focused || ContainsFocus);

        if (DrawMode == TreeViewDrawMode.OwnerDrawAll)
        {
            var args = new DrawTreeNodeEventArgs(g, node, row,
                (selected ? TreeNodeStates.Selected : 0) | (node.Checked ? TreeNodeStates.Checked : 0) | (Focused && selected ? TreeNodeStates.Focused : 0));
            OnDrawNode(args);
            if (!args.DrawDefault) return;
        }

        if (_showLines) PaintLines(g, node, row);

        var label = GetNodeBounds(node);
        var highlight = _fullRowSelect ? row : label;

        if (node.IsBackColorSet && !showSelection)
        {
            using var back = new SolidBrush(node.BackColor);
            g.FillRectangle(back, highlight);
        }
        if (showSelection)
        {
            using var brush = new SolidBrush(Focused || ContainsFocus ? Theme.Highlight : Theme.HighlightInactive);
            g.FillRectangle(brush, highlight);
        }

        if (_showPlusMinus && node.Nodes.Count > 0) PaintGlyph(g, node, row);

        if (_checkBoxes)
        {
            CheckBox.PaintBox(g, CheckBounds(node, row), node.Checked ? CheckState.Checked : CheckState.Unchecked,
                Enabled, hot: false, pressed: false, flat: false, ForeColor);
        }

        if (ImageList != null)
        {
            int index = selected && node.SelectedImageIndex >= 0 ? node.SelectedImageIndex
                : node.ImageIndex >= 0 ? node.ImageIndex
                : selected && SelectedImageIndex >= 0 ? SelectedImageIndex
                : ImageIndex;
            if (index >= 0)
            {
                var size = ImageList.ImageSize;
                ImageList.Draw(g, label.X - ImageWidth, row.Y + (row.Height - size.Height) / 2, size.Width, size.Height, index);
            }
        }

        if (DrawMode == TreeViewDrawMode.OwnerDrawText)
        {
            var args = new DrawTreeNodeEventArgs(g, node, label, selected ? TreeNodeStates.Selected : 0);
            OnDrawNode(args);
            if (!args.DrawDefault) return;
        }

        var color = showSelection ? Theme.HighlightText : node.ForeColor;
        TextRenderer.DrawText(g, node.Text, node.NodeFont ?? Font, label, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

        if (selected && Focused && ShowFocusCues && !_fullRowSelect) ControlPaint.DrawFocusRectangle(g, label);
    }

    private void PaintGlyph(Graphics g, TreeNode node, Rectangle row)
    {
        var box = GlyphBounds(node, row);
        using var pen = new Pen(EffectiveLineColor);
        g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
        using var mark = new Pen(ForeColor);
        int cy = box.Y + box.Height / 2;
        int cx = box.X + box.Width / 2;
        g.DrawLine(mark, box.X + 2, cy, box.Right - 3, cy);
        if (!node.IsExpanded) g.DrawLine(mark, cx, box.Y + 2, cx, box.Bottom - 3);
    }

    /// <summary>The dotted tree lines: a vertical run through each ancestor column and an elbow into the node.</summary>
    private void PaintLines(Graphics g, TreeNode node, Rectangle row)
    {
        using var pen = new Pen(EffectiveLineColor) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
        int half = row.Y + row.Height / 2;
        int column = row.X + NodeIndent(node) - _scroll.X - _indent / 2;

        if (node.Level > 0 || _showRootLines)
        {
            g.DrawLine(pen, column, half, column + _indent / 2 - (_showPlusMinus && node.Nodes.Count > 0 ? 5 : 0), half);
            bool last = node.NextNode == null;
            g.DrawLine(pen, column, row.Y, column, last ? half : row.Bottom);
        }

        // Continue the vertical line of every ancestor that still has siblings below it.
        for (var parent = node.Parent; parent != null; parent = parent.Parent)
        {
            if (parent.NextNode == null) continue;
            int x = row.X + NodeIndent(parent) - _scroll.X - _indent / 2;
            g.DrawLine(pen, x, row.Y, x, row.Bottom);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _vscroll.Dispose();
            _hscroll.Dispose();
        }
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + ", Nodes.Count: " + _nodes.Count;

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event PaintEventHandler? Paint
    {
        add => base.Paint += value;
        remove => base.Paint -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }
}
