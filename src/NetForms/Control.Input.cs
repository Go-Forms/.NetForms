using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

public partial class Control
{
    private bool _mousePressed;

    // --- mouse ---------------------------------------------------------------------

    /// <summary>True while this control receives all mouse input regardless of the pointer position.</summary>
    [Category("Focus")]
    [Description("Determines if this control is currently capturing all mouse input.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Capture
    {
        get => TopLevelForm?.CaptureControl == this;
        set
        {
            var form = TopLevelForm;
            if (form == null) return;
            if (value) form.SetCapture(this);
            else if (form.CaptureControl == this) form.SetCapture(null);
        }
    }

    public static MouseButtons MouseButtons => Form.PressedButtons;

    public static Point MousePosition => Form.LastMouseScreenPosition;

    /// <summary>
    /// Deepest visible, enabled descendant under <paramref name="p"/>, or this control.
    /// <paramref name="p"/> is in this control's own bounds coordinates; children live in the
    /// client area, so the point is shifted by <see cref="ClientOrigin"/> before they are tried.
    /// </summary>
    internal Control HitTest(Point p)
    {
        if ((_controls != null || _adornments != null) && !IsOverlayPoint(p))
        {
            var origin = ClientOrigin;
            var inClient = new Point(p.X - origin.X, p.Y - origin.Y);
            // Adornments float above the children; the last added is on top.
            if (_adornments != null)
            {
                for (int i = _adornments.Count - 1; i >= 0; i--)
                {
                    var a = _adornments[i];
                    if (!a._visible || !a._enabled || !a.Bounds.Contains(inClient)) continue;
                    var local = new Point(inClient.X - a._x, inClient.Y - a._y);
                    if (a.AdornmentContains(local)) return a.HitTest(local);
                }
            }
            if (_controls == null) return this;
            // Index 0 is the front-most child, so the first hit wins.
            for (int i = 0; i < _controls.Count; i++)
            {
                var child = _controls[i];
                if (!child._visible || !child._enabled) continue;
                if (child.Bounds.Contains(inClient))
                {
                    return child.HitTest(new Point(inClient.X - child._x, inClient.Y - child._y));
                }
            }
        }
        return this;
    }

    internal void RaiseMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && GetStyle(ControlStyles.StandardClick))
        {
            _mousePressed = true;
        }
        Capture = true;
        OnMouseDown(e);
    }

    internal void RaiseMouseUp(MouseEventArgs e)
    {
        bool wasPressed = _mousePressed;
        _mousePressed = false;
        if (Capture) Capture = false;

        if (e.Button == MouseButtons.Left && wasPressed && GetStyle(ControlStyles.StandardClick) && ClientRectangle.Contains(e.Location))
        {
            // WinForms order: MouseDown, Click, MouseClick, MouseUp.
            if (e.Clicks >= 2 && GetStyle(ControlStyles.StandardDoubleClick))
            {
                OnDoubleClick(EventArgs.Empty);
                OnMouseDoubleClick(e);
            }
            else
            {
                OnClick(EventArgs.Empty);
                OnMouseClick(e);
            }
        }
        OnMouseUp(e);

        // WM_CONTEXTMENU comes after the right button is released.
        if (e.Button == MouseButtons.Right && ClientRectangle.Contains(e.Location)) ShowContextMenuStrip(e.Location);
    }

    internal void RaiseMouseMove(MouseEventArgs e) => OnMouseMove(e);

    internal void RaiseMouseWheel(MouseEventArgs e) => OnMouseWheel(e);

    internal void RaiseMouseEnter() => OnMouseEnter(EventArgs.Empty);

    internal void RaiseMouseLeave() => OnMouseLeave(EventArgs.Empty);

    internal void RaiseCaptureChanged() => OnMouseCaptureChanged(EventArgs.Empty);

    // --- focus ---------------------------------------------------------------------

