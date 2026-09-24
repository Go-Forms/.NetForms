using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// An empty control for composing other controls into a reusable one — the base class the designer
/// offers for "Add User Control". Like WinForms: 150×150, a border style, a Load event raised once
/// when the control is first created, and it takes part in tab order as a container.
/// </summary>
[DefaultEvent(nameof(Load))]
public class UserControl : ContainerControl
{
    private BorderStyle _borderStyle = BorderStyle.None;
    private AutoSizeMode _autoSizeMode = AutoSizeMode.GrowOnly;
    private bool _loaded;

    public UserControl()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override Size DefaultSize => new Size(150, 150);

    [Category("Appearance")]
    [Description("Indicates whether the user control should have a border.")]
    [DefaultValue(BorderStyle.None)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(BorderStyle));
            if (_borderStyle == value) return;
            _borderStyle = value;
            PerformLayout(this, nameof(BorderStyle));
            Invalidate();
        }
    }

    [Category("Layout")]
    [Description("Specifies the mode by which the user interface element automatically resizes itself.")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    [Localizable(true)]
    [Browsable(true)]
    public AutoSizeMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(AutoSizeMode));
            if (_autoSizeMode == value) return;
            _autoSizeMode = value;
            if (AutoSize) AdjustSizeToPreferred();
        }
    }

    internal override AutoSizeMode AutoSizeModeCore => _autoSizeMode;

    private int BorderSize => _borderStyle == BorderStyle.None ? 0 : _borderStyle == BorderStyle.FixedSingle ? 1 : 2;

    /// <summary>The border is non-client in Win32; here it is carved out of the display rectangle, as for <see cref="Panel"/>.</summary>
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

    [Category("Behavior")]
    [Description("Occurs whenever the user loads the control.")]
    public event EventHandler? Load;

    protected virtual void OnLoad(EventArgs e) => Load?.Invoke(this, e);

    /// <summary>WinForms raises Load from OnCreateControl, once.</summary>
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        RaiseLoadOnce();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RaiseLoadOnce();
    }

    private void RaiseLoadOnce()
    {
        if (_loaded) return;
        _loaded = true;
        OnLoad(EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Panel.PaintBorder(e.Graphics, ClientRectangle, _borderStyle);
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
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override AutoValidate AutoValidate { get => base.AutoValidate; set => base.AutoValidate = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? AutoValidateChanged
    {
        add => base.AutoValidateChanged += value;
        remove => base.AutoValidateChanged -= value;
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
