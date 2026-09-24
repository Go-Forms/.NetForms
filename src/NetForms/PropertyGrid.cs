using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// The property browser. Rows come from <see cref="TypeDescriptor"/>, so anything that describes
/// itself with ComponentModel attributes - including our own controls - shows up the way it does in
/// the designer: categories, descriptions, read-only flags and nested (expandable) values.
/// </summary>
public class PropertyGrid : ContainerControl
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

    private const int RowHeight = 20;
    private const int IndentWidth = 16;
    private const int GlyphWidth = 9;

    private readonly ScrollBarCore _vscroll;
    private object?[] _selectedObjects = Array.Empty<object?>();
    private RootItem? _root;
    private readonly List<PropertyGridRow> _rows = new();
    private GridItem? _selected;
    private PropertySort _propertySort = PropertySort.CategorizedAlphabetical;
    private int _splitterX = -1;
    private bool _draggingSplitter;
    private int _scrollY;
    private bool _helpVisible = true;
    private Control? _editor;
    private PropertyItem? _editing;

    public PropertyGrid()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.ResizeRedraw, true);
        _vscroll = new ScrollBarCore(this, vertical: true, (v, _) => ScrollTo(v));
    }

    protected override Size DefaultSize => new Size(130, 130);

    // --- selection ----------------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Sets the currently selected object that the grid will browse.")]
    [DefaultValue(null)]
    public object? SelectedObject
    {
        get => _selectedObjects.Length > 0 ? _selectedObjects[0] : null;
        set => SelectedObjects = value == null ? Array.Empty<object?>() : new[] { value };
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object?[] SelectedObjects
    {
        get => (object?[])_selectedObjects.Clone();
        set
        {
            _selectedObjects = value ?? Array.Empty<object?>();
            EndEdit(commit: false);
            Rebuild();
            OnSelectedObjectsChanged(EventArgs.Empty);
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public GridItem? SelectedGridItem
    {
        get => _selected;
        set => SelectItem(value);
    }

    /// <summary>
    /// The invisible root of the item tree. WinForms has no such property - there it is reached
    /// through <c>SelectedGridItem.Parent</c> - so this is a NetForms convenience, not a copy.
    /// </summary>
    public GridItem? RootGridItem => _root;

    // --- appearance -----------------------------------------------------------------------------

    [Category("Appearance")]
    [Description("Sets the type of sorting that the PropertyGrid will use to display properties.")]
    [DefaultValue(PropertySort.CategorizedAlphabetical)]
    public PropertySort PropertySort
    {
        get => _propertySort;
        set
        {
            if (_propertySort == value) return;
            _propertySort = value;
            Rebuild();
        }
    }

    [Category("Appearance")]
    [Description("Sets whether to show the description pane.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool HelpVisible
    {
        get => _helpVisible;
        set
        {
            if (_helpVisible == value) return;
            _helpVisible = value;
            LayoutRows();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Sets whether to display the ToolBar at the top of the PropertyGrid.")]
    [DefaultValue(true)]
    public bool ToolbarVisible { get; set; } = true;

    [Category("Appearance")]
    [Description("Sets buttons to large (32x32) on the PropertyGrid tool bar.")]
    [DefaultValue(false)]
    public bool LargeButtons { get; set; }

    [Description("Sets whether the commands window will be displayed for components with commands.")]
    [Browsable(false)]
    public bool CanShowCommands => false;

    [Category("Appearance")]
    [Description("The text color used for category headings. The background color is determined by the LineColor property.")]
    [DefaultValue(typeof(Color), "ControlText")]
    public Color CategoryForeColor { get; set; } = SystemColors.ControlText;

    [Category("Appearance")]
    [Description("The color of the line that separates categories.")]
    [DefaultValue(typeof(Color), "Control")]
    public Color CategorySplitterColor { get; set; } = SystemColors.Control;

    [Category("Appearance")]
    [Description("The background color of the description pane.")]
    [DefaultValue(typeof(Color), "Control")]
    public Color HelpBackColor { get; set; } = SystemColors.Control;

    [Category("Appearance")]
    [Description("The foreground color of the description pane.")]
    [DefaultValue(typeof(Color), "ControlText")]
    public Color HelpForeColor { get; set; } = SystemColors.ControlText;

    [Category("Appearance")]
    [Description("Sets the color of the borders and grid lines within the grid area.")]
    [DefaultValue(typeof(Color), "InactiveBorder")]
    public Color LineColor { get; set; } = SystemColors.InactiveBorder;

    [Category("Appearance")]
    [Description("Sets the background color of the grid area.")]
    [DefaultValue(typeof(Color), "Window")]
    public Color ViewBackColor { get; set; } = SystemColors.Window;

    [Category("Appearance")]
    [Description("Sets the foreground color of the grid area.")]
    [DefaultValue(typeof(Color), "WindowText")]
    public Color ViewForeColor { get; set; } = SystemColors.WindowText;

    [Category("Appearance")]
    [Description("The background color of selected items that have focus.")]
    [DefaultValue(typeof(Color), "Highlight")]
    public Color SelectedItemWithFocusBackColor { get; set; } = SystemColors.Highlight;

    [Category("Appearance")]
    [Description("The foreground color of selected items that have focus.")]
    [DefaultValue(typeof(Color), "HighlightText")]
    public Color SelectedItemWithFocusForeColor { get; set; } = SystemColors.HighlightText;

    [Category("Appearance")]
    [Description("The background color of the component.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Control;
        set => base.BackColor = value;
    }

    // --- events -----------------------------------------------------------------------------------

    [Category("Property Changed")]
    [Description("Occurs when a property value changes.")]
    public event PropertyValueChangedEventHandler? PropertyValueChanged;

    [Category("Property Changed")]
    [Description("Occurs when the SelectedObjects property has changed.")]
    public event EventHandler? SelectedObjectsChanged;

    [Category("Property Changed")]
    [Description("Occurs when the selected grid item has changed.")]
    public event SelectedGridItemChangedEventHandler? SelectedGridItemChanged;

    [Category("Property Changed")]
    [Description("Occurs when the PropertySort property on the PropertyGrid has changed.")]
    public event EventHandler? PropertySortChanged;

    protected virtual void OnPropertyValueChanged(PropertyValueChangedEventArgs e) => PropertyValueChanged?.Invoke(this, e);
    protected virtual void OnSelectedObjectsChanged(EventArgs e) => SelectedObjectsChanged?.Invoke(this, e);
    protected virtual void OnSelectedGridItemChanged(SelectedGridItemChangedEventArgs e) => SelectedGridItemChanged?.Invoke(this, e);
    protected virtual void OnPropertySortChanged(EventArgs e) => PropertySortChanged?.Invoke(this, e);

    // --- the item tree -------------------------------------------------------------------------------

    /// <summary>Re-reads the selected object's properties, as WinForms' PropertyGrid.Refresh does.</summary>
    public override void Refresh()
    {
        Rebuild();
        base.Refresh();
    }

    public void RefreshTabs(PropertyTabScope tabScope) => Rebuild();

    public void CollapseAllGridItems() => SetAllExpanded(false);

    public void ExpandAllGridItems() => SetAllExpanded(true);

    private void SetAllExpanded(bool expanded)
    {
        if (_root == null) return;
        foreach (var item in Flatten(_root.GridItems))
        {
            if (item.Expandable) item.Expanded = expanded;
        }
        LayoutRows();
        Invalidate();
    }

    private static IEnumerable<GridItem> Flatten(GridItemCollection items)
    {
        foreach (var item in items)
        {
            yield return item;
            foreach (var child in Flatten(item.GridItems)) yield return child;
        }
    }

    private void Rebuild()
    {
        var target = SelectedObject;
        if (target == null)
        {
            _root = null;
            _selected = null;
            _rows.Clear();
            Invalidate();
            return;
        }

        var properties = TypeDescriptor.GetProperties(target);
        var visible = new List<PropertyDescriptor>();
        foreach (PropertyDescriptor property in properties)
        {
            if (!property.IsBrowsable) continue;
            // With more than one object selected, only the properties they share are shown.
            if (_selectedObjects.Length > 1 && !SharedByAll(property)) continue;
            visible.Add(property);
        }

        bool alphabetical = _propertySort is PropertySort.Alphabetical or PropertySort.CategorizedAlphabetical;
        if (alphabetical) visible.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));

        _root = new RootItem(this, target);
        if (_propertySort is PropertySort.Categorized or PropertySort.CategorizedAlphabetical)
        {
            var categories = new List<string>();
            var byCategory = new Dictionary<string, List<PropertyDescriptor>>(StringComparer.CurrentCulture);
            foreach (var property in visible)
            {
                var name = string.IsNullOrEmpty(property.Category) ? "Misc" : property.Category;
                if (!byCategory.TryGetValue(name, out var list))
                {
                    list = new List<PropertyDescriptor>();
                    byCategory[name] = list;
                    categories.Add(name);
                }
                list.Add(property);
            }
            categories.Sort(StringComparer.CurrentCulture);
            foreach (var name in categories)
            {
                var category = new CategoryItem(this, _root, name);
                foreach (var property in byCategory[name]) category.Children.Add(new PropertyItem(this, category, property, target));
                _root.Children.Add(category);
            }
        }
        else
        {
            foreach (var property in visible) _root.Children.Add(new PropertyItem(this, _root, property, target));
        }

        LayoutRows();

        // WinForms lands the selection on the first property as soon as an object is set.
        _selected = null;
        foreach (var row in _rows)
        {
            if (row.Item.GridItemType != GridItemType.Property) continue;
            SelectItem(row.Item);
            break;
        }
        Invalidate();
    }

    private bool SharedByAll(PropertyDescriptor property)
    {
        foreach (var target in _selectedObjects)
        {
            if (target == null) return false;
            var match = TypeDescriptor.GetProperties(target).Find(property.Name, false);
            if (match == null || match.PropertyType != property.PropertyType) return false;
        }
        return true;
    }

    /// <summary>Writes a value to every selected object, so a multi-selection edits them all.</summary>
    internal void SetValueOnTargets(PropertyDescriptor property, object? value)
    {
        foreach (var target in _selectedObjects)
        {
            if (target == null) continue;
            var match = TypeDescriptor.GetProperties(target).Find(property.Name, false);
            if (match == null || match.IsReadOnly) continue;
            try
            {
                match.SetValue(target, value);
            }
            catch (Exception)
            {
                // A property that rejects the value keeps its old one; the row simply refreshes.
            }
        }
    }

    // --- rows ------------------------------------------------------------------------------------------

    private readonly struct PropertyGridRow
    {
        public PropertyGridRow(GridItem item, int depth)
        {
            Item = item;
            Depth = depth;
        }

        public GridItem Item { get; }
        public int Depth { get; }
    }

    private void LayoutRows()
    {
        _rows.Clear();
        if (_root != null) AddRows(_root.GridItems, 0);

        var area = RowsRectangle;
        int content = _rows.Count * RowHeight;
        bool needed = content > area.Height;
        _vscroll.Minimum = 0;
        _vscroll.Maximum = Math.Max(0, content - 1);
        _vscroll.LargeChange = Math.Max(1, area.Height);
        _vscroll.SmallChange = RowHeight;
        _vscroll.Bounds = new Rectangle(Width - 1 - ScrollBarCore.Thickness, 1, ScrollBarCore.Thickness, Math.Max(0, area.Height));
        _vscroll.Enabled = Enabled;
        _scrollVisible = needed;
        _scrollY = Math.Clamp(_scrollY, 0, Math.Max(0, content - area.Height));
        _vscroll.Value = _scrollY;
        PositionEditor();
    }

    private bool _scrollVisible;

    private void AddRows(GridItemCollection items, int depth)
    {
        foreach (var item in items)
        {
            _rows.Add(new PropertyGridRow(item, depth));
            if (item.Expanded) AddRows(item.GridItems, depth + 1);
        }
    }

    private Rectangle RowsRectangle
    {
        get
        {
            int height = Math.Max(0, Height - 2 - (_helpVisible ? HelpHeight : 0));
            return new Rectangle(1, 1, Math.Max(0, Width - 2), height);
        }
    }

    private int HelpHeight => Math.Max(40, Font.Height * 3 + 10);

    private Rectangle HelpRectangle => new Rectangle(1, Math.Max(0, Height - 1 - HelpHeight), Math.Max(0, Width - 2), HelpHeight);

    /// <summary>The x of the splitter between the label and value columns.</summary>
    public int SplitterPosition
    {
        get => _splitterX > 0 ? _splitterX : Math.Max(60, Width / 2);
        set
        {
            _splitterX = Math.Clamp(value, 40, Math.Max(41, Width - 40));
            PositionEditor();
            Invalidate();
        }
    }

    private Rectangle RowBounds(int index)
    {
        var area = RowsRectangle;
        return new Rectangle(area.X, area.Y + index * RowHeight - _scrollY, area.Width - (_scrollVisible ? ScrollBarCore.Thickness : 0), RowHeight);
    }

    private Rectangle ValueBounds(int index)
    {
        var row = RowBounds(index);
        int x = SplitterPosition + 1;
        return new Rectangle(x, row.Y, Math.Max(0, row.Right - x), row.Height);
    }

    private int RowAt(Point p)
    {
        var area = RowsRectangle;
        if (!area.Contains(p)) return -1;
        int index = (p.Y - area.Y + _scrollY) / RowHeight;
        return index >= 0 && index < _rows.Count ? index : -1;
    }

    private void ScrollTo(int y)
    {
        var area = RowsRectangle;
        int max = Math.Max(0, _rows.Count * RowHeight - area.Height);
        int next = Math.Clamp(y, 0, max);
        if (next == _scrollY) return;
        _scrollY = next;
        _vscroll.Value = next;
        PositionEditor();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutRows();
    }

    // --- selection and editing ---------------------------------------------------------------------------

    internal void SelectItem(GridItem? item)
    {
        if (ReferenceEquals(_selected, item)) return;
        EndEdit(commit: true);
        var old = _selected;
        _selected = item;
        OnSelectedGridItemChanged(new SelectedGridItemChangedEventArgs(old, item));
        Invalidate();
    }

    public void ResetSelectedProperty()
    {
        if (_selected is not PropertyItem property) return;
        var descriptor = property.PropertyDescriptor!;
        object? old = property.Value;
        foreach (var target in _selectedObjects)
        {
            if (target != null && descriptor.CanResetValue(target)) descriptor.ResetValue(target);
        }
        OnPropertyValueChanged(new PropertyValueChangedEventArgs(property, old));
        Refresh();
    }

    private void BeginEdit(PropertyItem item)
    {
        if (item.IsReadOnly) return;
        EndEdit(commit: true);
        _editing = item;

        var choices = item.GetChoices();
        if (choices != null)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Visible = false };
            foreach (var choice in choices) combo.Items.Add(choice);
            combo.SelectedItem = item.FormattedValue;
            combo.SelectedIndexChanged += (_, _) => CommitEditor();
            _editor = combo;
        }
        else
        {
            var box = new TextBox { BorderStyle = BorderStyle.None, Visible = false, Text = item.FormattedValue };
            box.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Return) { EndEdit(commit: true); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) { EndEdit(commit: false); e.Handled = true; }
            };
            box.LostFocus += (_, _) => EndEdit(commit: true);
            _editor = box;
        }

        Controls.Add(_editor);
        PositionEditor();
        _editor.Visible = true;
        _editor.Focus();
    }

    private void CommitEditor()
    {
        if (_editing == null || _editor is not ComboBox combo) return;
        ApplyEditorValue(combo.SelectedItem?.ToString());
    }

    private void PositionEditor()
    {
        if (_editor == null || _editing == null) return;
        int index = IndexOf(_editing);
        if (index < 0)
        {
            _editor.Visible = false;
            return;
        }
        var bounds = ValueBounds(index);
        _editor.Bounds = new Rectangle(bounds.X + 2, bounds.Y + 1, Math.Max(0, bounds.Width - 4), Math.Max(0, bounds.Height - 2));
    }

    private int IndexOf(GridItem item)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (ReferenceEquals(_rows[i].Item, item)) return i;
        }
        return -1;
    }

    private void EndEdit(bool commit)
    {
        if (_editing == null || _editor == null) return;
        string? text = _editor switch
        {
            TextBox box => box.Text,
            ComboBox combo => combo.SelectedItem?.ToString(),
            _ => null,
        };
        var editor = _editor;
        _editor = null;
        Controls.Remove(editor);
        editor.Dispose();

        if (commit) ApplyEditorValue(text);
        _editing = null;
        Invalidate();
    }

    private void ApplyEditorValue(string? text)
    {
        var item = _editing;
        if (item == null) return;
        object? old = item.Value;
        if (item.TryParse(text, out object? parsed))
        {
            SetValueOnTargets(item.PropertyDescriptor!, parsed);
            OnPropertyValueChanged(new PropertyValueChangedEventArgs(item, old));
        }
    }

    // --- input ------------------------------------------------------------------------------------------------

    internal override bool IsOverlayPoint(Point p) => _scrollVisible && _vscroll.Bounds.Contains(p);

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (CanFocus) Focus();
        if (e.Button == MouseButtons.Left && _scrollVisible && _vscroll.Bounds.Contains(e.Location))
        {
            _vscroll.MouseDown(e.Location);
            base.OnMouseDown(e);
            return;
        }

        if (e.Button == MouseButtons.Left && Math.Abs(e.X - SplitterPosition) <= 3 && RowsRectangle.Contains(e.Location))
        {
            _draggingSplitter = true;
            Capture = true;
            base.OnMouseDown(e);
            return;
        }

        int index = RowAt(e.Location);
        if (index >= 0)
        {
            var row = _rows[index];
            SelectItem(row.Item);

            var glyph = GlyphBounds(index, row.Depth);
            if (row.Item.Expandable && glyph.Contains(e.Location))
            {
                row.Item.Expanded = !row.Item.Expanded;
                LayoutRows();
                Invalidate();
            }
            else if (row.Item is PropertyItem property && e.X > SplitterPosition)
            {
                BeginEdit(property);
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_draggingSplitter)
        {
            SplitterPosition = e.X;
            base.OnMouseMove(e);
            return;
        }
        if (_scrollVisible) _vscroll.MouseMove(e.Location, (e.Button & MouseButtons.Left) != 0);
        Cursor = Math.Abs(e.X - SplitterPosition) <= 3 && RowsRectangle.Contains(e.Location) ? Cursors.VSplit : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_draggingSplitter)
        {
            _draggingSplitter = false;
            Capture = false;
        }
        if (_scrollVisible) _vscroll.MouseUp(e.Location);
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
        ScrollTo(_scrollY - notches * RowHeight * 3);
        base.OnMouseWheel(e);
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        // A double click on a label toggles an expandable row, as WinForms does.
        if (_selected is { Expandable: true } item)
        {
            item.Expanded = !item.Expanded;
            LayoutRows();
            Invalidate();
        }
        base.OnDoubleClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int index = _selected != null ? IndexOf(_selected) : -1;
        switch (e.KeyCode)
        {
            case Keys.Up when index > 0:
                SelectRow(index - 1);
                e.Handled = true;
                break;
            case Keys.Down when index >= 0 && index + 1 < _rows.Count:
                SelectRow(index + 1);
                e.Handled = true;
                break;
            case Keys.Down when index < 0 && _rows.Count > 0:
                SelectRow(0);
                e.Handled = true;
                break;
            case Keys.Left when _selected is { Expandable: true, Expanded: true } collapse:
                collapse.Expanded = false;
                LayoutRows();
                Invalidate();
                e.Handled = true;
                break;
            case Keys.Right when _selected is { Expandable: true, Expanded: false } expand:
                expand.Expanded = true;
                LayoutRows();
                Invalidate();
                e.Handled = true;
                break;
            case Keys.Return or Keys.F4 when _selected is PropertyItem property:
                BeginEdit(property);
                e.Handled = true;
                break;
        }
        base.OnKeyDown(e);
    }

    private void SelectRow(int index)
    {
        SelectItem(_rows[index].Item);
        var bounds = RowBounds(index);
        var area = RowsRectangle;
        if (bounds.Top < area.Top) ScrollTo(_scrollY - (area.Top - bounds.Top));
        else if (bounds.Bottom > area.Bottom) ScrollTo(_scrollY + (bounds.Bottom - area.Bottom));
    }

    // --- painting ----------------------------------------------------------------------------------------------

    private Rectangle GlyphBounds(int index, int depth)
    {
        var row = RowBounds(index);
        int x = row.X + depth * IndentWidth + 2;
        return new Rectangle(x, row.Y + (row.Height - GlyphWidth) / 2, GlyphWidth, GlyphWidth);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(ViewBackColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var area = RowsRectangle;

        var state = g.Save();
        g.IntersectClip(area);
        for (int i = 0; i < _rows.Count; i++)
        {
            var bounds = RowBounds(i);
            if (bounds.Bottom < area.Top || bounds.Top > area.Bottom) continue;
            PaintRow(g, i, _rows[i], bounds);
        }
        g.Restore(state);

        if (_helpVisible) PaintHelp(g);

        using (var border = new Pen(Theme.WindowBorder))
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
        base.OnPaint(e);
    }

    internal override void OnPaintOverlay(Graphics g)
    {
        if (_scrollVisible) _vscroll.Paint(g);
        base.OnPaintOverlay(g);
    }

    private void PaintRow(Graphics g, int index, PropertyGridRow row, Rectangle bounds)
    {
        bool selected = ReferenceEquals(row.Item, _selected);
        int labelLeft = bounds.X + row.Depth * IndentWidth + (row.Item.GridItemType == GridItemType.Category ? 2 : IndentWidth + 2);

        if (row.Item.GridItemType == GridItemType.Category)
        {
            using (var back = new SolidBrush(SystemColors.Control))
            {
                g.FillRectangle(back, bounds);
            }
            using var bold = new Font(Font, FontStyle.Bold);
            TextRenderer.DrawText(g, row.Item.Label, bold, new Rectangle(labelLeft + GlyphWidth + 4, bounds.Y, Math.Max(0, bounds.Width - labelLeft), bounds.Height),
                CategoryForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
        else
        {
            var labelRect = new Rectangle(labelLeft, bounds.Y, Math.Max(0, SplitterPosition - labelLeft), bounds.Height);
            if (selected)
            {
                using var back = new SolidBrush(Focused ? SelectedItemWithFocusBackColor : Theme.HighlightInactive);
                g.FillRectangle(back, labelRect);
            }
            var labelColor = selected ? SelectedItemWithFocusForeColor : ViewForeColor;
            TextRenderer.DrawText(g, row.Item.Label, Font, labelRect, labelColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            var valueRect = ValueBounds(index);
            bool readOnly = row.Item is PropertyItem { IsReadOnly: true };
            if (!ReferenceEquals(row.Item, _editing))
            {
                TextRenderer.DrawText(g, (row.Item as PropertyItem)?.FormattedValue ?? string.Empty, Font,
                    new Rectangle(valueRect.X + 2, valueRect.Y, Math.Max(0, valueRect.Width - 4), valueRect.Height),
                    readOnly ? Theme.DisabledText : ViewForeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }

        using (var line = new Pen(LineColor))
        {
            g.DrawLine(line, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            if (row.Item.GridItemType != GridItemType.Category)
            {
                g.DrawLine(line, SplitterPosition, bounds.Top, SplitterPosition, bounds.Bottom);
            }
        }

        if (row.Item.Expandable) PaintGlyph(g, GlyphBounds(index, row.Depth), row.Item.Expanded);
    }

    private void PaintGlyph(Graphics g, Rectangle box, bool expanded)
    {
        using var pen = new Pen(ViewForeColor);
        g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
        int cx = box.X + box.Width / 2;
        int cy = box.Y + box.Height / 2;
        g.DrawLine(pen, box.X + 2, cy, box.Right - 3, cy);
        if (!expanded) g.DrawLine(pen, cx, box.Y + 2, cx, box.Bottom - 3);
    }

    private void PaintHelp(Graphics g)
    {
        var help = HelpRectangle;
        using (var back = new SolidBrush(HelpBackColor))
        {
            g.FillRectangle(back, help);
        }
        using (var line = new Pen(LineColor))
        {
            g.DrawLine(line, help.Left, help.Top, help.Right, help.Top);
        }

        var item = _selected;
        if (item == null) return;
        using var bold = new Font(Font, FontStyle.Bold);
        var titleRect = new Rectangle(help.X + 4, help.Y + 3, Math.Max(0, help.Width - 8), Font.Height);
        TextRenderer.DrawText(g, item.Label, bold, titleRect, HelpForeColor,
            TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        string description = (item as PropertyItem)?.Description ?? string.Empty;
        var bodyRect = new Rectangle(help.X + 4, titleRect.Bottom + 1, Math.Max(0, help.Width - 8), Math.Max(0, help.Bottom - titleRect.Bottom - 4));
        TextRenderer.DrawText(g, description, Font, bodyRect, HelpForeColor,
            TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _vscroll.Dispose();
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + ", SelectedObject: " + (SelectedObject?.ToString() ?? "(none)");

    // --- item implementations --------------------------------------------------------------------------------------

    private sealed class RootItem : GridItem
    {
        private readonly PropertyGrid _owner;
        private readonly object _target;

        public RootItem(PropertyGrid owner, object target)
        {
            _owner = owner;
            _target = target;
            Children = new GridItemCollection(Array.Empty<GridItem>());
        }

        internal GridItemCollection Children { get; }

        public override GridItemCollection GridItems => Children;
        public override GridItemType GridItemType => GridItemType.Root;
        public override string? Label => TypeDescriptor.GetClassName(_target);
        public override GridItem? Parent => null;
        public override PropertyDescriptor? PropertyDescriptor => null;
        public override object? Value => _target;
        public override bool Select() => true;
    }

    private sealed class CategoryItem : GridItem
    {
        private readonly PropertyGrid _owner;
        private bool _expanded = true;

        public CategoryItem(PropertyGrid owner, GridItem parent, string label)
        {
            _owner = owner;
            Parent = parent;
            Label = label;
            Children = new GridItemCollection(Array.Empty<GridItem>());
        }

        internal GridItemCollection Children { get; }

        public override GridItemCollection GridItems => Children;
        public override GridItemType GridItemType => GridItemType.Category;
        public override string? Label { get; }
        public override GridItem? Parent { get; }
        public override PropertyDescriptor? PropertyDescriptor => null;
        public override object? Value => null;
        public override bool Expandable => true;

        public override bool Expanded
        {
            get => _expanded;
            set => _expanded = value;
        }

        public override bool Select()
        {
            _owner.SelectItem(this);
            return true;
        }
    }

    private sealed class PropertyItem : GridItem
    {
        private readonly PropertyGrid _owner;
        private readonly object _target;
        private readonly PropertyDescriptor _property;
        private GridItemCollection? _children;
        private bool _expanded;

        public PropertyItem(PropertyGrid owner, GridItem parent, PropertyDescriptor property, object target)
        {
            _owner = owner;
            Parent = parent;
            _property = property;
            _target = target;
        }

        public override GridItemCollection GridItems => _children ??= BuildChildren();
        public override GridItemType GridItemType => GridItemType.Property;
        public override string? Label => _property.DisplayName;
        public override GridItem? Parent { get; }
        public override PropertyDescriptor? PropertyDescriptor => _property;

        public override object? Value
        {
            get
            {
                try
                {
                    return _property.GetValue(_target);
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public string Description => _property.Description;

        public bool IsReadOnly => _property.IsReadOnly;

        /// <summary>A value with sub-properties (Size, Point, Padding) can be expanded, as in the designer.</summary>
        public override bool Expandable
        {
            get
            {
                var converter = _property.Converter;
                return converter.GetPropertiesSupported() && GridItems.Count > 0;
            }
        }

        public override bool Expanded
        {
            get => _expanded;
            set => _expanded = value;
        }

        private GridItemCollection BuildChildren()
        {
            var converter = _property.Converter;
            object? value = Value;
            if (value == null || !converter.GetPropertiesSupported()) return GridItemCollection.Empty;
            var properties = converter.GetProperties(null, value, null);
            if (properties == null || properties.Count == 0) return GridItemCollection.Empty;

            var items = new List<GridItem>();
            foreach (PropertyDescriptor child in properties)
            {
                if (child.IsBrowsable) items.Add(new PropertyItem(_owner, this, child, value));
            }
            return new GridItemCollection(items);
        }

        public string FormattedValue
        {
            get
            {
                object? value = Value;
                if (value == null) return string.Empty;
                try
                {
                    return _property.Converter.ConvertToString(null, CultureInfo.CurrentCulture, value) ?? string.Empty;
                }
                catch (Exception)
                {
                    return value.ToString() ?? string.Empty;
                }
            }
        }

        /// <summary>The drop-down list for an enum or a bool, or null when the value is typed in.</summary>
        public string[]? GetChoices()
        {
            var type = Nullable.GetUnderlyingType(_property.PropertyType) ?? _property.PropertyType;
            if (type == typeof(bool)) return new[] { "False", "True" };
            if (type.IsEnum) return Enum.GetNames(type);

            var converter = _property.Converter;
            if (!converter.GetStandardValuesSupported()) return null;
            var values = converter.GetStandardValues();
            if (values == null) return null;
            var result = new List<string>();
            foreach (var value in values)
            {
                result.Add(converter.ConvertToString(null, CultureInfo.CurrentCulture, value) ?? string.Empty);
            }
            return result.Count > 0 ? result.ToArray() : null;
        }

        public bool TryParse(string? text, out object? value)
        {
            value = null;
            if (IsReadOnly) return false;
            try
            {
                value = _property.Converter.ConvertFromString(null, CultureInfo.CurrentCulture, text ?? string.Empty);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public override bool Select()
        {
            _owner.SelectItem(this);
            return true;
        }
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoScroll { get => base.AutoScroll; set => base.AutoScroll = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ControlCollection Controls => base.Controls;

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyEventHandler? KeyDown
    {
        add => base.KeyDown += value;
        remove => base.KeyDown -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyPressEventHandler? KeyPress
    {
        add => base.KeyPress += value;
        remove => base.KeyPress -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyEventHandler? KeyUp
    {
        add => base.KeyUp += value;
        remove => base.KeyUp -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseDown
    {
        add => base.MouseDown += value;
        remove => base.MouseDown -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? MouseEnter
    {
        add => base.MouseEnter += value;
        remove => base.MouseEnter -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? MouseLeave
    {
        add => base.MouseLeave += value;
        remove => base.MouseLeave -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseMove
    {
        add => base.MouseMove += value;
        remove => base.MouseMove -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseUp
    {
        add => base.MouseUp += value;
        remove => base.MouseUp -= value;
    }

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

public enum PropertyTabScope
{
    Static = 0,
    Global = 1,
    Document = 2,
    Component = 3,
}
