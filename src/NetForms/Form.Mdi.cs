using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The MDI half of <see cref="Form"/>. An MDI child is not a window: it is a control inside the
/// parent's <see cref="MdiClient"/> that draws its own caption and border and reports a client
/// area shifted by them, which is exactly the coordinate system WinForms gives an MDI child.
/// </summary>
public partial class Form
{
    /// <summary>
    /// Frame metrics of an MDI child, matching what real WinForms reports: a 300x200 child has a
    /// 284x161 client area, so the border is 8 pixels a side and the caption 23 (compat key
    /// "info/mdi/child-frame").
    /// </summary>
    internal const int MdiChildCaptionHeight = 23;

    internal const int MdiChildBorderWidth = 8;
    private const int CaptionButtonWidth = 18;
    private const int CaptionButtonHeight = 16;

    private enum MdiDrag { None, Move, SizeLeft, SizeRight, SizeTop, SizeBottom, SizeTopLeft, SizeTopRight, SizeBottomLeft, SizeBottomRight }

    private Form? _mdiParent;
    private MdiClient? _mdiClient;
    private Form? _activeMdiChild;
    private Rectangle _mdiRestoreBounds;
    private MdiDrag _dragMode;
    private Point _dragStart;
    private Rectangle _dragBounds;
    private int _captionButtonHot = -1;
    private int _captionButtonPressed = -1;

    [Category("Layout")]
    [Description("Occurs when an MDI Child window is activated.")]
    public event EventHandler? MdiChildActivate;

    protected virtual void OnMdiChildActivate(EventArgs e) => MdiChildActivate?.Invoke(this, e);

    /// <summary>Turns the form into an MDI parent: a sunken client area that holds the child forms.</summary>
    [Category("Window Style")]
    [Description("Determines whether the form is an MDI container.")]
    [DefaultValue(false)]
    public bool IsMdiContainer
    {
        get => _mdiClient != null;
        set
        {
            if (value == (_mdiClient != null)) return;
            if (value)
            {
                _mdiClient = new MdiClient();
                Controls.Add(_mdiClient);
                // The filling child has to be laid out last, and our layout engine docks from the
                // end of the collection backwards, so it belongs at the front of the z-order.
                Controls.SetChildIndex(_mdiClient, 0);
                PerformLayout();
            }
            else
            {
                var client = _mdiClient;
                _mdiClient = null;
                if (client != null)
                {
                    Controls.Remove(client);
                    client.Dispose();
                }
            }
        }
    }

    /// <summary>The control the children live in; null unless <see cref="IsMdiContainer"/> is set.</summary>
    public MdiClient? MdiClientArea => _mdiClient;

