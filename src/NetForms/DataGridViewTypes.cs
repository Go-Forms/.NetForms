using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

// --- enums -------------------------------------------------------------------------------

public enum DataGridViewSelectionMode
{
    CellSelect = 0,
    FullRowSelect = 1,
    FullColumnSelect = 2,
    RowHeaderSelect = 3,
    ColumnHeaderSelect = 4,
}

public enum DataGridViewColumnHeadersHeightSizeMode
{
    EnableResizing = 0,
    DisableResizing = 1,
    AutoSize = 2,
}

public delegate void DataGridViewAutoSizeModeEventHandler(object? sender, DataGridViewAutoSizeModeEventArgs e);

public class DataGridViewAutoSizeModeEventArgs : EventArgs
{
    public DataGridViewAutoSizeModeEventArgs(bool previousModeAutoSized) => PreviousModeAutoSized = previousModeAutoSized;

    public bool PreviousModeAutoSized { get; }
}

public enum DataGridViewAutoSizeColumnsMode
{
    None = 1,
    ColumnHeader = 2,
    AllCellsExceptHeader = 4,
    AllCells = 6,
    DisplayedCellsExceptHeader = 8,
    DisplayedCells = 10,
    Fill = 16,
}

public enum DataGridViewAutoSizeColumnMode
{
    NotSet = 0,
    None = 1,
    ColumnHeader = 2,
    AllCellsExceptHeader = 4,
    AllCells = 6,
    DisplayedCellsExceptHeader = 8,
    DisplayedCells = 10,
    Fill = 16,
}

public enum DataGridViewColumnSortMode
{
    NotSortable = 0,
    Automatic = 1,
    Programmatic = 2,
}

public enum DataGridViewTriState
{
    NotSet = 0,
    True = 1,
    False = 2,
}

public enum DataGridViewCellBorderStyle
{
    Custom = 0,
    Single = 1,
    Raised = 2,
    Sunken = 3,
    None = 4,
    SingleVertical = 5,
    RaisedVertical = 6,
    SunkenVertical = 7,
    SingleHorizontal = 8,
    RaisedHorizontal = 9,
    SunkenHorizontal = 10,
}

public enum DataGridViewHeaderBorderStyle
{
    Custom = 0,
    Single = 1,
    Raised = 2,
    Sunken = 3,
    None = 4,
}

public enum DataGridViewEditMode
{
    EditOnEnter = 0,
    EditOnKeystroke = 1,
    EditOnKeystrokeOrF2 = 2,
    EditOnF2 = 3,
    EditProgrammatically = 4,
}

[Flags]
public enum DataGridViewElementStates
{
    None = 0,
    Displayed = 1,
    Frozen = 2,
    ReadOnly = 4,
    Resizable = 8,
    ResizableSet = 16,
    Selected = 32,
    Visible = 64,
}

public enum DataGridViewContentAlignment
{
    NotSet = 0,
    TopLeft = 1,
    TopCenter = 2,
    TopRight = 4,
    MiddleLeft = 16,
    MiddleCenter = 32,
    MiddleRight = 64,
    BottomLeft = 256,
    BottomCenter = 512,
    BottomRight = 1024,
}

public enum DataGridViewHitTestType
{
    None = 0,
    Cell = 1,
    ColumnHeader = 2,
    RowHeader = 3,
    TopLeftHeader = 4,
    HorizontalScrollBar = 5,
    VerticalScrollBar = 6,
}

[Flags]
public enum DataGridViewDataErrorContexts
{
    Formatting = 0x0001,
    Display = 0x0002,
    PreferredSize = 0x0004,
    RowDeletion = 0x0008,
    Parsing = 0x0100,
    Commit = 0x0200,
    InitialValueRestoration = 0x0400,
    LeaveControl = 0x0800,
    CurrentCellChange = 0x1000,
    Scroll = 0x2000,
    ClipboardContent = 0x4000,
}

/// <summary>The wrap mode of a cell's text, as WinForms' DataGridViewTriState-based setting.</summary>
public enum DataGridViewTriStateWrapMode
{
    NotSet = 0,
    True = 1,
    False = 2,
}

