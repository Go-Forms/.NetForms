using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public delegate void PaintEventHandler(object? sender, PaintEventArgs e);
public delegate void MouseEventHandler(object? sender, MouseEventArgs e);
public delegate void KeyEventHandler(object? sender, KeyEventArgs e);
public delegate void KeyPressEventHandler(object? sender, KeyPressEventArgs e);
public delegate void LayoutEventHandler(object? sender, LayoutEventArgs e);
public delegate void ControlEventHandler(object? sender, ControlEventArgs e);
public delegate void InvalidateEventHandler(object? sender, InvalidateEventArgs e);
public delegate void FormClosingEventHandler(object? sender, FormClosingEventArgs e);
public delegate void FormClosedEventHandler(object? sender, FormClosedEventArgs e);

public class PaintEventArgs : EventArgs, IDisposable
{
    public PaintEventArgs(Graphics graphics, Rectangle clipRect)
    {
        Graphics = graphics ?? throw new ArgumentNullException(nameof(graphics));
        ClipRectangle = clipRect;
    }

    public Graphics Graphics { get; }
    public Rectangle ClipRectangle { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}

public class MouseEventArgs : EventArgs
{
    public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
    {
        Button = button;
        Clicks = clicks;
        X = x;
        Y = y;
        Delta = delta;
    }

    public MouseButtons Button { get; }
    public int Clicks { get; }
    public int X { get; }
    public int Y { get; }
    public int Delta { get; }
    public Point Location => new Point(X, Y);
}

public class KeyEventArgs : EventArgs
{
    public KeyEventArgs(Keys keyData) => KeyData = keyData;

    public Keys KeyData { get; }
    public Keys KeyCode => KeyData & Keys.KeyCode;
    public int KeyValue => (int)(KeyData & Keys.KeyCode);
    public Keys Modifiers => KeyData & Keys.Modifiers;
    public bool Alt => (KeyData & Keys.Alt) == Keys.Alt;
    public bool Control => (KeyData & Keys.Control) == Keys.Control;
    public bool Shift => (KeyData & Keys.Shift) == Keys.Shift;
    public bool Handled { get; set; }
    public bool SuppressKeyPress { get; set; }
}

public class KeyPressEventArgs : EventArgs
{
    public KeyPressEventArgs(char keyChar) => KeyChar = keyChar;

    public char KeyChar { get; set; }
    public bool Handled { get; set; }
}

public sealed class LayoutEventArgs : EventArgs
{
    public LayoutEventArgs(Control? affectedControl, string? affectedProperty)
    {
        AffectedControl = affectedControl;
        AffectedProperty = affectedProperty;
    }

    public Control? AffectedControl { get; }
    public string? AffectedProperty { get; }
}

public class ControlEventArgs : EventArgs
{
    public ControlEventArgs(Control? control) => Control = control;

    public Control? Control { get; }
}

public class InvalidateEventArgs : EventArgs
{
    public InvalidateEventArgs(Rectangle invalidRect) => InvalidRect = invalidRect;

    public Rectangle InvalidRect { get; }
}

public class FormClosingEventArgs : CancelEventArgs
{
    public FormClosingEventArgs(CloseReason closeReason, bool cancel) : base(cancel) => CloseReason = closeReason;

    public CloseReason CloseReason { get; }
}

public class FormClosedEventArgs : EventArgs
{
    public FormClosedEventArgs(CloseReason closeReason) => CloseReason = closeReason;

    public CloseReason CloseReason { get; }
}
