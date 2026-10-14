using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Columns on the design surface (decision 174): the headers of a DataGridView's and a ListView's columns are items of
/// the canvas, so a column is selected with a click and edited in the grid - and a DataGridView column's type is
/// changed there (ColumnType), as in VS's Edit Columns dialog. Before, only their captions could be edited, and
/// every new column was a text box column.
/// </summary>
public sealed class DesignerColumnTests
{
    public DesignerColumnTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    private static string ColumnNamed(DesignSurface s, string header) =>
        s.Model.Components.Single(c => c.Instance is DataGridViewColumn { HeaderText: var h } && h == header || c.Instance is ColumnHeader { Text: var t } && t == header).Name!;

    private static DesignSurface WithGrid()
    {
        var s = HelloForms();
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.DataGridView", Parent = "", X = 10, Y = 100, W = 300, H = 120 } });
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "dataGridView1", Prop = "Columns", Values = new[] { "Name", "Paid" }, Ids = new[] { "", "" } } });
        return s;
    }

    [Fact]
    public void AGridsColumnHeadersAreOnTheCanvas()
    {
        using var s = WithGrid();
        var view = s.Render();
        var grid = view.Items.Single(i => i.Id == "dataGridView1");
        var name = view.Items.Single(i => i.Id == ColumnNamed(s, "Name"));
        var paid = view.Items.Single(i => i.Id == ColumnNamed(s, "Paid"));
        Assert.Equal(("item", "dataGridView1"), (name.Kind, name.Parent));
        Assert.True(name.X >= grid.X && name.Y >= grid.Y && name.X + name.W <= paid.X);
        Assert.Equal(((DataGridView)s.Model.Find("dataGridView1")!.Instance).ColumnHeadersHeight, name.H);
        // After the grid, so a click on a header selects the column.
        Assert.True(view.Items.IndexOf(grid) < view.Items.IndexOf(name));
    }

    [Fact]
    public void AColumnIsEditedInTheGrid()
    {
        using var s = WithGrid();
        var name = ColumnNamed(s, "Name");
        var rows = s.GetProperties(name);
        Assert.Contains(rows, r => r.Name == "Width");
        Assert.Contains(rows, r => r.Name == "DataPropertyName");
        s.Apply(new[]
        {
            new DesignerOp { Op = "setProp", Id = name, Prop = "Width", Value = "150" },
            new DesignerOp { Op = "setProp", Id = name, Prop = "DataPropertyName", Value = "CustomerName" },
            new DesignerOp { Op = "setProp", Id = name, Prop = "ReadOnly", Value = "True" },
        });
        var source = s.Source.Replace("this.", "");
        Assert.Contains($"{name}.Width = 150;", source);
        Assert.Contains($"{name}.DataPropertyName = \"CustomerName\";", source);
        Assert.Contains($"{name}.ReadOnly = true;", source);
    }

    [Fact]
    public void AColumnsTypeIsChangedKeepingItsNamePlaceAndSettings()
    {
        using var s = WithGrid();
        var paid = ColumnNamed(s, "Paid");
        var row = s.GetProperties(paid).Single(r => r.Name == "ColumnType");
        Assert.Equal("DataGridViewTextBoxColumn", row.Value);
        Assert.Equal("enum", row.Editor);
        Assert.Contains("DataGridViewCheckBoxColumn", row.Options!);
        Assert.Contains("DataGridViewComboBoxColumn", row.Options!);

        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = paid, Prop = "Width", Value = "60" } });
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = paid, Prop = "ColumnType", Value = "DataGridViewCheckBoxColumn" } });
        var source = s.Source.Replace("this.", "");
        Assert.Contains($"{paid} = new DataGridViewCheckBoxColumn();", source);
        Assert.Contains($"{paid}.HeaderText = \"Paid\";", source);
        Assert.Contains($"{paid}.Width = 60;", source);
        var grid = (DataGridView)s.Model.Find("dataGridView1")!.Instance;
        Assert.IsType<DataGridViewCheckBoxColumn>(grid.Columns[1]);
        Assert.Equal(paid, grid.Columns[1].Name);
        Assert.Equal("DataGridViewCheckBoxColumn", s.GetProperties(paid).Single(r => r.Name == "ColumnType").Value);

        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setProp", Id = paid, Prop = "ColumnType", Value = "Button" } }));
        Assert.True(s.Undo());
        Assert.IsType<DataGridViewTextBoxColumn>(((DataGridView)s.Model.Find("dataGridView1")!.Instance).Columns[1]);
    }

    [Fact]
    public void ANewColumnIsNamedAndRenamedAsItsComponent()
    {
        using var s = WithGrid();
        var name = ColumnNamed(s, "Name");
        var grid = (DataGridView)s.Model.Find("dataGridView1")!.Instance;
        Assert.Equal(name, grid.Columns[0].Name);
        Assert.Contains($"{name}.Name = \"{name}\";", s.Source.Replace("this.", ""));
        Assert.Contains(s.GetProperties(name), r => r.Name == "Name" && r.Value == name);

        s.Apply(new[] { new DesignerOp { Op = "rename", Id = name, Value = "customerColumn" } });
        grid = (DataGridView)s.Model.Find("dataGridView1")!.Instance;
        Assert.Equal("customerColumn", grid.Columns[0].Name);
        Assert.Contains("customerColumn.Name = \"customerColumn\";", s.Source.Replace("this.", ""));
    }

    [Fact]
    public void AListViewsColumnsAreOnTheCanvasInDetailsView()
    {
        using var s = HelloForms();
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.ListView", Parent = "", X = 10, Y = 100, W = 250, H = 100 } });
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "listView1", Prop = "Columns", Values = new[] { "File", "Size" }, Ids = new[] { "", "" } } });
        var file = ColumnNamed(s, "File");
        Assert.DoesNotContain(s.Render().Items, i => i.Id == file); // LargeIcon: no header

        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "listView1", Prop = "View", Value = "Details" } });
        var view = s.Render();
        var header = view.Items.Single(i => i.Id == file);
        Assert.Equal(("item", "listView1"), (header.Kind, header.Parent));
        Assert.True(header.W > 0 && header.H > 0);
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = file, Prop = "Width", Value = "140" } });
        Assert.Contains($"{file}.Width = 140;", s.Source.Replace("this.", ""));
        Assert.Equal(140, s.Render().Items.Single(i => i.Id == file).W);
    }
}
