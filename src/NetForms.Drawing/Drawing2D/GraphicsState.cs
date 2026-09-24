using System.Drawing.Text;

namespace System.Drawing.Drawing2D;

/// <summary>What <see cref="Graphics.Save"/> captured, for <see cref="Graphics.Restore"/>.</summary>
public sealed class GraphicsState
{
    internal GraphicsState(int saveCount, SmoothingMode smoothing, TextRenderingHint textHint, InterpolationMode interpolation, PixelOffsetMode pixelOffset)
    {
        SaveCount = saveCount;
        Smoothing = smoothing;
        TextHint = textHint;
        Interpolation = interpolation;
        PixelOffset = pixelOffset;
    }

    internal int SaveCount { get; }
    internal SmoothingMode Smoothing { get; }
    internal TextRenderingHint TextHint { get; }
    internal InterpolationMode Interpolation { get; }
    internal PixelOffsetMode PixelOffset { get; }
}
