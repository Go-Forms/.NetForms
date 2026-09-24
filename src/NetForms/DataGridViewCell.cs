using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>Anything that belongs to a <see cref="DataGridView"/> and carries element state.</summary>
public class DataGridViewElement
{
    public DataGridView? DataGridView { get; internal set; }

    public virtual DataGridViewElementStates State { get; internal set; } = DataGridViewElementStates.Visible;

    protected virtual void OnDataGridViewChanged() { }

    internal void SetDataGridView(DataGridView? grid)
    {
        if (ReferenceEquals(DataGridView, grid)) return;
        DataGridView = grid;
        OnDataGridViewChanged();
    }
}

/// <summary>
/// One cell. The value lives in the cell only for an unbound grid; when the grid has a
/// <see cref="DataGridView.DataSource"/> the cell reads and writes the bound object's property,
/// so the grid and the data stay in step without a second copy of the values.
/// </summary>
public abstract class DataGridViewCell : DataGridViewElement, ICloneable
{
    private object? _value;
    private DataGridViewCellStyle? _style;

    protected DataGridViewCell() { }

    public DataGridViewColumn? OwningColumn { get; internal set; }

    public DataGridViewRow? OwningRow { get; internal set; }

    public int ColumnIndex => OwningColumn?.Index ?? -1;

    public int RowIndex => OwningRow?.Index ?? -1;

    public object? Tag { get; set; }

    public string ToolTipText { get; set; } = string.Empty;

    public string ErrorText { get; set; } = string.Empty;

    /// <summary>The type a cell of this kind holds when its column does not say (WinForms: the column's ValueType is null until set).</summary>
    internal virtual Type DefaultValueType => typeof(object);

    public virtual Type ValueType
    {
        get => OwningColumn?.ValueType ?? DefaultValueType;
        set { if (OwningColumn != null) OwningColumn.ValueType = value; }
    }

    public virtual Type FormattedValueType => typeof(string);

    /// <summary>The control type used to edit this cell, or null when it is not editable in place.</summary>
    public virtual Type? EditType => typeof(TextBox);

    public virtual object? Value
    {
        get => DataGridView != null && DataGridView.IsBound && RowIndex >= 0
            ? DataGridView.GetBoundValue(RowIndex, ColumnIndex)
            : _value;
        set => SetValue(RowIndex, value);
    }

    protected virtual bool SetValue(int rowIndex, object? value)
    {
        if (DataGridView != null && DataGridView.IsBound && rowIndex >= 0)
        {
            if (!DataGridView.SetBoundValue(rowIndex, ColumnIndex, value)) return false;
        }
        else
        {
            if (Equals(_value, value)) return true;
            _value = value;
        }
        DataGridView?.NotifyCellValueChanged(ColumnIndex, rowIndex);
        return true;
    }

    /// <summary>The value as the cell shows it: <see cref="Value"/> run through the style's format.</summary>
    public object? FormattedValue => GetFormattedValue(Value, RowIndex, InheritedStyle, DataGridViewDataErrorContexts.Formatting);

