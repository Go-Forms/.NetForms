using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum View
{
    LargeIcon = 0,
    Details = 1,
    SmallIcon = 2,
    List = 3,
    Tile = 4,
}

public enum ColumnHeaderStyle
{
    None = 0,
    Nonclickable = 1,
    Clickable = 2,
}

public enum SortOrder
{
    None = 0,
    Ascending = 1,
    Descending = 2,
}

public enum ListViewAlignment
{
    Default = 0,
    Left = 1,
    Top = 2,
    SnapToGrid = 5,
}

public enum ColumnHeaderAutoResizeStyle
{
    None = 0,
    HeaderSize = 1,
    ColumnContent = 2,
}

[Flags]
public enum ListViewHitTestLocations
{
    None = 1,
    Image = 2,
    Label = 4,
    StateImage = 8,
    AboveClientArea = 0x100,
    BelowClientArea = 0x200,
    LeftOfClientArea = 0x400,
    RightOfClientArea = 0x800,
}

/// <summary>One column of a Details-view <see cref="ListView"/>.</summary>
/// <remarks>A component, as in WinForms: the designer gives each column a field and a block of its own.</remarks>
[DefaultProperty(nameof(Text))]
[DesignTimeVisible(false)]
[ToolboxItem(false)]
public class ColumnHeader : Component, ICloneable
{
    private string _text = "ColumnHeader";
    private int _width = 60;
    private HorizontalAlignment _textAlign = HorizontalAlignment.Left;

    public ColumnHeader() { }

    public ColumnHeader(string? text)
    {
        _text = text ?? string.Empty;
        _textSet = true;
    }

    private bool _textSet;

    public ColumnHeader(int imageIndex) => ImageIndex = imageIndex;

    [Description("The text displayed in the column header.")]
    [Localizable(true)]
    public string Text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            _textSet = true;
            ListView?.Invalidate();
        }
    }

    // As in WinForms: a new column's text ("ColumnHeader"), its empty name and its own place are not written.
    internal bool ShouldSerializeText() => _textSet;
    internal bool ShouldSerializeName() => Name.Length != 0;
    internal bool ShouldSerializeDisplayIndex() => DisplayIndex != Index;

    private string _name = string.Empty;

    /// <summary>The name; unset, a designed column answers with its site's name (as WinForms), so the designer writes it.</summary>
    [Description("The name of the column header.")]
    [Browsable(false)]
    public string Name
    {
        get => _name.Length != 0 ? _name : Site?.Name ?? string.Empty;
        set => _name = value ?? string.Empty;
    }

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [DefaultValue(-1)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ImageIndex { get; set; } = -1;

    [DefaultValue("")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ImageKey { get; set; } = string.Empty;

    [Description("The visual width of the column in pixels.")]
    [DefaultValue(60)]
    [Localizable(true)]
    public int Width
    {
        get => _width;
        set
        {
            if (_width == value) return;
            _width = Math.Max(0, value);
            ListView?.ColumnsChanged();
        }
    }

    [Description("Indicates the horizontal alignment of the text displayed in the column header.")]
    [DefaultValue(HorizontalAlignment.Left)]
    [Localizable(true)]
    public HorizontalAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value) return;
            _textAlign = value;
            ListView?.Invalidate();
        }
    }

    [Description("Displays a collection of items in one of five different views.")]
    [Browsable(false)]
    public ListView? ListView { get; internal set; }

    [Browsable(false)]
    public int Index => ListView?.Columns.IndexOf(this) ?? -1;

    [Category("Behavior")]
    [Description("The display index of the column")]
    [Localizable(true)]
    public int DisplayIndex
    {
        get => Index;
        set { }
    }

    /// <summary>Widens the column to its header text, its content, or both (WinForms' -1/-2 widths).</summary>
    public void AutoResize(ColumnHeaderAutoResizeStyle headerAutoResize)
    {
        if (ListView == null) return;
        Width = ListView.MeasureColumnWidth(this, headerAutoResize);
    }

    public object Clone() => new ColumnHeader(_text) { _width = _width, _textAlign = _textAlign, Name = Name, Tag = Tag, ImageIndex = ImageIndex };

    public override string ToString() => "ColumnHeader: Text: " + _text;
}

