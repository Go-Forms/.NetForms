using System.ComponentModel;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>The property browser: rows from TypeDescriptor, categories, editing and the help pane.</summary>
public class PropertyGridTests
{
    public enum Flavour { Plain, Salty, Sweet }

    public class Sample
    {
        [Category("Appearance")]
        [Description("The caption shown to the user.")]
        public string Title { get; set; } = "Hello";

        [Category("Appearance")]
        public Flavour Flavour { get; set; } = Flavour.Plain;

        [Category("Behaviour")]
        public bool Enabled { get; set; } = true;

        [Category("Behaviour")]
        public int Count { get; set; } = 3;

        [Category("Layout")]
        public Size Size { get; set; } = new Size(10, 20);

        [Category("Behaviour")]
        public string ReadOnlyTag { get; } = "fixed";

        [Browsable(false)]
        public string Hidden { get; set; } = "invisible";
    }

    private static (TestPlatform platform, Form form, TestWindow window, PropertyGrid grid) ShowGrid(Action<PropertyGrid>? configure = null)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(320, 400) };
        var grid = new PropertyGrid { Bounds = new Rectangle(0, 0, 320, 400) };
        configure?.Invoke(grid);
        form.Controls.Add(grid);
        form.Show();
        return (platform, form, platform.Windows.Last(), grid);
    }

    private static GridItem? Find(PropertyGrid grid, string label)
    {
        return Walk(grid.RootGridItem?.GridItems);

        GridItem? Walk(GridItemCollection? items)
        {
            if (items == null) return null;
            foreach (GridItem item in items)
            {
                if (item.Label == label) return item;
                var found = Walk(item.GridItems);
                if (found != null) return found;
            }
            return null;
        }
    }

    // --- the item tree -----------------------------------------------------------------------

    [Fact]
    public void PropertiesAreGroupedIntoCategoriesAndHiddenOnesAreSkipped()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            grid.SelectedObject = new Sample();

            var root = grid.RootGridItem!;
            var categories = new List<string>();
            foreach (GridItem item in root.GridItems)
            {
                Assert.Equal(GridItemType.Category, item.GridItemType);
                categories.Add(item.Label!);
            }
            Assert.Equal(new[] { "Appearance", "Behaviour", "Layout" }, categories);

            // Browsable(false) never shows up.
            Assert.Null(Find(grid, "Hidden"));
            Assert.NotNull(Find(grid, "Title"));
            Assert.Equal(GridItemType.Property, Find(grid, "Title")!.GridItemType);

            // Inside a category the properties are sorted by name.
            var behaviour = root.GridItems["Behaviour"]!;
            var names = new List<string>();
            foreach (GridItem item in behaviour.GridItems) names.Add(item.Label!);
            Assert.Equal(new[] { "Count", "Enabled", "ReadOnlyTag" }, names);
        }
    }

    [Fact]
    public void AlphabeticalSortDropsTheCategories()
    {
        var (_, form, _, grid) = ShowGrid(g => g.PropertySort = PropertySort.Alphabetical);
        using (form)
        {
            grid.SelectedObject = new Sample();

            var names = new List<string>();
            foreach (GridItem item in grid.RootGridItem!.GridItems)
            {
                Assert.Equal(GridItemType.Property, item.GridItemType);
                names.Add(item.Label!);
            }
            Assert.Equal(new[] { "Count", "Enabled", "Flavour", "ReadOnlyTag", "Size", "Title" }, names);
        }
    }

    [Fact]
    public void AValueWithSubPropertiesCanBeExpanded()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            grid.SelectedObject = new Sample();
            var size = Find(grid, "Size")!;

            Assert.True(size.Expandable);
            var children = new List<string>();
            foreach (GridItem item in size.GridItems) children.Add(item.Label!);
            Assert.Contains("Width", children);
            Assert.Contains("Height", children);

            // A plain string has nothing to expand.
            Assert.False(Find(grid, "Title")!.Expandable);
        }
    }

    [Fact]
    public void ValuesAreReadThroughTheTypeConverter()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            var sample = new Sample { Title = "Caption", Count = 7, Size = new Size(30, 40) };
            grid.SelectedObject = sample;

            Assert.Equal("Caption", Find(grid, "Title")!.Value);
            Assert.Equal(7, Find(grid, "Count")!.Value);
            Assert.Equal(new Size(30, 40), Find(grid, "Size")!.Value);
            Assert.Equal(Flavour.Plain, Find(grid, "Flavour")!.Value);
        }
    }

    // --- editing ------------------------------------------------------------------------------

    [Fact]
    public void EditingARowWritesThroughToTheObject()
    {
        var (_, form, window, grid) = ShowGrid();
        using (form)
        {
            var sample = new Sample();
            grid.SelectedObject = sample;

            var changes = new List<string>();
            grid.PropertyValueChanged += (_, e) => changes.Add(e.ChangedItem!.Label + ":" + e.OldValue);

            // Click the value cell of "Title" to open the editor.
            var title = Find(grid, "Title")!;
            title.Select();
            window.Host.KeyDown((int)Keys.Return, InputModifiers.None);

            var editor = Assert.IsType<TextBox>(grid.Controls[0]);
            editor.Text = "Changed";
            window.Host.KeyDown((int)Keys.Return, InputModifiers.None);

            Assert.Equal("Changed", sample.Title);
            Assert.Equal(new[] { "Title:Hello" }, changes);
        }
    }

    [Fact]
    public void EnumAndBoolRowsEditThroughADropDown()
    {
        var (_, form, window, grid) = ShowGrid();
        using (form)
        {
            var sample = new Sample();
            grid.SelectedObject = sample;

            Find(grid, "Flavour")!.Select();
            window.Host.KeyDown((int)Keys.F4, InputModifiers.None);

            var combo = Assert.IsType<ComboBox>(grid.Controls[0]);
            Assert.Equal(3, combo.Items.Count);
            Assert.Equal("Plain", combo.SelectedItem);

            combo.SelectedItem = "Sweet";
            Assert.Equal(Flavour.Sweet, sample.Flavour);

            // Escape closes the editor without another write.
            window.Host.KeyDown((int)Keys.Escape, InputModifiers.None);
        }
    }

    [Fact]
    public void ReadOnlyPropertiesCannotBeEdited()
    {
        var (_, form, window, grid) = ShowGrid();
        using (form)
        {
            grid.SelectedObject = new Sample();
            Find(grid, "ReadOnlyTag")!.Select();
            window.Host.KeyDown((int)Keys.Return, InputModifiers.None);

            // No editor was created.
            Assert.Equal(0, grid.Controls.Count);
        }
    }

    [Fact]
    public void MultiSelectionShowsOnlyTheSharedPropertiesAndWritesToAll()
    {
        var (_, form, _, grid) = ShowGrid();
        using (form)
        {
            var a = new Sample { Title = "A" };
            var b = new Sample { Title = "B" };
            grid.SelectedObjects = new object?[] { a, b };

            var title = (GridItem)Find(grid, "Title")!;
            Assert.NotNull(title);

            grid.SelectedGridItem = title;
            grid.SetValueOnTargets(title.PropertyDescriptor!, "Both");
            Assert.Equal("Both", a.Title);
            Assert.Equal("Both", b.Title);
        }
    }

    // --- navigation and painting ------------------------------------------------------------------

    [Fact]
    public void ArrowKeysWalkTheRowsAndFoldCategories()
    {
        var (_, form, window, grid) = ShowGrid();
        using (form)
        {
            grid.SelectedObject = new Sample();
            var appearance = grid.RootGridItem!.GridItems["Appearance"]!;
            Assert.True(appearance.Expanded);

            grid.SelectedGridItem = appearance;
            window.Host.KeyDown((int)Keys.Left, InputModifiers.None);
            Assert.False(appearance.Expanded);

            window.Host.KeyDown((int)Keys.Right, InputModifiers.None);
            Assert.True(appearance.Expanded);

            window.Host.KeyDown((int)Keys.Down, InputModifiers.None);
            Assert.Equal("Flavour", grid.SelectedGridItem!.Label);

            grid.CollapseAllGridItems();
            foreach (GridItem item in grid.RootGridItem.GridItems) Assert.False(item.Expanded);
            grid.ExpandAllGridItems();
            foreach (GridItem item in grid.RootGridItem.GridItems) Assert.True(item.Expanded);
        }
    }

    [Fact]
    public void TheGridPaintsRowsCategoriesAndTheHelpPane()
    {
        var (_, form, window, grid) = ShowGrid();
        using (form)
        {
            grid.SelectedObject = new Sample();
            grid.SelectedGridItem = Find(grid, "Title");
            Find(grid, "Size")!.Expanded = true;
            grid.Refresh();

            Assert.True(grid.HelpVisible);
            using var bitmap = window.Paint();
            Assert.Equal(320, bitmap.Width);

            var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(outDir);
            bitmap.Save(Path.Combine(outDir, "propertygrid.png"));
        }
    }
}
