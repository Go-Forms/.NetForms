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

    /// <summary>The control type used to edit this cell, or null when it is not edited through a control.</summary>
    public virtual Type? EditType => typeof(DataGridViewTextBoxEditingControl);

    /// <summary>The value the cell of the new row starts with.</summary>
    public virtual object? DefaultNewRowValue => null;

    internal TypeConverter? ValueTypeConverter => ValueType is { } type ? TypeDescriptor.GetConverter(type) : null;

    internal TypeConverter? FormattedValueTypeConverter => FormattedValueType is { } type ? TypeDescriptor.GetConverter(type) : null;

    public virtual object? Value
    {
        get => DataGridView != null && DataGridView.IsBound && RowIndex >= 0
            ? DataGridView.GetBoundValue(RowIndex, ColumnIndex)
            : _value;
        set => SetValue(RowIndex, value);
    }

    /// <summary>
    /// Stores a value: into the bound item, through CellValuePushed in VirtualMode, or into the cell. Then
    /// CellValueChanged.
    /// </summary>
    protected virtual bool SetValue(int rowIndex, object? value)
    {
        var grid = DataGridView;
        if (grid != null && grid.IsBound && rowIndex >= 0)
        {
            if (!grid.SetBoundValue(rowIndex, ColumnIndex, value)) return false;
        }
        else if (grid != null && grid.VirtualMode && rowIndex >= 0 && ColumnIndex >= 0)
        {
            grid.RaiseCellValuePushed(ColumnIndex, rowIndex, value);
        }
        else
        {
            if (Equals(_value, value)) return true;
            _value = value;
        }
        grid?.NotifyCellValueChanged(ColumnIndex, rowIndex);
        return true;
    }

    internal bool SetValueInternal(int rowIndex, object? value) => SetValue(rowIndex, value);

    /// <summary>The value as the cell shows it: <see cref="Value"/> through CellFormatting and the style's format.</summary>
    [Browsable(false)]
    public object? FormattedValue
    {
        get
        {
            if (DataGridView == null) return null;
            var style = InheritedStyle;
            return DataGridView.FormattedValueOf(this, RowIndex, ref style, DataGridViewDataErrorContexts.Formatting);
        }
    }

    /// <summary>
    /// The formatted value of <paramref name="value"/>, as in WinForms: CellFormatting first (it may change the style
    /// or format the value itself); unless it did, the WinForms Formatter (Format, FormatProvider, NullValue,
    /// the type converters). A formatting failure raises DataError.
    /// </summary>
    protected virtual object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        var grid = DataGridView;
        if (grid == null) return null;
        var formatting = grid.RaiseCellFormatting(ColumnIndex, rowIndex, value, FormattedValueType, cellStyle);
        cellStyle = formatting.CellStyle ?? cellStyle;
        object? formatted = formatting.Value;
        if (formatting.FormattingApplied) return formatted;
        if (FormattedValueType is { } formattedType && (formatted == null || !formattedType.IsInstanceOfType(formatted)))
        {
            try
            {
                formatted = FormatValue(formatted, rowIndex, cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
            }
            catch (Exception ex) when (!ex.IsCriticalException())
            {
                var error = new DataGridViewDataErrorEventArgs(ex, ColumnIndex, rowIndex, context);
                grid.RaiseDataError(error);
                if (error.ThrowException) throw error.Exception!;
            }
        }
        return formatted;
    }

    /// <summary>What <see cref="GetFormattedValue(object?, int, ref DataGridViewCellStyle, TypeConverter?, TypeConverter?, DataGridViewDataErrorContexts)"/> does after CellFormatting.</summary>
    private protected virtual object? FormatValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        Formatter.FormatObject(value, FormattedValueType!, valueTypeConverter ?? ValueTypeConverter, formattedValueTypeConverter ?? FormattedValueTypeConverter,
            cellStyle.Format, cellStyle.FormatProvider, cellStyle.NullValue, cellStyle.DataSourceNullValue);

    internal object? GetFormattedValueInternal(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle, DataGridViewDataErrorContexts context) =>
        GetFormattedValue(value, rowIndex, ref cellStyle, null, null, context);

    /// <summary>The value being edited when this is the cell in edit mode, otherwise the formatted value.</summary>
    [Browsable(false)]
    public object? EditedFormattedValue => DataGridView == null ? null : GetEditedFormattedValue(RowIndex, DataGridViewDataErrorContexts.Formatting);

    public object? GetEditedFormattedValue(int rowIndex, DataGridViewDataErrorContexts context)
    {
        var grid = DataGridView;
        if (grid == null) return null;
        var current = grid.CurrentCellAddress;
        if (current.X == ColumnIndex && current.Y == rowIndex)
        {
            if (grid.EditingControl is IDataGridViewEditingControl editing) return editing.GetEditingControlFormattedValue(context);
            if (this is IDataGridViewEditingCell editingCell && grid.IsCurrentCellInEditMode) return editingCell.GetEditingCellFormattedValue(context);
        }
        var style = InheritedStyle;
        return grid.FormattedValueOf(this, rowIndex, ref style, context);
    }

    /// <summary>
    /// Turns an edited formatted value back into a value of <see cref="ValueType"/> through the WinForms Formatter;
    /// text that does not parse throws (the grid reports it as a DataError).
    /// </summary>
    public virtual object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle,
        TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        ArgumentNullException.ThrowIfNull(cellStyle);
        var formattedType = FormattedValueType ?? throw new FormatException("Formatted value type property cannot be null.");
        var valueType = ValueType ?? throw new FormatException("Value type property cannot be null.");
        if (formattedValue == null || !formattedType.IsInstanceOfType(formattedValue))
        {
            throw new ArgumentException("Formatted value has the wrong type.", nameof(formattedValue));
        }
        return Formatter.ParseObject(formattedValue, valueType, formattedType,
            valueTypeConverter ?? ValueTypeConverter, formattedValueTypeConverter ?? FormattedValueTypeConverter,
            cellStyle.FormatProvider, cellStyle.NullValue,
            cellStyle.DataSourceNullValue == DBNull.Value ? Formatter.GetDefaultDataSourceNullValue(valueType) : cellStyle.DataSourceNullValue);
    }

    // --- editing: what WinForms' editing model asks of a cell -------------------------------------------------

    /// <summary>Hosts the grid's editing control over this cell and gives it the value to edit (subclasses fill it in).</summary>
    public virtual void InitializeEditingControl(int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        var grid = DataGridView;
        if (grid?.EditingControl == null) throw new InvalidOperationException();
        if (grid.EditingControl.Parent == null)
        {
            grid.EditingControl.CausesValidation = grid.CausesValidation;
            grid.EditingPanel.CausesValidation = grid.CausesValidation;
            grid.EditingControl.Visible = true;
            grid.EditingPanel.Visible = false;
            if (grid.EditingPanel.Parent == null) grid.Controls.Add(grid.EditingPanel);
            grid.EditingPanel.Controls.Add(grid.EditingControl);
        }
    }

    /// <summary>Takes the editing control off the cell when the edit ends.</summary>
    public virtual void DetachEditingControl()
    {
        var grid = DataGridView;
        if (grid?.EditingControl == null) throw new InvalidOperationException();
        if (grid.EditingControl.Parent != null)
        {
            if (grid.EditingControl.ContainsFocus && grid.CanFocus) grid.Focus();
            grid.EditingPanel.Controls.Remove(grid.EditingControl);
        }
        if (grid.EditingPanel.Parent != null)
        {
            grid.EditingPanel.Visible = false;
            grid.Controls.Remove(grid.EditingPanel);
        }
    }

    /// <summary>Whether this key, pressed on the current cell, starts editing it (EditOnKeystroke).</summary>
    public virtual bool KeyEntersEditMode(KeyEventArgs e) => false;

    /// <summary>Places the editing panel over the cell and the editing control inside it.</summary>
    public virtual void PositionEditingControl(bool setLocation, bool setSize, Rectangle cellBounds, Rectangle cellClip,
        DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
        bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
    {
        var bounds = PositionEditingPanel(cellBounds, cellClip, cellStyle, singleVerticalBorderAdded, singleHorizontalBorderAdded,
            isFirstDisplayedColumn, isFirstDisplayedRow);
        if (DataGridView?.EditingControl is { } control)
        {
            if (setLocation) control.Location = bounds.Location;
            if (setSize) control.Size = bounds.Size;
        }
    }

    /// <summary>
    /// Positions the editing panel on the visible part of the cell, inside its grid lines and padding, and returns
    /// the editing control's bounds within the panel.
    /// </summary>
    public virtual Rectangle PositionEditingPanel(Rectangle cellBounds, Rectangle cellClip, DataGridViewCellStyle cellStyle,
        bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded, bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
    {
        var grid = DataGridView ?? throw new InvalidOperationException();
        var padding = cellStyle.Padding;
        // The grid lines are the cell's last column and row of pixels.
        var inner = new Rectangle(cellBounds.X + padding.Left, cellBounds.Y + padding.Top,
            Math.Max(0, cellBounds.Width - 1 - padding.Horizontal), Math.Max(0, cellBounds.Height - 1 - padding.Vertical));
        var panel = Rectangle.Intersect(inner, cellClip);
        grid.EditingPanel.Bounds = panel;
        return new Rectangle(inner.X - panel.X, inner.Y - panel.Y, inner.Width, inner.Height);
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
        var text = (DataGridView != null ? DataGridView.FormattedValueOf(this, rowIndex, ref style, DataGridViewDataErrorContexts.PreferredSize) : Value?.ToString()) as string ?? string.Empty;
        var font = style.Font ?? DataGridView?.Font ?? Control.DefaultFont;
        var size = TextRenderer.MeasureText(text, font);
        return new Size(size.Width + style.Padding.Horizontal + 9, size.Height + style.Padding.Vertical + 5);
    }

    /// <summary>How the grid calls <see cref="Paint"/>, which is protected in WinForms (and so overridable as protected from any assembly).</summary>
    internal void PaintCell(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
        DataGridViewElementStates cellState, object? value, object? formattedValue, string? errorText,
        DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts) =>
        Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts);

    /// <summary>Draws the cell; the base class paints background, border and text.</summary>
    protected virtual void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
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

    /// <summary>Called when the cell is clicked; the base does nothing.</summary>
    protected virtual void OnClick(DataGridViewCellEventArgs e) { }

    /// <summary>Called for a click inside the cell's content area (a button's face, a check box).</summary>
    protected virtual void OnContentClick(DataGridViewCellEventArgs e) { }

    internal void OnClickInternal(DataGridViewCellEventArgs e) => OnClick(e);

    internal void OnContentClickInternal(DataGridViewCellEventArgs e) => OnContentClick(e);

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

    private const int HorizontalTextOffsetLeft = 3;
    private const int HorizontalTextOffsetRight = 4;
    private const int VerticalTextOffsetTop = 2;
    private const int VerticalTextOffsetBottom = 1;

    public virtual int MaxInputLength { get; set; } = 32767;

    public override Type? EditType => typeof(DataGridViewTextBoxEditingControl);

    public override void InitializeEditingControl(int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
        if (DataGridView!.EditingControl is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.None;
            textBox.AcceptsReturn = textBox.Multiline = dataGridViewCellStyle.WrapMode == DataGridViewTriState.True;
            textBox.MaxLength = MaxInputLength;
            textBox.Text = initialFormattedValue as string ?? string.Empty;
        }
    }

    public override void DetachEditingControl()
    {
        if (DataGridView?.EditingControl is TextBox textBox) textBox.ClearUndo();
        base.DetachEditingControl();
    }

    /// <summary>Letters, digits, punctuation and Space (without Ctrl or Alt) start editing the text.</summary>
    public override bool KeyEntersEditMode(KeyEventArgs e)
    {
        if (((char.IsLetterOrDigit((char)e.KeyCode) && !(e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F24))
                || (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.Divide)
                || (e.KeyCode >= Keys.OemSemicolon && e.KeyCode <= Keys.Oem102)
                || (e.KeyCode == Keys.Space && !e.Shift))
            && !e.Alt && !e.Control)
        {
            return true;
        }
        return base.KeyEntersEditMode(e);
    }

    /// <summary>The text box sits where the cell draws its text (WinForms' offsets), vertically as the alignment says.</summary>
    public override void PositionEditingControl(bool setLocation, bool setSize, Rectangle cellBounds, Rectangle cellClip,
        DataGridViewCellStyle cellStyle, bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
        bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
    {
        var bounds = PositionEditingPanel(cellBounds, cellClip, cellStyle, singleVerticalBorderAdded, singleHorizontalBorderAdded,
            isFirstDisplayedColumn, isFirstDisplayedRow);
        var control = DataGridView!.EditingControl!;
        if (control is TextBox textBox)
        {
            const DataGridViewContentAlignment anyLeft = DataGridViewContentAlignment.TopLeft | DataGridViewContentAlignment.MiddleLeft | DataGridViewContentAlignment.BottomLeft;
            const DataGridViewContentAlignment anyCenter = DataGridViewContentAlignment.TopCenter | DataGridViewContentAlignment.MiddleCenter | DataGridViewContentAlignment.BottomCenter;
            const DataGridViewContentAlignment anyTop = DataGridViewContentAlignment.TopLeft | DataGridViewContentAlignment.TopCenter | DataGridViewContentAlignment.TopRight;
            const DataGridViewContentAlignment anyMiddle = DataGridViewContentAlignment.MiddleLeft | DataGridViewContentAlignment.MiddleCenter | DataGridViewContentAlignment.MiddleRight;
            var align = cellStyle.Alignment;
            if ((align & anyLeft) != 0)
            {
                bounds.X += HorizontalTextOffsetLeft;
                bounds.Width = Math.Max(0, bounds.Width - HorizontalTextOffsetLeft - 1);
            }
            else if ((align & anyCenter) != 0)
            {
                bounds.X += 1;
                bounds.Width = Math.Max(0, bounds.Width - 3);
            }
            else
            {
                bounds.X += 1;
                bounds.Width = Math.Max(0, bounds.Width - HorizontalTextOffsetRight - 1);
            }
            if ((align & anyTop) != 0)
            {
                bounds.Y += VerticalTextOffsetTop;
                bounds.Height = Math.Max(0, bounds.Height - VerticalTextOffsetTop);
            }
            else if ((align & anyMiddle) != 0)
            {
                bounds.Height++;
            }
            else
            {
                bounds.Height = Math.Max(0, bounds.Height - VerticalTextOffsetBottom);
            }
            int preferred = cellStyle.WrapMode == DataGridViewTriState.True ? bounds.Height : textBox.PreferredSize.Height;
            if (preferred < bounds.Height)
            {
                if ((align & anyMiddle) != 0) bounds.Y += (bounds.Height - preferred) / 2;
                else if ((align & anyTop) == 0) bounds.Y += bounds.Height - preferred;
                bounds.Height = preferred;
            }
        }
        if (setLocation) control.Location = bounds.Location;
        if (setSize) control.Size = bounds.Size;
    }

    public override object Clone()
    {
        var copy = (DataGridViewTextBoxCell)base.Clone();
        copy.MaxInputLength = MaxInputLength;
        return copy;
    }
}

/// <summary>
/// A check box cell. It edits itself (IDataGridViewEditingCell): a click on its box, or Space, toggles the edited
/// value and makes the cell dirty; the value is committed when the cell is left (or on EndEdit/CommitEdit), as in
/// WinForms - handle CurrentCellDirtyStateChanged and call CommitEdit to take it at once.
/// </summary>
public class DataGridViewCheckBoxCell : DataGridViewCell, IDataGridViewEditingCell
{
    private CheckState _editingState;
    private bool _editingValueChanged;

    public DataGridViewCheckBoxCell() { }

    public DataGridViewCheckBoxCell(bool threeState) => ThreeState = threeState;

    public bool ThreeState { get; set; }

    public object? TrueValue { get; set; }
    public object? FalseValue { get; set; }
    public object? IndeterminateValue { get; set; }

    internal override Type DefaultValueType => ThreeState ? typeof(CheckState) : typeof(bool);

    public override Type FormattedValueType => ThreeState ? typeof(CheckState) : typeof(bool);

    public override Type? EditType => null;

    public virtual object? EditingCellFormattedValue
    {
        get => GetEditingCellFormattedValue(DataGridViewDataErrorContexts.Formatting);
        set => _editingState = value switch
        {
            CheckState state => state,
            bool b => b ? CheckState.Checked : CheckState.Unchecked,
            _ => throw new ArgumentException("The value provided for the DataGridViewCheckBoxCell has the wrong type."),
        };
    }

    public virtual bool EditingCellValueChanged
    {
        get => _editingValueChanged;
        set => _editingValueChanged = value;
    }

    public virtual object? GetEditingCellFormattedValue(DataGridViewDataErrorContexts context) =>
        FormattedValueType == typeof(CheckState) ? _editingState : _editingState == CheckState.Checked;

    public virtual void PrepareEditingCellForEdit(bool selectAll)
    {
    }

    /// <summary>Two-state: a bool; three-state: a CheckState (TrueValue, FalseValue, IndeterminateValue map to them).</summary>
    private protected override object? FormatValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        var state = ToCheckState(value);
        return FormattedValueType == typeof(CheckState) ? state : state == CheckState.Checked;
    }

    /// <summary>The edited check state back to a value: TrueValue/FalseValue/IndeterminateValue when set.</summary>
    public override object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle,
        TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        var state = formattedValue switch
        {
            bool b => b ? CheckState.Checked : CheckState.Unchecked,
            CheckState s => s,
            _ => (CheckState?)null,
        };
        if (state is { } checkState)
        {
            var valueType = ValueType;
            switch (checkState)
            {
                case CheckState.Checked:
                    if (TrueValue != null) return TrueValue;
                    if (valueType != null && valueType.IsAssignableFrom(typeof(bool))) return true;
                    if (valueType != null && valueType.IsAssignableFrom(typeof(CheckState))) return CheckState.Checked;
                    break;
                case CheckState.Unchecked:
                    if (FalseValue != null) return FalseValue;
                    if (valueType != null && valueType.IsAssignableFrom(typeof(bool))) return false;
                    if (valueType != null && valueType.IsAssignableFrom(typeof(CheckState))) return CheckState.Unchecked;
                    break;
                case CheckState.Indeterminate:
                    if (IndeterminateValue != null) return IndeterminateValue;
                    if (valueType != null && valueType.IsAssignableFrom(typeof(CheckState))) return CheckState.Indeterminate;
                    break;
            }
        }
        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

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

    /// <summary>A click on the box of the cell being edited toggles the edited value (WinForms' OnCommonContentClick).</summary>
    protected override void OnContentClick(DataGridViewCellEventArgs e)
    {
        var grid = DataGridView;
        if (grid == null) return;
        var current = grid.CurrentCellAddress;
        if (current.X != ColumnIndex || current.Y != e.RowIndex || !grid.IsCurrentCellInEditMode) return;
        _editingState = _editingState switch
        {
            CheckState.Unchecked => CheckState.Checked,
            CheckState.Checked => ThreeState ? CheckState.Indeterminate : CheckState.Unchecked,
            _ => CheckState.Unchecked,
        };
        _editingValueChanged = true;
        grid.NotifyCurrentCellDirty(true);
        grid.Invalidate();
    }
}

