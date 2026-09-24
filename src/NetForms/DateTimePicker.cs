using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace System.Windows.Forms;

public enum DateTimePickerFormat
{
    Long = 0x0001,
    Short = 0x0002,
    Time = 0x0004,
    Custom = 0x0008,
}

/// <summary>
/// A date/time field (Ф6.К): the value shown in its format, split into fields - day, month, year, hour,
/// minute, second, AM/PM - that the keyboard selects (Left/Right) and changes (Up/Down, digits); a drop-down
/// MonthCalendar (F4, Alt+Down, the button), or up-down buttons with ShowUpDown; an optional check box
/// (ShowCheckBox) whose state is <see cref="Checked"/>.
/// </summary>
[DefaultProperty("Value")]
[DefaultEvent("ValueChanged")]
[DefaultBindingProperty("Value")]
public class DateTimePicker : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Color BackColor
    {
        get => base.BackColor;
        set => base.BackColor = value;
    }

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
    public override Color ForeColor
    {
        get => base.ForeColor;
        set => base.ForeColor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding
    {
        get => base.Padding;
        set => base.Padding = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackColorChanged
    {
        add => base.BackColorChanged += value;
        remove => base.BackColorChanged -= value;
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
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
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

    private static readonly DateTime s_minDateTime = new(1753, 1, 1);
    private static readonly DateTime s_maxDateTime = new(9998, 12, 31);

    public static readonly DateTime MinDateTime = s_minDateTime;
    public static readonly DateTime MaxDateTime = s_maxDateTime;

    protected static readonly Color DefaultMonthBackColor = SystemColors.Window;
    protected static readonly Color DefaultTitleBackColor = SystemColors.ActiveCaption;
    protected static readonly Color DefaultTitleForeColor = SystemColors.ActiveCaptionText;
    protected static readonly Color DefaultTrailingForeColor = SystemColors.GrayText;

    private DateTime _value = DateTime.Now;
    private DateTime _minDate = s_minDateTime;
    private DateTime _maxDate = s_maxDateTime;
    private DateTimePickerFormat _format = DateTimePickerFormat.Long;
    private string? _customFormat;
    private bool _showCheckBox;
    private bool _checked = true;
    private bool _showUpDown;
    private LeftRightAlignment _dropDownAlign = LeftRightAlignment.Left;
    private int _field = -1;
    private string _typed = string.Empty;
    private CalendarPopup? _popup;
    private bool _droppedDown;
    private Font? _calendarFont;
    private Color _calendarForeColor = SystemColors.ControlText;
    private Color _calendarMonthBackground = DefaultMonthBackColor;
    private Color _calendarTitleBackColor = DefaultTitleBackColor;
    private Color _calendarTitleForeColor = DefaultTitleForeColor;
    private Color _calendarTrailingForeColor = DefaultTrailingForeColor;

    public DateTimePicker()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.Selectable | ControlStyles.ResizeRedraw | ControlStyles.FixedHeight, true);
        SetStyle(ControlStyles.StandardClick, false);
        TabStop = true;
    }

    protected override Size DefaultSize => new(200, PreferredHeight);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PreferredHeight => Font.Height + 7;

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified) =>
        base.SetBoundsCore(x, y, width, PreferredHeight, specified);

    // --- the value ----------------------------------------------------------------------------------------

    [Category("Behavior")]
    [Bindable(true)]
    [RefreshProperties(RefreshProperties.All)]
    [Description("The current date/time value for this control.")]
    public DateTime Value
    {
        get => _value;
        set
        {
            if (value < _minDate || value > _maxDate)
                throw new ArgumentOutOfRangeException(nameof(value), $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'MinDate' and 'MaxDate'.");
            bool changed = value != _value;
            _value = value;
            if (_showCheckBox && !_checked)
            {
                _checked = true;
                changed = true;
            }
            if (!changed) return;
            Invalidate();
            OnValueChanged(EventArgs.Empty);
            OnTextChanged(EventArgs.Empty);
        }
    }

    internal bool ShouldSerializeValue() => false; // "now" at design time: written by the designer only when set explicitly

    [Category("Behavior")]
    [Description("The minimum date/time value that can be selected by the user.")]
    public DateTime MinDate
    {
        get => _minDate;
        set
        {
            if (value < s_minDateTime) throw new ArgumentOutOfRangeException(nameof(value));
            if (value > _maxDate) throw new ArgumentOutOfRangeException(nameof(value));
            _minDate = value;
            if (_value < value) Value = value;
        }
    }

    internal bool ShouldSerializeMinDate() => _minDate != s_minDateTime;

    [Category("Behavior")]
    [Description("The maximum date/time value that can be selected by the user.")]
    public DateTime MaxDate
    {
        get => _maxDate;
        set
        {
            if (value > s_maxDateTime) throw new ArgumentOutOfRangeException(nameof(value));
            if (value < _minDate) throw new ArgumentOutOfRangeException(nameof(value));
            _maxDate = value;
            if (_value > value) Value = value;
        }
    }

    internal bool ShouldSerializeMaxDate() => _maxDate != s_maxDateTime;

    public DateTime MinimumDateTime => s_minDateTime;

    public DateTime MaximumDateTime => s_maxDateTime;

    [Category("Appearance")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Description("Determines whether dates and times are displayed using standard or custom formatting.")]
    public DateTimePickerFormat Format
    {
        get => _format;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(DateTimePickerFormat));
            if (_format == value) return;
            _format = value;
            _field = -1;
            Invalidate();
            OnFormatChanged(EventArgs.Empty);
        }
    }

    internal bool ShouldSerializeFormat() => _format != DateTimePickerFormat.Long;

    [Category("Behavior")]
    [DefaultValue(null)]
    [Localizable(true)]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Description("The custom format string used to format the date and/or time displayed in the control.")]
    public string? CustomFormat
    {
        get => _customFormat;
        set
        {
            if (_customFormat == value) return;
            _customFormat = value;
            _field = -1;
            Invalidate();
            if (_format == DateTimePickerFormat.Custom) OnFormatChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [DefaultValue(false)]
    [Description("Determines whether a check box is displayed in the control. When the box is unchecked, no value is selected.")]
    public bool ShowCheckBox
    {
        get => _showCheckBox;
        set
        {
            _showCheckBox = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Bindable(true)]
    [Description("Determines if the check box is checked, indicating that the user has selected a value.")]
    public bool Checked
    {
        get => !_showCheckBox || _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            if (_showCheckBox) OnValueChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [DefaultValue(false)]
    [Description("Controls whether an up-down (spin) button is used to modify the date/time value instead of a drop-down calendar.")]
    public bool ShowUpDown
    {
        get => _showUpDown;
        set
        {
            _showUpDown = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [DefaultValue(LeftRightAlignment.Left)]
    [Localizable(true)]
    [Description("Controls the alignment of the drop-down calendar to the DateTimePicker control.")]
    public LeftRightAlignment DropDownAlign
    {
        get => _dropDownAlign;
        set => _dropDownAlign = value;
    }

    [Localizable(true)]
    [Category("Appearance")]
    [AmbientValue(null)]
    [Description("The font used to display the calendar.")]
    public Font CalendarFont
    {
        get => _calendarFont ?? Font;
        set => _calendarFont = value;
    }

    internal bool ShouldSerializeCalendarFont() => _calendarFont != null;

    [Category("Appearance")]
    [Description("The color used to display text within a month in the calendar.")]
    public Color CalendarForeColor { get => _calendarForeColor; set => _calendarForeColor = value; }

    [Category("Appearance")]
    [Description("The background color displayed within the month.")]
    public Color CalendarMonthBackground { get => _calendarMonthBackground; set => _calendarMonthBackground = value; }

    [Category("Appearance")]
    [Description("The background color displayed in the calendar's title.")]
    public Color CalendarTitleBackColor { get => _calendarTitleBackColor; set => _calendarTitleBackColor = value; }

    [Category("Appearance")]
    [Description("The color used to display text within the calendar's title.")]
    public Color CalendarTitleForeColor { get => _calendarTitleForeColor; set => _calendarTitleForeColor = value; }

    [Category("Appearance")]
    [Description("The color used to display the previous and following months that appear on the month calendar.")]
    public Color CalendarTrailingForeColor { get => _calendarTrailingForeColor; set => _calendarTrailingForeColor = value; }

    internal bool ShouldSerializeCalendarForeColor() => _calendarForeColor != SystemColors.ControlText;
    internal bool ShouldSerializeCalendarMonthBackground() => _calendarMonthBackground != DefaultMonthBackColor;
    internal bool ShouldSerializeCalendarTitleBackColor() => _calendarTitleBackColor != DefaultTitleBackColor;
    internal bool ShouldSerializeCalendarTitleForeColor() => _calendarTitleForeColor != DefaultTitleForeColor;
    internal bool ShouldSerializeCalendarTrailingForeColor() => _calendarTrailingForeColor != DefaultTrailingForeColor;

    [Localizable(true)]
    [DefaultValue(false)]
    [Category("Appearance")]
    [Description("Indicates whether the control layout is right-to-left when the RightToLeft property is set to Yes.")]
    public virtual bool RightToLeftLayout { get; set; }

    /// <summary>The value as displayed; setting it parses the text (culture's rules).</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _value.ToString(Pattern, CultureInfo.CurrentCulture);
        set
        {
            if (string.IsNullOrEmpty(value)) Value = DateTime.Now;
            else Value = DateTime.Parse(value, CultureInfo.CurrentCulture);
        }
    }

    // --- the format, as fields ------------------------------------------------------------------------------

    private string Pattern
    {
        get
        {
            var info = CultureInfo.CurrentCulture.DateTimeFormat;
            return _format switch
            {
                DateTimePickerFormat.Short => info.ShortDatePattern,
                DateTimePickerFormat.Time => info.LongTimePattern,
                DateTimePickerFormat.Custom => string.IsNullOrEmpty(_customFormat) ? info.LongDatePattern : _customFormat,
                _ => info.LongDatePattern,
            };
        }
    }

    private enum FieldKind { Literal, Day, DayName, Month, Year, Hour12, Hour24, Minute, Second, AmPm }

    private readonly record struct Segment(FieldKind Kind, string Format, string Text);

    /// <summary>The value's text split into the pattern's fields and literals (.NET custom format syntax).</summary>
    private List<Segment> Segments()
    {
        var pattern = Pattern;
        var culture = CultureInfo.CurrentCulture;
        var result = new List<Segment>();
        var literal = new StringBuilder();
        void FlushLiteral()
        {
            if (literal.Length == 0) return;
            result.Add(new Segment(FieldKind.Literal, "", literal.ToString()));
            literal.Clear();
        }
        for (int i = 0; i < pattern.Length;)
        {
            char c = pattern[i];
            if (c is '\'' or '"')
            {
                int end = pattern.IndexOf(c, i + 1);
                if (end < 0) end = pattern.Length;
                literal.Append(pattern, i + 1, end - i - 1);
                i = Math.Min(pattern.Length, end + 1);
                continue;
            }
            if (c == '\\' && i + 1 < pattern.Length)
            {
                literal.Append(pattern[i + 1]);
                i += 2;
                continue;
            }
            int run = 1;
            while (i + run < pattern.Length && pattern[i + run] == c) run++;
            var kind = c switch
            {
                'd' => run >= 3 ? FieldKind.DayName : FieldKind.Day,
                'M' => FieldKind.Month,
                'y' => FieldKind.Year,
                'h' => FieldKind.Hour12,
                'H' => FieldKind.Hour24,
                'm' => FieldKind.Minute,
                's' => FieldKind.Second,
                't' => FieldKind.AmPm,
                _ => FieldKind.Literal,
            };
            if (kind == FieldKind.Literal)
            {
                literal.Append(pattern, i, run);
            }
            else
            {
                FlushLiteral();
                var format = pattern.Substring(i, run);
                // A one-letter custom format means a standard one; "%d" makes it a custom field.
                var text = _value.ToString(format.Length == 1 ? "%" + format : format, culture);
                // Formatted alone, "MMMM" is the nominative; next to a day .NET uses the genitive ("23 сентября").
                if (kind == FieldKind.Month && run >= 3 && pattern.Contains('d'))
                {
                    var names = run == 3 ? culture.DateTimeFormat.AbbreviatedMonthGenitiveNames : culture.DateTimeFormat.MonthGenitiveNames;
                    if (!string.IsNullOrEmpty(names[_value.Month - 1])) text = names[_value.Month - 1];
                }
                result.Add(new Segment(kind, format, text));
            }
            i += run;
        }
        FlushLiteral();
        return result;
    }

    private List<int> EditableFields(List<Segment> segments)
    {
        var fields = new List<int>();
        for (int i = 0; i < segments.Count; i++)
            if (segments[i].Kind is not (FieldKind.Literal or FieldKind.DayName)) fields.Add(i);
        return fields;
    }

    private void ChangeField(Segment field, int delta)
    {
        var v = _value;
        try
        {
            v = field.Kind switch
            {
                FieldKind.Day => v.AddDays(delta).Month == v.Month ? v.AddDays(delta) : v.AddDays(delta > 0 ? 1 - v.Day : DateTime.DaysInMonth(v.Year, v.Month) - v.Day),
                FieldKind.Month => v.AddMonths(delta),
                FieldKind.Year => v.AddYears(delta),
                FieldKind.Hour12 or FieldKind.Hour24 => v.AddHours(delta).Date == v.Date ? v.AddHours(delta) : v.AddHours(delta > 0 ? -23 : 23),
                FieldKind.Minute => v.AddMinutes(delta).Hour == v.Hour ? v.AddMinutes(delta) : v.AddMinutes(delta > 0 ? -59 : 59),
                FieldKind.Second => v.AddSeconds(delta).Minute == v.Minute ? v.AddSeconds(delta) : v.AddSeconds(delta > 0 ? -59 : 59),
                FieldKind.AmPm => v.AddHours(v.Hour < 12 ? 12 : -12),
                _ => v,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }
        if (v < _minDate) v = _minDate;
        if (v > _maxDate) v = _maxDate;
        Value = v;
    }

    /// <summary>Digits typed into a field: they accumulate while they still make a valid value, as in Win32.</summary>
    private void TypeDigit(Segment field, char digit)
    {
        _typed += digit;
        int n = int.Parse(_typed, CultureInfo.InvariantCulture);
        var v = _value;
        DateTime? next = null;
        try
        {
            switch (field.Kind)
            {
                case FieldKind.Day when n >= 1 && n <= DateTime.DaysInMonth(v.Year, v.Month): next = new DateTime(v.Year, v.Month, n, v.Hour, v.Minute, v.Second); break;
                case FieldKind.Month when n >= 1 && n <= 12: next = new DateTime(v.Year, n, Math.Min(v.Day, DateTime.DaysInMonth(v.Year, n)), v.Hour, v.Minute, v.Second); break;
                case FieldKind.Year when _typed.Length == 4 || (field.Format.Length <= 2 && _typed.Length == 2):
                    int year = _typed.Length == 2 ? CultureInfo.CurrentCulture.Calendar.ToFourDigitYear(n) : n;
                    next = new DateTime(year, v.Month, Math.Min(v.Day, DateTime.DaysInMonth(year, v.Month)), v.Hour, v.Minute, v.Second);
                    break;
                case FieldKind.Hour24 when n <= 23: next = v.Date.AddHours(n).AddMinutes(v.Minute).AddSeconds(v.Second); break;
                case FieldKind.Hour12 when n >= 1 && n <= 12: next = v.Date.AddHours(n % 12 + (v.Hour >= 12 ? 12 : 0)).AddMinutes(v.Minute).AddSeconds(v.Second); break;
                case FieldKind.Minute when n <= 59: next = new DateTime(v.Year, v.Month, v.Day, v.Hour, n, v.Second); break;
                case FieldKind.Second when n <= 59: next = new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, n); break;
            }
        }
        catch (ArgumentOutOfRangeException)
        {
        }
        if (next is { } value && value >= _minDate && value <= _maxDate) Value = value;
        else _typed = digit.ToString();
        int max = field.Kind == FieldKind.Year ? 4 : 2;
        if (_typed.Length >= max) _typed = string.Empty;
    }

    // --- layout and painting --------------------------------------------------------------------------------

    private Rectangle ButtonRectangle
    {
        get
        {
            int w = _showUpDown ? 16 : SystemInformation.VerticalScrollBarWidth;
            return new Rectangle(Width - 1 - w, 1, w, Height - 2);
        }
    }

    private Rectangle CheckBoxRectangle => new(4, (Height - 13) / 2, 13, 13);

    private int TextLeft => _showCheckBox ? 21 : 3;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;
        using (var back = new SolidBrush(Enabled ? SystemColors.Window : SystemColors.Control)) g.FillRectangle(back, rect);
        using (var pen = new Pen(Focused ? Theme.ButtonBorderDefault : SystemColors.ControlDark)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        if (_showCheckBox)
        {
            var box = CheckBoxRectangle;
            using var pen = new Pen(SystemColors.ControlDarkDark);
            g.FillRectangle(Brushes.White, box);
            g.DrawRectangle(pen, box.X, box.Y, box.Width - 1, box.Height - 1);
            if (_checked)
            {
                using var check = new Pen(SystemColors.ControlText, 2);
                g.DrawLines(check, new[] { new Point(box.X + 3, box.Y + 6), new Point(box.X + 5, box.Y + 9), new Point(box.X + 10, box.Y + 3) });
            }
        }

        var segments = Segments();
        var fields = EditableFields(segments);
        int x = TextLeft;
        var textColor = Enabled && Checked ? ForeColor : SystemColors.GrayText;
        var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter;
        for (int i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            int w = TextRenderer.MeasureText(g, s.Text, Font, new Size(int.MaxValue, int.MaxValue), flags).Width;
            var r = new Rectangle(x, 0, w, Height);
            bool selected = Focused && !_droppedDown && Checked && _field >= 0 && _field < fields.Count && fields[_field] == i;
            if (selected)
            {
                g.FillRectangle(SystemBrushes.Highlight, new Rectangle(r.X, 3, r.Width, Height - 6));
                TextRenderer.DrawText(g, s.Text, Font, r, SystemColors.HighlightText, flags);
            }
            else
            {
                TextRenderer.DrawText(g, s.Text, Font, r, textColor, flags);
            }
            x += w;
        }

        var button = ButtonRectangle;
        if (_showUpDown)
        {
            var up = new Rectangle(button.X, button.Y, button.Width, button.Height / 2);
            var down = new Rectangle(button.X, button.Y + button.Height / 2, button.Width, button.Height - button.Height / 2);
            ControlPaint.DrawScrollButton(g, up, ScrollButton.Up, Enabled ? ButtonState.Normal : ButtonState.Inactive);
            ControlPaint.DrawScrollButton(g, down, ScrollButton.Down, Enabled ? ButtonState.Normal : ButtonState.Inactive);
        }
        else
        {
            int cx = button.X + button.Width / 2, cy = button.Y + button.Height / 2;
            using var brush = new SolidBrush(Enabled ? SystemColors.ControlText : SystemColors.GrayText);
            g.FillPolygon(brush, new[] { new Point(cx - 4, cy - 2), new Point(cx + 4, cy - 2), new Point(cx, cy + 2) });
        }
        base.OnPaint(e);
    }

    // --- input --------------------------------------------------------------------------------------------

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown or Keys.F4
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        if (_droppedDown && _popup != null)
        {
            if (e.KeyCode is Keys.Escape) { CloseDropDown(); e.Handled = true; return; }
            if (e.KeyCode is Keys.Enter) { Commit(_popup.Calendar.SelectionStart); e.Handled = true; return; }
            _popup.Calendar.NavigateFromOwner(e);
            return;
        }
        if (e.KeyCode == Keys.F4 || (e.KeyCode == Keys.Down && e.Alt))
        {
            if (!_showUpDown) ShowCalendar();
            e.Handled = true;
            return;
        }
        if (e.KeyCode == Keys.Space && _showCheckBox)
        {
            Checked = !Checked;
            e.Handled = true;
            return;
        }
        var segments = Segments();
        var fields = EditableFields(segments);
        if (fields.Count == 0) return;
        if (_field < 0) _field = 0;
        switch (e.KeyCode)
        {
            case Keys.Left: _field = (_field - 1 + fields.Count) % fields.Count; _typed = ""; break;
            case Keys.Right: _field = (_field + 1) % fields.Count; _typed = ""; break;
            case Keys.Up: ChangeField(segments[fields[_field]], 1); break;
            case Keys.Down: ChangeField(segments[fields[_field]], -1); break;
            case Keys.Home: _field = 0; break;
            case Keys.End: _field = fields.Count - 1; break;
            default: return;
        }
        e.Handled = true;
        Invalidate();
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || !char.IsDigit(e.KeyChar) || _droppedDown) return;
        var segments = Segments();
        var fields = EditableFields(segments);
        if (fields.Count == 0) return;
        if (_field < 0) _field = 0;
        TypeDigit(segments[fields[_field]], e.KeyChar);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (CanFocus) Focus();
        if (_showCheckBox && CheckBoxRectangle.Contains(e.Location))
        {
            Checked = !Checked;
            return;
        }
        var button = ButtonRectangle;
        if (button.Contains(e.Location))
        {
            if (_showUpDown)
            {
                var segments = Segments();
                var fields = EditableFields(segments);
                if (fields.Count == 0) return;
                if (_field < 0) _field = 0;
                ChangeField(segments[fields[_field]], e.Y < button.Y + button.Height / 2 ? 1 : -1);
                Invalidate();
            }
            else if (_droppedDown) CloseDropDown();
            else ShowCalendar();
            return;
        }
        // A click on a field selects it.
        var segs = Segments();
        var editable = EditableFields(segs);
        using var g = CreateGraphics();
        int x = TextLeft;
        for (int i = 0; i < segs.Count; i++)
        {
            int w = TextRenderer.MeasureText(g, segs[i].Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
            if (e.X >= x && e.X < x + w && editable.IndexOf(i) is var f && f >= 0)
            {
                _field = f;
                _typed = "";
                Invalidate();
                break;
            }
            x += w;
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (_field < 0) _field = 0;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _typed = "";
        Invalidate();
    }

    // --- the drop-down calendar -----------------------------------------------------------------------------

    private void ShowCalendar()
    {
        var form = FindForm();
        if (_droppedDown || form == null || !form.IsWindowCreated) return;
        _popup ??= new CalendarPopup(this);
        var calendar = _popup.Calendar;
        calendar.Font = CalendarFont;
        calendar.ForeColor = _calendarForeColor;
        calendar.BackColor = _calendarMonthBackground;
        calendar.TitleBackColor = _calendarTitleBackColor;
        calendar.TitleForeColor = _calendarTitleForeColor;
        calendar.TrailingForeColor = _calendarTrailingForeColor;
        calendar.MinDate = _minDate;
        calendar.MaxDate = _maxDate;
        calendar.MaxSelectionCount = 1;
        calendar.SetDate(_value.Date);
        var size = calendar.SingleMonthSize;
        var at = PointToScreen(new Point(_dropDownAlign == LeftRightAlignment.Right ? Width - size.Width : 0, Height));
        OnDropDown(EventArgs.Empty);
        _droppedDown = true;
        _popup.ShowAt(this, at, size);
        Invalidate();
    }

    private void CloseDropDown()
    {
        if (!_droppedDown) return;
        _popup?.Dismiss();
    }

    private void PopupDismissed()
    {
        if (!_droppedDown) return;
        _droppedDown = false;
        Invalidate();
        OnCloseUp(EventArgs.Empty);
    }

    /// <summary>A date picked in the calendar: the day changes, the time of day stays.</summary>
    private void Commit(DateTime date)
    {
        var value = date.Date + _value.TimeOfDay;
        if (value < _minDate) value = _minDate;
        if (value > _maxDate) value = _maxDate;
        Value = value;
        CloseDropDown();
        if (CanFocus) Focus();
    }

    private sealed class CalendarPopup : PopupForm
    {
        public CalendarPopup(DateTimePicker owner)
        {
            Calendar = new PopupCalendar { Dock = DockStyle.Fill, TabStop = false };
            Calendar.DateSelected += (_, e) => owner.Commit(e.Start);
            Controls.Add(Calendar);
            Dismissed += (_, _) => owner.PopupDismissed();
        }

        public PopupCalendar Calendar { get; }
    }

    /// <summary>The calendar of the drop-down: keys come from the picker, which keeps the focus.</summary>
    internal sealed class PopupCalendar : MonthCalendar
    {
        public void NavigateFromOwner(KeyEventArgs e) => OnKeyDown(e);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Moving through the days selects without committing; Enter commits (the picker handles it).
            base.OnKeyDown(e);
        }
    }

    // --- events -------------------------------------------------------------------------------------------

    [Category("Action")]
    [Description("Occurs when the value of the control changes.")]
    public event EventHandler? ValueChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the Format property changes.")]
    public event EventHandler? FormatChanged;

    [Category("Action")]
    [Description("Occurs when the drop-down calendar is shown.")]
    public event EventHandler? DropDown;

    [Category("Action")]
    [Description("Occurs when the drop-down calendar is dismissed and disappears.")]
    public event EventHandler? CloseUp;

    [Category("Property Changed")]
    [Description("Occurs when the value of the RightToLeftLayout property changes.")]
    public event EventHandler? RightToLeftLayoutChanged;

    protected virtual void OnValueChanged(EventArgs eventargs) => ValueChanged?.Invoke(this, eventargs);

    protected virtual void OnFormatChanged(EventArgs e) => FormatChanged?.Invoke(this, e);

    protected virtual void OnDropDown(EventArgs eventargs) => DropDown?.Invoke(this, eventargs);

    protected virtual void OnCloseUp(EventArgs eventargs) => CloseUp?.Invoke(this, eventargs);

    protected virtual void OnRightToLeftLayoutChanged(EventArgs e) => RightToLeftLayoutChanged?.Invoke(this, e);

    public override string ToString() => base.ToString() + ", Value: " + _value.ToString(CultureInfo.CurrentCulture);
}
