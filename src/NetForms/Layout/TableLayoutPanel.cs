using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

public enum SizeType
{
    AutoSize = 0,
    Absolute = 1,
    Percent = 2,
}

public enum TableLayoutPanelGrowStyle
{
    FixedSize = 0,
    AddRows = 1,
    AddColumns = 2,
}

public enum TableLayoutPanelCellBorderStyle
{
    None = 0,
    Single = 1,
    Inset = 2,
    InsetDouble = 3,
    Outset = 4,
    OutsetDouble = 5,
    OutsetPartial = 6,
}

public abstract class TableLayoutStyle
{
    private SizeType _sizeType = SizeType.AutoSize;
    private float _size;

    internal TableLayoutPanel? Owner { get; set; }

    public SizeType SizeType
    {
        get => _sizeType;
        set
        {
            if (_sizeType == value) return;
            _sizeType = value;
            Owner?.PerformLayout(Owner, "Style");
        }
    }

    internal float Size
    {
        get => _size;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_size == value) return;
            _size = value;
            Owner?.PerformLayout(Owner, "Style");
        }
    }
}

public class ColumnStyle : TableLayoutStyle
{
    public ColumnStyle() { }

    public ColumnStyle(SizeType sizeType) => SizeType = sizeType;

    public ColumnStyle(SizeType sizeType, float width)
    {
        SizeType = sizeType;
        Width = width;
    }

    public float Width
    {
        get => Size;
        set => Size = value;
    }
}

public class RowStyle : TableLayoutStyle
{
    public RowStyle() { }

    public RowStyle(SizeType sizeType) => SizeType = sizeType;

    public RowStyle(SizeType sizeType, float height)
    {
        SizeType = sizeType;
        Height = height;
    }

    public float Height
    {
        get => Size;
        set => Size = value;
    }
}

public abstract class TableLayoutStyleCollection : IList
{
    private readonly List<TableLayoutStyle> _items = new();
    private readonly TableLayoutPanel _owner;

    internal TableLayoutStyleCollection(TableLayoutPanel owner) => _owner = owner;

    public int Count => _items.Count;
    public bool IsReadOnly => false;
    public bool IsFixedSize => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public TableLayoutStyle this[int index]
    {
        get => _items[index];
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.Owner = _owner;
            _items[index] = value;
            _owner.PerformLayout(_owner, "Styles");
        }
    }

    object? IList.this[int index]
    {
        get => this[index];
        set => this[index] = (TableLayoutStyle)value!;
    }

    public int Add(TableLayoutStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        style.Owner = _owner;
        _items.Add(style);
        _owner.PerformLayout(_owner, "Styles");
        return _items.Count - 1;
    }

    int IList.Add(object? value) => Add((TableLayoutStyle)value!);

    public void Insert(int index, TableLayoutStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        style.Owner = _owner;
        _items.Insert(index, style);
        _owner.PerformLayout(_owner, "Styles");
    }

    void IList.Insert(int index, object? value) => Insert(index, (TableLayoutStyle)value!);

    public void Remove(TableLayoutStyle style)
    {
        if (_items.Remove(style)) _owner.PerformLayout(_owner, "Styles");
    }

    void IList.Remove(object? value) => Remove((TableLayoutStyle)value!);

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        _owner.PerformLayout(_owner, "Styles");
    }

    public void Clear()
    {
        _items.Clear();
        _owner.PerformLayout(_owner, "Styles");
    }

    public bool Contains(TableLayoutStyle style) => _items.Contains(style);
    bool IList.Contains(object? value) => value is TableLayoutStyle s && Contains(s);
    public int IndexOf(TableLayoutStyle style) => _items.IndexOf(style);
    int IList.IndexOf(object? value) => value is TableLayoutStyle s ? IndexOf(s) : -1;
    public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
    public IEnumerator GetEnumerator() => _items.GetEnumerator();
}

public class TableLayoutColumnStyleCollection : TableLayoutStyleCollection
{
    internal TableLayoutColumnStyleCollection(TableLayoutPanel owner) : base(owner) { }

