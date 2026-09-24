using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum ToolStripItemDisplayStyle
{
    None = 0,
    Text = 1,
    Image = 2,
    ImageAndText = 3,
}

public enum ToolStripItemAlignment
{
    Left = 0,
    Right = 1,
}

public enum ToolStripItemOverflow
{
    Never = 0,
    Always = 1,
    AsNeeded = 2,
}

public enum ToolStripItemPlacement
{
    Main = 0,
    Overflow = 1,
    None = 2,
}

public enum TextImageRelation
{
    Overlay = 0,
    ImageAboveText = 1,
    TextAboveImage = 2,
    ImageBeforeText = 4,
    TextBeforeImage = 8,
}

public enum ToolStripItemImageScaling
{
    None = 0,
    SizeToFit = 1,
}

public enum MergeAction
{
    Append = 0,
    Insert = 1,
    Replace = 2,
    Remove = 3,
    MatchOnly = 4,
}

public delegate void ToolStripItemEventHandler(object? sender, ToolStripItemEventArgs e);
public delegate void ToolStripItemClickedEventHandler(object? sender, ToolStripItemClickedEventArgs e);

public class ToolStripItemEventArgs : EventArgs
{
    public ToolStripItemEventArgs(ToolStripItem item) => Item = item;

    public ToolStripItem Item { get; }
}

public class ToolStripItemClickedEventArgs : EventArgs
{
    public ToolStripItemClickedEventArgs(ToolStripItem clickedItem) => ClickedItem = clickedItem;

    public ToolStripItem ClickedItem { get; }
}

/// <summary>
/// One element of a ToolStrip/MenuStrip/StatusStrip. Items are components, not controls:
/// the owning strip lays them out, paints them through its renderer and routes its mouse
/// and keyboard input to them, exactly as WinForms does.
/// </summary>
[DefaultEvent("Click")]
[DefaultProperty("Text")]
public abstract class ToolStripItem : Component
{
    private string _text = string.Empty;
    private Image? _image;
    private bool _enabled = true;
    private bool _visible = true;
    private bool _selected;
    private bool _pressed;
    private Rectangle _bounds;
    private Padding? _margin;
    private Padding? _padding;
    private Font? _font;
    private Color _foreColor = Color.Empty;
    private Color _backColor = Color.Empty;
    private ToolStripItemDisplayStyle? _displayStyle;
    private ToolStripItemAlignment _alignment = ToolStripItemAlignment.Left;
    private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;
    private ContentAlignment _imageAlign = ContentAlignment.MiddleCenter;
    private TextImageRelation _textImageRelation = TextImageRelation.ImageBeforeText;
    private bool _autoSize = true;
    private Size _size;
    private ToolStrip? _owner;

    protected ToolStripItem() : this(null, null, null) { }

    protected ToolStripItem(string? text, Image? image, EventHandler? onClick) : this(text, image, onClick, null) { }

    protected ToolStripItem(string? text, Image? image, EventHandler? onClick, string? name)
    {
        // Margin, Padding and DisplayStyle stay unset here: their defaults depend on where the
        // item ends up (a menu item on a bar and the same item on a drop-down differ), and the
        // owner is only known later.
        _size = DefaultSize;
        _textAlign = DefaultTextAlign;
        AutoToolTip = DefaultAutoToolTip;
        Overflow = DefaultOverflow;
        _text = text ?? string.Empty;
        _image = image;
        Name = name ?? string.Empty;
        if (onClick != null) Click += onClick;
    }

    // --- events --------------------------------------------------------------------

    [Category("Action")]
    [Description("Occurs when the item is clicked.")]
    public event EventHandler? Click;

    [Category("Action")]
    [Description("Occurs when the component is double-clicked.")]
    public event EventHandler? DoubleClick;

    [Category("Mouse")]
    [Description("Occurs when a mouse button is pressed.")]
    public event MouseEventHandler? MouseDown;

    [Category("Mouse")]
    [Description("Occurs when a mouse button is released.")]
    public event MouseEventHandler? MouseUp;

    [Category("Mouse")]
    [Description("Occurs when the mouse is moved.")]
    public event MouseEventHandler? MouseMove;

