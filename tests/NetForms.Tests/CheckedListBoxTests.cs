using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Ф6: CheckedListBox - check states that belong to the items, the ItemCheck veto, mouse and keyboard.</summary>
public class CheckedListBoxTests
{
    private static (Form form, TestWindow window) ShowForm(Control control)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(300, 200) };
        form.Controls.Add(control);
        form.Show();
        return (form, platform.Windows.Last());
    }

    [Fact]
    public void CheckStatesFollowTheirItems()
    {
        var list = new CheckedListBox();
        list.Items.Add("b", true);
        list.Items.Add("d");
        list.Items.Add("a", CheckState.Indeterminate);
        Assert.Equal(new[] { 0, 2 }, list.CheckedIndices.Cast<int>());
        Assert.Equal(new object[] { "b", "a" }, list.CheckedItems.Cast<object>());
        Assert.Equal(CheckState.Indeterminate, list.GetItemCheckState(2));
        Assert.True(list.GetItemChecked(2));

        list.Items.Insert(0, "c");                 // everything moves down one
        Assert.Equal(new[] { 1, 3 }, list.CheckedIndices.Cast<int>());
        list.Items.RemoveAt(1);                    // "b" goes, with its mark
        Assert.Equal(new object[] { "a" }, list.CheckedItems.Cast<object>());

        list.Sorted = true;                        // a, c, d: the mark stays on "a"
        Assert.Equal(new object[] { "a", "c", "d" }, list.Items.Cast<object>());
        Assert.Equal(CheckState.Indeterminate, list.GetItemCheckState(0));
        list.Items.Add("b", true);                 // sorted insert: a, b, c, d
        Assert.Equal(new[] { 0, 1 }, list.CheckedIndices.Cast<int>());

        list.Items.Clear();
        Assert.Empty(list.CheckedIndices);
    }

    [Fact]
    public void ItemCheckComesFirstAndCanChangeTheOutcome()
    {
        var list = new CheckedListBox();
        list.Items.AddRange(new object[] { "one", "two" });
        var seen = new List<string>();
        list.ItemCheck += (_, e) =>
        {
            seen.Add($"{e.Index}:{e.CurrentValue}->{e.NewValue}");
            if (e.Index == 1) e.NewValue = e.CurrentValue;   // "two" cannot be checked
        };
        list.SetItemChecked(0, true);
        list.SetItemChecked(1, true);
        Assert.Equal(new[] { "0:Unchecked->Checked", "1:Unchecked->Checked" }, seen);
        Assert.True(list.GetItemChecked(0));
        Assert.False(list.GetItemChecked(1));
        Assert.Throws<ArgumentException>(() => list.SelectionMode = SelectionMode.MultiExtended);
        Assert.Equal(DrawMode.Normal, list.DrawMode);
    }

    [Fact]
    public void TheBoxTogglesOnClickTheTextOnlyWithCheckOnClickAndSpaceToggles()
    {
        var list = new CheckedListBox { Bounds = new Rectangle(10, 10, 150, 80) };
        list.Items.AddRange(new object[] { "one", "two", "three" });
        var (form, window) = ShowForm(list);
        using (form)
        {
            var row = list.GetItemRectangle(1);
            window.Click(new Point(10 + row.X + 5, 10 + row.Y + row.Height / 2));      // on the box
            Assert.True(list.GetItemChecked(1));
            Assert.Equal(1, list.SelectedIndex);

            var text = list.GetItemRectangle(2);
            window.Click(new Point(10 + text.X + 60, 10 + text.Y + text.Height / 2));  // on the text: selects only
            Assert.False(list.GetItemChecked(2));
            Assert.Equal(2, list.SelectedIndex);

            window.Host.TextInput(" ");                                                  // Space toggles the selected
            Assert.True(list.GetItemChecked(2));

            list.CheckOnClick = true;
            var first = list.GetItemRectangle(0);
            window.Click(new Point(10 + first.X + 60, 10 + first.Y + first.Height / 2));
            Assert.True(list.GetItemChecked(0));
        }
    }
}
