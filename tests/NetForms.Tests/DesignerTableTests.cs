using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// A TableLayoutPanel on the design surface (decision 170): controls are placed by cell - a drop at the cell under
/// the mouse, a move onto a taken cell swaps, a full table grows - and columns and rows are edited as styles with
/// the controls going along. Everything a table edit touches is written as <c>Controls.Add(control, column, row)</c>.
/// </summary>
public sealed class DesignerTableTests
{
    public DesignerTableTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    /// <summary>HelloForms with an empty table (2 × 2, 50% each) named tableLayoutPanel1.</summary>
    private static DesignSurface WithTable()
    {
        var surface = HelloForms();
        surface.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.TableLayoutPanel", Parent = "", X = 10, Y = 150, W = 300, H = 120 } });
        return surface;
    }

    private static DesignerOp AddButton(int? column = null, int? row = null) =>
        new() { Op = "add", Type = "System.Windows.Forms.Button", Parent = "tableLayoutPanel1", Column = column, Row = row };

    private static TableLayoutPanel Table(DesignSurface s) => (TableLayoutPanel)s.Model.Find("tableLayoutPanel1")!.Instance;

    private static (int Column, int Row) CellOf(DesignSurface s, string name)
    {
        var p = Table(s).GetCellPosition((Control)s.Model.Find(name)!.Instance);
        return (p.Column, p.Row);
    }

    [Fact]
    public void ANewTableHasTwoColumnsAndTwoRowsOfFiftyPercent()
    {
        using var s = WithTable();
        Assert.Contains("tableLayoutPanel1.ColumnCount = 2;", s.Source);
        Assert.Contains("tableLayoutPanel1.RowCount = 2;", s.Source);
        Assert.Equal(2, CountOf(s.Source, "tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));"));
        Assert.Equal(2, CountOf(s.Source, "tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));"));
    }

