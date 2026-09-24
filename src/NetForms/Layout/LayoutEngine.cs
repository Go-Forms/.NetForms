using System;
using System.Drawing;

namespace System.Windows.Forms.Layout;

/// <summary>Base class for the engines that arrange a container's children (WinForms API).</summary>
public abstract class LayoutEngine
{
    /// <summary>Called when a child's bounds were set by user code, so the engine can refresh cached data (anchor offsets).</summary>
    public virtual void InitLayout(object child, BoundsSpecified specified) { }

    /// <summary>Arrange the children of <paramref name="container"/>. Returns true if the container's own size should change.</summary>
    public virtual bool Layout(object container, LayoutEventArgs layoutEventArgs) => false;
}

/// <summary>
/// The engine every plain container uses, ported from dotnet/winforms DefaultLayout
/// (MIT): docked children carve up the display rectangle first, from the bottom of the
/// z-order up; anchored children are then placed from offsets captured against the
/// display rectangle when their bounds were last set by user code.
/// </summary>
internal sealed class DefaultLayout : LayoutEngine
{
    public static readonly DefaultLayout Instance = new();

    private DefaultLayout() { }

    /// <summary>Offsets of a control's edges from its parent's display rectangle, GDI-style (see <see cref="UpdateAnchorInfo"/>).</summary>
    internal sealed class AnchorInfo
    {
        public int Left, Top, Right, Bottom;
    }

    public override void InitLayout(object child, BoundsSpecified specified)
    {
        if (child is Control c && specified != BoundsSpecified.None && NeedsAnchorLayout(c))
        {
            UpdateAnchorInfo(c);
        }
    }

    public override bool Layout(object container, LayoutEventArgs layoutEventArgs)
    {
        var parent = (Control)container;
        var displayRect = parent.DisplayRectangle;
        LayoutDockedChildren(parent, displayRect);
        LayoutAnchoredChildren(parent, displayRect);
        return false;
    }

    private static bool NeedsDockLayout(Control c) => c.Dock != DockStyle.None && c.ParticipatesInLayout;

    private static bool NeedsAnchorLayout(Control c) =>
        c.Dock == DockStyle.None && c.ParticipatesInLayout && c.Anchor != (AnchorStyles.Top | AnchorStyles.Left);