    public virtual object? GetFormattedValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle, DataGridViewDataErrorContexts context)
    {
        var style = cellStyle ?? InheritedStyle;
        if (value == null || value == DBNull.Value) return style.NullValue?.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(style.Format) && value is IFormattable formattable)
        {
            return formattable.ToString(style.Format, style.FormatProvider ?? CultureInfo.CurrentCulture);
        }
        return Convert.ToString(value, style.FormatProvider ?? CultureInfo.CurrentCulture) ?? string.Empty;
    }

    /// <summary>Turns edited text back into a value of <see cref="ValueType"/>.</summary>
    public virtual object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle,
        TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        var text = formattedValue as string ?? formattedValue?.ToString() ?? string.Empty;
        var target = Nullable.GetUnderlyingType(ValueType) ?? ValueType;
        if (target == typeof(string) || target == typeof(object)) return text;
        if (text.Length == 0) return null;
        try
        {
            return Convert.ChangeType(text, target, cellStyle?.FormatProvider ?? CultureInfo.CurrentCulture);
        }
        catch (Exception)
        {
            return text;
        }
    }

    public DataGridViewCellStyle Style => _style ??= new DataGridViewCellStyle();

    public bool HasStyle => _style != null;

    /// <summary>Grid default → column → row (or alternating row) → cell, each overriding the last.</summary>
    public virtual DataGridViewCellStyle InheritedStyle
    {
        get
        {
            var style = new DataGridViewCellStyle();
            var grid = DataGridView;
            if (grid != null)
            {
                style.ApplyStyle(grid.DefaultCellStyle);
                style.ApplyStyle(grid.RowsDefaultCellStyle);
                if (RowIndex >= 0 && RowIndex % 2 == 1) style.ApplyStyle(grid.AlternatingRowsDefaultCellStyle);
            }
            if (OwningColumn?.HasDefaultCellStyle == true) style.ApplyStyle(OwningColumn.DefaultCellStyle);
            if (OwningRow?.HasDefaultCellStyle == true) style.ApplyStyle(OwningRow.DefaultCellStyle);
            if (_style != null) style.ApplyStyle(_style);
            return style;
        }
    }

    public virtual bool ReadOnly
    {
        get => (State & DataGridViewElementStates.ReadOnly) != 0
            || OwningColumn?.ReadOnly == true || OwningRow?.ReadOnly == true || DataGridView?.ReadOnly == true;
        set => State = value ? State | DataGridViewElementStates.ReadOnly : State & ~DataGridViewElementStates.ReadOnly;
    }

    public virtual bool Selected
    {
        get => DataGridView != null && DataGridView.IsCellSelected(ColumnIndex, RowIndex);
        set => DataGridView?.SetCellSelected(ColumnIndex, RowIndex, value);
    }

    public virtual bool Visible => OwningColumn?.Visible != false && OwningRow?.Visible != false;

    public virtual bool Displayed => Visible;

    public virtual bool Frozen => OwningColumn?.Frozen == true || OwningRow?.Frozen == true;

    public virtual bool Resizable => OwningColumn?.Resizable == DataGridViewTriState.True;

    public virtual Size Size => DataGridView?.GetCellSize(ColumnIndex, RowIndex) ?? Size.Empty;

    public Rectangle ContentBounds => DataGridView?.GetCellDisplayRectangle(ColumnIndex, RowIndex, false) ?? Rectangle.Empty;

    public bool IsInEditMode => DataGridView?.IsCellInEditMode(ColumnIndex, RowIndex) == true;

    /// <summary>Width the cell would like: its text plus the style's padding and the grid's margins.</summary>
    public virtual Size GetPreferredSize(Graphics? graphics, DataGridViewCellStyle cellStyle, int rowIndex, Size constraintSize)
    {
        var style = cellStyle ?? InheritedStyle;
        var text = GetFormattedValue(Value, rowIndex, style, DataGridViewDataErrorContexts.PreferredSize) as string ?? string.Empty;
        var font = style.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        var size = TextRenderer.MeasureText(text, font);
        return new Size(size.Width + style.Padding.Horizontal + 9, size.Height + style.Padding.Vertical + 5);
    }

    /// <summary>Draws the cell; the base class paints background, border and text.</summary>
    protected internal virtual void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object? value, object? formattedValue, string? errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        var grid = DataGridView;
        if (grid == null) return;
        bool selected = (cellState & DataGridViewElementStates.Selected) != 0;

        if ((paintParts & DataGridViewPaintParts.Background) != 0)
        {
            var back = selected ? cellStyle.SelectionBackColor : cellStyle.BackColor;
            if (!back.IsEmpty)
            {
                using var brush = new SolidBrush(back);
                graphics.FillRectangle(brush, cellBounds);
            }
        }

        if ((paintParts & DataGridViewPaintParts.ContentForeground) != 0)
        {
            PaintContentForeground(graphics, cellBounds, rowIndex, selected, formattedValue, cellStyle);
        }

        if ((paintParts & DataGridViewPaintParts.Border) != 0)
        {
            using var pen = new Pen(grid.GridColor);
            graphics.DrawLine(pen, cellBounds.Right - 1, cellBounds.Top, cellBounds.Right - 1, cellBounds.Bottom - 1);
            graphics.DrawLine(pen, cellBounds.Left, cellBounds.Bottom - 1, cellBounds.Right - 1, cellBounds.Bottom - 1);
        }
    }

    private protected virtual void PaintContentForeground(Graphics graphics, Rectangle cellBounds, int rowIndex, bool selected,
        object? formattedValue, DataGridViewCellStyle cellStyle)
    {
        var text = formattedValue as string ?? formattedValue?.ToString();
        if (string.IsNullOrEmpty(text)) return;
        var color = selected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        if (color.IsEmpty) color = DataGridView?.ForeColor ?? Control.DefaultForeColor;
        var font = cellStyle.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        TextRenderer.DrawText(graphics, text, font, ContentRectangle(cellBounds, cellStyle), color,
            DataGridView!.TranslateAlignment(cellStyle) | TextFormatFlags.EndEllipsis
            | (cellStyle.WrapMode == DataGridViewTriState.True ? TextFormatFlags.WordBreak : TextFormatFlags.SingleLine));
    }

    private protected static Rectangle ContentRectangle(Rectangle cellBounds, DataGridViewCellStyle style)
    {
        var p = style.Padding;
        return new Rectangle(cellBounds.X + 2 + p.Left, cellBounds.Y + p.Top,
            Math.Max(0, cellBounds.Width - 5 - p.Horizontal), Math.Max(0, cellBounds.Height - 1 - p.Vertical));
    }

    /// <summary>Called when the cell is clicked; the base does nothing, buttons and check boxes react.</summary>
    protected internal virtual void OnCellClick(DataGridViewCellEventArgs e) { }

    /// <summary>Called for a click inside the cell's content area (a button's face, a check box).</summary>
    protected internal virtual void OnCellContentClick(DataGridViewCellEventArgs e) { }

    public virtual object Clone()
    {
        var copy = (DataGridViewCell)Activator.CreateInstance(GetType())!;
        if (_style != null) copy._style = (DataGridViewCellStyle)_style.Clone();
        copy.ToolTipText = ToolTipText;
        copy.Tag = Tag;
        return copy;
    }

    public override string ToString() => $"DataGridViewCell {{ ColumnIndex={ColumnIndex}, RowIndex={RowIndex} }}";
}