    public new ColumnStyle this[int index]
    {
        get => (ColumnStyle)base[index];
        set => base[index] = value;
    }

    public int Add(ColumnStyle columnStyle) => base.Add(columnStyle);
    public void Insert(int index, ColumnStyle columnStyle) => base.Insert(index, columnStyle);
    public void Remove(ColumnStyle columnStyle) => base.Remove(columnStyle);
    public bool Contains(ColumnStyle columnStyle) => base.Contains(columnStyle);
    public int IndexOf(ColumnStyle columnStyle) => base.IndexOf(columnStyle);
}

public class TableLayoutRowStyleCollection : TableLayoutStyleCollection
{
    internal TableLayoutRowStyleCollection(TableLayoutPanel owner) : base(owner) { }

    public new RowStyle this[int index]
    {
        get => (RowStyle)base[index];
        set => base[index] = value;
    }

    public int Add(RowStyle rowStyle) => base.Add(rowStyle);
    public void Insert(int index, RowStyle rowStyle) => base.Insert(index, rowStyle);
    public void Remove(RowStyle rowStyle) => base.Remove(rowStyle);
    public bool Contains(RowStyle rowStyle) => base.Contains(rowStyle);
    public int IndexOf(RowStyle rowStyle) => base.IndexOf(rowStyle);
}

public struct TableLayoutPanelCellPosition : IEquatable<TableLayoutPanelCellPosition>
{
    public TableLayoutPanelCellPosition(int column, int row)
    {
        if (column < -1) throw new ArgumentOutOfRangeException(nameof(column));
        if (row < -1) throw new ArgumentOutOfRangeException(nameof(row));
        Column = column;
        Row = row;
    }

    public int Column { get; set; }
    public int Row { get; set; }

    public readonly bool Equals(TableLayoutPanelCellPosition other) => Column == other.Column && Row == other.Row;
    public override readonly bool Equals(object? obj) => obj is TableLayoutPanelCellPosition p && Equals(p);
    public override readonly int GetHashCode() => HashCode.Combine(Column, Row);
    public static bool operator ==(TableLayoutPanelCellPosition a, TableLayoutPanelCellPosition b) => a.Equals(b);
    public static bool operator !=(TableLayoutPanelCellPosition a, TableLayoutPanelCellPosition b) => !a.Equals(b);
    public override readonly string ToString() => Column + "," + Row;
}

public delegate void TableLayoutCellPaintEventHandler(object? sender, TableLayoutCellPaintEventArgs e);

public class TableLayoutCellPaintEventArgs : PaintEventArgs
{
    public TableLayoutCellPaintEventArgs(Graphics g, Rectangle clipRectangle, Rectangle cellBounds, int column, int row) : base(g, clipRectangle)
    {
        CellBounds = cellBounds;
        Column = column;
        Row = row;
    }

    public Rectangle CellBounds { get; }
    public int Column { get; }
    public int Row { get; }
}

/// <summary>Cell assignment of one control: explicit or flowing (−1), plus spans.</summary>
internal sealed class TableCellInfo
{
    public int Column = -1;
    public int Row = -1;
    public int ColumnSpan = 1;
    public int RowSpan = 1;
}

/// <summary>Per-control table settings; the panel implements them, this is WinForms' API shape.</summary>
public sealed class TableLayoutSettings : LayoutSettings
{
    private readonly TableLayoutPanel _owner;

    internal TableLayoutSettings(TableLayoutPanel owner) => _owner = owner;

    public override LayoutEngine LayoutEngine => TableLayout.Instance;

    public int ColumnCount { get => _owner.ColumnCount; set => _owner.ColumnCount = value; }
    public int RowCount { get => _owner.RowCount; set => _owner.RowCount = value; }
    public TableLayoutColumnStyleCollection ColumnStyles => _owner.ColumnStyles;
    public TableLayoutRowStyleCollection RowStyles => _owner.RowStyles;
    public TableLayoutPanelGrowStyle GrowStyle { get => _owner.GrowStyle; set => _owner.GrowStyle = value; }