/// <summary>A push button inside a cell; clicking it raises CellContentClick on the grid.</summary>
public class DataGridViewButtonCell : DataGridViewCell
{
    internal override Type DefaultValueType => typeof(string);

    public bool UseColumnTextForButtonValue { get; set; }

    public override Type? EditType => null;

    protected override object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (UseColumnTextForButtonValue && OwningColumn is DataGridViewButtonColumn column) value = column.Text;
        return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
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

    public override Type? EditType => typeof(DataGridViewComboBoxEditingControl);

    /// <summary>The list the editor shows: the cell's own items, or the column's when it has none.</summary>
    internal IList EditItems => _items is { Count: > 0 } ? _items.Inner
        : OwningColumn is DataGridViewComboBoxColumn column ? column.Items.Inner
        : Array.Empty<object>();

    /// <summary>The items to pick from: the data source's list, or the items.</summary>
    private IList PickList
    {
        get
        {
            if (DataSource != null)
            {
                var list = DataGridView?.BindingContext is { } context
                    ? (context[DataSource] as CurrencyManager)?.List
                    : ListBindingHelper.GetList(DataSource) as IList;
                if (list != null) return list;
            }
            return EditItems;
        }
    }

    private object? ItemMember(object? item, string member)
    {
        if (item == null || string.IsNullOrEmpty(member)) return item;
        var property = TypeDescriptor.GetProperties(item).Find(new BindingMemberInfo(member).BindingField, true);
        return property == null ? item : property.GetValue(item);
    }