/// <summary>A row of a <see cref="ListView"/>: a label, an image and any number of sub-items.</summary>
[DefaultProperty(nameof(Text))]
public class ListViewItem : ICloneable
{
    private readonly ListViewSubItemCollection _subItems;
    private bool _checked;
    private Font? _font;
    private Color _foreColor = Color.Empty;
    private Color _backColor = Color.Empty;

    public ListViewItem() : this(string.Empty) { }

    public ListViewItem(string? text) : this(text, -1) { }

    public ListViewItem(string? text, int imageIndex)
    {
        _subItems = new ListViewSubItemCollection(this);
        _subItems.Add(new ListViewSubItem(this, text ?? string.Empty));
        ImageIndex = imageIndex;
    }

    public ListViewItem(string? text, string? imageKey) : this(text)
    {
        ImageKey = imageKey ?? string.Empty;
    }

    public ListViewItem(string[] items) : this(items, -1) { }

    public ListViewItem(string[] items, int imageIndex) : this(items is { Length: > 0 } ? items[0] : string.Empty, imageIndex)
    {
        if (items == null) return;
        for (int i = 1; i < items.Length; i++) _subItems.Add(new ListViewSubItem(this, items[i]));
    }

    public ListViewItem(string[] items, string? imageKey) : this(items, -1) => ImageKey = imageKey ?? string.Empty;

    public ListViewItem(string[] items, int imageIndex, Color foreColor, Color backColor, Font? font) : this(items, imageIndex)
    {
        _foreColor = foreColor;
        _backColor = backColor;
        _font = font;
    }

    public ListViewItem(string[] items, string? imageKey, Color foreColor, Color backColor, Font? font) : this(items, -1, foreColor, backColor, font)
        => ImageKey = imageKey ?? string.Empty;

    public ListViewItem(ListViewSubItem[] subItems, string? imageKey) : this(subItems, -1) => ImageKey = imageKey ?? string.Empty;

    public ListViewItem(ListViewSubItem[] subItems, int imageIndex) : this(string.Empty, imageIndex)
    {
        ArgumentNullException.ThrowIfNull(subItems);
        _subItems.Clear();
        foreach (var s in subItems)
        {
            s.Owner = this;
            _subItems.Add(s);
        }
        if (_subItems.Count == 0) _subItems.Add(new ListViewSubItem(this, string.Empty));
        // The first sub-item's style is the item's own, as in WinForms.
        var first = _subItems[0];
        if (first.HasCustomStyle) (_foreColor, _backColor, _font) = (first.RawForeColor, first.RawBackColor, first.RawFont);
    }

    /// <summary>Whether the item has a color or font of its own (WinForms: SubItems[0].CustomStyle).</summary>
    internal bool HasCustomStyle => _font != null || !_foreColor.IsEmpty || !_backColor.IsEmpty;
    internal Color RawForeColor => _foreColor;
    internal Color RawBackColor => _backColor;
    internal Font? RawFont => _font;

    public ListViewItem(string? text, int imageIndex, ListViewGroup? group) : this(text, imageIndex) => Group = group;

