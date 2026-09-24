using System;

namespace System.Windows.Forms;

/// <summary>A named mouse cursor. Only identity for now; the platform maps the name to a native cursor.</summary>
[System.ComponentModel.TypeConverter(typeof(CursorConverter))]
public sealed class Cursor : IDisposable
{
    private Cursor(string name, bool named) => Name = name;

    /// <summary>A cursor the platform knows by name (the Cursors set).</summary>
    internal static Cursor Named(string name) => new(name, named: true);

    /// <summary>
    /// A cursor from a .cur/.ico file. The platform layer shows named cursors only (decision 116): a custom
    /// one is kept - its image, size and hot spot - and shown as the default arrow.
    /// </summary>
    public Cursor(System.IO.Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Name = "Default";
        try
        {
            using var icon = new System.Drawing.Icon(stream);
            Size = icon.Size;
            _image = icon.ToBitmap();
        }
        catch (ArgumentException)
        {
        }
    }

    public Cursor(string fileName) : this(OpenFile(fileName)) { }

    public Cursor(Type type, string resource) : this(type.Module.Assembly.GetManifestResourceStream(type, resource)
        ?? throw new ArgumentException($"Resource '{resource}' cannot be found in class '{type.FullName}'.")) { }

    public Cursor(IntPtr handle)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("Parameter is not valid.", nameof(handle));
        Name = "Default";
    }

    private static System.IO.Stream OpenFile(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return System.IO.File.OpenRead(fileName.Replace('\\', System.IO.Path.DirectorySeparatorChar));
    }

    private readonly System.Drawing.Bitmap? _image;

    internal string Name { get; }

    public System.Drawing.Size Size { get; } = new(32, 32);

    public System.Drawing.Point HotSpot => System.Drawing.Point.Empty;

    public IntPtr Handle => IntPtr.Zero;

    public IntPtr CopyHandle() => IntPtr.Zero;

    public object? Tag { get; set; }

    public void Draw(System.Drawing.Graphics g, System.Drawing.Rectangle targetRect)
    {
        if (_image != null) g.DrawImage(_image, targetRect.X, targetRect.Y, Size.Width, Size.Height);
    }

    public void DrawStretched(System.Drawing.Graphics g, System.Drawing.Rectangle targetRect)
    {
        if (_image != null) g.DrawImage(_image, targetRect);
    }

    private static Cursor? s_current;

    /// <summary>
    /// The cursor shown now. Setting it shows it on the active form until the mouse moves (WM_SETCURSOR
    /// then restores the control's cursor) - enough for the wait cursor of a blocking operation.
    /// </summary>
    public static Cursor? Current
    {
        get => s_current ?? Cursors.Default;
        set
        {
            s_current = value;
            Form.ActiveForm?.ShowCursorNow(value ?? Cursors.Default);
        }
    }

    /// <summary>The pointer in screen coordinates. Setting it is not supported by the platform layer (decision 116) and does nothing.</summary>
    public static System.Drawing.Point Position
    {
        get => Control.MousePosition;
        set { }
    }

    public static System.Drawing.Rectangle Clip { get; set; }

    public static void Hide() { }

    public static void Show() { }

    public void Dispose() => _image?.Dispose();

    public override string ToString() => "[Cursor: " + Name + "]";

    public override bool Equals(object? obj) => obj is Cursor c && c.Name == Name;

    public override int GetHashCode() => Name.GetHashCode();

    public static bool operator ==(Cursor? left, Cursor? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Cursor? left, Cursor? right) => !(left == right);
}

public static class Cursors
{
    public static Cursor Default { get; } = Cursor.Named("Default");
    public static Cursor Arrow { get; } = Cursor.Named("Arrow");
    public static Cursor Hand { get; } = Cursor.Named("Hand");
    public static Cursor IBeam { get; } = Cursor.Named("IBeam");
    public static Cursor WaitCursor { get; } = Cursor.Named("Wait");
    public static Cursor Cross { get; } = Cursor.Named("Cross");
    public static Cursor No { get; } = Cursor.Named("No");
    public static Cursor SizeAll { get; } = Cursor.Named("SizeAll");
    public static Cursor SizeNS { get; } = Cursor.Named("SizeNS");
    public static Cursor SizeWE { get; } = Cursor.Named("SizeWE");
    public static Cursor SizeNESW { get; } = Cursor.Named("SizeNESW");
    public static Cursor SizeNWSE { get; } = Cursor.Named("SizeNWSE");
    public static Cursor Help { get; } = Cursor.Named("Help");
    public static Cursor AppStarting { get; } = Cursor.Named("AppStarting");
    public static Cursor HSplit { get; } = Cursor.Named("HSplit");
    public static Cursor VSplit { get; } = Cursor.Named("VSplit");
    public static Cursor UpArrow { get; } = Cursor.Named("UpArrow");
}
