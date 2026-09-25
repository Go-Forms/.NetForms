using System.Collections.Generic;
using System.Drawing;
using NetForms.Platform;

namespace System.Windows.Forms;

/// <summary>
/// Drag and drop, the OLE protocol WinForms is built on, run by NetForms itself (decision 155).
///
/// A drag started in the application (Control.DoDragDrop) is a modal loop: the mouse and the keyboard go to the
/// drag, not to the controls. On every move the source is asked QueryContinueDrag (Escape cancels, releasing the
/// button that started the drag drops), the control under the pointer that has AllowDrop - itself or the nearest
/// parent that does, as OLE walks up to a registered window - gets DragEnter/DragOver/DragLeave, and the source
/// gets GiveFeedback with the effect the target chose. On the drop the target gets DragDrop and DoDragDrop
/// returns the effect. Every form of the application is a target, whichever window the drag started in.
///
/// A drag coming from another application (files from the file manager, text from a browser) arrives from the
/// platform (IWindowHost.DragEnter/DragOver/DragLeave/Drop) and is delivered to the same events.
/// </summary>
internal static class DragDropManager
{
    private static Session? s_current;

    /// <summary>The drag in progress in this application, if any.</summary>
    internal static bool IsDragging => s_current != null;

    // OLE key-state bits (MK_* and the Alt bit), as DragEventArgs.KeyState and QueryContinueDragEventArgs.KeyState report them.
    private const int MK_LBUTTON = 0x01;
    private const int MK_RBUTTON = 0x02;
    private const int MK_SHIFT = 0x04;
    private const int MK_CONTROL = 0x08;
    private const int MK_MBUTTON = 0x10;
    private const int MK_ALT = 0x20;

    internal static int KeyState(MouseButtons buttons, Keys modifiers)
    {
        int state = 0;
        if ((buttons & MouseButtons.Left) != 0) state |= MK_LBUTTON;
        if ((buttons & MouseButtons.Right) != 0) state |= MK_RBUTTON;
        if ((buttons & MouseButtons.Middle) != 0) state |= MK_MBUTTON;
        if ((modifiers & Keys.Shift) != 0) state |= MK_SHIFT;
        if ((modifiers & Keys.Control) != 0) state |= MK_CONTROL;
        if ((modifiers & Keys.Alt) != 0) state |= MK_ALT;
        return state;
    }

    /// <summary>Control.DoDragDrop: run the drag and return the effect the drop target chose (None if cancelled).</summary>
    internal static DragDropEffects DoDragDrop(Control source, object data, DragDropEffects allowedEffects)
    {
        ArgumentNullException.ThrowIfNull(data);
        // One drag at a time, as OLE: DoDragDrop from inside a drag event returns at once.
        if (s_current != null) return DragDropEffects.None;

        var dataObject = data as IDataObject ?? new DataObject(data);
        var session = new Session(source, dataObject, allowedEffects, Form.PressedButtons);
        s_current = session;
        try
        {
            // OLE evaluates the position at once: the target under the pointer gets DragEnter before any move.
            session.Update(Control.MousePosition, Form.PressedButtons, Form.CurrentModifiers, escapePressed: false);
            if (!session.Done)
            {
                session.InLoop = true;
                Application.Platform.RunMessageLoop();
                session.InLoop = false;
            }
            // The loop ended without a drop (the application is closing, or no platform loop at all): cancel.
            if (!session.Done) session.Cancel();
        }
        finally
        {
            s_current = null;
            // OLE held the mouse: the source never sees the button come up, and loses the capture.
            source.TopLevelForm?.SetCapture(null);
            source.TopLevelForm?.UpdateCursor();
        }
        return session.Result;
    }

    // --- the input of an in-process drag -------------------------------------------------------------

    /// <summary>A mouse move reported by any form while a drag runs; true when the drag took it.</summary>
    internal static bool MouseMove(Point screenPoint, MouseButtons buttons, Keys modifiers)
    {
        var session = s_current;
        if (session == null) return false;
        session.Update(screenPoint, buttons, modifiers, escapePressed: false);
        return true;
    }

    /// <summary>A button went down or up during the drag: QueryContinueDrag decides (releasing the drag button drops).</summary>
    internal static bool MouseButton(Point screenPoint, MouseButtons buttons, Keys modifiers)
    {
        var session = s_current;
        if (session == null) return false;
        session.Update(screenPoint, buttons, modifiers, escapePressed: false);
        return true;
    }

    /// <summary>A key during the drag: Escape asks to cancel, the modifiers change the key state (copy/move/link).</summary>
    internal static bool Key(Keys keyCode, MouseButtons buttons, Keys modifiers, bool down)
    {
        var session = s_current;
        if (session == null) return false;
        session.Update(session.Position, buttons, modifiers, escapePressed: down && keyCode == Keys.Escape);
        return true;
    }

