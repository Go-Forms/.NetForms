using System.Drawing.Drawing2D;
using SkiaSharp;

namespace System.Drawing;

/// <summary>Fills with a tiled image. In <c>System.Drawing</c>, like <see cref="SolidBrush"/>; its companions
/// <see cref="LinearGradientBrush"/> and <see cref="HatchBrush"/> live in <c>System.Drawing.Drawing2D</c>.</summary>
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
