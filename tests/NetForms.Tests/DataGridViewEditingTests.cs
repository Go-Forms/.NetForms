using System.Globalization;
using NetForms.Platform;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// The DataGridView editing model of WinForms (decision 142): editing controls (IDataGridViewEditingControl) and
/// editing cells, EditingControlShowing, the dirty current cell, CellValidating/CellParsing/CellValidated around a
/// commit, DataError on a value that does not parse, the keyboard, VirtualMode - and, as the acceptance, the
/// calendar column of Microsoft's "How to: Host Controls in Windows Forms DataGridView Cells".
/// </summary>
public class DataGridViewEditingTests
{
    private static (Form form, TestWindow window, DataGridView grid) ShowGrid(Action<DataGridView>? configure = null, params Control[] others)
    {
        var platform = TestPlatform.Install();
        var form = new Form { ClientSize = new Size(500, 300) };
        var grid = new DataGridView { Bounds = new Rectangle(0, 0, 500, 250), BorderStyle = BorderStyle.None, AllowUserToAddRows = false };
        configure?.Invoke(grid);
        form.Controls.Add(grid);
        form.Controls.AddRange(others);
        form.Show();
        return (form, platform.Windows.Last(), grid);
    }

    private static Point Center(Rectangle r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);

    private static void Click(TestWindow window, DataGridView grid, int column, int row) =>
        window.Click(Center(grid.GetCellDisplayRectangle(column, row, false)));

    private static void Key(TestWindow window, Keys key, InputModifiers mods = InputModifiers.None)
    {
        window.Host.KeyDown((int)(key & Keys.KeyCode), mods);
        window.Host.KeyUp((int)(key & Keys.KeyCode), mods);
    }

    private static DataGridView People(DataGridView grid)
    {
        grid.Columns.Add("Name", "Name");
        grid.Columns.Add("Age", "Age");
        grid.Columns["Age"]!.ValueType = typeof(int);
        grid.Rows.Add("Ada", 36);
        grid.Rows.Add("Alan", 41);
        return grid;
    }

    private static List<string> Record(DataGridView grid)
    {
        var events = new List<string>();
        grid.CellBeginEdit += (_, e) => events.Add($"CellBeginEdit {e.ColumnIndex},{e.RowIndex}");
        grid.EditingControlShowing += (_, e) => events.Add($"EditingControlShowing {e.Control.GetType().Name}");
        grid.CurrentCellDirtyStateChanged += (_, _) => events.Add($"CurrentCellDirtyStateChanged {grid.IsCurrentCellDirty}");
        grid.CellLeave += (_, e) => events.Add($"CellLeave {e.ColumnIndex},{e.RowIndex}");
        grid.RowLeave += (_, e) => events.Add($"RowLeave {e.RowIndex}");
        grid.CellValidating += (_, e) => events.Add($"CellValidating {e.ColumnIndex},{e.RowIndex} '{e.FormattedValue}'");
        grid.CellParsing += (_, e) => events.Add($"CellParsing '{e.Value}' {e.DesiredType?.Name}");
        grid.CellValueChanged += (_, e) => events.Add($"CellValueChanged {e.ColumnIndex},{e.RowIndex}");
        grid.CellValidated += (_, e) => events.Add($"CellValidated {e.ColumnIndex},{e.RowIndex}");
        grid.CellEndEdit += (_, e) => events.Add($"CellEndEdit {e.ColumnIndex},{e.RowIndex}");
        grid.RowValidating += (_, e) => events.Add($"RowValidating {e.RowIndex}");
        grid.RowValidated += (_, e) => events.Add($"RowValidated {e.RowIndex}");
        grid.RowEnter += (_, e) => events.Add($"RowEnter {e.RowIndex}");
        grid.CellEnter += (_, e) => events.Add($"CellEnter {e.ColumnIndex},{e.RowIndex}");
        return events;
    }

    [Fact]
    public void AnEditIsCommittedWhenTheUserLeavesTheCellInWinFormsOrder()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            Click(window, grid, 1, 0);
            var events = Record(grid);
            Click(window, grid, 1, 0); // a click on the current cell edits it
            var editor = Assert.IsType<DataGridViewTextBoxEditingControl>(grid.EditingControl);
            Assert.Equal("36", editor.Text);
            Assert.Equal(editor.Text.Length, editor.SelectionLength);
            editor.Text = "37";

            Click(window, grid, 0, 1);

