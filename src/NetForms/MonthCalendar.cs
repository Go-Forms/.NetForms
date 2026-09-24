using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;

namespace System.Windows.Forms;

public enum Day
{
    Monday = 0,
    Tuesday = 1,
    Wednesday = 2,
    Thursday = 3,
    Friday = 4,
    Saturday = 5,
    Sunday = 6,
    Default = 7,
}

/// <summary>A range of dates: what a MonthCalendar has selected.</summary>
[TypeConverter(typeof(ExpandableObjectConverter))]
public sealed class SelectionRange
{
    public SelectionRange() { }

    public SelectionRange(DateTime lower, DateTime upper)
    {
        if (lower < upper)
        {
            Start = lower.Date;
            End = upper.Date;
        }
        else
        {
            Start = upper.Date;
            End = lower.Date;
        }
    }

    public SelectionRange(SelectionRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        Start = range.Start;
        End = range.End;
    }

    public DateTime Start { get; set; } = DateTime.MinValue.Date;

    public DateTime End { get; set; } = DateTime.MaxValue.Date;

    public override string ToString() => $"SelectionRange: Start: {Start}, End: {End}";
}

public class DateRangeEventArgs : EventArgs
{
    public DateRangeEventArgs(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    public DateTime Start { get; }
    public DateTime End { get; }
}

public delegate void DateRangeEventHandler(object? sender, DateRangeEventArgs e);

/// <summary>
/// A month calendar (Ф6.К): one month at a time (CalendarDimensions is kept, the layout shows 1×1), the
/// title with previous/next arrows, the day grid with the neighbouring months' days, today circled and
/// the "Today:" line, a range selection up to MaxSelectionCount days, bolded dates, the keyboard
/// (arrows, Page Up/Down, Home/End, Shift to extend).
/// </summary>
[DefaultProperty("SelectionRange")]
[DefaultEvent("DateChanged")]
[DefaultBindingProperty("SelectionRange")]
public class MonthCalendar : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new ImeMode ImeMode
    {
        get => base.ImeMode;
        set => base.ImeMode = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding
    {
        get => base.Padding;
        set => base.Padding = value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Localizable(false)]
    public new Size Size
    {
        get => base.Size;
        set => base.Size = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? Click
    {
        add => base.Click += value;
        remove => base.Click -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? DoubleClick
    {
        add => base.DoubleClick += value;
        remove => base.DoubleClick -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event MouseEventHandler? MouseClick
    {
        add => base.MouseClick += value;
        remove => base.MouseClick -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event MouseEventHandler? MouseDoubleClick
    {
        add => base.MouseDoubleClick += value;
        remove => base.MouseDoubleClick -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event PaintEventHandler? Paint
    {
        add => base.Paint += value;
        remove => base.Paint -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }

    private const int DefaultMaxSelectionCount = 7;
    private const Day DefaultFirstDayOfWeek = Day.Default;
    private const int DefaultScrollChange = 0;
    private const int Margin = 6;

    private static readonly DateTime s_minDate = new(1753, 1, 1);
    private static readonly DateTime s_maxDate = new(9998, 12, 31);

    private DateTime _selectionStart;
    private DateTime _selectionEnd;
    private DateTime _anchor;
    private DateTime _displayMonth;
    private int _maxSelectionCount = DefaultMaxSelectionCount;
    private DateTime _minDate = s_minDate;
    private DateTime _maxDate = s_maxDate;
    private DateTime? _todayDate;
    private bool _showToday = true;
    private bool _showTodayCircle = true;
    private bool _showWeekNumbers;
    private Day _firstDayOfWeek = DefaultFirstDayOfWeek;
    private int _scrollChange = DefaultScrollChange;
    private Size _dimensions = new(1, 1);
    private readonly List<DateTime> _bolded = new();
    private readonly List<DateTime> _annuallyBolded = new();
    private readonly List<DateTime> _monthlyBolded = new();
    private Color _titleBackColor = SystemColors.ActiveCaption;
    private Color _titleForeColor = SystemColors.ActiveCaptionText;
    private Color _trailingForeColor = SystemColors.GrayText;

    public MonthCalendar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.ResizeRedraw, true);
        _selectionStart = _selectionEnd = _anchor = DateTime.Today;
        _displayMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        TabStop = true;
    }

    protected override Size DefaultSize => SingleMonthSize;

    protected override Padding DefaultMargin => new(9);

    /// <summary>The size one month needs with the control's font (WinForms: 227×162 with Segoe UI 9).</summary>
    [Browsable(false)]
    [Category("Appearance")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Size SingleMonthSize
    {
        get
        {
            var (cell, rowHeight) = CellSize();
            int weeks = _showWeekNumbers ? cell : 0;
            return new Size(2 * Margin + weeks + 7 * cell, TitleHeight + rowHeight + 6 * rowHeight + (_showToday ? rowHeight + 4 : 0) + 2 * Margin);
        }
    }

    /// <summary>A day cell: about two font heights wide (31px with Segoe UI 9, so one month is 227px wide as in WinForms).</summary>
    private (int Cell, int Row) CellSize()
    {
        int cell = Math.Max((int)Math.Round(Font.Height * 1.95), TextRenderer.MeasureText("Wed", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width + 6);
        return (cell, Font.Height + 1);
    }

    private int TitleHeight => Font.Height + 12;

    // --- selection ----------------------------------------------------------------------------------------

    [Category("Behavior")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The start date in a range of dates selected in a month calendar control.")]
    public DateTime SelectionStart
    {
        get => _selectionStart;
        set => SetSelection(value, value > _selectionEnd || (_selectionEnd - value).Days >= _maxSelectionCount ? value : _selectionEnd);
    }

    [Category("Behavior")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The end date in a range of dates selected in a month calendar control.")]
    public DateTime SelectionEnd
    {
        get => _selectionEnd;
        set => SetSelection(value < _selectionStart || (value - _selectionStart).Days >= _maxSelectionCount ? value : _selectionStart, value);
    }

    [Category("Behavior")]
    [Description("The range of dates selected in a month calendar control.")]
    public SelectionRange SelectionRange
    {
        get => new(_selectionStart, _selectionEnd);
        set => SetSelectionRange(value.Start, value.End);
    }

    internal bool ShouldSerializeSelectionRange() => _selectionStart != DateTime.Today || _selectionEnd != DateTime.Today;

    public void SetDate(DateTime date) => SetSelectionRange(date, date);

    public void SetSelectionRange(DateTime date1, DateTime date2)
    {
        if (date1 < _minDate || date1 > _maxDate) throw new ArgumentOutOfRangeException(nameof(date1));
        if (date2 < _minDate || date2 > _maxDate) throw new ArgumentOutOfRangeException(nameof(date2));
        if (date1 > date2) (date1, date2) = (date2, date1);
        // A range longer than MaxSelectionCount keeps its start (WinForms).
        if ((date2.Date - date1.Date).Days >= _maxSelectionCount) date2 = date1.Date.AddDays(_maxSelectionCount - 1);
        SetSelection(date1, date2);
    }

    private void SetSelection(DateTime start, DateTime end)
    {
        start = Clamp(start.Date);
        end = Clamp(end.Date);
        if (start == _selectionStart && end == _selectionEnd) return;
        _selectionStart = start;
        _selectionEnd = end;
        _anchor = start;
        EnsureVisible(start);
        Invalidate();
        OnDateChanged(new DateRangeEventArgs(start, end));
    }

    private DateTime Clamp(DateTime d) => d < _minDate ? _minDate.Date : d > _maxDate ? _maxDate.Date : d;

    private void EnsureVisible(DateTime date)
    {
        var month = new DateTime(date.Year, date.Month, 1);
        if (month != _displayMonth)
        {
            _displayMonth = month;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(DefaultMaxSelectionCount)]
    [Description("The total number of days that can be selected for the control.")]
    public int MaxSelectionCount
    {
        get => _maxSelectionCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxSelectionCount = value;
        }
    }

    [Category("Behavior")]
    [Description("The minimum date that can be selected for a month calendar control.")]
    public DateTime MinDate
    {
        get => _minDate;
        set
        {
            if (value < s_minDate) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > _maxDate) throw new ArgumentOutOfRangeException(nameof(value));
            _minDate = value;
            if (_selectionStart < value) SetSelection(value, _selectionEnd < value ? value : _selectionEnd);
            Invalidate();
        }
    }

    internal bool ShouldSerializeMinDate() => _minDate != s_minDate;

    [Category("Behavior")]
    [Description("The maximum date that can be selected for a month calendar control.")]
    public DateTime MaxDate
    {
        get => _maxDate;
        set
        {
            if (value > s_maxDate) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < _minDate) throw new ArgumentOutOfRangeException(nameof(value));
            _maxDate = value;
            if (_selectionEnd > value) SetSelection(_selectionStart > value ? value : _selectionStart, value);
            Invalidate();
        }
    }

    internal bool ShouldSerializeMaxDate() => _maxDate != s_maxDate;

    [Category("Behavior")]
    [Description("The current day.")]
    public DateTime TodayDate
    {
        get => (_todayDate ?? DateTime.Today).Date;
        set
        {
            _todayDate = value.Date;
            Invalidate();
        }
    }

    [Browsable(false)]
    [Category("Behavior")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TodayDateSet => _todayDate.HasValue;

    internal bool ShouldSerializeTodayDate() => _todayDate.HasValue;

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Indicates whether the month calendar control will display the \"today\" date at the bottom of the control.")]
    public bool ShowToday
    {
        get => _showToday;
        set
        {
            if (_showToday == value) return;
            _showToday = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Indicates whether the month calendar control will circle the \"today\" date.")]
    public bool ShowTodayCircle
    {
        get => _showTodayCircle;
        set
        {
            _showTodayCircle = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Localizable(true)]
    [DefaultValue(false)]
    [Description("Indicates whether the month calendar control will display week numbers (1 to 52) to the left of each row of days.")]
    public bool ShowWeekNumbers
    {
        get => _showWeekNumbers;
        set
        {
            _showWeekNumbers = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Localizable(true)]
    [DefaultValue(DefaultFirstDayOfWeek)]
    [Description("The first day of the week.")]
    public Day FirstDayOfWeek
    {
        get => _firstDayOfWeek;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Day));
            _firstDayOfWeek = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(DefaultScrollChange)]
    [Description("The number of months that a single click on a next/previous button moves the display by.")]
    public int ScrollChange
    {
        get => _scrollChange;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _scrollChange = value;
        }
    }

    [Category("Appearance")]
    [Localizable(true)]
    [Description("The number of rows and columns of months in a month calendar.")]
    public Size CalendarDimensions
    {
        get => _dimensions;
        set
        {
            if (value.Width < 1 || value.Height < 1 || value.Width * value.Height > 12) throw new ArgumentOutOfRangeException(nameof(value));
            _dimensions = value;
        }
    }

    internal bool ShouldSerializeCalendarDimensions() => _dimensions != new Size(1, 1);

    [Category("Appearance")]
    [Description("The background color displayed in the calendar's title.")]
    public Color TitleBackColor
    {
        get => _titleBackColor;
        set
        {
            _titleBackColor = value;
            Invalidate();
        }
    }

    internal bool ShouldSerializeTitleBackColor() => _titleBackColor != SystemColors.ActiveCaption;

    [Category("Appearance")]
    [Description("The color used to display text within the calendar's title.")]
    public Color TitleForeColor
    {
        get => _titleForeColor;
        set
        {
            _titleForeColor = value;
            Invalidate();
        }
    }

    internal bool ShouldSerializeTitleForeColor() => _titleForeColor != SystemColors.ActiveCaptionText;

    [Category("Appearance")]
    [Description("The color used to display the previous and following months that appear on the month calendar.")]
    public Color TrailingForeColor
    {
        get => _trailingForeColor;
        set
        {
            _trailingForeColor = value;
            Invalidate();
        }
    }

    internal bool ShouldSerializeTrailingForeColor() => _trailingForeColor != SystemColors.GrayText;

    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Window;
        set => base.BackColor = value;
    }

    // --- bolded dates -------------------------------------------------------------------------------------

    [Localizable(true)]
    public DateTime[] BoldedDates
    {
        get => _bolded.ToArray();
        set { _bolded.Clear(); if (value != null) _bolded.AddRange(value); Invalidate(); }
    }

    [Localizable(true)]
    [Description("Indicates which annual dates should be boldface.")]
    public DateTime[] AnnuallyBoldedDates
    {
        get => _annuallyBolded.ToArray();
        set { _annuallyBolded.Clear(); if (value != null) _annuallyBolded.AddRange(value); Invalidate(); }
    }

    [Localizable(true)]
    [Description("Indicates which monthly dates to bold.")]
    public DateTime[] MonthlyBoldedDates
    {
        get => _monthlyBolded.ToArray();
        set { _monthlyBolded.Clear(); if (value != null) _monthlyBolded.AddRange(value); Invalidate(); }
    }

    internal bool ShouldSerializeBoldedDates() => _bolded.Count > 0;
    internal bool ShouldSerializeAnnuallyBoldedDates() => _annuallyBolded.Count > 0;
    internal bool ShouldSerializeMonthlyBoldedDates() => _monthlyBolded.Count > 0;

    public void AddBoldedDate(DateTime date) => _bolded.Add(date);
    public void AddAnnuallyBoldedDate(DateTime date) => _annuallyBolded.Add(date);
    public void AddMonthlyBoldedDate(DateTime date) => _monthlyBolded.Add(date);
    public void RemoveBoldedDate(DateTime date) => _bolded.RemoveAll(d => d.Date == date.Date);
    public void RemoveAnnuallyBoldedDate(DateTime date) => _annuallyBolded.RemoveAll(d => d.Month == date.Month && d.Day == date.Day);
    public void RemoveMonthlyBoldedDate(DateTime date) => _monthlyBolded.RemoveAll(d => d.Day == date.Day);
    public void RemoveAllBoldedDates() => _bolded.Clear();
    public void RemoveAllAnnuallyBoldedDates() => _annuallyBolded.Clear();
    public void RemoveAllMonthlyBoldedDates() => _monthlyBolded.Clear();
    public void UpdateBoldedDates() => Invalidate();

    private bool IsBold(DateTime d) =>
        _bolded.Any(b => b.Date == d) || _annuallyBolded.Any(b => b.Month == d.Month && b.Day == d.Day) || _monthlyBolded.Any(b => b.Day == d.Day);

    /// <summary>The first and last day of the months shown (only the visible month's own days, or with the trailing ones).</summary>
    public SelectionRange GetDisplayRange(bool visible) =>
        visible ? new SelectionRange(_displayMonth, _displayMonth.AddMonths(1).AddDays(-1)) : new SelectionRange(GridStart, GridStart.AddDays(41));

    // --- layout -------------------------------------------------------------------------------------------

    private DayOfWeek FirstDay => _firstDayOfWeek == Day.Default
        ? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek
        : (DayOfWeek)(((int)_firstDayOfWeek + 1) % 7);

    private DateTime GridStart
    {
        get
        {
            int offset = ((int)_displayMonth.DayOfWeek - (int)FirstDay + 7) % 7;
            if (offset == 0) offset = 7; // a trailing row of the previous month, as the Win32 calendar shows
            return _displayMonth.AddDays(-offset);
        }
    }

    private Rectangle GridBounds
    {
        get
        {
            var (cell, row) = CellSize();
            int x = Margin + (_showWeekNumbers ? cell : 0);
            int y = Margin + TitleHeight + row;
            return new Rectangle(x, y, 7 * cell, 6 * row);
        }
    }

    private Rectangle PreviousButton => new(Margin, Margin + 4, TitleHeight - 8, TitleHeight - 8);

    private Rectangle NextButton => new(Width - Margin - (TitleHeight - 8), Margin + 4, TitleHeight - 8, TitleHeight - 8);

    private Rectangle TodayLine
    {
        get
        {
            var grid = GridBounds;
            return new Rectangle(Margin, grid.Bottom + 4, Width - 2 * Margin, Font.Height + 1);
        }
    }

    private DateTime? DateAt(Point p)
    {
        var grid = GridBounds;
        if (!grid.Contains(p)) return null;
        var (cell, row) = CellSize();
        int col = (p.X - grid.X) / cell, r = (p.Y - grid.Y) / row;
        var date = GridStart.AddDays(r * 7 + col);
        return date < _minDate.Date || date > _maxDate.Date ? null : date;
    }

    // --- painting -----------------------------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var (cell, row) = CellSize();
        using (var back = new SolidBrush(BackColor)) g.FillRectangle(back, ClientRectangle);

        // Title: "May 2026" between the arrows.
        var title = new Rectangle(Margin, Margin, Width - 2 * Margin, TitleHeight);
        var culture = CultureInfo.CurrentCulture;
        var titleText = _displayMonth.ToString("MMMM yyyy", culture);
        titleText = char.ToUpper(titleText[0], culture) + titleText.Substring(1);
        using (var bold = new Font(Font, FontStyle.Bold))
            TextRenderer.DrawText(g, titleText, bold, title, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        DrawArrow(g, PreviousButton, left: true, _displayMonth > _minDate);
        DrawArrow(g, NextButton, left: false, _displayMonth.AddMonths(1) <= _maxDate);

        // Day names.
        var grid = GridBounds;
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        for (int i = 0; i < 7; i++)
        {
            var name = names[((int)FirstDay + i) % 7];
            var r = new Rectangle(grid.X + i * cell, grid.Y - row, cell, row);
            TextRenderer.DrawText(g, name, Font, r, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        using (var pen = new Pen(SystemColors.ControlDark))
            g.DrawLine(pen, grid.X, grid.Y - 1, grid.Right, grid.Y - 1);

        // Days.
        var today = TodayDate;
        using var boldFont = new Font(Font, FontStyle.Bold);
        for (int i = 0; i < 42; i++)
        {
            var date = GridStart.AddDays(i);
            var r = new Rectangle(grid.X + i % 7 * cell, grid.Y + i / 7 * row, cell, row);
            bool selected = date >= _selectionStart && date <= _selectionEnd;
            bool trailing = date.Month != _displayMonth.Month;
            bool outOfRange = date < _minDate.Date || date > _maxDate.Date;
            if (selected)
            {
                using var highlight = new SolidBrush(Focused ? SystemColors.Highlight : SystemColors.ControlLight);
                g.FillRectangle(highlight, r);
            }
            var color = selected && Focused ? SystemColors.HighlightText : trailing || outOfRange ? _trailingForeColor : ForeColor;
            TextRenderer.DrawText(g, date.Day.ToString(culture), IsBold(date) ? boldFont : Font, r, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            if (_showTodayCircle && date == today)
            {
                using var pen = new Pen(Color.FromArgb(0, 102, 204));
                g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
            if (_showWeekNumbers && i % 7 == 0)
            {
                int week = culture.Calendar.GetWeekOfYear(date, culture.DateTimeFormat.CalendarWeekRule, FirstDay);
                var wr = new Rectangle(Margin, r.Y, cell, row);
                TextRenderer.DrawText(g, week.ToString(culture), Font, wr, _trailingForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        if (_showToday)
        {
            var line = TodayLine;
            TextRenderer.DrawText(g, SystemStrings.Get("Today:") + " " + today.ToString("d", culture), boldFont, line, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
        base.OnPaint(e);
    }

    private void DrawArrow(Graphics g, Rectangle r, bool left, bool enabled)
    {
        int cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, s = Math.Max(3, r.Height / 4);
        var points = left
            ? new[] { new Point(cx + s / 2, cy - s), new Point(cx - s / 2 - 1, cy), new Point(cx + s / 2, cy + s) }
            : new[] { new Point(cx - s / 2, cy - s), new Point(cx + s / 2 + 1, cy), new Point(cx - s / 2, cy + s) };
        using var brush = new SolidBrush(enabled ? ForeColor : SystemColors.GrayText);
        g.FillPolygon(brush, points);
    }

    // --- input --------------------------------------------------------------------------------------------

    private void Scroll(int months)
    {
        var next = _displayMonth.AddMonths(months);
        if (next < new DateTime(_minDate.Year, _minDate.Month, 1) || next > _maxDate) return;
        _displayMonth = next;
        Invalidate();
        OnDateChanged(new DateRangeEventArgs(_selectionStart, _selectionEnd));
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (CanFocus) Focus();
        int step = _scrollChange == 0 ? 1 : _scrollChange;
        if (PreviousButton.Contains(e.Location)) { Scroll(-step); return; }
        if (NextButton.Contains(e.Location)) { Scroll(step); return; }
        if (_showToday && TodayLine.Contains(e.Location)) { SelectByUser(TodayDate, extend: false); return; }
        if (DateAt(e.Location) is { } date) SelectByUser(date, extend: (ModifierKeys & Keys.Shift) != 0);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        // Dragging extends the range from where the press began.
        if (e.Button == MouseButtons.Left && DateAt(e.Location) is { } date && date != _selectionEnd && date != _selectionStart)
            ExtendTo(date, raiseSelected: false);
    }

    private void SelectByUser(DateTime date, bool extend)
    {
        if (extend) ExtendTo(date, raiseSelected: true);
        else
        {
            SetSelection(date, date);
            _anchor = date;
            OnDateSelected(new DateRangeEventArgs(_selectionStart, _selectionEnd));
        }
    }

    private void ExtendTo(DateTime date, bool raiseSelected)
    {
        var start = _anchor <= date ? _anchor : date;
        var end = _anchor <= date ? date : _anchor;
        if ((end - start).Days >= _maxSelectionCount)
        {
            if (_anchor <= date) end = start.AddDays(_maxSelectionCount - 1);
            else start = end.AddDays(-(_maxSelectionCount - 1));
        }
        var anchor = _anchor;
        SetSelection(start, end);
        _anchor = anchor;
        if (raiseSelected) OnDateSelected(new DateRangeEventArgs(_selectionStart, _selectionEnd));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        var focus = _anchor == _selectionStart ? _selectionEnd : _selectionStart;
        DateTime? to = e.KeyCode switch
        {
            Keys.Left => focus.AddDays(-1),
            Keys.Right => focus.AddDays(1),
            Keys.Up => focus.AddDays(-7),
            Keys.Down => focus.AddDays(7),
            Keys.PageUp => focus.AddMonths(-1),
            Keys.PageDown => focus.AddMonths(1),
            Keys.Home => new DateTime(focus.Year, focus.Month, 1),
            Keys.End => new DateTime(focus.Year, focus.Month, 1).AddMonths(1).AddDays(-1),
            _ => null,
        };
        if (to is not { } date || date < _minDate.Date || date > _maxDate.Date) return;
        SelectByUser(date, extend: e.Shift);
        e.Handled = true;
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    // --- events -------------------------------------------------------------------------------------------

    [Category("Action")]
    [Description("Occurs when the range of dates changes due to user selection, or through next/previous month navigation.")]
    public event DateRangeEventHandler? DateChanged;

    [Category("Action")]
    [Description("Occurs when the user selects a date or a range of dates.")]
    public event DateRangeEventHandler? DateSelected;

    protected virtual void OnDateChanged(DateRangeEventArgs drevent) => DateChanged?.Invoke(this, drevent);

    protected virtual void OnDateSelected(DateRangeEventArgs drevent) => DateSelected?.Invoke(this, drevent);

    public override string ToString() => base.ToString() + ", " + SelectionRange;
}