    public int GetColumn(object control) => _owner.GetColumn((Control)control);
    public void SetColumn(object control, int column) => _owner.SetColumn((Control)control, column);
    public int GetRow(object control) => _owner.GetRow((Control)control);
    public void SetRow(object control, int row) => _owner.SetRow((Control)control, row);
    public int GetColumnSpan(object control) => _owner.GetColumnSpan((Control)control);
    public void SetColumnSpan(object control, int value) => _owner.SetColumnSpan((Control)control, value);
    public int GetRowSpan(object control) => _owner.GetRowSpan((Control)control);
    public void SetRowSpan(object control, int value) => _owner.SetRowSpan((Control)control, value);
    public TableLayoutPanelCellPosition GetCellPosition(object control) => _owner.GetCellPosition((Control)control);
    public void SetCellPosition(object control, TableLayoutPanelCellPosition cellPosition) => _owner.SetCellPosition((Control)control, cellPosition);
}

/// <summary>
/// A grid of styled rows and columns. Absolute tracks take their pixels, AutoSize tracks
/// fit their widest content, Percent tracks share what is left (percentages normalised),
/// and any leftover goes to the last AutoSize track. A child sits in its cell by its
/// Anchor/Dock, inside its Margin.
/// </summary>
/// <remarks>
/// An extender provider, as in WinForms: Column, Row, ColumnSpan, RowSpan and CellPosition are
/// properties it gives its children. The designer writes ColumnSpan/RowSpan as
/// <c>tableLayoutPanel1.SetColumnSpan(child, 2)</c> in the child's block; position goes into
/// <c>Controls.Add(child, column, row)</c>, which is why Column, Row and CellPosition are hidden.
/// </remarks>
[ProvideProperty("ColumnSpan", typeof(Control))]
[ProvideProperty("RowSpan", typeof(Control))]
[ProvideProperty("Row", typeof(Control))]
[ProvideProperty("Column", typeof(Control))]
[ProvideProperty("CellPosition", typeof(Control))]
[DefaultProperty("ColumnCount")]
public class TableLayoutPanel : Panel, IExtenderProvider
{
    bool IExtenderProvider.CanExtend(object obj) => obj is Control control && control.Parent == this;

    private readonly TableLayoutSettings _settings;
    private readonly TableLayoutColumnStyleCollection _columnStyles;
    private readonly TableLayoutRowStyleCollection _rowStyles;
    private readonly Dictionary<Control, TableCellInfo> _cells = new();
    private int _columnCount;
    private int _rowCount;
    private TableLayoutPanelGrowStyle _growStyle = TableLayoutPanelGrowStyle.AddRows;
    private TableLayoutPanelCellBorderStyle _cellBorderStyle = TableLayoutPanelCellBorderStyle.None;
    private int[] _columnWidths = Array.Empty<int>();
    private int[] _rowHeights = Array.Empty<int>();
    private Dictionary<Control, TableLayoutPanelCellPosition> _placed = new();

    public TableLayoutPanel()
    {
        _settings = new TableLayoutSettings(this);
        _columnStyles = new TableLayoutColumnStyleCollection(this);
        _rowStyles = new TableLayoutRowStyleCollection(this);
    }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override LayoutEngine LayoutEngine => TableLayout.Instance;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TableLayoutSettings LayoutSettings => _settings;

    [Category("Appearance")]
    [Description("Occurs when a cell needs repainting.")]
    public event TableLayoutCellPaintEventHandler? CellPaint;