    [Fact]
    public void ADropLandsInTheCellUnderTheMouse()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(1, 1) });
        Assert.Equal((1, 1), CellOf(s, "button2"));
        Assert.Contains("tableLayoutPanel1.Controls.Add(button2, 1, 1);", s.Source);

        // A taken cell: the first free one from there, then from the top.
        s.Apply(new[] { AddButton(1, 1) });
        Assert.Equal((0, 0), CellOf(s, "button3"));
        Assert.Equal((1, 1), CellOf(s, "button2"));
    }

    [Fact]
    public void AddedWithoutACellTheControlTakesTheFirstFreeOneAndTheOthersStay()
    {
        // Before, a new control went first in Controls and flowed into cell 0, 0, pushing the others along.
        using var s = WithTable();
        s.Apply(new[] { AddButton() });
        s.Apply(new[] { AddButton() });
        s.Apply(new[] { AddButton() });
        Assert.Equal((0, 0), CellOf(s, "button2"));
        Assert.Equal((1, 0), CellOf(s, "button3"));
        Assert.Equal((0, 1), CellOf(s, "button4"));
    }

    [Fact]
    public void MovingOntoAFreeCellMovesAndOntoATakenOneSwaps()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(0, 0), AddButton(1, 0) });
        s.Apply(new[] { new DesignerOp { Op = "setCell", Id = "button2", Column = 1, Row = 1 } });
        Assert.Equal((1, 1), CellOf(s, "button2"));
        Assert.Equal((1, 0), CellOf(s, "button3"));

        s.Apply(new[] { new DesignerOp { Op = "setCell", Id = "button2", Column = 1, Row = 0 } });
        Assert.Equal((1, 0), CellOf(s, "button2"));
        Assert.Equal((1, 1), CellOf(s, "button3"));
        Assert.Contains("tableLayoutPanel1.Controls.Add(button3, 1, 1);", s.Source);

        Assert.True(s.Undo());
        Assert.Equal((1, 1), CellOf(s, "button2"));
    }

    [Fact]
    public void AControlFromOutsideIsDroppedIntoACellAndCanLeaveAgain()
    {
        using var s = WithTable();
        s.Apply(new[] { new DesignerOp { Op = "setParent", Id = "button1", Parent = "tableLayoutPanel1", Column = 1, Row = 0 } });
        Assert.Equal((1, 0), CellOf(s, "button1"));
        Assert.Contains("tableLayoutPanel1.Controls.Add(button1, 1, 0);", s.Source);

        s.Apply(new[] { new DesignerOp { Op = "setParent", Id = "button1", Parent = "", X = 20, Y = 20 } });
        Assert.Same(s.Model.Root.Instance, ((Control)s.Model.Find("button1")!.Instance).Parent);
        Assert.DoesNotContain("tableLayoutPanel1.Controls.Add(button1", s.Source);
    }

    [Fact]
    public void AFullTableGrowsARowSizedAsTheLast()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(), AddButton(), AddButton(), AddButton(), AddButton() });
        Assert.Equal(3, Table(s).RowCount);
        Assert.Equal(3, Table(s).RowStyles.Count);
        Assert.Equal((0, 2), CellOf(s, "button6"));
        Assert.Contains("tableLayoutPanel1.RowCount = 3;", s.Source);
    }

    [Fact]
    public void AFullFixedSizeTableRefusesTheDrop()
    {
        using var s = WithTable();
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "tableLayoutPanel1", Prop = "GrowStyle", Value = "FixedSize" } });
        s.Apply(new[] { AddButton(), AddButton(), AddButton(), AddButton() });
        var source = s.Source;
        var ex = Assert.Throws<DesignerEditException>(() => s.Apply(new[] { AddButton() }));
        Assert.Contains("FixedSize", ex.Message);
        Assert.Equal(source, s.Source);
    }

    [Fact]
    public void AnInsertedColumnTakesTheControlsAlong()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(0, 0), AddButton(1, 0) });
        s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "ColumnStyles", Values = new[] { "Absolute 80", "Percent 50", "Percent 50" }, Ids = new[] { "", "#0", "#1" } } });
        Assert.Equal(3, Table(s).ColumnCount);
        Assert.Equal((1, 0), CellOf(s, "button2"));
        Assert.Equal((2, 0), CellOf(s, "button3"));
        Assert.Contains("tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));", s.Source);
        Assert.Contains("tableLayoutPanel1.ColumnCount = 3;", s.Source);
    }

    [Fact]
    public void ARemovedColumnKeepsItsControlsInFreeCells()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(0, 0), AddButton(1, 0) });
        s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "Columns", Values = new[] { "Percent 100" }, Ids = new[] { "#1" } } });
        Assert.Equal(1, Table(s).ColumnCount);
        Assert.Equal((0, 0), CellOf(s, "button3"));
        Assert.Equal((0, 1), CellOf(s, "button2"));
        Assert.NotNull(s.Model.Find("button2"));
    }

    [Fact]
    public void ReorderedRowsTakeTheirControlsAlongAndSizesChange()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(0, 0), AddButton(0, 1) });
        s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "RowStyles", Values = new[] { "Absolute 40", "AutoSize" }, Ids = new[] { "#1", "#0" } } });
        Assert.Equal((0, 1), CellOf(s, "button2"));
        Assert.Equal((0, 0), CellOf(s, "button3"));
        Assert.Contains("tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));", s.Source);
        Assert.Contains("tableLayoutPanel1.RowStyles.Add(new RowStyle());", s.Source);
    }

    [Fact]
    public void BadStylesAreRefused()
    {
        using var s = WithTable();
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "ColumnStyles", Values = new[] { "Wide 3" } } }));
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "ColumnStyles", Values = new[] { "Percent" } } }));
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "ColumnStyles", Values = Array.Empty<string>() } }));
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "button1", Prop = "ColumnStyles", Values = new[] { "AutoSize" } } }));
    }

    [Fact]
    public void ColumnCountInTheGridKeepsTheStylesInStep()
    {
        using var s = WithTable();
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "tableLayoutPanel1", Prop = "ColumnCount", Value = "3" } });
        Assert.Equal(3, Table(s).ColumnStyles.Count);
        Assert.Equal(3, CountOf(s.Source, "tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));"));
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "tableLayoutPanel1", Prop = "ColumnCount", Value = "1" } });
        Assert.Single(Table(s).ColumnStyles.Cast<ColumnStyle>());
    }

    [Fact]
    public void AWiderSpanPushesAsideWhatItCovers()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(0, 0), AddButton(1, 0) });
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "button2", Prop = "ColumnSpan", Value = "2" } });
        Assert.Equal(2, Table(s).GetColumnSpan((Control)s.Model.Find("button2")!.Instance));
        Assert.Equal((0, 1), CellOf(s, "button3"));
        Assert.Contains("tableLayoutPanel1.SetColumnSpan(button2, 2);", s.Source);

        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "button2", Prop = "ColumnSpan", Value = "3" } }));
        s.Apply(new[] { new DesignerOp { Op = "resetProp", Id = "button2", Prop = "ColumnSpan" } });
        Assert.DoesNotContain("SetColumnSpan(button2", s.Source);

        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "button3", Prop = "Row", Value = "0" } });
        Assert.Equal((0, 0), CellOf(s, "button3"));
        Assert.Equal((0, 1), CellOf(s, "button2"));
    }

    [Fact]
    public void TheViewHasTheGridAndEachControlsCell()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(1, 1) });
        var view = s.Render();
        var table = view.Items.Single(i => i.Id == "tableLayoutPanel1");
        Assert.NotNull(table.Table);
        Assert.Equal(2, table.Table!.Columns.Length);
        Assert.Equal(2, table.Table.Rows.Length);
        Assert.Equal(new[] { "Percent 50", "Percent 50" }, table.Table.ColumnStyles);
        Assert.Equal(table.ClientX, table.Table.X);
        var button = view.Items.Single(i => i.Id == "button2");
        Assert.Equal(new[] { 1, 1, 1, 1 }, button.Cell);
        // The button sits in its cell.
        Assert.True(button.X >= table.Table.X + table.Table.Columns[0]);
        Assert.True(button.Y >= table.Table.Y + table.Table.Rows[0]);
        Assert.Null(view.Items.Single(i => i.Id == "button1").Cell);
    }

    [Fact]
    public void TheGridShowsCellsOfAControlAndTheTablesColumnsAndRows()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(1, 0) });
        var rows = s.GetProperties("button2");
        Assert.Equal("1", rows.Single(r => r.Name == "Column").Value);
        Assert.Equal("0", rows.Single(r => r.Name == "Row").Value);
        Assert.Equal("1", rows.Single(r => r.Name == "ColumnSpan").Value);
        Assert.Equal("Layout", rows.Single(r => r.Name == "RowSpan").Category);
        Assert.DoesNotContain(s.GetProperties("button1"), r => r.Name == "ColumnSpan");

        var table = s.GetProperties("tableLayoutPanel1");
        var columns = table.Single(r => r.Name == "Columns");
        Assert.Equal("styles", columns.Editor);
        Assert.Equal(new[] { "Percent 50", "Percent 50" }, columns.Items);
        Assert.Equal("styles", table.Single(r => r.Name == "Rows").ItemsFormat);
    }

    [Fact]
    public void APastedCopyGoesBesideItsOriginal()
    {
        using var s = WithTable();
        s.Apply(new[] { AddButton(0, 0) });
        s.Apply(new[] { new DesignerOp { Op = "duplicate", Ids = new[] { "button2" } } });
        var copy = s.Render().Select!.Single();
        Assert.Equal((0, 0), CellOf(s, "button2"));
        Assert.Equal((1, 0), CellOf(s, copy));
    }

    [Fact]
    public void ATableWhoseControlsFlowIsPinnedAsItShowsWhenEdited()
    {
        // Gallery's table: its controls are not components (built inline), with cells in the file; one spans two columns.
        var path = Sample("Gallery/GalleryForm.Designer.cs");
        using var s = DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
        s.Apply(new[] { new DesignerOp { Op = "setStyles", Id = "tableLayoutPanel1", Prop = "RowStyles", Values = new[] { "Absolute 32", "Absolute 32", "Absolute 24" }, Ids = new[] { "#0", "#1", "" } } });
        Assert.Contains("tableLayoutPanel1.RowCount = 3;", s.Source);
        Assert.Contains("spans two columns", s.Source);
        var table = Table(s);
        Assert.Equal(5, table.Controls.Count);
        Assert.Equal(2, table.GetColumnSpan(table.GetControlFromPosition(1, 0)!));
    }

    private static int CountOf(string text, string what)
    {
        int n = 0;
        for (int i = text.IndexOf(what, StringComparison.Ordinal); i >= 0; i = text.IndexOf(what, i + what.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
