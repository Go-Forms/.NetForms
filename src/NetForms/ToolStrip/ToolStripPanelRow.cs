using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

/// <summary>
/// One row of a <see cref="ToolStripPanel"/>: tool strips side by side, in the order of their position along
/// the row. A strip keeps where it was put; one that would overlap the strip before it is pushed along, and
/// the row squeezes them back when they run past its end. Rows of a vertical panel are columns.
/// WinForms' row managers do the same with cells and overflow; here a strip that does not fit simply
/// overlaps (decision 178).
/// </summary>
[ToolboxItem(false)]
public class ToolStripPanelRow : Component
{
    private readonly List<Control> _controls = new();
    private Rectangle _bounds;
    private Padding _margin;
    private Padding _padding;

    public ToolStripPanelRow(ToolStripPanel parent)
    {
        ToolStripPanel = parent ?? throw new ArgumentNullException(nameof(parent));
        _margin = DefaultMargin;
        _padding = DefaultPadding;
    }

    public Rectangle Bounds => _bounds;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The collection of child controls within this control.")]
    public Control[] Controls => _controls.ToArray();

    internal List<Control> ControlsInternal => _controls;

    protected virtual Padding DefaultMargin => ToolStripPanel.RowMargin;

    protected virtual Padding DefaultPadding => Padding.Empty;

    public Rectangle DisplayRectangle => new Rectangle(_bounds.X + _padding.Left, _bounds.Y + _padding.Top,
        Math.Max(0, _bounds.Width - _padding.Horizontal), Math.Max(0, _bounds.Height - _padding.Vertical));

    public LayoutEngine LayoutEngine => RowLayout.Instance;

    public Padding Margin
    {
        get => _margin;
        set
        {
            if (_margin == value) return;
            _margin = value;
            ToolStripPanel.PerformLayout();
        }
    }

    public virtual Padding Padding
    {
        get => _padding;
        set
        {
            if (_padding == value) return;
            _padding = value;
            ToolStripPanel.PerformLayout();
        }
    }

    public ToolStripPanel ToolStripPanel { get; }

    public Orientation Orientation => ToolStripPanel.Orientation;

    private bool Horizontal => Orientation == Orientation.Horizontal;

    /// <summary>The strip can be put in this row: neither is locked, and it is already here or there is room for it.</summary>
    public bool CanMove(ToolStrip toolStripToDrag)
    {
        ArgumentNullException.ThrowIfNull(toolStripToDrag);
        if (ToolStripPanel.Locked) return false;
        if (_controls.Contains(toolStripToDrag)) return true;
        int room = Horizontal ? DisplayRectangle.Width : DisplayRectangle.Height;
        // A row not laid out yet (the panel has no size) takes anything.
        if (room <= 0) return true;
        int used = _controls.Where(c => c.ParticipatesInLayout).Sum(MinimumLength);
        return used + MinimumLength(toolStripToDrag) < room;
    }

    /// <summary>
    /// What a strip needs at the least (WinForms' GetMinimumSize): one that can overflow its items shrinks to its
    /// grip and overflow button, so it still fits in a row that is full; a stretched one or one that cannot overflow
    /// needs all of its length.
    /// </summary>
    private int MinimumLength(Control c)
    {
        if (c is not ToolStrip { CanOverflow: true, Stretch: false } strip) return Length(c);
        int grip = strip.GripStyle == ToolStripGripStyle.Visible ? ToolStrip.GripThickness + (Horizontal ? strip.GripMargin.Horizontal : strip.GripMargin.Vertical) : 0;
        var padding = strip.Padding;
        return grip + ToolStrip.OverflowButtonWidth + (Horizontal ? padding.Horizontal + c.Margin.Horizontal : padding.Vertical + c.Margin.Vertical);
    }

    /// <summary>The length a strip takes along the row, margins included.</summary>
    private int Length(Control c)
    {
        var size = ToolStripPanel.SizeOf(c);
        return Horizontal ? size.Width + c.Margin.Horizontal : size.Height + c.Margin.Vertical;
    }

    /// <summary>How thick the row is across: its thickest strip, margins included.</summary>
    internal int Thickness()
    {
        int thickness = 0;
        foreach (var c in _controls)
        {
            if (!c.ParticipatesInLayout) continue;
            var size = ToolStripPanel.SizeOf(c);
            thickness = Math.Max(thickness, Horizontal ? size.Height + c.Margin.Vertical : size.Width + c.Margin.Horizontal);
        }
        return thickness + (Horizontal ? _padding.Vertical : _padding.Horizontal);
    }

    /// <summary>What the strips need along the row, from its start: the end of the last one.</summary>
    internal int Extent()
    {
        int end = 0;
        foreach (var c in Ordered())
        {
            if (!c.ParticipatesInLayout) continue;
            end = Math.Max(end, Along(c)) + Length(c);
        }
        return end + (Horizontal ? _padding.Horizontal : _padding.Vertical);
    }

    internal bool HasVisibleControls => _controls.Any(c => c.ParticipatesInLayout);

