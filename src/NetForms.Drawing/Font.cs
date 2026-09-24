using System;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// System.Drawing.Font on Skia. Sizes are stored as given (with their unit) and converted
/// to device-independent pixels at 96 DPI, so 9pt is 12px on every platform.
/// </summary>
[System.ComponentModel.TypeConverter(typeof(FontConverter))]
public sealed class Font : ICloneable, IDisposable
{
    private SKFont? _skFont;

    public Font(string familyName, float emSize) : this(familyName, emSize, FontStyle.Regular, GraphicsUnit.Point) { }

    public Font(string familyName, float emSize, FontStyle style) : this(familyName, emSize, style, GraphicsUnit.Point) { }

    public Font(string familyName, float emSize, GraphicsUnit unit) : this(familyName, emSize, FontStyle.Regular, unit) { }

    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit)
        : this(familyName, emSize, style, unit, 1, false) { }

    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet)
        : this(familyName, emSize, style, unit, gdiCharSet, false) { }

    public Font(string familyName, float emSize, FontStyle style, GraphicsUnit unit, byte gdiCharSet, bool gdiVerticalFont)
    {
        ArgumentNullException.ThrowIfNull(familyName);
        if (emSize <= 0 || float.IsNaN(emSize) || float.IsInfinity(emSize))
            throw new ArgumentException("Font size must be greater than zero.", nameof(emSize));
        Name = familyName;
        Size = emSize;
        Style = style;
        Unit = unit;
        GdiCharSet = gdiCharSet;
        GdiVerticalFont = gdiVerticalFont;
        FontFamily = new FontFamily(familyName);
    }

    public Font(FontFamily family, float emSize) : this(family, emSize, FontStyle.Regular, GraphicsUnit.Point) { }

    public Font(FontFamily family, float emSize, FontStyle style) : this(family, emSize, style, GraphicsUnit.Point) { }

    public Font(FontFamily family, float emSize, GraphicsUnit unit) : this(family, emSize, FontStyle.Regular, unit) { }

    public Font(FontFamily family, float emSize, FontStyle style, GraphicsUnit unit)
        : this((family ?? throw new ArgumentNullException(nameof(family))).Name, emSize, style, unit) { }

    public Font(Font prototype, FontStyle newStyle)
        : this((prototype ?? throw new ArgumentNullException(nameof(prototype))).Name, prototype.Size, newStyle, prototype.Unit, prototype.GdiCharSet, prototype.GdiVerticalFont) { }

    public string Name { get; }
    public FontFamily FontFamily { get; }
    public float Size { get; }
    public FontStyle Style { get; }
    public GraphicsUnit Unit { get; }
    public byte GdiCharSet { get; }
    public bool GdiVerticalFont { get; }
    public string OriginalFontName => Name;
    public string SystemFontName => string.Empty;
    public bool IsSystemFont => false;

    public bool Bold => Style.HasFlag(FontStyle.Bold);
    public bool Italic => Style.HasFlag(FontStyle.Italic);
    public bool Underline => Style.HasFlag(FontStyle.Underline);
    public bool Strikeout => Style.HasFlag(FontStyle.Strikeout);

    public float SizeInPoints => Unit switch
    {
        GraphicsUnit.Point => Size,
        GraphicsUnit.Pixel => Size * 72f / 96f,
        GraphicsUnit.Inch => Size * 72f,
        GraphicsUnit.Millimeter => Size * 72f / 25.4f,
        GraphicsUnit.Document => Size * 72f / 300f,
        _ => Size,
    };

    /// <summary>Em size in device-independent pixels (96 DPI).</summary>
    internal float SizeInPixels => SizeInPoints * 96f / 72f;

    /// <summary>Line spacing in pixels, rounded up, as <c>Font.Height</c> reports.</summary>
    public int Height => (int)Math.Ceiling(GetHeight());

    public float GetHeight()
    {
        var m = SKFont.Metrics;
        return m.Descent - m.Ascent + m.Leading;
    }

    public float GetHeight(Graphics graphics) => GetHeight();

    public float GetHeight(float dpi) => GetHeight() * dpi / 96f;

    internal SKTypeface Typeface => SKFont.Typeface;

    /// <summary>The Skia font used for measuring and drawing; owned by this Font.</summary>
    internal SKFont SKFont
    {
        get
        {
            if (_skFont == null)
            {
                var tf = FontResolver.Resolve(Name, Style);
                _skFont = new SKFont(tf, SizeInPixels)
                {
                    Subpixel = true,
                    Edging = SKFontEdging.SubpixelAntialias,
                    // Hermetic (golden tests): no hinting and unhinted advances, straight from the
                    // font file - FreeType and DirectWrite hint differently.
                    Hinting = HermeticRendering.OutlineText ? SKFontHinting.None : SKFontHinting.Normal,
                    LinearMetrics = HermeticRendering.OutlineText,
                };
                // Synthesize bold/italic when the family has no such face.
                if (Bold && !tf.IsBold) _skFont.Embolden = true;
                if (Italic && !tf.IsItalic) _skFont.SkewX = -0.25f;
            }
            return _skFont;
        }
    }

    public object Clone() => new Font(Name, Size, Style, Unit, GdiCharSet, GdiVerticalFont);

    public void Dispose()
    {
        _skFont?.Dispose();
        _skFont = null;
        GC.SuppressFinalize(this);
    }

    public override bool Equals(object? obj) =>
        obj is Font f && string.Equals(f.Name, Name, StringComparison.OrdinalIgnoreCase) && f.Size == Size && f.Style == Style && f.Unit == Unit;

    public override int GetHashCode() => HashCode.Combine(Name.ToLowerInvariant(), Size, Style, Unit);

    public override string ToString() => $"[Font: Name={Name}, Size={Size}, Units={(int)Unit}, GdiCharSet={GdiCharSet}, GdiVerticalFont={GdiVerticalFont}]";
}