            Assert.Equal(new[]
            {
                "CellBeginEdit 1,0", "EditingControlShowing DataGridViewTextBoxEditingControl", "CurrentCellDirtyStateChanged True",
                "CellLeave 1,0", "RowLeave 0", "CellValidating 1,0 '37'", "CellParsing '37' Int32", "CellValueChanged 1,0",
                "CurrentCellDirtyStateChanged False", "CellValidated 1,0", "CellEndEdit 1,0",
                "RowValidating 0", "RowValidated 0", "RowEnter 1", "CellEnter 0,1",
            }, events);
            Assert.Equal(37, grid.Rows[0].Cells[1].Value);
            Assert.Null(grid.EditingControl);
            Assert.False(grid.EditingPanel.Visible);
        }
    }

    [Fact]
    public void SettingCurrentCellInCodeValidatesTheCellItLeaves()
    {
        // WinForms: the setter's ScrollIntoView commits through CommitEditForOperation (oracle exact/dgv/edit-current, edit-move).
        var (form, _, grid) = ShowGrid(g => People(g));
        using (form)
        {
            grid.CurrentCell = grid.Rows[0].Cells[0];
            var events = Record(grid);
            grid.CurrentCell = grid.Rows[0].Cells[1];
            Assert.Equal(new[] { "CellLeave 0,0", "CellValidating 0,0 'Ada'", "CellValidated 0,0", "CellEnter 1,0" }, events);

            events.Clear();
            grid.BeginEdit(true);
            grid.EditingControl!.Text = "38";
            grid.CurrentCell = grid.Rows[1].Cells[0];
            Assert.Equal(new[]
            {
                "CellBeginEdit 1,0", "EditingControlShowing DataGridViewTextBoxEditingControl", "CurrentCellDirtyStateChanged True",
                "CellLeave 1,0", "RowLeave 0", "CellValidating 1,0 '38'", "CellParsing '38' Int32", "CellValueChanged 1,0",
                "CurrentCellDirtyStateChanged False", "CellValidated 1,0", "CellEndEdit 1,0",
                "RowValidating 0", "RowValidated 0", "RowEnter 1", "CellEnter 0,1",
            }, events);
            Assert.Equal(38, grid.Rows[0].Cells[1].Value);

            // A cancelled validation keeps the cell, and the setter throws as in WinForms.
            grid.CellValidating += (_, e) => e.Cancel = true;
            Assert.Throws<InvalidOperationException>(() => grid.CurrentCell = grid.Rows[0].Cells[0]);
            Assert.Equal(new Point(0, 1), grid.CurrentCellAddress);
        }
    }

    [Fact]
    public void CancellingCellValidatingKeepsTheCellAndItsEdit()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            grid.CellValidating += (_, e) =>
            {
                if (e.ColumnIndex == 1 && !int.TryParse(e.FormattedValue as string, out int _))
                {
                    grid.Rows[e.RowIndex].ErrorText = "Age must be a number";
                    e.Cancel = true;
                }
            };
            Click(window, grid, 1, 0);
            Assert.True(grid.BeginEdit(true));
            grid.EditingControl!.Text = "old";

            Click(window, grid, 0, 1);
            Assert.Equal(new Point(1, 0), grid.CurrentCellAddress);
            Assert.True(grid.IsCurrentCellInEditMode);
            Assert.Equal("old", grid.EditingControl!.Text);
            Assert.Equal(36, grid.Rows[0].Cells[1].Value);
            Assert.Equal("Age must be a number", grid.Rows[0].ErrorText);

            grid.EditingControl.Text = "40";
            Click(window, grid, 0, 1);
            Assert.Equal(new Point(0, 1), grid.CurrentCellAddress);
            Assert.Equal(40, grid.Rows[0].Cells[1].Value);
        }
    }

    [Fact]
    public void AValueThatDoesNotParseRaisesDataErrorAndStaysInEditMode()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            var errors = new List<DataGridViewDataErrorEventArgs>();
            grid.DataError += (_, e) => errors.Add(e);
            Click(window, grid, 1, 0);
            grid.BeginEdit(true);
            grid.EditingControl!.Text = "abc";

            Assert.False(grid.EndEdit());
            var error = Assert.Single(errors);
            Assert.IsType<FormatException>(error.Exception);
            Assert.Equal(DataGridViewDataErrorContexts.Parsing | DataGridViewDataErrorContexts.Commit, error.Context);
            Assert.True(grid.IsCurrentCellInEditMode);
            Assert.Equal(36, grid.Rows[0].Cells[1].Value);

            // Escape gives the edit up: the old value comes back and edit mode ends.
            Key(window, Keys.Escape);
            Assert.False(grid.IsCurrentCellInEditMode);
            Assert.False(grid.IsCurrentCellDirty);
            Assert.Equal(36, grid.Rows[0].Cells[1].Value);
        }
    }

    [Fact]
    public void CellParsingCanParseTheValueItself()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            grid.CellParsing += (_, e) =>
            {
                if (e.Value is string text && text.EndsWith(" years", StringComparison.Ordinal))
                {
                    e.Value = int.Parse(text[..^6], CultureInfo.InvariantCulture);
                    e.ParsingApplied = true;
                }
            };
            Click(window, grid, 1, 1);
            grid.BeginEdit(false);
            grid.EditingControl!.Text = "42 years";
            Assert.True(grid.EndEdit());
            Assert.Equal(42, grid.Rows[1].Cells[1].Value);
        }
    }

    [Fact]
    public void EditingControlShowingHandsOutTheEditingControlToHook()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            // The WinForms idiom: let the age column take digits only.
            grid.EditingControlShowing += (_, e) =>
            {
                e.CellStyle.BackColor = Color.LightYellow;
                if (e.Control is TextBox box && grid.CurrentCell!.ColumnIndex == 1)
                {
                    box.KeyPress -= DigitsOnly;
                    box.KeyPress += DigitsOnly;
                }
            };
            Click(window, grid, 1, 0);
            Key(window, Keys.F2);
            var editor = grid.EditingControl!;
            Assert.Equal(Color.LightYellow, editor.BackColor);
            Assert.Equal(editor.Text.Length, ((TextBox)editor).SelectionStart); // F2: the caret at the end
            window.Host.TextInput("x5");
            Assert.Equal("365", editor.Text);
        }

        static void DigitsOnly(object? sender, KeyPressEventArgs e) => e.Handled = !char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar);
    }

    [Fact]
    public void TypingStartsTheEditAndEnterCommitsAndMovesDown()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            // A shown grid has made (0, 0) current (decision 147); a click on it would start editing it, as in WinForms.
            Assert.Equal(new Point(0, 0), grid.CurrentCellAddress);
            grid.Focus();
            Assert.False(grid.IsCurrentCellInEditMode);

            // EditOnKeystrokeOrF2 (the default): the key starts the edit, its character replaces the text.
            window.Host.KeyDown((int)Keys.G, InputModifiers.None);
            window.Host.TextInput("G");
            window.Host.KeyUp((int)Keys.G, InputModifiers.None);
            window.Host.TextInput("race");
            Assert.True(grid.IsCurrentCellInEditMode);
            Assert.Equal("Grace", grid.EditingControl!.Text);

            Key(window, Keys.Enter);
            Assert.False(grid.IsCurrentCellInEditMode);
            Assert.Equal("Grace", grid.Rows[0].Cells[0].Value);
            Assert.Equal(new Point(0, 1), grid.CurrentCellAddress);

            // Down in a one-line editing control moves the grid (and commits); Escape discards.
            Key(window, Keys.F2);
            grid.EditingControl!.Text = "Barbara";
            Key(window, Keys.Up);
            Assert.Equal(new Point(0, 0), grid.CurrentCellAddress);
            Assert.Equal("Barbara", grid.Rows[1].Cells[0].Value);
            Key(window, Keys.F2);
            grid.EditingControl!.Text = "nobody";
            Key(window, Keys.Escape);
            Assert.Equal("Grace", grid.Rows[0].Cells[0].Value);
        }
    }

    [Fact]
    public void CancelEditRestoresTheValueAndKeepsEditing()
    {
        var (form, window, grid) = ShowGrid(g => People(g));
        using (form)
        {
            Click(window, grid, 0, 0);
            grid.BeginEdit(true);
            grid.EditingControl!.Text = "changed";
            Assert.True(grid.IsCurrentCellDirty);
            Assert.True(grid.CancelEdit());
            Assert.True(grid.IsCurrentCellInEditMode);
            Assert.False(grid.IsCurrentCellDirty);
            Assert.Equal("Ada", grid.EditingControl!.Text);
            Assert.Equal("Ada", grid.Rows[0].Cells[0].EditedFormattedValue);
        }
    }

    [Fact]
    public void LeavingTheGridForAnotherControlCommitsTheEdit()
    {
        var button = new Button { Bounds = new Rectangle(10, 260, 80, 25) };
        var (form, window, grid) = ShowGrid(g => People(g), button);
        using (form)
        {
            Click(window, grid, 0, 1);   // leaving the first cell, current since the grid was shown, validates it
            int validated = 0;
            grid.CellValidated += (_, _) => validated++;
            grid.BeginEdit(true);
            grid.EditingControl!.Text = "Edsger";

            button.Focus();
            Assert.Equal("Edsger", grid.Rows[1].Cells[0].Value);
            Assert.False(grid.IsCurrentCellInEditMode);
            Assert.Equal(1, validated);
        }
    }

    [Fact]
    public void AComboBoxCellEditsThroughItsEditingControl()
    {
        var (form, window, grid) = ShowGrid(g =>
        {
            var column = new DataGridViewComboBoxColumn { Name = "Level", DisplayMember = "Text", ValueMember = "Id" };
            column.Items.AddRange(new Level(1, "Low"), new Level(2, "High"));
            g.Columns.Add(column);
            g.Rows.Add(new object[] { 1 }); // Rows.Add(1) would be Add(int count), as in WinForms
        });
        using (form)
        {
            Assert.Equal("Low", grid.Rows[0].Cells[0].FormattedValue);
            Click(window, grid, 0, 0);
            Assert.True(grid.BeginEdit(true));
            var combo = Assert.IsType<DataGridViewComboBoxEditingControl>(grid.EditingControl);
            Assert.Equal(2, combo.Items.Count);
            Assert.Equal("Low", combo.Text);
            combo.SelectedIndex = 1;
            Assert.True(grid.IsCurrentCellDirty);
            Assert.True(grid.EndEdit());
            Assert.Equal(2, grid.Rows[0].Cells[0].Value);
            Assert.Equal("High", grid.Rows[0].Cells[0].FormattedValue);
        }
    }

    public sealed record Level(int Id, string Text);

    [Fact]
    public void VirtualModeAsksForValuesAndPushesTheEditedOnes()
    {
        var store = new Dictionary<(int, int), object?> { [(0, 0)] = "zero", [(0, 1)] = "one", [(0, 2)] = "two" };
        var pushed = new List<string>();
        var (form, window, grid) = ShowGrid(g =>
        {
            g.VirtualMode = true;
            g.ColumnCount = 1;
            g.RowCount = 3;
            g.CellValueNeeded += (_, e) => e.Value = store.GetValueOrDefault((e.ColumnIndex, e.RowIndex));
            g.CellValuePushed += (_, e) =>
            {
                store[(e.ColumnIndex, e.RowIndex)] = e.Value;
                pushed.Add($"{e.RowIndex}={e.Value}");
            };
        });
        using (form)
        {
            Assert.Equal(3, grid.RowCount);
            Assert.Equal("one", grid.Rows[1].Cells[0].FormattedValue);
            Click(window, grid, 0, 1);
            grid.BeginEdit(true);
            Assert.Equal("one", grid.EditingControl!.Text);
            grid.EditingControl.Text = "uno";
            grid.EndEdit();
            Assert.Equal(new[] { "1=uno" }, pushed);
            Assert.Equal("uno", grid.Rows[1].Cells[0].FormattedValue);
        }
    }

    // --- the acceptance: Microsoft's calendar column, as the documentation writes it --------------------------------

    public class CalendarColumn : DataGridViewColumn
    {
        public CalendarColumn() : base(new CalendarCell())
        {
        }

        public override DataGridViewCell? CellTemplate
        {
            get => base.CellTemplate;
            set
            {
                // Ensure that the cell used for the template is a CalendarCell.
                if (value != null && !value.GetType().IsAssignableFrom(typeof(CalendarCell)))
                {
                    throw new InvalidCastException("Must be a CalendarCell");
                }
                base.CellTemplate = value;
            }
        }
    }

    public class CalendarCell : DataGridViewTextBoxCell
    {
        public CalendarCell()
        {
            // Use the short date format.
            Style.Format = "d";
        }

        public override void InitializeEditingControl(int rowIndex, object? initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
        {
            // Set the value of the editing control to the current cell value.
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            var ctl = (CalendarEditingControl)DataGridView!.EditingControl!;
            // Use the default row value when Value property is null.
            ctl.Value = Value == null ? (DateTime)DefaultNewRowValue! : (DateTime)Value;
        }

        // Return the type of the editing control that CalendarCell uses.
        public override Type EditType => typeof(CalendarEditingControl);

        // Return the type of the value that CalendarCell contains.
        public override Type ValueType => typeof(DateTime);

        // Use the current date and time as the default value.
        public override object DefaultNewRowValue => DateTime.Now;
    }

    public class CalendarEditingControl : DateTimePicker, IDataGridViewEditingControl
    {
        private DataGridView? _dataGridView;
        private bool _valueChanged;

        public CalendarEditingControl()
        {
            Format = DateTimePickerFormat.Short;
        }

        // Implements the IDataGridViewEditingControl.EditingControlFormattedValue property.
        public object EditingControlFormattedValue
        {
            get => Value.ToShortDateString();
            set
            {
                if (value is string text)
                {
                    try
                    {
                        // This will throw an exception of the string is null, empty, or not in the format of a date.
                        Value = DateTime.Parse(text);
                    }
                    catch
                    {
                        // In the case of an exception, just use the default value so we're not left with a null value.
                        Value = DateTime.Now;
                    }
                }
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

        // Changes the control's user interface (UI) to be consistent with the specified cell style.
        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            Font = dataGridViewCellStyle.Font;
            CalendarForeColor = dataGridViewCellStyle.ForeColor;
            CalendarMonthBackground = dataGridViewCellStyle.BackColor;
        }

        public int EditingControlRowIndex { get; set; }

        // Implements the IDataGridViewEditingControl.EditingControlWantsInputKey method.
        public bool EditingControlWantsInputKey(Keys key, bool dataGridViewWantsInputKey)
        {
            // Let the DateTimePicker handle the keys listed.
            switch (key & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Up:
                case Keys.Down:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.PageDown:
                case Keys.PageUp:
                    return true;
                default:
                    return !dataGridViewWantsInputKey;
            }
        }

        // No preparation needs to be done.
        public void PrepareEditingControlForEdit(bool selectAll)
        {
        }

        public bool RepositionEditingControlOnValueChange => false;

        public DataGridView? EditingControlDataGridView
        {
            get => _dataGridView;
            set => _dataGridView = value;
        }

        public bool EditingControlValueChanged
        {
            get => _valueChanged;
            set => _valueChanged = value;
        }

        public Cursor EditingPanelCursor => base.Cursor;

        protected override void OnValueChanged(EventArgs eventargs)
        {
            // Notify the DataGridView that the contents of the cell have changed.
            _valueChanged = true;
            EditingControlDataGridView!.NotifyCurrentCellDirty(true);
            base.OnValueChanged(eventargs);
        }
    }

    [Fact]
    public void TheCalendarColumnOfTheMicrosoftSampleEditsDates()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            var (form, window, grid) = ShowGrid(g =>
            {
                g.Columns.Add(new CalendarColumn { Name = "Date", HeaderText = "Date" });
                g.Rows.Add(new DateTime(2026, 9, 24));
                g.Rows.Add(new DateTime(2026, 12, 31));
            });
            using (form)
            {
                var shown = new List<Control>();
                grid.EditingControlShowing += (_, e) => shown.Add(e.Control);
                Assert.Equal("09/24/2026", grid.Rows[0].Cells[0].FormattedValue);

                Click(window, grid, 0, 0);
                Click(window, grid, 0, 0);
                var picker = Assert.IsType<CalendarEditingControl>(grid.EditingControl);
                Assert.Same(picker, Assert.Single(shown));
                Assert.Equal(new DateTime(2026, 9, 24), picker.Value);
                Assert.Same(grid, picker.EditingControlDataGridView);
                Assert.Equal(0, picker.EditingControlRowIndex);
                Assert.False(grid.IsCurrentCellDirty);

                picker.Value = new DateTime(2026, 10, 1);
                Assert.True(grid.IsCurrentCellDirty);
                Assert.Equal("10/01/2026", grid.Rows[0].Cells[0].EditedFormattedValue);

                Click(window, grid, 0, 1);
                Assert.Equal(new DateTime(2026, 10, 1), grid.Rows[0].Cells[0].Value);
                Assert.Equal("10/01/2026", grid.Rows[0].Cells[0].FormattedValue);

                // The same control is used again for the next cell, as WinForms does.
                Click(window, grid, 0, 1);
                Assert.Same(picker, grid.EditingControl);
                Assert.Equal(new DateTime(2026, 12, 31), picker.Value);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