    // --- target lookup ----------------------------------------------------------------------------------

    /// <summary>The drop target under a screen point: the deepest control there that has AllowDrop, or its nearest such parent.</summary>
    internal static Control? TargetAt(Point screenPoint)
    {
        foreach (var form in FormsTopFirst())
        {
            var target = TargetIn(form, screenPoint, out bool onForm);
            if (onForm) return target;
        }
        return null;
    }

    private static Control? TargetIn(Form form, Point screenPoint, out bool onForm)
    {
        var origin = form.WindowLocation;
        var p = new Point(screenPoint.X - origin.X, screenPoint.Y - origin.Y);
        onForm = new Rectangle(Point.Empty, form.ClientSize).Contains(p);
        if (!onForm) return null;
        for (Control? c = form.HitTest(p); c != null; c = c.Parent)
        {
            if (c.AllowDrop) return c;
        }
        return null;
    }

    private static IEnumerable<Form> FormsTopFirst()
    {
        var forms = new List<Form>();
        foreach (Form f in Application.OpenForms)
        {
            if (f.Visible && !f.IsDisposed && f.TopLevel && f.WindowState != FormWindowState.Minimized) forms.Add(f);
        }
        // The active form, then top-most ones, then the most recently opened: the order windows overlap in.
        forms.Reverse();
        forms.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
        return forms;

        static int Rank(Form f) => f == Form.ActiveForm ? 0 : f.TopMost ? 1 : 2;
    }

    // --- the session --------------------------------------------------------------------------------------

    private sealed class Session
    {
        private readonly Control _source;
        private readonly IDataObject _data;
        private readonly DragDropEffects _allowed;
        private readonly MouseButtons _dragButtons;
        private Control? _target;
        private DragDropEffects _lastEffect;

        public Session(Control source, IDataObject data, DragDropEffects allowed, MouseButtons dragButtons)
        {
            _source = source;
            _data = data;
            _allowed = allowed;
            _dragButtons = dragButtons;
        }

        public bool Done { get; private set; }

        public bool InLoop { get; set; }

        public DragDropEffects Result { get; private set; }

        public Point Position { get; private set; }

        public void Update(Point screenPoint, MouseButtons buttons, Keys modifiers, bool escapePressed)
        {
            if (Done) return;
            Position = screenPoint;
            int keyState = KeyState(buttons, modifiers);

            // IDropSource.QueryContinueDrag: Escape cancels, the drag button(s) released drops.
            var action = escapePressed ? DragAction.Cancel
                : (buttons & _dragButtons) == 0 ? DragAction.Drop
                : DragAction.Continue;
            var query = new QueryContinueDragEventArgs(keyState, escapePressed, action);
            _source.RaiseQueryContinueDrag(query);
            switch (query.Action)
            {
                case DragAction.Cancel:
                    Cancel();
                    return;
                case DragAction.Drop:
                    Drop(screenPoint, keyState);
                    return;
            }

            // IDropTarget.DragEnter / DragOver / DragLeave.
            var target = TargetAt(screenPoint);
            if (target != _target)
            {
                if (_target != null) _target.RaiseDragLeave(EventArgs.Empty);
                _target = target;
                _lastEffect = DragDropEffects.None;
                if (target != null)
                {
                    var enter = new DragEventArgs(_data, keyState, screenPoint.X, screenPoint.Y, _allowed, DragDropEffects.None);
                    target.RaiseDragEnter(enter);
                    _lastEffect = enter.Effect & _allowed;
                }
            }
            else if (target != null)
            {
                var over = new DragEventArgs(_data, keyState, screenPoint.X, screenPoint.Y, _allowed, _lastEffect);
                target.RaiseDragOver(over);
                _lastEffect = over.Effect & _allowed;
            }

            // IDropSource.GiveFeedback: the source may draw its own cursor, else the standard drag cursors show.
            var feedback = new GiveFeedbackEventArgs(_target != null ? _lastEffect : DragDropEffects.None, useDefaultCursors: true);
            _source.RaiseGiveFeedback(feedback);
            if (feedback.UseDefaultCursors) ShowCursor(DefaultCursor(feedback.Effect));
        }

        private void ShowCursor(Cursor cursor)
        {
            var form = _source.TopLevelForm;
            form?.ShowCursorNow(cursor);
        }

        private static Cursor DefaultCursor(DragDropEffects effect) =>
            (effect & DragDropEffects.Move) != 0 ? Cursors.DragMove
            : (effect & DragDropEffects.Copy) != 0 ? Cursors.DragCopy
            : (effect & DragDropEffects.Link) != 0 ? Cursors.DragLink
            : Cursors.No;