    /// <summary>
    /// Where each strip was put along the row (its left, or top, in the panel): the layout pushes a strip on past
    /// the one before it or squeezes it back into a short row, but it goes back to here when there is room again -
    /// so a panel that is narrow for a moment (while the form is being built) moves nothing for good.
    /// </summary>
    private readonly Dictionary<Control, int> _wanted = new(ReferenceEqualityComparer.Instance);

    /// <summary>Where a strip is meant to be along the row, from the start of its display area (margin excluded).</summary>
    private int Along(Control c)
    {
        var display = DisplayRectangle;
        int at = _wanted.TryGetValue(c, out var wanted) ? wanted : Horizontal ? c.Left : c.Top;
        return Math.Max(0, Horizontal ? at - c.Margin.Left - display.X : at - c.Margin.Top - display.Y);
    }

    private IEnumerable<Control> Ordered() => _controls.OrderBy(Along);

    internal void JoinRow(ToolStrip strip, Point location)
    {
        if (!_controls.Contains(strip))
        {
            _controls.Add(strip);
            OnControlAdded(strip, _controls.Count - 1);
        }
        MoveControl(strip, location);
    }

    /// <summary>A strip joined the row (WinForms' row managers start its cell here).</summary>
    protected internal virtual void OnControlAdded(Control control, int index) { }

    /// <summary>A strip left the row.</summary>
    protected internal virtual void OnControlRemoved(Control control, int index) { }

    /// <summary>The panel turned: the strips of the row are laid out the other way.</summary>
    protected internal virtual void OnOrientationChanged()
    {
        foreach (var c in _controls) if (c is ToolStrip strip) strip.ItemsChanged();
    }

    /// <summary>The strip goes to the position along the row the point gives; the layout settles the rest.</summary>
    internal void MoveControl(ToolStrip strip, Point location)
    {
        var size = strip.Size;
        var b = Horizontal
            ? new Rectangle(location.X, _bounds.Y + strip.Margin.Top, size.Width, size.Height)
            : new Rectangle(_bounds.X + strip.Margin.Left, location.Y, size.Width, size.Height);
        strip.SetBoundsFromLayout(b);
        _wanted[strip] = Horizontal ? location.X : location.Y;
    }

    internal void ClearInternal()
    {
        _controls.Clear();
        _wanted.Clear();
    }

    internal void LeaveRow(Control control)
    {
        int index = _controls.IndexOf(control);
        if (index < 0) return;
        _controls.RemoveAt(index);
        _wanted.Remove(control);
        OnControlRemoved(control, index);
        if (_controls.Count == 0) ToolStripPanel.RowsInternal.RemoveInternal(this);
    }

    internal void SetBounds(Rectangle bounds)
    {
        if (_bounds == bounds) return;
        var old = _bounds;
        _bounds = bounds;
        OnBoundsChanged(old, bounds);
    }

    protected void OnBoundsChanged(Rectangle oldBounds, Rectangle newBounds) { }

    /// <summary>Places the strips: in order along the row, none over the one before, squeezed back from the end when they run past it.</summary>
    protected virtual void OnLayout(LayoutEventArgs e) => Arrange();

    internal void PerformRowLayout(LayoutEventArgs e) => OnLayout(e);

    private void Arrange()
    {
        var display = DisplayRectangle;
        var strips = Ordered().Where(c => c.ParticipatesInLayout).ToList();
        int room = Horizontal ? display.Width : display.Height;
        var at = new int[strips.Count];
        var lengths = new int[strips.Count];
        int cursor = 0;
        for (int i = 0; i < strips.Count; i++)
        {
            var c = strips[i];
            lengths[i] = Length(c);
            if (c is ToolStrip { Stretch: true }) lengths[i] = Math.Max(lengths[i], room - Math.Max(Along(c), cursor));
            at[i] = Math.Max(Along(c), cursor);
            cursor = at[i] + lengths[i];
        }
        int limit = room;
        for (int i = strips.Count - 1; i >= 0 && cursor > room; i--)
        {
            at[i] = Math.Max(i == 0 ? 0 : at[i - 1] + lengths[i - 1], Math.Min(at[i], limit - lengths[i]));
            limit = at[i];
        }
        for (int i = 0; i < strips.Count; i++)
        {
            var c = strips[i];
            var m = c.Margin;
            var size = ToolStripPanel.SizeOf(c);
            if (Horizontal)
            {
                int width = c is ToolStrip { Stretch: true } ? lengths[i] - m.Horizontal : size.Width;
                c.SetBoundsFromLayout(new Rectangle(display.X + at[i] + m.Left, display.Y + m.Top, width, size.Height));
            }
            else
            {
                int height = c is ToolStrip { Stretch: true } ? lengths[i] - m.Vertical : size.Height;
                c.SetBoundsFromLayout(new Rectangle(display.X + m.Left, display.Y + at[i] + m.Top, size.Width, height));
            }
            ToolStripPanel.Placed(c);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ClearInternal();
        base.Dispose(disposing);
    }

    /// <summary>The row's engine: its strips one after another (it is the row that knows how).</summary>
    private sealed class RowLayout : LayoutEngine
    {
        public static readonly RowLayout Instance = new();

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            if (container is ToolStripPanelRow row) row.PerformRowLayout(layoutEventArgs);
            return false;
        }
    }
}
