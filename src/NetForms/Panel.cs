using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

[DefaultEvent("Paint")]
[DefaultProperty("BorderStyle")]
public class Panel : ScrollableControl
{
    private BorderStyle _borderStyle = BorderStyle.None;

    public Panel()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.AllPaintingInWmPaint, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        TabStop = false;
    }

    protected override Size DefaultSize => new Size(200, 100);

    [Category("Appearance")]
    [Description("Indicates whether the panel should have a border.")]
    [DefaultValue(BorderStyle.None)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            PerformLayout(this, nameof(BorderStyle));
            Invalidate();
        }
    }

    private AutoSizeMode _autoSizeMode = AutoSizeMode.GrowOnly;

    [Category("Layout")]
    [Description("Specifies the mode by which the user interface element automatically resizes itself.")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    [Localizable(true)]
    public virtual AutoSizeMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            if (_autoSizeMode == value) return;
            _autoSizeMode = value;
            if (AutoSize) AdjustSizeToPreferred();
        }
    }

    internal override AutoSizeMode AutoSizeModeCore => _autoSizeMode;

    private int BorderSize => _borderStyle == BorderStyle.None ? 0 : _borderStyle == BorderStyle.FixedSingle ? 1 : 2;

    internal int BorderSizeForLayout => BorderSize;

    [Description("Retrieves the display rectangle of this control.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Rectangle DisplayRectangle
    {
        get
        {
            var r = base.DisplayRectangle;
            int b = BorderSize;
            return new Rectangle(r.X + b, r.Y + b, Math.Max(0, r.Width - 2 * b), Math.Max(0, r.Height - 2 * b));
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        PaintBorder(e.Graphics, ClientRectangle, _borderStyle);
    }

    /// <summary>A panel reads as a plain client area (WinForms' PanelAccessibleObject: role Client, no name of its own).</summary>
    protected override AccessibleObject CreateAccessibilityInstance() => new ControlAccessibleObject(this);

    public override string ToString() => base.ToString() + ", BorderStyle: " + _borderStyle;

    internal static void PaintBorder(Graphics g, Rectangle rect, BorderStyle style)
    {
        if (style == BorderStyle.None || rect.Width <= 0 || rect.Height <= 0) return;
        if (style == BorderStyle.FixedSingle)
        {
            using var pen = new Pen(SystemColors.WindowFrame);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            return;
        }
        // Fixed3D: the classic sunken two-pixel edge.
        using var dark = new Pen(SystemColors.ControlDark);
        using var darkDark = new Pen(SystemColors.ControlDarkDark);
        using var light = new Pen(SystemColors.ControlLight);
        using var lightLight = new Pen(SystemColors.ControlLightLight);
        g.DrawLine(dark, rect.Left, rect.Top, rect.Right - 1, rect.Top);
        g.DrawLine(dark, rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
        g.DrawLine(darkDark, rect.Left + 1, rect.Top + 1, rect.Right - 2, rect.Top + 1);
        g.DrawLine(darkDark, rect.Left + 1, rect.Top + 1, rect.Left + 1, rect.Bottom - 2);
        g.DrawLine(lightLight, rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);
        g.DrawLine(lightLight, rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
        g.DrawLine(light, rect.Left + 1, rect.Bottom - 2, rect.Right - 2, rect.Bottom - 2);
        g.DrawLine(light, rect.Right - 2, rect.Top + 1, rect.Right - 2, rect.Bottom - 2);
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
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

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

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyEventHandler? KeyDown
    {
        add => base.KeyDown += value;
        remove => base.KeyDown -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyPressEventHandler? KeyPress
    {
        add => base.KeyPress += value;
        remove => base.KeyPress -= value;
    }

    [Category("Key")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event KeyEventHandler? KeyUp
    {
        add => base.KeyUp += value;
        remove => base.KeyUp -= value;
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