        private void Drop(Point screenPoint, int keyState)
        {
            var target = _target;
            if (target != null && _lastEffect != DragDropEffects.None)
            {
                var drop = new DragEventArgs(_data, keyState, screenPoint.X, screenPoint.Y, _allowed, _lastEffect);
                target.RaiseDragDrop(drop);
                Finish(drop.Effect & _allowed);
                return;
            }
            target?.RaiseDragLeave(EventArgs.Empty);
            Finish(DragDropEffects.None);
        }

        public void Cancel()
        {
            _target?.RaiseDragLeave(EventArgs.Empty);
            Finish(DragDropEffects.None);
        }

        private void Finish(DragDropEffects result)
        {
            if (Done) return;
            Done = true;
            Result = result;
            _target = null;
            if (InLoop) Application.Platform.ExitMessageLoop();
        }
    }

    // --- drags from other applications ---------------------------------------------------------------------

    private static Control? s_externalTarget;
    private static DragDropEffects s_externalEffect;
    private static IDataObject? s_externalData;

    /// <summary>A drag from outside entered or moved over <paramref name="form"/>; returns the effect for the OS cursor.</summary>
    internal static DragDropEffects ExternalOver(Form form, Point clientPoint, PlatformDragData data, DragDropEffects allowed, Keys modifiers, bool enter)
    {
        if (enter || s_externalData == null) s_externalData = ToDataObject(data);
        var screen = new Point(form.WindowLocation.X + clientPoint.X, form.WindowLocation.Y + clientPoint.Y);
        var target = TargetIn(form, screen, out _);
        int keyState = KeyState(MouseButtons.Left, modifiers);
        if (target != s_externalTarget)
        {
            s_externalTarget?.RaiseDragLeave(EventArgs.Empty);
            s_externalTarget = target;
            s_externalEffect = DragDropEffects.None;
            if (target != null)
            {
                var e = new DragEventArgs(s_externalData, keyState, screen.X, screen.Y, allowed, DragDropEffects.None);
                target.RaiseDragEnter(e);
                s_externalEffect = e.Effect & allowed;
            }
        }
        else if (target != null)
        {
            var e = new DragEventArgs(s_externalData, keyState, screen.X, screen.Y, allowed, s_externalEffect);
            target.RaiseDragOver(e);
            s_externalEffect = e.Effect & allowed;
        }
        return target != null ? s_externalEffect : DragDropEffects.None;
    }

    internal static void ExternalLeave()
    {
        s_externalTarget?.RaiseDragLeave(EventArgs.Empty);
        s_externalTarget = null;
        s_externalData = null;
        s_externalEffect = DragDropEffects.None;
    }

    internal static DragDropEffects ExternalDrop(Form form, Point clientPoint, PlatformDragData data, DragDropEffects allowed, Keys modifiers)
    {
        // The last DragOver decided the effect; a platform that drops without one first gets an implicit DragEnter.
        ExternalOver(form, clientPoint, data, allowed, modifiers, enter: s_externalData == null);
        var target = s_externalTarget;
        var result = DragDropEffects.None;
        if (target != null && s_externalEffect != DragDropEffects.None)
        {
            var screen = new Point(form.WindowLocation.X + clientPoint.X, form.WindowLocation.Y + clientPoint.Y);
            var e = new DragEventArgs(s_externalData, KeyState(MouseButtons.None, modifiers), screen.X, screen.Y, allowed, s_externalEffect);
            target.RaiseDragDrop(e);
            result = e.Effect & allowed;
        }
        else
        {
            target?.RaiseDragLeave(EventArgs.Empty);
        }
        s_externalTarget = null;
        s_externalData = null;
        s_externalEffect = DragDropEffects.None;
        return result;
    }

    /// <summary>What the platform carried, as the formats WinForms code asks for: FileDrop, Text/UnicodeText, Bitmap.</summary>
    internal static DataObject ToDataObject(PlatformDragData data)
    {
        var result = new DataObject();
        if (data.Files is { Count: > 0 } files)
        {
            var paths = new string[files.Count];
            for (int i = 0; i < files.Count; i++) paths[i] = files[i];
            result.SetData(DataFormats.FileDrop, autoConvert: true, paths);
        }
        if (!string.IsNullOrEmpty(data.Text)) result.SetData(DataFormats.UnicodeText, autoConvert: true, data.Text);
        if (data.ImagePng is { Length: > 0 } png)
        {
            try
            {
                result.SetData(DataFormats.Bitmap, autoConvert: true, new Bitmap(new System.IO.MemoryStream(png)));
            }
            catch (ArgumentException)
            {
                // Not an image we can read: the other formats still go through.
            }
        }
        return result;
    }

    internal static DragDropEffects FromPlatform(PlatformDragEffects effects) => (DragDropEffects)(int)effects;

    internal static PlatformDragEffects ToPlatform(DragDropEffects effects) =>
        (PlatformDragEffects)((int)effects & (int)(DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link));
}
