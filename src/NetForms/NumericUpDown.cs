using System.ComponentModel;
using System;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

public enum LeftRightAlignment
{
    Left = 0,
    Right = 1,
}

/// <summary>Text box plus spin buttons; the base of NumericUpDown and DomainUpDown.</summary>
public abstract class UpDownBase : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
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

    internal const int ButtonsWidth = 16;

    private readonly TextBox _edit;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private LeftRightAlignment _upDownAlign = LeftRightAlignment.Right;
    private HorizontalAlignment _textAlign = HorizontalAlignment.Left;
    private bool _readOnly;
    private bool _interceptArrowKeys = true;
    private int _hotButton = -1;
    private int _pressedButton = -1;
    private Timer? _repeat;

    protected UpDownBase()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        _edit = new TextBox { BorderStyle = BorderStyle.None, TabStop = false, Name = "upDownEdit" };
        _edit.KeyDown += EditKeyDown;
        _edit.LostFocus += (_, _) => { ValidateEditText(); Invalidate(); };
        _edit.GotFocus += (_, _) => Invalidate();
        _edit.TextChanged += (_, _) => { if (!UpdatingText) UserEdit = true; OnTextBoxTextChanged(EventArgs.Empty); };
        Controls.Add(_edit);
        LayoutEdit();
    }

    protected override Size DefaultSize => new Size(120, PreferredHeight);

    [Category("Appearance")]
    [Description("The background color of the component.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : SystemColors.Window;
        set { base.BackColor = value; _edit.BackColor = value; }
    }

    [Category("Layout")]
    [Description("The preferred height of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PreferredHeight => Font.Height + 7;

    protected TextBox TextBoxControl => _edit;

    protected bool UserEdit { get; set; }

    internal bool UpdatingText { get; private set; }

    protected bool ChangingText { get; set; }

    [Category("Appearance")]
    [Description("Indicates the border style of the up-down control.")]
    [DefaultValue(BorderStyle.Fixed3D)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            LayoutEdit();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Indicates how the up-down control will position the up and down buttons relative to its edit box.")]
    [DefaultValue(LeftRightAlignment.Right)]
    [Localizable(true)]
    public LeftRightAlignment UpDownAlign
    {
        get => _upDownAlign;
        set
        {
            _upDownAlign = value;
            LayoutEdit();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Indicates how the text should be aligned in the edit box.")]
    [DefaultValue(HorizontalAlignment.Left)]
    [Localizable(true)]
    public HorizontalAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            _textAlign = value;
            _edit.TextAlign = value;
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the edit box is read-only.")]
    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            _readOnly = value;
            _edit.ReadOnly = value;
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the up-down control will increment and decrement the value when the UP ARROW and DOWN ARROW keys are pressed.")]
    [DefaultValue(true)]
    public bool InterceptArrowKeys
    {
        get => _interceptArrowKeys;
        set => _interceptArrowKeys = value;
    }

    [Category("Appearance")]
    [Description("The text associated with the control.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => _edit.Text;
        set
        {
            _edit.Text = value;
            ValidateEditText();
        }
    }

    public void Select(int start, int length) => _edit.Select(start, length);

    public abstract void UpButton();

    public abstract void DownButton();

    protected abstract void UpdateEditText();

    protected virtual void ValidateEditText() { }

    protected virtual void OnTextBoxTextChanged(EventArgs e) { }

    /// <summary>Set the text box text without marking the change as a user edit.</summary>
    protected void SetEditText(string text)
    {
        UpdatingText = true;
        try
        {
            if (_edit.Text != text) _edit.Text = text;
        }
        finally
        {
            UpdatingText = false;
        }
    }

    private Rectangle ButtonsRectangle => _upDownAlign == LeftRightAlignment.Right
        ? new Rectangle(Width - 1 - ButtonsWidth, 1, ButtonsWidth, Math.Max(0, Height - 2))
        : new Rectangle(1, 1, ButtonsWidth, Math.Max(0, Height - 2));

    private Rectangle UpRectangle
    {
        get
        {
            var r = ButtonsRectangle;
            return new Rectangle(r.X, r.Y, r.Width, r.Height / 2);
        }
    }

    private Rectangle DownRectangle
    {
        get
        {
            var r = ButtonsRectangle;
            return new Rectangle(r.X, r.Y + r.Height / 2, r.Width, r.Height - r.Height / 2);
        }
    }

    private void LayoutEdit()
    {
        int x = _upDownAlign == LeftRightAlignment.Right ? 3 : ButtonsWidth + 3;
        int h = _edit.PreferredHeight;
        _edit.Bounds = new Rectangle(x, Math.Max(1, (Height - h) / 2), Math.Max(0, Width - ButtonsWidth - 5), h);
    }

    protected override void OnResize(EventArgs e)
    {
        LayoutEdit();
        base.OnResize(e);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        _edit.Font = Font;
        if (Height != PreferredHeight) Height = PreferredHeight;
        LayoutEdit();
    }

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        height = PreferredHeight;
        base.SetBoundsCore(x, y, width, height, specified);
    }

    private void EditKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_interceptArrowKeys) return;
        if (e.KeyCode == Keys.Up) { UpButton(); e.Handled = true; }
        else if (e.KeyCode == Keys.Down) { DownButton(); e.Handled = true; }
        else if (e.KeyCode == Keys.Return) { ValidateEditText(); e.Handled = true; }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        if (_edit.CanFocus) { _edit.Focus(); _edit.SelectAll(); }
        base.OnGotFocus(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtonsLeft)
        {
            if (UpRectangle.Contains(e.Location)) { _pressedButton = 0; Spin(); StartRepeat(); }
            else if (DownRectangle.Contains(e.Location)) { _pressedButton = 1; Spin(); StartRepeat(); }
            if (_edit.CanFocus) _edit.Focus();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    private static MouseButtons MouseButtonsLeft => MouseButtons.Left;

    private void Spin()
    {
        if (_pressedButton == 0) UpButton();
        else if (_pressedButton == 1) DownButton();
    }

    private void StartRepeat()
    {
        _repeat ??= new Timer();
        _repeat.Tick -= RepeatTick;
        _repeat.Tick += RepeatTick;
        _repeat.Interval = 400;
        _repeat.Start();
    }

    private void RepeatTick(object? sender, EventArgs e)
    {
        _repeat!.Interval = 70;
        Spin();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _repeat?.Stop();
        _pressedButton = -1;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int hot = UpRectangle.Contains(e.Location) ? 0 : DownRectangle.Contains(e.Location) ? 1 : -1;
        if (hot != _hotButton)
        {
            _hotButton = hot;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hotButton = -1;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (e.Delta > 0) UpButton(); else if (e.Delta < 0) DownButton();
        base.OnMouseWheel(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _edit.Enabled = Enabled;
        base.OnEnabledChanged(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(Enabled ? BackColor : SystemColors.Control);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        bool enabled = Enabled;
        PaintSpinButton(g, UpRectangle, 0, up: true, enabled);
        PaintSpinButton(g, DownRectangle, 1, up: false, enabled);

        if (_borderStyle != BorderStyle.None)
        {
            var rect = ClientRectangle;
            var border = !enabled ? Theme.ButtonBorderDisabled : _edit.Focused || Focused ? Theme.WindowBorderFocused : Theme.WindowBorder;
            using var pen = new Pen(border);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        }
        base.OnPaint(e);
    }

    private void PaintSpinButton(Graphics g, Rectangle rect, int index, bool up, bool enabled)
    {
        var face = !enabled ? Theme.ButtonFaceDisabled : _pressedButton == index ? Theme.ButtonFacePressed : _hotButton == index ? Theme.ButtonFaceHot : Theme.ButtonFace;
        using (var b = new SolidBrush(face)) g.FillRectangle(b, rect);
        using (var p = new Pen(Theme.ButtonBorder)) g.DrawRectangle(p, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        int cx = rect.X + rect.Width / 2, cy = rect.Y + rect.Height / 2;
        using var pen = new Pen(enabled ? Theme.ScrollArrow : Theme.ScrollThumbDisabled, 1.5f);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (up) g.DrawLines(pen, new[] { new PointF(cx - 3, cy + 1.5f), new PointF(cx, cy - 1.5f), new PointF(cx + 3, cy + 1.5f) });
        else g.DrawLines(pen, new[] { new PointF(cx - 3, cy - 1.5f), new PointF(cx, cy + 1.5f), new PointF(cx + 3, cy - 1.5f) });
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _repeat?.Dispose();
        base.Dispose(disposing);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? MouseEnter
    {
        add => base.MouseEnter += value;
        remove => base.MouseEnter -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? MouseHover
    {
        add => base.MouseHover += value;
        remove => base.MouseHover -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? MouseLeave
    {
        add => base.MouseLeave += value;
        remove => base.MouseLeave -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseMove
    {
        add => base.MouseMove += value;
        remove => base.MouseMove -= value;
    }
}

[DefaultEvent("ValueChanged")]
[DefaultProperty("Value")]
public class NumericUpDown : UpDownBase, ISupportInitialize
{
    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _increment = 1m;
    private int _decimalPlaces;
    private bool _thousandsSeparator;
    private bool _hexadecimal;
    private bool _initializing;

    public NumericUpDown()
    {
        UpdateEditText();
    }

    [Category("Action")]
    [Description("Occurs when the value in the up-down control changes.")]
    public event EventHandler? ValueChanged;

    [Category("Appearance")]
    [Description("The current value of the numeric up-down control.")]
    public decimal Value
    {
        get
        {
            if (UserEdit) ValidateEditText();
            return _value;
        }
        set
        {
            if (value < _minimum || value > _maximum)
            {
                if (!_initializing) throw new ArgumentOutOfRangeException(nameof(value), $"Value must be between {_minimum} and {_maximum}.");
            }
            if (_value == value) return;
            _value = value;
            UpdateEditText();
            OnValueChanged(EventArgs.Empty);
        }
    }

    [Category("Data")]
    [Description("Indicates the minimum value for the numeric up-down control.")]
    public decimal Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            if (_maximum < value) _maximum = value;
            Value = Math.Clamp(_value, _minimum, _maximum);
            UpdateEditText();
        }
    }

    [Category("Data")]
    [Description("Indicates the maximum value for the numeric up-down control.")]
    public decimal Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            if (_minimum > value) _minimum = value;
            Value = Math.Clamp(_value, _minimum, _maximum);
            UpdateEditText();
        }
    }

    [Category("Data")]
    [Description("Indicates the amount to increment or decrement on each button click.")]
    public decimal Increment
    {
        get => _increment;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _increment = value;
        }
    }

    internal bool ShouldSerializeIncrement() => _increment != 1m;

    internal bool ShouldSerializeMaximum() => _maximum != 100m;

    internal bool ShouldSerializeMinimum() => _minimum != 0m;

    internal bool ShouldSerializeValue() => _value != 0m;

    public void ResetIncrement() => Increment = 1m;

    public void ResetMaximum() => Maximum = 100m;

    public void ResetMinimum() => Minimum = 0m;

    public void ResetValue() => Value = 0m;

    [Category("Data")]
    [Description("Indicates the number of decimal places to display.")]
    [DefaultValue(0)]
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            if (value < 0 || value > 99) throw new ArgumentOutOfRangeException(nameof(value));
            _decimalPlaces = value;
            UpdateEditText();
        }
    }

    [Category("Data")]
    [Description("Indicates whether the thousands separator will be inserted between every three decimal digits.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool ThousandsSeparator
    {
        get => _thousandsSeparator;
        set
        {
            _thousandsSeparator = value;
            UpdateEditText();
        }
    }

    [Category("Appearance")]
    [Description("Indicates whether the numeric up-down should display its value in hexadecimal.")]
    [DefaultValue(false)]
    public bool Hexadecimal
    {
        get => _hexadecimal;
        set
        {
            _hexadecimal = value;
            UpdateEditText();
        }
    }

    public void BeginInit() => _initializing = true;

    public void EndInit()
    {
        _initializing = false;
        _value = Math.Clamp(_value, _minimum, _maximum);
        UpdateEditText();
    }

    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    public override void UpButton()
    {
        if (ReadOnly) return;
        if (UserEdit) ValidateEditText();
        var next = _value + _increment;
        if (next > _maximum) next = _maximum;
        if (next != _value) Value = next;
        Select(0, Text.Length);
    }

    public override void DownButton()
    {
        if (ReadOnly) return;
        if (UserEdit) ValidateEditText();
        var next = _value - _increment;
        if (next < _minimum) next = _minimum;
        if (next != _value) Value = next;
        Select(0, Text.Length);
    }

    protected override void UpdateEditText()
    {
        string text;
        if (_hexadecimal)
        {
            text = ((long)Math.Round(_value)).ToString("X", CultureInfo.CurrentCulture);
        }
        else
        {
            text = _value.ToString((_thousandsSeparator ? "N" : "F") + _decimalPlaces, CultureInfo.CurrentCulture);
        }
        UserEdit = false;
        SetEditText(text);
    }

    protected override void ValidateEditText()
    {
        if (!UserEdit) return;
        UserEdit = false;
        decimal parsed;
        var text = Text.Trim();
        bool ok = _hexadecimal
            ? long.TryParse(text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var hex) && (parsed = hex) == hex
            : decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed);
        if (!ok)
        {
            UpdateEditText();
            return;
        }
        parsed = _hexadecimal ? long.Parse(text, NumberStyles.HexNumber, CultureInfo.CurrentCulture) : decimal.Parse(text, NumberStyles.Number, CultureInfo.CurrentCulture);
        parsed = Math.Clamp(parsed, _minimum, _maximum);
        if (parsed != _value)
        {
            _value = parsed;
            OnValueChanged(EventArgs.Empty);
        }
        UpdateEditText();
    }

    protected override void OnTextBoxTextChanged(EventArgs e)
    {
        base.OnTextBoxTextChanged(e);
        OnTextChanged(e);
    }

    public override string ToString() => base.ToString() + $", Minimum = {_minimum}, Maximum = {_maximum}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }
}
