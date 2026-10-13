using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// TableLayoutPanel on the design surface, as VS's TableLayoutPanelDesigner: a control in a table is placed by
/// cell, not by coordinates. A drop goes to the cell under the mouse; a move onto a taken cell swaps the two
/// controls; columns and rows are edited as styles (Absolute / Percent / AutoSize) and inserted or removed with
/// what is in them moving along. Whatever the designer touches in a table is pinned: every control is written
/// as <c>Controls.Add(control, column, row)</c>, so the file says where each one is, as VS writes it.
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>A table's grid while an edit works on it: the tracks it has (at least ColumnCount/RowCount, and every one a control was laid out in).</summary>
    private sealed class TableGrid
    {
        public TableGrid(TableLayoutPanel table)
        {
            Table = table;
            table.PerformLayout();
            Columns = Math.Max(1, Math.Max(table.ColumnCount, table.GetColumnWidths().Length));
            Rows = Math.Max(1, Math.Max(table.RowCount, table.GetRowHeights().Length));
        }

        public TableLayoutPanel Table { get; }
        public int Columns { get; set; }
        public int Rows { get; set; }
    }

    /// <summary>
    /// Every control of the table at the cell it is shown in (a control that flows - no cell in the file - gets the
    /// one it flowed into), and the grid as it is. Taken before an edit, so the edit moves only what it means to.
    /// </summary>
    private static TableGrid PinCells(TableLayoutPanel table)
    {
        var grid = new TableGrid(table);
        var placed = table.Controls.Cast<Control>().Select(c => (Control: c, Cell: table.GetPositionFromControl(c))).ToList();
        foreach (var (control, cell) in placed)
            if (cell.Column >= 0 && cell.Row >= 0 && table.GetCellPosition(control) != cell) table.SetCellPosition(control, cell);
        return grid;
    }

    /// <summary>The cells the pinned controls of the table cover, but for <paramref name="skip"/>.</summary>
    private static HashSet<(int Column, int Row)> Occupied(TableLayoutPanel table, Func<Control, bool>? skip = null)
    {
        var cells = new HashSet<(int, int)>();
        foreach (Control c in table.Controls)
        {
            if (skip != null && skip(c)) continue;
            int column = table.GetColumn(c), row = table.GetRow(c);
            if (column < 0 || row < 0) continue;
            for (int r = row; r < row + table.GetRowSpan(c); r++)
                for (int k = column; k < column + table.GetColumnSpan(c); k++) cells.Add((k, r));
        }
        return cells;
    }

    private static bool Fits(TableGrid grid, HashSet<(int Column, int Row)> taken, int column, int row, int columnSpan, int rowSpan)
    {
        if (column < 0 || row < 0 || column + columnSpan > grid.Columns || row + rowSpan > grid.Rows) return false;
        for (int r = row; r < row + rowSpan; r++)
            for (int k = column; k < column + columnSpan; k++)
                if (taken.Contains((k, r))) return false;
        return true;
    }

    /// <summary>
    /// Seats a control of the table: at the cell asked for when it is free, else at the first free cell from there in
    /// reading order (then from the top), else in a column or row the table grows by, as its GrowStyle says. A span
    /// wider than the grid is cut to it.
    /// </summary>
    private static void Seat(TableGrid grid, Control control, int? column, int? row)
    {
        var table = grid.Table;
        int columnSpan = Math.Min(table.GetColumnSpan(control), grid.Columns);
        int rowSpan = Math.Min(table.GetRowSpan(control), grid.Rows);
        if (columnSpan != table.GetColumnSpan(control)) table.SetColumnSpan(control, columnSpan);
        if (rowSpan != table.GetRowSpan(control)) table.SetRowSpan(control, rowSpan);
        var taken = Occupied(table, c => ReferenceEquals(c, control));

        int startColumn = Math.Clamp(column ?? 0, 0, grid.Columns - columnSpan);
        int startRow = Math.Clamp(row ?? 0, 0, grid.Rows - rowSpan);
        int total = grid.Columns * grid.Rows;
        for (int i = 0; i < total; i++)
        {
            int at = (startRow * grid.Columns + startColumn + i) % total;
            int c = at % grid.Columns, r = at / grid.Columns;
            if (Fits(grid, taken, c, r, columnSpan, rowSpan))
            {
                table.SetCellPosition(control, new TableLayoutPanelCellPosition(c, r));
                return;
            }
        }

        switch (table.GrowStyle)
        {
            case TableLayoutPanelGrowStyle.FixedSize:
                throw new DesignerEditException($"{Name(table)} is full and its GrowStyle is FixedSize: add a row or a column to it first.");
            case TableLayoutPanelGrowStyle.AddColumns:
                AddTrack(grid, columns: true);
                table.SetCellPosition(control, new TableLayoutPanelCellPosition(grid.Columns - 1, 0));
                break;
            default:
                AddTrack(grid, columns: false);
                table.SetCellPosition(control, new TableLayoutPanelCellPosition(0, grid.Rows - 1));
                break;
        }
    }

    private static string Name(Control control) => string.IsNullOrEmpty(control.Name) ? control.GetType().Name : control.Name;

    /// <summary>One more column or row at the end, sized as the last one (Percent 50 next to Percent 50 shares evenly).</summary>
    private static void AddTrack(TableGrid grid, bool columns)
    {
        var table = grid.Table;
        int count = columns ? grid.Columns : grid.Rows;
        var styles = Styles(table, columns);
        var list = Enumerable.Range(0, count).Select(i => i < styles.Count ? styles[i] : (SizeType.AutoSize, 0f)).ToList();
        list.Add(list.Count > 0 ? list[^1] : (SizeType.AutoSize, 0f));
        WriteStyles(table, columns, list);
        if (columns) grid.Columns = list.Count; else grid.Rows = list.Count;
    }

    private static List<(SizeType Type, float Size)> Styles(TableLayoutPanel table, bool columns) => columns
        ? table.ColumnStyles.Cast<ColumnStyle>().Select(s => (s.SizeType, s.Width)).ToList()
        : table.RowStyles.Cast<RowStyle>().Select(s => (s.SizeType, s.Height)).ToList();

    /// <summary>The styles of the table's columns (rows) and their count, kept equal as the VS designer keeps them.</summary>
    private static void WriteStyles(TableLayoutPanel table, bool columns, List<(SizeType Type, float Size)> styles)
    {
        if (columns)
        {
            table.ColumnStyles.Clear();
            foreach (var (type, size) in styles) table.ColumnStyles.Add(new ColumnStyle(type, size));
            table.ColumnCount = styles.Count;
        }
        else
        {
            table.RowStyles.Clear();
            foreach (var (type, size) in styles) table.RowStyles.Add(new RowStyle(type, size));
            table.RowCount = styles.Count;
        }
    }

    /// <summary>A track's style as the designer client spells it: <c>Percent 50</c>, <c>Absolute 120</c>, <c>AutoSize</c>.</summary>
    internal static string StyleText(SizeType type, float size) =>
        type == SizeType.AutoSize ? "AutoSize" : type + " " + size.ToString("0.##", CultureInfo.InvariantCulture);

    private static (SizeType Type, float Size) ParseStyle(string text)
    {
        var parts = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Enum.TryParse<SizeType>(parts[0], ignoreCase: true, out var type) || !Enum.IsDefined(type))
            throw new DesignerEditException($"'{text}' is not a column or row style: write Absolute 100, Percent 50 or AutoSize.");
        float size = 0;
        if (parts.Length > 1 && (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out size) || size < 0 || float.IsNaN(size) || float.IsInfinity(size)))
            throw new DesignerEditException($"'{parts[1]}' is not a size for a column or row.");
        if (type != SizeType.AutoSize && parts.Length < 2)
            throw new DesignerEditException($"'{text}' needs a size: {type} 100.");
        return (type, type == SizeType.AutoSize ? 0 : size);
    }

    /// <summary>The styles of every track of the grid as it is laid out: a track without a style of its own is AutoSize.</summary>
    internal static List<string> TrackStyles(TableLayoutPanel table, bool columns, int count)
    {
        var styles = Styles(table, columns);
        return Enumerable.Range(0, count).Select(i => i < styles.Count ? StyleText(styles[i].Type, styles[i].Size) : "AutoSize").ToList();
    }

    // --- placing controls ---------------------------------------------------------------------------

    /// <summary>A control put into a table (dropped from the toolbox, moved in, pasted): at its cell, the table's other controls staying where they are.</summary>
    private static void PlaceInTable(TableLayoutPanel table, Control control, DesignerOp op, bool atTop)
    {
        // The cell is found before the control goes in: a control without one would flow, and a full table that
        // cannot grow refuses that.
        var grid = PinCells(table);
        Seat(grid, control, op.Column, op.Row);
        table.Controls.Add(control);
        if (atTop) table.Controls.SetChildIndex(control, 0);
        if (op.W != null || op.H != null) control.Size = new System.Drawing.Size(op.W ?? control.Width, op.H ?? control.Height);
    }

    /// <summary>
    /// Moves a control of a table to another cell (setCell), as a drag in VS does: onto a free cell it just goes; onto
    /// a cell another control holds, the two swap when that one fits where this one was; anything else in the way
    /// finds the first free cell from where it was.
    /// </summary>
    private static void MoveToCell(Control control, int column, int row)
    {
        var table = control.Parent as TableLayoutPanel ?? throw new DesignerEditException($"'{Name(control)}' is not in a TableLayoutPanel.");
        var grid = PinCells(table);
        int columnSpan = Math.Min(table.GetColumnSpan(control), grid.Columns);
        int rowSpan = Math.Min(table.GetRowSpan(control), grid.Rows);
        column = Math.Clamp(column, 0, grid.Columns - columnSpan);
        row = Math.Clamp(row, 0, grid.Rows - rowSpan);
        var from = table.GetCellPosition(control);
        if (from.Column == column && from.Row == row && columnSpan == table.GetColumnSpan(control) && rowSpan == table.GetRowSpan(control)) return;

        table.SetColumnSpan(control, columnSpan);
        table.SetRowSpan(control, rowSpan);
        table.SetCellPosition(control, new TableLayoutPanelCellPosition(column, row));
        var mine = Covers(table, control).ToHashSet();
        var inTheWay = table.Controls.Cast<Control>().Where(c => !ReferenceEquals(c, control) && Covers(table, c).Any(mine.Contains)).ToList();
        if (inTheWay.Count == 0) return;

        if (inTheWay.Count == 1 && from.Column >= 0)
        {
            var other = inTheWay[0];
            var taken = Occupied(table, c => ReferenceEquals(c, other));
            if (Fits(grid, taken, from.Column, from.Row, table.GetColumnSpan(other), table.GetRowSpan(other)))
            {
                table.SetCellPosition(other, from);
                return;
            }
        }
        Reseat(grid, inTheWay);
    }

    /// <summary>The cells a pinned control covers.</summary>
    private static IEnumerable<(int Column, int Row)> Covers(TableLayoutPanel table, Control c)
    {
        int column = table.GetColumn(c), row = table.GetRow(c);
        if (column < 0 || row < 0) yield break;
        for (int r = row; r < row + table.GetRowSpan(c); r++)
            for (int k = column; k < column + table.GetColumnSpan(c); k++) yield return (k, r);
    }

    /// <summary>Controls that lost their cell take the first free ones from where they were.</summary>
    private static void Reseat(TableGrid grid, IReadOnlyList<Control> controls)
    {
        var at = controls.Select(c => grid.Table.GetCellPosition(c)).ToList();
        foreach (var c in controls) grid.Table.SetCellPosition(c, new TableLayoutPanelCellPosition(-1, -1));
        for (int i = 0; i < controls.Count; i++) Seat(grid, controls[i], at[i].Column, at[i].Row);
    }

    /// <summary>Column, Row, ColumnSpan, RowSpan of a control in a table (the extender properties of the grid).</summary>
    private static bool IsCellProperty(object target, string name, out TableLayoutPanel table, out Control control)
    {
        table = null!;
        control = null!;
        if (target is not Control { Parent: TableLayoutPanel parent } c || name is not ("Column" or "Row" or "ColumnSpan" or "RowSpan")) return false;
        (table, control) = (parent, c);
        return true;
    }

    private static void SetCellProperty(TableLayoutPanel table, Control control, string name, string? value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            throw new DesignerEditException($"'{value}' is not a valid value for {name}.");
        var grid = PinCells(table);
        var cell = table.GetCellPosition(control);
        switch (name)
        {
            case "Column":
                if (n < 0) throw new DesignerEditException("Column cannot be negative.");
                MoveToCell(control, n, cell.Row);
                return;
            case "Row":
                if (n < 0) throw new DesignerEditException("Row cannot be negative.");
                MoveToCell(control, cell.Column, n);
                return;
        }

        bool columns = name == "ColumnSpan";
        int count = columns ? grid.Columns : grid.Rows;
        int start = columns ? cell.Column : cell.Row;
        if (n < 1) throw new DesignerEditException($"{name} must be at least 1.");
        if (start + n > count)
            throw new DesignerEditException($"{name} {n} does not fit: {Name(table)} has {count} {(columns ? "columns" : "rows")}, and {Name(control)} starts at {(columns ? "column" : "row")} {start}.");
        if (columns) table.SetColumnSpan(control, n); else table.SetRowSpan(control, n);
        // What the wider span now covers moves out of its way.
        var mine = Covers(table, control).ToHashSet();
        Reseat(grid, table.Controls.Cast<Control>().Where(c => !ReferenceEquals(c, control) && Covers(table, c).Any(mine.Contains)).ToList());
    }

    // --- columns and rows ---------------------------------------------------------------------------

    /// <summary>
    /// Replaces the columns (rows) of a table (setStyles): <paramref name="values"/> are the new tracks' styles in
    /// order, <paramref name="ids"/> the track each one was (<c>#2</c>) or "" for a new one; without ids the lines are
    /// the old tracks in order. Controls go with their tracks; a control whose tracks are all gone (or that a
    /// reorder put on top of another) takes the first free cell - a control is never deleted with its column.
    /// </summary>
    private static void SetTracks(TableLayoutPanel table, bool columns, string[] values, string?[]? ids)
    {
        if (values.Length == 0) throw new DesignerEditException($"A table needs at least one {(columns ? "column" : "row")}.");
        if (ids != null && ids.Length != values.Length) throw new DesignerEditException("The edit needs one id for each value.");
        var styles = values.Select(ParseStyle).ToList();
        var grid = PinCells(table);
        int old = columns ? grid.Columns : grid.Rows;

        // The new index of each old track (-1: removed).
        var map = Enumerable.Repeat(-1, old).ToArray();
        for (int i = 0; i < values.Length; i++)
        {
            int was = i;
            if (ids != null)
            {
                var id = ids[i];
                if (string.IsNullOrEmpty(id)) continue;
                if (id[0] != '#' || !int.TryParse(id.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out was) || was >= old)
                    throw new DesignerEditException($"'{id}' is not a {(columns ? "column" : "row")} of {Name(table)}.");
                if (map[was] >= 0) throw new DesignerEditException($"'{id}' is listed twice.");
            }
            if (was < old) map[was] = i;
        }

        var homeless = new List<Control>();
        foreach (Control c in table.Controls)
        {
            int start = columns ? table.GetColumn(c) : table.GetRow(c);
            int span = columns ? table.GetColumnSpan(c) : table.GetRowSpan(c);
            if (start < 0) { homeless.Add(c); continue; }
            var kept = Enumerable.Range(start, span).Where(k => k < old && map[k] >= 0).Select(k => map[k]).ToList();
            if (kept.Count == 0) { homeless.Add(c); continue; }
            int first = kept.Min(), last = kept.Max();
            if (columns)
            {
                table.SetColumn(c, first);
                table.SetColumnSpan(c, last - first + 1);
            }
            else
            {
                table.SetRow(c, first);
                table.SetRowSpan(c, last - first + 1);
            }
        }

        WriteStyles(table, columns, styles);
        if (columns) grid.Columns = styles.Count; else grid.Rows = styles.Count;

        // Two controls brought onto the same cells by a reorder: the first in the collection keeps them.
        var taken = new HashSet<(int, int)>();
        foreach (Control c in table.Controls)
        {
            if (homeless.Contains(c)) continue;
            var cells = Covers(table, c).ToList();
            bool inside = cells.All(t => t.Column < grid.Columns && t.Row < grid.Rows);
            if (!inside || cells.Any(taken.Contains)) { homeless.Add(c); continue; }
            foreach (var t in cells) taken.Add(t);
        }
        Reseat(grid, homeless);
    }

    /// <summary>ColumnCount / RowCount typed in the grid: tracks added at the end (sized as the last) or taken from it, as VS's designer keeps the styles in step.</summary>
    private static bool SetTrackCount(TableLayoutPanel table, string name, string? value)
    {
        if (name is not ("ColumnCount" or "RowCount")) return false;
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1) return false; // 0 (flow) and errors: the property itself
        bool columns = name == "ColumnCount";
        var grid = new TableGrid(table);
        int count = columns ? grid.Columns : grid.Rows;
        var styles = TrackStyles(table, columns, count);
        var values = Enumerable.Range(0, n).Select(i => i < count ? styles[i] : styles[^1]).ToArray();
        var ids = Enumerable.Range(0, n).Select(i => i < count ? "#" + i.ToString(CultureInfo.InvariantCulture) : "").ToArray();
        SetTracks(table, columns, values, ids);
        return true;
    }

    /// <summary>A new table, as TableLayoutPanelDesigner.InitializeNewComponent makes it: two columns and two rows of 50%.</summary>
    private static void InitializeTable(TableLayoutPanel table)
    {
        WriteStyles(table, columns: true, new List<(SizeType, float)> { (SizeType.Percent, 50), (SizeType.Percent, 50) });
        WriteStyles(table, columns: false, new List<(SizeType, float)> { (SizeType.Percent, 50), (SizeType.Percent, 50) });
    }

    private static TableLayoutPanel AsTable(object target) =>
        target as TableLayoutPanel ?? throw new DesignerEditException($"'{target.GetType().Name}' is not a TableLayoutPanel.");

    // --- what the client sees -------------------------------------------------------------------------

    /// <summary>The grid of a table on the canvas, for the client to draw its cells and hit-test drops.</summary>
    private static DesignerTable TableView(TableLayoutPanel table)
    {
        var origin = table.PointToForm(table.DisplayRectangle.Location);
        var columns = table.GetColumnWidths();
        var rows = table.GetRowHeights();
        return new DesignerTable
        {
            X = origin.X,
            Y = origin.Y,
            Border = table.CellBorderWidth,
            Columns = columns,
            Rows = rows,
            ColumnStyles = TrackStyles(table, true, columns.Length),
            RowStyles = TrackStyles(table, false, rows.Length),
        };
    }

    /// <summary>The Column/Row/ColumnSpan/RowSpan rows of the grid for a control in a table, and Columns/Rows for the table.</summary>
    private static void TableRows(object target, List<DesignerPropertyRow> rows)
    {
        if (target is Control { Parent: TableLayoutPanel table } control)
        {
            var cell = table.GetPositionFromControl(control);
            void Add(string name, int value, string description, bool modified) => rows.Add(new DesignerPropertyRow
            {
                Name = name, Category = "Layout", Description = description, Type = "Int32",
                Value = value.ToString(CultureInfo.InvariantCulture), Editor = "number", Modified = modified,
            });
            Add("Column", cell.Column, $"The column of {Name(table)} the control is in.", false);
            Add("Row", cell.Row, $"The row of {Name(table)} the control is in.", false);
            Add("ColumnSpan", table.GetColumnSpan(control), "The number of columns the control spans.", table.GetColumnSpan(control) != 1);
            Add("RowSpan", table.GetRowSpan(control), "The number of rows the control spans.", table.GetRowSpan(control) != 1);
        }
        if (target is TableLayoutPanel grid)
        {
            foreach (var columns in new[] { true, false })
            {
                int count = Math.Max(1, Math.Max(columns ? grid.ColumnCount : grid.RowCount, columns ? grid.GetColumnWidths().Length : grid.GetRowHeights().Length));
                var items = TrackStyles(grid, columns, count);
                rows.Add(new DesignerPropertyRow
                {
                    Name = columns ? "Columns" : "Rows",
                    Category = "Layout",
                    Description = columns
                        ? "The columns of the table and how each is sized: Absolute (pixels), Percent (a share of the space left) or AutoSize (fits its controls)."
                        : "The rows of the table and how each is sized: Absolute (pixels), Percent (a share of the space left) or AutoSize (fits its controls).",
                    Type = columns ? "TableLayoutColumnStyleCollection" : "TableLayoutRowStyleCollection",
                    Value = "(Collection)",
                    Editor = "styles",
                    Items = items,
                    ItemsFormat = "styles",
                    Modified = (columns ? grid.ColumnStyles.Count : grid.RowStyles.Count) > 0,
                });
            }
        }
    }
}

/// <summary>A TableLayoutPanel's grid on the canvas.</summary>
public sealed class DesignerTable
{
    /// <summary>The top-left of the grid (the table's display rectangle) on the canvas.</summary>
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>The width of a cell border (CellBorderStyle): before the first cell and after each.</summary>
    public int Border { get; set; }

    /// <summary>Column widths and row heights as laid out.</summary>
    public int[] Columns { get; set; } = Array.Empty<int>();
    public int[] Rows { get; set; } = Array.Empty<int>();

    /// <summary>Each column's (row's) style: <c>Percent 50</c>, <c>Absolute 120</c>, <c>AutoSize</c>.</summary>
    public List<string> ColumnStyles { get; set; } = new();
    public List<string> RowStyles { get; set; } = new();
}
