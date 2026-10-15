using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

/// <summary>
/// An area that holds tool strips in rows, along one edge of a <see cref="ToolStripContainer"/> (or on its own,
/// docked). A strip put into it joins the row its location falls in - a new row when none does - and the user
/// can drag it by its grip to another place, another row or another panel of the same container, as in
/// WinForms. The panel sizes itself to its rows: an empty one takes no room (while designed, it keeps a band
/// to drop a strip onto - decision 178).
/// </summary>
public class ToolStripPanel : ContainerControl
{
    /// <summary>How far outside a panel a dragged strip still lands in it (WinForms' dragMargin).</summary>
    internal const int DragMargin = 10;

    /// <summary>While designed: the band an empty panel keeps (25 for the top one, as in VS), and the one past its last row.</summary>
    internal const int DesignTopBand = 25;
    internal const int DesignBand = 8;

    private readonly ToolStripContainer? _owner;
    private readonly ToolStripPanelRowCollection _rows;
    private readonly Dictionary<Control, Point> _placed = new(ReferenceEqualityComparer.Instance);
    private Orientation _orientation = Orientation.Horizontal;
    private Padding _rowMargin = new Padding(3, 0, 0, 0);
    private ToolStripRenderer? _renderer;
    private ToolStripRenderMode _renderMode = ToolStripRenderMode.ManagerRenderMode;
    private bool _locked;
    private bool _beginInit;
    private bool _inJoin;
    private bool _inLayout;
    private bool _joinPending;

    public ToolStripPanel()
    {
        _rows = new ToolStripPanelRowCollection(this);
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.None;
        AutoSize = true;
        MinimumSize = Size.Empty;
        TabStop = false;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        ResumeLayout(true);
    }

    internal ToolStripPanel(ToolStripContainer owner) : this() => _owner = owner;

    internal ToolStripContainer? Owner => _owner;