/// <summary>A plain text cell, edited with a text box; the grid's default.</summary>
public class DataGridViewTextBoxCell : DataGridViewCell
{
    internal override Type DefaultValueType => typeof(string);

    public virtual int MaxInputLength { get; set; } = 32767;

    public override Type? EditType => typeof(TextBox);

    public override object Clone()
    {
        var copy = (DataGridViewTextBoxCell)base.Clone();
        copy.MaxInputLength = MaxInputLength;
        return copy;
    }
}

/// <summary>A check box cell. Clicking its box toggles the value without opening an editor.</summary>
public class DataGridViewCheckBoxCell : DataGridViewCell
{
    public DataGridViewCheckBoxCell() { }

    public DataGridViewCheckBoxCell(bool threeState) => ThreeState = threeState;

    public bool ThreeState { get; set; }

    public object? TrueValue { get; set; }
    public object? FalseValue { get; set; }
    public object? IndeterminateValue { get; set; }

    internal override Type DefaultValueType => ThreeState ? typeof(CheckState) : typeof(bool);

    public override Type FormattedValueType => ThreeState ? typeof(CheckState) : typeof(bool);

    public override Type? EditType => null;

    public override object? GetFormattedValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle, DataGridViewDataErrorContexts context) =>
        ToCheckState(value);

    internal CheckState ToCheckState(object? value) => value switch
    {
        null => CheckState.Unchecked,
        bool b => b ? CheckState.Checked : CheckState.Unchecked,
        CheckState s => s,
        _ when Equals(value, TrueValue) => CheckState.Checked,
        _ when Equals(value, FalseValue) => CheckState.Unchecked,
        _ when Equals(value, IndeterminateValue) => CheckState.Indeterminate,
        _ => Convert.ToBoolean(value, CultureInfo.CurrentCulture) ? CheckState.Checked : CheckState.Unchecked,
    };

    /// <summary>The 13x13 box, centred in the cell - the only part a click toggles.</summary>
    internal Rectangle BoxBounds(Rectangle cellBounds) =>
        new Rectangle(cellBounds.X + (cellBounds.Width - 13) / 2, cellBounds.Y + (cellBounds.Height - 13) / 2, 13, 13);

    public override object Clone()
    {
        var copy = (DataGridViewCheckBoxCell)base.Clone();
        copy.ThreeState = ThreeState;
        copy.TrueValue = TrueValue;
        copy.FalseValue = FalseValue;
        copy.IndeterminateValue = IndeterminateValue;
        return copy;
    }

    private protected override void PaintContentForeground(Graphics graphics, Rectangle cellBounds, int rowIndex, bool selected,
        object? formattedValue, DataGridViewCellStyle cellStyle)
    {
        var state = formattedValue is CheckState s ? s : ToCheckState(formattedValue);
        CheckBox.PaintBox(graphics, BoxBounds(cellBounds), state, DataGridView?.Enabled ?? true,
            hot: false, pressed: false, flat: false, cellStyle.ForeColor);
    }

    protected internal override void OnCellContentClick(DataGridViewCellEventArgs e)
    {
        if (ReadOnly) return;
        var next = ToCheckState(Value) switch
        {
            CheckState.Unchecked => ThreeState ? CheckState.Checked : CheckState.Checked,
            CheckState.Checked => ThreeState ? CheckState.Indeterminate : CheckState.Unchecked,
            _ => CheckState.Unchecked,
        };
        Value = ThreeState
            ? next
            : next == CheckState.Checked ? (TrueValue ?? true) : (FalseValue ?? (object)false);
    }
}

