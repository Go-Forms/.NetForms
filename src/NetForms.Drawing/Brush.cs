using System;
using SkiaSharp;

namespace System.Drawing;

public abstract class Brush : ICloneable, IDisposable
{
    /// <summary>Set on the shared <see cref="Brushes"/>/<see cref="SystemBrushes"/> instances: Dispose is a no-op for them, as in GDI+.</summary>
    internal bool IsSystemOwned { get; set; }

    public abstract object Clone();

    /// <summary>Configure a Skia paint for filling with this brush.</summary>
    internal abstract void ApplyTo(SKPaint paint);

    public void Dispose()
    {
        if (IsSystemOwned) return;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}

public sealed class SolidBrush : Brush
{
    private Color _color;

    public SolidBrush(Color color) => _color = color;

    public Color Color
    {
        get => _color;
        set
        {
            if (IsSystemOwned) throw new ArgumentException("Cannot change a system brush.");
            _color = value;
        }
    }

    public override object Clone() => new SolidBrush(_color);

    internal override void ApplyTo(SKPaint paint)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = SkiaConvert.ToSK(_color);
        paint.Shader = null;
    }
}

internal static class SkiaConvert
{
    public static SKColor ToSK(Color c)
    {
        c = HermeticRendering.Resolve(c);
        return new SKColor(c.R, c.G, c.B, c.A);
    }
    public static Color ToColor(SKColor c) => Color.FromArgb(c.Alpha, c.Red, c.Green, c.Blue);
    public static SKRect ToSK(RectangleF r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);
    public static SKRect ToSK(Rectangle r) => new SKRect(r.Left, r.Top, r.Right, r.Bottom);
}
