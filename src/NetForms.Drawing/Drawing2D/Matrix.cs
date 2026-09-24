using System;
using SkiaSharp;

namespace System.Drawing.Drawing2D;

public enum MatrixOrder
{
    Prepend = 0,
    Append = 1,
}

public enum CombineMode
{
    Replace = 0,
    Intersect = 1,
    Union = 2,
    Xor = 3,
    Exclude = 4,
    Complement = 5,
}

public enum FillMode
{
    Alternate = 0,
    Winding = 1,
}

public enum LinearGradientMode
{
    Horizontal = 0,
    Vertical = 1,
    ForwardDiagonal = 2,
    BackwardDiagonal = 3,
}

public enum WrapMode
{
    Tile = 0,
    TileFlipX = 1,
    TileFlipY = 2,
    TileFlipXY = 3,
    Clamp = 4,
}

public enum HatchStyle
{
    Horizontal = 0,
    Vertical = 1,
    ForwardDiagonal = 2,
    BackwardDiagonal = 3,
    Cross = 4,
    DiagonalCross = 5,
    Percent05 = 6,
    Percent10 = 7,
    Percent20 = 8,
    Percent25 = 9,
    Percent30 = 10,
    Percent40 = 11,
    Percent50 = 12,
    Percent60 = 13,
    Percent70 = 14,
    Percent75 = 15,
    Percent80 = 16,
    Percent90 = 17,
    LightDownwardDiagonal = 18,
    LightUpwardDiagonal = 19,
    DarkDownwardDiagonal = 20,
    DarkUpwardDiagonal = 21,
    WideDownwardDiagonal = 22,
    WideUpwardDiagonal = 23,
    LightVertical = 24,
    LightHorizontal = 25,
    NarrowVertical = 26,
    NarrowHorizontal = 27,
    DarkVertical = 28,
    DarkHorizontal = 29,
    DashedDownwardDiagonal = 30,
    DashedUpwardDiagonal = 31,
    DashedHorizontal = 32,
    DashedVertical = 33,
    SmallConfetti = 34,
    LargeConfetti = 35,
    ZigZag = 36,
    Wave = 37,
    DiagonalBrick = 38,
    HorizontalBrick = 39,
    Weave = 40,
    Plaid = 41,
    Divot = 42,
    DottedGrid = 43,
    DottedDiamond = 44,
    Shingle = 45,
    Trellis = 46,
    Sphere = 47,
    SmallGrid = 48,
    SmallCheckerBoard = 49,
    LargeCheckerBoard = 50,
    OutlinedDiamond = 51,
    SolidDiamond = 52,
    LargeGrid = Cross,
    Min = Horizontal,
    Max = SolidDiamond,
}

/// <summary>A 2D affine matrix (GDI+ layout: m11 m12 / m21 m22 / dx dy) over <see cref="SKMatrix"/>.</summary>
public sealed class Matrix : IDisposable
{
    private SKMatrix _m;

    public Matrix() => _m = SKMatrix.Identity;

