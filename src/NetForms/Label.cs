using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

public class Label : Control
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

    private ContentAlignment _textAlign = ContentAlignment.TopLeft;
    private BorderStyle _borderStyle = BorderStyle.None;
    private FlatStyle _flatStyle = FlatStyle.Standard;
    private bool _autoEllipsis;
    private bool _useMnemonic = true;

    public Label()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.FixedHeight | ControlStyles.Selectable, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        TabStop = false;
    }

    protected override Size DefaultSize => new Size(100, 23);

    protected override Padding DefaultMargin => new Padding(3, 0, 3, 0);

    [Category("Layout")]
    [Description("Enables automatic resizing based on font size. Note that this is only valid for label controls that do not wrap text.")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    [Category("Appearance")]
    [Description("Determines the position of the text within the label.")]
    [DefaultValue(typeof(ContentAlignment), "TopLeft")]
    [Localizable(true)]
    public virtual ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value) return;
            _textAlign = value;
            OnTextAlignChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Determines if the label has a visible border.")]
    [DefaultValue(BorderStyle.None)]
    public virtual BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            AdjustSizeToPreferred();
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Determines the appearance of the control when a user moves the mouse over the control and clicks.")]
    [DefaultValue(FlatStyle.Standard)]
    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set { _flatStyle = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Enables the automatic handling of text that extends beyond the width of the label control.")]
    [DefaultValue(false)]
    public bool AutoEllipsis
    {
        get => _autoEllipsis;
        set { _autoEllipsis = value; Invalidate(); }
    }

    [Category("Appearance")]
    [Description("If true, the first character preceded by an ampersand (&&) will be used as the label's mnemonic key.")]
    [DefaultValue(true)]
    public bool UseMnemonic
    {
        get => _useMnemonic;
        set { _useMnemonic = value; Invalidate(); }
    }

    [Category("Behavior")]
    [Description("Specifies whether text rendering should be compatible with previous releases of Windows Forms.")]
    [DefaultValue(false)]
    public bool UseCompatibleTextRendering { get; set; }

    [Category("Appearance")]
    [Description("The image that will be displayed on the control.")]
    [Localizable(true)]
    public Image? Image { get; set; }

    [Category("Property Changed")]
    [Description("Event raised when the value of the TextAlign property is changed on Label.")]
    public event EventHandler? TextAlignChanged;

    protected virtual void OnTextAlignChanged(EventArgs e) => TextAlignChanged?.Invoke(this, e);

    private int BorderSize => _borderStyle == BorderStyle.None ? 0 : 1;

    internal TextFormatFlags CreateTextFormatFlags()
    {
        var flags = TextFormatFlags.WordBreak;
        switch (_textAlign)
        {
            case ContentAlignment.TopCenter:
            case ContentAlignment.MiddleCenter:
            case ContentAlignment.BottomCenter:
                flags |= TextFormatFlags.HorizontalCenter;
                break;
            case ContentAlignment.TopRight:
            case ContentAlignment.MiddleRight:
            case ContentAlignment.BottomRight:
                flags |= TextFormatFlags.Right;
                break;
        }
        switch (_textAlign)
        {
            case ContentAlignment.MiddleLeft:
            case ContentAlignment.MiddleCenter:
            case ContentAlignment.MiddleRight:
                flags |= TextFormatFlags.VerticalCenter;
                break;
            case ContentAlignment.BottomLeft:
            case ContentAlignment.BottomCenter:
            case ContentAlignment.BottomRight:
                flags |= TextFormatFlags.Bottom;
                break;
        }
        if (!_useMnemonic) flags |= TextFormatFlags.NoPrefix;
        if (_autoEllipsis) flags |= TextFormatFlags.EndEllipsis;
        return flags;
    }

    internal bool ShouldSerializeImage() => Image != null;

    /// <summary>The vertical breathing room WinForms leaves around an AutoSize label's text.</summary>
    private const int TextBuffer = 6;

    public override Size GetPreferredSize(Size proposedSize)
    {
        var padding = Padding;
        var border = new Size(BorderSize * 2, BorderSize * 2);
        if (string.IsNullOrEmpty(Text))
        {
            return new Size(padding.Horizontal + border.Width, Font.Height + padding.Vertical + border.Height);
        }

        var flags = CreateTextFormatFlags();
        if (proposedSize.Width <= 0 || proposedSize.Width == int.MaxValue)
        {
            // Unbounded: a single line per paragraph.
            flags &= ~TextFormatFlags.WordBreak;
        }
        var text = TextRenderer.MeasureText(Text, Font, proposedSize, flags);
        // WinForms keeps a vertical buffer ("else the Text gets clipped for AutoSize = true") only on its
        // GDI+ path; with TextRenderer - what an application uses - the preferred size is the measured
        // text (Segoe UI 9: "label1" 38x15, as VS writes it). Checked against WinForms, decision 110.
        int buffer = UseCompatibleTextRendering ? TextBuffer : 0;
        return new Size(text.Width + padding.Horizontal + border.Width, text.Height + buffer + padding.Vertical + border.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var rect = ClientRectangle;

        if (_borderStyle != BorderStyle.None)
        {
            using var pen = new Pen(_borderStyle == BorderStyle.Fixed3D ? SystemColors.ControlDark : SystemColors.WindowFrame);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            rect.Inflate(-BorderSize, -BorderSize);
        }

        var pad = Padding;
        rect = new Rectangle(rect.X + pad.Left, rect.Y + pad.Top, Math.Max(0, rect.Width - pad.Horizontal), Math.Max(0, rect.Height - pad.Vertical));

        var color = Enabled ? ForeColor : Theme.DisabledText;
        TextRenderer.DrawText(g, Text, Font, rect, color, CreateTextFormatFlags());

        base.OnPaint(e);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ImeMode ImeMode { get => base.ImeMode; set => base.ImeMode = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
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
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }
}