// --- cell style ----------------------------------------------------------------------------

/// <summary>
/// Colours, font, alignment and format of a cell. Styles cascade: cell → row → alternating row →
/// column → grid default, and an unset member takes the next style's value (WinForms' InheritedStyle).
/// </summary>
public class DataGridViewCellStyle : ICloneable
{
    public DataGridViewCellStyle() { }

    public DataGridViewCellStyle(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        ApplyStyle(dataGridViewCellStyle);
    }

    [Category("Appearance")]
    public Color BackColor { get; set; } = Color.Empty;

    [Category("Appearance")]
    public Color ForeColor { get; set; } = Color.Empty;

    [Category("Appearance")]
    public Color SelectionBackColor { get; set; } = Color.Empty;

    [Category("Appearance")]
    public Color SelectionForeColor { get; set; } = Color.Empty;

    [Category("Appearance")]
    public Font? Font { get; set; }

    [Category("Layout")]
    [Description("Indicates the position of the content within a cell.")]
    [DefaultValue(DataGridViewContentAlignment.NotSet)]
    public DataGridViewContentAlignment Alignment { get; set; } = DataGridViewContentAlignment.NotSet;

    [Category("Layout")]
    [DefaultValue(DataGridViewTriState.NotSet)]
    public DataGridViewTriState WrapMode { get; set; } = DataGridViewTriState.NotSet;

    [Category("Layout")]
    public Padding Padding { get; set; } = Padding.Empty;

    [Category("Behavior")]
    [DefaultValue("")]
    public string Format { get; set; } = string.Empty;

    [Browsable(false)]
    public IFormatProvider? FormatProvider { get; set; }

    [Category("Data")]
    [DefaultValue(typeof(Object), "")]
    public object? NullValue { get; set; } = string.Empty;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? DataSourceNullValue { get; set; } = DBNull.Value;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? Tag { get; set; }

    [Browsable(false)]
    public bool IsFormatProviderDefault => FormatProvider == null;

    // What the designer writes of a style: only what was set (the colors, font, padding and provider
    // have no [DefaultValue] in WinForms - these methods answer instead).
    internal bool ShouldSerializeBackColor() => !BackColor.IsEmpty;
    internal bool ShouldSerializeForeColor() => !ForeColor.IsEmpty;
    internal bool ShouldSerializeSelectionBackColor() => !SelectionBackColor.IsEmpty;
    internal bool ShouldSerializeSelectionForeColor() => !SelectionForeColor.IsEmpty;
    internal bool ShouldSerializeFont() => Font != null;
    internal bool ShouldSerializePadding() => Padding != Padding.Empty;
    internal bool ShouldSerializeFormatProvider() => FormatProvider != null;

    /// <summary>Every member equal - what the designer compares against a default style before writing one.</summary>
    internal bool IsEquivalentTo(DataGridViewCellStyle other) =>
        BackColor == other.BackColor && ForeColor == other.ForeColor
        && SelectionBackColor == other.SelectionBackColor && SelectionForeColor == other.SelectionForeColor
        && Equals(Font, other.Font) && Alignment == other.Alignment && WrapMode == other.WrapMode
        && Padding == other.Padding && Format == other.Format && Equals(FormatProvider, other.FormatProvider)
        && Equals(NullValue, other.NullValue) && Equals(DataSourceNullValue, other.DataSourceNullValue)
        && Equals(Tag, other.Tag);

