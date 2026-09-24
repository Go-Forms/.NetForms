using System;
using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing;

public sealed class Pen : ICloneable, IDisposable
{
    private Color _color;
    private float _width;
    private DashStyle _dashStyle = DashStyle.Solid;
    private float[]? _dashPattern;

    internal bool IsSystemOwned { get; set; }

    public Pen(Color color) : this(color, 1f) { }

    public Pen(Color color, float width)
    {
        _color = color;
        _width = width;
    }

    public Pen(Brush brush) : this(brush, 1f) { }

    public Pen(Brush brush, float width)
    {
        ArgumentNullException.ThrowIfNull(brush);
        _color = brush is SolidBrush sb ? sb.Color : Color.Black;
        _width = width;
    }

    public Color Color
    {
        get => _color;
        set { CheckMutable(); _color = value; }
    }

    public float Width
    {
        get => _width;
        set { CheckMutable(); _width = value; }
    }

    public DashStyle DashStyle
    {
        get => _dashStyle;
        set { CheckMutable(); _dashStyle = value; }
    }

    public float[] DashPattern
    {
        get => _dashPattern ?? Array.Empty<float>();
        set { CheckMutable(); _dashPattern = value; _dashStyle = DashStyle.Custom; }
    }

    public LineCap StartCap { get; set; } = LineCap.Flat;
    public LineCap EndCap { get; set; } = LineCap.Flat;
    public LineJoin LineJoin { get; set; } = LineJoin.Miter;

    public Brush Brush
    {
        get => new SolidBrush(_color);
        set { CheckMutable(); if (value is SolidBrush sb) _color = sb.Color; }
    }

    private void CheckMutable()
    {
        if (IsSystemOwned) throw new ArgumentException("Cannot change a system pen.");
    }

    public object Clone() => new Pen(_color, _width) { _dashStyle = _dashStyle, _dashPattern = _dashPattern, StartCap = StartCap, EndCap = EndCap, LineJoin = LineJoin };

    internal void ApplyTo(SKPaint paint)
    {
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = SkiaConvert.ToSK(_color);
        paint.StrokeWidth = _width;
        paint.Shader = null;
        paint.StrokeCap = StartCap switch
        {
            LineCap.Round => SKStrokeCap.Round,
            LineCap.Square => SKStrokeCap.Square,
            _ => SKStrokeCap.Butt,
        };
        paint.StrokeJoin = LineJoin switch
        {
            LineJoin.Bevel => SKStrokeJoin.Bevel,
            LineJoin.Round => SKStrokeJoin.Round,
            _ => SKStrokeJoin.Miter,
        };
        float w = Math.Max(1f, _width);
        paint.PathEffect = _dashStyle switch
        {
            DashStyle.Dash => SKPathEffect.CreateDash(new[] { 3 * w, w }, 0),
            DashStyle.Dot => SKPathEffect.CreateDash(new[] { w, w }, 0),
            DashStyle.DashDot => SKPathEffect.CreateDash(new[] { 3 * w, w, w, w }, 0),
            DashStyle.DashDotDot => SKPathEffect.CreateDash(new[] { 3 * w, w, w, w, w, w }, 0),
            DashStyle.Custom when _dashPattern is { Length: > 1 } => SKPathEffect.CreateDash(Scale(_dashPattern, w), 0),
            _ => null,
        };
    }

    private static float[] Scale(float[] pattern, float w)
    {
        var r = new float[pattern.Length % 2 == 0 ? pattern.Length : pattern.Length * 2];
        for (int i = 0; i < r.Length; i++) r[i] = pattern[i % pattern.Length] * w;
        return r;
    }

    public void Dispose()
    {
        if (IsSystemOwned) return;
        GC.SuppressFinalize(this);
    }
}
