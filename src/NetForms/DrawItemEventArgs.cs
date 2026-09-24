using System;
using System.Drawing;

namespace System.Windows.Forms;

public enum DrawMode
{
    Normal = 0,
    OwnerDrawFixed = 1,
    OwnerDrawVariable = 2,
}

/// <summary>Owner-draw mode of <see cref="TabControl"/> - its tabs are all the same height, so there is no variable variant.</summary>
public enum TabDrawMode
{
    Normal = 0,
    OwnerDrawFixed = 1,
}

[Flags]
public enum DrawItemState
{
    None = 0,
    Selected = 1,
    Grayed = 2,
    Disabled = 4,
    Checked = 8,
    Focus = 16,
    Default = 32,
    HotLight = 64,
    Inactive = 128,
    NoAccelerator = 256,
    NoFocusRect = 512,
    ComboBoxEdit = 4096,
}

public delegate void DrawItemEventHandler(object? sender, DrawItemEventArgs e);
public delegate void MeasureItemEventHandler(object? sender, MeasureItemEventArgs e);

public class DrawItemEventArgs : EventArgs
{
    public DrawItemEventArgs(Graphics graphics, Font font, Rectangle rect, int index, DrawItemState state)
        : this(graphics, font, rect, index, state, SystemColors.WindowText, SystemColors.Window) { }

    public DrawItemEventArgs(Graphics graphics, Font font, Rectangle rect, int index, DrawItemState state, Color foreColor, Color backColor)
    {
        Graphics = graphics;
        Font = font;
        Bounds = rect;
        Index = index;
        State = state;
        ForeColor = (state & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : foreColor;
        BackColor = (state & DrawItemState.Selected) != 0 ? SystemColors.Highlight : backColor;
    }

    public Graphics Graphics { get; }
    public Font Font { get; }
    public Rectangle Bounds { get; }
    public int Index { get; }
    public DrawItemState State { get; }
    public Color ForeColor { get; }
    public Color BackColor { get; }

    public virtual void DrawBackground()
    {
        using var brush = new SolidBrush(BackColor);
        Graphics.FillRectangle(brush, Bounds);
    }

    public virtual void DrawFocusRectangle()
    {
        if ((State & DrawItemState.Focus) != 0 && (State & DrawItemState.NoFocusRect) == 0)
        {
            ControlPaint.DrawFocusRectangle(Graphics, Bounds, ForeColor, BackColor);
        }
    }
}

public class MeasureItemEventArgs : EventArgs
{
    public MeasureItemEventArgs(Graphics graphics, int index) : this(graphics, index, 0) { }

    public MeasureItemEventArgs(Graphics graphics, int index, int itemHeight)
    {
        Graphics = graphics;
        Index = index;
        ItemHeight = itemHeight;
    }

    public Graphics Graphics { get; }
    public int Index { get; }
    public int ItemHeight { get; set; }
    public int ItemWidth { get; set; }
}
