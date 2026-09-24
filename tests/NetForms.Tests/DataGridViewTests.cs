using System.Collections.Generic;
using System.ComponentModel;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Grid model, width policy, selection, editing, sorting and data binding.</summary>
public class DataGridViewTests
{
    public class Person
    {
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public bool Active { get; set; }
    }

    private static (TestPlatform platform, Form form, TestWindow window, DataGridView grid) ShowGrid(Action<DataGridView>? configure = null)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(500, 300) };
        var grid = new DataGridView { Bounds = new Rectangle(0, 0, 500, 300), BorderStyle = BorderStyle.None };
        configure?.Invoke(grid);
        form.Controls.Add(grid);
        form.Show();
        return (platform, form, platform.Windows.Last(), grid);
    }

    private static DataGridView WithColumns(DataGridView grid, params string[] names)
    {
        foreach (var name in names) grid.Columns.Add(name, name);
        return grid;
    }

    private static void Key(TestWindow w, Keys key, InputModifiers mods = InputModifiers.None)
    {
        w.Host.KeyDown((int)(key & Keys.KeyCode), mods);
        w.Host.KeyUp((int)(key & Keys.KeyCode), mods);
    }

    private static Point Center(Rectangle r) => new Point(r.X + r.Width / 2, r.Y + r.Height / 2);

    // --- model ------------------------------------------------------------------------------

    [Fact]
    public void AddingColumnsGivesEveryRowAMatchingCell()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Name", "Age");
            grid.Rows.Add("Ada", 36);
            grid.Rows.Add("Alan", 41);

            Assert.Equal(2, grid.Rows.Count);
            Assert.Equal(2, grid.Rows[0].Cells.Count);
            Assert.Equal("Ada", grid.Rows[0].Cells[0].Value);
            Assert.Equal(36, grid.Rows[0].Cells["Age"].Value);
            Assert.Equal(0, grid.Rows[0].Index);
            Assert.Equal(1, grid.Columns["Age"].Index);

            // A column added later reaches the existing rows too.
            grid.Columns.Add("Active", "Active");
            Assert.Equal(3, grid.Rows[0].Cells.Count);
            Assert.Same(grid.Columns[2], grid.Rows[0].Cells[2].OwningColumn);
            Assert.Same(grid.Rows[1], grid.Rows[1].Cells[0].OwningRow);
        }
    }

    [Fact]
    public void CellValueChangedFiresWhenAValueIsSet()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Name");
            grid.Rows.Add("Ada");

            var changes = new List<string>();
            grid.CellValueChanged += (_, e) => changes.Add($"{e.ColumnIndex},{e.RowIndex}");
            grid.Rows[0].Cells[0].Value = "Grace";

            Assert.Equal("Grace", grid.Rows[0].Cells[0].Value);
            Assert.Equal(new[] { "0,0" }, changes);
        }
    }

    [Fact]
    public void StylesCascadeFromTheGridThroughTheColumnToTheCell()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Name");
            grid.Rows.Add("Ada");
            grid.Rows.Add("Alan");

            grid.DefaultCellStyle.ForeColor = Color.Black;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.LightGray;
            grid.Columns[0].DefaultCellStyle.ForeColor = Color.Blue;
            grid.Rows[0].Cells[0].Style.ForeColor = Color.Red;

            Assert.Equal(Color.Red, grid.Rows[0].Cells[0].InheritedStyle.ForeColor);
            Assert.Equal(Color.Blue, grid.Rows[1].Cells[0].InheritedStyle.ForeColor);
            // The odd row picks up the alternating background.
            Assert.Equal(Color.LightGray, grid.Rows[1].Cells[0].InheritedStyle.BackColor);
        }
    }

    /// <summary>A cell that paints itself, as in Microsoft's samples: it draws with the cellStyle it is given.</summary>
    private sealed class FontRecordingCell : DataGridViewTextBoxCell
    {
        public static readonly List<Font?> Fonts = new();

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
            DataGridViewElementStates cellState, object? value, object? formattedValue, string? errorText,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            Fonts.Add(cellStyle.Font);
            using var brush = new SolidBrush(cellStyle.ForeColor);
            graphics.DrawString(formattedValue as string ?? "", cellStyle.Font, brush, cellBounds.Location);
        }
    }

    [Fact]
    public void ACustomCellPaintsWithTheGridsFont()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            grid.Columns.Add(new DataGridViewColumn(new FontRecordingCell()) { Name = "Name" });
            grid.Rows.Add("Ada");
            FontRecordingCell.Fonts.Clear();
            RenderOnce(grid)?.Dispose();

            Assert.NotEmpty(FontRecordingCell.Fonts);
            Assert.All(FontRecordingCell.Fonts, f => Assert.Same(grid.Font, f));
            // WinForms: the default styles carry the grid's font, so code like new Font(DefaultCellStyle.Font, Bold) works.
            Assert.Same(grid.Font, grid.DefaultCellStyle.Font);
            Assert.Same(grid.Font, grid.ColumnHeadersDefaultCellStyle.Font);
            Assert.Same(grid.Font, grid.RowHeadersDefaultCellStyle.Font);
            Assert.Same(grid.Font, grid.Rows[0].Cells[0].InheritedStyle.Font);
        }
    }

    [Fact]
    public void TheDefaultStylesFollowTheGridsFontUntilTheyHaveTheirOwn()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            // Ambient: the form's font reaches the grid and its default styles.
            var formFont = new Font("Arial", 12);
            form.Font = formFont;
            Assert.Same(formFont, grid.Font);
            Assert.Same(formFont, grid.DefaultCellStyle.Font);
            Assert.Same(formFont, grid.ColumnHeadersDefaultCellStyle.Font);

            // A font of the style's own stays when the grid's changes.
            var header = new Font("Arial", 16, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Font = header;
            var gridFont = new Font("Arial", 10);
            grid.Font = gridFont;
            Assert.Same(gridFont, grid.DefaultCellStyle.Font);
            Assert.Same(header, grid.ColumnHeadersDefaultCellStyle.Font);
            Assert.Same(gridFont, grid.RowHeadersDefaultCellStyle.Font);

            // A style assigned without a font gets the grid's (WinForms fills it in the getter); null restores the default.
            grid.DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.Yellow };
            Assert.Same(gridFont, grid.DefaultCellStyle.Font);
            Assert.Equal(Color.Yellow, grid.DefaultCellStyle.BackColor);
            Assert.Equal(DataGridViewContentAlignment.MiddleLeft, grid.DefaultCellStyle.Alignment);
            grid.DefaultCellStyle = null;
            Assert.Equal(SystemColors.Window, grid.DefaultCellStyle.BackColor);
            Assert.Same(gridFont, grid.DefaultCellStyle.Font);
        }
    }

    // --- width policy ------------------------------------------------------------------------

    [Fact]
    public void FillModeSharesTheWidthByFillWeight()
    {
        var (_, form, _, grid) = ShowGrid(g =>
        {
            g.AllowUserToAddRows = false;
            g.RowHeadersVisible = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        });
        using (form)
        {
            WithColumns(grid, "A", "B");
            grid.Columns[0].FillWeight = 100;
            grid.Columns[1].FillWeight = 300;
            grid.Rows.Add("x", "y");
            grid.PerformLayout();
            using var _bmp = grid.FindForm()!.Controls.Count > 0 ? RenderOnce(grid) : null;

            // 500 wide, no row headers: a quarter and three quarters.
            Assert.Equal(500, grid.Columns[0].Width + grid.Columns[1].Width);
            Assert.InRange(grid.Columns[0].Width, 120, 130);
            Assert.InRange(grid.Columns[1].Width, 370, 380);
        }
    }

    [Fact]
    public void AllCellsModeWidensAColumnToItsWidestValue()
    {
        var (_, form, _, grid) = ShowGrid(g =>
        {
            g.AllowUserToAddRows = false;
            g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        });
        using (form)
        {
            WithColumns(grid, "Name");
            grid.Rows.Add("x");
            int narrow = MeasureWidth(grid);

            grid.Rows.Add("a considerably longer value");
            int wide = MeasureWidth(grid);
            Assert.True(wide > narrow + 50, $"expected the column to grow, {narrow} → {wide}");
        }
    }

    private static int MeasureWidth(DataGridView grid)
    {
        RenderOnce(grid)?.Dispose();
        return grid.Columns[0].Width;
    }

    private static Bitmap? RenderOnce(DataGridView grid)
    {
        var form = grid.FindForm();
        if (form == null) return null;
        var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.ClientSize));
        return bmp;
    }

    // --- geometry and hit-testing ----------------------------------------------------------------

    [Fact]
    public void CellsAreLaidOutRightOfTheRowHeadersAndBelowTheColumnHeaders()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "A", "B");
            grid.Rows.Add("1", "2");
            grid.Rows.Add("3", "4");

            var first = grid.GetCellDisplayRectangle(0, 0, false);
            Assert.Equal(grid.RowHeadersWidth, first.Left);
            Assert.Equal(grid.ColumnHeadersHeight, first.Top);
            Assert.Equal(grid.Columns[0].Width, first.Width);
            Assert.Equal(grid.Rows[0].Height, first.Height);

            Assert.Equal(first.Right, grid.GetCellDisplayRectangle(1, 0, false).Left);
            Assert.Equal(first.Bottom, grid.GetCellDisplayRectangle(0, 1, false).Top);

            var hit = grid.HitTest(Center(first).X, Center(first).Y);
            Assert.Equal(DataGridViewHitTestType.Cell, hit.Type);
            Assert.Equal(0, hit.ColumnIndex);
            Assert.Equal(0, hit.RowIndex);

            Assert.Equal(DataGridViewHitTestType.ColumnHeader, grid.HitTest(Center(first).X, 5).Type);
            Assert.Equal(DataGridViewHitTestType.RowHeader, grid.HitTest(5, Center(first).Y).Type);
            Assert.Equal(DataGridViewHitTestType.TopLeftHeader, grid.HitTest(5, 5).Type);
        }
    }

    // --- selection ---------------------------------------------------------------------------------

    [Fact]
    public void ClickingACellSelectsItAndMakesItCurrent()
    {
        var (_, form, window, grid) = ShowGrid(g =>
        {
            g.AllowUserToAddRows = false;
            g.SelectionMode = DataGridViewSelectionMode.CellSelect;
        });
        using (form)
        {
            WithColumns(grid, "A", "B");
            grid.Rows.Add("1", "2");
            grid.Rows.Add("3", "4");

            int selectionChanges = 0;
            grid.SelectionChanged += (_, _) => selectionChanges++;

            window.Click(Center(grid.GetCellDisplayRectangle(1, 1, false)));
            Assert.Same(grid.Rows[1].Cells[1], grid.CurrentCell);
            Assert.Equal(new Point(1, 1), grid.CurrentCellAddress);
            Assert.Equal(1, grid.SelectedCells.Count);
            Assert.True(selectionChanges > 0);

            // Ctrl extends, Shift covers the block.
            var target = Center(grid.GetCellDisplayRectangle(0, 0, false));
            window.Host.MouseDown(MouseButton.Left, target, 1, InputModifiers.Shift);
            window.Host.MouseUp(MouseButton.Left, target, InputModifiers.Shift);
            Assert.Equal(4, grid.SelectedCells.Count);
        }
    }

    [Fact]
    public void FullRowSelectPicksTheWholeRow()
    {
        var (_, form, window, grid) = ShowGrid(g =>
        {
            g.AllowUserToAddRows = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        });
        using (form)
        {
            WithColumns(grid, "A", "B", "C");
            grid.Rows.Add("1", "2", "3");
            grid.Rows.Add("4", "5", "6");

            window.Click(Center(grid.GetCellDisplayRectangle(1, 1, false)));
            Assert.Equal(1, grid.SelectedRows.Count);
            Assert.Same(grid.Rows[1], grid.SelectedRows[0]);
            Assert.Equal(3, grid.SelectedCells.Count);
            Assert.True(grid.Rows[1].Selected);
            Assert.False(grid.Rows[0].Selected);
        }
    }

    [Fact]
    public void ArrowKeysMoveTheCurrentCellAndCtrlAPicksEverything()
    {
        var (_, form, window, grid) = ShowGrid(g =>
        {
            g.AllowUserToAddRows = false;
            g.SelectionMode = DataGridViewSelectionMode.CellSelect;
        });
        using (form)
        {
            WithColumns(grid, "A", "B");
            grid.Rows.Add("1", "2");
            grid.Rows.Add("3", "4");
            window.Click(Center(grid.GetCellDisplayRectangle(0, 0, false)));

            Key(window, Keys.Right);
            Assert.Equal(new Point(1, 0), grid.CurrentCellAddress);
            Key(window, Keys.Down);
            Assert.Equal(new Point(1, 1), grid.CurrentCellAddress);
            Key(window, Keys.Home);
            Assert.Equal(new Point(0, 1), grid.CurrentCellAddress);

            window.Host.KeyDown((int)Keys.A, InputModifiers.Control);
            Assert.Equal(4, grid.SelectedCells.Count);
        }
    }

    // --- cell kinds -----------------------------------------------------------------------------------

    [Fact]
    public void ClickingACheckBoxCellTogglesItWithoutOpeningAnEditor()
    {
        var (_, form, window, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Active", HeaderText = "Active" });
            grid.Rows.Add(false);

            int contentClicks = 0;
            grid.CellContentClick += (_, _) => contentClicks++;

            var bounds = grid.GetCellDisplayRectangle(0, 0, false);
            window.Click(Center(bounds));

            Assert.Equal(1, contentClicks);
            Assert.Equal(true, grid.Rows[0].Cells[0].Value);
            Assert.False(grid.IsCurrentCellInEditMode);

            window.Click(Center(bounds));
            Assert.Equal(false, grid.Rows[0].Cells[0].Value);
        }
    }

    [Fact]
    public void ButtonAndLinkCellsRaiseContentClick()
    {
        var (_, form, window, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Go", HeaderText = "Go", Text = "Go", UseColumnTextForButtonValue = true });
            grid.Columns.Add(new DataGridViewLinkColumn { Name = "Link", HeaderText = "Link" });
            grid.Rows.Add(null, "open");

            var clicks = new List<int>();
            grid.CellContentClick += (_, e) => clicks.Add(e.ColumnIndex);

            window.Click(Center(grid.GetCellDisplayRectangle(0, 0, false)));
            window.Click(Center(grid.GetCellDisplayRectangle(1, 0, false)));

            Assert.Equal(new[] { 0, 1 }, clicks);
            Assert.Equal("Go", grid.Rows[0].Cells[0].FormattedValue);
            Assert.True(((DataGridViewLinkCell)grid.Rows[0].Cells[1]).LinkVisited);
        }
    }

    [Fact]
    public void CellsFormatTheirValuesThroughTheStyleAndTheEvent()
    {
        var (_, form, _, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Price");
            grid.Columns[0].ValueType = typeof(decimal);
            grid.Columns[0].DefaultCellStyle.Format = "0.00";
            // Pinned to the invariant culture so the test does not depend on the machine's separator.
            grid.Columns[0].DefaultCellStyle.FormatProvider = System.Globalization.CultureInfo.InvariantCulture;
            grid.Rows.Add(12.5m);

            Assert.Equal("12.50", grid.Rows[0].Cells[0].FormattedValue);

            grid.Rows[0].Cells[0].Value = null;
            grid.Rows[0].Cells[0].Style.NullValue = "-";
            Assert.Equal("-", grid.Rows[0].Cells[0].FormattedValue);
        }
    }

    // --- editing ----------------------------------------------------------------------------------------

    [Fact]
    public void EditingACellCommitsTheParsedValue()
    {
        var (_, form, window, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Name", "Age");
            grid.Columns[1].ValueType = typeof(int);
            grid.Rows.Add("Ada", 36);

            window.Click(Center(grid.GetCellDisplayRectangle(1, 0, false)));
            Assert.True(grid.BeginEdit(true));
            Assert.True(grid.IsCurrentCellInEditMode);

            var editor = (TextBox)grid.Controls[0];
            editor.Text = "41";
            grid.EndEdit();

            Assert.False(grid.IsCurrentCellInEditMode);
            Assert.Equal(41, grid.Rows[0].Cells[1].Value);
        }
    }

    [Fact]
    public void ReadOnlyCellsCannotBeEdited()
    {
        var (_, form, window, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Name");
            grid.Rows.Add("Ada");
            grid.Columns[0].ReadOnly = true;

            window.Click(Center(grid.GetCellDisplayRectangle(0, 0, false)));
            Assert.False(grid.BeginEdit(true));
            Assert.True(grid.Rows[0].Cells[0].ReadOnly);
        }
    }

    // --- sorting ------------------------------------------------------------------------------------------

    [Fact]
    public void ClickingAHeaderSortsAndFlipsTheDirection()
    {
        var (_, form, window, grid) = ShowGrid(g => g.AllowUserToAddRows = false);
        using (form)
        {
            WithColumns(grid, "Name");
            grid.Rows.Add("Charlie");
            grid.Rows.Add("alpha");
            grid.Rows.Add("Bravo");

            var header = new Point(Center(grid.GetCellDisplayRectangle(0, 0, false)).X, grid.ColumnHeadersHeight / 2);
            window.Click(header);

            Assert.Same(grid.Columns[0], grid.SortedColumn);
            Assert.Equal(SortOrder.Ascending, grid.SortOrder);
            Assert.Equal(new[] { "alpha", "Bravo", "Charlie" }, Values(grid));

            window.Click(header);
            Assert.Equal(SortOrder.Descending, grid.SortOrder);
            Assert.Equal(new[] { "Charlie", "Bravo", "alpha" }, Values(grid));
        }
    }

    private static string[] Values(DataGridView grid)
    {
        var result = new List<string>();
        foreach (DataGridViewRow row in grid.Rows) result.Add(row.Cells[0].Value?.ToString() ?? string.Empty);
        return result.ToArray();
    }

    // --- data binding ---------------------------------------------------------------------------------------

    [Fact]
    public void BindingToAListGeneratesColumnsAndReadsThroughToTheObjects()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            var people = new BindingList<Person>
            {
                new Person { Name = "Ada", Age = 36, Active = true },
                new Person { Name = "Alan", Age = 41 },
            };
            grid.DataSource = people;

            Assert.Equal(3, grid.Columns.Count);
            Assert.Equal("Name", grid.Columns[0].HeaderText);
            Assert.IsType<DataGridViewCheckBoxColumn>(grid.Columns[2]);
            Assert.Equal(2, grid.Rows.Count);
            Assert.Equal("Ada", grid.Rows[0].Cells[0].Value);
            Assert.Equal(41, grid.Rows[1].Cells[1].Value);

            // The cell is a window onto the object, not a copy of it.
            people[0].Name = "Grace";
            Assert.Equal("Grace", grid.Rows[0].Cells[0].Value);

            // ...and writing through the cell reaches the object.
            grid.Rows[1].Cells[1].Value = 42;
            Assert.Equal(42, people[1].Age);

            // The list's own notifications rebuild the rows.
            people.Add(new Person { Name = "Edsger", Age = 32 });
            Assert.Equal(3, grid.Rows.Count);
            Assert.Equal("Edsger", grid.Rows[2].Cells[0].Value);
        }
    }

    [Fact]
    public void BindingSourceTracksThePositionAndForwardsListChanges()
    {
        var people = new BindingList<Person>
        {
            new Person { Name = "Ada" },
            new Person { Name = "Alan" },
        };
        using var source = new BindingSource { DataSource = people };

        Assert.Equal(2, source.Count);
        Assert.Equal(0, source.Position);
        Assert.Same(people[0], source.Current);

        var positions = new List<int>();
        source.PositionChanged += (_, _) => positions.Add(source.Position);
        source.MoveNext();
        Assert.Same(people[1], source.Current);
        source.MoveLast();
        source.MoveFirst();
        Assert.Equal(new[] { 1, 0 }, positions);

        int listChanges = 0;
        source.ListChanged += (_, _) => listChanges++;
        people.Add(new Person { Name = "Grace" });
        Assert.True(listChanges > 0);
        Assert.Equal(3, source.Count);

        // A BindingSource over a bare type gives an empty typed list you can add to.
        using var typed = new BindingSource { DataSource = typeof(Person) };
        Assert.Equal(0, typed.Count);
        var added = (Person)typed.AddNew();
        added.Name = "New";
        Assert.Equal(1, typed.Count);
    }

    [Fact]
    public void AGridBoundThroughABindingSourceSeesTheSameItems()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            var people = new BindingList<Person> { new Person { Name = "Ada", Age = 36 } };
            using var source = new BindingSource { DataSource = people };
            grid.DataSource = source;

            Assert.Equal(1, grid.Rows.Count);
            Assert.Equal("Ada", grid.Rows[0].Cells["Name"].Value);

            people.Add(new Person { Name = "Alan" });
            Assert.Equal(2, grid.Rows.Count);

            // Rows cannot be added by hand while the grid is bound.
            Assert.Throws<InvalidOperationException>(() => grid.Rows.Add(new DataGridViewRow()));
        }
    }

    [Fact]
    public void ControlDataBindingsMoveValuesBothWays()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            var people = new BindingList<Person> { new Person { Name = "Ada" } };
            using var source = new BindingSource { DataSource = people };
            var box = new TextBox();
            form.Controls.Add(box);
            box.DataBindings.Add("Text", source, "Name");

            Assert.Equal("Ada", box.Text);

            box.Text = "Grace";
            Assert.Equal("Grace", people[0].Name);

            people.Add(new Person { Name = "Alan" });
            source.MoveNext();
            Assert.Equal("Alan", box.Text);
        }
    }

    // --- painting ----------------------------------------------------------------------------------------------

    [Fact]
    public void TheGridPaintsHeadersCellsAndSelection()
    {
        var (_, form, window, grid) = ShowGrid(g =>
        {
            g.AllowUserToAddRows = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        });
        using (form)
        {
            grid.Columns.Add("Name", "Name");
            grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Active", HeaderText = "Active" });
            grid.Columns.Add(new DataGridViewButtonColumn { Name = "Go", HeaderText = "Go", Text = "Go", UseColumnTextForButtonValue = true });
            for (int i = 0; i < 20; i++) grid.Rows.Add("Row " + i, i % 2 == 0, null);

            grid.Rows[2].Selected = true;
            grid.CurrentCell = grid.Rows[2].Cells[0];
            grid.Sort(grid.Columns[0], ListSortDirection.Ascending);

            using var bitmap = window.Paint();
            Assert.Equal(500, bitmap.Width);

            var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(outDir);
            bitmap.Save(Path.Combine(outDir, "datagridview.png"));
        }
    }
}
