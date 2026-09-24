using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

[DefaultEvent("Scroll")]
[DefaultProperty("Value")]
public abstract class ScrollBar : Control
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
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    private readonly ScrollBarCore _core;

    private protected ScrollBar(bool vertical)
    {
        _core = new ScrollBarCore(this, vertical, SetValueFromUser);
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    /// <summary>A scroll bar hugs whatever it is attached to: no margin, as in WinForms.</summary>
    protected override Padding DefaultMargin => Padding.Empty;

    internal bool IsVertical => _core.Vertical;

    [Category("Action")]
    [Description("Occurs when the user moves the scroll box.")]
    public event ScrollEventHandler? Scroll;

    [Category("Action")]
    [Description("Occurs when the value of the control changes.")]
    public event EventHandler? ValueChanged;

    [Category("Behavior")]
    [Description("The lower limit value of the scrollable range.")]
    [DefaultValue(0)]
    public int Minimum
    {
        get => _core.Minimum;
        set { _core.Minimum = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("The upper limit value of the scrollable range.")]
    [DefaultValue(100)]
    public int Maximum
    {
        get => _core.Maximum;
        set { _core.Maximum = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("The value that the scroll box position represents.")]
    [DefaultValue(0)]
    public int Value
    {
        get => _core.Value;
        set
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value), "Value must be between Minimum and Maximum.");
            if (_core.Value == value) return;
            _core.Value = value;
            OnValueChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("The amount by which the scroll box position changes when the user clicks a scroll arrow or presses an arrow key.")]
    [DefaultValue(1)]
    public int SmallChange
    {
        get => _core.SmallChange;
        set => _core.SmallChange = value;
    }

    [Category("Behavior")]
    [Description("The amount by which the scroll box position changes when the user clicks in the scroll bar or presses the PAGE UP or PAGE DOWN keys.")]
    [DefaultValue(10)]
    public int LargeChange
    {
        get => _core.LargeChange;
        set { _core.LargeChange = value; Invalidate(); }
    }

    protected virtual void OnScroll(ScrollEventArgs se) => Scroll?.Invoke(this, se);
    protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

    private void SetValueFromUser(int newValue, ScrollEventType type)
    {
        int old = _core.Value;
        var e = new ScrollEventArgs(type, old, newValue, _core.Vertical ? ScrollOrientation.VerticalScroll : ScrollOrientation.HorizontalScroll);
        OnScroll(e);
        newValue = Math.Clamp(e.NewValue, Minimum, _core.MaxValue);
        if (newValue != old)
        {
            _core.Value = newValue;
            OnValueChanged(EventArgs.Empty);
        }
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        _core.Enabled = Enabled;
        base.OnEnabledChanged(e);
    }

    protected override void OnResize(EventArgs e)
    {
        _core.Bounds = ClientRectangle;
        base.OnResize(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _core.MouseDown(e.Location);
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        _core.MouseMove(e.Location, (e.Button & MouseButtons.Left) != 0);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) _core.MouseUp(e.Location);
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _core.MouseLeave();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        _core.MouseWheel(e.Delta);
        base.OnMouseWheel(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        _core.Bounds = ClientRectangle;
        _core.Enabled = Enabled;
        _core.Paint(e.Graphics);
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _core.Dispose();
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + $", Minimum: {Minimum}, Maximum: {Maximum}, Value: {Value}";

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor { get => base.BackColor; set => base.BackColor = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Font Font { get => base.Font; set => base.Font = value; }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ImeMode ImeMode { get => base.ImeMode; set => base.ImeMode = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? BackColorChanged
    {
        add => base.BackColorChanged += value;
        remove => base.BackColorChanged -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Click
    {
        add => base.Click += value;
        remove => base.Click -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DoubleClick
    {
        add => base.DoubleClick += value;
        remove => base.DoubleClick -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? FontChanged
    {
        add => base.FontChanged += value;
        remove => base.FontChanged -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseClick
    {
        add => base.MouseClick += value;
        remove => base.MouseClick -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseDoubleClick
    {
        add => base.MouseDoubleClick += value;
        remove => base.MouseDoubleClick -= value;
    }

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseDown
    {
        add => base.MouseDown += value;
        remove => base.MouseDown -= value;
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

    [Category("Mouse")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseUp
    {
        add => base.MouseUp += value;
        remove => base.MouseUp -= value;
    }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event PaintEventHandler? Paint
    {
        add => base.Paint += value;
        remove => base.Paint -= value;
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

public class HScrollBar : ScrollBar
{
    public HScrollBar() : base(vertical: false) { }

    protected override Size DefaultSize => new Size(80, ScrollBarCore.Thickness);
}

public class VScrollBar : ScrollBar
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? RightToLeftChanged
    {
        add => base.RightToLeftChanged += value;
        remove => base.RightToLeftChanged -= value;
    }

    public VScrollBar() : base(vertical: true) { }

    protected override Size DefaultSize => new Size(ScrollBarCore.Thickness, 80);

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override RightToLeft RightToLeft { get => base.RightToLeft; set => base.RightToLeft = value; }
}