    private string ItemText(object? item) => Convert.ToString(ItemMember(item, DisplayMember), CultureInfo.CurrentCulture) ?? string.Empty;

    /// <summary>
    /// The text of the item whose value (ValueMember) is <paramref name="value"/>, as WinForms shows it. A value that
    /// is in no item is an error (WinForms' "DataGridViewComboBoxCell value is not valid.", through DataError).
    /// </summary>
    private protected override object? FormatValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (value == null || value == DBNull.Value)
        {
            return base.FormatValue(value, rowIndex, cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
        }
        var list = PickList;
        if (list.Count == 0 && DataSource == null && string.IsNullOrEmpty(ValueMember))
        {
            return base.FormatValue(value, rowIndex, cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
        }
        foreach (var item in list)
        {
            if (Equals(ItemMember(item, ValueMember), value)) return ItemText(item);
        }
        throw new ArgumentException("DataGridViewComboBoxCell value is not valid.");
    }

    /// <summary>The picked text back to the value: the ValueMember of the item with that text.</summary>
    public override object? ParseFormattedValue(object? formattedValue, DataGridViewCellStyle cellStyle,
        TypeConverter? formattedValueTypeConverter, TypeConverter? valueTypeConverter)
    {
        ArgumentNullException.ThrowIfNull(cellStyle);
        if (formattedValue is string text)
        {
            if (text.Length == 0 || Equals(text, cellStyle.NullValue))
            {
                return cellStyle.DataSourceNullValue == DBNull.Value ? Formatter.GetDefaultDataSourceNullValue(ValueType) : cellStyle.DataSourceNullValue;
            }
            foreach (var item in PickList)
            {
                if (string.Equals(ItemText(item), text, StringComparison.CurrentCultureIgnoreCase)) return ItemMember(item, ValueMember);
            }
        }
        return base.ParseFormattedValue(formattedValue, cellStyle, formattedValueTypeConverter, valueTypeConverter);
    }

    /// <summary>The combo box gets the items (or the data source), and the item with the cell's text is selected.</summary>
    public override void InitializeEditingControl(int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
        if (DataGridView!.EditingControl is not ComboBox comboBox) return;
        comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBox.FormattingEnabled = true;
        comboBox.MaxDropDownItems = MaxDropDownItems;
        comboBox.DataSource = null;
        comboBox.ValueMember = string.Empty;
        comboBox.DisplayMember = string.Empty;
        comboBox.Items.Clear();
        if (DataSource != null)
        {
            comboBox.DataSource = DataSource;
            comboBox.DisplayMember = DisplayMember;
            comboBox.ValueMember = ValueMember;
        }
        else
        {
            comboBox.DisplayMember = DisplayMember;
            comboBox.ValueMember = ValueMember;
            foreach (var item in EditItems) if (item != null) comboBox.Items.Add(item);
        }
        comboBox.SelectedIndex = initialFormattedValue is string text ? comboBox.FindStringExact(text) : -1;
    }

    public override void DetachEditingControl()
    {
        if (DataGridView?.EditingControl is ComboBox comboBox)
        {
            comboBox.DataSource = null;
            comboBox.Items.Clear();
        }
        base.DetachEditingControl();
    }

    /// <summary>F4 and Alt+Down open the list (WinForms); typing selects by the first letters.</summary>
    public override bool KeyEntersEditMode(KeyEventArgs e) =>
        ((char.IsLetterOrDigit((char)e.KeyCode) && !(e.KeyCode >= Keys.F1 && e.KeyCode <= Keys.F24))
            || (e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.Divide)
            || (e.KeyCode >= Keys.OemSemicolon && e.KeyCode <= Keys.Oem102)
            || (e.KeyCode == Keys.Space && !e.Shift)
            || e.KeyCode == Keys.F4
            || ((e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) && e.Alt))
        && (!e.Alt || e.KeyCode is Keys.Down or Keys.Up) && !e.Control;

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

    private protected override object? FormatValue(object? value, int rowIndex, DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context) =>
        value switch
        {
            Image image => image,
            Icon icon => icon.ToBitmap(),
            byte[] bytes => ImageFromBytes(bytes),
            _ => null,
        };

    private static Image? ImageFromBytes(byte[] bytes)
    {
        try
        {
            using var stream = new System.IO.MemoryStream(bytes);
            return Image.FromStream(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

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

    protected override object? GetFormattedValue(object? value, int rowIndex, ref DataGridViewCellStyle cellStyle,
        TypeConverter? valueTypeConverter, TypeConverter? formattedValueTypeConverter, DataGridViewDataErrorContexts context)
    {
        if (UseColumnTextForLinkValue && OwningColumn is DataGridViewLinkColumn column) value = column.Text;
        return base.GetFormattedValue(value, rowIndex, ref cellStyle, valueTypeConverter, formattedValueTypeConverter, context);
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

    protected override void OnContentClick(DataGridViewCellEventArgs e)
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