    [Description("Determines if this control has focus.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Focused
    {
        get
        {
            var form = TopLevelForm;
            return form != null && form.FocusedControl == this && form.IsWindowActive;
        }
    }

    [Description("Determines if this control or one if its children currently has the focus.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ContainsFocus
    {
        get
        {
            var form = TopLevelForm;
            if (form == null || !form.IsWindowActive) return false;
            var f = form.FocusedControl;
            return f != null && (f == this || Contains(f));
        }
    }

    public bool Focus()
    {
        if (!CanFocus) return false;
        var form = TopLevelForm;
        if (form == null) return false;
        form.SetFocusedControl(this, nativeFirst: true);
        return Focused;
    }

    /// <summary>Activates the control: gives it focus if it can take it.</summary>
    public void Select()
    {
        var form = TopLevelForm;
        if (form == null) return;
        Select(false, false);
    }

    protected virtual void Select(bool directed, bool forward)
    {
        var form = TopLevelForm;
        if (form == null) return;
        if (CanSelect)
        {
            form.SetFocusedControl(this);
        }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    protected internal virtual bool ShowFocusCues => TopLevelForm?.KeyboardFocusCuesShown ?? false;

    protected bool ShowKeyboardCues => true;

    /// <summary>Walk the tab order (TabIndex among siblings, depth-first) starting after <paramref name="ctl"/>.</summary>
    public Control? GetNextControl(Control? ctl, bool forward)
    {
        var order = new List<Control>();
        CollectTabOrder(this, order);
        if (order.Count == 0) return null;

        int start = ctl == null ? -1 : order.IndexOf(ctl);
        if (forward)
        {
            return start + 1 < order.Count ? order[start + 1] : null;
        }
        if (start == -1) return order[^1];
        return start > 0 ? order[start - 1] : null;
    }

    private static void CollectTabOrder(Control parent, List<Control> into)
    {
        if (parent._controls == null) return;
        var sorted = new List<Control>(parent._controls);
        // Stable sort by TabIndex; ties keep z-order (creation order in designer code).
        for (int i = 1; i < sorted.Count; i++)
        {
            var c = sorted[i];
            int j = i - 1;
            while (j >= 0 && sorted[j].TabIndex > c.TabIndex)
            {
                sorted[j + 1] = sorted[j];
                j--;
            }
            sorted[j + 1] = c;
        }
        foreach (var c in sorted)
        {
            into.Add(c);
            CollectTabOrder(c, into);
        }
    }

    public bool SelectNextControl(Control? ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
    {
        if (ctl != null && !Contains(ctl)) ctl = null;

        var order = new List<Control>();
        CollectTabOrder(this, order);
        if (order.Count == 0) return false;

        int start = ctl == null ? (forward ? -1 : order.Count) : order.IndexOf(ctl);
        int step = forward ? 1 : -1;
        int i = start;
        for (int n = 0; n < order.Count; n++)
        {
            i += step;
            if (i < 0 || i >= order.Count)
            {
                if (!wrap) return false;
                i = forward ? 0 : order.Count - 1;
            }
            var candidate = order[i];
            if (candidate == ctl) break;
            if (!nested && candidate._parent != this) continue;
            if (tabStopOnly && !candidate._tabStop) continue;
            if (!candidate.CanSelect) continue;
            candidate.Select(true, forward);
            return true;
        }
        return false;
    }

    // --- keyboard ------------------------------------------------------------------

    /// <summary>Handles navigation keys (Tab, arrows, Enter, Esc) before they reach <see cref="OnKeyDown"/>. Returns true if consumed.</summary>
    protected virtual bool ProcessDialogKey(Keys keyData) => _parent?.ProcessDialogKey(keyData) ?? false;

    protected virtual bool ProcessCmdKey(ref Message msg, Keys keyData) => _parent?.ProcessCmdKey(ref msg, keyData) ?? false;

    protected virtual bool IsInputKey(Keys keyData) => false;

    protected virtual bool ProcessKeyEventArgs(ref Message m)
    {
        switch (m.Msg)
        {
            case Message.WM_KEYDOWN:
                {
                    var e = new KeyEventArgs((Keys)m.WParam | ModifierKeys);
                    OnKeyDown(e);
                    return e.Handled;
                }
            case Message.WM_KEYUP:
                {
                    var e = new KeyEventArgs((Keys)m.WParam | ModifierKeys);
                    OnKeyUp(e);
                    return e.Handled;
                }
            case Message.WM_CHAR:
                {
                    var e = new KeyPressEventArgs((char)m.WParam);
                    OnKeyPress(e);
                    return e.Handled;
                }
        }
        return false;
    }

    public static Keys ModifierKeys => Form.CurrentModifiers;

    internal bool ProcessDialogKeyInternal(Keys keyData) => !IsInputKey(keyData) && ProcessDialogKey(keyData);

    internal bool RaiseKeyDown(Keys keyData)
    {
        // As WM_KEYDOWN reaches a WinForms control: the parents' ProcessKeyPreview first (a grid hears the keys of
        // its editing control there), then the control's own ProcessKeyEventArgs.
        var m = new Message { Msg = Message.WM_KEYDOWN, WParam = (int)(keyData & Keys.KeyCode) };
        return ProcessKeyMessage(ref m);
    }

    internal void RaiseEnter() => OnEnter(EventArgs.Empty);
    internal void RaiseLeave() => OnLeave(EventArgs.Empty);
    internal void RaiseGotFocus() => OnGotFocus(EventArgs.Empty);
    internal void RaiseValidating(System.ComponentModel.CancelEventArgs e) => OnValidating(e);
    internal void RaiseValidated() => OnValidated(EventArgs.Empty);
    internal void RaiseLostFocus() => OnLostFocus(EventArgs.Empty);

    internal bool RaiseKeyUp(Keys keyData)
    {
        var m = new Message { Msg = Message.WM_KEYUP, WParam = (int)(keyData & Keys.KeyCode) };
        return ProcessKeyMessage(ref m);
    }

    internal bool RaiseKeyPress(char ch)
    {
        var m = new Message { Msg = Message.WM_CHAR, WParam = ch };
        return ProcessKeyMessage(ref m);
    }
}

/// <summary>A minimal Win32-style message, kept so the ProcessCmdKey/ProcessKeyEventArgs signatures match WinForms.</summary>
public struct Message : IEquatable<Message>
{
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_CHAR = 0x0102;

    public IntPtr HWnd { get; set; }
    public int Msg { get; set; }
    public IntPtr WParam { get; set; }
    public IntPtr LParam { get; set; }
    public IntPtr Result { get; set; }

    public static Message Create(IntPtr hWnd, int msg, IntPtr wparam, IntPtr lparam) =>
        new Message { HWnd = hWnd, Msg = msg, WParam = wparam, LParam = lparam };

    public readonly bool Equals(Message other) =>
        HWnd == other.HWnd && Msg == other.Msg && WParam == other.WParam && LParam == other.LParam && Result == other.Result;

    public override readonly bool Equals(object? o) => o is Message other && Equals(other);

    public override readonly int GetHashCode() => HashCode.Combine(HWnd, Msg);

    /// <summary>The structure LParam points to (a Win32 message carries one; NetForms messages rarely do).</summary>
    public readonly object? GetLParam(Type cls) => System.Runtime.InteropServices.Marshal.PtrToStructure(LParam, cls);

    public override readonly string ToString() =>
        $"msg=0x{Msg:x} hwnd=0x{(long)HWnd:x} wparam=0x{(long)WParam:x} lparam=0x{(long)LParam:x} result=0x{(long)Result:x}";

    public static bool operator ==(Message a, Message b) => a.Equals(b);

    public static bool operator !=(Message a, Message b) => !a.Equals(b);
}