    [Category("Layout")]
    [Description("The column styles of the table.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public TableLayoutColumnStyleCollection ColumnStyles => _columnStyles;

    [Category("Layout")]
    [Description("The row styles of the table.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public TableLayoutRowStyleCollection RowStyles => _rowStyles;

    [Category("Layout")]
    [Description("The number of columns on the table.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int ColumnCount
    {
        get => _columnCount;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_columnCount == value) return;
            _columnCount = value;
            PerformLayout(this, nameof(ColumnCount));
        }
    }

    [Category("Layout")]
    [Description("The number of rows on the table.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int RowCount
    {
        get => _rowCount;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (_rowCount == value) return;
            _rowCount = value;
            PerformLayout(this, nameof(RowCount));
        }
    }

    [Category("Layout")]
    [Description("Indicates whether a TableLayoutPanel will expand to include new cells when all existing cells are occupied.")]
    [DefaultValue(TableLayoutPanelGrowStyle.AddRows)]
    public TableLayoutPanelGrowStyle GrowStyle
    {
        get => _growStyle;
        set
        {
            _growStyle = value;
            PerformLayout(this, nameof(GrowStyle));
        }
    }

    [Category("Appearance")]
    [Description("Indicates the appearance of cell borders in a table.")]
    [DefaultValue(TableLayoutPanelCellBorderStyle.None)]
    [Localizable(true)]
    public TableLayoutPanelCellBorderStyle CellBorderStyle
    {
        get => _cellBorderStyle;
        set
        {
            _cellBorderStyle = value;
            PerformLayout(this, nameof(CellBorderStyle));
            Invalidate();
        }
    }

    internal int CellBorderWidth => _cellBorderStyle switch
    {
        TableLayoutPanelCellBorderStyle.None => 0,
        TableLayoutPanelCellBorderStyle.Single => 1,
        TableLayoutPanelCellBorderStyle.Inset or TableLayoutPanelCellBorderStyle.Outset => 2,
        _ => 3,
    };

    [Description("The collection of child controls within this control.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public new TableLayoutControlCollection Controls => (TableLayoutControlCollection)base.Controls;

    protected override ControlCollection CreateControlsInstance() => new TableLayoutControlCollection(this);

    // --- per-control settings ------------------------------------------------------

    private TableCellInfo Info(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (!_cells.TryGetValue(control, out var info))
        {
            info = new TableCellInfo();
            _cells[control] = info;
        }
        return info;
    }

    [DefaultValue(-1)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GetColumn(Control control) => Info(control).Column;

    public void SetColumn(Control control, int column)
    {
        if (column < -1) throw new ArgumentOutOfRangeException(nameof(column));
        Info(control).Column = column;
        PerformLayout(control, "Column");
    }

    [DefaultValue(-1)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int GetRow(Control control) => Info(control).Row;

    public void SetRow(Control control, int row)
    {
        if (row < -1) throw new ArgumentOutOfRangeException(nameof(row));
        Info(control).Row = row;
        PerformLayout(control, "Row");
    }

    [DefaultValue(1)]
    public int GetColumnSpan(Control control) => Info(control).ColumnSpan;

    public void SetColumnSpan(Control control, int value)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
        Info(control).ColumnSpan = value;
        PerformLayout(control, "ColumnSpan");
    }

    [DefaultValue(1)]
    public int GetRowSpan(Control control) => Info(control).RowSpan;

    public void SetRowSpan(Control control, int value)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
        Info(control).RowSpan = value;
        PerformLayout(control, "RowSpan");
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public TableLayoutPanelCellPosition GetCellPosition(Control control)
    {
        var i = Info(control);
        return new TableLayoutPanelCellPosition(i.Column, i.Row);
    }

    public void SetCellPosition(Control control, TableLayoutPanelCellPosition position)
    {
        var i = Info(control);
        i.Column = position.Column;
        i.Row = position.Row;
        PerformLayout(control, "CellPosition");
    }

    /// <summary>Where the control actually ended up after the last layout (flowing controls included).</summary>
    public TableLayoutPanelCellPosition GetPositionFromControl(Control? control)
    {
        if (control != null && Placement.TryGetValue(control, out var p)) return p;
        return new TableLayoutPanelCellPosition(-1, -1);
    }

    /// <summary>Cell assignments; computed on demand while layout is suspended (designer code queries them before ResumeLayout).</summary>
    private Dictionary<Control, TableLayoutPanelCellPosition> Placement
    {
        get
        {
            if (_placed.Count == 0 && Controls.Count > 0) _placed = TableLayout.ComputePlacement(this);
            return _placed;
        }
    }

    public Control? GetControlFromPosition(int column, int row)
    {
        foreach (var (control, pos) in Placement)
        {
            var info = Info(control);
            if (column >= pos.Column && column < pos.Column + info.ColumnSpan && row >= pos.Row && row < pos.Row + info.RowSpan && control.VisibleOwn)
                return control;
        }
        return null;
    }

    public int[] GetColumnWidths() => (int[])_columnWidths.Clone();

    public int[] GetRowHeights() => (int[])_rowHeights.Clone();

    internal void SetTrackSizes(int[] columns, int[] rows, Dictionary<Control, TableLayoutPanelCellPosition> placed)
    {
        _columnWidths = columns;
        _rowHeights = rows;
        _placed = placed;
    }

    internal void ForgetControl(Control control)
    {
        _cells.Remove(control);
        _placed.Clear();
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var (cols, rows) = TableLayout.MeasureTracks(this, proposedSize, forPreferred: true);
        int w = 0, h = 0;
        foreach (var c in cols) w += c;
        foreach (var r in rows) h += r;
        int border = CellBorderWidth;
        return new Size(w + Padding.Horizontal + border * (cols.Length + 1), h + Padding.Vertical + border * (rows.Length + 1));
    }

    protected virtual void OnCellPaint(TableLayoutCellPaintEventArgs e) => CellPaint?.Invoke(this, e);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        var g = e.Graphics;
        int border = CellBorderWidth;
        var display = DisplayRectangle;
        int y = display.Y;
        for (int r = 0; r < _rowHeights.Length; r++)
        {
            int x = display.X;
            for (int c = 0; c < _columnWidths.Length; c++)
            {
                var cell = new Rectangle(x + border, y + border, _columnWidths[c], _rowHeights[r]);
                OnCellPaint(new TableLayoutCellPaintEventArgs(g, e.ClipRectangle, cell, c, r));
                x += _columnWidths[c] + border;
            }
            y += _rowHeights[r] + border;
        }

        if (_cellBorderStyle != TableLayoutPanelCellBorderStyle.None && _columnWidths.Length > 0)
        {
            using var dark = new Pen(SystemColors.ControlDark);
            using var light = new Pen(SystemColors.ControlLightLight);
            int totalW = border, totalH = border;
            foreach (var w in _columnWidths) totalW += w + border;
            foreach (var h in _rowHeights) totalH += h + border;
            int cx = display.X;
            for (int c = 0; c <= _columnWidths.Length; c++)
            {
                for (int b = 0; b < border; b++)
                {
                    g.DrawLine(b == 0 ? dark : light, cx + b, display.Y, cx + b, display.Y + totalH - 1);
                }
                if (c < _columnWidths.Length) cx += _columnWidths[c] + border;
            }
            int cy = display.Y;
            for (int r = 0; r <= _rowHeights.Length; r++)
            {
                for (int b = 0; b < border; b++)
                {
                    g.DrawLine(b == 0 ? dark : light, display.X, cy + b, display.X + totalW - 1, cy + b);
                }
                if (r < _rowHeights.Length) cy += _rowHeights[r] + border;
            }
        }
    }

    public class TableLayoutControlCollection : ControlCollection
    {
        private readonly TableLayoutPanel _owner;

        public TableLayoutControlCollection(TableLayoutPanel container) : base(container) => _owner = container;

        public TableLayoutPanel Container => _owner;

        public virtual void Add(Control control, int column, int row)
        {
            base.Add(control);
            _owner.SetCellPosition(control, new TableLayoutPanelCellPosition(column, row));
        }

        public override void Remove(Control? value)
        {
            base.Remove(value);
            if (value != null) _owner.ForgetControl(value);
        }
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [DefaultValue(BorderStyle.None)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new BorderStyle BorderStyle { get => base.BorderStyle; set => base.BorderStyle = value; }
}

/// <summary>The table engine: assign cells, size tracks, place children.</summary>
internal sealed class TableLayout : LayoutEngine
{
    public static readonly TableLayout Instance = new();

    private TableLayout() { }

    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var panel = (TableLayoutPanel)container;
        var (cols, rows) = MeasureTracks(panel, panel.Size, forPreferred: false);
        Place(panel, cols, rows);
        return false;
    }

    private sealed class Grid
    {
        public int Columns;
        public int Rows;
        public readonly Dictionary<Control, TableLayoutPanelCellPosition> Placed = new();
        public readonly HashSet<(int c, int r)> Occupied = new();
    }

    /// <summary>Assign every visible child a cell: explicit positions first, then flowing ones into the first free cell, growing the grid as GrowStyle allows.</summary>
    private static Grid AssignCells(TableLayoutPanel panel)
    {
        var grid = new Grid { Columns = Math.Max(1, panel.ColumnCount), Rows = Math.Max(1, panel.RowCount) };
        var children = new List<Control>();
        foreach (var c in panel.Controls) if (c.VisibleOwn) children.Add(c);

        void Occupy(Control control, int col, int row)
        {
            int cs = panel.GetColumnSpan(control), rs = panel.GetRowSpan(control);
            for (int r = row; r < row + rs; r++)
                for (int c = col; c < col + cs; c++)
                    grid.Occupied.Add((c, r));
            grid.Placed[control] = new TableLayoutPanelCellPosition(col, row);
            grid.Columns = Math.Max(grid.Columns, col + cs);
            grid.Rows = Math.Max(grid.Rows, row + rs);
        }

        // Explicitly positioned children.
        foreach (var child in children)
        {
            int col = panel.GetColumn(child), row = panel.GetRow(child);
            if (col >= 0 && row >= 0) Occupy(child, col, row);
        }

        // Flowing children take the first free cell in reading order.
        bool addRows = panel.GrowStyle != TableLayoutPanelGrowStyle.AddColumns;
        int fixedColumns = panel.ColumnCount > 0 ? panel.ColumnCount : (panel.GrowStyle == TableLayoutPanelGrowStyle.AddColumns ? 0 : 1);
        int fixedRows = panel.RowCount > 0 ? panel.RowCount : 1;
        int scanColumns = addRows ? Math.Max(fixedColumns, grid.Columns) : int.MaxValue;
        int scanRows = addRows ? int.MaxValue : Math.Max(fixedRows, grid.Rows);
        int cursorCol = 0, cursorRow = 0;
        foreach (var child in children)
        {
            if (grid.Placed.ContainsKey(child)) continue;
            int cs = panel.GetColumnSpan(child), rs = panel.GetRowSpan(child);
            int col = panel.GetColumn(child), row = panel.GetRow(child);
            bool found = false;
            if (addRows)
            {
                for (int r = cursorRow; r < scanRows && !found; r++)
                {
                    for (int c = r == cursorRow ? cursorCol : 0; c + cs <= scanColumns; c++)
                    {
                        if (row >= 0 && r != row) break;
                        if (col >= 0 && c != col) continue;
                        if (Free(grid, c, r, cs, rs))
                        {
                            Occupy(child, c, r);
                            cursorRow = r;
                            cursorCol = c + cs;
                            found = true;
                            break;
                        }
                    }
                    if (!found && panel.GrowStyle == TableLayoutPanelGrowStyle.FixedSize && r >= fixedRows - 1) break;
                }
            }
            else
            {
                for (int c = cursorCol; c < int.MaxValue && !found; c++)
                {
                    for (int r = c == cursorCol ? cursorRow : 0; r + rs <= scanRows; r++)
                    {
                        if (col >= 0 && c != col) break;
                        if (row >= 0 && r != row) continue;
                        if (Free(grid, c, r, cs, rs))
                        {
                            Occupy(child, c, r);
                            cursorCol = c;
                            cursorRow = r + rs;
                            found = true;
                            break;
                        }
                    }
                }
            }
            if (!found)
            {
                if (panel.GrowStyle == TableLayoutPanelGrowStyle.FixedSize)
                    throw new ArgumentException("The TableLayoutPanel is full and GrowStyle is FixedSize.");
                // Overflow (a span wider than the grid): put it at the start of a new row/column.
                if (addRows) Occupy(child, 0, grid.Rows);
                else Occupy(child, grid.Columns, 0);
            }
        }

        grid.Columns = Math.Max(grid.Columns, panel.ColumnCount);
        grid.Rows = Math.Max(grid.Rows, panel.RowCount);
        return grid;
    }

    internal static Dictionary<Control, TableLayoutPanelCellPosition> ComputePlacement(TableLayoutPanel panel) => AssignCells(panel).Placed;

    private static bool Free(Grid grid, int col, int row, int cs, int rs)
    {
        for (int r = row; r < row + rs; r++)
            for (int c = col; c < col + cs; c++)
                if (grid.Occupied.Contains((c, r))) return false;
        return true;
    }

    private static Size PreferredOf(Control c)
    {
        var s = c.AutoSize ? c.GetPreferredSize(Size.Empty) : c.Size;
        return new Size(s.Width + c.Margin.Horizontal, s.Height + c.Margin.Vertical);
    }

    /// <summary>Track sizes for the current grid. <paramref name="forPreferred"/> sizes percent tracks to their content instead of the panel.</summary>
    internal static (int[] columns, int[] rows) MeasureTracks(TableLayoutPanel panel, Size panelSize, bool forPreferred)
    {
        var grid = AssignCells(panel);
        int border = panel.CellBorderWidth;
        var pad = panel.Padding;
        int availableW = Math.Max(0, panelSize.Width - pad.Horizontal - border * (grid.Columns + 1) - 2 * panel.BorderSizeForLayout);
        int availableH = Math.Max(0, panelSize.Height - pad.Vertical - border * (grid.Rows + 1) - 2 * panel.BorderSizeForLayout);

        var cols = Resolve(grid.Columns, i => i < panel.ColumnStyles.Count ? panel.ColumnStyles[i] : null,
            i => ContentExtent(panel, grid, i, isColumn: true), availableW, forPreferred);
        var rows = Resolve(grid.Rows, i => i < panel.RowStyles.Count ? panel.RowStyles[i] : null,
            i => ContentExtent(panel, grid, i, isColumn: false), availableH, forPreferred);
        if (!forPreferred) panel.SetTrackSizes(cols, rows, grid.Placed);
        return (cols, rows);
    }

    /// <summary>The widest (tallest) single-span content of a track.</summary>
    private static int ContentExtent(TableLayoutPanel panel, Grid grid, int index, bool isColumn)
    {
        int extent = 0;
        foreach (var (control, pos) in grid.Placed)
        {
            int span = isColumn ? panel.GetColumnSpan(control) : panel.GetRowSpan(control);
            int start = isColumn ? pos.Column : pos.Row;
            if (span != 1 || start != index) continue;
            var pref = PreferredOf(control);
            extent = Math.Max(extent, isColumn ? pref.Width : pref.Height);
        }
        return extent;
    }

    private static int[] Resolve(int count, Func<int, TableLayoutStyle?> styleAt, Func<int, int> contentAt, int available, bool forPreferred)
    {
        var sizes = new int[count];
        var types = new SizeType[count];
        float percentSum = 0;
        int used = 0;
        int lastAuto = -1;
        for (int i = 0; i < count; i++)
        {
            var style = styleAt(i);
            types[i] = style?.SizeType ?? SizeType.AutoSize;
            switch (types[i])
            {
                case SizeType.Absolute:
                    sizes[i] = (int)Math.Round(style!.Size);
                    used += sizes[i];
                    break;
                case SizeType.Percent:
                    percentSum += style!.Size;
                    break;
                default:
                    sizes[i] = contentAt(i);
                    used += sizes[i];
                    lastAuto = i;
                    break;
            }
        }

        int remaining = Math.Max(0, available - used);
        if (percentSum > 0)
        {
            if (forPreferred)
            {
                for (int i = 0; i < count; i++) if (types[i] == SizeType.Percent) sizes[i] = contentAt(i);
            }
            else
            {
                int given = 0;
                int lastPercent = -1;
                for (int i = 0; i < count; i++)
                {
                    if (types[i] != SizeType.Percent) continue;
                    sizes[i] = (int)Math.Floor(remaining * styleAt(i)!.Size / percentSum);
                    given += sizes[i];
                    lastPercent = i;
                }
                if (lastPercent >= 0) sizes[lastPercent] += remaining - given; // rounding remainder
            }
        }
        else if (!forPreferred && lastAuto >= 0 && remaining > 0)
        {
            sizes[lastAuto] += remaining;
        }
        return sizes;
    }

    private static void Place(TableLayoutPanel panel, int[] cols, int[] rows)
    {
        int border = panel.CellBorderWidth;
        var display = panel.DisplayRectangle;
        var colStart = new int[cols.Length + 1];
        var rowStart = new int[rows.Length + 1];
        colStart[0] = display.X + border;
        for (int i = 0; i < cols.Length; i++) colStart[i + 1] = colStart[i] + cols[i] + border;
        rowStart[0] = display.Y + border;
        for (int i = 0; i < rows.Length; i++) rowStart[i + 1] = rowStart[i] + rows[i] + border;

        foreach (var child in panel.Controls)
        {
            var pos = panel.GetPositionFromControl(child);
            if (pos.Column < 0 || !child.VisibleOwn) continue;
            int cs = Math.Min(panel.GetColumnSpan(child), cols.Length - pos.Column);
            int rs = Math.Min(panel.GetRowSpan(child), rows.Length - pos.Row);
            if (cs <= 0 || rs <= 0) continue;
            int x = colStart[pos.Column];
            int y = rowStart[pos.Row];
            int w = colStart[pos.Column + cs] - x - border;
            int h = rowStart[pos.Row + rs] - y - border;
            var cell = new Rectangle(x, y, w, h);
            child.SetBoundsFromLayout(PlaceInCell(child, cell));
        }
    }

    /// <summary>A child's bounds inside its cell: its Margin, then Anchor/Dock (Top|Left default → top-left corner).</summary>
    internal static Rectangle PlaceInCell(Control child, Rectangle cell)
    {
        var m = child.Margin;
        var inner = new Rectangle(cell.X + m.Left, cell.Y + m.Top, Math.Max(0, cell.Width - m.Horizontal), Math.Max(0, cell.Height - m.Vertical));
        var size = child.AutoSize ? child.GetPreferredSize(inner.Size) : child.Size;
        var anchor = child.Anchor;
        var dock = child.Dock;

        bool left = (anchor & AnchorStyles.Left) != 0 || dock is DockStyle.Left or DockStyle.Fill or DockStyle.Top or DockStyle.Bottom;
        bool right = (anchor & AnchorStyles.Right) != 0 || dock is DockStyle.Right or DockStyle.Fill or DockStyle.Top or DockStyle.Bottom;
        bool top = (anchor & AnchorStyles.Top) != 0 || dock is DockStyle.Top or DockStyle.Fill or DockStyle.Left or DockStyle.Right;
        bool bottom = (anchor & AnchorStyles.Bottom) != 0 || dock is DockStyle.Bottom or DockStyle.Fill or DockStyle.Left or DockStyle.Right;
        if (dock != DockStyle.None)
        {
            // Dock.Top/Bottom/Left/Right keep the control's size on the docked axis.
            if (dock is DockStyle.Top or DockStyle.Bottom) { top = dock == DockStyle.Top; bottom = dock == DockStyle.Bottom; }
            if (dock is DockStyle.Left or DockStyle.Right) { left = dock == DockStyle.Left; right = dock == DockStyle.Right; }
        }

        int x, w, y, h;
        if (left && right) { x = inner.X; w = inner.Width; }
        else if (right) { w = Math.Min(size.Width, inner.Width); x = inner.Right - w; }
        else if (left) { w = Math.Min(size.Width, inner.Width); x = inner.X; }
        else { w = Math.Min(size.Width, inner.Width); x = inner.X + (inner.Width - w) / 2; }

        if (top && bottom) { y = inner.Y; h = inner.Height; }
        else if (bottom) { h = Math.Min(size.Height, inner.Height); y = inner.Bottom - h; }
        else if (top) { h = Math.Min(size.Height, inner.Height); y = inner.Y; }
        else { h = Math.Min(size.Height, inner.Height); y = inner.Y + (inner.Height - h) / 2; }

        return new Rectangle(x, y, w, h);
    }
}
