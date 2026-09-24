using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

/// <summary>
/// A strip of items: tool bar, menu bar, status bar and the body of a drop-down all derive from it.
/// Items are components, not controls - the strip lays them out, paints them through its
/// <see cref="Renderer"/> and routes mouse and keyboard input to them, exactly as WinForms does.
/// The only real child controls a strip has are the ones hosted by <see cref="ToolStripControlHost"/>.
/// </summary>
[DefaultProperty(nameof(Items))]
[DefaultEvent(nameof(ItemClicked))]
public class ToolStrip : ScrollableControl
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

    /// <summary>
    /// Thickness of the move grip: dotnet/winforms' ToolStripGrip is 3 without visual styles and 5 with
    /// them - and an application enables them (ApplicationConfiguration). Checked against WinForms, decision 110.
    /// </summary>
    internal const int GripThickness = 5;

    internal const int OverflowButtonWidth = 16;

    private readonly ToolStripItemCollection _items;
    private readonly List<ToolStripItem> _displayed = new();
    private ToolStripRenderer? _renderer;
    private ToolStripRenderMode _renderMode = ToolStripRenderMode.ManagerRenderMode;
    private ToolStripLayoutStyle _layoutStyle = ToolStripLayoutStyle.StackWithOverflow;
    private ToolStripGripStyle _gripStyle = ToolStripGripStyle.Visible;
    private Padding _gripMargin = new Padding(2);
    private Rectangle _gripBounds;
    private Size _imageScalingSize = new Size(16, 16);
    private ToolStripItem? _selectedItem;
    private ToolStripItem? _pressedItem;
    private ToolStripOverflowButton? _overflowButton;
    private bool _canOverflow = true;
    private bool _layingOutItems;
    private bool _itemLayoutPending;
    private ToolTip? _itemToolTip;
    private ToolStripItem? _toolTipItem;

    public ToolStrip()
    {
        SetStyle(ControlStyles.ContainerControl | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
        CausesValidation = false;
        _items = new ToolStripItemCollection(this, null);
        ShowItemToolTips = DefaultShowItemToolTips;
        Dock = DefaultDock;
        _gripMargin = DefaultGripMargin;
        // As in WinForms: the strip sizes itself to its items - docked, only across the dock.
        AutoSize = true;
        ToolStripManager.Register(this);
    }

    public ToolStrip(params ToolStripItem[] items) : this()
    {
        _items.AddRange(items);
    }

    protected override Size DefaultSize => new Size(100, 25);

    protected override Padding DefaultPadding => new Padding(0, 0, 1, 0);

    protected override Padding DefaultMargin => Padding.Empty;

    protected virtual Padding DefaultGripMargin => new Padding(2);

    protected virtual DockStyle DefaultDock => DockStyle.Top;

    protected virtual bool DefaultShowItemToolTips => true;

    // --- items -------------------------------------------------------------------------

    [Category("Data")]
    [Description("Collection of items to display on the ToolStrip.")]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public virtual ToolStripItemCollection Items => _items;

    /// <summary>The items that are actually placed on the strip: no hidden ones, nothing on the overflow.</summary>
    public ToolStripItemCollection DisplayedItems
    {
        get
        {
            EnsureItemLayout();
            return new ToolStripItemCollection(this, _displayed.ToArray(), isReadOnly: true);
        }
    }

    protected internal virtual ToolStripItem CreateDefaultItem(string? text, Image? image, EventHandler? onClick)
    {
        if (text == "-") return new ToolStripSeparator();
        return new ToolStripButton(text, image, onClick);
    }

    protected internal virtual void OnItemAdded(ToolStripItemEventArgs e)
    {
        ItemAdded?.Invoke(this, e);
    }

    protected internal virtual void OnItemRemoved(ToolStripItemEventArgs e)
    {
        if (_selectedItem == e.Item) _selectedItem = null;
        if (_pressedItem == e.Item) _pressedItem = null;
        if (e.Item is ToolStripControlHost host) host.Detach();
        ItemRemoved?.Invoke(this, e);
    }

    protected virtual void OnItemClicked(ToolStripItemClickedEventArgs e) => ItemClicked?.Invoke(this, e);

    internal virtual void HandleItemClick(ToolStripItem item)
    {
        OnItemClicked(new ToolStripItemClickedEventArgs(item));
    }

    /// <summary>An item changed in a way that affects the layout: re-run it before the next paint.</summary>
    internal void ItemsChanged()
    {
        if (_layingOutItems) return;
        _itemLayoutPending = true;
        PerformLayout(this, nameof(Items));
        Invalidate();
    }

    /// <summary>Flush a deferred item layout before anything reads item bounds (hit-test, DisplayedItems).</summary>
    internal void EnsureItemLayout()
    {
        if (_itemLayoutPending) PerformItemLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnsureItemLayout();
    }

    // --- appearance --------------------------------------------------------------------

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStripRenderer Renderer
    {
        get
        {
            if (_renderMode == ToolStripRenderMode.ManagerRenderMode) return ToolStripManager.Renderer;
            return _renderer ??= CreateRenderer(_renderMode);
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_renderer, value) && _renderMode == ToolStripRenderMode.Custom) return;
            _renderMode = ToolStripRenderMode.Custom;
            _renderer = value;
            OnRendererChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [Description("The painting styles applied to the control.")]
    public ToolStripRenderMode RenderMode
    {
        get => _renderMode;
        set
        {
            if (value == ToolStripRenderMode.Custom && _renderer == null)
                throw new NotSupportedException("Set the Renderer property to use ToolStripRenderMode.Custom.");
            if (_renderMode == value) return;
            _renderMode = value;
            if (value != ToolStripRenderMode.Custom) _renderer = null;
            OnRendererChanged(EventArgs.Empty);
        }
    }

    private static ToolStripRenderer CreateRenderer(ToolStripRenderMode mode) => mode switch
    {
        ToolStripRenderMode.System => new ToolStripSystemRenderer(),
        _ => new ToolStripProfessionalRenderer(),
    };

    protected virtual void OnRendererChanged(EventArgs e)
    {
        RendererChanged?.Invoke(this, e);
        ItemsChanged();
    }

    /// <summary>Mnemonic underlines are always shown; WinForms hides them until Alt is pressed (docs/PLAN.md).</summary>
    internal bool ShowKeyboardCuesInternal => true;

    [Category("Appearance")]
    [Description("Specifies the background color of the ToolStrip.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : Theme.StripBackground;
        set => base.BackColor = value;
    }

    [Category("Appearance")]
    [Description("Specifies the size of images on items.  To control the scaling of items, use the 'ToolStripItem.ImageScaling' property.")]
    [DefaultValue(typeof(Size), "16, 16")]
    public Size ImageScalingSize
    {
        get => _imageScalingSize;
        set
        {
            if (_imageScalingSize == value) return;
            _imageScalingSize = value;
            ItemsChanged();
        }
    }

    [Category("Appearance")]
    [Description("Specifies the ImageList on the ToolStrip.")]
    [DefaultValue(null)]
    [Browsable(false)]
    public ImageList? ImageList { get; set; }

    [Category("Behavior")]
    [Description("Specifies whether to display ToolTips on items.")]
    [DefaultValue(true)]
    public bool ShowItemToolTips { get; set; }

    /// <summary>Items are dropped from the tab order: a strip is reached with Alt and the arrow keys.</summary>
    protected override bool CanEnableIme => false;

    [Category("Behavior")]
    [Description("Allow the items to be merged.")]
    [DefaultValue(true)]
    public bool AllowMerge { get; set; } = true;

    [Category("Behavior")]
    [Description("Allows the items to be reordered when the ALT key is pressed.")]
    [DefaultValue(false)]
    public bool AllowItemReorder { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the control can accept data that the user drags onto it.")]
    [DefaultValue(false)]
    public bool AllowDrop { get; set; }

    [Category("Appearance")]
    [Description("Specifies visibility of the grip on the ToolStrip.")]
    [DefaultValue(ToolStripGripStyle.Visible)]
    public ToolStripGripStyle GripStyle
    {
        get => _gripStyle;
        set
        {
            if (_gripStyle == value) return;
            _gripStyle = value;
            ItemsChanged();
        }
    }

    [Category("Layout")]
    [Description("Specifies the orientation of the grip on the ToolStrip.")]
    public Padding GripMargin
    {
        get => _gripMargin;
        set
        {
            if (_gripMargin == value) return;
            _gripMargin = value;
            ItemsChanged();
        }
    }

    [Browsable(false)]
    public Rectangle GripRectangle => _gripStyle == ToolStripGripStyle.Visible ? _gripBounds : Rectangle.Empty;

    [Browsable(false)]
    public ToolStripGripDisplayStyle GripDisplayStyle =>
        Orientation == Orientation.Horizontal ? ToolStripGripDisplayStyle.Vertical : ToolStripGripDisplayStyle.Horizontal;

    /// <summary>Reading this gives the resolved style: StackWithOverflow reports which way it stacked.</summary>
    [Category("Layout")]
    [Description("Specifies the layout orientation of the ToolStrip.")]
    public virtual ToolStripLayoutStyle LayoutStyle
    {
        get => ResolvedLayoutStyle;
        set
        {
            if (_layoutStyle == value) return;
            _layoutStyle = value;
            OnLayoutStyleChanged(EventArgs.Empty);
            ItemsChanged();
        }
    }

    /// <summary>The style as set, before StackWithOverflow is resolved against the dock edge.</summary>
    internal ToolStripLayoutStyle LayoutStyleSetting => _layoutStyle;

    protected virtual void OnLayoutStyleChanged(EventArgs e) => LayoutStyleChanged?.Invoke(this, e);

    /// <summary>Horizontal unless the strip is docked to a side (or told to stack vertically).</summary>
    [Browsable(false)]
    public Orientation Orientation => ResolvedLayoutStyle == ToolStripLayoutStyle.VerticalStackWithOverflow ? Orientation.Vertical : Orientation.Horizontal;

    internal ToolStripLayoutStyle ResolvedLayoutStyle => _layoutStyle != ToolStripLayoutStyle.StackWithOverflow
        ? _layoutStyle
        : Dock is DockStyle.Left or DockStyle.Right ? ToolStripLayoutStyle.VerticalStackWithOverflow : ToolStripLayoutStyle.HorizontalStackWithOverflow;

    [Category("Behavior")]
    [Description("Specifies the default direction for DropDowns to open.")]
    [Browsable(false)]
    public virtual ToolStripDropDownDirection DefaultDropDownDirection { get; set; } = ToolStripDropDownDirection.Default;

    internal bool ShouldSerializeDefaultDropDownDirection() => DefaultDropDownDirection != ToolStripDropDownDirection.Default;

    internal bool ShouldSerializeGripMargin() => GripMargin != DefaultGripMargin;

    internal bool ShouldSerializeLayoutStyle() => _layoutStyle != ToolStripLayoutStyle.StackWithOverflow;

    /// <summary>ManagerRenderMode is the default; Custom is implied by setting Renderer, which is written instead.</summary>
    internal bool ShouldSerializeRenderMode() => _renderMode != ToolStripRenderMode.ManagerRenderMode && _renderMode != ToolStripRenderMode.Custom;

    public void ResetRenderMode() => RenderMode = ToolStripRenderMode.ManagerRenderMode;

    // --- overflow -----------------------------------------------------------------------

    [Category("Layout")]
    [Description("Indicates whether items can be sent to an overflow menu.")]
    [DefaultValue(true)]
    public bool CanOverflow
    {
        get => _canOverflow;
        set
        {
            if (_canOverflow == value) return;
            _canOverflow = value;
            ItemsChanged();
        }
    }

    [Browsable(false)]
    public ToolStripOverflowButton OverflowButton => _overflowButton ??= new ToolStripOverflowButton(this);

    internal bool OverflowButtonVisible { get; private set; }

    // --- events ---------------------------------------------------------------------------

    [Category("Action")]
    [Description("Occurs when the item is clicked.")]
    public event ToolStripItemClickedEventHandler? ItemClicked;

    [Category("Appearance")]
    [Description("Occurs when a ToolStripItem has been added to the ToolStrip's item collection.")]
    public event ToolStripItemEventHandler? ItemAdded;

    [Category("Appearance")]
    [Description("Occurs when a ToolStripItem has been removed from the ToolStrip's item collection.")]
    public event ToolStripItemEventHandler? ItemRemoved;
    public event EventHandler? RendererChanged;

    [Category("Appearance")]
    [Description("Occurs when the layout style of the ToolStrip has changed.")]
    public event EventHandler? LayoutStyleChanged;

    [Category("Appearance")]
    [Description("Occurs when the move handle needs repainting.")]
    public event PaintEventHandler? PaintGrip;

    protected virtual void OnPaintGrip(PaintEventArgs e) => PaintGrip?.Invoke(this, e);

    // --- layout ---------------------------------------------------------------------------

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override LayoutEngine LayoutEngine => ToolStripLayout.Instance;

    /// <summary>Runs the item layout; the LayoutEngine hook keeps Control's Layout event firing as usual.</summary>
    private sealed class ToolStripLayout : LayoutEngine
    {
        public static readonly ToolStripLayout Instance = new();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            var strip = (ToolStrip)container;
            strip.PerformItemLayout();
            strip.AdjustSizeToPreferred();
            return false;
        }
    }

    internal void PerformItemLayout()
    {
        if (_layingOutItems) return;
        _layingOutItems = true;
        _itemLayoutPending = false;
        try
        {
            _displayed.Clear();
            LayoutItems();
        }
        finally
        {
            _layingOutItems = false;
        }
    }

    /// <summary>Places every available item; drop-downs and the overflow replace this with their own rules.</summary>
    private protected virtual void LayoutItems()
    {
        var display = DisplayRectangle;
        LayoutGrip(ref display);
        switch (ResolvedLayoutStyle)
        {
            case ToolStripLayoutStyle.VerticalStackWithOverflow:
                LayoutStack(display, vertical: true);
                break;
            case ToolStripLayoutStyle.Flow:
                LayoutFlow(display);
                break;
            case ToolStripLayoutStyle.Table:
                LayoutTable(display);
                break;
            default:
                LayoutStack(display, vertical: false);
                break;
        }
    }

    private void LayoutGrip(ref Rectangle display)
    {
        if (_gripStyle != ToolStripGripStyle.Visible)
        {
            _gripBounds = Rectangle.Empty;
            return;
        }
        // The grip margin only applies along the strip: across it the grip spans the whole
        // display rectangle, which is what WinForms reports (a 400x25 strip grips at 2,0,3,25).
        if (Orientation == Orientation.Horizontal)
        {
            _gripBounds = new Rectangle(display.Left + _gripMargin.Left, display.Top, GripThickness, display.Height);
            int consumed = _gripMargin.Horizontal + GripThickness;
            display = new Rectangle(display.Left + consumed, display.Top, Math.Max(0, display.Width - consumed), display.Height);
        }
        else
        {
            _gripBounds = new Rectangle(display.Left, display.Top + _gripMargin.Top, display.Width, GripThickness);
            int consumed = _gripMargin.Vertical + GripThickness;
            display = new Rectangle(display.Left, display.Top + consumed, display.Width, Math.Max(0, display.Height - consumed));
        }
    }

    /// <summary>The size an item wants along the flow; across it, items stretch to the strip.</summary>
    private Size MeasureItem(ToolStripItem item, Rectangle display, bool vertical)
    {
        int across = vertical ? Math.Max(0, display.Width - item.Margin.Horizontal) : Math.Max(0, display.Height - item.Margin.Vertical);
        var pref = item.GetPreferredSize(vertical ? new Size(across, 0) : new Size(0, across));
        return vertical ? new Size(across, pref.Height) : new Size(pref.Width, across);
    }

    private void LayoutStack(Rectangle display, bool vertical)
    {
        var main = new List<ToolStripItem>();
        var overflow = new List<ToolStripItem>();
        foreach (var item in _items)
        {
            if (!item.Available)
            {
                item.Placement = ToolStripItemPlacement.None;
                continue;
            }
            if (_canOverflow && item.Overflow == ToolStripItemOverflow.Always) overflow.Add(item);
            else main.Add(item);
        }

        int available = vertical ? display.Height : display.Width;
        int required = 0;
        var sizes = new Dictionary<ToolStripItem, Size>();
        foreach (var item in main)
        {
            var size = MeasureItem(item, display, vertical);
            sizes[item] = size;
            required += (vertical ? size.Height + item.Margin.Vertical : size.Width + item.Margin.Horizontal);
        }

        OverflowButtonVisible = false;
        if (_canOverflow && required > available && main.Count > 0)
        {
            OverflowButtonVisible = true;
            available -= OverflowButtonWidth;
            // Push items off the end until the rest fits; items marked Never stay put.
            for (int i = main.Count - 1; i >= 0 && required > available; i--)
            {
                var item = main[i];
                if (item.Overflow == ToolStripItemOverflow.Never) continue;
                required -= vertical ? sizes[item].Height + item.Margin.Vertical : sizes[item].Width + item.Margin.Horizontal;
                main.RemoveAt(i);
                overflow.Insert(0, item);
            }
        }
        else if (overflow.Count > 0 && _canOverflow)
        {
            OverflowButtonVisible = true;
            available -= OverflowButtonWidth;
        }

        int start = vertical ? display.Top : display.Left;
        int end = start + available;

        foreach (var item in main)
        {
            var size = sizes[item];
            var margin = item.Margin;
            if (item.Alignment == ToolStripItemAlignment.Right)
            {
                if (vertical)
                {
                    end -= size.Height + margin.Bottom;
                    item.SetBounds(new Rectangle(display.Left + margin.Left, end, size.Width, size.Height));
                    end -= margin.Top;
                }
                else
                {
                    end -= size.Width + margin.Right;
                    item.SetBounds(new Rectangle(end, display.Top + margin.Top, size.Width, size.Height));
                    end -= margin.Left;
                }
            }
            else
            {
                if (vertical)
                {
                    start += margin.Top;
                    item.SetBounds(new Rectangle(display.Left + margin.Left, start, size.Width, size.Height));
                    start += size.Height + margin.Bottom;
                }
                else
                {
                    start += margin.Left;
                    item.SetBounds(new Rectangle(start, display.Top + margin.Top, size.Width, size.Height));
                    start += size.Width + margin.Right;
                }
            }
            item.Placement = ToolStripItemPlacement.Main;
            _displayed.Add(item);
        }

        foreach (var item in overflow)
        {
            item.Placement = ToolStripItemPlacement.Overflow;
            // A hosted control has no business on the strip once its item moved off it.
            if (item is ToolStripControlHost host) host.Control.Visible = false;
        }
        if (_overflowButton != null || OverflowButtonVisible)
        {
            var button = OverflowButton;
            button.SetOverflowItems(overflow);
            button.SetBounds(vertical
                ? new Rectangle(display.Left, display.Bottom - OverflowButtonWidth, display.Width, OverflowButtonWidth)
                : new Rectangle(display.Right - OverflowButtonWidth, display.Top, OverflowButtonWidth, display.Height));
        }
    }

    private void LayoutFlow(Rectangle display)
    {
        int x = display.Left, y = display.Top, rowHeight = 0;
        OverflowButtonVisible = false;
        foreach (var item in _items)
        {
            if (!item.Available)
            {
                item.Placement = ToolStripItemPlacement.None;
                continue;
            }
            var margin = item.Margin;
            var pref = item.GetPreferredSize(Size.Empty);
            if (x + margin.Left + pref.Width + margin.Right > display.Right && x > display.Left)
            {
                x = display.Left;
                y += rowHeight;
                rowHeight = 0;
            }
            item.SetBounds(new Rectangle(x + margin.Left, y + margin.Top, pref.Width, pref.Height));
            item.Placement = ToolStripItemPlacement.Main;
            _displayed.Add(item);
            x += margin.Left + pref.Width + margin.Right;
            rowHeight = Math.Max(rowHeight, pref.Height + margin.Vertical);
        }
    }

    /// <summary>
    /// The row layout StatusStrip uses: every item keeps its preferred width except Spring labels,
    /// which share what is left over equally.
    /// </summary>
    private void LayoutTable(Rectangle display)
    {
        OverflowButtonVisible = false;
        var items = new List<ToolStripItem>();
        foreach (var item in _items)
        {
            if (item.Available) items.Add(item);
            else item.Placement = ToolStripItemPlacement.None;
        }

        int fixedWidth = 0, springs = 0;
        var sizes = new Dictionary<ToolStripItem, Size>();
        foreach (var item in items)
        {
            var size = MeasureItem(item, display, vertical: false);
            sizes[item] = size;
            if (item is ToolStripStatusLabel { Spring: true }) springs++;
            else fixedWidth += size.Width + item.Margin.Horizontal;
        }

        int remaining = Math.Max(0, display.Width - fixedWidth);
        foreach (var item in items)
        {
            if (item is ToolStripStatusLabel { Spring: true }) remaining -= item.Margin.Horizontal;
        }
        int springWidth = springs > 0 ? Math.Max(0, remaining / springs) : 0;
        int extra = springs > 0 ? Math.Max(0, remaining - springWidth * springs) : 0;

        int x = display.Left;
        foreach (var item in items)
        {
            var size = sizes[item];
            var margin = item.Margin;
            int width = size.Width;
            if (item is ToolStripStatusLabel { Spring: true })
            {
                width = springWidth + extra;
                extra = 0;
            }
            x += margin.Left;
            item.SetBounds(new Rectangle(x, display.Top + margin.Top, width, size.Height));
            item.Placement = ToolStripItemPlacement.Main;
            _displayed.Add(item);
            x += width + margin.Right;
        }
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        EnsureItemLayout();
        bool vertical = Orientation == Orientation.Vertical;
        int along = 0, across = 0;
        foreach (var item in _items)
        {
            if (!item.Available) continue;
            var size = item.GetPreferredSize(Size.Empty);
            if (vertical)
            {
                along += size.Height + item.Margin.Vertical;
                across = Math.Max(across, size.Width + item.Margin.Horizontal);
            }
            else
            {
                along += size.Width + item.Margin.Horizontal;
                across = Math.Max(across, size.Height + item.Margin.Vertical);
            }
        }
        if (_gripStyle == ToolStripGripStyle.Visible)
        {
            along += GripThickness + (vertical ? _gripMargin.Vertical : _gripMargin.Horizontal);
        }
        var padding = Padding;
        // WinForms' GetPreferredSizeHorizontal/Vertical: the default size (less padding) is the minimum
        // across the strip, so a strip of short items keeps its 25px (a StatusStrip its 22px).
        var minimum = DefaultSize - padding.Size;
        across = Math.Max(across, vertical ? minimum.Width : minimum.Height);
        return vertical
            ? new Size(across + padding.Horizontal, along + padding.Vertical)
            : new Size(along + padding.Horizontal, across + padding.Vertical);
    }

    // --- painting ---------------------------------------------------------------------------

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Renderer.DrawToolStripBackground(new ToolStripRenderEventArgs(e.Graphics, this, ClientRectangle, BackColor));
        if (_gripStyle == ToolStripGripStyle.Visible)
        {
            Renderer.DrawGrip(new ToolStripGripRenderEventArgs(e.Graphics, this));
            OnPaintGrip(e);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        // No layout here: it can attach a hosted control, and mutating the tree while painting
        // is exactly what a retained-mode backend forbids. Pending layout is flushed by
        // PerformLayout before the frame; painting one stale frame is harmless.
        foreach (var item in _displayed)
        {
            PaintItem(e, item);
        }
        if (OverflowButtonVisible) PaintItem(e, OverflowButton);
        Renderer.DrawToolStripBorder(new ToolStripRenderEventArgs(e.Graphics, this, ClientRectangle, BackColor));
        base.OnPaint(e);
    }

    /// <summary>Paints one item with the Graphics translated to its origin, as WinForms does.</summary>
    private protected void PaintItem(PaintEventArgs e, ToolStripItem item)
    {
        var bounds = item.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0 || !bounds.IntersectsWith(e.ClipRectangle)) return;

        var canvas = e.Graphics.Canvas;
        int saved = canvas.Save();
        canvas.ClipRect(new SkiaSharp.SKRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom), SkiaSharp.SKClipOperation.Intersect, false);
        canvas.Translate(bounds.X, bounds.Y);
        using (var g = Graphics.FromCanvas(canvas))
        using (var args = new PaintEventArgs(g, new Rectangle(Point.Empty, bounds.Size)))
        {
            item.PaintItem(args);
        }
        canvas.RestoreToCount(saved);
    }

    // --- input ---------------------------------------------------------------------------------

    /// <summary>The item under a point in client coordinates, or null.</summary>
    public ToolStripItem? GetItemAt(Point point)
    {
        EnsureItemLayout();
        foreach (var item in _displayed)
        {
            if (item.Bounds.Contains(point)) return item;
        }
        if (OverflowButtonVisible && OverflowButton.Bounds.Contains(point)) return OverflowButton;
        return null;
    }

    public ToolStripItem? GetItemAt(int x, int y) => GetItemAt(new Point(x, y));

    /// <summary>The next selectable item in the given direction, wrapping at the ends (WinForms' GetNextItem).</summary>
    public virtual ToolStripItem? GetNextItem(ToolStripItem? start, ArrowDirection direction)
    {
        EnsureItemLayout();
        var order = new List<ToolStripItem>();
        foreach (var item in _items)
        {
            if (item.Available && item.CanSelect) order.Add(item);
        }
        if (order.Count == 0) return null;
        bool forward = direction is ArrowDirection.Right or ArrowDirection.Down;
        int index = start == null ? (forward ? -1 : 0) : order.IndexOf(start);
        if (index < 0) index = forward ? -1 : 0;
        int next = forward ? index + 1 : index - 1;
        if (next >= order.Count) next = 0;
        if (next < 0) next = order.Count - 1;
        return order[next];
    }

    internal ToolStripItem? SelectedItemInternal => _selectedItem;

    /// <summary>Used by the layouts derived strips write themselves (drop-downs, overflow).</summary>
    private protected void AddDisplayedItem(ToolStripItem item) => _displayed.Add(item);

    internal virtual void SelectItem(ToolStripItem? item)
    {
        if (_selectedItem == item) return;
        _selectedItem?.SetSelected(false);
        _selectedItem = item;
        item?.SetSelected(true);
        UpdateItemToolTip(item);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        var item = GetItemAt(e.Location);
        if (item != null && item.Enabled && item.CanSelect)
        {
            _pressedItem = item;
            SelectItem(item);
            item.HandleMouseDown(Translate(e, item));
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        var pressed = _pressedItem;
        _pressedItem = null;
        if (pressed != null)
        {
            pressed.HandleMouseUp(Translate(e, pressed));
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var item = GetItemAt(e.Location);
        if (item != null && !item.CanSelect) item = null;
        SelectItem(item);
        if (item != null) item.HandleMouseMove(Translate(e, item));
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_pressedItem == null) SelectItem(null);
        base.OnMouseLeave(e);
    }

    private static MouseEventArgs Translate(MouseEventArgs e, ToolStripItem item) =>
        new MouseEventArgs(e.Button, e.Clicks, e.X - item.Bounds.X, e.Y - item.Bounds.Y, e.Delta);

    // --- item tool tips --------------------------------------------------------------------------

    private void UpdateItemToolTip(ToolStripItem? item)
    {
        if (!ShowItemToolTips)
        {
            _toolTipItem = null;
            return;
        }
        if (ReferenceEquals(_toolTipItem, item)) return;
        _toolTipItem = item;
        _itemToolTip ??= new ToolTip();
        var text = item?.ToolTipText;
        if (string.IsNullOrEmpty(text) && item is { AutoToolTip: true }) text = item.Text;
        if (string.IsNullOrEmpty(text)) _itemToolTip.SetToolTip(this, null);
        else _itemToolTip.SetToolTip(this, text);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ToolStripManager.Unregister(this);
            _itemToolTip?.Dispose();
            foreach (var item in _items.ToArray()) item.Dispose();
        }
        base.Dispose(disposing);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [DefaultValue(false)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoScroll { get => base.AutoScroll; set => base.AutoScroll = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMargin { get => base.AutoScrollMargin; set => base.AutoScrollMargin = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMinSize { get => base.AutoScrollMinSize; set => base.AutoScrollMinSize = value; }

    [Category("Layout")]
    [DefaultValue(true)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Category("Focus")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CausesValidation { get => base.CausesValidation; set => base.CausesValidation = value; }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new ControlCollection Controls => base.Controls;

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override Cursor Cursor { get => base.Cursor; set => base.Cursor = value; }

    [Category("Layout")]
    [DefaultValue(DockStyle.Top)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override DockStyle Dock { get => base.Dock; set => base.Dock = value; }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor { get => base.ForeColor; set => base.ForeColor = value; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
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

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event ControlEventHandler? ControlAdded
    {
        add => base.ControlAdded += value;
        remove => base.ControlAdded -= value;
    }

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event ControlEventHandler? ControlRemoved
    {
        add => base.ControlRemoved += value;
        remove => base.ControlRemoved -= value;
    }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? CursorChanged
    {
        add => base.CursorChanged += value;
        remove => base.CursorChanged -= value;
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
}

/// <summary>The chevron at the end of a full strip; its drop-down holds the items that did not fit.</summary>
public class ToolStripOverflowButton : ToolStripDropDownItem
{
    private readonly ToolStrip _parentStrip;

    internal ToolStripOverflowButton(ToolStrip parentToolStrip)
    {
        _parentStrip = parentToolStrip;
        SetOwner(parentToolStrip);
        Alignment = ToolStripItemAlignment.Right;
    }

    protected override bool DefaultAutoToolTip => true;

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool HasDropDownItems => DropDown is ToolStripOverflow { HasItems: true };

    protected override ToolStripDropDown CreateDefaultDropDown() => new ToolStripOverflow(this);

    internal void SetOverflowItems(IReadOnlyList<ToolStripItem> items)
    {
        var dropDown = DropDown;
        // The items stay owned by the strip; the overflow only borrows them for display.
        ((ToolStripOverflow)dropDown).SetItems(items);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var renderer = Owner?.Renderer ?? ToolStripManager.Renderer;
        renderer.DrawOverflowButtonBackground(new ToolStripItemRenderEventArgs(e.Graphics, this));
        var bounds = new Rectangle(Point.Empty, Size);
        renderer.DrawArrow(new ToolStripArrowRenderEventArgs(e.Graphics, this, bounds, Enabled ? ForeColor : Theme.DisabledText,
            _parentStrip.Orientation == Orientation.Horizontal ? ArrowDirection.Down : ArrowDirection.Right));
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool AutoToolTip { get => base.AutoToolTip; set => base.AutoToolTip = value; }
}
