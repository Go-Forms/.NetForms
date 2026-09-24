using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

public enum FlowDirection
{
    LeftToRight = 0,
    TopDown = 1,
    RightToLeft = 2,
    BottomUp = 3,
}

/// <summary>Per-control flow settings (flow breaks), as WinForms exposes them through LayoutSettings.</summary>
public class FlowLayoutSettings : LayoutSettings
{
    private readonly Dictionary<Control, bool> _breaks = new();

    internal FlowLayoutSettings(FlowLayoutPanel owner) => Owner = owner;

    internal FlowLayoutPanel Owner { get; }

    public override LayoutEngine LayoutEngine => FlowLayout.Instance;

    public FlowDirection FlowDirection
    {
        get => Owner.FlowDirection;
        set => Owner.FlowDirection = value;
    }

    public bool WrapContents
    {
        get => Owner.WrapContents;
        set => Owner.WrapContents = value;
    }

    public bool GetFlowBreak(object child) => child is Control c && _breaks.TryGetValue(c, out var b) && b;

    public void SetFlowBreak(object child, bool value)
    {
        if (child is not Control c) throw new ArgumentException("A Control is required.", nameof(child));
        _breaks[c] = value;
        Owner.PerformLayout(c, "FlowBreak");
    }
}

public abstract class LayoutSettings
{
    public abstract LayoutEngine LayoutEngine { get; }
}

/// <summary>
/// Children are placed one after another in the flow direction, each keeping its own size
/// and Margin, and wrap to a new row (or column) when they run out of room. Within a row a
/// child sits by its Anchor: Top (default) at the top, Bottom at the bottom, Top|Bottom or
/// Dock.Fill stretched, none centred.
/// </summary>
/// <remarks>An extender provider, as in WinForms: FlowBreak is a property it gives its children.</remarks>
[ProvideProperty("FlowBreak", typeof(Control))]
[DefaultProperty("FlowDirection")]
public class FlowLayoutPanel : Panel, IExtenderProvider
{
    bool IExtenderProvider.CanExtend(object obj) => obj is Control control && control.Parent == this;

    private readonly FlowLayoutSettings _settings;
    private FlowDirection _flowDirection = FlowDirection.LeftToRight;
    private bool _wrapContents = true;

    public FlowLayoutPanel()
    {
        _settings = new FlowLayoutSettings(this);
    }

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override LayoutEngine LayoutEngine => FlowLayout.Instance;

    public FlowLayoutSettings LayoutSettings => _settings;

    [Category("Layout")]
    [Description("Specifies the direction in which controls are laid out.")]
    [DefaultValue(FlowDirection.LeftToRight)]
    [Localizable(true)]
    public FlowDirection FlowDirection
    {
        get => _flowDirection;
        set
        {
            if (_flowDirection == value) return;
            _flowDirection = value;
            PerformLayout(this, nameof(FlowDirection));
        }
    }

    [Category("Layout")]
    [Description("Indicates whether contents are wrapped or clipped at the control boundary.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool WrapContents
    {
        get => _wrapContents;
        set
        {
            if (_wrapContents == value) return;
            _wrapContents = value;
            PerformLayout(this, nameof(WrapContents));
        }
    }

    [DefaultValue(false)]
    public bool GetFlowBreak(Control control) => _settings.GetFlowBreak(control);

    public void SetFlowBreak(Control control, bool value) => _settings.SetFlowBreak(control, value);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var size = FlowLayout.Measure(this, proposedSize.Width > 0 && proposedSize.Width < int.MaxValue ? proposedSize.Width : (AutoSize && !WrapContents ? int.MaxValue : DisplayRectangle.Width + Padding.Horizontal), apply: false);
        return new Size(size.Width + Padding.Horizontal, size.Height + Padding.Vertical);
    }
}

/// <summary>The flow engine. Rows are built from the visible children in collection order.</summary>
internal sealed class FlowLayout : LayoutEngine
{
    public static readonly FlowLayout Instance = new();

    private FlowLayout() { }

    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var panel = (FlowLayoutPanel)container;
        Measure(panel, panel.Width, apply: true);
        return false;
    }