    /// <summary>Copies every member that <paramref name="other"/> has set over this style's unset ones.</summary>
    public virtual void ApplyStyle(DataGridViewCellStyle dataGridViewCellStyle)
    {
        ArgumentNullException.ThrowIfNull(dataGridViewCellStyle);
        var s = dataGridViewCellStyle;
        if (!s.BackColor.IsEmpty) BackColor = s.BackColor;
        if (!s.ForeColor.IsEmpty) ForeColor = s.ForeColor;
        if (!s.SelectionBackColor.IsEmpty) SelectionBackColor = s.SelectionBackColor;
        if (!s.SelectionForeColor.IsEmpty) SelectionForeColor = s.SelectionForeColor;
        if (s.Font != null) Font = s.Font;
        if (s.Alignment != DataGridViewContentAlignment.NotSet) Alignment = s.Alignment;
        if (s.WrapMode != DataGridViewTriState.NotSet) WrapMode = s.WrapMode;
        if (s.Padding != Padding.Empty) Padding = s.Padding;
        if (!string.IsNullOrEmpty(s.Format)) Format = s.Format;
        if (s.FormatProvider != null) FormatProvider = s.FormatProvider;
        // "Unset" for the null placeholders means the defaults: an empty string and DBNull.
        if (s.NullValue is not string { Length: 0 }) NullValue = s.NullValue;
        if (s.DataSourceNullValue != DBNull.Value) DataSourceNullValue = s.DataSourceNullValue;
        if (s.Tag != null) Tag = s.Tag;
    }

    public virtual object Clone() => new DataGridViewCellStyle(this);

    public override bool Equals(object? obj) =>
        obj is DataGridViewCellStyle s && s.BackColor == BackColor && s.ForeColor == ForeColor
        && s.SelectionBackColor == SelectionBackColor && s.SelectionForeColor == SelectionForeColor
        && Equals(s.Font, Font) && s.Alignment == Alignment && s.WrapMode == WrapMode
        && s.Padding == Padding && s.Format == Format;

    public override int GetHashCode() => HashCode.Combine(BackColor, ForeColor, Font, Alignment, WrapMode, Padding, Format);

    /// <summary>The members that are set, in WinForms' order and format.</summary>
    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (!BackColor.IsEmpty) parts.Add($"BackColor={BackColor}");
        if (!ForeColor.IsEmpty) parts.Add($"ForeColor={ForeColor}");
        if (!SelectionBackColor.IsEmpty) parts.Add($"SelectionBackColor={SelectionBackColor}");
        if (!SelectionForeColor.IsEmpty) parts.Add($"SelectionForeColor={SelectionForeColor}");
        if (Font != null) parts.Add($"Font={Font}");
        if (NullValue is not string { Length: 0 }) parts.Add($"NullValue={NullValue}");
        if (DataSourceNullValue is not DBNull) parts.Add($"DataSourceNullValue={DataSourceNullValue}");
        if (!string.IsNullOrEmpty(Format)) parts.Add($"Format={Format}");
        if (WrapMode != DataGridViewTriState.NotSet) parts.Add($"WrapMode={WrapMode}");
        if (Alignment != DataGridViewContentAlignment.NotSet) parts.Add($"Alignment={Alignment}");
        if (Padding != Padding.Empty) parts.Add($"Padding={Padding}");
        if (Tag != null) parts.Add($"Tag={Tag}");
        return "DataGridViewCellStyle {" + (parts.Count == 0 ? "" : " " + string.Join(", ", parts)) + " }";
    }
}

// --- advanced border styles (accepted, drawn as a single line) ---------------------------------

public enum DataGridViewAdvancedCellBorderStyle
{
    NotSet = 0,
    None = 1,
    Single = 2,
    Inset = 3,
    InsetDouble = 4,
    Outset = 5,
    OutsetDouble = 6,
    OutsetPartial = 7,
    OutsetPartialDouble = 8,
}

public sealed class DataGridViewAdvancedBorderStyle : ICloneable
{
    public DataGridViewAdvancedCellBorderStyle All
    {
        get => Left == Top && Top == Right && Right == Bottom ? Left : DataGridViewAdvancedCellBorderStyle.NotSet;
        set => Left = Top = Right = Bottom = value;
    }

    public DataGridViewAdvancedCellBorderStyle Left { get; set; } = DataGridViewAdvancedCellBorderStyle.Single;
    public DataGridViewAdvancedCellBorderStyle Right { get; set; } = DataGridViewAdvancedCellBorderStyle.Single;
    public DataGridViewAdvancedCellBorderStyle Top { get; set; } = DataGridViewAdvancedCellBorderStyle.Single;
    public DataGridViewAdvancedCellBorderStyle Bottom { get; set; } = DataGridViewAdvancedCellBorderStyle.Single;

