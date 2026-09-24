using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;

namespace System.Windows.Forms;

public enum SelectionMode
{
    None = 0,
    One = 1,
    MultiSimple = 2,
    MultiExtended = 3,
}

/// <summary>
/// A list of items with single/multi selection, keyboard navigation and an embedded
/// vertical scroll bar (WinForms has it in the non-client area; ours is drawn inside the
/// client rectangle, which is also where WinForms' client area ends up).
/// </summary>
[DefaultEvent("SelectedIndexChanged")]
[DefaultProperty("Items")]
public class ListBox : ListControl
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

    public const int DefaultItemHeight = 13;
    public const int NoMatches = -1;

    private readonly ObjectCollection _items;
    private readonly SelectedIndexCollection _selectedIndices;
    private readonly SelectedObjectCollection _selectedItems;
    private readonly ScrollBarCore _vscroll;
    private readonly List<int> _selection = new();
    private int _focusedIndex = -1;
    private int _anchorIndex = -1;
    private int _topIndex;
    private int _itemHeight;
    private SelectionMode _selectionMode = SelectionMode.One;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private DrawMode _drawMode = DrawMode.Normal;
    private bool _sorted;
    private bool _integralHeight = true;
    private bool _scrollAlwaysVisible;
    private int _updateCount;

    public ListBox()
    {
        _items = CreateItemCollection();
        _selectedIndices = new SelectedIndexCollection(this);
        _selectedItems = new SelectedObjectCollection(this);
        _vscroll = new ScrollBarCore(this, vertical: true, (v, _) => { TopIndex = v; });
        SetStyle(ControlStyles.StandardClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
    }

    protected override Size DefaultSize => new Size(120, 96);

    /// <summary>Lists are white by default (SystemColors.Window), whatever the parent's colour.</summary>
    [Category("Appearance")]
    [Description("The background color of the component.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Window;
        set => base.BackColor = value;
    }

    [Category("Behavior")]
    [Description("Occurs when the value of the SelectedIndex property changes.")]
    public event EventHandler? SelectedIndexChanged;

    [Category("Behavior")]
    [Description("Occurs whenever a particular item/area needs to be painted.")]
    public event DrawItemEventHandler? DrawItem;

    [Category("Behavior")]
    [Description("Occurs whenever a particular item's height needs to be calculated.")]
    public event MeasureItemEventHandler? MeasureItem;

    // --- properties ----------------------------------------------------------------

    [Category("Data")]
    [Description("The items in the list box.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public ObjectCollection Items => _items;

    [Description("A collection of indexes for the currently selected items in the list box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SelectedIndexCollection SelectedIndices => _selectedIndices;

    [Description("A collection of currently selected items.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SelectedObjectCollection SelectedItems => _selectedItems;

    [Description("Retrieves the index of the first selection in the list box, or -1 if there is no selection.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override int SelectedIndex
    {
        get => _selection.Count > 0 ? _selection[0] : -1;
        set
        {
            if (value < -1 || value >= _items.Count) throw new ArgumentOutOfRangeException(nameof(value));
            if (_selectionMode == SelectionMode.None && value != -1) throw new ArgumentException("SelectionMode is None.");
            if (value == -1)
            {
                if (_selection.Count == 0) return;
                _selection.Clear();
            }
            else
            {
                if (_selection.Count == 1 && _selection[0] == value) return;
                _selection.Clear();
                _selection.Add(value);
                _focusedIndex = value;
                _anchorIndex = value;
                EnsureVisible(value);
            }
            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
        }
    }

    [Description("The currently selected item in the list box, or null.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedItem
    {
        get => SelectedIndex >= 0 ? _items[SelectedIndex] : null;
        set => SelectedIndex = value == null ? -1 : _items.IndexOf(value);
    }

    [Category("Behavior")]
    [Description("Indicates if the list box is to be single-select, multi-select, or not selectable.")]
    [DefaultValue(SelectionMode.One)]
    public virtual SelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (_selectionMode == value) return;
            _selectionMode = value;
            if (value == SelectionMode.None) _selection.Clear();
            else if (value == SelectionMode.One && _selection.Count > 1) _selection.RemoveRange(1, _selection.Count - 1);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The height, in pixels, of items in a fixed-height owner-draw list box.")]
    [Localizable(true)]
    public virtual int ItemHeight
    {
        // A normally drawn list box measures its items by the font, whatever was set (WinForms asks the
        // native list box, which does the same); an owner-drawn one uses the value it was given.
        get => _itemHeight > 0 && _drawMode != DrawMode.Normal ? _itemHeight : Font.Height;
        set
        {
            if (value <= 0 || value > 255) throw new ArgumentOutOfRangeException(nameof(value));
            _itemHeight = value;
            UpdateScroll();
            Invalidate();
        }
    }

    /// <summary>As in WinForms: only an owner-drawn list box persists its item height.</summary>
    internal bool ShouldSerializeItemHeight() => _itemHeight > 0 && _itemHeight != DefaultItemHeight && DrawMode != DrawMode.Normal;

    [Description("The index of the first visible item in the list box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int TopIndex
    {
        get => _topIndex;
        set
        {
            int max = Math.Max(0, _items.Count - VisibleRows());
            value = Math.Clamp(value, 0, max);
            if (_topIndex == value) return;
            _topIndex = value;
            _vscroll.Value = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Controls whether the list is sorted.")]
    [DefaultValue(false)]
    public bool Sorted
    {
        get => _sorted;
        set
        {
            if (_sorted == value) return;
            _sorted = value;
            if (value) _items.SortInternal();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Controls what type of border is drawn around the ListBox.")]
    [DefaultValue(BorderStyle.Fixed3D)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            UpdateScroll();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Controls list box painting. Either the system [NORMAL] or the user [OWNERDRAW] paints each item.")]
    [DefaultValue(DrawMode.Normal)]
    public virtual DrawMode DrawMode
    {
        get => _drawMode;
        set
        {
            _drawMode = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the list can contain only complete items.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool IntegralHeight
    {
        get => _integralHeight;
        set => _integralHeight = value;
    }

    [Category("Behavior")]
    [Description("Indicates if the list box should always have a scroll bar present, regardless of how many items are in it.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool ScrollAlwaysVisible
    {
        get => _scrollAlwaysVisible;
        set
        {
            _scrollAlwaysVisible = value;
            UpdateScroll();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Indicates if values should be displayed in columns horizontally.")]
    [DefaultValue(false)]
    public bool MultiColumn { get; set; }

    [Category("Behavior")]
    [Description("Indicates how wide each column should be in a multicolumn ListBox.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int ColumnWidth { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the ListBox will display a horizontal scroll bar for items beyond the right edge of the ListBox.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool HorizontalScrollbar { get; set; }

    [Category("Behavior")]
    [Description("The width, in pixels, by which a list box can be scrolled horizontally. Only valid if HorizontalScrollBars is true.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int HorizontalExtent { get; set; }

    [Category("Behavior")]
    [Description("Indicates if TAB characters should be expanded into full spacing.")]
    [DefaultValue(true)]
    public bool UseTabStops { get; set; } = true;

    [Description("The preferred height of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PreferredHeight => _items.Count * ItemHeight + 2 * BorderSize;

    [Category("Appearance")]
    [Description("The text associated with the control.")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text
    {
        get => SelectedItem != null ? GetItemText(SelectedItem) : string.Empty;
        set
        {
            if (value == null) return;
            int i = FindStringExact(value);
            if (i >= 0) SelectedIndex = i;
        }
    }

    // --- geometry ------------------------------------------------------------------

    private int BorderSize => _borderStyle == BorderStyle.None ? 0 : _borderStyle == BorderStyle.FixedSingle ? 1 : 2;

    private bool ScrollVisible => _scrollAlwaysVisible || _items.Count > VisibleRows();

    private Rectangle ItemsArea
    {
        get
        {
            int b = BorderSize;
            var r = new Rectangle(b, b, Math.Max(0, Width - 2 * b), Math.Max(0, Height - 2 * b));
            if (ScrollVisible) r.Width = Math.Max(0, r.Width - ScrollBarCore.Thickness);
            return r;
        }
    }

    private Rectangle ScrollArea
    {
        get
        {
            int b = BorderSize;
            return new Rectangle(Width - b - ScrollBarCore.Thickness, b, ScrollBarCore.Thickness, Math.Max(0, Height - 2 * b));
        }
    }

    private int VisibleRows()
    {
        int h = Math.Max(0, Height - 2 * BorderSize);
        return Math.Max(1, h / Math.Max(1, ItemHeight));
    }

    public Rectangle GetItemRectangle(int index)
    {
        if (index < 0 || index >= _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var area = ItemsArea;
        return new Rectangle(area.X, area.Y + (index - _topIndex) * ItemHeight, area.Width, ItemHeight);
    }

    public int IndexFromPoint(Point p) => IndexFromPoint(p.X, p.Y);

    public int IndexFromPoint(int x, int y)
    {
        var area = ItemsArea;
        if (!area.Contains(x, y)) return NoMatches;
        int index = _topIndex + (y - area.Y) / Math.Max(1, ItemHeight);
        return index < _items.Count ? index : NoMatches;
    }

    private void EnsureVisible(int index)
    {
        if (index < 0) return;
        int rows = VisibleRows();
        if (index < _topIndex) TopIndex = index;
        else if (index >= _topIndex + rows) TopIndex = index - rows + 1;
    }

    private void UpdateScroll()
    {
        int rows = VisibleRows();
        _vscroll.Minimum = 0;
        _vscroll.Maximum = Math.Max(0, _items.Count - 1);
        _vscroll.LargeChange = rows;
        _vscroll.SmallChange = 1;
        _vscroll.Bounds = ScrollArea;
        _vscroll.Enabled = Enabled;
        int max = Math.Max(0, _items.Count - rows);
        if (_topIndex > max) _topIndex = max;
        _vscroll.Value = _topIndex;
    }

    // --- items ---------------------------------------------------------------------

    public void BeginUpdate() => _updateCount++;

    public void EndUpdate()
    {
        if (_updateCount > 0) _updateCount--;
        if (_updateCount == 0)
        {
            UpdateScroll();
            Invalidate();
        }
    }

    /// <summary>The Items collection; CheckedListBox makes its own (as in WinForms).</summary>
    protected virtual ObjectCollection CreateItemCollection() => new ObjectCollection(this);

    // Where items came and went, for state kept per item (CheckedListBox's check marks). Selected
    // indices are shifted by the collection itself.
    internal virtual void ItemInserted(int index) { }
    internal virtual void ItemRemoved(int index) { }
    internal virtual void ItemsCleared() { }
    /// <summary>After a sort: <paramref name="oldIndices"/>[i] is where the item now at i used to be.</summary>
    internal virtual void ItemsReordered(int[] oldIndices) { }

    private void ShiftSelectionForInsert(int index)
    {
        for (int i = 0; i < _selection.Count; i++)
            if (_selection[i] >= index) _selection[i]++;
    }

    internal void ItemsChanged()
    {
        _selection.RemoveAll(i => i >= _items.Count);
        if (_focusedIndex >= _items.Count) _focusedIndex = _items.Count - 1;
        if (_updateCount == 0)
        {
            UpdateScroll();
            Invalidate();
        }
    }

    /// <summary>The items of the data source (ListControl calls it; the selection follows DataManager.Position).</summary>
    protected override void SetItemsCore(IList items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items.SetFromDataSource(items);
    }

    protected override void SetItemCore(int index, object value) => _items.SetItemInternal(index, value);

    /// <summary>Re-reads the items - from the data source when there is one - and their text (WinForms).</summary>
    protected override void RefreshItems()
    {
        if (DataManager is { } manager)
        {
            _items.SetFromDataSource(manager.List);
            if (SelectionMode != SelectionMode.None) SelectedIndex = manager.Position;
        }
        else
        {
            ItemsChanged();
        }
    }

    protected override void RefreshItem(int index) => Invalidate();

    protected override void OnDataSourceChanged(EventArgs e)
    {
        if (DataSource == null)
        {
            SelectedIndex = -1;
            _items.SetFromDataSource(Array.Empty<object>());
        }
        base.OnDataSourceChanged(e);
        RefreshItems();
    }

    protected override void OnDisplayMemberChanged(EventArgs e)
    {
        base.OnDisplayMemberChanged(e);
        RefreshItems();
        if (SelectionMode != SelectionMode.None && DataManager != null) SelectedIndex = DataManager.Position;
    }

    private void CheckNoDataSource()
    {
        if (DataSource != null) throw new ArgumentException(SR.DataSourceLocksItems);
    }

    public int FindString(string s) => FindString(s, -1);

    public int FindString(string s, int startIndex)
    {
        if (string.IsNullOrEmpty(s)) return NoMatches;
        for (int n = 0; n < _items.Count; n++)
        {
            int i = (startIndex + 1 + n) % _items.Count;
            if (GetItemText(_items[i]).StartsWith(s, StringComparison.CurrentCultureIgnoreCase)) return i;
        }
        return NoMatches;
    }

    public int FindStringExact(string s) => FindStringExact(s, -1);

    public int FindStringExact(string s, int startIndex)
    {
        if (s == null) return NoMatches;
        for (int n = 0; n < _items.Count; n++)
        {
            int i = (startIndex + 1 + n) % _items.Count;
            if (string.Equals(GetItemText(_items[i]), s, StringComparison.CurrentCultureIgnoreCase)) return i;
        }
        return NoMatches;
    }

    // --- selection -----------------------------------------------------------------

    public bool GetSelected(int index)
    {
        if (index < 0 || index >= _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        return _selection.Contains(index);
    }

    public void SetSelected(int index, bool value)
    {
        if (index < 0 || index >= _items.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_selectionMode == SelectionMode.None) throw new ArgumentException("SelectionMode is None.");
        bool changed;
        if (value)
        {
            if (_selectionMode == SelectionMode.One)
            {
                changed = !(_selection.Count == 1 && _selection[0] == index);
                _selection.Clear();
                _selection.Add(index);
            }
            else
            {
                changed = !_selection.Contains(index);
                if (changed)
                {
                    _selection.Add(index);
                    _selection.Sort();
                }
            }
            _focusedIndex = index;
        }
        else
        {
            changed = _selection.Remove(index);
        }
        if (changed)
        {
            Invalidate();
            OnSelectedIndexChanged(EventArgs.Empty);
        }
    }

    public void ClearSelected()
    {
        if (_selection.Count == 0) return;
        _selection.Clear();
        Invalidate();
        OnSelectedIndexChanged(EventArgs.Empty);
    }

    private void SelectRange(int from, int to)
    {
        _selection.Clear();
        for (int i = Math.Min(from, to); i <= Math.Max(from, to); i++) _selection.Add(i);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        // The data source's current item follows the selection (setting Position only when it differs: it
        // ends the current edit). Unselecting everything leaves the position alone.
        if (DataManager != null && DataManager.Position != SelectedIndex && (!FormattingEnabled || SelectedIndex != -1))
        {
            DataManager.Position = SelectedIndex;
        }
        SelectedIndexChanged?.Invoke(this, e);
    }

    // --- input ---------------------------------------------------------------------

    private void ClickItem(int index, Keys modifiers)
    {
        if (index < 0 || _selectionMode == SelectionMode.None) return;
        _focusedIndex = index;
        switch (_selectionMode)
        {
            case SelectionMode.One:
                SelectedIndex = index;
                return;
            case SelectionMode.MultiSimple:
                SetSelected(index, !GetSelected(index));
                return;
            case SelectionMode.MultiExtended:
                if ((modifiers & Keys.Shift) != 0 && _anchorIndex >= 0)
                {
                    SelectRange(_anchorIndex, index);
                }
                else if ((modifiers & Keys.Control) != 0)
                {
                    if (_selection.Contains(index)) _selection.Remove(index);
                    else { _selection.Add(index); _selection.Sort(); }
                    _anchorIndex = index;
                }
                else
                {
                    _selection.Clear();
                    _selection.Add(index);
                    _anchorIndex = index;
                }
                Invalidate();
                OnSelectedIndexChanged(EventArgs.Empty);
                return;
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (CanFocus) Focus();
            if (ScrollVisible && ScrollArea.Contains(e.Location))
            {
                _vscroll.MouseDown(e.Location);
            }
            else
            {
                int index = IndexFromPoint(e.Location);
                if (index >= 0) ClickItem(index, Control.ModifierKeys);
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _vscroll.MouseMove(e.Location, (e.Button & MouseButtons.Left) != 0);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _vscroll.MouseUp(e.Location);
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _vscroll.MouseLeave();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        int notches = e.Delta / 120;
        if (notches == 0) notches = Math.Sign(e.Delta);
        TopIndex -= notches * 3;
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int count = _items.Count;
        if (count > 0)
        {
            int current = _focusedIndex >= 0 ? _focusedIndex : SelectedIndex;
            int target = current;
            switch (e.KeyCode)
            {
                case Keys.Up: target = Math.Max(0, current - 1); break;
                case Keys.Down: target = Math.Min(count - 1, current + 1); break;
                case Keys.PageUp: target = Math.Max(0, current - VisibleRows() + 1); break;
                case Keys.PageDown: target = Math.Min(count - 1, current + VisibleRows() - 1); break;
                case Keys.Home: target = 0; break;
                case Keys.End: target = count - 1; break;
                case Keys.Space:
                    if (_selectionMode is SelectionMode.MultiSimple or SelectionMode.MultiExtended && current >= 0)
                    {
                        SetSelected(current, !GetSelected(current));
                        e.Handled = true;
                    }
                    base.OnKeyDown(e);
                    return;
                default:
                    base.OnKeyDown(e);
                    return;
            }
            if (target < 0) target = 0;
            e.Handled = true;
            _focusedIndex = target;
            EnsureVisible(target);
            if (_selectionMode == SelectionMode.One)
            {
                SelectedIndex = target;
            }
            else if (_selectionMode == SelectionMode.MultiExtended)
            {
                if (e.Shift && _anchorIndex >= 0) SelectRange(_anchorIndex, target);
                else if (!e.Control) { _selection.Clear(); _selection.Add(target); _anchorIndex = target; }
                Invalidate();
                OnSelectedIndexChanged(EventArgs.Empty);
            }
            else
            {
                Invalidate();
            }
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnResize(EventArgs e)
    {
        UpdateScroll();
        base.OnResize(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        UpdateScroll();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _vscroll.Enabled = Enabled;
        base.OnEnabledChanged(e);
    }

    // --- painting ------------------------------------------------------------------

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(Enabled ? BackColor : SystemColors.Control);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    protected virtual void OnDrawItem(DrawItemEventArgs e)
    {
        if (_drawMode != DrawMode.Normal)
        {
            DrawItem?.Invoke(this, e);
            return;
        }
        e.DrawBackground();
        if (e.Index >= 0 && e.Index < _items.Count)
        {
            var color = Enabled ? e.ForeColor : Theme.DisabledText;
            TextRenderer.DrawText(e.Graphics, GetItemText(_items[e.Index]), e.Font, e.Bounds, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
        e.DrawFocusRectangle();
    }

    protected virtual void OnMeasureItem(MeasureItemEventArgs e) => MeasureItem?.Invoke(this, e);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var area = ItemsArea;
        bool focused = Focused;

        if (area.Width > 0 && area.Height > 0)
        {
            var state = g.Save();
            g.IntersectClip(area);
            int rows = VisibleRows() + 1;
            for (int row = 0; row < rows; row++)
            {
                int index = _topIndex + row;
                if (index >= _items.Count) break;
                var rect = new Rectangle(area.X, area.Y + row * ItemHeight, area.Width, ItemHeight);
                if (!rect.IntersectsWith(e.ClipRectangle)) continue;
                var itemState = DrawItemState.None;
                if (_selection.Contains(index)) itemState |= DrawItemState.Selected;
                if (focused && index == _focusedIndex && ShowFocusCues) itemState |= DrawItemState.Focus;
                if (!Enabled) itemState |= DrawItemState.Disabled;
                var back = (itemState & DrawItemState.Selected) != 0 ? (focused ? Theme.Highlight : Theme.HighlightInactive) : BackColor;
                var fore = (itemState & DrawItemState.Selected) != 0 ? (focused ? Theme.HighlightText : ForeColor) : ForeColor;
                OnDrawItem(new DrawItemEventArgs(g, Font, rect, index, itemState & ~DrawItemState.Selected, fore, back));
            }
            g.Restore(state);
        }

        if (ScrollVisible)
        {
            _vscroll.Bounds = ScrollArea;
            _vscroll.Paint(g);
        }

        Panel.PaintBorder(g, ClientRectangle, _borderStyle);
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _vscroll.Dispose();
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + ", Items.Count: " + _items.Count;

    // --- collections ---------------------------------------------------------------

    public class ObjectCollection : IList, IList<object>
    {
        private readonly ListBox _owner;
        private readonly List<object> _items = new();

        public ObjectCollection(ListBox owner) => _owner = owner;

        public ObjectCollection(ListBox owner, ObjectCollection value) : this(owner) => AddRange(value);

        public ObjectCollection(ListBox owner, object[] value) : this(owner) => AddRange(value);

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public virtual object this[int index]
        {
            get => _items[index];
            set
            {
                _owner.CheckNoDataSource();
                SetItemInternal(index, value);
            }
        }

        internal void SetItemInternal(int index, object value)
        {
            ArgumentNullException.ThrowIfNull(value);
            _items[index] = value;
            _owner.ItemsChanged();
        }

        public int Add(object item)
        {
            _owner.CheckNoDataSource();
            ArgumentNullException.ThrowIfNull(item);
            int index;
            if (_owner._sorted)
            {
                index = 0;
                var text = _owner.GetItemText(item);
                while (index < _items.Count && string.Compare(_owner.GetItemText(_items[index]), text, StringComparison.CurrentCultureIgnoreCase) <= 0) index++;
                _items.Insert(index, item);
                _owner.ShiftSelectionForInsert(index);
            }
            else
            {
                _items.Add(item);
                index = _items.Count - 1;
            }
            _owner.ItemInserted(index);
            _owner.ItemsChanged();
            return index;
        }

        void ICollection<object>.Add(object item) => Add(item);

        public void AddRange(object[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            _owner.BeginUpdate();
            foreach (var i in items) Add(i);
            _owner.EndUpdate();
        }

        public void AddRange(ObjectCollection value)
        {
            ArgumentNullException.ThrowIfNull(value);
            AddRange(value._items.ToArray());
        }

        public void AddRange(IEnumerable<object> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            _owner.BeginUpdate();
            foreach (var i in items) Add(i);
            _owner.EndUpdate();
        }

        public void Insert(int index, object item)
        {
            _owner.CheckNoDataSource();
            ArgumentNullException.ThrowIfNull(item);
            if (_owner._sorted) { Add(item); return; }
            _items.Insert(index, item);
            _owner.ShiftSelectionForInsert(index);
            _owner.ItemInserted(index);
            _owner.ItemsChanged();
        }

        public void Clear()
        {
            _owner.CheckNoDataSource();
            _items.Clear();
            _owner._selection.Clear();
            _owner._focusedIndex = -1;
            _owner._topIndex = 0;
            _owner.ItemsCleared();
            _owner.ItemsChanged();
        }

        public bool Contains(object value) => _items.Contains(value);
        public int IndexOf(object value) => _items.IndexOf(value);

        public bool Remove(object value)
        {
            int i = _items.IndexOf(value);
            if (i < 0) return false;
            RemoveAt(i);
            return true;
        }

        public void RemoveAt(int index)
        {
            _owner.CheckNoDataSource();
            _items.RemoveAt(index);
            _owner._selection.Remove(index);
            for (int i = 0; i < _owner._selection.Count; i++)
            {
                if (_owner._selection[i] > index) _owner._selection[i]--;
            }
            _owner.ItemRemoved(index);
            _owner.ItemsChanged();
        }

        public void CopyTo(object[] destination, int arrayIndex) => _items.CopyTo(destination, arrayIndex);
        public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        internal void SortInternal()
        {
            // A stable sort that remembers where each item came from, so state kept per item follows it.
            var order = Enumerable.Range(0, _items.Count)
                .OrderBy(i => _owner.GetItemText(_items[i]), StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
            var sorted = order.Select(i => _items[i]).ToList();
            _items.Clear();
            _items.AddRange(sorted);
            _owner.ItemsReordered(order);
        }

        internal void SetFromDataSource(IList source)
        {
            _items.Clear();
            _owner.ItemsCleared();
            foreach (var o in source) if (o != null) _items.Add(o);
            for (int i = 0; i < _items.Count; i++) _owner.ItemInserted(i);
            _owner._selection.RemoveAll(i => i >= _items.Count);
            if (_owner._focusedIndex >= _items.Count) _owner._focusedIndex = -1;
            if (_owner._topIndex >= _items.Count) _owner._topIndex = 0;
            _owner.ItemsChanged();
        }

        bool IList.IsFixedSize => false;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => this[index]; set => this[index] = value!; }
        int IList.Add(object? value) => Add(value!);
        bool IList.Contains(object? value) => value != null && Contains(value);
        int IList.IndexOf(object? value) => value == null ? -1 : IndexOf(value);
        void IList.Insert(int index, object? value) => Insert(index, value!);
        void IList.Remove(object? value) { if (value != null) Remove(value); }
        void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    }

    public class SelectedIndexCollection : IList, IReadOnlyList<int>
    {
        private readonly ListBox _owner;

        public SelectedIndexCollection(ListBox owner) => _owner = owner;

        public int Count => _owner._selection.Count;
        public int this[int index] => _owner._selection[index];
        public bool Contains(int selectedIndex) => _owner._selection.Contains(selectedIndex);
        public int IndexOf(int selectedIndex) => _owner._selection.IndexOf(selectedIndex);
        public void Add(int index) => _owner.SetSelected(index, true);
        public void Remove(int index) => _owner.SetSelected(index, false);
        public void Clear() => _owner.ClearSelected();
        public void CopyTo(Array destination, int index) => ((ICollection)_owner._selection).CopyTo(destination, index);
        public IEnumerator<int> GetEnumerator() => _owner._selection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _owner._selection.GetEnumerator();

        bool IList.IsReadOnly => true;
        bool IList.IsFixedSize => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) => throw new NotSupportedException();
        bool IList.Contains(object? value) => value is int i && Contains(i);
        int IList.IndexOf(object? value) => value is int i ? IndexOf(i) : -1;
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => throw new NotSupportedException();
        void IList.RemoveAt(int index) => throw new NotSupportedException();
        void IList.Clear() => Clear();
    }

    public class SelectedObjectCollection : IList, IReadOnlyList<object>
    {
        private readonly ListBox _owner;

        public SelectedObjectCollection(ListBox owner) => _owner = owner;

        public int Count => _owner._selection.Count;
        public object this[int index] => _owner._items[_owner._selection[index]];
        public bool Contains(object? selectedObject) => selectedObject != null && IndexOf(selectedObject) >= 0;

        public int IndexOf(object? selectedObject)
        {
            if (selectedObject == null) return -1;
            for (int i = 0; i < Count; i++) if (Equals(this[i], selectedObject)) return i;
            return -1;
        }

        public void Add(object value) => _owner.SetSelected(_owner._items.IndexOf(value), true);
        public void Remove(object value) { int i = _owner._items.IndexOf(value); if (i >= 0) _owner.SetSelected(i, false); }
        public void Clear() => _owner.ClearSelected();
        public void CopyTo(Array destination, int index) { for (int i = 0; i < Count; i++) destination.SetValue(this[i], index + i); }
        public IEnumerator<object> GetEnumerator() { for (int i = 0; i < Count; i++) yield return this[i]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        bool IList.IsReadOnly => true;
        bool IList.IsFixedSize => true;
        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;
        object? IList.this[int index] { get => this[index]; set => throw new NotSupportedException(); }
        int IList.Add(object? value) { Add(value!); return IndexOf(value); }
        bool IList.Contains(object? value) => Contains(value);
        int IList.IndexOf(object? value) => IndexOf(value);
        void IList.Insert(int index, object? value) => throw new NotSupportedException();
        void IList.Remove(object? value) => Remove(value!);
        void IList.RemoveAt(int index) => throw new NotSupportedException();
        void IList.Clear() => Clear();
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

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