    /// <summary>Lay the children out (when <paramref name="apply"/>) and return the content extent (without Padding).</summary>
    internal static Size Measure(FlowLayoutPanel panel, int availableWidth, bool apply)
    {
        var display = panel.DisplayRectangle;
        var pad = panel.Padding;
        bool horizontal = panel.FlowDirection is FlowDirection.LeftToRight or FlowDirection.RightToLeft;
        int limit = horizontal ? Math.Max(0, availableWidth - pad.Horizontal) : Math.Max(0, display.Height);
        bool wrap = panel.WrapContents;

        var rows = new List<List<Control>>();
        var rowExtents = new List<int>();
        var current = new List<Control>();
        int along = 0, cross = 0;
        // WinForms measures the row a flow break ends one control too far: the control after the break
        // counts towards that row's height too (checked against WinForms, CompatScenarios flow/break*).
        int brokenRow = -1;

        foreach (var child in panel.Controls)
        {
            if (!child.VisibleOwn) continue;
            var m = child.Margin;
            int size = horizontal ? child.Width + m.Horizontal : child.Height + m.Vertical;
            int crossSize = horizontal ? child.Height + m.Vertical : child.Width + m.Horizontal;
            if (brokenRow >= 0)
            {
                rowExtents[brokenRow] = Math.Max(rowExtents[brokenRow], crossSize);
                brokenRow = -1;
            }
            if (wrap && current.Count > 0 && along + size > limit)
            {
                rows.Add(current);
                rowExtents.Add(cross);
                current = new List<Control>();
                along = 0;
                cross = 0;
            }
            current.Add(child);
            along += size;
            cross = Math.Max(cross, crossSize);
            if (panel.GetFlowBreak(child))
            {
                rows.Add(current);
                rowExtents.Add(cross);
                brokenRow = rows.Count - 1;
                current = new List<Control>();
                along = 0;
                cross = 0;
            }
        }
        if (current.Count > 0)
        {
            rows.Add(current);
            rowExtents.Add(cross);
        }

        int crossPos = 0;
        int maxAlong = 0;
        for (int r = 0; r < rows.Count; r++)
        {
            int extent = rowExtents[r];
            int alongPos = 0;
            foreach (var child in rows[r])
            {
                var m = child.Margin;
                if (apply)
                {
                    Rectangle bounds;
                    if (horizontal)
                    {
                        int x = panel.FlowDirection == FlowDirection.LeftToRight
                            ? display.X + alongPos + m.Left
                            : display.Right - alongPos - m.Right - child.Width;
                        var (y, h) = CrossPlacement(child, display.Y + crossPos, extent, m.Top, m.Bottom, child.Height, vertical: true);
                        bounds = new Rectangle(x, y, child.Width, h);
                    }
                    else
                    {
                        int y = panel.FlowDirection == FlowDirection.TopDown
                            ? display.Y + alongPos + m.Top
                            : display.Bottom - alongPos - m.Bottom - child.Height;
                        var (x, w) = CrossPlacement(child, display.X + crossPos, extent, m.Left, m.Right, child.Width, vertical: false);
                        bounds = new Rectangle(x, y, w, child.Height);
                    }
                    child.SetBoundsFromLayout(bounds);
                }
                alongPos += horizontal ? child.Width + m.Horizontal : child.Height + m.Vertical;
            }
            maxAlong = Math.Max(maxAlong, alongPos);
            crossPos += extent;
        }

        return horizontal ? new Size(maxAlong, crossPos) : new Size(crossPos, maxAlong);
    }

    /// <summary>Position and size across the flow: anchored to the near edge, far edge, both (stretch) or neither (centre).</summary>
    private static (int pos, int size) CrossPlacement(Control child, int rowStart, int rowExtent, int marginNear, int marginFar, int size, bool vertical)
    {
        var anchor = child.Anchor;
        bool near = vertical ? (anchor & AnchorStyles.Top) != 0 : (anchor & AnchorStyles.Left) != 0;
        bool far = vertical ? (anchor & AnchorStyles.Bottom) != 0 : (anchor & AnchorStyles.Right) != 0;
        if (child.Dock == DockStyle.Fill || (vertical && child.Dock is DockStyle.Left or DockStyle.Right) || (!vertical && child.Dock is DockStyle.Top or DockStyle.Bottom))
        {
            near = far = true;
        }
        int available = rowExtent - marginNear - marginFar;
        if (near && far) return (rowStart + marginNear, Math.Max(0, available));
        if (far) return (rowStart + rowExtent - marginFar - size, size);
        if (near) return (rowStart + marginNear, size);
        return (rowStart + marginNear + Math.Max(0, (available - size) / 2), size);
    }
}