    public object Clone() => new DataGridViewAdvancedBorderStyle { Left = Left, Right = Right, Top = Top, Bottom = Bottom };
}

// --- event args -----------------------------------------------------------------------------------

public delegate void DataGridViewCellEventHandler(object? sender, DataGridViewCellEventArgs e);
public delegate void DataGridViewCellMouseEventHandler(object? sender, DataGridViewCellMouseEventArgs e);
public delegate void DataGridViewCellFormattingEventHandler(object? sender, DataGridViewCellFormattingEventArgs e);
public delegate void DataGridViewCellPaintingEventHandler(object? sender, DataGridViewCellPaintingEventArgs e);
public delegate void DataGridViewCellCancelEventHandler(object? sender, DataGridViewCellCancelEventArgs e);
public delegate void DataGridViewCellValueEventHandler(object? sender, DataGridViewCellValueEventArgs e);
public delegate void DataGridViewRowsAddedEventHandler(object? sender, DataGridViewRowsAddedEventArgs e);
public delegate void DataGridViewRowsRemovedEventHandler(object? sender, DataGridViewRowsRemovedEventArgs e);
public delegate void DataGridViewSortCompareEventHandler(object? sender, DataGridViewSortCompareEventArgs e);
public delegate void DataGridViewDataErrorEventHandler(object? sender, DataGridViewDataErrorEventArgs e);
public delegate void DataGridViewRowCancelEventHandler(object? sender, DataGridViewRowCancelEventArgs e);
public delegate void DataGridViewRowEventHandler(object? sender, DataGridViewRowEventArgs e);

public class DataGridViewCellEventArgs : EventArgs
{
    public DataGridViewCellEventArgs(int columnIndex, int rowIndex)
    {
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
    }

    public int ColumnIndex { get; }
    public int RowIndex { get; }
}

public class DataGridViewCellMouseEventArgs : MouseEventArgs
{
    public DataGridViewCellMouseEventArgs(int columnIndex, int rowIndex, int localX, int localY, MouseEventArgs e)
        : base(e.Button, e.Clicks, localX, localY, e.Delta)
    {
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
    }

    public int ColumnIndex { get; }
    public int RowIndex { get; }
}

public class DataGridViewCellCancelEventArgs : CancelEventArgs
{
    public DataGridViewCellCancelEventArgs(int columnIndex, int rowIndex)
    {
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
    }

    public int ColumnIndex { get; }
    public int RowIndex { get; }
}

public class DataGridViewCellFormattingEventArgs : ConvertEventArgs
{
    public DataGridViewCellFormattingEventArgs(int columnIndex, int rowIndex, object? value, Type? desiredType, DataGridViewCellStyle? cellStyle)
        : base(value, desiredType!)
    {
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
        CellStyle = cellStyle;
    }

    public int ColumnIndex { get; }
    public int RowIndex { get; }
    public DataGridViewCellStyle? CellStyle { get; set; }
    public bool FormattingApplied { get; set; }
}

public class DataGridViewCellValueEventArgs : EventArgs
{
    public DataGridViewCellValueEventArgs(int columnIndex, int rowIndex)
    {
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
    }

    public int ColumnIndex { get; }
    public int RowIndex { get; }
    public object? Value { get; set; }
}

