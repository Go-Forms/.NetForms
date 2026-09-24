using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

public enum PictureBoxSizeMode
{
    Normal = 0,
    StretchImage = 1,
    AutoSize = 2,
    CenterImage = 3,
    Zoom = 4,
}

[DefaultProperty("Image")]
public class PictureBox : Control, ISupportInitialize
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? CausesValidationChanged
    {
        add => base.CausesValidationChanged += value;
        remove => base.CausesValidationChanged -= value;
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
    public new event EventHandler? RightToLeftChanged
    {
        add => base.RightToLeftChanged += value;
        remove => base.RightToLeftChanged -= value;
    }

    private Image? _image;
    private PictureBoxSizeMode _sizeMode = PictureBoxSizeMode.Normal;
    private BorderStyle _borderStyle = BorderStyle.None;

    public PictureBox()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer, true);
        SetStyle(ControlStyles.StandardClick, true);
        TabStop = false;
    }

    protected override Size DefaultSize => new Size(100, 50);

    [Category("Property Changed")]
    [Description("Event raised when the value of the SizeMode property is changed on the PictureBox.")]
    public event EventHandler? SizeModeChanged;

    [Category("Appearance")]
    [Description("The image displayed in the PictureBox.")]
    [Localizable(true)]
    public Image? Image
    {
        get => _image;
        set
        {
            if (ReferenceEquals(_image, value)) return;
            _image = value;
            if (_sizeMode == PictureBoxSizeMode.AutoSize) AdjustToImage();
            Invalidate();
        }
    }

    internal bool ShouldSerializeImage() => _image != null;

    [Category("Asynchronous")]
    [Description("Image to display when the load of another image fails.")]
    [Localizable(true)]
    public Image? ErrorImage { get; set; }

    [Category("Asynchronous")]
    [Description("Image to display while another image is loading.")]
    [Localizable(true)]
    public Image? InitialImage { get; set; }

    [Category("Asynchronous")]
    [Description("Disk or Web location to load image from.")]
    [DefaultValue(null)]
    [Localizable(true)]
    public string? ImageLocation { get; set; }

    [Category("Asynchronous")]
    [Description("Controls whether processing will stop until the image is loaded.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool WaitOnLoad { get; set; }

    [Category("Behavior")]
    [Description("Controls how the PictureBox will handle image placement and control sizing.")]
    [DefaultValue(PictureBoxSizeMode.Normal)]
    [Localizable(true)]
    public PictureBoxSizeMode SizeMode
    {
        get => _sizeMode;
        set
        {
            if (_sizeMode == value) return;
            _sizeMode = value;
            if (value == PictureBoxSizeMode.AutoSize) AdjustToImage();
            OnSizeModeChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    internal bool ShouldSerializeInitialImage() => InitialImage != null;

    internal bool ShouldSerializeErrorImage() => ErrorImage != null;

    [Category("Appearance")]
    [Description("Controls what type of border the PictureBox should have.")]
    [DefaultValue(BorderStyle.None)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            if (_sizeMode == PictureBoxSizeMode.AutoSize) AdjustToImage();
            Invalidate();
        }
    }

    protected virtual void OnSizeModeChanged(EventArgs e) => SizeModeChanged?.Invoke(this, e);

    private int BorderSize => _borderStyle == BorderStyle.None ? 0 : _borderStyle == BorderStyle.FixedSingle ? 1 : 2;

    private void AdjustToImage()
    {
        if (_image == null) return;
        Size = new Size(_image.Width + 2 * BorderSize, _image.Height + 2 * BorderSize);
    }

    public override Size GetPreferredSize(Size proposedSize) =>
        _image == null ? Size : new Size(_image.Width + 2 * BorderSize, _image.Height + 2 * BorderSize);

    public void Load(string url) => Image = System.Drawing.Image.FromFile(url);

    public void Load()
    {
        if (!string.IsNullOrEmpty(ImageLocation)) Load(ImageLocation);
    }

    /// <summary>The rectangle the image is drawn into for the current SizeMode.</summary>
    internal Rectangle ImageRectangle
    {
        get
        {
            if (_image == null) return Rectangle.Empty;
            int b = BorderSize;
            var client = new Rectangle(b, b, Math.Max(0, Width - 2 * b), Math.Max(0, Height - 2 * b));
            var img = _image.Size;
            switch (_sizeMode)
            {
                case PictureBoxSizeMode.StretchImage:
                    return client;
                case PictureBoxSizeMode.CenterImage:
                    return new Rectangle(client.X + (client.Width - img.Width) / 2, client.Y + (client.Height - img.Height) / 2, img.Width, img.Height);
                case PictureBoxSizeMode.Zoom:
                    {
                        if (img.Width == 0 || img.Height == 0) return Rectangle.Empty;
                        float ratio = Math.Min(client.Width / (float)img.Width, client.Height / (float)img.Height);
                        int w = (int)(img.Width * ratio), h = (int)(img.Height * ratio);
                        return new Rectangle(client.X + (client.Width - w) / 2, client.Y + (client.Height - h) / 2, w, h);
                    }
                default:
                    return new Rectangle(client.Location, img);
            }
        }
    }

    protected override void OnPaint(PaintEventArgs pe)
    {
        if (_image != null)
        {
            var rect = ImageRectangle;
            if (rect.Width > 0 && rect.Height > 0)
            {
                pe.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                pe.Graphics.DrawImage(_image, rect);
            }
        }
        Panel.PaintBorder(pe.Graphics, ClientRectangle, _borderStyle);
        base.OnPaint(pe);
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

    [Category("Focus")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CausesValidation { get => base.CausesValidation; set => base.CausesValidation = value; }

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

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override RightToLeft RightToLeft { get => base.RightToLeft; set => base.RightToLeft = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Category("Appearance")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Category("Focus")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Enter
    {
        add => base.Enter += value;
        remove => base.Enter -= value;
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

    [Category("Focus")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? Leave
    {
        add => base.Leave += value;
        remove => base.Leave -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? TabIndexChanged
    {
        add => base.TabIndexChanged += value;
        remove => base.TabIndexChanged -= value;
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

    // WinForms implements these explicitly; the designer writes ((ISupportInitialize)x).BeginInit().
    void ISupportInitialize.BeginInit() { }

    void ISupportInitialize.EndInit() { }
}
