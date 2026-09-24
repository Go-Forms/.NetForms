// The editing model of the DataGridView: the interfaces an editing control and an editing cell implement, the
// events around an edit and the two stock editing controls. The interfaces and event arguments are copied from
// dotnet/winforms (MIT, src/System.Windows.Forms/System/Windows/Forms/Controls/DataGridView, THIRD-PARTY-NOTICES.md);
// the editing controls are ported from the same place, without their Win32 message handling.

using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>A control that edits a cell in place: the grid shows it over the cell and reads its value back.</summary>
public interface IDataGridViewEditingControl
{
    DataGridView? EditingControlDataGridView { get; set; }

    [AllowNull]
    object EditingControlFormattedValue { get; set; }

    int EditingControlRowIndex { get; set; }

    bool EditingControlValueChanged { get; set; }

    Cursor EditingPanelCursor { get; }

    bool RepositionEditingControlOnValueChange { get; }

    void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle);

    bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey);

    object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context);

    void PrepareEditingControlForEdit(bool selectAll);
}

/// <summary>A cell that edits itself, without an editing control (the check box cell).</summary>
public interface IDataGridViewEditingCell
{
    object? EditingCellFormattedValue { get; set; }

    bool EditingCellValueChanged { get; set; }

    object? GetEditingCellFormattedValue(DataGridViewDataErrorContexts context);

    void PrepareEditingCellForEdit(bool selectAll);
}

public delegate void DataGridViewEditingControlShowingEventHandler(object? sender, DataGridViewEditingControlShowingEventArgs e);

public class DataGridViewEditingControlShowingEventArgs : EventArgs
{
    private DataGridViewCellStyle _cellStyle;

    public DataGridViewEditingControlShowingEventArgs(Control control, DataGridViewCellStyle cellStyle)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(cellStyle);
        Control = control;
        _cellStyle = cellStyle;
    }

    public Control Control { get; }

    public DataGridViewCellStyle CellStyle
    {
        get => _cellStyle;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _cellStyle = value;
        }
    }
}

public delegate void DataGridViewCellValidatingEventHandler(object? sender, DataGridViewCellValidatingEventArgs e);

public class DataGridViewCellValidatingEventArgs : CancelEventArgs
{
    internal DataGridViewCellValidatingEventArgs(int columnIndex, int rowIndex, object? formattedValue)
    {
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
        FormattedValue = formattedValue;
    }

    public int ColumnIndex { get; }

    public int RowIndex { get; }

    public object? FormattedValue { get; }
}

public delegate void DataGridViewCellParsingEventHandler(object? sender, DataGridViewCellParsingEventArgs e);

public class DataGridViewCellParsingEventArgs : ConvertEventArgs
{
    public DataGridViewCellParsingEventArgs(int rowIndex, int columnIndex, object? value, Type? desiredType, DataGridViewCellStyle? inheritedCellStyle)
        : base(value, desiredType)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        InheritedCellStyle = inheritedCellStyle;
    }

    public int RowIndex { get; }

    public int ColumnIndex { get; }

    public DataGridViewCellStyle? InheritedCellStyle { get; set; }

    public bool ParsingApplied { get; set; }
}

/// <summary>The text box that edits a text cell.</summary>
public class DataGridViewTextBoxEditingControl : TextBox, IDataGridViewEditingControl
{
    private const DataGridViewContentAlignment AnyTop = DataGridViewContentAlignment.TopLeft | DataGridViewContentAlignment.TopCenter | DataGridViewContentAlignment.TopRight;
    private const DataGridViewContentAlignment AnyRight = DataGridViewContentAlignment.TopRight | DataGridViewContentAlignment.MiddleRight | DataGridViewContentAlignment.BottomRight;
    private const DataGridViewContentAlignment AnyCenter = DataGridViewContentAlignment.TopCenter | DataGridViewContentAlignment.MiddleCenter | DataGridViewContentAlignment.BottomCenter;

    private DataGridView? _dataGridView;
    private bool _valueChanged;
    private bool _repositionOnValueChange;
    private int _rowIndex;

    public DataGridViewTextBoxEditingControl()
    {
        TabStop = false;
    }

    public virtual DataGridView? EditingControlDataGridView
    {
        get => _dataGridView;
        set => _dataGridView = value;
    }

    [AllowNull]
    public virtual object EditingControlFormattedValue
    {
        get => GetEditingControlFormattedValue(DataGridViewDataErrorContexts.Formatting);
        set => Text = (string?)value;
    }

    public virtual int EditingControlRowIndex
    {
        get => _rowIndex;
        set => _rowIndex = value;
    }

    public virtual bool EditingControlValueChanged
    {
        get => _valueChanged;
        set => _valueChanged = value;
    }

    public virtual Cursor EditingPanelCursor => Cursors.Default;

    public virtual bool RepositionEditingControlOnValueChange => _repositionOnValueChange;

