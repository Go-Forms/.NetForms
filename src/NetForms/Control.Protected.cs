using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Protected members of WinForms' Control that derived controls call: raising events by key, right-to-left
/// translation of alignments, invalidation notice and the default IME mode.
/// </summary>
public partial class Control
{
    /// <summary>The IME mode a control starts with; <see cref="ImeMode.Inherit"/> unless a control says otherwise.</summary>
    protected virtual ImeMode DefaultImeMode => ImeMode.Inherit;

    /// <summary>False: NetForms does not mirror a right-to-left window's coordinates (WS_EX_LAYOUTRTL).</summary>
    [Category("Layout")]
    [Description("Indicates whether the control is mirrored.")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsMirrored => false;

    /// <summary>True when this control or one of its parents is sited in a designer.</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsAncestorSiteInDesignMode => DesignMode || (_parent?.IsAncestorSiteInDesignMode ?? false);

    /// <summary>Raises <see cref="Invalidated"/> for <paramref name="invalidatedArea"/>, as the control does when it is invalidated.</summary>
    protected virtual void NotifyInvalidate(Rectangle invalidatedArea) => OnInvalidated(new InvalidateEventArgs(invalidatedArea));

    /// <summary>Raises the Paint event (whatever the key: WinForms uses the Paint event's own).</summary>
    protected void RaisePaintEvent(object key, PaintEventArgs e) => Paint?.Invoke(this, e);

    /// <summary>Raises the key event stored in <see cref="Component.Events"/> under <paramref name="key"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void RaiseKeyEvent(object key, KeyEventArgs e) => ((KeyEventHandler?)Events[key])?.Invoke(this, e);

    /// <summary>Raises the mouse event stored in <see cref="Component.Events"/> under <paramref name="key"/>.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void RaiseMouseEvent(object key, MouseEventArgs e) => ((MouseEventHandler?)Events[key])?.Invoke(this, e);

    /// <summary>Win32 re-arms mouse hover tracking with it; NetForms raises MouseHover on its own, so nothing is needed.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void ResetMouseEventArgs()
    {
    }

    private bool IsRtl => RightToLeft == RightToLeft.Yes;

    protected HorizontalAlignment RtlTranslateAlignment(HorizontalAlignment align) => RtlTranslateHorizontal(align);

    protected LeftRightAlignment RtlTranslateAlignment(LeftRightAlignment align) => RtlTranslateLeftRight(align);

    protected ContentAlignment RtlTranslateAlignment(ContentAlignment align) => RtlTranslateContent(align);

    /// <summary>Left becomes right and right left when the control is right to left.</summary>
    protected HorizontalAlignment RtlTranslateHorizontal(HorizontalAlignment align) => !IsRtl ? align : align switch
    {
        HorizontalAlignment.Left => HorizontalAlignment.Right,
        HorizontalAlignment.Right => HorizontalAlignment.Left,
        _ => align,
    };

    protected LeftRightAlignment RtlTranslateLeftRight(LeftRightAlignment align) => !IsRtl ? align : align switch
    {
        LeftRightAlignment.Left => LeftRightAlignment.Right,
        _ => LeftRightAlignment.Left,
    };

    protected internal ContentAlignment RtlTranslateContent(ContentAlignment align) => !IsRtl ? align : align switch
    {
        ContentAlignment.TopLeft => ContentAlignment.TopRight,
        ContentAlignment.TopRight => ContentAlignment.TopLeft,
        ContentAlignment.MiddleLeft => ContentAlignment.MiddleRight,
        ContentAlignment.MiddleRight => ContentAlignment.MiddleLeft,
        ContentAlignment.BottomLeft => ContentAlignment.BottomRight,
        ContentAlignment.BottomRight => ContentAlignment.BottomLeft,
        _ => align,
    };
}