    [Category("Window Style")]
    [Description("Retrieves the MDI parent of this form.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Form? MdiParent
    {
        get => _mdiParent;
        set
        {
            if (ReferenceEquals(_mdiParent, value)) return;
            if (value != null && value._mdiClient == null)
                throw new ArgumentException("The form is not an MDI container; set IsMdiContainer first.", nameof(value));
            if (value == this) throw new ArgumentException("A form cannot be its own MDI parent.", nameof(value));

            var old = _mdiParent;
            old?._mdiClient?.Controls.Remove(this);
            if (old != null && ReferenceEquals(old._activeMdiChild, this)) old.ActivateMdiChild(null);

            _mdiParent = value;
            if (value != null)
            {
                // A child never owns a window; its bounds live in the parent's client area.
                if (_window != null)
                {
                    var window = _window;
                    _window = null;
                    _host = null;
                    Application.UnregisterForm(this);
                    window.Dispose();
                }
                if (_mdiRestoreBounds.IsEmpty) _mdiRestoreBounds = new Rectangle(Location, Size);
                value._mdiClient!.Controls.Add(this);
                Application.RegisterForm(this);
            }
        }
    }

    [Category("Window Style")]
    [Description("Determines whether the form is a child of a MDI container.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsMdiChild => _mdiParent != null;

    [Category("Window Style")]
    [Description("Retrieves the MDI children of this form.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Form[] MdiChildren
    {
        get
        {
            if (_mdiClient == null) return Array.Empty<Form>();
            var children = _mdiClient.Children;
            return children.ToArray();
        }
    }

    [Description("The currently active MDI child form.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Form? ActiveMdiChild => _activeMdiChild;

    /// <summary>Arranges the children (Cascade by default), as WinForms' LayoutMdi does.</summary>
    public void LayoutMdi(MdiLayout value)
    {
        _mdiClient?.LayoutMdi(value);
    }

    /// <summary>Brings <paramref name="child"/> to the front and makes it the active one.</summary>
    internal void ActivateMdiChild(Form? child)
    {
        if (ReferenceEquals(_activeMdiChild, child)) return;
        var previous = _activeMdiChild;
        _activeMdiChild = child;

        if (child != null && _mdiClient != null && _mdiClient.Controls.Contains(child))
        {
            _mdiClient.Controls.SetChildIndex(child, 0);
        }
        previous?.Invalidate();
        child?.Invalidate();
        _mdiClient?.Invalidate();

        UpdateMdiTitle();
        OnMdiChildActivate(EventArgs.Empty);
        if (previous != null) previous.OnDeactivate(EventArgs.Empty);
        if (child != null) child.OnActivated(EventArgs.Empty);
    }

    /// <summary>Walks up from <paramref name="control"/> to the MDI child that contains it, if any.</summary>
    internal static Form? MdiChildOf(Control? control)
    {
        for (Control? c = control; c != null; c = c.Parent)
        {
            if (c is Form { IsMdiChild: true } child) return child;
        }
        return null;
    }

    /// <summary>WinForms shows the maximized child's caption in the parent's title: "Parent - [Child]".</summary>
    private void UpdateMdiTitle()
    {
        if (_mdiClient == null || _window == null) return;
        var child = _activeMdiChild;
        string title = base.Text;
        if (child is { WindowState: FormWindowState.Maximized }) title += " - [" + child.Text + "]";
        _window.Title = title;
    }

    // --- the child's frame ---------------------------------------------------------------

    private bool MdiChildSizable => _borderStyle is FormBorderStyle.Sizable or FormBorderStyle.SizableToolWindow;

    private int MdiBorder => !IsMdiChild ? 0 : _borderStyle == FormBorderStyle.None ? 0 : MdiChildSizable ? MdiChildBorderWidth : 1;

    private int MdiCaption => !IsMdiChild || _borderStyle == FormBorderStyle.None ? 0 : MdiChildCaptionHeight;

    internal override Point ClientOrigin => IsMdiChild ? new Point(MdiBorder, MdiBorder + MdiCaption) : Point.Empty;

    internal override Size ClientSizeCore => IsMdiChild
        ? new Size(Math.Max(0, Width - 2 * MdiBorder), Math.Max(0, Height - 2 * MdiBorder - MdiCaption))
        : base.ClientSizeCore;

    internal override bool HasNonClientArea => IsMdiChild && _borderStyle != FormBorderStyle.None;

    private Rectangle MdiCaptionBounds => new Rectangle(MdiBorder, MdiBorder, Math.Max(0, Width - 2 * MdiBorder), MdiCaption);

    private Rectangle CaptionButtonBounds(int index)
    {
        var caption = MdiCaptionBounds;
        int right = caption.Right - 2 - index * (CaptionButtonWidth + 1);
        return new Rectangle(right - CaptionButtonWidth, caption.Y + (caption.Height - CaptionButtonHeight) / 2, CaptionButtonWidth, CaptionButtonHeight);
    }

    /// <summary>Close is button 0, maximise/restore 1, minimise 2 - counted from the right.</summary>
    private int CaptionButtonAt(Point boundsPoint)
    {
        if (!ControlBox) return -1;
        for (int i = 0; i < 3; i++)
        {
            if (i == 1 && !_maximizeBox) continue;
            if (i == 2 && !_minimizeBox) continue;
            if (CaptionButtonBounds(i).Contains(boundsPoint)) return i;
        }
        return -1;
    }

    internal override void OnPaintNonClient(Graphics g)
    {
        bool active = _mdiParent?.ActiveMdiChild == this;
        var bounds = new Rectangle(0, 0, Width, Height);

        using (var frame = new SolidBrush(active ? Theme.WindowBorderFocused : Theme.ButtonBorder))
        {
            // The border is drawn as four bands so the client area is never painted over.
            int b = MdiBorder;
            g.FillRectangle(frame, new Rectangle(0, 0, bounds.Width, b + MdiCaption));
            g.FillRectangle(frame, new Rectangle(0, bounds.Height - b, bounds.Width, b));
            g.FillRectangle(frame, new Rectangle(0, 0, b, bounds.Height));
            g.FillRectangle(frame, new Rectangle(bounds.Width - b, 0, b, bounds.Height));
        }

        var caption = MdiCaptionBounds;
        if (caption.Height <= 0) return;

        int textLeft = caption.X + 4;
        if (ShowIcon && _icon != null)
        {
            var iconRect = new Rectangle(caption.X + 3, caption.Y + (caption.Height - 16) / 2, 16, 16);
            using var bitmap = _icon.ToBitmap();
            g.DrawImage(bitmap, iconRect);
            textLeft = iconRect.Right + 4;
        }

        int buttonsLeft = ControlBox ? CaptionButtonBounds(2).Left - 4 : caption.Right - 4;
        var textRect = new Rectangle(textLeft, caption.Y, Math.Max(0, buttonsLeft - textLeft), caption.Height);
        TextRenderer.DrawText(g, Text, Font, textRect, active ? Theme.HighlightText : SystemColors.ControlText,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

        if (!ControlBox) return;
        for (int i = 0; i < 3; i++)
        {
            if (i == 1 && !_maximizeBox) continue;
            if (i == 2 && !_minimizeBox) continue;
            PaintCaptionButton(g, i, CaptionButtonBounds(i), active);
        }
    }

    private void PaintCaptionButton(Graphics g, int index, Rectangle bounds, bool active)
    {
        if (_captionButtonPressed == index || _captionButtonHot == index)
        {
            using var back = new SolidBrush(index == 0 ? Color.FromArgb(0xE8, 0x11, 0x23) : Theme.StripItemHot);
            g.FillRectangle(back, bounds);
        }

        var color = _captionButtonPressed == index || _captionButtonHot == index
            ? (index == 0 ? Color.White : SystemColors.ControlText)
            : active ? Theme.HighlightText : SystemColors.ControlText;
        using var pen = new Pen(color);
        int cx = bounds.X + bounds.Width / 2;
        int cy = bounds.Y + bounds.Height / 2;

        switch (index)
        {
            case 0:
                g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
                g.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
                break;
            case 1:
                if (_windowState == FormWindowState.Maximized)
                {
                    g.DrawRectangle(pen, cx - 4, cy - 2, 6, 6);
                    g.DrawLine(pen, cx - 2, cy - 4, cx + 4, cy - 4);
                    g.DrawLine(pen, cx + 4, cy - 4, cx + 4, cy + 2);
                }
                else
                {
                    g.DrawRectangle(pen, cx - 4, cy - 4, 8, 8);
                }
                break;
            default:
                g.DrawLine(pen, cx - 4, cy + 4, cx + 4, cy + 4);
                break;
        }
    }

    // --- the child's frame input ------------------------------------------------------------

    /// <summary>The mouse position in the child's own bounds coordinates (the client area is inset by the frame).</summary>
    private Point ToBounds(Point clientPoint)
    {
        var origin = ClientOrigin;
        return new Point(clientPoint.X + origin.X, clientPoint.Y + origin.Y);
    }

    private MdiDrag HitFrame(Point boundsPoint)
    {
        if (_windowState == FormWindowState.Maximized) return MdiDrag.None;
        int b = MdiBorder;
        if (b > 0 && MdiChildSizable)
        {
            bool left = boundsPoint.X < b, right = boundsPoint.X >= Width - b;
            bool top = boundsPoint.Y < b, bottom = boundsPoint.Y >= Height - b;
            if (top && left) return MdiDrag.SizeTopLeft;
            if (top && right) return MdiDrag.SizeTopRight;
            if (bottom && left) return MdiDrag.SizeBottomLeft;
            if (bottom && right) return MdiDrag.SizeBottomRight;
            if (left) return MdiDrag.SizeLeft;
            if (right) return MdiDrag.SizeRight;
            if (top) return MdiDrag.SizeTop;
            if (bottom) return MdiDrag.SizeBottom;
        }
        return MdiCaptionBounds.Contains(boundsPoint) ? MdiDrag.Move : MdiDrag.None;
    }

    private bool MdiChildMouseDown(MouseEventArgs e)
    {
        if (!IsMdiChild) return false;
        _mdiParent?.ActivateMdiChild(this);
        if (e.Button != MouseButtons.Left) return false;

        var point = ToBounds(e.Location);
        int button = CaptionButtonAt(point);
        if (button >= 0)
        {
            _captionButtonPressed = button;
            Capture = true;
            Invalidate();
            return true;
        }

        var mode = HitFrame(point);
        if (mode == MdiDrag.None) return false;
        if (mode == MdiDrag.Move && e.Clicks >= 2)
        {
            ToggleMdiMaximize();
            return true;
        }
        _dragMode = mode;
        _dragStart = PointToScreen(e.Location);
        _dragBounds = Bounds;
        Capture = true;
        return true;
    }

    private bool MdiChildMouseMove(MouseEventArgs e)
    {
        if (!IsMdiChild) return false;
        var point = ToBounds(e.Location);

        if (_dragMode != MdiDrag.None)
        {
            var now = PointToScreen(e.Location);
            int dx = now.X - _dragStart.X;
            int dy = now.Y - _dragStart.Y;
            Bounds = DraggedBounds(dx, dy);
            return true;
        }

        int hot = _captionButtonPressed >= 0 ? _captionButtonPressed : CaptionButtonAt(point);
        if (hot != _captionButtonHot)
        {
            _captionButtonHot = hot;
            Invalidate();
        }
        return false;
    }

    private Rectangle DraggedBounds(int dx, int dy)
    {
        var b = _dragBounds;
        const int min = 80;
        switch (_dragMode)
        {
            case MdiDrag.Move: return new Rectangle(b.X + dx, b.Y + dy, b.Width, b.Height);
            case MdiDrag.SizeLeft: return FromEdges(b.Left + dx, b.Top, b.Right, b.Bottom);
            case MdiDrag.SizeRight: return FromEdges(b.Left, b.Top, b.Right + dx, b.Bottom);
            case MdiDrag.SizeTop: return FromEdges(b.Left, b.Top + dy, b.Right, b.Bottom);
            case MdiDrag.SizeBottom: return FromEdges(b.Left, b.Top, b.Right, b.Bottom + dy);
            case MdiDrag.SizeTopLeft: return FromEdges(b.Left + dx, b.Top + dy, b.Right, b.Bottom);
            case MdiDrag.SizeTopRight: return FromEdges(b.Left, b.Top + dy, b.Right + dx, b.Bottom);
            case MdiDrag.SizeBottomLeft: return FromEdges(b.Left + dx, b.Top, b.Right, b.Bottom + dy);
            default: return FromEdges(b.Left, b.Top, b.Right + dx, b.Bottom + dy);
        }

        Rectangle FromEdges(int left, int top, int right, int bottom)
        {
            if (right - left < min) left = right - min;
            if (bottom - top < min) top = bottom - min;
            return new Rectangle(left, top, right - left, bottom - top);
        }
    }

    private bool MdiChildMouseUp(MouseEventArgs e)
    {
        if (!IsMdiChild) return false;
        if (_dragMode != MdiDrag.None)
        {
            _dragMode = MdiDrag.None;
            Capture = false;
            if (_windowState == FormWindowState.Normal) _mdiRestoreBounds = Bounds;
            return true;
        }

        int pressed = _captionButtonPressed;
        if (pressed < 0) return false;
        _captionButtonPressed = -1;
        Capture = false;
        Invalidate();

        if (CaptionButtonAt(ToBounds(e.Location)) != pressed) return true;
        switch (pressed)
        {
            case 0: Close(); break;
            case 1: ToggleMdiMaximize(); break;
            default: MinimizeMdiChild(); break;
        }
        return true;
    }

    private void ToggleMdiMaximize()
    {
        if (_windowState == FormWindowState.Maximized) RestoreMdiChild();
        else MaximizeMdiChild();
    }

    private void MaximizeMdiChild()
    {
        if (_mdiParent?._mdiClient == null) return;
        if (_windowState == FormWindowState.Normal) _mdiRestoreBounds = Bounds;
        _windowState = FormWindowState.Maximized;
        Bounds = _mdiParent._mdiClient.ClientRectangle;
        _mdiParent.UpdateMdiTitle();
        Invalidate();
    }

    private void MinimizeMdiChild()
    {
        if (_windowState == FormWindowState.Normal) _mdiRestoreBounds = Bounds;
        _windowState = FormWindowState.Minimized;
        // A minimised child shrinks to its caption, as it does in Windows.
        Bounds = new Rectangle(Bounds.X, Bounds.Y, 160, MdiChildCaptionHeight + 2 * MdiBorder);
        _mdiParent?.UpdateMdiTitle();
        Invalidate();
    }

    private void RestoreMdiChild()
    {
        _windowState = FormWindowState.Normal;
        if (!_mdiRestoreBounds.IsEmpty) Bounds = _mdiRestoreBounds;
        _mdiParent?.UpdateMdiTitle();
        Invalidate();
    }

    /// <summary>Used by <see cref="MdiClient.LayoutMdi"/>: a tiled child is always in its normal state.</summary>
    internal void RestoreForLayout()
    {
        if (_windowState != FormWindowState.Normal)
        {
            _windowState = FormWindowState.Normal;
            _mdiParent?.UpdateMdiTitle();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (MdiChildMouseDown(e)) return;
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (MdiChildMouseMove(e)) return;
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (MdiChildMouseUp(e)) return;
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (_captionButtonHot >= 0 && _captionButtonPressed < 0)
        {
            _captionButtonHot = -1;
            Invalidate();
        }
        base.OnMouseLeave(e);
    }

    /// <summary>An MDI child follows its parent's client area when it is maximised.</summary>
    internal void OnMdiClientResized()
    {
        if (_windowState == FormWindowState.Maximized && _mdiParent?._mdiClient != null)
        {
            Bounds = _mdiParent._mdiClient.ClientRectangle;
        }
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

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new Size ClientSize { get => base.ClientSize; set => base.ClientSize = value; }

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new Padding Margin { get => base.Margin; set => base.Margin = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Size Size { get => base.Size; set => base.Size = value; }

    [Category("Behavior")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new int TabIndex { get => base.TabIndex; set => base.TabIndex = value; }

    [Category("Behavior")]
    [DefaultValue(true)]
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

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? MarginChanged
    {
        add => base.MarginChanged += value;
        remove => base.MarginChanged -= value;
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

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override AutoValidate AutoValidate { get => base.AutoValidate; set => base.AutoValidate = value; }

    [Category("Property Changed")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? AutoValidateChanged
    {
        add => base.AutoValidateChanged += value;
        remove => base.AutoValidateChanged -= value;
    }
}
