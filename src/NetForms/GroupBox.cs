using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// A captioned frame. The frame runs through the caption's vertical middle, the top line
/// is interrupted 5px either side of the caption which starts 8px in (the GoForms
/// geometry); the display rectangle is inset by the caption height and Padding, as in WinForms.
/// </summary>
[DefaultEvent("Enter")]
public class GroupBox : Control
{
    private FlatStyle _flatStyle = FlatStyle.Standard;

    public GroupBox()
    {
        SetStyle(ControlStyles.ContainerControl | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    protected override Size DefaultSize => new Size(200, 100);

    protected override Padding DefaultPadding => new Padding(3);

    [Category("Appearance")]
    [Description("Determines the appearance of the control when a user moves the mouse over the control and clicks.")]
    [DefaultValue(FlatStyle.Standard)]
    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set
        {
            _flatStyle = value;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Specifies whether text rendering should be compatible with previous releases of Windows Forms.")]
    [DefaultValue(false)]
    public bool UseCompatibleTextRendering { get; set; }

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

    [Description("Retrieves the display rectangle of this control.")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Rectangle DisplayRectangle
    {
        get
        {
            var p = Padding;
            int top = Font.Height;
            return new Rectangle(p.Left, top + p.Top, Math.Max(0, Width - p.Horizontal), Math.Max(0, Height - top - p.Vertical));
        }
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        PerformLayout(this, nameof(Font));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Width <= 0 || Height <= 0) return;
        var lineColor = _flatStyle == FlatStyle.Flat ? ForeColor : Theme.GroupBoxBorder;
        var textColor = Enabled ? ForeColor : Theme.DisabledText;
        var flags = RightToLeft == RightToLeft.Yes ? TextFormatFlags.Right : TextFormatFlags.Left;
        GroupBoxRenderer.DrawFrame(e.Graphics, new Rectangle(0, 0, Width, Height), Text, Font, textColor, lineColor, flags);
        base.OnPaint(e);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new GroupBoxAccessibleObject(this);

    /// <summary>A group box reads as a grouping named by its caption, as in WinForms.</summary>
    private sealed class GroupBoxAccessibleObject : ControlAccessibleObject
    {
        public GroupBoxAccessibleObject(GroupBox owner) : base(owner)
        {
        }

        public override AccessibleRole Role => Owner.AccessibleRole != AccessibleRole.Default ? Owner.AccessibleRole : AccessibleRole.Grouping;

        // The caption, without its mnemonic, unless AccessibleName says otherwise.
        public override string? Name => Owner.AccessibleName ?? base.Name;
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool AllowDrop { get => base.AllowDrop; set => base.AllowDrop = value; }

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

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

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }
}
