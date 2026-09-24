using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The list control with four (five, counting Tile) faces. Items live in a single collection and
/// are placed by <see cref="PerformItemLayout"/> into content coordinates; the viewport scrolls
/// over that content, so every view shares the same hit-test, selection and keyboard code.
/// </summary>
[DefaultProperty(nameof(Items))]
[DefaultEvent(nameof(SelectedIndexChanged))]
public class ListView : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    private readonly ListViewItemCollection _items;
    private readonly ColumnHeaderCollection _columns;
    private readonly ListViewGroupCollection _groups;
    private readonly List<ListViewItem> _selection = new();

    // Layout, in content coordinates; index-parallel with _items.
    private readonly List<Rectangle> _layout = new();
    private readonly List<(ListViewGroup Group, Rectangle Bounds)> _groupBands = new();
    private readonly List<int> _order = new();
    private Size _content;
    private bool _layoutDirty = true;

    private ListViewItem? _focused;
    private ListViewItem? _anchor;
    private Point _scroll;
    private readonly ScrollBarCore _vscroll;
    private readonly ScrollBarCore _hscroll;
    private bool _vVisible;
    private bool _hVisible;
    private int _updateCount;

    private View _view = View.LargeIcon;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private ColumnHeaderStyle _headerStyle = ColumnHeaderStyle.Clickable;
    private SortOrder _sorting = SortOrder.None;
    private bool _checkBoxes;
    private bool _gridLines;
    private bool _fullRowSelect;
    private bool _multiSelect = true;
    private bool _showGroups = true;
    private Size _tileSize = new Size(158, 40);
    private ImageList? _largeImages;
    private ImageList? _smallImages;
    private ImageList? _stateImages;
    private int _pressedColumn = -1;
    private int _hotColumn = -1;
    private TextBox? _editor;
    private ListViewItem? _editing;

    public ListView()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
        _items = new ListViewItemCollection(this);
        _columns = new ColumnHeaderCollection(this);
        _groups = new ListViewGroupCollection(this);
        _vscroll = new ScrollBarCore(this, vertical: true, (v, _) => ScrollTo(_scroll.X, v));
        _hscroll = new ScrollBarCore(this, vertical: false, (v, _) => ScrollTo(v, _scroll.Y));
    }

    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Window;
        set => base.BackColor = value;
    }

    protected override Size DefaultSize => new Size(121, 97);

    // --- appearance ---------------------------------------------------------------------

    [Category("Appearance")]
    [Description("Selects one of five different views that items can be shown in.")]
    [DefaultValue(View.LargeIcon)]
    public View View
    {
        get => _view;
        set
        {
            if (_view == value) return;
            _view = value;
            _scroll = Point.Empty;
            InvalidateLayout();
        }
    }

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
    [Description("The style of the column headers in Details view.")]
    [DefaultValue(ColumnHeaderStyle.Clickable)]
    public ColumnHeaderStyle HeaderStyle
    {
        get => _headerStyle;
        set
        {
            if (_headerStyle == value) return;
            _headerStyle = value;
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("Indicates whether check boxes are displayed beside items.")]
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

    [Category("Appearance")]
    [Description("Displays grid lines around items and SubItems. Only shown when in Details view.")]
    [DefaultValue(false)]
    public bool GridLines
    {
        get => _gridLines;
        set { _gridLines = value; Invalidate(); }
    }

    [Category("Appearance")]
    [Description("Indicates whether all SubItems are highlighted along with the item when selected.")]
    [DefaultValue(false)]
    public bool FullRowSelect
    {
        get => _fullRowSelect;
        set { _fullRowSelect = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Allows multiple items to be selected.")]
    [DefaultValue(true)]
    public bool MultiSelect
    {
        get => _multiSelect;
        set
        {
            _multiSelect = value;
            if (!value && _selection.Count > 1) SelectOnly(_selection[0]);
        }
    }

    /// <summary>False since .NET 5 (WinForms on .NET Framework defaulted to true); verified by the compat diff.</summary>
    [Category("Behavior")]
    [Description("Removes highlighting from the selected item when the control does not have focus.")]
    [DefaultValue(false)]
    public bool HideSelection { get; set; }

    [Category("Behavior")]
    [Description("Allows item labels to be edited in place by the user.")]
    [DefaultValue(false)]
    public bool LabelEdit { get; set; }

    [Category("Behavior")]
    [Description("Determines whether label text can wrap to a new line.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool LabelWrap { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates whether the control will display scroll bars if it contains more items than can fit in the client area.")]
    [DefaultValue(true)]
    public bool Scrollable { get; set; } = true;

    [Category("Behavior")]
    [Description("Allows items to be selected by hovering over them with the mouse.")]
    [DefaultValue(false)]
    public bool HoverSelection { get; set; }

    [Category("Behavior")]
    [Description("Allows ListViewItems to display ToolTips.")]
    [DefaultValue(false)]
    public bool ShowItemToolTips { get; set; }

    [DefaultValue(true)]
    [Browsable(false)]
    public bool UseCompatibleStateImageBehavior { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether the system or the user paints items/SubItems.")]
    [DefaultValue(false)]
    public bool OwnerDraw { get; set; }

    [Category("Behavior")]
    [Description("Indicates how items are aligned within the ListView.")]
    [DefaultValue(ListViewAlignment.Top)]
    [Localizable(true)]
    public ListViewAlignment Alignment { get; set; } = ListViewAlignment.Top;

    [Category("Behavior")]
    [Description("Indicates whether the control will display the items in group form.")]
    [DefaultValue(true)]
    public bool ShowGroups
    {
        get => _showGroups;
        set
        {
            if (_showGroups == value) return;
            _showGroups = value;
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("The size of the tile in Tile view.")]
    public Size TileSize
    {
        get => _tileSize;
        set
        {
            if (_tileSize == value) return;
            _tileSize = value;
            InvalidateLayout();
        }
    }

    internal bool ShouldSerializeTileSize() => _tileSize != new Size(158, 40);

    [Category("Behavior")]
    [Description("The ImageList control used by the ListView for images in Large Icon View.")]
    [DefaultValue(null)]
    public ImageList? LargeImageList
    {
        get => _largeImages;
        set { _largeImages = value; InvalidateLayout(); }
    }

    [Category("Behavior")]
    [Description("The ImageList control used by the ListView for images in all views except for the large icon view.")]
    [DefaultValue(null)]
    public ImageList? SmallImageList
    {
        get => _smallImages;
        set { _smallImages = value; InvalidateLayout(); }
    }

    [Category("Behavior")]
    [Description("The ImageList control used by the ListView for custom states.")]
    [DefaultValue(null)]
    public ImageList? StateImageList
    {
        get => _stateImages;
        set { _stateImages = value; InvalidateLayout(); }
    }

    [Category("Behavior")]
    [Description("Indicates the manner in which items are to be sorted.")]
    [DefaultValue(SortOrder.None)]
    public SortOrder Sorting
    {
        get => _sorting;
        set
        {
            if (_sorting == value) return;
            _sorting = value;
            Sort();
        }
    }

    [Category("Behavior")]
    [Description("The sorting comparer for this view.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IComparer? ListViewItemSorter { get; set; }

    // --- collections ----------------------------------------------------------------------

    [Category("Behavior")]
    [Description("The items in the ListView.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ListViewItemCollection Items => _items;

    [Category("Behavior")]
    [Description("The columns shown in Details view.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ColumnHeaderCollection Columns => _columns;

    [Category("Behavior")]
    [Description("The groups in the ListView.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ListViewGroupCollection Groups => _groups;

    [Category("Appearance")]
    [Description("A collection of items that are currently selected in the ListView.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SelectedListViewItemCollection SelectedItems => new SelectedListViewItemCollection(this);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SelectedIndexCollection SelectedIndices => new SelectedIndexCollection(this);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CheckedListViewItemCollection CheckedItems => new CheckedListViewItemCollection(this);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public CheckedIndexCollection CheckedIndices => new CheckedIndexCollection(this);

    internal IReadOnlyList<ListViewItem> SelectionCore => _selection;

    [Category("Appearance")]
    [Description("The ListView item that currently has the user focus.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ListViewItem? FocusedItem
    {
        get => _focused;
        set => SetFocusedItem(value);
    }

    [Category("Appearance")]
    [Description("The first item that is visible to the user.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ListViewItem? TopItem
    {
        get
        {
            EnsureLayout();
            var area = ItemsRectangle;
            foreach (int index in _order)
            {
                if (ContentToClient(_layout[index]).Bottom > area.Top) return _items[index];
            }
            return _items.Count > 0 ? _items[0] : null;
        }
        set
        {
            if (value != null) EnsureVisible(value.Index);
        }
    }

    // --- events ------------------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs whenever the 'SelectedIndex' property for this ListView changes.")]
    public event EventHandler? SelectedIndexChanged;

    [Category("Behavior")]
    [Description("Event raised when the selection state of an item has changed.")]
    public event ListViewItemSelectionChangedEventHandler? ItemSelectionChanged;

    [Category("Action")]
    [Description("Occurs when a column header is clicked.")]
    public event ColumnClickEventHandler? ColumnClick;

    [Category("Behavior")]
    [Description("Indicates that an item is about to have its checked state changed. The value is not updated until after the event occurs.")]
    public event ItemCheckEventHandler? ItemCheck;

    [Category("Behavior")]
    [Description("Event raised when the checked property of a ListView item changes.")]
    public event ItemCheckedEventHandler? ItemChecked;

    [Category("Action")]
    [Description("Occurs when an item is activated.")]
    public event EventHandler? ItemActivate;

    [Category("Behavior")]
    [Description("Occurs when the text of an item is about to be edited by the user.")]
    public event LabelEditEventHandler? BeforeLabelEdit;

    [Category("Behavior")]
    [Description("Occurs when the text of an item has been edited by the user.")]
    public event LabelEditEventHandler? AfterLabelEdit;

    [Category("Behavior")]
    [Description("Occurs in owner-draw mode, when an item needs to be drawn.")]
    public event DrawListViewItemEventHandler? DrawItem;

    [Category("Behavior")]
    [Description("Occurs in owner-draw mode, when a SubItem (Details view only) needs to be drawn.")]
    public event DrawListViewSubItemEventHandler? DrawSubItem;

    [Category("Behavior")]
    [Description("Occurs in owner draw mode, when a column header needs to be drawn.")]
    public event DrawListViewColumnHeaderEventHandler? DrawColumnHeader;

    protected virtual void OnSelectedIndexChanged(EventArgs e) => SelectedIndexChanged?.Invoke(this, e);
    protected virtual void OnItemSelectionChanged(ListViewItemSelectionChangedEventArgs e) => ItemSelectionChanged?.Invoke(this, e);
    protected virtual void OnColumnClick(ColumnClickEventArgs e) => ColumnClick?.Invoke(this, e);
    protected virtual void OnItemCheck(ItemCheckEventArgs ice) => ItemCheck?.Invoke(this, ice);
    protected virtual void OnItemChecked(ItemCheckedEventArgs e) => ItemChecked?.Invoke(this, e);
    protected virtual void OnItemActivate(EventArgs e) => ItemActivate?.Invoke(this, e);
    protected virtual void OnBeforeLabelEdit(LabelEditEventArgs e) => BeforeLabelEdit?.Invoke(this, e);
    protected virtual void OnAfterLabelEdit(LabelEditEventArgs e) => AfterLabelEdit?.Invoke(this, e);
    protected virtual void OnDrawItem(DrawListViewItemEventArgs e) => DrawItem?.Invoke(this, e);
    protected virtual void OnDrawSubItem(DrawListViewSubItemEventArgs e) => DrawSubItem?.Invoke(this, e);
    protected virtual void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e) => DrawColumnHeader?.Invoke(this, e);

    // --- bulk updates --------------------------------------------------------------------------

    public void BeginUpdate() => _updateCount++;

    public void EndUpdate()
    {
        if (_updateCount > 0) _updateCount--;
        if (_updateCount == 0) InvalidateLayout();
    }

    internal void ItemsChanged()
    {
        _selection.RemoveAll(i => i.ListView != this);
        if (_focused != null && _focused.ListView != this) _focused = null;
        InvalidateLayout();
    }

    internal void ColumnsChanged() => InvalidateLayout();

    private void InvalidateLayout()
    {
        _layoutDirty = true;
        if (_updateCount == 0)
        {
            PerformItemLayout();
            Invalidate();
        }
    }

    // --- geometry -------------------------------------------------------------------------------

    private int BorderSize => _borderStyle switch { BorderStyle.None => 0, BorderStyle.FixedSingle => 1, _ => 2 };

    private Rectangle InnerRectangle
    {
        get
        {
            int b = BorderSize;
            return new Rectangle(b, b, Math.Max(0, Width - 2 * b), Math.Max(0, Height - 2 * b));
        }
    }

    /// <summary>The inner rectangle minus whichever scroll bars are showing.</summary>
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

    internal int HeaderHeight => _view == View.Details && _headerStyle != ColumnHeaderStyle.None ? Font.Height + 6 : 0;

    private Rectangle HeaderRectangle
    {
        get
        {
            var v = ViewportRectangle;
            return new Rectangle(v.X, v.Y, v.Width, HeaderHeight);
        }
    }

    /// <summary>Where items are drawn: the viewport below the column header.</summary>
    private Rectangle ItemsRectangle
    {
        get
        {
            var v = ViewportRectangle;
            int h = HeaderHeight;
            return new Rectangle(v.X, v.Y + h, v.Width, Math.Max(0, v.Height - h));
        }
    }

    internal int RowHeight
    {
        get
        {
            int image = _view == View.Details || _view == View.SmallIcon || _view == View.List ? _smallImages?.ImageSize.Height ?? 0 : 0;
            int state = _checkBoxes ? 16 : 0;
            return Math.Max(Font.Height + 2, Math.Max(image, state) + 2);
        }
    }

    private Size LargeIconSize => _largeImages?.ImageSize ?? new Size(32, 32);

    private Size SmallIconSize => _smallImages?.ImageSize ?? Size.Empty;

    private int CheckWidth => _checkBoxes ? 16 + 2 : 0;

    private Rectangle ContentToClient(Rectangle r)
    {
        var area = ItemsRectangle;
        return new Rectangle(r.X + area.X - _scroll.X, r.Y + area.Y - _scroll.Y, r.Width, r.Height);
    }

    private Point ClientToContent(Point p)
    {
        var area = ItemsRectangle;
        return new Point(p.X - area.X + _scroll.X, p.Y - area.Y + _scroll.Y);
    }

    // --- layout ------------------------------------------------------------------------------------

    internal void EnsureLayout()
    {
        if (_layoutDirty) PerformItemLayout();
    }

    /// <summary>Places every item in content coordinates for the current <see cref="View"/>.</summary>
    private void PerformItemLayout()
    {
        _layoutDirty = false;
        _layout.Clear();
        _groupBands.Clear();
        _order.Clear();
        for (int i = 0; i < _items.Count; i++) _layout.Add(Rectangle.Empty);

        BuildDisplayOrder();

        var viewport = ViewportRectangle;
        int usableWidth = Math.Max(1, viewport.Width);
        int usableHeight = Math.Max(1, viewport.Height - HeaderHeight);

        _content = _view switch
        {
            View.Details => LayoutDetails(),
            View.List => LayoutList(usableHeight),
            View.SmallIcon => LayoutGrid(SmallCellSize(), usableWidth),
            View.Tile => LayoutGrid(_tileSize, usableWidth),
            _ => LayoutGrid(LargeCellSize(), usableWidth),
        };

        UpdateScrollBars();
    }

    /// <summary>Items in collection order, or grouped when groups are on and used.</summary>
    private void BuildDisplayOrder()
    {
        bool grouped = _showGroups && _groups.Count > 0 && _view != View.List;
        if (!grouped)
        {
            for (int i = 0; i < _items.Count; i++) _order.Add(i);
            return;
        }

        foreach (ListViewGroup group in _groups)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (ReferenceEquals(_items[i].Group, group)) _order.Add(i);
            }
        }
        // Items without a group (or in a group that is not in the collection) come last.
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_order.Contains(i)) _order.Add(i);
        }
    }

    private ListViewGroup? GroupOf(int index) => _showGroups && _groups.Count > 0 && _view != View.List ? _items[index].Group : null;

    private int GroupHeaderHeight => Font.Height + 6;

    private Size LargeCellSize()
    {
        var icon = LargeIconSize;
        int width = Math.Max(icon.Width + 16, 76);
        int height = icon.Height + Font.Height * (LabelWrap ? 2 : 1) + 8;
        return new Size(width, height);
    }

    private Size SmallCellSize()
    {
        int textWidth = 0;
        foreach (ListViewItem item in _items)
        {
            textWidth = Math.Max(textWidth, TextRenderer.MeasureText(item.Text, item.Font).Width);
        }
        int width = CheckWidth + SmallIconSize.Width + textWidth + 8;
        return new Size(Math.Max(width, 40), RowHeight);
    }

    private Size LayoutDetails()
    {
        int totalWidth = 0;
        foreach (ColumnHeader column in _columns) totalWidth += column.Width;

        int rowHeight = RowHeight;
        int y = 0;
        ListViewGroup? currentGroup = null;
        foreach (int index in _order)
        {
            var group = GroupOf(index);
            if (!ReferenceEquals(group, currentGroup) && group != null)
            {
                currentGroup = group;
                _groupBands.Add((group, new Rectangle(0, y, Math.Max(totalWidth, 1), GroupHeaderHeight)));
                y += GroupHeaderHeight;
            }
            _layout[index] = new Rectangle(0, y, Math.Max(totalWidth, 1), rowHeight);
            y += rowHeight;
        }
        return new Size(totalWidth, y);
    }

    private Size LayoutList(int usableHeight)
    {
        var cell = SmallCellSize();
        int rows = Math.Max(1, usableHeight / Math.Max(1, cell.Height));
        int columns = 0;
        for (int n = 0; n < _order.Count; n++)
        {
            int column = n / rows;
            int row = n % rows;
            _layout[_order[n]] = new Rectangle(column * cell.Width, row * cell.Height, cell.Width, cell.Height);
            columns = Math.Max(columns, column + 1);
        }
        return new Size(columns * cell.Width, Math.Min(_order.Count, rows) * cell.Height);
    }

    private Size LayoutGrid(Size cell, int usableWidth)
    {
        int columns = Math.Max(1, usableWidth / Math.Max(1, cell.Width));
        int x = 0, y = 0, column = 0, maxRight = 0;
        ListViewGroup? currentGroup = null;

        foreach (int index in _order)
        {
            var group = GroupOf(index);
            if (!ReferenceEquals(group, currentGroup) && group != null)
            {
                if (column != 0)
                {
                    column = 0;
                    x = 0;
                    y += cell.Height;
                }
                currentGroup = group;
                _groupBands.Add((group, new Rectangle(0, y, Math.Max(usableWidth, cell.Width), GroupHeaderHeight)));
                y += GroupHeaderHeight;
            }

            _layout[index] = new Rectangle(x, y, cell.Width, cell.Height);
            maxRight = Math.Max(maxRight, x + cell.Width);
            column++;
            if (column >= columns)
            {
                column = 0;
                x = 0;
                y += cell.Height;
            }
            else
            {
                x += cell.Width;
            }
        }
        if (column != 0) y += cell.Height;
        return new Size(maxRight, y);
    }

    private void UpdateScrollBars()
    {
        if (!Scrollable)
        {
            _vVisible = _hVisible = false;
            return;
        }

        var inner = InnerRectangle;
        int header = HeaderHeight;
        bool v = _content.Height > inner.Height - header;
        bool h = _content.Width > inner.Width - (v ? ScrollBarCore.Thickness : 0);
        if (h && !v) v = _content.Height > inner.Height - header - ScrollBarCore.Thickness;
        _vVisible = v;
        _hVisible = h;

        var viewport = ViewportRectangle;
        int pageHeight = Math.Max(1, viewport.Height - header);
        _vscroll.Minimum = 0;
        _vscroll.Maximum = Math.Max(0, _content.Height - 1);
        _vscroll.LargeChange = pageHeight;
        _vscroll.SmallChange = Math.Max(1, RowHeight);
        _vscroll.Bounds = new Rectangle(inner.Right - ScrollBarCore.Thickness, inner.Y, ScrollBarCore.Thickness, Math.Max(0, viewport.Height));
        _vscroll.Enabled = Enabled;

        _hscroll.Minimum = 0;
        _hscroll.Maximum = Math.Max(0, _content.Width - 1);
        _hscroll.LargeChange = Math.Max(1, viewport.Width);
        _hscroll.SmallChange = Math.Max(1, RowHeight);
        _hscroll.Bounds = new Rectangle(inner.X, inner.Bottom - ScrollBarCore.Thickness, Math.Max(0, viewport.Width), ScrollBarCore.Thickness);
        _hscroll.Enabled = Enabled;

        int maxX = Math.Max(0, _content.Width - viewport.Width);
        int maxY = Math.Max(0, _content.Height - pageHeight);
        _scroll = new Point(Math.Clamp(_scroll.X, 0, maxX), Math.Clamp(_scroll.Y, 0, maxY));
        _vscroll.Value = _scroll.Y;
        _hscroll.Value = _scroll.X;
    }

    private void ScrollTo(int x, int y)
    {
        var viewport = ViewportRectangle;
        int maxX = Math.Max(0, _content.Width - viewport.Width);
        int maxY = Math.Max(0, _content.Height - Math.Max(1, viewport.Height - HeaderHeight));
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

    // --- item geometry and hit-testing ------------------------------------------------------------

    public Rectangle GetItemRect(int index) => GetItemRect(index, ItemBoundsPortion.Entire);

    public Rectangle GetItemRect(int index, ItemBoundsPortion portion)
    {
        if (index < 0 || index >= _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        EnsureLayout();
        var bounds = ContentToClient(_layout[index]);
        return portion switch
        {
            ItemBoundsPortion.Icon => IconRect(bounds),
            ItemBoundsPortion.Label => LabelRect(index, bounds),
            _ => bounds,
        };
    }

    private Rectangle IconRect(Rectangle bounds)
    {
        if (_view is View.LargeIcon or View.Tile)
        {
            var icon = LargeIconSize;
            return _view == View.Tile
                ? new Rectangle(bounds.X + 2, bounds.Y + (bounds.Height - icon.Height) / 2, icon.Width, icon.Height)
                : new Rectangle(bounds.X + (bounds.Width - icon.Width) / 2, bounds.Y + 4, icon.Width, icon.Height);
        }
        var small = SmallIconSize;
        return new Rectangle(bounds.X + CheckWidth, bounds.Y + (bounds.Height - small.Height) / 2, small.Width, small.Height);
    }

    private Rectangle LabelRect(int index, Rectangle bounds)
    {
        if (_view is View.LargeIcon)
        {
            int top = bounds.Y + 4 + LargeIconSize.Height + 2;
            return new Rectangle(bounds.X + 1, top, bounds.Width - 2, Math.Max(0, bounds.Bottom - top));
        }
        if (_view == View.Tile) return TileText(index, bounds).Block;
        int x = bounds.X + CheckWidth + SmallIconSize.Width + (SmallIconSize.Width > 0 ? 2 : 0);
        int width = _view == View.Details && _columns.Count > 0
            ? Math.Max(0, bounds.X + _columns[0].Width - x)
            : Math.Max(0, bounds.Right - x);
        return new Rectangle(x, bounds.Y, width, bounds.Height);
    }

    /// <summary>
    /// The text block of a Tile item: the label on the first line and one line per sub-item after
    /// it (at most one per column), centred beside the icon - the way WinForms stacks tile text.
    /// </summary>
    private (Rectangle Block, int LineHeight, int Lines) TileText(int index, Rectangle bounds)
    {
        var item = _items[index];
        int lineHeight = Math.Max(1, item.Font.Height);
        int wanted = Math.Max(1, Math.Min(item.SubItems.Count, Math.Max(1, _columns.Count)));
        int lines = Math.Max(1, Math.Min(wanted, bounds.Height / lineHeight));
        int left = bounds.X + 2 + LargeIconSize.Width + 4;
        int top = bounds.Y + Math.Max(0, (bounds.Height - lines * lineHeight) / 2);
        return (new Rectangle(left, top, Math.Max(0, bounds.Right - left - 2), lines * lineHeight), lineHeight, lines);
    }

    private Rectangle CheckRect(Rectangle bounds) =>
        new Rectangle(bounds.X + 2, bounds.Y + (bounds.Height - 13) / 2, 13, 13);

    public ListViewItem? GetItemAt(int x, int y)
    {
        EnsureLayout();
        if (!ItemsRectangle.Contains(x, y)) return null;
        var point = new Point(x, y);
        foreach (int index in _order)
        {
            if (ContentToClient(_layout[index]).Contains(point)) return _items[index];
        }
        return null;
    }

    public ListViewHitTestInfo HitTest(Point point) => HitTest(point.X, point.Y);

    public ListViewHitTestInfo HitTest(int x, int y)
    {
        var item = GetItemAt(x, y);
        if (item == null) return new ListViewHitTestInfo(null, null, ListViewHitTestLocations.None);

        var bounds = GetItemRect(item.Index);
        var location = ListViewHitTestLocations.None;
        if (_checkBoxes && CheckRect(bounds).Contains(x, y)) location = ListViewHitTestLocations.StateImage;
        else if (IconRect(bounds).Contains(x, y) && IconRect(bounds).Width > 0) location = ListViewHitTestLocations.Image;
        else location = ListViewHitTestLocations.Label;

        ListViewItem.ListViewSubItem? subItem = item.SubItems.Count > 0 ? item.SubItems[0] : null;
        if (_view == View.Details)
        {
            int left = bounds.X;
            for (int c = 0; c < _columns.Count; c++)
            {
                int right = left + _columns[c].Width;
                if (x >= left && x < right)
                {
                    subItem = c < item.SubItems.Count ? item.SubItems[c] : null;
                    break;
                }
                left = right;
            }
        }
        return new ListViewHitTestInfo(item, subItem, location);
    }

    public ListViewItem? FindItemWithText(string text) => FindItemWithText(text, false, 0, true);

    public ListViewItem? FindItemWithText(string text, bool includeSubItemsInSearch, int startIndex, bool isPrefixSearch)
    {
        if (string.IsNullOrEmpty(text)) return null;
        for (int i = Math.Max(0, startIndex); i < _items.Count; i++)
        {
            var item = _items[i];
            if (Matches(item.Text)) return item;
            if (!includeSubItemsInSearch) continue;
            for (int s = 1; s < item.SubItems.Count; s++)
            {
                if (Matches(item.SubItems[s].Text)) return item;
            }
        }
        return null;

        bool Matches(string candidate) => isPrefixSearch
            ? candidate.StartsWith(text, StringComparison.CurrentCultureIgnoreCase)
            : string.Equals(candidate, text, StringComparison.CurrentCultureIgnoreCase);
    }

    public void EnsureVisible(int index)
    {
        if (index < 0 || index >= _items.Count) return;
        EnsureLayout();
        var target = _layout[index];
        var area = ItemsRectangle;
        int x = _scroll.X, y = _scroll.Y;
        if (target.Bottom > y + area.Height) y = target.Bottom - area.Height;
        if (target.Top < y) y = target.Top;
        if (target.Right > x + area.Width) x = target.Right - area.Width;
        if (target.Left < x) x = target.Left;
        ScrollTo(x, y);
    }

    public void Clear()
    {
        _items.Clear();
        _columns.Clear();
    }

    /// <summary>The width a column needs for its header text, its content, or the wider of the two.</summary>
    internal int MeasureColumnWidth(ColumnHeader column, ColumnHeaderAutoResizeStyle style)
    {
        int index = _columns.IndexOf(column);
        if (index < 0) return column.Width;
        int width = 0;
        if (style != ColumnHeaderAutoResizeStyle.ColumnContent)
        {
            width = TextRenderer.MeasureText(column.Text, Font).Width + 12;
        }
        if (style != ColumnHeaderAutoResizeStyle.HeaderSize)
        {
            foreach (ListViewItem item in _items)
            {
                if (index >= item.SubItems.Count) continue;
                int cell = TextRenderer.MeasureText(item.SubItems[index].Text, item.SubItems[index].Font).Width + 8;
                if (index == 0) cell += CheckWidth + SmallIconSize.Width;
                width = Math.Max(width, cell);
            }
        }
        return Math.Max(width, 12);
    }

    public void AutoResizeColumn(int columnIndex, ColumnHeaderAutoResizeStyle headerAutoResize) =>
        _columns[columnIndex].AutoResize(headerAutoResize);

    public void AutoResizeColumns(ColumnHeaderAutoResizeStyle headerAutoResize)
    {
        BeginUpdate();
        foreach (ColumnHeader column in _columns) column.AutoResize(headerAutoResize);
        EndUpdate();
    }

    // --- selection and checking ----------------------------------------------------------------

    internal bool IsItemSelected(ListViewItem item) => _selection.Contains(item);

    internal void SetItemSelected(ListViewItem item, bool selected)
    {
        if (selected == _selection.Contains(item)) return;
        if (selected)
        {
            if (!_multiSelect) _selection.Clear();
            _selection.Add(item);
        }
        else
        {
            _selection.Remove(item);
        }
        OnItemSelectionChanged(new ListViewItemSelectionChangedEventArgs(item, item.Index, selected));
        OnSelectedIndexChanged(EventArgs.Empty);
        Invalidate();
    }

    private void SelectOnly(ListViewItem item)
    {
        bool changed = _selection.Count != 1 || _selection[0] != item;
        var previous = _selection.ToArray();
        _selection.Clear();
        _selection.Add(item);
        if (changed)
        {
            foreach (var old in previous)
            {
                if (old != item) OnItemSelectionChanged(new ListViewItemSelectionChangedEventArgs(old, old.Index, false));
            }
            OnItemSelectionChanged(new ListViewItemSelectionChangedEventArgs(item, item.Index, true));
            OnSelectedIndexChanged(EventArgs.Empty);
        }
        Invalidate();
    }

    private void SelectRange(ListViewItem from, ListViewItem to)
    {
        EnsureLayout();
        int a = _order.IndexOf(from.Index);
        int b = _order.IndexOf(to.Index);
        if (a < 0 || b < 0) return;
        if (a > b) (a, b) = (b, a);
        _selection.Clear();
        for (int n = a; n <= b; n++) _selection.Add(_items[_order[n]]);
        OnSelectedIndexChanged(EventArgs.Empty);
        Invalidate();
    }

    internal void SetFocusedItem(ListViewItem? item)
    {
        if (_focused == item) return;
        _focused = item;
        Invalidate();
    }

    internal void SetItemChecked(ListViewItem item, bool value)
    {
        var e = new ItemCheckEventArgs(item.Index, value ? CheckState.Checked : CheckState.Unchecked,
            item.Checked ? CheckState.Checked : CheckState.Unchecked);
        OnItemCheck(e);
        bool result = e.NewValue != CheckState.Unchecked;
        if (result == item.Checked) return;
        item.SetCheckedCore(result);
        OnItemChecked(new ItemCheckedEventArgs(item));
        Invalidate();
    }

    public void Sort()
    {
        if (_items.Count == 0) return;
        var comparer = ListViewItemSorter;
        if (comparer == null)
        {
            if (_sorting == SortOrder.None) return;
            comparer = new TextComparer(_sorting);
        }
        _items.SortCore(comparer);
        InvalidateLayout();
    }

    private sealed class TextComparer : IComparer
    {
        private readonly SortOrder _order;

        public TextComparer(SortOrder order) => _order = order;

        public int Compare(object? x, object? y)
        {
            int result = string.Compare(((ListViewItem)x!).Text, ((ListViewItem)y!).Text, StringComparison.CurrentCulture);
            return _order == SortOrder.Descending ? -result : result;
        }
    }

    // --- label editing ------------------------------------------------------------------------

    internal void BeginLabelEdit(ListViewItem item)
    {
        if (!LabelEdit || item.ListView != this) return;
        var e = new LabelEditEventArgs(item.Index);
        OnBeforeLabelEdit(e);
        if (e.CancelEdit) return;

        EnsureVisible(item.Index);
        var bounds = GetItemRect(item.Index, ItemBoundsPortion.Label);
        _editing = item;
        _editor ??= CreateEditor();
        _editor.Bounds = new Rectangle(bounds.X, bounds.Y, Math.Max(40, bounds.Width), Math.Max(_editor.PreferredHeight, bounds.Height));
        _editor.Text = item.Text;
        _editor.Visible = true;
        _editor.Focus();
        _editor.SelectAll();
    }

    private TextBox CreateEditor()
    {
        var editor = new TextBox { BorderStyle = BorderStyle.FixedSingle, Visible = false };
        editor.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Return) { EndLabelEdit(commit: true); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { EndLabelEdit(commit: false); e.Handled = true; }
        };
        editor.LostFocus += (_, _) => EndLabelEdit(commit: true);
        Controls.Add(editor);
        return editor;
    }

    private void EndLabelEdit(bool commit)
    {
        if (_editing == null || _editor == null) return;
        var item = _editing;
        _editing = null;
        string text = _editor.Text;
        _editor.Visible = false;

        var e = new LabelEditEventArgs(item.Index, commit ? text : null);
        OnAfterLabelEdit(e);
        if (commit && !e.CancelEdit) item.Text = text;
        if (CanFocus) Focus();
    }

    // --- input ------------------------------------------------------------------------------------

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

            int column = ColumnFromPoint(e.Location);
            if (column >= 0)
            {
                _pressedColumn = column;
                Invalidate(HeaderRectangle);
                base.OnMouseDown(e);
                return;
            }
        }

        var item = GetItemAt(e.X, e.Y);
        if (item != null && e.Button == MouseButtons.Left)
        {
            if (_checkBoxes && CheckRect(GetItemRect(item.Index)).Contains(e.Location))
            {
                SetItemChecked(item, !item.Checked);
                base.OnMouseDown(e);
                return;
            }

            bool control = (ModifierKeys & Keys.Control) != 0;
            bool shift = (ModifierKeys & Keys.Shift) != 0;
            if (_multiSelect && shift && _anchor != null) SelectRange(_anchor, item);
            else if (_multiSelect && control) { SetItemSelected(item, !item.Selected); _anchor = item; }
            else { SelectOnly(item); _anchor = item; }
            SetFocusedItem(item);
        }
        else if (item == null && e.Button == MouseButtons.Left && _selection.Count > 0)
        {
            var previous = _selection.ToArray();
            _selection.Clear();
            foreach (var old in previous) OnItemSelectionChanged(new ListViewItemSelectionChangedEventArgs(old, old.Index, false));
            OnSelectedIndexChanged(EventArgs.Empty);
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool left = (e.Button & MouseButtons.Left) != 0;
        if (_vVisible) _vscroll.MouseMove(e.Location, left);
        if (_hVisible) _hscroll.MouseMove(e.Location, left);

        int column = ColumnFromPoint(e.Location);
        if (column != _hotColumn)
        {
            _hotColumn = column;
            if (HeaderHeight > 0) Invalidate(HeaderRectangle);
        }
        if (HoverSelection)
        {
            var item = GetItemAt(e.X, e.Y);
            if (item != null && !item.Selected) SelectOnly(item);
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible) _vscroll.MouseUp(e.Location);
            if (_hVisible) _hscroll.MouseUp(e.Location);

            if (_pressedColumn >= 0)
            {
                int column = ColumnFromPoint(e.Location);
                int pressed = _pressedColumn;
                _pressedColumn = -1;
                Invalidate(HeaderRectangle);
                if (column == pressed && _headerStyle == ColumnHeaderStyle.Clickable)
                {
                    OnColumnClick(new ColumnClickEventArgs(pressed));
                }
            }
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vscroll.MouseLeave();
        _hscroll.MouseLeave();
        if (_hotColumn != -1)
        {
            _hotColumn = -1;
            if (HeaderHeight > 0) Invalidate(HeaderRectangle);
        }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int notches = e.Delta / 120;
        if (notches == 0) notches = Math.Sign(e.Delta);
        if (_vVisible) ScrollTo(_scroll.X, _scroll.Y - notches * RowHeight * 3);
        else if (_hVisible) ScrollTo(_scroll.X - notches * RowHeight * 3, _scroll.Y);
        base.OnMouseWheel(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        if (_focused != null) OnItemActivate(EventArgs.Empty);
        base.OnDoubleClick(e);
    }

    private int ColumnFromPoint(Point p)
    {
        if (HeaderHeight == 0 || !HeaderRectangle.Contains(p)) return -1;
        int x = ViewportRectangle.X - _scroll.X;
        for (int i = 0; i < _columns.Count; i++)
        {
            int right = x + _columns[i].Width;
            if (p.X >= x && p.X < right) return i;
            x = right;
        }
        return -1;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        EnsureLayout();
        if (_order.Count > 0)
        {
            int current = _focused != null ? _order.IndexOf(_focused.Index) : -1;
            int next = current;
            int perRow = ItemsPerRow();

            switch (e.KeyCode)
            {
                case Keys.Up: next = _view is View.Details or View.SmallIcon or View.LargeIcon or View.Tile ? current - perRow : current - 1; break;
                case Keys.Down: next = _view is View.Details or View.SmallIcon or View.LargeIcon or View.Tile ? current + perRow : current + 1; break;
                case Keys.Left: next = _view == View.Details ? current : current - 1; break;
                case Keys.Right: next = _view == View.Details ? current : current + 1; break;
                case Keys.Home: next = 0; break;
                case Keys.End: next = _order.Count - 1; break;
                case Keys.PageUp: next = current - Math.Max(1, ItemsRectangle.Height / Math.Max(1, RowHeight)); break;
                case Keys.PageDown: next = current + Math.Max(1, ItemsRectangle.Height / Math.Max(1, RowHeight)); break;
                case Keys.Space:
                    if (_checkBoxes && _focused != null)
                    {
                        SetItemChecked(_focused, !_focused.Checked);
                        e.Handled = true;
                    }
                    break;
                case Keys.Return:
                    if (_focused != null)
                    {
                        OnItemActivate(EventArgs.Empty);
                        e.Handled = true;
                    }
                    break;
                case Keys.F2:
                    if (LabelEdit && _focused != null)
                    {
                        BeginLabelEdit(_focused);
                        e.Handled = true;
                    }
                    break;
                case Keys.A when (e.Modifiers & Keys.Control) != 0 && _multiSelect:
                    _selection.Clear();
                    foreach (int index in _order) _selection.Add(_items[index]);
                    OnSelectedIndexChanged(EventArgs.Empty);
                    Invalidate();
                    e.Handled = true;
                    break;
            }

            if (next != current && !e.Handled)
            {
                next = Math.Clamp(next, 0, _order.Count - 1);
                var item = _items[_order[next]];
                SetFocusedItem(item);
                if (e.Shift && _multiSelect && _anchor != null) SelectRange(_anchor, item);
                else { SelectOnly(item); _anchor = item; }
                EnsureVisible(item.Index);
                e.Handled = true;
            }
        }
        base.OnKeyDown(e);
    }

    private int ItemsPerRow()
    {
        if (_view == View.Details) return 1;
        var cell = _view switch
        {
            View.Tile => _tileSize,
            View.SmallIcon or View.List => SmallCellSize(),
            _ => LargeCellSize(),
        };
        return Math.Max(1, ViewportRectangle.Width / Math.Max(1, cell.Width));
    }

    // --- painting -----------------------------------------------------------------------------------

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        EnsureLayout();
        var g = e.Graphics;

        var items = ItemsRectangle;
        var state = g.Save();
        g.IntersectClip(items);
        foreach (var (group, bounds) in _groupBands) PaintGroupBand(g, group, ContentToClient(bounds));
        foreach (int index in _order)
        {
            var bounds = ContentToClient(_layout[index]);
            if (!bounds.IntersectsWith(items)) continue;
            PaintItem(g, _items[index], index, bounds);
        }
        if (_gridLines && _view == View.Details) PaintGridLines(g, items);
        g.Restore(state);

        if (HeaderHeight > 0) PaintHeader(g);
        PaintBorder(g);
        base.OnPaint(e);
    }

    internal override void OnPaintOverlay(Graphics g)
    {
        if (_vVisible) _vscroll.Paint(g);
        if (_hVisible) _hscroll.Paint(g);
        if (_vVisible && _hVisible)
        {
            var inner = InnerRectangle;
            using var corner = new SolidBrush(SystemColors.Control);
            g.FillRectangle(corner, new Rectangle(inner.Right - ScrollBarCore.Thickness, inner.Bottom - ScrollBarCore.Thickness, ScrollBarCore.Thickness, ScrollBarCore.Thickness));
        }
        base.OnPaintOverlay(g);
    }

    private void PaintBorder(Graphics g)
    {
        if (_borderStyle == BorderStyle.None) return;
        using var pen = new Pen(_borderStyle == BorderStyle.Fixed3D ? Theme.WindowBorder : Theme.ButtonBorder);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    private void PaintGroupBand(Graphics g, ListViewGroup group, Rectangle bounds)
    {
        var text = new Rectangle(bounds.X + 4, bounds.Y, bounds.Width - 8, bounds.Height);
        var flags = group.HeaderAlignment switch
        {
            HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
            HorizontalAlignment.Right => TextFormatFlags.Right,
            _ => TextFormatFlags.Left,
        } | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        using var font = new Font(Font, FontStyle.Bold);
        TextRenderer.DrawText(g, group.Header, font, text, Theme.Accent, flags);
        using var pen = new Pen(Theme.StripSeparator);
        g.DrawLine(pen, bounds.X + 2, bounds.Bottom - 2, bounds.Right - 2, bounds.Bottom - 2);
    }

    private void PaintItem(Graphics g, ListViewItem item, int index, Rectangle bounds)
    {
        bool selected = _selection.Contains(item);
        bool showSelection = selected && (!HideSelection || Focused || ContainsFocus);

        if (OwnerDraw)
        {
            var state = (selected ? ListViewItemStates.Selected : 0) | (item == _focused ? ListViewItemStates.Focused : 0)
                | (item.Checked ? ListViewItemStates.Checked : 0);
            var args = new DrawListViewItemEventArgs(g, item, bounds, index, state);
            OnDrawItem(args);
            if (!args.DrawDefault)
            {
                if (_view == View.Details) PaintOwnerDrawSubItems(g, item, index, bounds, state);
                return;
            }
        }

        var label = LabelRect(index, bounds);
        var highlight = _fullRowSelect && _view == View.Details ? bounds : label;

        if (item.IsBackColorSet && !showSelection)
        {
            using var back = new SolidBrush(item.BackColor);
            g.FillRectangle(back, _view == View.Details ? bounds : highlight);
        }
        if (showSelection)
        {
            using var brush = new SolidBrush(Focused || ContainsFocus ? Theme.Highlight : Theme.HighlightInactive);
            g.FillRectangle(brush, highlight);
        }

        if (_checkBoxes)
        {
            CheckBox.PaintBox(g, CheckRect(bounds), item.Checked ? CheckState.Checked : CheckState.Unchecked,
                Enabled, hot: false, pressed: false, flat: false, ForeColor);
        }

        var imageList = _view is View.LargeIcon or View.Tile ? _largeImages : _smallImages;
        if (imageList != null && item.ImageIndex >= 0)
        {
            var iconRect = IconRect(bounds);
            imageList.Draw(g, iconRect.X, iconRect.Y, iconRect.Width, iconRect.Height, item.ImageIndex);
        }

        var foreColor = showSelection ? Theme.HighlightText : item.ForeColor;
        var textFlags = _view == View.LargeIcon
            ? TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | (LabelWrap ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine) | TextFormatFlags.EndEllipsis
            : TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

        // A tile puts its label on the first line of the block and the sub-items under it.
        var textRect = label;
        if (_view == View.Tile)
        {
            var (block, lineHeight, _) = TileText(index, bounds);
            textRect = new Rectangle(block.X, block.Y, block.Width, lineHeight);
        }
        TextRenderer.DrawText(g, item.Text, item.Font, textRect, foreColor, textFlags);

        if (_view == View.Details) PaintSubItems(g, item, index, bounds, showSelection);
        else if (_view == View.Tile) PaintTileLines(g, item, index, bounds, showSelection);

        if (item == _focused && Focused && ShowFocusCues)
        {
            ControlPaint.DrawFocusRectangle(g, _fullRowSelect && _view == View.Details ? bounds : label);
        }
    }

    private void PaintSubItems(Graphics g, ListViewItem item, int index, Rectangle bounds, bool selected)
    {
        int x = bounds.X + (_columns.Count > 0 ? _columns[0].Width : 0);
        for (int c = 1; c < _columns.Count; c++)
        {
            var column = _columns[c];
            var cell = new Rectangle(x + 4, bounds.Y, Math.Max(0, column.Width - 8), bounds.Height);
            if (c < item.SubItems.Count)
            {
                var sub = item.SubItems[c];
                var color = selected && _fullRowSelect ? Theme.HighlightText : item.UseItemStyleForSubItems ? item.ForeColor : sub.ForeColor;
                if (!selected && !item.UseItemStyleForSubItems && sub.IsBackColorSet)
                {
                    using var back = new SolidBrush(sub.BackColor);
                    g.FillRectangle(back, new Rectangle(x, bounds.Y, column.Width, bounds.Height));
                }
                var flags = column.TextAlign switch
                {
                    HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
                    HorizontalAlignment.Right => TextFormatFlags.Right,
                    _ => TextFormatFlags.Left,
                } | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
                TextRenderer.DrawText(g, sub.Text, item.UseItemStyleForSubItems ? item.Font : sub.Font, cell, color, flags);
            }
            x += column.Width;
        }
    }

    private void PaintOwnerDrawSubItems(Graphics g, ListViewItem item, int index, Rectangle bounds, ListViewItemStates state)
    {
        int x = bounds.X;
        for (int c = 0; c < _columns.Count && c < item.SubItems.Count; c++)
        {
            var cell = new Rectangle(x, bounds.Y, _columns[c].Width, bounds.Height);
            OnDrawSubItem(new DrawListViewSubItemEventArgs(g, cell, item, item.SubItems[c], index, c, _columns[c], state));
            x += _columns[c].Width;
        }
    }

    private void PaintTileLines(Graphics g, ListViewItem item, int index, Rectangle bounds, bool selected)
    {
        var (block, lineHeight, lines) = TileText(index, bounds);
        var color = selected ? Theme.HighlightText : Theme.DisabledText;
        for (int s = 1; s < lines && s < item.SubItems.Count; s++)
        {
            var line = new Rectangle(block.X, block.Y + s * lineHeight, block.Width, lineHeight);
            TextRenderer.DrawText(g, item.SubItems[s].Text, item.Font, line, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    private void PaintGridLines(Graphics g, Rectangle area)
    {
        using var pen = new Pen(Theme.StripSeparator);
        int rowHeight = RowHeight;
        for (int y = area.Y - _scroll.Y % rowHeight; y < area.Bottom; y += rowHeight)
        {
            g.DrawLine(pen, area.Left, y, area.Right, y);
        }
        int x = area.X - _scroll.X;
        foreach (ColumnHeader column in _columns)
        {
            x += column.Width;
            if (x > area.Left && x < area.Right) g.DrawLine(pen, x, area.Top, x, area.Bottom);
        }
    }

    private void PaintHeader(Graphics g)
    {
        var header = HeaderRectangle;
        using (var back = new SolidBrush(Theme.StripBackground))
        {
            g.FillRectangle(back, header);
        }
        using var pen = new Pen(Theme.StripBorder);
        g.DrawLine(pen, header.Left, header.Bottom - 1, header.Right, header.Bottom - 1);

        var state = g.Save();
        g.IntersectClip(header);
        int x = header.X - _scroll.X;
        for (int i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            var bounds = new Rectangle(x, header.Y, column.Width, header.Height);
            x += column.Width;
            if (bounds.Right < header.Left || bounds.Left > header.Right) continue;

            if (OwnerDraw)
            {
                var args = new DrawListViewColumnHeaderEventArgs(g, bounds, i, column, ListViewItemStates.ShowKeyboardCues, ForeColor, Theme.StripBackground, Font);
                OnDrawColumnHeader(args);
                if (!args.DrawDefault) continue;
            }

            if (i == _pressedColumn || i == _hotColumn)
            {
                using var hot = new SolidBrush(i == _pressedColumn ? Theme.StripItemPressed : Theme.StripItemHot);
                g.FillRectangle(hot, new Rectangle(bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1));
            }
            g.DrawLine(pen, bounds.Right - 1, bounds.Y + 3, bounds.Right - 1, bounds.Bottom - 4);

            var flags = column.TextAlign switch
            {
                HorizontalAlignment.Center => TextFormatFlags.HorizontalCenter,
                HorizontalAlignment.Right => TextFormatFlags.Right,
                _ => TextFormatFlags.Left,
            } | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(g, column.Text, Font, new Rectangle(bounds.X + 4, bounds.Y, Math.Max(0, bounds.Width - 8), bounds.Height), ForeColor, flags);
        }
        g.Restore(state);
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

    public override string ToString() => base.ToString() + ", Items.Count: " + _items.Count;

    // --- collections ------------------------------------------------------------------------------

    public class ListViewItemCollection : IList, IList<ListViewItem>
    {
        private readonly ListView _owner;
        private readonly List<ListViewItem> _list = new();

        public ListViewItemCollection(ListView owner) => _owner = owner;

        public int Count => _list.Count;
        public bool IsReadOnly => false;

        public virtual ListViewItem this[int index]
        {
            get => _list[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _list[index].ListView = null;
                value.ListView = _owner;
                _list[index] = value;
                _owner.ItemsChanged();
            }
        }

        public virtual ListViewItem? this[string? key]
        {
            get
            {
                int i = IndexOfKey(key);
                return i >= 0 ? _list[i] : null;
            }
        }

        public virtual ListViewItem Add(string? text) => Add(new ListViewItem(text));

        public virtual ListViewItem Add(string? text, int imageIndex) => Add(new ListViewItem(text, imageIndex));

        public virtual ListViewItem Add(string? text, string? imageKey) => Add(new ListViewItem(text, imageKey));

        public virtual ListViewItem Add(string? key, string? text, int imageIndex)
        {
            var item = new ListViewItem(text, imageIndex) { Name = key ?? string.Empty };
            return Add(item);
        }

        public virtual ListViewItem Add(ListViewItem value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ListView = _owner;
            _list.Add(value);
            _owner.ItemsChanged();
            return value;
        }

        void ICollection<ListViewItem>.Add(ListViewItem item) => Add(item);

        public void AddRange(ListViewItem[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            _owner.BeginUpdate();
            foreach (var i in items) Add(i);
            _owner.EndUpdate();
        }

        public void AddRange(ListViewItemCollection items)
        {
            ArgumentNullException.ThrowIfNull(items);
            AddRange(items.ToArray());
        }

        public virtual void Clear()
        {
            foreach (var item in _list) item.ListView = null;
            _list.Clear();
            _owner.ItemsChanged();
        }

        public bool Contains(ListViewItem item) => _list.Contains(item);
        public virtual bool ContainsKey(string? key) => IndexOfKey(key) >= 0;
        public int IndexOf(ListViewItem item) => _list.IndexOf(item);

        public virtual int IndexOfKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < _list.Count; i++)
            {
                if (string.Equals(_list[i].Name, key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public ListViewItem Insert(int index, ListViewItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.ListView = _owner;
            _list.Insert(Math.Clamp(index, 0, _list.Count), item);
            _owner.ItemsChanged();
            return item;
        }

        public ListViewItem Insert(int index, string? text) => Insert(index, new ListViewItem(text));

        public ListViewItem Insert(int index, string? text, int imageIndex) => Insert(index, new ListViewItem(text, imageIndex));

        void IList<ListViewItem>.Insert(int index, ListViewItem item) => Insert(index, item);

        public bool Remove(ListViewItem item)
        {
            if (item == null || !_list.Remove(item)) return false;
            item.ListView = null;
            _owner.ItemsChanged();
            return true;
        }

        public virtual void RemoveAt(int index)
        {
            _list[index].ListView = null;
            _list.RemoveAt(index);
            _owner.ItemsChanged();
        }

        public virtual void RemoveByKey(string? key)
        {
            int i = IndexOfKey(key);
            if (i >= 0) RemoveAt(i);
        }

        public ListViewItem[] Find(string key, bool searchAllSubItems)
        {
            var result = new List<ListViewItem>();
            foreach (var item in _list)
            {
                if (string.Equals(item.Name, key, StringComparison.OrdinalIgnoreCase)) result.Add(item);
                else if (searchAllSubItems && item.SubItems.ContainsKey(key)) result.Add(item);
            }
            return result.ToArray();
        }

        internal void SortCore(IComparer comparer) => _list.Sort((a, b) => comparer.Compare(a, b));

        public ListViewItem[] ToArray() => _list.ToArray();
        public void CopyTo(ListViewItem[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        public IEnumerator<ListViewItem> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _list[index]; set => this[index] = (ListViewItem)value!; }
        int IList.Add(object? value) { Add((ListViewItem)value!); return _list.Count - 1; }
        bool IList.Contains(object? value) => value is ListViewItem i && Contains(i);
        int IList.IndexOf(object? value) => value is ListViewItem i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => Insert(index, (ListViewItem)value!);
        void IList.Remove(object? value) { if (value is ListViewItem i) Remove(i); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_list).CopyTo(array, index);
    }

    public class ColumnHeaderCollection : IList, IList<ColumnHeader>
    {
        private readonly ListView _owner;
        private readonly List<ColumnHeader> _list = new();

        public ColumnHeaderCollection(ListView owner) => _owner = owner;

        public int Count => _list.Count;
        public bool IsReadOnly => false;

        public virtual ColumnHeader this[int index] => _list[index];

        ColumnHeader IList<ColumnHeader>.this[int index]
        {
            get => _list[index];
            set => throw new NotSupportedException();
        }

        public virtual ColumnHeader? this[string? key]
        {
            get
            {
                int i = IndexOfKey(key);
                return i >= 0 ? _list[i] : null;
            }
        }

        public virtual int Add(ColumnHeader value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ListView = _owner;
            _list.Add(value);
            _owner.ColumnsChanged();
            return _list.Count - 1;
        }

        public virtual ColumnHeader Add(string? text) => Add(text, 60, HorizontalAlignment.Left);

        public virtual ColumnHeader Add(string? text, int width) => Add(text, width, HorizontalAlignment.Left);

        public virtual ColumnHeader Add(string? text, int width, HorizontalAlignment textAlign)
        {
            var column = new ColumnHeader(text) { Width = width, TextAlign = textAlign };
            Add(column);
            return column;
        }

        public virtual ColumnHeader Add(string? key, string? text, int width)
        {
            var column = Add(text, width);
            column.Name = key ?? string.Empty;
            return column;
        }

        void ICollection<ColumnHeader>.Add(ColumnHeader item) => Add(item);

        public virtual void AddRange(ColumnHeader[] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            _owner.BeginUpdate();
            foreach (var v in values) Add(v);
            _owner.EndUpdate();
        }

        public virtual void Clear()
        {
            foreach (var column in _list) column.ListView = null;
            _list.Clear();
            _owner.ColumnsChanged();
        }

        public bool Contains(ColumnHeader value) => _list.Contains(value);
        public virtual bool ContainsKey(string? key) => IndexOfKey(key) >= 0;
        public int IndexOf(ColumnHeader value) => _list.IndexOf(value);

        public virtual int IndexOfKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < _list.Count; i++)
            {
                if (string.Equals(_list[i].Name, key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public void Insert(int index, ColumnHeader value)
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ListView = _owner;
            _list.Insert(Math.Clamp(index, 0, _list.Count), value);
            _owner.ColumnsChanged();
        }

        public void Insert(int index, string? text) => Insert(index, new ColumnHeader(text));

        public void Insert(int index, string? text, int width) => Insert(index, new ColumnHeader(text) { Width = width });

        public bool Remove(ColumnHeader column)
        {
            if (column == null || !_list.Remove(column)) return false;
            column.ListView = null;
            _owner.ColumnsChanged();
            return true;
        }

        public virtual void RemoveAt(int index)
        {
            _list[index].ListView = null;
            _list.RemoveAt(index);
            _owner.ColumnsChanged();
        }

        public virtual void RemoveByKey(string? key)
        {
            int i = IndexOfKey(key);
            if (i >= 0) RemoveAt(i);
        }

        public void CopyTo(ColumnHeader[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
        public IEnumerator<ColumnHeader> GetEnumerator() => _list.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _list[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) => Add((ColumnHeader)value!);
        bool IList.Contains(object? value) => value is ColumnHeader c && Contains(c);
        int IList.IndexOf(object? value) => value is ColumnHeader c ? IndexOf(c) : -1;
        void IList.Insert(int index, object? value) => Insert(index, (ColumnHeader)value!);
        void IList.Remove(object? value) { if (value is ColumnHeader c) Remove(c); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_list).CopyTo(array, index);
    }

    /// <summary>A snapshot of the selected items, in the order the list shows them.</summary>
    public class SelectedListViewItemCollection : IList, IReadOnlyList<ListViewItem>
    {
        private readonly List<ListViewItem> _snapshot;

        internal SelectedListViewItemCollection(ListView owner)
        {
            owner.EnsureLayout();
            _snapshot = new List<ListViewItem>();
            foreach (int index in owner._order)
            {
                var item = owner._items[index];
                if (owner._selection.Contains(item)) _snapshot.Add(item);
            }
        }

        public int Count => _snapshot.Count;
        public ListViewItem this[int index] => _snapshot[index];

        public ListViewItem? this[string? key]
        {
            get
            {
                foreach (var item in _snapshot)
                {
                    if (string.Equals(item.Name, key, StringComparison.OrdinalIgnoreCase)) return item;
                }
                return null;
            }
        }

        public bool Contains(ListViewItem item) => _snapshot.Contains(item);
        public bool ContainsKey(string? key) => this[key] != null;
        public int IndexOf(ListViewItem item) => _snapshot.IndexOf(item);
        public void CopyTo(Array dest, int index) => ((ICollection)_snapshot).CopyTo(dest, index);
        public IEnumerator<ListViewItem> GetEnumerator() => _snapshot.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _snapshot.GetEnumerator();

        bool IList.IsFixedSize => true;
        public bool IsReadOnly => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _snapshot[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        bool IList.Contains(object? value) => value is ListViewItem i && Contains(i);
        int IList.IndexOf(object? value) => value is ListViewItem i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    public class SelectedIndexCollection : IList, IReadOnlyList<int>
    {
        private readonly List<int> _snapshot;

        internal SelectedIndexCollection(ListView owner)
        {
            owner.EnsureLayout();
            _snapshot = new List<int>();
            foreach (int index in owner._order)
            {
                if (owner._selection.Contains(owner._items[index])) _snapshot.Add(index);
            }
        }

        public int Count => _snapshot.Count;
        public int this[int index] => _snapshot[index];
        public bool Contains(int selectedIndex) => _snapshot.Contains(selectedIndex);
        public int IndexOf(int selectedIndex) => _snapshot.IndexOf(selectedIndex);
        public void CopyTo(Array dest, int index) => ((ICollection)_snapshot).CopyTo(dest, index);
        public IEnumerator<int> GetEnumerator() => _snapshot.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _snapshot.GetEnumerator();

        bool IList.IsFixedSize => true;
        public bool IsReadOnly => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _snapshot[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        bool IList.Contains(object? value) => value is int i && Contains(i);
        int IList.IndexOf(object? value) => value is int i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    public class CheckedListViewItemCollection : IList, IReadOnlyList<ListViewItem>
    {
        private readonly List<ListViewItem> _snapshot;

        internal CheckedListViewItemCollection(ListView owner)
        {
            _snapshot = new List<ListViewItem>();
            foreach (ListViewItem item in owner._items)
            {
                if (item.Checked) _snapshot.Add(item);
            }
        }

        public int Count => _snapshot.Count;
        public ListViewItem this[int index] => _snapshot[index];
        public bool Contains(ListViewItem item) => _snapshot.Contains(item);
        public int IndexOf(ListViewItem item) => _snapshot.IndexOf(item);
        public void CopyTo(Array dest, int index) => ((ICollection)_snapshot).CopyTo(dest, index);
        public IEnumerator<ListViewItem> GetEnumerator() => _snapshot.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _snapshot.GetEnumerator();

        bool IList.IsFixedSize => true;
        public bool IsReadOnly => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _snapshot[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        bool IList.Contains(object? value) => value is ListViewItem i && Contains(i);
        int IList.IndexOf(object? value) => value is ListViewItem i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

    public class CheckedIndexCollection : IList, IReadOnlyList<int>
    {
        private readonly List<int> _snapshot;

        internal CheckedIndexCollection(ListView owner)
        {
            _snapshot = new List<int>();
            for (int i = 0; i < owner._items.Count; i++)
            {
                if (owner._items[i].Checked) _snapshot.Add(i);
            }
        }

        public int Count => _snapshot.Count;
        public int this[int index] => _snapshot[index];
        public bool Contains(int checkedIndex) => _snapshot.Contains(checkedIndex);
        public int IndexOf(int checkedIndex) => _snapshot.IndexOf(checkedIndex);
        public void CopyTo(Array dest, int index) => ((ICollection)_snapshot).CopyTo(dest, index);
        public IEnumerator<int> GetEnumerator() => _snapshot.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _snapshot.GetEnumerator();

        bool IList.IsFixedSize => true;
        public bool IsReadOnly => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _snapshot[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) => throw new NotSupportedException();
        void IList.Clear() => throw new NotSupportedException();
        bool IList.Contains(object? value) => value is int i && Contains(i);
        int IList.IndexOf(object? value) => value is int i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
    }

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

public class ListViewGroupCollection : IList, IList<ListViewGroup>
{
    private readonly ListView _owner;
    private readonly List<ListViewGroup> _list = new();

    internal ListViewGroupCollection(ListView owner) => _owner = owner;

    public int Count => _list.Count;
    public bool IsReadOnly => false;

    public ListViewGroup this[int index]
    {
        get => _list[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ListView = _owner;
            _list[index] = value;
            _owner.ColumnsChanged();
        }
    }

    public ListViewGroup? this[string? key]
    {
        get
        {
            foreach (var group in _list)
            {
                if (string.Equals(group.Name, key, StringComparison.OrdinalIgnoreCase)) return group;
            }
            return null;
        }
    }

    public int Add(ListViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.ListView = _owner;
        _list.Add(group);
        _owner.ColumnsChanged();
        return _list.Count - 1;
    }

    public ListViewGroup Add(string? key, string? headerText)
    {
        var group = new ListViewGroup(key, headerText);
        Add(group);
        return group;
    }

    void ICollection<ListViewGroup>.Add(ListViewGroup item) => Add(item);

    public void AddRange(ListViewGroup[] groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        foreach (var g in groups) Add(g);
    }

    public void AddRange(ListViewGroupCollection groups)
    {
        ArgumentNullException.ThrowIfNull(groups);
        foreach (ListViewGroup g in groups) Add(g);
    }

    public void Clear()
    {
        foreach (var group in _list) group.ListView = null;
        _list.Clear();
        _owner.ColumnsChanged();
    }

    public bool Contains(ListViewGroup value) => _list.Contains(value);
    public int IndexOf(ListViewGroup value) => _list.IndexOf(value);

    public void Insert(int index, ListViewGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.ListView = _owner;
        _list.Insert(Math.Clamp(index, 0, _list.Count), group);
        _owner.ColumnsChanged();
    }

    public bool Remove(ListViewGroup group)
    {
        if (group == null || !_list.Remove(group)) return false;
        group.ListView = null;
        _owner.ColumnsChanged();
        return true;
    }

    public void RemoveAt(int index)
    {
        _list[index].ListView = null;
        _list.RemoveAt(index);
        _owner.ColumnsChanged();
    }

    public void CopyTo(ListViewGroup[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);
    public IEnumerator<ListViewGroup> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;
    object? IList.this[int index] { get => _list[index]; set => this[index] = (ListViewGroup)value!; }
    int IList.Add(object? value) => Add((ListViewGroup)value!);
    bool IList.Contains(object? value) => value is ListViewGroup g && Contains(g);
    int IList.IndexOf(object? value) => value is ListViewGroup g ? IndexOf(g) : -1;
    void IList.Insert(int index, object? value) => Insert(index, (ListViewGroup)value!);
    void IList.Remove(object? value) { if (value is ListViewGroup g) Remove(g); }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_list).CopyTo(array, index);
}