    [Category("Appearance")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Text
    {
        get => _subItems.Count > 0 ? _subItems[0].Text : string.Empty;
        set
        {
            if (_subItems.Count == 0) _subItems.Add(new ListViewSubItem(this, value));
            else _subItems[0].Text = value ?? string.Empty;
            ListView?.ItemsChanged();
        }
    }

    [Localizable(true)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string Name { get; set; } = string.Empty;

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [Category("Appearance")]
    [DefaultValue("")]
    public string ToolTipText { get; set; } = string.Empty;

    [Category("Data")]
    [Description("The SubItems for this ListViewItem.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ListViewSubItemCollection SubItems => _subItems;

    private int _imageIndex = -1;
    private ListViewGroup? _group;

    [Category("Behavior")]
    [Description("The ImageList index value of the image displayed when the list view item is in the unselected state.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int ImageIndex
    {
        get => _imageIndex;
        set
        {
            if (_imageIndex == value) return;
            _imageIndex = value;
            ListView?.Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue("")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string ImageKey { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("The image-list index value of the state image displayed.")]
    [DefaultValue(-1)]
    [Localizable(true)]
    public int StateImageIndex { get; set; } = -1;

    [Category("Display")]
    [Description("Number of indents for a list ListViewItem")]
    [DefaultValue(0)]
    public int IndentCount { get; set; }

    /// <summary>Changing the group re-bands the list, so it has to go through the layout.</summary>
    [Category("Behavior")]
    [DefaultValue(null)]
    [Localizable(true)]
    public ListViewGroup? Group
    {
        get => _group;
        set
        {
            if (ReferenceEquals(_group, value)) return;
            _group = value;
            ListView?.ItemsChanged();
        }
    }

    [Category("Appearance")]
    [DefaultValue(true)]
    public bool UseItemStyleForSubItems { get; set; } = true;

    [Description("Displays a collection of items in one of five different views.")]
    [Browsable(false)]
    public ListView? ListView { get; internal set; }

    [Browsable(false)]
    public int Index => ListView?.Items.IndexOf(this) ?? -1;

    [Category("Appearance")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Font Font
    {
        get => _font ?? ListView?.Font ?? Control.DefaultFont;
        set { _font = value; ListView?.ItemsChanged(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color ForeColor
    {
        get => !_foreColor.IsEmpty ? _foreColor : ListView?.ForeColor ?? Control.DefaultForeColor;
        set { _foreColor = value; ListView?.Invalidate(); }
    }

    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color BackColor
    {
        get => !_backColor.IsEmpty ? _backColor : ListView?.BackColor ?? Control.DefaultBackColor;
        set { _backColor = value; ListView?.Invalidate(); }
    }

    internal bool IsBackColorSet => !_backColor.IsEmpty;

    [Category("Appearance")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            if (ListView != null) ListView.SetItemChecked(this, value);
            else _checked = value;
        }
    }

    internal void SetCheckedCore(bool value) => _checked = value;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Selected
    {
        get => ListView != null && ListView.IsItemSelected(this);
        set => ListView?.SetItemSelected(this, value);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Focused
    {
        get => ListView?.FocusedItem == this;
        set { if (value) ListView?.SetFocusedItem(this); }
    }

    [Browsable(false)]
    public Rectangle Bounds => ListView?.GetItemRect(Index) ?? Rectangle.Empty;

    public Rectangle GetBounds(ItemBoundsPortion portion) => ListView?.GetItemRect(Index, portion) ?? Rectangle.Empty;

    public void EnsureVisible() => ListView?.EnsureVisible(Index);

    public void Remove() => ListView?.Items.Remove(this);

    public void BeginEdit() => ListView?.BeginLabelEdit(this);

    public object Clone()
    {
        var copy = new ListViewItem(Text, ImageIndex) { Name = Name, Tag = Tag, _font = _font, _foreColor = _foreColor, _backColor = _backColor };
        for (int i = 1; i < _subItems.Count; i++) copy.SubItems.Add(_subItems[i].Text);
        return copy;
    }

    public override string ToString() => "ListViewItem: {" + Text + "}";

    /// <summary>A cell of a Details-view row.</summary>
    public class ListViewSubItem
    {
        private string _text = string.Empty;
        private Font? _font;
        private Color _foreColor = Color.Empty;
        private Color _backColor = Color.Empty;

        public ListViewSubItem() { }

        public ListViewSubItem(ListViewItem? owner, string? text)
        {
            Owner = owner;
            _text = text ?? string.Empty;
        }

        public ListViewSubItem(ListViewItem? owner, string? text, Color foreColor, Color backColor, Font? font) : this(owner, text)
        {
            _foreColor = foreColor;
            _backColor = backColor;
            _font = font;
        }

        internal ListViewItem? Owner { get; set; }

        public string Text
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;
                Owner?.ListView?.ItemsChanged();
            }
        }

        public string Name { get; set; } = string.Empty;

        public object? Tag { get; set; }

        public Font Font
        {
            get => _font ?? Owner?.Font ?? Control.DefaultFont;
            set => _font = value;
        }

        public Color ForeColor
        {
            get => !_foreColor.IsEmpty ? _foreColor : Owner?.ForeColor ?? Control.DefaultForeColor;
            set => _foreColor = value;
        }

        public Color BackColor
        {
            get => !_backColor.IsEmpty ? _backColor : Owner?.BackColor ?? Control.DefaultBackColor;
            set => _backColor = value;
        }

        internal bool IsBackColorSet => !_backColor.IsEmpty;

        /// <summary>Whether this sub-item has a color or font of its own (WinForms: CustomStyle).</summary>
        internal bool HasCustomStyle => _font != null || !_foreColor.IsEmpty || !_backColor.IsEmpty;
        internal Color RawForeColor => _foreColor;
        internal Color RawBackColor => _backColor;
        internal Font? RawFont => _font;

        public void ResetStyle()
        {
            _font = null;
            _foreColor = Color.Empty;
            _backColor = Color.Empty;
        }

        public override string ToString() => "ListViewSubItem: {" + _text + "}";
    }

    public class ListViewSubItemCollection : IList, IList<ListViewSubItem>
    {
        private readonly ListViewItem _owner;
        private readonly List<ListViewSubItem> _items = new();

        public ListViewSubItemCollection(ListViewItem owner) => _owner = owner;

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public ListViewSubItem this[int index]
        {
            get => _items[index];
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                value.Owner = _owner;
                _items[index] = value;
            }
        }

        public ListViewSubItem? this[string? key]
        {
            get
            {
                int i = IndexOfKey(key);
                return i >= 0 ? _items[i] : null;
            }
        }

        public ListViewSubItem Add(string? text)
        {
            var item = new ListViewSubItem(_owner, text);
            Add(item);
            return item;
        }

        public ListViewSubItem Add(string? text, Color foreColor, Color backColor, Font? font)
        {
            var item = new ListViewSubItem(_owner, text, foreColor, backColor, font);
            Add(item);
            return item;
        }

        public ListViewSubItem Add(ListViewSubItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.Owner = _owner;
            _items.Add(item);
            _owner.ListView?.ItemsChanged();
            return item;
        }

        void ICollection<ListViewSubItem>.Add(ListViewSubItem item) => Add(item);

        public void AddRange(ListViewSubItem[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            foreach (var i in items) Add(i);
        }

        public void AddRange(string[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            foreach (var i in items) Add(i);
        }

        public void Clear()
        {
            _items.Clear();
            _owner.ListView?.ItemsChanged();
        }

        public bool Contains(ListViewSubItem item) => _items.Contains(item);
        public bool ContainsKey(string? key) => IndexOfKey(key) >= 0;
        public int IndexOf(ListViewSubItem item) => _items.IndexOf(item);

        public int IndexOfKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Name, key, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        public void Insert(int index, ListViewSubItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            item.Owner = _owner;
            _items.Insert(index, item);
            _owner.ListView?.ItemsChanged();
        }

        public bool Remove(ListViewSubItem item)
        {
            bool removed = _items.Remove(item);
            if (removed) _owner.ListView?.ItemsChanged();
            return removed;
        }

        public void RemoveAt(int index)
        {
            _items.RemoveAt(index);
            _owner.ListView?.ItemsChanged();
        }

        public void RemoveByKey(string? key)
        {
            int i = IndexOfKey(key);
            if (i >= 0) RemoveAt(i);
        }

        public void CopyTo(ListViewSubItem[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public IEnumerator<ListViewSubItem> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => _items[index]; set => this[index] = (ListViewSubItem)value!; }
        int IList.Add(object? value) { Add((ListViewSubItem)value!); return _items.Count - 1; }
        bool IList.Contains(object? value) => value is ListViewSubItem s && Contains(s);
        int IList.IndexOf(object? value) => value is ListViewSubItem s ? IndexOf(s) : -1;
        void IList.Insert(int index, object? value) => Insert(index, (ListViewSubItem)value!);
        void IList.Remove(object? value) { if (value is ListViewSubItem s) Remove(s); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    }
}

public enum ItemBoundsPortion
{
    Entire = 0,
    Icon = 1,
    Label = 2,
    ItemOnly = 3,
}

/// <summary>A named band of items shown when <see cref="ListView.ShowGroups"/> is on.</summary>
[DefaultProperty(nameof(Header))]
public class ListViewGroup
{
    private string _header = string.Empty;

    /// <summary>A group headed "ListViewGroup" and without a name, as in WinForms.</summary>
    public ListViewGroup() : this("ListViewGroup") { }

    public ListViewGroup(string? header) => _header = header ?? string.Empty;

    public ListViewGroup(string? key, string? headerText) : this(headerText) => Name = key;

    public ListViewGroup(string? header, HorizontalAlignment headerAlignment) : this(header) => HeaderAlignment = headerAlignment;

    [Category("Appearance")]
    [DefaultValue("")]
    public string Header
    {
        get => _header;
        set
        {
            _header = value ?? string.Empty;
            ListView?.Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(HorizontalAlignment.Left)]
    public HorizontalAlignment HeaderAlignment { get; set; } = HorizontalAlignment.Left;

    [Category("Behavior")]
    [Description("The name of this group.")]
    [DefaultValue("")]
    public string? Name { get; set; }

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [Description("Displays a collection of items in one of five different views.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ListView? ListView { get; internal set; }

    /// <summary>The items that currently belong to this group; the ListView owns them.</summary>
    [Browsable(false)]
    public IReadOnlyList<ListViewItem> Items
    {
        get
        {
            var result = new List<ListViewItem>();
            if (ListView == null) return result;
            foreach (ListViewItem item in ListView.Items)
            {
                if (ReferenceEquals(item.Group, this)) result.Add(item);
            }
            return result;
        }
    }

    public override string ToString() => _header;
}

public class ListViewHitTestInfo
{
    public ListViewHitTestInfo(ListViewItem? hitItem, ListViewItem.ListViewSubItem? hitSubItem, ListViewHitTestLocations hitLocation)
    {
        Item = hitItem;
        SubItem = hitSubItem;
        Location = hitLocation;
    }

    public ListViewItem? Item { get; }
    public ListViewItem.ListViewSubItem? SubItem { get; }
    public ListViewHitTestLocations Location { get; }
}

// --- event args -----------------------------------------------------------------------

public delegate void ColumnClickEventHandler(object? sender, ColumnClickEventArgs e);
public delegate void LabelEditEventHandler(object? sender, LabelEditEventArgs e);
public delegate void ItemCheckEventHandler(object? sender, ItemCheckEventArgs e);
public delegate void ItemCheckedEventHandler(object? sender, ItemCheckedEventArgs e);
public delegate void ListViewItemSelectionChangedEventHandler(object? sender, ListViewItemSelectionChangedEventArgs e);
public delegate void DrawListViewItemEventHandler(object? sender, DrawListViewItemEventArgs e);
public delegate void DrawListViewSubItemEventHandler(object? sender, DrawListViewSubItemEventArgs e);
public delegate void DrawListViewColumnHeaderEventHandler(object? sender, DrawListViewColumnHeaderEventArgs e);

public class ColumnClickEventArgs : EventArgs
{
    public ColumnClickEventArgs(int column) => Column = column;

    public int Column { get; }
}

public class LabelEditEventArgs : EventArgs
{
    public LabelEditEventArgs(int item) : this(item, null) { }

    public LabelEditEventArgs(int item, string? label)
    {
        Item = item;
        Label = label;
    }

    public int Item { get; }
    public string? Label { get; }
    public bool CancelEdit { get; set; }
}

public class ItemCheckEventArgs : EventArgs
{
    public ItemCheckEventArgs(int index, CheckState newCheckValue, CheckState currentValue)
    {
        Index = index;
        NewValue = newCheckValue;
        CurrentValue = currentValue;
    }

    public int Index { get; }
    public CheckState NewValue { get; set; }
    public CheckState CurrentValue { get; }
}

public class ItemCheckedEventArgs : EventArgs
{
    public ItemCheckedEventArgs(ListViewItem item) => Item = item;

    public ListViewItem Item { get; }
}

public class ListViewItemSelectionChangedEventArgs : EventArgs
{
    public ListViewItemSelectionChangedEventArgs(ListViewItem item, int itemIndex, bool isSelected)
    {
        Item = item;
        ItemIndex = itemIndex;
        IsSelected = isSelected;
    }

    public ListViewItem Item { get; }
    public int ItemIndex { get; }
    public bool IsSelected { get; }
}

[Flags]
public enum ListViewItemStates
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

public class DrawListViewItemEventArgs : EventArgs
{
    public DrawListViewItemEventArgs(Graphics graphics, ListViewItem item, Rectangle bounds, int itemIndex, ListViewItemStates state)
    {
        Graphics = graphics;
        Item = item;
        Bounds = bounds;
        ItemIndex = itemIndex;
        State = state;
    }

    public Graphics Graphics { get; }
    public ListViewItem Item { get; }
    public Rectangle Bounds { get; }
    public int ItemIndex { get; }
    public ListViewItemStates State { get; }
    public bool DrawDefault { get; set; }
}

public class DrawListViewSubItemEventArgs : EventArgs
{
    public DrawListViewSubItemEventArgs(Graphics graphics, Rectangle bounds, ListViewItem item, ListViewItem.ListViewSubItem subItem,
        int itemIndex, int columnIndex, ColumnHeader header, ListViewItemStates itemState)
    {
        Graphics = graphics;
        Bounds = bounds;
        Item = item;
        SubItem = subItem;
        ItemIndex = itemIndex;
        ColumnIndex = columnIndex;
        Header = header;
        ItemState = itemState;
    }

    public Graphics Graphics { get; }
    public Rectangle Bounds { get; }
    public ListViewItem Item { get; }
    public ListViewItem.ListViewSubItem SubItem { get; }
    public int ItemIndex { get; }
    public int ColumnIndex { get; }
    public ColumnHeader Header { get; }
    public ListViewItemStates ItemState { get; }
    public bool DrawDefault { get; set; }
}

public class DrawListViewColumnHeaderEventArgs : EventArgs
{
    public DrawListViewColumnHeaderEventArgs(Graphics graphics, Rectangle bounds, int columnIndex, ColumnHeader header,
        ListViewItemStates state, Color foreColor, Color backColor, Font? font)
    {
        Graphics = graphics;
        Bounds = bounds;
        ColumnIndex = columnIndex;
        Header = header;
        State = state;
        ForeColor = foreColor;
        BackColor = backColor;
        Font = font;
    }

    public Graphics Graphics { get; }
    public Rectangle Bounds { get; }
    public int ColumnIndex { get; }
    public ColumnHeader Header { get; }
    public ListViewItemStates State { get; }
    public Color ForeColor { get; }
    public Color BackColor { get; }
    public Font? Font { get; }
    public bool DrawDefault { get; set; }
}
