using System.Globalization;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>Ф6.К: MonthCalendar and DateTimePicker - selection, navigation, fields and the drop-down.</summary>
public class DateControlsTests
{
    private static (Form form, TestPlatform platform) ShowForm(Control control)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(400, 300) };
        form.Controls.Add(control);
        form.Show();
        return (form, platform);
    }

    [Fact]
    public void AMonthCalendarSelectsWithinMaxSelectionCount()
    {
        var calendar = new MonthCalendar { MaxSelectionCount = 3 };
        var changed = new List<DateRangeEventArgs>();
        calendar.DateChanged += (_, e) => changed.Add(e);
        calendar.SetDate(new DateTime(2026, 5, 12));
        Assert.Equal(new DateTime(2026, 5, 12), calendar.SelectionStart);
        calendar.SetSelectionRange(new DateTime(2026, 5, 12), new DateTime(2026, 5, 20));
        Assert.Equal(new DateTime(2026, 5, 14), calendar.SelectionEnd); // three days from the start
        Assert.Equal(2, changed.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => calendar.SetDate(new DateTime(1700, 1, 1)));
    }

    [Fact]
    public void TheCalendarKeyboardMovesTheSelection()
    {
        var calendar = new MonthCalendar();
        var (form, platform) = ShowForm(calendar);
        using (form)
        {
            calendar.SetDate(new DateTime(2026, 5, 31));
            calendar.Focus();
            var window = platform.Windows.Last();
            window.Host.KeyDown((int)Keys.Right, InputModifiers.None);
            Assert.Equal(new DateTime(2026, 6, 1), calendar.SelectionStart); // across the month end
            window.Host.KeyDown((int)Keys.Right, InputModifiers.Shift);
            Assert.Equal(new DateTime(2026, 6, 2), calendar.SelectionEnd);
            Assert.Equal(new DateTime(2026, 6, 1), calendar.SelectionStart);
            window.Host.KeyDown((int)Keys.PageDown, InputModifiers.None);
            Assert.Equal(new DateTime(2026, 7, 2), calendar.SelectionStart);
        }
    }

    [Fact]
    public void ADayClickedInTheGridIsSelected()
    {
        var calendar = new MonthCalendar { Location = Point.Empty };
        var (form, platform) = ShowForm(calendar);
        using (form)
        {
            calendar.SetDate(new DateTime(2026, 5, 1));
            DateTime? selected = null;
            calendar.DateSelected += (_, e) => selected = e.Start;
            // Find the cell of the 15th by trying the grid: every cell maps to one date.
            var grid = typeof(MonthCalendar).GetProperty("GridBounds", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(calendar)!;
            var r = (Rectangle)grid;
            platform.Windows.Last().Click(new Point(r.X + r.Width / 2, r.Y + r.Height / 2));
            Assert.NotNull(selected);
            Assert.Equal(5, selected!.Value.Month);
            Assert.Equal(selected, calendar.SelectionStart);
        }
    }

    [Fact]
    public void ThePickerChangesTheSelectedFieldWithTheArrows()
    {
        var picker = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm" };
        var (form, platform) = ShowForm(picker);
        using (form)
        {
            picker.Value = new DateTime(2026, 5, 31, 10, 30, 0);
            Assert.Equal("31.05.2026 10:30", picker.Text);
            picker.Focus();
            var window = platform.Windows.Last();
            int changes = 0;
            picker.ValueChanged += (_, _) => changes++;
            window.Host.KeyDown((int)Keys.Up, InputModifiers.None); // the day wraps within the month
            Assert.Equal(new DateTime(2026, 5, 1, 10, 30, 0), picker.Value);
            window.Host.KeyDown((int)Keys.Right, InputModifiers.None);
            window.Host.KeyDown((int)Keys.Down, InputModifiers.None); // the month
            Assert.Equal(new DateTime(2026, 4, 1, 10, 30, 0), picker.Value);
            window.Host.KeyDown((int)Keys.Right, InputModifiers.None);
            window.Host.KeyDown((int)Keys.Right, InputModifiers.None);
            window.Host.TextInput("1");
            window.Host.TextInput("5");                               // the hour typed: 15
            Assert.Equal(15, picker.Value.Hour);
            Assert.Equal(4, changes);
        }
    }

    [Fact]
    public void ThePickerKeepsItsValueWithinMinAndMax()
    {
        TestPlatform.Install();
        var picker = new DateTimePicker { MinDate = new DateTime(2026, 1, 1), MaxDate = new DateTime(2026, 12, 31) };
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.Value = new DateTime(2027, 1, 1));
        picker.Value = new DateTime(2026, 6, 1);
        picker.MaxDate = new DateTime(2026, 3, 1);
        Assert.Equal(new DateTime(2026, 3, 1), picker.Value);
        picker.ShowCheckBox = true;
        picker.Checked = false;
        Assert.False(picker.Checked);
        picker.Value = new DateTime(2026, 2, 1); // setting a value checks the box, as in WinForms
        Assert.True(picker.Checked);
    }

    [Fact]
    public void TheDropDownCommitsTheDayPickedAndKeepsTheTime()
    {
        var picker = new DateTimePicker();
        var (form, platform) = ShowForm(picker);
        using (form)
        {
            picker.Value = new DateTime(2026, 5, 12, 8, 45, 0);
            picker.Focus();
            var window = platform.Windows.Last();
            bool dropped = false, closed = false;
            picker.DropDown += (_, _) => dropped = true;
            picker.CloseUp += (_, _) => closed = true;
            window.Host.KeyDown((int)Keys.F4, InputModifiers.None);
            Assert.True(dropped);
            window.Host.KeyDown((int)Keys.Down, InputModifiers.None);  // a week later, in the calendar
            window.Host.KeyDown((int)Keys.Enter, InputModifiers.None);
            Assert.True(closed);
            Assert.Equal(new DateTime(2026, 5, 19, 8, 45, 0), picker.Value);
        }
    }
}
