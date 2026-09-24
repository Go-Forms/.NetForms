using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Layout, selection, checking and keyboard behaviour of the four ListView faces.</summary>
public class ListViewTests
{
    private static (TestPlatform platform, Form form, TestWindow window, ListView list) ShowList(Action<ListView> configure)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        var list = new ListView { Bounds = new Rectangle(0, 0, 400, 300), BorderStyle = BorderStyle.None };
        configure(list);
        form.Controls.Add(list);
        form.Show();
        return (platform, form, platform.Windows.Last(), list);
    }

    private static void Fill(ListView list, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var item = list.Items.Add("Item " + i);
            item.SubItems.Add("Sub " + i);
            item.SubItems.Add((i * 10).ToString());
        }
    }

    private static void Key(TestWindow w, Keys key, InputModifiers mods = InputModifiers.None)
    {
        w.Host.KeyDown((int)(key & Keys.KeyCode), mods);
        w.Host.KeyUp((int)(key & Keys.KeyCode), mods);
    }

    // --- layout ------------------------------------------------------------------------

    [Fact]
    public void DetailsViewStacksRowsUnderTheColumnHeader()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 120);
            l.Columns.Add("Value", 80);
        });
        using (form)
        {
            Fill(list, 5);
            int rowHeight = list.RowHeight;
            int header = list.HeaderHeight;
            Assert.True(header > 0);

            var first = list.GetItemRect(0);
            Assert.Equal(header, first.Top);
            Assert.Equal(rowHeight, first.Height);
            // Rows are as wide as the columns together and follow one another.
            Assert.Equal(200, first.Width);
            Assert.Equal(first.Bottom, list.GetItemRect(1).Top);

            // No header when the style says so.
            list.HeaderStyle = ColumnHeaderStyle.None;
            Assert.Equal(0, list.HeaderHeight);
            Assert.Equal(0, list.GetItemRect(0).Top);
        }
    }

    [Fact]
    public void LargeIconViewWrapsItemsIntoRows()
    {
        var (_, form, _, list) = ShowList(l => l.View = View.LargeIcon);
        using (form)
        {
            Fill(list, 12);
            var first = list.GetItemRect(0);
            var second = list.GetItemRect(1);
            Assert.Equal(first.Top, second.Top);
            Assert.Equal(first.Right, second.Left);

            int perRow = 400 / first.Width;
            var wrapped = list.GetItemRect(perRow);
            Assert.Equal(first.Left, wrapped.Left);
            Assert.Equal(first.Bottom, wrapped.Top);
        }
    }

    [Fact]
    public void ListViewFlowsDownColumnsAndScrollsSideways()
    {
        var (_, form, _, list) = ShowList(l => l.View = View.List);
        using (form)
        {
            Fill(list, 40);
            var first = list.GetItemRect(0);
            var second = list.GetItemRect(1);
            // Down first, then across.
            Assert.Equal(first.Left, second.Left);
            Assert.Equal(first.Bottom, second.Top);

            int rows = 300 / first.Height;
            var nextColumn = list.GetItemRect(rows);
            Assert.Equal(first.Top, nextColumn.Top);
            Assert.True(nextColumn.Left > first.Left);
        }
    }

    [Fact]
    public void SmallIconAndTileViewsUseDifferentCells()
    {
        var (_, form, _, list) = ShowList(l => l.View = View.SmallIcon);
        using (form)
        {
            Fill(list, 6);
            var small = list.GetItemRect(0);
            Assert.Equal(list.RowHeight, small.Height);

            list.View = View.Tile;
            list.TileSize = new Size(150, 40);
            var tile = list.GetItemRect(0);
            Assert.Equal(new Size(150, 40), tile.Size);
        }
    }

    [Fact]
    public void GroupsSplitTheItemsIntoBandsWithHeaders()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            var a = list.Groups.Add("a", "First group");
            var b = list.Groups.Add("b", "Second group");
            Fill(list, 4);
            list.Items[0].Group = b;
            list.Items[1].Group = a;
            list.Items[2].Group = b;
            list.Items[3].Group = a;

            // Display order follows the groups, not the collection.
            Assert.True(list.GetItemRect(1).Top < list.GetItemRect(0).Top);
            Assert.True(list.GetItemRect(3).Top < list.GetItemRect(0).Top);
            // A band header sits above each group, so the first row is not at the top.
            Assert.True(list.GetItemRect(1).Top > list.HeaderHeight);
            Assert.Same(a, list.Items[1].Group);
            Assert.Equal(2, a.Items.Count);
        }
    }

    // --- selection ----------------------------------------------------------------------

    [Fact]
    public void ClickingSelectsAndCtrlClickExtendsTheSelection()
    {
        var (_, form, window, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            Fill(list, 5);
            int changes = 0;
            list.SelectedIndexChanged += (_, _) => changes++;

            window.Click(Center(list.GetItemRect(1)));
            Assert.Equal(new[] { 1 }, Indices(list));
            Assert.Same(list.Items[1], list.FocusedItem);
            Assert.True(changes > 0);

            window.Host.MouseDown(MouseButton.Left, Center(list.GetItemRect(3)), 1, InputModifiers.Control);
            window.Host.MouseUp(MouseButton.Left, Center(list.GetItemRect(3)), InputModifiers.Control);
            Assert.Equal(new[] { 1, 3 }, Indices(list));

            // Shift extends from the anchor.
            window.Host.MouseDown(MouseButton.Left, Center(list.GetItemRect(0)), 1, InputModifiers.Shift);
            window.Host.MouseUp(MouseButton.Left, Center(list.GetItemRect(0)), InputModifiers.Shift);
            Assert.Equal(new[] { 0, 1, 2, 3 }, Indices(list));

            // A click on empty space clears it.
            window.Click(new Point(10, 290));
            Assert.Empty(list.SelectedItems);
        }
    }

    [Fact]
    public void SingleSelectKeepsOneItemSelected()
    {
        var (_, form, window, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.MultiSelect = false;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            Fill(list, 3);
            window.Click(Center(list.GetItemRect(0)));
            window.Host.MouseDown(MouseButton.Left, Center(list.GetItemRect(2)), 1, InputModifiers.Control);
            window.Host.MouseUp(MouseButton.Left, Center(list.GetItemRect(2)), InputModifiers.Control);
            Assert.Equal(new[] { 2 }, Indices(list));
        }
    }

    [Fact]
    public void ArrowKeysMoveTheSelectionAndCtrlAPicksEverything()
    {
        var (_, form, window, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            Fill(list, 5);
            window.Click(Center(list.GetItemRect(0)));

            Key(window, Keys.Down);
            Assert.Same(list.Items[1], list.FocusedItem);
            Assert.Equal(new[] { 1 }, Indices(list));

            Key(window, Keys.End);
            Assert.Equal(new[] { 4 }, Indices(list));

            Key(window, Keys.Home);
            Assert.Equal(new[] { 0 }, Indices(list));

            window.Host.KeyDown((int)Keys.A, InputModifiers.Control);
            Assert.Equal(5, list.SelectedItems.Count);
        }
    }

    // --- checking, columns, sorting -------------------------------------------------------

    [Fact]
    public void CheckBoxesToggleOnClickAndOnSpace()
    {
        var (_, form, window, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.CheckBoxes = true;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            Fill(list, 3);
            int checkedCount = 0;
            list.ItemChecked += (_, _) => checkedCount++;

            var row = list.GetItemRect(0);
            window.Click(new Point(row.X + 8, row.Y + row.Height / 2));
            Assert.True(list.Items[0].Checked);
            Assert.Equal(1, checkedCount);
            Assert.Equal(new[] { 0 }, list.CheckedIndices);

            // The click landed on the box, so it did not select the row.
            Assert.Empty(list.SelectedItems);

            window.Click(Center(list.GetItemRect(1)));
            Key(window, Keys.Space);
            Assert.True(list.Items[1].Checked);
            Assert.Equal(2, list.CheckedItems.Count);
        }
    }

    [Fact]
    public void ItemCheckCanVetoTheChange()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.CheckBoxes = true;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            Fill(list, 1);
            list.ItemCheck += (_, e) => e.NewValue = CheckState.Unchecked;
            list.Items[0].Checked = true;
            Assert.False(list.Items[0].Checked);
        }
    }

    [Fact]
    public void ClickingAColumnHeaderRaisesColumnClick()
    {
        var (_, form, window, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 120);
            l.Columns.Add("Value", 80);
        });
        using (form)
        {
            Fill(list, 3);
            int clicked = -1;
            list.ColumnClick += (_, e) => clicked = e.Column;

            window.Click(new Point(150, list.HeaderHeight / 2));
            Assert.Equal(1, clicked);

            // Nonclickable headers stay quiet.
            clicked = -1;
            list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            window.Click(new Point(30, list.HeaderHeight / 2));
            Assert.Equal(-1, clicked);
        }
    }

    [Fact]
    public void SortingReordersTheItems()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            list.Items.Add("Charlie");
            list.Items.Add("alpha");
            list.Items.Add("Bravo");

            list.Sorting = SortOrder.Ascending;
            Assert.Equal(new[] { "alpha", "Bravo", "Charlie" }, Texts(list));

            list.Sorting = SortOrder.Descending;
            Assert.Equal(new[] { "Charlie", "Bravo", "alpha" }, Texts(list));

            list.Sorting = SortOrder.None;
            list.ListViewItemSorter = new LengthComparer();
            list.Sort();
            Assert.Equal(new[] { "Bravo", "alpha", "Charlie" }, Texts(list));
        }
    }

    private sealed class LengthComparer : System.Collections.IComparer
    {
        public int Compare(object? x, object? y)
        {
            int result = ((ListViewItem)x!).Text.Length.CompareTo(((ListViewItem)y!).Text.Length);
            return result != 0 ? result : string.CompareOrdinal(((ListViewItem)x!).Text, ((ListViewItem)y!).Text);
        }
    }

    [Fact]
    public void AutoResizeFitsTheColumnToItsContentOrItsHeader()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("N", 10);
        });
        using (form)
        {
            list.Items.Add("A very long item caption indeed");
            list.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.ColumnContent);
            int content = list.Columns[0].Width;
            Assert.True(content > 100);

            list.AutoResizeColumn(0, ColumnHeaderAutoResizeStyle.HeaderSize);
            Assert.True(list.Columns[0].Width < content);
        }
    }

    // --- scrolling and hit-testing ----------------------------------------------------------

    [Fact]
    public void TheListScrollsToKeepAnItemVisible()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.Columns.Add("Name", 200);
        });
        using (form)
        {
            Fill(list, 200);
            Assert.Equal(list.Items[0], list.TopItem);

            list.EnsureVisible(150);
            Assert.NotEqual(list.Items[0], list.TopItem);
            var rect = list.GetItemRect(150);
            Assert.True(rect.Top >= 0 && rect.Bottom <= list.Height);
        }
    }

    [Fact]
    public void HitTestReportsTheItemTheSubItemAndWhereTheClickLanded()
    {
        var (_, form, _, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.CheckBoxes = true;
            l.Columns.Add("Name", 120);
            l.Columns.Add("Sub", 120);
        });
        using (form)
        {
            Fill(list, 3);
            var row = list.GetItemRect(1);

            var onCheck = list.HitTest(row.X + 8, row.Y + row.Height / 2);
            Assert.Same(list.Items[1], onCheck.Item);
            Assert.Equal(ListViewHitTestLocations.StateImage, onCheck.Location);

            var onSecondColumn = list.HitTest(row.X + 180, row.Y + row.Height / 2);
            Assert.Same(list.Items[1].SubItems[1], onSecondColumn.SubItem);

            var outside = list.HitTest(10, 295);
            Assert.Null(outside.Item);
            Assert.Equal(ListViewHitTestLocations.None, outside.Location);

            Assert.Same(list.Items[2], list.GetItemAt(Center(list.GetItemRect(2)).X, Center(list.GetItemRect(2)).Y));
            Assert.Same(list.Items[0], list.FindItemWithText("Item 0"));
        }
    }

    [Fact]
    public void EveryViewPaintsWithoutThrowing()
    {
        var (_, form, window, list) = ShowList(l =>
        {
            l.View = View.Details;
            l.CheckBoxes = true;
            l.GridLines = true;
            l.FullRowSelect = true;
            l.Columns.Add("Name", 120);
            l.Columns.Add("Sub", 100);
            l.Columns.Add("Number", 80);
        });
        using (form)
        {
            Fill(list, 20);
            list.Items[2].Selected = true;
            list.Items[3].Checked = true;
            list.Items[4].BackColor = Color.LightYellow;

            var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(outDir);
            foreach (var view in new[] { View.Details, View.LargeIcon, View.SmallIcon, View.List, View.Tile })
            {
                list.View = view;
                using var bitmap = window.Paint();
                Assert.Equal(400, bitmap.Width);
                // Saved for eyeballing, not compared (see the golden question in docs/PLAN.md).
                bitmap.Save(Path.Combine(outDir, "listview-" + view.ToString().ToLowerInvariant() + ".png"));
            }
        }
    }

    private static Point Center(Rectangle r) => new Point(r.X + r.Width / 2, r.Y + r.Height / 2);

    private static int[] Indices(ListView list)
    {
        var result = new List<int>();
        foreach (int i in list.SelectedIndices) result.Add(i);
        return result.ToArray();
    }

    private static string[] Texts(ListView list)
    {
        var result = new List<string>();
        foreach (ListViewItem item in list.Items) result.Add(item.Text);
        return result.ToArray();
    }
}
