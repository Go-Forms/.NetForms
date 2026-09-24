using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Shared behaviour of Button/CheckBox/RadioButton: press tracking, hover state, keyboard activation.</summary>
public abstract class ButtonBase : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    private FlatStyle _flatStyle = FlatStyle.Standard;
    private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;
    private bool _useVisualStyleBackColor = true;
    private bool _mouseIsDown;
    private bool _mouseIsPressed;
    private bool _mouseIsOver;
    private bool _keyPressed;
    private bool _isDefault;

    protected ButtonBase()
    {
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.Opaque | ControlStyles.ResizeRedraw |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.CacheText | ControlStyles.UserPaint, true);
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
    }

    protected override Size DefaultSize => new Size(75, 23);

    [Category("Appearance")]
    [Description("Determines the appearance of the control when a user moves the mouse over the control and clicks.")]
    [DefaultValue(FlatStyle.Standard)]
    [Localizable(true)]
    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set
        {
            if (_flatStyle == value) return;
            _flatStyle = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("The alignment of the text that will be displayed on the control.")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    public virtual ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value) return;
            _textAlign = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Determines whether the background is drawn using visual styles, if visual styles are supported.")]
    public bool UseVisualStyleBackColor
    {
        get => _useVisualStyleBackColor;
        set
        {
            _useVisualStyleBackColor = value;
            _useVisualStyleBackColorSet = true;
            Invalidate();
        }
    }

    private bool _useVisualStyleBackColorSet;

    /// <summary>Written once set explicitly - the designer does that for every new button.</summary>
    internal bool ShouldSerializeUseVisualStyleBackColor() => _useVisualStyleBackColorSet;

    public void ResetUseVisualStyleBackColor()
    {
        _useVisualStyleBackColorSet = false;
        _useVisualStyleBackColor = true;
        Invalidate();
    }

    [Category("Appearance")]
    [Description("If true, the first character preceded by an ampersand (&&) will be used as the button's mnemonic key.")]
    [DefaultValue(true)]
    public bool UseMnemonic { get; set; } = true;

    [Category("Behavior")]
    [Description("Specifies whether text rendering should be compatible with previous releases of Windows Forms.")]
    [DefaultValue(false)]
    public bool UseCompatibleTextRendering { get; set; }

    [Category("Behavior")]
    [Description("Enables the automatic handling of text that extends beyond the width of the button.")]
    [DefaultValue(false)]
    public bool AutoEllipsis { get; set; }

    [Category("Appearance")]
    [Description("The image that will be displayed on the control.")]
    [Localizable(true)]
    public Image? Image
    {
        get
        {
            if (_image != null || _imageList == null) return _image;
            // No image of its own: the one ImageIndex or ImageKey names in the ImageList.
            if (_imageIndex >= 0 && _imageIndex < _imageList.Images.Count) return _imageList.Images[_imageIndex];
            if (!string.IsNullOrEmpty(_imageKey)) return _imageList.Images[_imageKey];
            return null;
        }
        set
        {
            if (_image == value) return;
            _image = value;
            if (value != null)
            {
                _imageIndex = -1;
                _imageKey = string.Empty;
                _imageList = null;
            }
            if (AutoSize) Parent?.PerformLayout(this, nameof(Image));
            Invalidate();
        }
    }

    private Image? _image;
    private ImageList? _imageList;
    private int _imageIndex = -1;
    private string _imageKey = string.Empty;
    private TextImageRelation _textImageRelation = TextImageRelation.Overlay;
    private FlatButtonAppearance? _flatAppearance;

    [DefaultValue(null)]
    [Description("The ImageList to get the image to display on the control.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Category("Appearance")]
    public ImageList? ImageList
    {
        get => _imageList;
        set
        {
            if (_imageList == value) return;
            _imageList = value;
            if (value != null) _image = null;
            Invalidate();
        }
    }

    [Localizable(true)]
    [DefaultValue(-1)]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Description("The index of the image in the ImageList to display on the control.")]
    [Category("Appearance")]
    [TypeConverter(typeof(ImageIndexConverter))]
    public int ImageIndex
    {
        get => _imageIndex != -1 && _imageList != null && _imageIndex >= _imageList.Images.Count ? _imageList.Images.Count - 1 : _imageIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            if (_imageIndex == value && value != -1) return;
            _imageIndex = value;
            if (value != -1)
            {
                _imageKey = string.Empty;
                _image = null;
            }
            Invalidate();
        }
    }

    [Localizable(true)]
    [DefaultValue("")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Description("The index of the image in the ImageList to display on the control.")]
    [Category("Appearance")]
    [TypeConverter(typeof(ImageKeyConverter))]
    public string ImageKey
    {
        get => _imageKey;
        set
        {
            if (_imageKey == value && !string.Equals(value, string.Empty)) return;
            _imageKey = value ?? string.Empty;
            if (_imageKey.Length > 0)
            {
                _imageIndex = -1;
                _image = null;
            }
            Invalidate();
        }
    }

    [DefaultValue(TextImageRelation.Overlay)]
    [Localizable(true)]
    [Description("Specifies the relative location of the image to the text on the button.")]
    [Category("Appearance")]
    public TextImageRelation TextImageRelation
    {
        get => _textImageRelation;
        set
        {
            if (_textImageRelation == value) return;
            _textImageRelation = value;
            if (AutoSize) Parent?.PerformLayout(this, nameof(TextImageRelation));
            Invalidate();
        }
    }

    [Browsable(true)]
    [Category("Appearance")]
    [Description("For buttons whose FlatStyle is FlatStyle.Flat, determines the appearance of the border and the colors used to indicate check state and mouse state.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public FlatButtonAppearance FlatAppearance => _flatAppearance ??= new FlatButtonAppearance(this);

    /// <summary>
    /// Lays out the image and the text in <paramref name="content"/> (the button's padded client area) by
    /// <see cref="TextImageRelation"/>: Overlay aligns each on its own; the others give the image its
    /// width (height) on the side it goes, and the text the rest.
    /// </summary>
    internal (Rectangle Image, Rectangle Text) LayoutImageAndText(Rectangle content, Size imageSize)
    {
        if (imageSize.IsEmpty) return (Rectangle.Empty, content);
        const int gap = 2;
        Rectangle imageArea = content, textArea = content;
        switch (_textImageRelation)
        {
            case TextImageRelation.ImageBeforeText:
                imageArea.Width = imageSize.Width;
                textArea = Rectangle.FromLTRB(content.Left + imageSize.Width + gap, content.Top, content.Right, content.Bottom);
                break;
            case TextImageRelation.TextBeforeImage:
                imageArea = Rectangle.FromLTRB(content.Right - imageSize.Width, content.Top, content.Right, content.Bottom);
                textArea = Rectangle.FromLTRB(content.Left, content.Top, content.Right - imageSize.Width - gap, content.Bottom);
                break;
            case TextImageRelation.ImageAboveText:
                imageArea.Height = imageSize.Height;
                textArea = Rectangle.FromLTRB(content.Left, content.Top + imageSize.Height + gap, content.Right, content.Bottom);
                break;
            case TextImageRelation.TextAboveImage:
                imageArea = Rectangle.FromLTRB(content.Left, content.Bottom - imageSize.Height, content.Right, content.Bottom);
                textArea = Rectangle.FromLTRB(content.Left, content.Top, content.Right, content.Bottom - imageSize.Height - gap);
                break;
        }
        return (Align(imageArea, imageSize, ImageAlign), textArea);
    }

    internal static Rectangle Align(Rectangle area, Size size, ContentAlignment alignment)
    {
        int x = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => area.Left,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => area.Right - size.Width,
            _ => area.Left + (area.Width - size.Width) / 2,
        };
        int y = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => area.Top,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => area.Bottom - size.Height,
            _ => area.Top + (area.Height - size.Height) / 2,
        };
        return new Rectangle(x, y, size.Width, size.Height);
    }

    [Category("Appearance")]
    [Description("The alignment of the image that will be displayed on the control.")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    public ContentAlignment ImageAlign { get; set; } = ContentAlignment.MiddleCenter;

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    protected internal bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (_isDefault == value) return;
            _isDefault = value;
            Invalidate();
        }
    }

    internal bool ShouldSerializeImage() => Image != null;

    /// <summary>Left button held down over the control.</summary>
    internal bool MouseIsDown => _mouseIsDown;

    internal bool MouseIsPressed => _mouseIsPressed || _keyPressed;

    internal bool MouseIsOver => _mouseIsOver;

    /// <summary>The control looks pressed: the pointer is down and inside, or Space is held.</summary>
    internal bool IsPressedVisual => (_mouseIsDown && _mouseIsOver) || _keyPressed;

    protected override void OnMouseEnter(EventArgs e)
    {
        _mouseIsOver = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _mouseIsOver = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseMove(MouseEventArgs mevent)
    {
        if (mevent.Button != MouseButtons.None && _mouseIsPressed)
        {
            bool inside = ClientRectangle.Contains(mevent.Location);
            if (inside != _mouseIsDown)
            {
                _mouseIsDown = inside;
                Invalidate();
            }
        }
        base.OnMouseMove(mevent);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left)
        {
            _mouseIsDown = true;
            _mouseIsPressed = true;
            if (CanFocus) Focus();
            Invalidate();
        }
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        if (mevent.Button == MouseButtons.Left && _mouseIsPressed)
        {
            bool wasDown = _mouseIsDown;
            _mouseIsDown = false;
            _mouseIsPressed = false;
            Invalidate();
            if (wasDown && ClientRectangle.Contains(mevent.Location))
            {
                OnClick(mevent);
                OnMouseClick(mevent);
            }
        }
        base.OnMouseUp(mevent);
    }

    protected override void OnKeyDown(KeyEventArgs kevent)
    {
        if (kevent.KeyData == Keys.Space && !_keyPressed)
        {
            _keyPressed = true;
            Invalidate();
            kevent.Handled = true;
        }
        base.OnKeyDown(kevent);
    }

    protected override void OnKeyUp(KeyEventArgs kevent)
    {
        if (_keyPressed && kevent.KeyCode == Keys.Space)
        {
            _keyPressed = false;
            Invalidate();
            OnClick(EventArgs.Empty);
            kevent.Handled = true;
        }
        base.OnKeyUp(kevent);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        _keyPressed = false;
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            _mouseIsDown = false;
            _mouseIsPressed = false;
            _mouseIsOver = false;
            _keyPressed = false;
        }
        base.OnEnabledChanged(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

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
        if (!UseMnemonic) flags |= TextFormatFlags.NoPrefix;
        if (AutoEllipsis) flags |= TextFormatFlags.EndEllipsis;
        return flags;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, proposedSize, CreateTextFormatFlags() & ~TextFormatFlags.WordBreak);
        // WinForms' standard button: text plus 14px horizontally, 9px vertically, never below the default height.
        var size = new Size(text.Width + Padding.Horizontal + 14, text.Height + Padding.Vertical + 9);
        return new Size(Math.Max(size.Width, 75), Math.Max(size.Height, 23));
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
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new ImeMode ImeMode { get => base.ImeMode; set => base.ImeMode = value; }

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
}
