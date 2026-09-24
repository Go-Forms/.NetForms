using System.Drawing;
using SkiaSharp;

namespace NetForms.Platform;

/// <summary>
/// The window's owner on the NetForms side. The platform calls these on the UI thread,
/// in device-independent pixels, and never holds on to the canvas after Paint returns.
/// </summary>
public interface IWindowHost
{
    /// <summary>
    /// Paint onto <paramref name="canvas"/> (origin = client top-left). Only <paramref name="clip"/>
    /// needs repainting; the canvas is already clipped to it and the platform keeps the rest.
    /// </summary>
    void Paint(SKCanvas canvas, Size clientSize, Rectangle clip);

    void MouseDown(MouseButton button, Point position, int clicks, InputModifiers modifiers);
    void MouseUp(MouseButton button, Point position, InputModifiers modifiers);
    void MouseMove(Point position, MouseButton pressedButtons, InputModifiers modifiers);
    void MouseWheel(Point position, int delta, InputModifiers modifiers);
    void MouseLeave();

    /// <param name="virtualKey">Win32 virtual-key code (the values of <c>System.Windows.Forms.Keys</c>).</param>
    /// <returns>true if the key was consumed and should not be handled further by the platform.</returns>
    bool KeyDown(int virtualKey, InputModifiers modifiers);
    bool KeyUp(int virtualKey, InputModifiers modifiers);
    void TextInput(string text);

    void Resized(Size clientSize);
    void Moved(Point location);
    void Activated();
    void Deactivated();
    void StateChanged(PlatformWindowState state);
    /// <summary>The user or the code asked to close. Return true to cancel.</summary>
    bool Closing(bool userRequested);
    void Closed();
    /// <summary>The window is now visible on screen.</summary>
    void Shown();
}

[System.Flags]
public enum MouseButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 4,
    XButton1 = 8,
    XButton2 = 16,
}

[System.Flags]
public enum InputModifiers
{
    None = 0,
    Shift = 1,
    Control = 2,
    Alt = 4,
}
