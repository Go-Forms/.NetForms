using System.Drawing;

namespace System.Windows.Forms;

// Drag and drop: the types of WinForms. The protocol itself is DragDropManager (decision 155).

[Flags]
public enum DragDropEffects
{
    None = 0x00000000,
    Copy = 0x00000001,
    Move = 0x00000002,
    Link = 0x00000004,
    Scroll = unchecked((int)0x80000000),
    All = Copy | Move | Scroll,
}

public enum DragAction
{
    Continue = 0,
    Drop = 1,
    Cancel = 2,
}

public enum DropImageType
{
    Invalid = -1,
    None = 0,
    Copy = DragDropEffects.Copy,
    Move = DragDropEffects.Move,
    Link = DragDropEffects.Link,
    Label = 6,
    Warning = 7,
    NoImage = 8,
}

public class DragEventArgs : EventArgs
{
    public DragEventArgs(IDataObject? data, int keyState, int x, int y, DragDropEffects allowedEffect, DragDropEffects effect)
        : this(data, keyState, x, y, allowedEffect, effect, DropImageType.Invalid, string.Empty, string.Empty)
    {
    }

    public DragEventArgs(IDataObject? data, int keyState, int x, int y, DragDropEffects allowedEffect, DragDropEffects effect,
        DropImageType dropImageType, string? message, string? messageReplacementToken)
    {
        Data = data;
        KeyState = keyState;
        X = x;
        Y = y;
        AllowedEffect = allowedEffect;
        Effect = effect;
        DropImageType = dropImageType;
        Message = message;
        MessageReplacementToken = messageReplacementToken;
    }

    public IDataObject? Data { get; }
    public int KeyState { get; }
    public int X { get; }
    public int Y { get; }
    public DragDropEffects AllowedEffect { get; }
    public DragDropEffects Effect { get; set; }
    public DropImageType DropImageType { get; set; }
    public string? Message { get; set; }
    public string? MessageReplacementToken { get; set; }
}

public delegate void DragEventHandler(object? sender, DragEventArgs e);

public class GiveFeedbackEventArgs : EventArgs
{
    public GiveFeedbackEventArgs(DragDropEffects effect, bool useDefaultCursors)
        : this(effect, useDefaultCursors, dragImage: null, cursorOffset: default, useDefaultDragImage: false)
    {
    }

    public GiveFeedbackEventArgs(DragDropEffects effect, bool useDefaultCursors, Bitmap? dragImage, Point cursorOffset, bool useDefaultDragImage)
    {
        Effect = effect;
        UseDefaultCursors = useDefaultCursors;
        DragImage = dragImage;
        CursorOffset = cursorOffset;
        UseDefaultDragImage = useDefaultDragImage;
    }

    public DragDropEffects Effect { get; }
    public bool UseDefaultCursors { get; set; }
    public Bitmap? DragImage { get; set; }
    public Point CursorOffset { get; set; }
    public bool UseDefaultDragImage { get; set; }
}

public delegate void GiveFeedbackEventHandler(object? sender, GiveFeedbackEventArgs e);

public class QueryContinueDragEventArgs : EventArgs
{
    public QueryContinueDragEventArgs(int keyState, bool escapePressed, DragAction action)
    {
        KeyState = keyState;
        EscapePressed = escapePressed;
        Action = action;
    }

    public int KeyState { get; }
    public bool EscapePressed { get; }
    public DragAction Action { get; set; }
}

public delegate void QueryContinueDragEventHandler(object? sender, QueryContinueDragEventArgs e);

/// <summary>ItemDrag of TreeView and ListView: the user started dragging an item with <see cref="Button"/>.</summary>
public class ItemDragEventArgs : EventArgs
{
    public ItemDragEventArgs(MouseButtons button) : this(button, null)
    {
    }

    public ItemDragEventArgs(MouseButtons button, object? item)
    {
        Button = button;
        Item = item;
    }

    public MouseButtons Button { get; }

    public object? Item { get; }
}

public delegate void ItemDragEventHandler(object? sender, ItemDragEventArgs e);
