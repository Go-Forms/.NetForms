using System;
using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// An area made of rectangles, over <see cref="SKRegion"/> (integer pixel coverage, which
/// is also what GDI+ regions are once rasterised). An "infinite" region is tracked by a flag
/// so it stays infinite through Union/Complement rather than clamping to a large box.
/// </summary>
public sealed class Region : IDisposable
{
    // Big enough for any window, small enough not to overflow int arithmetic in Skia.
    private static readonly SKRectI s_infinite = new(-1 << 22, -1 << 22, 1 << 22, 1 << 22);

    private SKRegion _region;
    private bool _isInfinite;

    public Region()
    {
        _region = new SKRegion(s_infinite);
        _isInfinite = true;
    }

    public Region(Rectangle rect)
    {
        _region = new SKRegion(ToSK(rect));
    }

    public Region(RectangleF rect) : this(Rectangle.Round(rect)) { }

    public Region(GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        _region = new SKRegion();
        using var clip = new SKRegion(s_infinite);
        _region.SetPath(path.Skia, clip);
    }

    private Region(SKRegion region, bool infinite)
    {
        _region = region;
        _isInfinite = infinite;
    }

    internal SKRegion Skia => _region;

    private static SKRectI ToSK(Rectangle r) => new(r.Left, r.Top, r.Right, r.Bottom);

    public Region Clone() => new Region(new SKRegion(_region), _isInfinite);

    public void MakeEmpty()
    {
        _region.SetEmpty();
        _isInfinite = false;
    }

    public void MakeInfinite()
    {
        _region.SetRect(s_infinite);
        _isInfinite = true;
    }

    public bool IsEmpty(Graphics g) => _region.IsEmpty;

    public bool IsInfinite(Graphics g) => _isInfinite;

    public RectangleF GetBounds(Graphics g)
    {
        var b = _region.Bounds;
        return new RectangleF(b.Left, b.Top, b.Width, b.Height);
    }

    public bool IsVisible(Point point) => _region.Contains(point.X, point.Y);

    public bool IsVisible(PointF point) => _region.Contains((int)Math.Floor(point.X), (int)Math.Floor(point.Y));

    public bool IsVisible(int x, int y) => _region.Contains(x, y);

    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    public bool IsVisible(Point point, Graphics? g) => IsVisible(point);

    public bool IsVisible(PointF point, Graphics? g) => IsVisible(point);

    public bool IsVisible(Rectangle rect) => _region.Intersects(ToSK(rect));

    public bool IsVisible(RectangleF rect) => IsVisible(Rectangle.Round(rect));

    public bool IsVisible(Rectangle rect, Graphics? g) => IsVisible(rect);

    public bool IsVisible(RectangleF rect, Graphics? g) => IsVisible(rect);

    public RectangleF[] GetRegionScans(Matrix matrix)
    {
        var list = new System.Collections.Generic.List<RectangleF>();
        using var it = _region.CreateRectIterator();
        while (it.Next(out var r))
        {
            var rf = new RectangleF(r.Left, r.Top, r.Width, r.Height);
            list.Add(rf);
        }
        return list.ToArray();
    }

    // --- combination ---------------------------------------------------------------

    private void Apply(SKRegion other, bool otherInfinite, SKRegionOperation op)
    {
        _region.Op(other, op);
        _isInfinite = op switch
        {
            SKRegionOperation.Union => _isInfinite || otherInfinite,
            SKRegionOperation.Intersect => _isInfinite && otherInfinite,
            SKRegionOperation.Difference => _isInfinite && !otherInfinite && other.IsEmpty,
            SKRegionOperation.ReverseDifference => otherInfinite && !_isInfinite && _region.IsEmpty,
            SKRegionOperation.XOR => _isInfinite != otherInfinite,
            _ => false,
        };
    }

    private static SKRegion FromRect(Rectangle rect) => new SKRegion(ToSK(rect));

    private static SKRegion FromPath(GraphicsPath path)
    {
        var r = new SKRegion();
        using var clip = new SKRegion(s_infinite);
        r.SetPath(path.Skia, clip);
        return r;
    }

    public void Intersect(Rectangle rect) { using var o = FromRect(rect); Apply(o, false, SKRegionOperation.Intersect); }
    public void Intersect(RectangleF rect) => Intersect(Rectangle.Round(rect));
    public void Intersect(GraphicsPath path) { using var o = FromPath(path); Apply(o, false, SKRegionOperation.Intersect); }
    public void Intersect(Region region) => Apply(region._region, region._isInfinite, SKRegionOperation.Intersect);

    public void Union(Rectangle rect) { using var o = FromRect(rect); Apply(o, false, SKRegionOperation.Union); }
    public void Union(RectangleF rect) => Union(Rectangle.Round(rect));
    public void Union(GraphicsPath path) { using var o = FromPath(path); Apply(o, false, SKRegionOperation.Union); }
    public void Union(Region region) => Apply(region._region, region._isInfinite, SKRegionOperation.Union);

    public void Exclude(Rectangle rect) { using var o = FromRect(rect); Apply(o, false, SKRegionOperation.Difference); }
    public void Exclude(RectangleF rect) => Exclude(Rectangle.Round(rect));
    public void Exclude(GraphicsPath path) { using var o = FromPath(path); Apply(o, false, SKRegionOperation.Difference); }
    public void Exclude(Region region) => Apply(region._region, region._isInfinite, SKRegionOperation.Difference);

    public void Xor(Rectangle rect) { using var o = FromRect(rect); Apply(o, false, SKRegionOperation.XOR); }
    public void Xor(RectangleF rect) => Xor(Rectangle.Round(rect));
    public void Xor(GraphicsPath path) { using var o = FromPath(path); Apply(o, false, SKRegionOperation.XOR); }
    public void Xor(Region region) => Apply(region._region, region._isInfinite, SKRegionOperation.XOR);

    /// <summary>This = other − this.</summary>
    public void Complement(Rectangle rect) { using var o = FromRect(rect); Apply(o, false, SKRegionOperation.ReverseDifference); }
    public void Complement(RectangleF rect) => Complement(Rectangle.Round(rect));
    public void Complement(GraphicsPath path) { using var o = FromPath(path); Apply(o, false, SKRegionOperation.ReverseDifference); }
    public void Complement(Region region) => Apply(region._region, region._isInfinite, SKRegionOperation.ReverseDifference);

    public void Translate(int dx, int dy)
    {
        if (_isInfinite) return;
        _region.Translate(dx, dy);
    }

    public void Translate(float dx, float dy) => Translate((int)Math.Round(dx), (int)Math.Round(dy));

    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (_isInfinite) return;
        using var path = _region.GetBoundaryPath();
        path.Transform(matrix.Skia);
        using var clip = new SKRegion(s_infinite);
        var r = new SKRegion();
        r.SetPath(path, clip);
        _region.Dispose();
        _region = r;
    }

    public bool Equals(Region region, Graphics g)
    {
        ArgumentNullException.ThrowIfNull(region);
        using var xor = new SKRegion(_region);
        xor.Op(region._region, SKRegionOperation.XOR);
        return xor.IsEmpty && _isInfinite == region._isInfinite;
    }

    public void Dispose()
    {
        _region.Dispose();
        GC.SuppressFinalize(this);
    }
}