    // --- members WinForms hides or re-defaults (checked by AttributeDiffTests) ----------------------

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AllowDrop { get => base.AllowDrop; set => base.AllowDrop = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoScroll { get => base.AutoScroll; set => base.AutoScroll = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMargin { get => base.AutoScrollMargin; set => base.AutoScrollMargin = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size AutoScrollMinSize { get => base.AutoScrollMinSize; set => base.AutoScrollMinSize = value; }

    [DefaultValue(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize { get => base.AutoSize; set => base.AutoSize = value; }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TabIndexChanged
    {
        add => base.TabIndexChanged += value;
        remove => base.TabIndexChanged -= value;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override string Text { get => base.Text; set => base.Text = value; }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }

    protected override Padding DefaultPadding => Padding.Empty;

    protected override Padding DefaultMargin => Padding.Empty;

    // --- the panel's own -------------------------------------------------------------------------

    /// <summary>The margin of every new row: 3 pixels before the first strip, as in WinForms.</summary>
    public Padding RowMargin
    {
        get => _rowMargin;
        set
        {
            _rowMargin = value;
            PerformLayout(this, nameof(RowMargin));
        }
    }

    /// <summary>Docked to a side, the panel is vertical: its rows are columns and its strips stand up.</summary>
    public override DockStyle Dock
    {
        get => base.Dock;
        set
        {
            base.Dock = value;
            Orientation = value is DockStyle.Left or DockStyle.Right ? Orientation.Vertical : Orientation.Horizontal;
        }
    }

    public override LayoutEngine LayoutEngine => PanelLayout.Instance;

    /// <summary>The user cannot drag the strips in it.</summary>
    [DefaultValue(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public bool Locked
    {
        get => _locked;
        set => _locked = value;
    }

    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value) return;
            _orientation = value;
            foreach (ToolStripPanelRow row in _rows) row.OnOrientationChanged();
            PerformLayout(this, nameof(Orientation));
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ToolStripRenderer Renderer
    {
        get
        {
            if (_renderMode == ToolStripRenderMode.ManagerRenderMode) return ToolStripManager.Renderer;
            return _renderer ??= _renderMode == ToolStripRenderMode.System ? new ToolStripSystemRenderer() : new ToolStripProfessionalRenderer();
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

    [Description("The painting styles applied to the control.")]
    [Category("Appearance")]
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

    private bool ShouldSerializeRenderMode() => _renderMode != ToolStripRenderMode.ManagerRenderMode && _renderMode != ToolStripRenderMode.Custom;

    private void ResetRenderMode() => RenderMode = ToolStripRenderMode.ManagerRenderMode;

    private bool ShouldSerializeDock() => _owner == null && Dock != DockStyle.None;

    [Category("Appearance")]
    [Description("Occurs when the value of the Renderer property changes.")]
    public event EventHandler? RendererChanged;

    protected virtual void OnRendererChanged(EventArgs e)
    {
        Invalidate();
        RendererChanged?.Invoke(this, e);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("A collection of rows in the panel. ")]
    public ToolStripPanelRow[] Rows
    {
        get
        {
            var rows = new ToolStripPanelRow[_rows.Count];
            _rows.CopyTo(rows, 0);
            return rows;
        }
    }

    internal ToolStripPanelRowCollection RowsInternal => _rows;

    // --- joining ----------------------------------------------------------------------------------

    /// <summary>Holds back the joining of added strips until <see cref="EndInit"/>, which joins them by their locations.</summary>
    public void BeginInit() => _beginInit = true;

    public void EndInit()
    {
        _beginInit = false;
        JoinControls();
    }

    public void Join(ToolStrip toolStripToDrag) => Join(toolStripToDrag, Point.Empty);

    /// <summary>Into row <paramref name="row"/>, at its start; past the last row, into a new one.</summary>
    public void Join(ToolStrip toolStripToDrag, int row)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        LayoutRows();
        Point location;
        if (row >= _rows.Count)
        {
            var display = DisplayRectangle;
            int end = _rows.Count == 0 ? 0 : (Orientation == Orientation.Horizontal ? _rows[_rows.Count - 1].Bounds.Bottom + _rows[_rows.Count - 1].Margin.Bottom : _rows[_rows.Count - 1].Bounds.Right + _rows[_rows.Count - 1].Margin.Bottom);
            location = Orientation == Orientation.Horizontal ? new Point(display.X, Math.Max(end, display.Y + 1)) : new Point(Math.Max(end, display.X + 1), display.Y);
        }
        else
        {
            var bounds = _rows[row].Bounds;
            location = Orientation == Orientation.Horizontal ? new Point(0, bounds.Bottom - 1) : new Point(bounds.Right - 1, 0);
        }
        Join(toolStripToDrag, location);
    }

    public void Join(ToolStrip toolStripToDrag, int x, int y) => Join(toolStripToDrag, new Point(x, y));

    /// <summary>Into the row <paramref name="location"/> falls in (a new one if none), at that place along it.</summary>
    public void Join(ToolStrip toolStripToDrag, Point location)
    {
        ArgumentNullException.ThrowIfNull(toolStripToDrag);
        if (_beginInit || _inJoin)
        {
            Controls.Add(toolStripToDrag);
            toolStripToDrag.Location = location;
            return;
        }
        try
        {
            _inJoin = true;
            if (toolStripToDrag.Parent != this) Controls.Add(toolStripToDrag);
            MoveInsideContainer(toolStripToDrag, location);
        }
        finally
        {
            _inJoin = false;
        }
        toolStripToDrag.ItemsChanged();
        PerformLayout(toolStripToDrag, "Rows");
    }

    /// <summary>The row whose bounds (with its margin) hold the point; null between or past them.</summary>
    public ToolStripPanelRow? PointToRow(Point clientLocation)
    {
        foreach (ToolStripPanelRow row in _rows)
        {
            var m = row.Margin;
            var bounds = Orientation == Orientation.Horizontal
                ? Rectangle.FromLTRB(row.Bounds.Left - m.Left, row.Bounds.Top - m.Top, row.Bounds.Right + m.Right, row.Bounds.Bottom + m.Bottom)
                : Rectangle.FromLTRB(row.Bounds.Left - m.Top, row.Bounds.Top - m.Left, row.Bounds.Right + m.Bottom, row.Bounds.Bottom + m.Right);
            if (Orientation == Orientation.Horizontal && bounds.Width <= 0) bounds.Width = Math.Max(Width, Parent?.DisplayRectangle.Width ?? 0);
            if (Orientation == Orientation.Vertical && bounds.Height <= 0) bounds.Height = Math.Max(Height, Parent?.DisplayRectangle.Height ?? 0);
            if (bounds.Contains(clientLocation)) return row;
        }
        return null;
    }

    internal ToolStripPanelRow? RowOf(Control control)
    {
        foreach (ToolStripPanelRow row in _rows)
            if (row.ControlsInternal.Contains(control)) return row;
        return null;
    }

    /// <summary>WinForms' MoveInsideContainer: stay in the row, go to the row under the point, or start a new row.</summary>
    private void MoveInsideContainer(ToolStrip strip, Point location)
    {
        LayoutRows();
        var current = RowOf(strip);
        if (current != null && current == PointToRow(location))
        {
            current.MoveControl(strip, location);
            return;
        }
        var row = PointToRow(location);
        if (row == null)
        {
            int index = (Orientation == Orientation.Horizontal ? location.Y : location.X) <= Padding.Left ? 0 : _rows.Count;
            ToolStripPanelRow? previous = _rows.Count == 0 ? null : _rows[index == 0 ? 0 : index - 1];
            if (previous != null && previous.ControlsInternal.Count == 1 && previous.ControlsInternal.Contains(strip))
            {
                previous.MoveControl(strip, location);
                return;
            }
            row = new ToolStripPanelRow(this);
            _rows.InsertInternal(index, row);
        }
        else if (!row.CanMove(strip))
        {
            int index = _rows.IndexOf(row);
            if (current != null && current.ControlsInternal.Count == 1 && index > 0 && index - 1 == _rows.IndexOf(current)) return;
            row = new ToolStripPanelRow(this);
            _rows.InsertInternal(index, row);
        }
        current?.LeaveRow(strip);
        row.JoinRow(strip, location);
    }

    /// <summary>Joins every strip again, in the order of their locations (top to bottom, then along): what EndInit and the end of designer code do.</summary>
    private void JoinControls()
    {
        _joinPending = false;
        var strips = Controls.OfType<ToolStrip>()
            .Select((s, i) => (Strip: s, Index: i, Location: s.Location))
            .OrderBy(s => Orientation == Orientation.Horizontal ? s.Location.Y : s.Location.X)
            .ThenBy(s => Orientation == Orientation.Horizontal ? s.Location.X : s.Location.Y)
            .ThenBy(s => s.Index)
            .ToList();
        foreach (ToolStripPanelRow row in _rows.Cast<ToolStripPanelRow>().ToList()) row.ClearInternal();
        _rows.ClearInternal();
        _placed.Clear();
        try
        {
            _inJoin = true;
            foreach (var (strip, _, location) in strips)
            {
                if (strip.AutoSize) strip.Size = SizeOf(strip);
                MoveInsideContainer(strip, location);
                strip.ItemsChanged();
            }
        }
        finally
        {
            _inJoin = false;
        }
        PerformLayout(this, "Rows");
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        if (_beginInit || _inJoin) return;
        if (IsLayoutSuspended)
        {
            _joinPending = true;
            return;
        }
        if (e.Control is ToolStrip strip) Join(strip, strip.Location);
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        if (e.Control != null)
        {
            RowOf(e.Control)?.LeaveRow(e.Control);
            _placed.Remove(e.Control);
            if (e.Control is ToolStrip strip) strip.ItemsChanged();
        }
        base.OnControlRemoved(e);
        // The last strip gone, the panel shrinks to nothing: its own layout does not resize an empty control.
        if (Controls.Count == 0 && AutoSize) Parent?.PerformLayout(this, nameof(AutoSize));
    }

    internal override void OnLayoutResuming(bool performLayout)
    {
        base.OnLayoutResuming(performLayout);
        if (_joinPending && !_beginInit) JoinControls();
    }

    /// <summary>A strip's location set by code (or the designer): it joins the row there. Its size changing just lays the row out again.</summary>
    private void ChildBoundsSet(Control child, BoundsSpecified specified)
    {
        if (_inLayout || _inJoin || (specified & BoundsSpecified.Location) == 0 || child is not ToolStrip strip) return;
        if (RowOf(strip) == null) return;
        if (_placed.TryGetValue(strip, out var placed) && placed == strip.Location) return;
        if (_beginInit || IsLayoutSuspended)
        {
            _joinPending = true;
            return;
        }
        var location = strip.Location;
        try
        {
            _inJoin = true;
            MoveInsideContainer(strip, location);
        }
        finally
        {
            _inJoin = false;
        }
        PerformLayout(strip, "Rows");
    }

    internal void Placed(Control control) => _placed[control] = control.Location;

    /// <summary>The size a strip takes in a row: its preferred size when it sizes itself.</summary>
    internal Size SizeOf(Control control) => control.AutoSize ? control.GetPreferredSize(Size.Empty) : control.Size;

    /// <summary>The rows, one after another across the panel; each lays out its strips.</summary>
    private void LayoutRows()
    {
        if (_inLayout) return;
        _inLayout = true;
        try
        {
            var display = DisplayRectangle;
            bool horizontal = Orientation == Orientation.Horizontal;
            int position = horizontal ? display.Top : display.Left;
            foreach (ToolStripPanelRow row in _rows)
            {
                // Left and Right are along the row, Top and Bottom across it - turned for a vertical panel,
                // so the 3 pixels of RowMargin come before the first strip either way.
                var m = row.Margin;
                if (!row.HasVisibleControls)
                {
                    row.SetBounds(horizontal ? new Rectangle(display.Left + m.Left, position, Math.Max(0, display.Width - m.Horizontal), 0) : new Rectangle(position, display.Top + m.Left, 0, Math.Max(0, display.Height - m.Horizontal)));
                    continue;
                }
                int thickness = row.Thickness();
                row.SetBounds(horizontal
                    ? new Rectangle(display.Left + m.Left, position + m.Top, Math.Max(0, display.Width - m.Horizontal), thickness)
                    : new Rectangle(position + m.Top, display.Top + m.Left, thickness, Math.Max(0, display.Height - m.Horizontal)));
                row.PerformRowLayout(new LayoutEventArgs(this, "Rows"));
                position += thickness + m.Vertical;
            }
        }
        finally
        {
            _inLayout = false;
        }
    }

    /// <summary>Its rows across, the longest of them along - plus, while designed, the band to drop a strip onto.</summary>
    public override Size GetPreferredSize(Size proposedSize)
    {
        bool horizontal = Orientation == Orientation.Horizontal;
        int across = 0, along = 0;
        foreach (ToolStripPanelRow row in _rows)
        {
            if (!row.HasVisibleControls) continue;
            var m = row.Margin;
            across += row.Thickness() + m.Vertical;
            along = Math.Max(along, row.Extent() + m.Horizontal);
        }
        if (IsAncestorSiteInDesignMode) across += across == 0 && Dock == DockStyle.Top ? DesignTopBand : DesignBand;
        var padding = Padding;
        return horizontal
            ? new Size(along + padding.Horizontal, across + padding.Vertical)
            : new Size(across + padding.Horizontal, along + padding.Vertical);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (_joinPending && !_beginInit && !IsLayoutSuspended) JoinControls();
        base.OnLayout(e);
    }

    protected override void OnParentChanged(EventArgs e)
    {
        if (!_beginInit && !_inJoin && !IsLayoutSuspended && Controls.Count > 0) JoinControls();
        base.OnParentChanged(e);
    }

    protected override void OnDockChanged(EventArgs e)
    {
        if (!_beginInit && !_inJoin && !IsLayoutSuspended && Controls.Count > 0) JoinControls();
        base.OnDockChanged(e);
    }

    protected override void OnRightToLeftChanged(EventArgs e) => base.OnRightToLeftChanged(e);

    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var args = new ToolStripPanelRenderEventArgs(e.Graphics, this);
        Renderer.DrawToolStripPanelBackground(args);
        if (!args.Handled) base.OnPaintBackground(e);
    }

    protected override ControlCollection CreateControlsInstance() => base.CreateControlsInstance();

    protected override AccessibleObject CreateAccessibilityInstance() => base.CreateAccessibilityInstance();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (ToolStripPanelRow row in _rows.Cast<ToolStripPanelRow>().ToList()) row.Dispose();
            _rows.ClearInternal();
            _placed.Clear();
        }
        base.Dispose(disposing);
    }

    // --- dragging a strip by its grip ----------------------------------------------------------------

    /// <summary>
    /// A strip dragged by its grip: to the panel under the pointer (this one, or another panel of the same
    /// container, each a little larger than it is so an empty one can be reached), the row and place there.
    /// <paramref name="pointer"/> is in form coordinates; <paramref name="grab"/> is where the strip was taken.
    /// </summary>
    internal static void Drag(ToolStrip strip, Point pointer, Point grab)
    {
        if (strip.Parent is not ToolStripPanel from || from.Locked) return;
        var target = from;
        if (from.Owner is { } container)
        {
            ToolStripPanel? best = null;
            foreach (var panel in container.Panels)
            {
                if (!panel.Visible || panel.Locked) continue;
                var r = new Rectangle(panel.Parent!.PointToForm(panel.Location), panel.Size);
                r.Inflate(DragMargin, DragMargin);
                if (!r.Contains(pointer)) continue;
                if (panel == from) { best = panel; break; }
                best ??= panel;
            }
            if (best != null) target = best;
        }
        var origin = target.PointToForm(Point.Empty);
        var client = new Point(pointer.X - origin.X, pointer.Y - origin.Y);
        // Along the row the strip follows the pointer; across, the pointer picks the row.
        var location = target.Orientation == Orientation.Horizontal ? new Point(client.X - grab.X, client.Y) : new Point(client.X, client.Y - grab.Y);
        target.Join(strip, location);
    }

    // --- the rows -------------------------------------------------------------------------------------

    /// <summary>The panel's engine: the rows across, the strips along each.</summary>
    private sealed class PanelLayout : LayoutEngine
    {
        public static readonly PanelLayout Instance = new();

        public override void InitLayout(object child, BoundsSpecified specified)
        {
            if (child is Control { Parent: ToolStripPanel panel } control) panel.ChildBoundsSet(control, specified);
        }

        public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
        {
            ((ToolStripPanel)container).LayoutRows();
            return false;
        }
    }

    [ListBindable(false)]
    public class ToolStripPanelRowCollection : ArrangedElementCollection, IList
    {
        private readonly ToolStripPanel _owner;

        public ToolStripPanelRowCollection(ToolStripPanel owner) => _owner = owner;

        public ToolStripPanelRowCollection(ToolStripPanel owner, ToolStripPanelRow[] value) : this(owner) => AddRange(value);

        public virtual ToolStripPanelRow this[int index] => (ToolStripPanelRow)InnerList[index];

        public override bool IsReadOnly => false;

        public int Add(ToolStripPanelRow value)
        {
            ArgumentNullException.ThrowIfNull(value);
            InnerList.Add(value);
            _owner.PerformLayout(_owner, "Rows");
            return InnerList.Count - 1;
        }

        public void AddRange(ToolStripPanelRow[] value)
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var row in value) Add(row);
        }

        public void AddRange(ToolStripPanelRowCollection value)
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (ToolStripPanelRow row in value.InnerList.ToList()) Add(row);
        }

        public bool Contains(ToolStripPanelRow value) => InnerList.Contains(value);

        public virtual void Clear()
        {
            InnerList.Clear();
            _owner.PerformLayout(_owner, "Rows");
        }

        public int IndexOf(ToolStripPanelRow value) => InnerList.IndexOf(value);

        public void Insert(int index, ToolStripPanelRow value)
        {
            ArgumentNullException.ThrowIfNull(value);
            InnerList.Insert(index, value);
            _owner.PerformLayout(_owner, "Rows");
        }

        public void Remove(ToolStripPanelRow value)
        {
            InnerList.Remove(value);
            _owner.PerformLayout(_owner, "Rows");
        }

        public void RemoveAt(int index) => Remove(this[index]);

        public void CopyTo(ToolStripPanelRow[] array, int index) => InnerList.Cast<ToolStripPanelRow>().ToList().CopyTo(array, index);

        internal void InsertInternal(int index, ToolStripPanelRow row) => InnerList.Insert(index, row);

        internal void RemoveInternal(ToolStripPanelRow row) => InnerList.Remove(row);

        internal void ClearInternal() => InnerList.Clear();

        object? IList.this[int index] { get => InnerList[index]; set => throw new NotSupportedException(); }

        int IList.Add(object? value) => Add((ToolStripPanelRow)value!);

        void IList.Clear() => Clear();

        bool IList.Contains(object? value) => value is ToolStripPanelRow row && Contains(row);

        int IList.IndexOf(object? value) => value is ToolStripPanelRow row ? IndexOf(row) : -1;

        void IList.Insert(int index, object? value) => Insert(index, (ToolStripPanelRow)value!);

        void IList.Remove(object? value) { if (value is ToolStripPanelRow row) Remove(row); }

        void IList.RemoveAt(int index) => RemoveAt(index);
    }
}