    [Category("Mouse")]
    [Description("Occurs when the mouse enters the visible part of the item.")]
    public event EventHandler? MouseEnter;

    [Category("Mouse")]
    [Description("Occurs when the mouse leaves the visible part of the item.")]
    public event EventHandler? MouseLeave;

    [Category("Mouse")]
    [Description("Occurs when the mouse remains stationary inside of the item for an amount of time.")]
    public event EventHandler? MouseHover;

    [Category("Appearance")]
    [Description("Occurs when an item needs repainting.")]
    public event PaintEventHandler? Paint;

    [Category("Property Changed")]
    [Description("Occurs when the Text property is changed on the item.")]
    public event EventHandler? TextChanged;

    [Description("Occurs when the enabled state of the item has changed.")]
    public event EventHandler? EnabledChanged;

    [Category("Property Changed")]
    [Description("Occurs when the visibility of an item has changed.")]
    public event EventHandler? VisibleChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the Available property changes.")]
    [Browsable(false)]
    public event EventHandler? AvailableChanged;
    public event EventHandler? DisplayStyleChanged;

    [Category("Behavior")]
    [Description("Occurs when the value of the Owner property changes.")]
    public event EventHandler? OwnerChanged;

    [Category("Layout")]
    [Description("Occurs when the location of a ToolStripItem is updated.")]
    public event EventHandler? LocationChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the BackColor property is changed on the item.")]
    public event EventHandler? ForeColorChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the BackColor property is changed on the item.")]
    public event EventHandler? BackColorChanged;

    // --- identity & appearance -----------------------------------------------------

    [DefaultValue(null)]
    [Browsable(false)]
    public string Name { get; set; }

