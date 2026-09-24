using System;
using SkiaSharp;

namespace System.Drawing.Drawing2D;

public sealed class LinearGradientBrush : Brush
{
    private PointF _p1, _p2;
    private Color _c1, _c2;
    private Matrix _transform = new();

    public LinearGradientBrush(Point point1, Point point2, Color color1, Color color2)
        : this((PointF)point1, point2, color1, color2) { }

    public LinearGradientBrush(PointF point1, PointF point2, Color color1, Color color2)
    {
        _p1 = point1;
        _p2 = point2;
        _c1 = color1;
        _c2 = color2;
        Rectangle = RectangleF.FromLTRB(Math.Min(point1.X, point2.X), Math.Min(point1.Y, point2.Y), Math.Max(point1.X, point2.X), Math.Max(point1.Y, point2.Y));
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
        : this((RectangleF)rect, color1, color2, linearGradientMode) { }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, LinearGradientMode linearGradientMode)
    {
        if (rect.Width == 0 || rect.Height == 0) throw new ArgumentException("Rectangle must have a non-zero size.", nameof(rect));
        Rectangle = rect;
        _c1 = color1;
        _c2 = color2;
        (_p1, _p2) = linearGradientMode switch
        {
            LinearGradientMode.Vertical => (new PointF(rect.Left, rect.Top), new PointF(rect.Left, rect.Bottom)),
            LinearGradientMode.ForwardDiagonal => (new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Bottom)),
            LinearGradientMode.BackwardDiagonal => (new PointF(rect.Right, rect.Top), new PointF(rect.Left, rect.Bottom)),
            _ => (new PointF(rect.Left, rect.Top), new PointF(rect.Right, rect.Top)),
        };
    }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle)
        : this((RectangleF)rect, color1, color2, angle, false) { }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle)
        : this(rect, color1, color2, angle, false) { }

    public LinearGradientBrush(Rectangle rect, Color color1, Color color2, float angle, bool isAngleScaleable)
        : this((RectangleF)rect, color1, color2, angle, isAngleScaleable) { }

    public LinearGradientBrush(RectangleF rect, Color color1, Color color2, float angle, bool isAngleScaleable)
    {
        if (rect.Width == 0 || rect.Height == 0) throw new ArgumentException("Rectangle must have a non-zero size.", nameof(rect));
        Rectangle = rect;
        _c1 = color1;
        _c2 = color2;
        double rad = angle * Math.PI / 180.0;
        float cx = rect.Left + rect.Width / 2f, cy = rect.Top + rect.Height / 2f;
        // Half-length of the gradient axis: the projection of the rectangle onto the angle.
        float half = (float)(Math.Abs(rect.Width * Math.Cos(rad)) + Math.Abs(rect.Height * Math.Sin(rad))) / 2f;
        float dx = (float)(Math.Cos(rad) * half), dy = (float)(Math.Sin(rad) * half);
        _p1 = new PointF(cx - dx, cy - dy);
        _p2 = new PointF(cx + dx, cy + dy);
    }

    public RectangleF Rectangle { get; }

    public Color[] LinearColors
    {
        get => new[] { _c1, _c2 };
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length < 2) throw new ArgumentException("Two colours are required.", nameof(value));
            _c1 = value[0];
            _c2 = value[1];
        }
    }

    public Matrix Transform
    {
        get => _transform.Clone();
        set => _transform = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    public WrapMode WrapMode { get; set; } = WrapMode.Tile;

    public bool GammaCorrection { get; set; }

    public void ResetTransform() => _transform.Reset();

    public void TranslateTransform(float dx, float dy) => _transform.Translate(dx, dy);

    public void TranslateTransform(float dx, float dy, MatrixOrder order) => _transform.Translate(dx, dy, order);

    public void ScaleTransform(float sx, float sy) => _transform.Scale(sx, sy);

    public void RotateTransform(float angle) => _transform.Rotate(angle);

    public override object Clone() => new LinearGradientBrush(_p1, _p2, _c1, _c2) { _transform = _transform.Clone(), WrapMode = WrapMode };

    internal override void ApplyTo(SKPaint paint)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.Black;
        var tile = WrapMode switch
        {
            WrapMode.TileFlipX or WrapMode.TileFlipY or WrapMode.TileFlipXY => SKShaderTileMode.Mirror,
            WrapMode.Clamp => SKShaderTileMode.Clamp,
            _ => SKShaderTileMode.Repeat,
        };
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(_p1.X, _p1.Y), new SKPoint(_p2.X, _p2.Y),
            new[] { SkiaConvert.ToSK(_c1), SkiaConvert.ToSK(_c2) }, null, tile, _transform.Skia);
    }
}

public sealed class TextureBrush : Brush
{
    private Matrix _transform = new();

    public TextureBrush(Image image) : this(image, WrapMode.Tile) { }

    public TextureBrush(Image image, WrapMode wrapMode)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        WrapMode = wrapMode;
    }

    public TextureBrush(Image image, Rectangle dstRect) : this(image, WrapMode.Tile) { }

    public TextureBrush(Image image, WrapMode wrapMode, Rectangle dstRect) : this(image, wrapMode) { }

    public Image Image { get; }

    public WrapMode WrapMode { get; set; }

    public Matrix Transform
    {
        get => _transform.Clone();
        set => _transform = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    public void ResetTransform() => _transform.Reset();

    public void TranslateTransform(float dx, float dy) => _transform.Translate(dx, dy);

    public void ScaleTransform(float sx, float sy) => _transform.Scale(sx, sy);

    public void RotateTransform(float angle) => _transform.Rotate(angle);

    public override object Clone() => new TextureBrush(Image, WrapMode) { _transform = _transform.Clone() };

    internal override void ApplyTo(SKPaint paint)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.Black;
        var bmp = Image.SkBitmap ?? throw new ObjectDisposedException(nameof(Image));
        var (tx, ty) = WrapMode switch
        {
            WrapMode.TileFlipX => (SKShaderTileMode.Mirror, SKShaderTileMode.Repeat),
            WrapMode.TileFlipY => (SKShaderTileMode.Repeat, SKShaderTileMode.Mirror),
            WrapMode.TileFlipXY => (SKShaderTileMode.Mirror, SKShaderTileMode.Mirror),
            WrapMode.Clamp => (SKShaderTileMode.Decal, SKShaderTileMode.Decal),
            _ => (SKShaderTileMode.Repeat, SKShaderTileMode.Repeat),
        };
        paint.Shader = SKShader.CreateBitmap(bmp, tx, ty, _transform.Skia);
    }
}