    public Matrix(float m11, float m12, float m21, float m22, float dx, float dy) =>
        _m = new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1);

    public Matrix(Rectangle rect, Point[] plgpts) : this((RectangleF)rect, ToPointF(plgpts)) { }

    public Matrix(RectangleF rect, PointF[] plgpts)
    {
        ArgumentNullException.ThrowIfNull(plgpts);
        if (plgpts.Length != 3) throw new ArgumentException("Three points are required.", nameof(plgpts));
        // Map the rectangle's top-left, top-right and bottom-left onto the three points.
        float sx = rect.Width == 0 ? 0 : 1f / rect.Width;
        float sy = rect.Height == 0 ? 0 : 1f / rect.Height;
        float m11 = (plgpts[1].X - plgpts[0].X) * sx;
        float m12 = (plgpts[1].Y - plgpts[0].Y) * sx;
        float m21 = (plgpts[2].X - plgpts[0].X) * sy;
        float m22 = (plgpts[2].Y - plgpts[0].Y) * sy;
        float dx = plgpts[0].X - rect.X * m11 - rect.Y * m21;
        float dy = plgpts[0].Y - rect.X * m12 - rect.Y * m22;
        _m = new SKMatrix(m11, m21, dx, m12, m22, dy, 0, 0, 1);
    }

    internal Matrix(SKMatrix m) => _m = m;

    internal SKMatrix Skia => _m;

    public float[] Elements => new[] { _m.ScaleX, _m.SkewY, _m.SkewX, _m.ScaleY, _m.TransX, _m.TransY };

    public float OffsetX => _m.TransX;
    public float OffsetY => _m.TransY;

    public bool IsIdentity => _m.IsIdentity;

    public bool IsInvertible => _m.TryInvert(out _);

    public Matrix Clone() => new Matrix(_m);

    public void Reset() => _m = SKMatrix.Identity;

    public void Multiply(Matrix matrix) => Multiply(matrix, MatrixOrder.Prepend);

    public void Multiply(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        Combine(matrix._m, order);
    }

    private void Combine(SKMatrix other, MatrixOrder order)
    {
        // GDI+ "Prepend" applies `other` first: result = other × this in row-vector terms,
        // which is SKMatrix.Concat(this, other) in Skia's column-vector convention.
        _m = order == MatrixOrder.Prepend ? SKMatrix.Concat(_m, other) : SKMatrix.Concat(other, _m);
    }

    public void Translate(float offsetX, float offsetY) => Translate(offsetX, offsetY, MatrixOrder.Prepend);

    public void Translate(float offsetX, float offsetY, MatrixOrder order) => Combine(SKMatrix.CreateTranslation(offsetX, offsetY), order);

    public void Scale(float scaleX, float scaleY) => Scale(scaleX, scaleY, MatrixOrder.Prepend);

    public void Scale(float scaleX, float scaleY, MatrixOrder order) => Combine(SKMatrix.CreateScale(scaleX, scaleY), order);

    public void Rotate(float angle) => Rotate(angle, MatrixOrder.Prepend);

    public void Rotate(float angle, MatrixOrder order) => Combine(SKMatrix.CreateRotationDegrees(angle), order);

    public void RotateAt(float angle, PointF point) => RotateAt(angle, point, MatrixOrder.Prepend);

    public void RotateAt(float angle, PointF point, MatrixOrder order) => Combine(SKMatrix.CreateRotationDegrees(angle, point.X, point.Y), order);

    public void Shear(float shearX, float shearY) => Shear(shearX, shearY, MatrixOrder.Prepend);

    public void Shear(float shearX, float shearY, MatrixOrder order) => Combine(SKMatrix.CreateSkew(shearX, shearY), order);

    public void Invert()
    {
        if (_m.TryInvert(out var inv)) _m = inv;
    }

    public void TransformPoints(PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var p = _m.MapPoint(pts[i].X, pts[i].Y);
            pts[i] = new PointF(p.X, p.Y);
        }
    }

    public void TransformPoints(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var p = _m.MapPoint(pts[i].X, pts[i].Y);
            pts[i] = new Point((int)Math.Round(p.X), (int)Math.Round(p.Y));
        }
    }

    public void TransformVectors(PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var v = _m.MapVector(pts[i].X, pts[i].Y);
            pts[i] = new PointF(v.X, v.Y);
        }
    }

    public void TransformVectors(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        for (int i = 0; i < pts.Length; i++)
        {
            var v = _m.MapVector(pts[i].X, pts[i].Y);
            pts[i] = new Point((int)Math.Round(v.X), (int)Math.Round(v.Y));
        }
    }

    public void VectorTransformPoints(Point[] pts) => TransformVectors(pts);

    public override bool Equals(object? obj) => obj is Matrix m && m._m == _m;

    public override int GetHashCode() => _m.GetHashCode();

    public void Dispose() { }

    private static PointF[] ToPointF(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        var r = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++) r[i] = pts[i];
        return r;
    }
}