    private static void LayoutDockedChildren(Control parent, Rectangle displayRectangle)
    {
        var remainingBounds = displayRectangle;
        var children = parent.Controls;

        // Docking layout is order dependent. WinForms uses z-order as the docking
        // order: the control at the bottom (last in the collection) docks first.
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var element = children[i];
            if (!NeedsDockLayout(element)) continue;

            switch (element.Dock)
            {
                case DockStyle.Top:
                    {
                        int h = DockedSize(element, new Size(remainingBounds.Width, 1)).Height;
                        SetDockedBounds(element, new Rectangle(remainingBounds.X, remainingBounds.Y, remainingBounds.Width, h));
                        remainingBounds.Y += element.Height;
                        remainingBounds.Height -= element.Height;
                        break;
                    }
                case DockStyle.Bottom:
                    {
                        int h = DockedSize(element, new Size(remainingBounds.Width, 1)).Height;
                        SetDockedBounds(element, new Rectangle(remainingBounds.X, remainingBounds.Bottom - h, remainingBounds.Width, h));
                        remainingBounds.Height -= element.Height;
                        break;
                    }
                case DockStyle.Left:
                    {
                        int w = DockedSize(element, new Size(1, remainingBounds.Height)).Width;
                        SetDockedBounds(element, new Rectangle(remainingBounds.X, remainingBounds.Y, w, remainingBounds.Height));
                        remainingBounds.X += element.Width;
                        remainingBounds.Width -= element.Width;
                        break;
                    }
                case DockStyle.Right:
                    {
                        int w = DockedSize(element, new Size(1, remainingBounds.Height)).Width;
                        SetDockedBounds(element, new Rectangle(remainingBounds.Right - w, remainingBounds.Y, w, remainingBounds.Height));
                        remainingBounds.Width -= element.Width;
                        break;
                    }
                case DockStyle.Fill:
                    // Every Fill child gets whatever is left; WinForms does not reserve it for the first one.
                    SetDockedBounds(element, remainingBounds);
                    break;
            }
        }
    }

    private static void SetDockedBounds(Control element, Rectangle bounds)
    {
        // A dock wider than the client leaves a negative remainder; a control cannot be
        // negative-sized, so it collapses to zero (Win32 clamps the same way).
        bounds.Width = Math.Max(0, bounds.Width);
        bounds.Height = Math.Max(0, bounds.Height);
        element.SetBoundsFromLayout(bounds);
    }

    private static void LayoutAnchoredChildren(Control parent, Rectangle displayRectangle)
    {
        foreach (var element in parent.Controls)
        {
            if (!NeedsAnchorLayout(element)) continue;
            element.SetBoundsFromLayout(GetAnchorDestination(element, displayRectangle));
        }
    }

    private static bool IsAnchored(AnchorStyles anchor, AnchorStyles desiredAnchor) => (anchor & desiredAnchor) == desiredAnchor;

    /// <summary>
    /// The size the children need, measured from the origin of the display rectangle: DefaultLayout's
    /// <c>TryCalculatePreferredSize(measureOnly: true)</c>, the union of what the docked children need
    /// (<see cref="MeasureDockedChildren"/>) and what the anchored ones need (<see cref="MeasureAnchoredChildren"/>).
    /// The caller adds the container's own insets (padding, border).
    /// </summary>
    internal static Size GetPreferredContentSize(Control container)
    {
        var display = container.DisplayRectangle;
        var docked = MeasureDockedChildren(container);
        var anchored = MeasureAnchoredChildren(container);
        // WinForms subtracts Padding.Left/Top here; our display rectangle starts exactly there
        // (and also after a border that lives inside the client area, see Panel).
        anchored.Width -= display.X;
        anchored.Height -= display.Y;
        return new Size(Math.Max(docked.Width, anchored.Width), Math.Max(docked.Height, anchored.Height));
    }

    /// <summary>
    /// <c>LayoutDockedControls(measureOnly: true)</c>: start from an empty rectangle and grow it by what
    /// each docked child needs. Top/Bottom children add only height and Left/Right only width; a Fill
    /// child adds its preferred size only if it is AutoSize itself. Otherwise it just takes what is left,
    /// so it never makes its container bigger (and its Margin never counts).
    /// </summary>
    private static Size MeasureDockedChildren(Control container)
    {
        var remaining = Rectangle.Empty;
        var preferred = Size.Empty;
        var children = container.Controls;
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var element = children[i];
            if (!NeedsDockLayout(element)) continue;
            switch (element.Dock)
            {
                case DockStyle.Top:
                case DockStyle.Bottom:
                    {
                        var size = DockedSize(element, new Size(remaining.Width, 1));
                        var needed = new Size(0, Math.Max(0, size.Height - remaining.Height));
                        preferred += needed;
                        remaining.Size += needed;
                        if (element.Dock == DockStyle.Top) remaining.Y += element.Height;
                        remaining.Height -= element.Height;
                        break;
                    }
                case DockStyle.Left:
                case DockStyle.Right:
                    {
                        var size = DockedSize(element, new Size(1, remaining.Height));
                        var needed = new Size(Math.Max(0, size.Width - remaining.Width), 0);
                        preferred += needed;
                        remaining.Size += needed;
                        if (element.Dock == DockStyle.Left) remaining.X += element.Width;
                        remaining.Width -= element.Width;
                        break;
                    }
                case DockStyle.Fill:
                    if (element is MdiClient) break;
                    if (element.LayoutAutoSize)
                    {
                        var pref = element.GetPreferredSize(Size.Empty);
                        remaining.Size += pref;
                        preferred += pref;
                    }
                    break;
            }
        }
        return preferred;
    }

    /// <summary>DefaultLayout's <c>xGetDockedSize</c>: an AutoSize child asks for its preferred size, the others keep theirs.</summary>
    private static Size DockedSize(Control element, Size constraints) =>
        element.LayoutAutoSize ? element.GetPreferredSize(constraints) : element.Size;

    /// <summary>
    /// <c>GetAnchorPreferredSize</c> (anchor layout V1): a child anchored Left (and not Right) must not be
    /// clipped, so its right edge plus margin counts; a Right-anchored child keeps its distance to the far
    /// edge. The same for Top/Bottom. In client coordinates, like WinForms.
    /// </summary>
    private static Size MeasureAnchoredChildren(Control container)
    {
        var pref = Size.Empty;
        var children = container.Controls;
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var element = children[i];
            if (NeedsDockLayout(element) || !element.ParticipatesInLayout) continue;
            var anchor = element.Anchor;
            var margin = element.Margin;
            var b = element.Bounds;
            var space = Rectangle.FromLTRB(b.Left - margin.Left, b.Top - margin.Top, b.Right + margin.Right, b.Bottom + margin.Bottom);

            if (IsAnchored(anchor, AnchorStyles.Left) && !IsAnchored(anchor, AnchorStyles.Right))
                pref.Width = Math.Max(pref.Width, space.Right);
            if (!IsAnchored(anchor, AnchorStyles.Bottom))
                pref.Height = Math.Max(pref.Height, space.Bottom);

            if (IsAnchored(anchor, AnchorStyles.Right) || IsAnchored(anchor, AnchorStyles.Bottom))
            {
                var dest = MeasureAnchorDestination(element);
                if (IsAnchored(anchor, AnchorStyles.Right))
                    pref.Width = dest.Width < 0 ? Math.Max(pref.Width, space.Right + dest.Width) : Math.Max(pref.Width, dest.Right);
                if (IsAnchored(anchor, AnchorStyles.Bottom))
                    pref.Height = dest.Height < 0 ? Math.Max(pref.Height, space.Bottom + dest.Height) : Math.Max(pref.Height, dest.Bottom);
            }
        }
        return pref;
    }

    /// <summary>
    /// <c>ComputeAnchoredBounds(element, Rectangle.Empty, measureOnly: true)</c>: the anchor offsets laid
    /// out against an empty display rectangle, with negative (far-edge) offsets folded back onto the
    /// positive plane. Cached bounds equal the current bounds while measuring, which simplifies the original.
    /// </summary>
    private static Rectangle MeasureAnchorDestination(Control element)
    {
        if (element.AnchorInfo == null) UpdateAnchorInfo(element);
        var layout = element.AnchorInfo;
        if (layout == null) return element.Bounds;
        var bounds = element.Bounds;
        int left = layout.Left, top = layout.Top, right = layout.Right, bottom = layout.Bottom;

        if (right < left)
        {
            right = left + bounds.Width + Math.Abs(right);
        }
        else
        {
            left = left > 0 ? left : bounds.Left;
            right = right > 0 ? right : bounds.Right + Math.Abs(right);
        }

        if (bottom < top)
        {
            bottom = top + bounds.Height + Math.Abs(bottom);
        }
        else
        {
            top = top > 0 ? top : bounds.Top;
            bottom = bottom > 0 ? bottom : bounds.Bottom + Math.Abs(bottom);
        }
        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>Where an anchored control belongs for the current display rectangle.</summary>
    private static Rectangle GetAnchorDestination(Control element, Rectangle displayRect)
    {
        var layout = element.AnchorInfo;
        if (layout == null)
        {
            UpdateAnchorInfo(element);
            layout = element.AnchorInfo!;
        }

        int left = layout.Left + displayRect.X;
        int top = layout.Top + displayRect.Y;
        int right = layout.Right + displayRect.X;
        int bottom = layout.Bottom + displayRect.Y;

        var anchor = element.Anchor;

        if (IsAnchored(anchor, AnchorStyles.Right))
        {
            right += displayRect.Width;
            if (!IsAnchored(anchor, AnchorStyles.Left))
            {
                left += displayRect.Width;
            }
        }
        else if (!IsAnchored(anchor, AnchorStyles.Left))
        {
            right += displayRect.Width / 2;
            left += displayRect.Width / 2;
        }

        if (IsAnchored(anchor, AnchorStyles.Bottom))
        {
            bottom += displayRect.Height;
            if (!IsAnchored(anchor, AnchorStyles.Top))
            {
                top += displayRect.Height;
            }
        }
        else if (!IsAnchored(anchor, AnchorStyles.Top))
        {
            bottom += displayRect.Height / 2;
            top += displayRect.Height / 2;
        }

        // A control anchored to both sides of a parent that shrank below its own size
        // would go negative; WinForms keeps the minimum size instead.
        if (right < left) right = left;
        if (bottom < top) bottom = top;

        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Capture the edges of <paramref name="element"/> relative to the parent's display
    /// rectangle. Edges anchored Right/Bottom are stored relative to the far edge, unanchored
    /// ones relative to the centre line - the integer arithmetic is GDI's, kept on purpose.
    /// </summary>
    internal static void UpdateAnchorInfo(Control element)
    {
        var parent = element.Parent;
        if (parent == null) return;

        var anchorInfo = element.AnchorInfo ?? (element.AnchorInfo = new AnchorInfo());
        var bounds = element.Bounds;
        var displayRect = parent.DisplayRectangle;
        var anchor = element.Anchor;

        anchorInfo.Left = bounds.Left - displayRect.X;
        anchorInfo.Top = bounds.Top - displayRect.Y;
        anchorInfo.Right = bounds.Right - displayRect.X;
        anchorInfo.Bottom = bounds.Bottom - displayRect.Y;

        if (IsAnchored(anchor, AnchorStyles.Right))
        {
            anchorInfo.Right -= displayRect.Width;
            if (!IsAnchored(anchor, AnchorStyles.Left))
            {
                anchorInfo.Left -= displayRect.Width;
            }
        }
        else if (!IsAnchored(anchor, AnchorStyles.Left))
        {
            anchorInfo.Right -= displayRect.Width / 2;
            anchorInfo.Left -= displayRect.Width / 2;
        }

        if (IsAnchored(anchor, AnchorStyles.Bottom))
        {
            anchorInfo.Bottom -= displayRect.Height;
            if (!IsAnchored(anchor, AnchorStyles.Top))
            {
                anchorInfo.Top -= displayRect.Height;
            }
        }
        else if (!IsAnchored(anchor, AnchorStyles.Top))
        {
            anchorInfo.Bottom -= displayRect.Height / 2;
            anchorInfo.Top -= displayRect.Height / 2;
        }
    }
}