    [Category("Data")]
    [Description("User-defined data associated with the item.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [Category("Behavior")]
    [Description("Specifies the text to show on the ToolTip.")]
    [Localizable(true)]
    public string? ToolTipText { get; set; }

    [Category("Behavior")]
    [Description("Specifies whether to use the Text or ToolTipText property to display on the ToolTip.")]
    [DefaultValue(false)]
    public bool AutoToolTip { get; set; }

    /// <summary>
    /// Buttons show their Text as a tip by default, plain items do not - the same split WinForms
    /// makes. Applied by the constructor, so user code can still turn it off.
    /// </summary>
    protected virtual bool DefaultAutoToolTip => false;

    [Category("Appearance")]
    [Description("The text to display on the item.")]
    [DefaultValue("")]
    [Localizable(true)]
    public virtual string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (_text == value) return;
            _text = value;
            OnTextChanged(EventArgs.Empty);
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("The image that will be displayed on the item.")]
    [Localizable(true)]
    public virtual Image? Image
    {
        get => _image;
        set
        {
            if (ReferenceEquals(_image, value)) return;
            _image = value;
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("Specifies the transparent color on the item's image for images that support transparency.")]
    [Localizable(true)]
    public Color ImageTransparentColor { get; set; } = Color.Empty;

    [Category("Behavior")]
    [Description("The index of the image in the ImageList to display on the item.")]
    [Localizable(true)]
    [Browsable(false)]
    public int ImageIndex { get; set; } = -1;

    [Category("Behavior")]
    [Description("The key representing the image in the ImageList to display on the item.")]
    [Localizable(true)]
    [Browsable(false)]
    public string ImageKey { get; set; } = string.Empty;

    [Category("Appearance")]
    [Description("Specifies whether the image on the item will size to fit on the ToolStrip.  To control the image size, use the 'ToolStrip.ImageScalingSize' property.")]
    [DefaultValue(ToolStripItemImageScaling.SizeToFit)]
    [Localizable(true)]
    public ToolStripItemImageScaling ImageScaling { get; set; } = ToolStripItemImageScaling.SizeToFit;

    [Category("Appearance")]
    [Description("Specifies whether the image and text are rendered.")]
    public virtual ToolStripItemDisplayStyle DisplayStyle
    {
        get => _displayStyle ?? DefaultDisplayStyle;
        set
        {
            if (DisplayStyle == value) return;
            _displayStyle = value;
            OnDisplayStyleChanged(EventArgs.Empty);
            InvalidateLayout();
        }
    }

    protected virtual ToolStripItemDisplayStyle DefaultDisplayStyle => ToolStripItemDisplayStyle.ImageAndText;

    [Category("Layout")]
    [Description("Indicates whether the item aligns toward the beginning or end of the ToolStrip.")]
    [DefaultValue(ToolStripItemAlignment.Left)]
    public ToolStripItemAlignment Alignment
    {
        get => _alignment;
        set
        {
            if (_alignment == value) return;
            _alignment = value;
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("The alignment of the text that will be displayed on the item.")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    public virtual ContentAlignment TextAlign
    {
        get => _textAlign;
        set { _textAlign = value; Invalidate(); }
    }

    protected virtual ContentAlignment DefaultTextAlign => ContentAlignment.MiddleCenter;

    [Category("Appearance")]
    [Description("The alignment of the image that will be displayed on the item.")]
    [DefaultValue(typeof(ContentAlignment), "MiddleCenter")]
    [Localizable(true)]
    public ContentAlignment ImageAlign
    {
        get => _imageAlign;
        set { _imageAlign = value; Invalidate(); }
    }

    [Category("Appearance")]
    [Description("Specifies the relative location of the image to the text on the item.")]
    [DefaultValue(TextImageRelation.ImageBeforeText)]
    [Localizable(true)]
    public TextImageRelation TextImageRelation
    {
        get => _textImageRelation;
        set { _textImageRelation = value; InvalidateLayout(); }
    }

    [Category("Layout")]
    [Description("Specifies whether the item will always move to the overflow, move to the overflow as needed, or never move to the overflow.")]
    [DefaultValue(ToolStripItemOverflow.AsNeeded)]
    public ToolStripItemOverflow Overflow { get; set; }

    /// <summary>A menu item never moves to the overflow; other items do so as needed.</summary>
    protected virtual ToolStripItemOverflow DefaultOverflow => ToolStripItemOverflow.AsNeeded;

    [Browsable(false)]
    public ToolStripItemPlacement Placement { get; internal set; } = ToolStripItemPlacement.Main;

    [Category("Layout")]
    [Description("Specifies what action to take if match is successful.")]
    [DefaultValue(MergeAction.Append)]
    public MergeAction MergeAction { get; set; } = MergeAction.Append;

    [Category("Layout")]
    [Description("Used for matching and positioning within target ToolStrip.")]
    [DefaultValue(-1)]
    public int MergeIndex { get; set; } = -1;

    [Category("Appearance")]
    [Description("The font used to display text in the item.")]
    [Localizable(true)]
    public virtual Font Font
    {
        get => _font ?? _owner?.Font ?? Control.DefaultFont;
        set
        {
            _font = value;
            InvalidateLayout();
        }
    }

    [Category("Appearance")]
    [Description("The foreground color used to display text and graphics in the item.")]
    public virtual Color ForeColor
    {
        get => !_foreColor.IsEmpty ? _foreColor : (_owner?.ForeColor ?? Control.DefaultForeColor);
        set
        {
            if (_foreColor == value) return;
            _foreColor = value;
            OnForeColorChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("The background color used to display text and graphics in the control.")]
    public virtual Color BackColor
    {
        get => !_backColor.IsEmpty ? _backColor : (_owner?.BackColor ?? Control.DefaultBackColor);
        set
        {
            if (_backColor == value) return;
            _backColor = value;
            OnBackColorChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    internal bool IsForeColorSet => !_foreColor.IsEmpty;

    internal bool IsBackColorSet => !_backColor.IsEmpty;

    [Category("Layout")]
    [Description("Specifies the spacing between this item and an adjacent item.")]
    public Padding Margin
    {
        get => _margin ?? DefaultMargin;
        set
        {
            if (Margin == value) return;
            _margin = value;
            InvalidateLayout();
        }
    }

    protected internal virtual Padding DefaultMargin => new Padding(0, 1, 0, 2);

    public void ResetMargin()
    {
        _margin = null;
        InvalidateLayout();
    }

    [Category("Layout")]
    [Description("Specifies the internal spacing within this item.")]
    public virtual Padding Padding
    {
        get => _padding ?? DefaultPadding;
        set
        {
            if (Padding == value) return;
            _padding = value;
            InvalidateLayout();
        }
    }

    protected virtual Padding DefaultPadding => Padding.Empty;

    public void ResetPadding()
    {
        _padding = null;
        InvalidateLayout();
    }

    // --- state ---------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Indicates whether the control is enabled.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public virtual bool Enabled
    {
        get => _enabled && (_owner?.Enabled ?? true);
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (!value) _selected = _pressed = false;
            OnEnabledChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Determines whether the item is visible or hidden.")]
    [Localizable(true)]
    public bool Visible
    {
        get => _visible && _owner != null && _owner.Visible;
        set
        {
            if (_visible == value) return;
            _visible = value;
            OnVisibleChanged(EventArgs.Empty);
            OnAvailableChanged(EventArgs.Empty);
            InvalidateLayout();
        }
    }

    /// <summary>The Visible flag as set, regardless of the owner: what layout looks at.</summary>
    [Description("Specifies whether or not an item should be placed on a ToolStrip.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Available
    {
        get => _visible;
        set => Visible = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual bool Selected => _selected;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual bool Pressed => _pressed;

    [Browsable(false)]
    public virtual bool CanSelect => true;

    internal void SetSelected(bool value)
    {
        if (_selected == value) return;
        _selected = value;
        if (value) OnMouseEnter(EventArgs.Empty); else OnMouseLeave(EventArgs.Empty);
        Invalidate();
    }

    internal void SetPressed(bool value)
    {
        if (_pressed == value) return;
        _pressed = value;
        Invalidate();
    }

    // --- geometry ------------------------------------------------------------------

    [Browsable(false)]
    public Rectangle Bounds => _bounds;

    public Point Location => _bounds.Location;

    [Category("Layout")]
    [Description("The size of the item in pixels.")]
    [Localizable(true)]
    public Size Size
    {
        get => _bounds.Size.IsEmpty ? _size : _bounds.Size;
        set
        {
            // As in WinForms, this sets the bounds and nothing else: an AutoSize item is re-measured by
            // the next layout (the designer writes every item's Size), a fixed-size one keeps it.
            _size = value;
            InvalidateLayout();
        }
    }

    [Category("Layout")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Width
    {
        get => Size.Width;
        set => Size = new Size(value, Size.Height);
    }

    [Category("Layout")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Height
    {
        get => Size.Height;
        set => Size = new Size(Size.Width, value);
    }

    [Browsable(false)]
    public Rectangle ContentRectangle
    {
        get
        {
            var r = new Rectangle(Point.Empty, _bounds.Size);
            var p = Padding;
            return new Rectangle(r.X + p.Left, r.Y + p.Top, Math.Max(0, r.Width - p.Horizontal), Math.Max(0, r.Height - p.Vertical));
        }
    }

    [Category("Behavior")]
    [Description("Determines whether the item should automatically size based on its image and text.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool AutoSize
    {
        get => _autoSize;
        set
        {
            if (_autoSize == value) return;
            _autoSize = value;
            InvalidateLayout();
        }
    }

    protected virtual Size DefaultSize => new Size(23, 23);

    /// <summary>Explicit size when AutoSize is off.</summary>
    internal Size ExplicitSize => _size;

    protected internal virtual void SetBounds(Rectangle bounds)
    {
        if (_bounds == bounds) return;
        bool moved = _bounds.Location != bounds.Location;
        _bounds = bounds;
        if (moved) OnLocationChanged(EventArgs.Empty);
    }

    /// <summary>
    /// The border dotnet/winforms' ToolStripItemInternalLayout leaves around an item's content
    /// (BorderWidth = 2 on every side); it is what makes a "File" menu 37px wide and not 31.
    /// </summary>
    internal const int BorderSize = 2;

    /// <summary>The border this kind of item leaves around its content; labels leave none (ToolStripLabelLayout).</summary>
    internal virtual int ContentBorderSize => BorderSize;

    public virtual Size GetPreferredSize(Size constrainingSize)
    {
        if (!_autoSize) return _size;
        var content = MeasureContent();
        var padding = Padding;
        int border = ContentBorderSize;
        return new Size(content.Width + padding.Horizontal + border * 2, content.Height + padding.Vertical + border * 2);
    }

    /// <summary>The text and image extent, arranged by TextImageRelation and DisplayStyle.</summary>
    internal Size MeasureContent()
    {
        bool showText = (_displayStyle & ToolStripItemDisplayStyle.Text) != 0 && _text.Length > 0;
        bool showImage = (_displayStyle & ToolStripItemDisplayStyle.Image) != 0 && _image != null;
        var textSize = showText ? TextRenderer.MeasureText(_text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine) : Size.Empty;
        var imageSize = showImage ? ImageSize : Size.Empty;
        if (showText && showImage)
        {
            return _textImageRelation switch
            {
                TextImageRelation.ImageAboveText or TextImageRelation.TextAboveImage => new Size(Math.Max(textSize.Width, imageSize.Width), textSize.Height + imageSize.Height),
                TextImageRelation.Overlay => new Size(Math.Max(textSize.Width, imageSize.Width), Math.Max(textSize.Height, imageSize.Height)),
                _ => new Size(textSize.Width + imageSize.Width, Math.Max(textSize.Height, imageSize.Height)),
            };
        }
        if (showText) return textSize;
        if (showImage) return imageSize;
        return Size.Empty;
    }

    internal Size ImageSize
    {
        get
        {
            if (_image == null) return Size.Empty;
            if (ImageScaling == ToolStripItemImageScaling.SizeToFit && _owner != null) return _owner.ImageScalingSize;
            return _image.Size;
        }
    }

    // --- ownership -----------------------------------------------------------------

    [Description("Provides toolbars and other user interface elements that support many appearance options, and that support overflow and run-time item reordering.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStrip? Owner
    {
        get => _owner;
        set
        {
            if (_owner == value) return;
            _owner?.Items.Remove(this);
            value?.Items.Add(this);
        }
    }

    internal void SetOwner(ToolStrip? owner)
    {
        if (_owner == owner) return;
        _owner = owner;
        OnOwnerChanged(EventArgs.Empty);
    }

    /// <summary>The item whose drop-down this item lives in, if any.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStripItem? OwnerItem => (_owner as ToolStripDropDown)?.OwnerItem;

    [Browsable(false)]
    public bool IsOnDropDown => _owner is ToolStripDropDown;

    [Browsable(false)]
    public bool IsOnOverflow => Placement == ToolStripItemPlacement.Overflow;

    public ToolStrip? GetCurrentParent() => _owner;

    public void Invalidate() => _owner?.Invalidate(_bounds);

    public void Invalidate(Rectangle r) => _owner?.Invalidate(new Rectangle(_bounds.X + r.X, _bounds.Y + r.Y, r.Width, r.Height));

    internal void InvalidateLayout() => _owner?.ItemsChanged();

    public void Select()
    {
        if (!CanSelect || !Enabled) return;
        _owner?.SelectItem(this);
    }

    public void PerformClick()
    {
        if (Enabled && Available) OnClick(EventArgs.Empty);
    }

    // --- input from the owner ------------------------------------------------------

    internal virtual void HandleMouseDown(MouseEventArgs e)
    {
        if (!Enabled) return;
        if (e.Button == MouseButtons.Left) SetPressed(true);
        OnMouseDown(e);
    }

    internal virtual void HandleMouseUp(MouseEventArgs e)
    {
        if (!Enabled) return;
        bool wasPressed = _pressed;
        SetPressed(false);
        OnMouseUp(e);
        if (wasPressed && e.Button == MouseButtons.Left && new Rectangle(Point.Empty, _bounds.Size).Contains(e.Location))
        {
            HandleClick(e);
        }
    }

    internal virtual void HandleClick(EventArgs e)
    {
        OnClick(e);
        _owner?.HandleItemClick(this);
    }

    internal virtual void HandleMouseMove(MouseEventArgs e) => OnMouseMove(e);

    internal virtual void HandleDoubleClick(EventArgs e) => OnDoubleClick(e);

    // --- On* ------------------------------------------------------------------------

    protected virtual void OnClick(EventArgs e) => Click?.Invoke(this, e);
    protected virtual void OnDoubleClick(EventArgs e) => DoubleClick?.Invoke(this, e);
    protected virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(this, e);
    protected virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(this, e);
    protected virtual void OnMouseMove(MouseEventArgs mea) => MouseMove?.Invoke(this, mea);
    protected virtual void OnMouseEnter(EventArgs e) => MouseEnter?.Invoke(this, e);
    protected virtual void OnMouseLeave(EventArgs e) => MouseLeave?.Invoke(this, e);
    protected virtual void OnMouseHover(EventArgs e) => MouseHover?.Invoke(this, e);
    protected virtual void OnTextChanged(EventArgs e) => TextChanged?.Invoke(this, e);
    protected virtual void OnEnabledChanged(EventArgs e) => EnabledChanged?.Invoke(this, e);
    protected virtual void OnVisibleChanged(EventArgs e) => VisibleChanged?.Invoke(this, e);
    protected virtual void OnAvailableChanged(EventArgs e) => AvailableChanged?.Invoke(this, e);
    protected virtual void OnDisplayStyleChanged(EventArgs e) => DisplayStyleChanged?.Invoke(this, e);
    protected virtual void OnOwnerChanged(EventArgs e) => OwnerChanged?.Invoke(this, e);
    protected virtual void OnLocationChanged(EventArgs e) => LocationChanged?.Invoke(this, e);
    protected virtual void OnForeColorChanged(EventArgs e) => ForeColorChanged?.Invoke(this, e);
    protected virtual void OnBackColorChanged(EventArgs e) => BackColorChanged?.Invoke(this, e);

    /// <summary>
    /// Draws the item through the owner's <see cref="ToolStripRenderer"/>; <paramref name="e"/> has the
    /// item's origin at (0,0). Each item type overrides this and calls the renderer methods it needs,
    /// as WinForms does - the base only raises <see cref="Paint"/>.
    /// </summary>
    protected virtual void OnPaint(PaintEventArgs e) => Paint?.Invoke(this, e);

    /// <summary>Raises <see cref="Paint"/> for an override that must not run its base class's drawing.</summary>
    private protected void RaisePaint(PaintEventArgs e) => Paint?.Invoke(this, e);

    /// <summary>The renderer this item draws through: the owner's, or the manager's when it has no owner yet.</summary>
    protected internal ToolStripRenderer Renderer => _owner?.Renderer ?? ToolStripManager.Renderer;

    /// <summary>Draws the item's image and text where <see cref="LayoutContent()"/> puts them.</summary>
    private protected void PaintImageAndText(PaintEventArgs e)
    {
        var renderer = Renderer;
        var (text, image) = LayoutContent();
        if (_image != null && (_displayStyle & ToolStripItemDisplayStyle.Image) != 0 && image.Width > 0)
        {
            renderer.DrawItemImage(new ToolStripItemImageRenderEventArgs(e.Graphics, this, _image, image));
        }
        if (_text.Length > 0 && (_displayStyle & ToolStripItemDisplayStyle.Text) != 0 && text.Width > 0)
        {
            renderer.DrawItemText(new ToolStripItemTextRenderEventArgs(e.Graphics, this, _text, text,
                Enabled ? ForeColor : Theme.DisabledText, Font,
                ToolStripItemTextRenderEventArgs.TranslateAlignment(_textAlign) | TextFormatFlags.SingleLine));
        }
    }

    internal void PaintItem(PaintEventArgs e) => OnPaint(e);

    // --- text & image placement (used by renderers) --------------------------------

    /// <summary>Where the text and the image go inside the item, per DisplayStyle/TextImageRelation/alignments.</summary>
    internal (Rectangle text, Rectangle image) LayoutContent() => LayoutContent(ContentRectangle);

    /// <summary>The same, laid out inside a caller-chosen area: the button half of a split button, the part left of an arrow.</summary>
    internal (Rectangle text, Rectangle image) LayoutContent(Rectangle area)
    {
        bool showText = (_displayStyle & ToolStripItemDisplayStyle.Text) != 0 && _text.Length > 0;
        bool showImage = (_displayStyle & ToolStripItemDisplayStyle.Image) != 0 && _image != null;
        var textSize = showText ? TextRenderer.MeasureText(_text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine) : Size.Empty;
        var imageSize = showImage ? ImageSize : Size.Empty;

        if (showText && showImage)
        {
            switch (_textImageRelation)
            {
                case TextImageRelation.ImageBeforeText:
                    {
                        var image = Align(area, imageSize, ContentAlignment.MiddleLeft);
                        var textArea = new Rectangle(image.Right, area.Y, Math.Max(0, area.Right - image.Right), area.Height);
                        return (Align(textArea, textSize, _textAlign), image);
                    }
                case TextImageRelation.TextBeforeImage:
                    {
                        var text = Align(area, textSize, ContentAlignment.MiddleLeft);
                        var imageArea = new Rectangle(text.Right, area.Y, Math.Max(0, area.Right - text.Right), area.Height);
                        return (text, Align(imageArea, imageSize, _imageAlign));
                    }
                case TextImageRelation.ImageAboveText:
                    {
                        var image = Align(area, imageSize, ContentAlignment.TopCenter);
                        var textArea = new Rectangle(area.X, image.Bottom, area.Width, Math.Max(0, area.Bottom - image.Bottom));
                        return (Align(textArea, textSize, _textAlign), image);
                    }
                case TextImageRelation.TextAboveImage:
                    {
                        var text = Align(area, textSize, ContentAlignment.TopCenter);
                        var imageArea = new Rectangle(area.X, text.Bottom, area.Width, Math.Max(0, area.Bottom - text.Bottom));
                        return (text, Align(imageArea, imageSize, _imageAlign));
                    }
                default:
                    return (Align(area, textSize, _textAlign), Align(area, imageSize, _imageAlign));
            }
        }
        return (showText ? Align(area, textSize, _textAlign) : Rectangle.Empty, showImage ? Align(area, imageSize, _imageAlign) : Rectangle.Empty);
    }

    internal static Rectangle Align(Rectangle area, Size size, ContentAlignment alignment)
    {
        int x = alignment switch
        {
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => area.X + (area.Width - size.Width) / 2,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => area.Right - size.Width,
            _ => area.X,
        };
        int y = alignment switch
        {
            ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => area.Y + (area.Height - size.Height) / 2,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => area.Bottom - size.Height,
            _ => area.Y,
        };
        return new Rectangle(x, y, size.Width, size.Height);
    }

    public override string ToString() => string.IsNullOrEmpty(_text) ? base.ToString()! : _text;

    // --- design-time serialization (found by name by PropertyDescriptor, as in WinForms) ---------

    internal virtual bool ShouldSerializeBackColor() => !_backColor.IsEmpty;

    internal virtual bool ShouldSerializeForeColor() => !_foreColor.IsEmpty;

    internal virtual bool ShouldSerializeFont() => _font != null;

    internal bool ShouldSerializeDisplayStyle() => DisplayStyle != DefaultDisplayStyle;

    internal bool ShouldSerializeImage() => _image != null && ImageIndex < 0 && string.IsNullOrEmpty(ImageKey);

    internal bool ShouldSerializeImageIndex() => ImageIndex >= 0;

    internal bool ShouldSerializeImageKey() => !string.IsNullOrEmpty(ImageKey);

    internal bool ShouldSerializeImageTransparentColor() => ImageTransparentColor != Color.Empty;

    internal bool ShouldSerializeMargin() => Margin != DefaultMargin;

    internal bool ShouldSerializePadding() => Padding != DefaultPadding;

    internal bool ShouldSerializeToolTipText() => !string.IsNullOrEmpty(ToolTipText);

    internal bool ShouldSerializeVisible() => !_visible;
}