public class DataGridViewCellPaintingEventArgs : HandledEventArgs
{
    public DataGridViewCellPaintingEventArgs(DataGridView dataGridView, Graphics graphics, Rectangle clipBounds, Rectangle cellBounds,
        int rowIndex, int columnIndex, DataGridViewElementStates cellState, object? value, object? formattedValue,
        string? errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
    {
        DataGridView = dataGridView;
        Graphics = graphics;
        ClipBounds = clipBounds;
        CellBounds = cellBounds;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        State = cellState;
        Value = value;
        FormattedValue = formattedValue;
        ErrorText = errorText;
        CellStyle = cellStyle;
        AdvancedBorderStyle = advancedBorderStyle;
        PaintParts = paintParts;
    }

    public DataGridView DataGridView { get; }
    public Graphics Graphics { get; }
    public Rectangle ClipBounds { get; }
    public Rectangle CellBounds { get; }
    public int RowIndex { get; }
    public int ColumnIndex { get; }
    public DataGridViewElementStates State { get; }
    public object? Value { get; }
    public object? FormattedValue { get; }
    public string? ErrorText { get; }
    public DataGridViewCellStyle CellStyle { get; }
    public DataGridViewAdvancedBorderStyle AdvancedBorderStyle { get; }
    public DataGridViewPaintParts PaintParts { get; }

    public void PaintBackground(Rectangle clipBounds, bool cellsPaintSelectionBackground) =>
        DataGridView.PaintCellBackground(this, clipBounds, cellsPaintSelectionBackground);

    public void PaintContent(Rectangle clipBounds) => DataGridView.PaintCellContent(this, clipBounds);
}

[Flags]
public enum DataGridViewPaintParts
{
    None = 0,
    Background = 1,
    Border = 2,
    ContentBackground = 4,
    ContentForeground = 8,
    ErrorIcon = 16,
    Focus = 32,
    SelectionBackground = 64,
    All = 127,
}

public class DataGridViewRowsAddedEventArgs : EventArgs
{
    public DataGridViewRowsAddedEventArgs(int rowIndex, int rowCount)
    {
        RowIndex = rowIndex;
        RowCount = rowCount;
    }

    public int RowIndex { get; }
    public int RowCount { get; }
}

public class DataGridViewRowsRemovedEventArgs : EventArgs
{
    public DataGridViewRowsRemovedEventArgs(int rowIndex, int rowCount)
    {
        RowIndex = rowIndex;
        RowCount = rowCount;
    }

    public int RowIndex { get; }
    public int RowCount { get; }
}

public class DataGridViewRowEventArgs : EventArgs
{
    public DataGridViewRowEventArgs(DataGridViewRow dataGridViewRow) => Row = dataGridViewRow;

    public DataGridViewRow Row { get; }
}

public class DataGridViewRowCancelEventArgs : CancelEventArgs
{
    public DataGridViewRowCancelEventArgs(DataGridViewRow dataGridViewRow) => Row = dataGridViewRow;

    public DataGridViewRow Row { get; }
}

public class DataGridViewSortCompareEventArgs : HandledEventArgs
{
    public DataGridViewSortCompareEventArgs(DataGridViewColumn dataGridViewColumn, object? cellValue1, object? cellValue2, int rowIndex1, int rowIndex2)
    {
        Column = dataGridViewColumn;
        CellValue1 = cellValue1;
        CellValue2 = cellValue2;
        RowIndex1 = rowIndex1;
        RowIndex2 = rowIndex2;
    }

    public DataGridViewColumn Column { get; }
    public object? CellValue1 { get; }
    public object? CellValue2 { get; }
    public int RowIndex1 { get; }
    public int RowIndex2 { get; }
    public int SortResult { get; set; }
}

public class DataGridViewDataErrorEventArgs : DataGridViewCellCancelEventArgs
{
    public DataGridViewDataErrorEventArgs(Exception? exception, int columnIndex, int rowIndex, DataGridViewDataErrorContexts context)
        : base(columnIndex, rowIndex)
    {
        Exception = exception;
        Context = context;
    }

    public Exception? Exception { get; }
    public DataGridViewDataErrorContexts Context { get; }
    public bool ThrowException { get; set; }
}

public class DataGridViewHitTestInfo
{
    public static readonly DataGridViewHitTestInfo Nowhere = new(DataGridViewHitTestType.None, -1, -1);

    internal DataGridViewHitTestInfo(DataGridViewHitTestType type, int columnIndex, int rowIndex)
    {
        Type = type;
        ColumnIndex = columnIndex;
        RowIndex = rowIndex;
    }

    public DataGridViewHitTestType Type { get; }
    public int ColumnIndex { get; }
    public int RowIndex { get; }

    public override string ToString() => $"DataGridViewHitTestInfo {{ Type={Type}, Column={ColumnIndex}, Row={RowIndex} }}";
}
