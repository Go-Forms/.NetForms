using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Decision 147: the row for new records as WinForms has it (a real row that Rows.Count counts, Rows.Add goes
/// above, typing into turns into an ordinary row), the first cell a shown grid makes current, image columns for
/// picture properties of a data source.
/// </summary>
public class DataGridViewNewRowTests
{
    private static (Form form, DataGridView grid) ShowGrid(Action<DataGridView>? configure = null)
    {
        TestPlatform.Install();
        var form = new Form { ClientSize = new Size(500, 300) };
        var grid = new DataGridView { Bounds = new Rectangle(0, 0, 500, 260) };
        configure?.Invoke(grid);
        form.Controls.Add(grid);
        form.Show();
        return (form, grid);
    }

    [Fact]
    public void AnUnboundGridHasARowForNewRecordsThatRowsAddGoesAbove()
    {
        var (form, grid) = ShowGrid();
        using (form)
        {
            Assert.Equal(0, grid.Rows.Count);
            grid.Columns.Add("Name", "Name");
            Assert.Equal(1, grid.Rows.Count);
            Assert.True(grid.Rows[0].IsNewRow);
            Assert.Equal(0, grid.NewRowIndex);

            Assert.Equal(0, grid.Rows.Add("Ada"));
            Assert.Equal(1, grid.Rows.Add("Alan"));
            Assert.Equal(3, grid.Rows.Count);
            Assert.Equal(2, grid.NewRowIndex);
            Assert.Equal("Ada", grid.Rows[0].Cells[0].Value);
            Assert.True(grid.Rows[2].IsNewRow);
            Assert.Throws<InvalidOperationException>(() => grid.Rows.RemoveAt(2));
            Assert.Throws<InvalidOperationException>(() => grid.Rows.Insert(3, "x"));

            // The loop every WinForms program has.
            var names = new List<string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.IsNewRow) names.Add((string)row.Cells[0].Value!);
            }
            Assert.Equal(new[] { "Ada", "Alan" }, names);

            grid.Sort(grid.Columns[0], ListSortDirection.Descending);
            Assert.Equal("Alan", grid.Rows[0].Cells[0].Value);
            Assert.True(grid.Rows[2].IsNewRow);

            grid.Rows.Clear();
            Assert.Equal(1, grid.Rows.Count);
            Assert.True(grid.Rows[0].IsNewRow);

            grid.AllowUserToAddRows = false;
            Assert.Equal(0, grid.Rows.Count);
            Assert.Equal(-1, grid.NewRowIndex);
            grid.AllowUserToAddRows = true;
            Assert.Equal(1, grid.Rows.Count);

            grid.Columns.Clear();
            Assert.Equal(0, grid.Rows.Count);
        }
    }

    [Fact]
    public void TypingIntoTheNewRowAddsARowBelowIt()
    {
        var (form, grid) = ShowGrid();
        using (form)
        {
            grid.Columns.Add("Name", "Name");
            grid.Columns.Add("City", "City");
            grid.Rows.Add("Ada", "London");
            var added = new List<int>();
            var defaults = new List<int>();
            grid.UserAddedRow += (_, e) => added.Add(e.Row.Index);
            grid.DefaultValuesNeeded += (_, e) =>
            {
                defaults.Add(e.Row.Index);
                e.Row.Cells["City"].Value = "Kazan";
            };
            grid.Focus();

            grid.CurrentCell = grid.Rows[1].Cells[0];
            Assert.Equal(new[] { 1 }, defaults);
            Assert.Equal("Kazan", grid.Rows[1].Cells[1].Value);

            grid.BeginEdit(true);
            grid.EditingControl!.Text = "Grace";
            Assert.Equal(new[] { 2 }, added);
            Assert.Equal(3, grid.Rows.Count);
            Assert.False(grid.Rows[1].IsNewRow);
            Assert.True(grid.Rows[2].IsNewRow);

            grid.EndEdit();
            Assert.Equal("Grace", grid.Rows[1].Cells[0].Value);
        }
    }

    [Fact]
    public void RowsAddOfOneNumberAddsThatManyRows()
    {
        // As in WinForms: Rows.Add(36) is Add(int count). A value needs Rows.Add((object)36).
        var (form, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            grid.Columns.Add("Age", "Age");
            grid.Rows.Add(3);
            Assert.Equal(3, grid.Rows.Count);
            grid.Rows.Add((object)36);
            Assert.Equal(36, grid.Rows[3].Cells[0].Value);
        }
    }

    [Fact]
    public void AShownGridMakesItsFirstRealCellCurrent()
    {
        var (form, grid) = ShowGrid();
        using (form)
        {
            grid.Columns.Add("Name", "Name");
            Assert.Null(grid.CurrentCell);   // only the row for new records
            grid.Rows.Add("Ada");
            Assert.Equal(new Point(0, 0), grid.CurrentCellAddress);
            Assert.True(grid.Rows[0].Cells[0].Selected);
        }

        // Rows added before the form is shown: the first cell becomes current when the grid is created.
        TestPlatform.Install();
        using var early = new Form();
        var hidden = new DataGridView();
        early.Controls.Add(hidden);
        hidden.Columns.Add("Name", "Name");
        hidden.Rows.Add("Ada");
        Assert.Null(hidden.CurrentCell);
        early.Show();
        Assert.Equal(new Point(0, 0), hidden.CurrentCellAddress);
    }

    [Fact]
    public void VirtualModeRaisesNewRowNeededOnEnteringTheNewRow()
    {
        var (form, grid) = ShowGrid(g => g.VirtualMode = true);
        using (form)
        {
            grid.Columns.Add("A", "A");
            grid.CellValueNeeded += (_, e) => e.Value = "r" + e.RowIndex;
            grid.RowCount = 5;
            Assert.Equal(5, grid.Rows.Count);
            Assert.Equal(4, grid.NewRowIndex);
            int needed = 0;
            grid.NewRowNeeded += (_, _) => needed++;
            grid.CurrentCell = grid.Rows[4].Cells[0];
            Assert.Equal(1, needed);
        }
    }

    [Fact]
    public void PicturePropertiesOfADataSourceGetImageColumns()
    {
        var table = new DataTable();
        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Photo", typeof(byte[]));
        using (var bitmap = new Bitmap(4, 4))
        using (var stream = new System.IO.MemoryStream())
        {
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            table.Rows.Add("Ada", stream.ToArray());
        }
        var (form, grid) = ShowGrid();
        using (form)
        {
            grid.DataSource = table;
            Assert.IsType<DataGridViewTextBoxColumn>(grid.Columns["Name"]);
            Assert.IsType<DataGridViewImageColumn>(grid.Columns["Photo"]);
            Assert.IsAssignableFrom<Image>(grid.Rows[0].Cells["Photo"].FormattedValue);
            Assert.Equal(-1, grid.NewRowIndex);
        }
    }
}