/// <summary>A push button inside a cell; clicking it raises CellContentClick on the grid.</summary>
public class DataGridViewButtonCell : DataGridViewCell
{
    internal override Type DefaultValueType => typeof(string);

    public bool UseColumnTextForButtonValue { get; set; }

    public override Type? EditType => null;

    public override object? GetFormattedValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle, DataGridViewDataErrorContexts context)
    {
        if (UseColumnTextForButtonValue && OwningColumn is DataGridViewButtonColumn column) return column.Text;
        return base.GetFormattedValue(value, rowIndex, cellStyle, context);
    }

    public override object Clone()
    {
        var copy = (DataGridViewButtonCell)base.Clone();
        copy.UseColumnTextForButtonValue = UseColumnTextForButtonValue;
        return copy;
    }

    private protected override void PaintContentForeground(Graphics graphics, Rectangle cellBounds, int rowIndex, bool selected,
        object? formattedValue, DataGridViewCellStyle cellStyle)
    {
        var face = Rectangle.Inflate(cellBounds, -3, -3);
        if (face.Width <= 0 || face.Height <= 0) return;
        using (var brush = new SolidBrush(Theme.ButtonFace))
        {
            graphics.FillRectangle(brush, face);
        }
        using (var pen = new Pen(Theme.ButtonBorder))
        {
            graphics.DrawRectangle(pen, face.X, face.Y, face.Width - 1, face.Height - 1);
        }
        var text = formattedValue as string ?? formattedValue?.ToString();
        if (string.IsNullOrEmpty(text)) return;
        TextRenderer.DrawText(graphics, text, cellStyle.Font ?? DataGridView?.Font ?? Control.DefaultFont, face,
            DataGridView?.ForeColor ?? Control.DefaultForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

/// <summary>A cell that picks from a list; the drop-down is a real ComboBox hosted over the cell.</summary>
public class DataGridViewComboBoxCell : DataGridViewCell
{
    private ObjectCollection? _items;

    public ObjectCollection Items => _items ??= new ObjectCollection(this);

    public object? DataSource { get; set; }
    public string DisplayMember { get; set; } = string.Empty;
    public string ValueMember { get; set; } = string.Empty;
    public int MaxDropDownItems { get; set; } = 8;
    public bool DisplayStyleForCurrentCellOnly { get; set; }

    public override Type? EditType => typeof(ComboBox);

    /// <summary>The list the editor shows: the cell's own items, or the column's when it has none.</summary>
    internal IList EditItems => _items is { Count: > 0 } ? _items.Inner
        : OwningColumn is DataGridViewComboBoxColumn column ? column.Items.Inner
        : Array.Empty<object>();

    public override object Clone()
    {
        var copy = (DataGridViewComboBoxCell)base.Clone();
        copy.DataSource = DataSource;
        copy.DisplayMember = DisplayMember;
        copy.ValueMember = ValueMember;
        copy.MaxDropDownItems = MaxDropDownItems;
        copy.DisplayStyleForCurrentCellOnly = DisplayStyleForCurrentCellOnly;
        return copy;
    }

    private protected override void PaintContentForeground(Graphics graphics, Rectangle cellBounds, int rowIndex, bool selected,
        object? formattedValue, DataGridViewCellStyle cellStyle)
    {
        base.PaintContentForeground(graphics, cellBounds, rowIndex, selected, formattedValue, cellStyle);
        // The drop-down arrow, drawn on the right of the cell as WinForms does for the current cell.
        var arrow = new Rectangle(cellBounds.Right - 17, cellBounds.Y, 16, cellBounds.Height);
        var color = selected ? cellStyle.SelectionForeColor : cellStyle.ForeColor;
        if (color.IsEmpty) color = DataGridView?.ForeColor ?? Control.DefaultForeColor;
        var middle = new Point(arrow.Left + arrow.Width / 2, arrow.Top + arrow.Height / 2);
        using var brush = new SolidBrush(color);
        graphics.FillPolygon(brush, new[]
        {
            new Point(middle.X - 3, middle.Y - 2), new Point(middle.X + 3, middle.Y - 2), new Point(middle.X, middle.Y + 2),
        });
    }

    public class ObjectCollection : IList
    {
        private readonly List<object> _items = new();

        internal ObjectCollection(DataGridViewComboBoxCell owner) { }

        internal IList Inner => _items;

        public int Count => _items.Count;
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;

        public object? this[int index] { get => _items[index]; set => _items[index] = value!; }

        public int Add(object? item)
        {
            ArgumentNullException.ThrowIfNull(item);
            _items.Add(item);
            return _items.Count - 1;
        }

        public void AddRange(params object[] items)
        {
            ArgumentNullException.ThrowIfNull(items);
            _items.AddRange(items);
        }

        public void Clear() => _items.Clear();
        public bool Contains(object? item) => item != null && _items.Contains(item);
        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
        public IEnumerator GetEnumerator() => _items.GetEnumerator();
        public int IndexOf(object? item) => item == null ? -1 : _items.IndexOf(item);
        public void Insert(int index, object? item) => _items.Insert(index, item!);
        public void Remove(object? item) { if (item != null) _items.Remove(item); }
        public void RemoveAt(int index) => _items.RemoveAt(index);
    }
}

/// <summary>A cell that shows an image scaled into its bounds.</summary>
public class DataGridViewImageCell : DataGridViewCell
{
    public DataGridViewImageCell() { }

    public DataGridViewImageCell(bool valueIsIcon) => ValueIsIcon = valueIsIcon;

    public bool ValueIsIcon { get; set; }

    public string Description { get; set; } = string.Empty;

    internal override Type DefaultValueType => typeof(Image);

    public override Type FormattedValueType => typeof(Image);

    public override Type? EditType => null;

    public override object? GetFormattedValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle, DataGridViewDataErrorContexts context) => value;

    public override object Clone()
    {
        var copy = (DataGridViewImageCell)base.Clone();
        copy.ValueIsIcon = ValueIsIcon;
        copy.Description = Description;
        return copy;
    }

    private protected override void PaintContentForeground(Graphics graphics, Rectangle cellBounds, int rowIndex, bool selected,
        object? formattedValue, DataGridViewCellStyle cellStyle)
    {
        if (formattedValue is not Image image) return;
        var area = Rectangle.Inflate(cellBounds, -2, -2);
        if (area.Width <= 0 || area.Height <= 0) return;
        int width = Math.Min(image.Width, area.Width);
        int height = Math.Min(image.Height, area.Height);
        graphics.DrawImage(image, new Rectangle(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
    }
}

/// <summary>A cell whose text is a link; clicking it raises CellContentClick.</summary>
public class DataGridViewLinkCell : DataGridViewCell
{
    internal override Type DefaultValueType => typeof(string);

    public Color LinkColor { get; set; } = Theme.LinkText;
    public Color ActiveLinkColor { get; set; } = Theme.LinkActive;
    public Color VisitedLinkColor { get; set; } = Theme.LinkVisited;
    public bool LinkVisited { get; set; }
    public bool TrackVisitedState { get; set; } = true;
    public LinkBehavior LinkBehavior { get; set; } = LinkBehavior.SystemDefault;
    public bool UseColumnTextForLinkValue { get; set; }

    public override Type? EditType => null;

    public override object? GetFormattedValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle, DataGridViewDataErrorContexts context)
    {
        if (UseColumnTextForLinkValue && OwningColumn is DataGridViewLinkColumn column) return column.Text;
        return base.GetFormattedValue(value, rowIndex, cellStyle, context);
    }

    public override object Clone()
    {
        var copy = (DataGridViewLinkCell)base.Clone();
        copy.LinkColor = LinkColor;
        copy.ActiveLinkColor = ActiveLinkColor;
        copy.VisitedLinkColor = VisitedLinkColor;
        copy.TrackVisitedState = TrackVisitedState;
        copy.LinkBehavior = LinkBehavior;
        copy.UseColumnTextForLinkValue = UseColumnTextForLinkValue;
        return copy;
    }

    private protected override void PaintContentForeground(Graphics graphics, Rectangle cellBounds, int rowIndex, bool selected,
        object? formattedValue, DataGridViewCellStyle cellStyle)
    {
        var text = formattedValue as string ?? formattedValue?.ToString();
        if (string.IsNullOrEmpty(text)) return;
        var baseFont = cellStyle.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        using var font = new Font(baseFont, baseFont.Style | FontStyle.Underline);
        TextRenderer.DrawText(graphics, text, font, ContentRectangle(cellBounds, cellStyle),
            LinkVisited ? VisitedLinkColor : LinkColor,
            DataGridView!.TranslateAlignment(cellStyle) | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }

    protected internal override void OnCellContentClick(DataGridViewCellEventArgs e)
    {
        if (TrackVisitedState) LinkVisited = true;
    }
}

/// <summary>The cell of a column header; it draws the header text and the sort glyph.</summary>
public class DataGridViewColumnHeaderCell : DataGridViewCell
{
    public SortOrder SortGlyphDirection { get; set; } = SortOrder.None;

    public override Type? EditType => null;

    public override object? Value
    {
        get => OwningColumn?.HeaderText ?? string.Empty;
        set { if (OwningColumn != null) OwningColumn.HeaderText = value?.ToString() ?? string.Empty; }
    }
}

/// <summary>The cell of a row header; it draws the current-row arrow.</summary>
public class DataGridViewRowHeaderCell : DataGridViewCell
{
    public override Type? EditType => null;

    public override object? Value
    {
        get => OwningRow?.HeaderCellValue;
        set { if (OwningRow != null) OwningRow.HeaderCellValue = value; }
    }
}

/// <summary>The top-left corner cell of the grid.</summary>
public class DataGridViewTopLeftHeaderCell : DataGridViewColumnHeaderCell
{
}