    public virtual void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        if (dataGridViewCellStyle.Font != null) Font = dataGridViewCellStyle.Font;
        if (dataGridViewCellStyle.BackColor.A < 255)
        {
            // The text box does not paint a transparent back colour.
            var opaque = Color.FromArgb(255, dataGridViewCellStyle.BackColor);
            BackColor = opaque;
            if (_dataGridView != null) _dataGridView.EditingPanel.BackColor = opaque;
        }
        else
        {
            BackColor = dataGridViewCellStyle.BackColor;
        }
        ForeColor = dataGridViewCellStyle.ForeColor;
        if (dataGridViewCellStyle.WrapMode == DataGridViewTriState.True) WordWrap = true;
        TextAlign = TranslateAlignment(dataGridViewCellStyle.Alignment);
        _repositionOnValueChange = dataGridViewCellStyle.WrapMode == DataGridViewTriState.True && (dataGridViewCellStyle.Alignment & AnyTop) == 0;
    }

    public virtual bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
    {
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Right:
                // The caret at the end of the text: the grid moves to the next cell.
                if ((RightToLeft == RightToLeft.No && !(SelectionLength == 0 && SelectionStart == Text.Length))
                    || (RightToLeft == RightToLeft.Yes && !(SelectionLength == 0 && SelectionStart == 0)))
                {
                    return true;
                }
                break;
            case Keys.Left:
                if ((RightToLeft == RightToLeft.No && !(SelectionLength == 0 && SelectionStart == 0))
                    || (RightToLeft == RightToLeft.Yes && !(SelectionLength == 0 && SelectionStart == Text.Length)))
                {
                    return true;
                }
                break;
            case Keys.Down:
                // Not on the last line: the text box moves the caret down.
                int end = SelectionStart + SelectionLength;
                if (Text.IndexOf("\r\n", end, StringComparison.Ordinal) != -1) return true;
                break;
            case Keys.Up:
                if (!(Text.IndexOf("\r\n", StringComparison.Ordinal) < 0
                    || SelectionStart + SelectionLength < Text.IndexOf("\r\n", StringComparison.Ordinal)))
                {
                    return true;
                }
                break;
            case Keys.Home:
            case Keys.End:
                if (SelectionLength != Text.Length) return true;
                break;
            case Keys.Prior:
            case Keys.Next:
                if (_valueChanged) return true;
                break;
            case Keys.Delete:
                if (SelectionLength > 0 || SelectionStart < Text.Length) return true;
                break;
            case Keys.Enter:
                if ((keyData & (Keys.Control | Keys.Shift | Keys.Alt)) == Keys.Shift && Multiline && AcceptsReturn) return true;
                break;
        }
        return !dataGridViewWantsInputKey;
    }

    public virtual object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => Text;

    public virtual void PrepareEditingControlForEdit(bool selectAll)
    {
        if (selectAll) SelectAll();
        else SelectionStart = Text.Length; // the caret at the end of the text
    }

    private void NotifyDataGridViewOfValueChange()
    {
        _valueChanged = true;
        _dataGridView?.NotifyCurrentCellDirty(true);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        NotifyDataGridViewOfValueChange();
    }

    protected override void OnMouseWheel(MouseEventArgs e) => _dataGridView?.OnMouseWheelInternal(e);

    private static HorizontalAlignment TranslateAlignment(DataGridViewContentAlignment align) =>
        (align & AnyRight) != 0 ? HorizontalAlignment.Right
        : (align & AnyCenter) != 0 ? HorizontalAlignment.Center
        : HorizontalAlignment.Left;
}

/// <summary>The combo box that edits a combo box cell.</summary>
public class DataGridViewComboBoxEditingControl : ComboBox, IDataGridViewEditingControl
{
    private DataGridView? _dataGridView;
    private bool _valueChanged;
    private int _rowIndex;

    public DataGridViewComboBoxEditingControl()
    {
        TabStop = false;
    }

    public virtual DataGridView? EditingControlDataGridView
    {
        get => _dataGridView;
        set => _dataGridView = value;
    }

    [AllowNull]
    public virtual object EditingControlFormattedValue
    {
        get => GetEditingControlFormattedValue(DataGridViewDataErrorContexts.Formatting);
        set
        {
            if (value is string text)
            {
                // The item with that text, found without regard to case, as the native combo box does.
                int index = -1;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (string.Compare(GetItemText(Items[i]), text, true, CultureInfo.CurrentCulture) == 0) { index = i; break; }
                }
                if (index >= 0) SelectedIndex = index;
                else if (DropDownStyle == ComboBoxStyle.DropDownList) SelectedIndex = -1;
                else Text = text;
            }
        }
    }

    public virtual int EditingControlRowIndex
    {
        get => _rowIndex;
        set => _rowIndex = value;
    }

    public virtual bool EditingControlValueChanged
    {
        get => _valueChanged;
        set => _valueChanged = value;
    }

    public virtual Cursor EditingPanelCursor => Cursors.Default;

    public virtual bool RepositionEditingControlOnValueChange => false;

    public virtual void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        if (dataGridViewCellStyle.Font != null) Font = dataGridViewCellStyle.Font;
        if (dataGridViewCellStyle.BackColor.A < 255)
        {
            var opaque = Color.FromArgb(255, dataGridViewCellStyle.BackColor);
            BackColor = opaque;
            if (_dataGridView != null) _dataGridView.EditingPanel.BackColor = opaque;
        }
        else
        {
            BackColor = dataGridViewCellStyle.BackColor;
        }
        ForeColor = dataGridViewCellStyle.ForeColor;
    }

    public virtual bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
    {
        var key = keyData & Keys.KeyCode;
        if (key == Keys.Down || key == Keys.Up || (DroppedDown && key == Keys.Escape) || key == Keys.Enter) return true;
        return !dataGridViewWantsInputKey;
    }

    public virtual object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => Text;

    public virtual void PrepareEditingControlForEdit(bool selectAll)
    {
        if (selectAll) SelectAll();
    }

    private void NotifyDataGridViewOfValueChange()
    {
        _valueChanged = true;
        _dataGridView?.NotifyCurrentCellDirty(true);
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        if (SelectedIndex != -1) NotifyDataGridViewOfValueChange();
    }
}

public delegate void QuestionEventHandler(object? sender, QuestionEventArgs e);

/// <summary>A yes/no question an event asks its handler (VirtualMode's RowDirtyStateNeeded and CancelRowEdit).</summary>
public class QuestionEventArgs : EventArgs
{
    public QuestionEventArgs()
    {
    }

    public QuestionEventArgs(bool response)
    {
        Response = response;
    }

    public bool Response { get; set; }
}