/// <summary>GDI+ hatch patterns, rasterised as 8×8 tiles.</summary>
public sealed class HatchBrush : Brush
{
    private SKBitmap? _tile;

    public HatchBrush(HatchStyle hatchstyle, Color foreColor) : this(hatchstyle, foreColor, Color.Black) { }

    public HatchBrush(HatchStyle hatchstyle, Color foreColor, Color backColor)
    {
        HatchStyle = hatchstyle;
        ForegroundColor = foreColor;
        BackgroundColor = backColor;
    }

    public HatchStyle HatchStyle { get; }
    public Color ForegroundColor { get; }
    public Color BackgroundColor { get; }

    public override object Clone() => new HatchBrush(HatchStyle, ForegroundColor, BackgroundColor);

    internal override void ApplyTo(SKPaint paint)
    {
        paint.Style = SKPaintStyle.Fill;
        paint.Color = SKColors.Black;
        _tile ??= BuildTile();
        paint.Shader = SKShader.CreateBitmap(_tile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
    }

    private SKBitmap BuildTile()
    {
        var bmp = new SKBitmap(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul));
        var fg = SkiaConvert.ToSK(ForegroundColor);
        var bg = SkiaConvert.ToSK(BackgroundColor);
        for (int y = 0; y < 8; y++)
            for (int x = 0; x < 8; x++)
                bmp.SetPixel(x, y, IsForeground(x, y) ? fg : bg);
        return bmp;
    }

    private bool IsForeground(int x, int y) => HatchStyle switch
    {
        HatchStyle.Horizontal => y == 0,
        HatchStyle.Vertical => x == 0,
        HatchStyle.ForwardDiagonal => x == y,
        HatchStyle.BackwardDiagonal => x == 7 - y,
        HatchStyle.Cross => x == 0 || y == 0,
        HatchStyle.DiagonalCross => x == y || x == 7 - y,
        HatchStyle.Percent05 => x == 0 && y == 0,
        HatchStyle.Percent10 => (x + y) % 8 == 0 && (x % 4 == 0),
        HatchStyle.Percent20 => (x % 4 == 0 && y % 4 == 0) || (x % 4 == 2 && y % 4 == 2),
        HatchStyle.Percent25 => (x + y) % 4 == 0,
        HatchStyle.Percent30 => (x + y) % 3 == 0,
        HatchStyle.Percent40 => (x + y) % 5 < 2,
        HatchStyle.Percent50 => (x + y) % 2 == 0,
        HatchStyle.Percent60 => (x + y) % 5 >= 2,
        HatchStyle.Percent70 => (x + y) % 3 != 0,
        HatchStyle.Percent75 => (x + y) % 4 != 0,
        HatchStyle.Percent80 => !((x % 4 == 0 && y % 4 == 0) || (x % 4 == 2 && y % 4 == 2)),
        HatchStyle.Percent90 => !(x == 0 && y == 0),
        HatchStyle.LightDownwardDiagonal => (x + y) % 4 == 0,
        HatchStyle.LightUpwardDiagonal => (x - y + 8) % 4 == 0,
        HatchStyle.DarkDownwardDiagonal => (x + y) % 4 < 2,
        HatchStyle.DarkUpwardDiagonal => (x - y + 8) % 4 < 2,
        HatchStyle.WideDownwardDiagonal => (x + y) % 8 < 3,
        HatchStyle.WideUpwardDiagonal => (x - y + 8) % 8 < 3,
        HatchStyle.LightVertical => x % 4 == 0,
        HatchStyle.LightHorizontal => y % 4 == 0,
        HatchStyle.NarrowVertical => x % 2 == 0,
        HatchStyle.NarrowHorizontal => y % 2 == 0,
        HatchStyle.DarkVertical => x % 4 < 2,
        HatchStyle.DarkHorizontal => y % 4 < 2,
        HatchStyle.DashedHorizontal => y == 0 && x < 4 || y == 4 && x >= 4,
        HatchStyle.DashedVertical => x == 0 && y < 4 || x == 4 && y >= 4,
        HatchStyle.SmallGrid => x % 4 == 0 || y % 4 == 0,
        HatchStyle.SmallCheckerBoard => ((x / 2) + (y / 2)) % 2 == 0,
        HatchStyle.LargeCheckerBoard => ((x / 4) + (y / 4)) % 2 == 0,
        HatchStyle.DottedGrid => (x % 4 == 0 && y % 2 == 0) || (y % 4 == 0 && x % 2 == 0),
        HatchStyle.OutlinedDiamond => Math.Abs(x - 3.5) + Math.Abs(y - 3.5) is > 3 and < 4.5,
        HatchStyle.SolidDiamond => Math.Abs(x - 3.5) + Math.Abs(y - 3.5) < 4,
        _ => (x + y) % 2 == 0,
    };

    protected override void Dispose(bool disposing)
    {
        _tile?.Dispose();
        _tile = null;
        base.Dispose(disposing);
    }
}
