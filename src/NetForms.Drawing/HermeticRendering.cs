using System.Collections.Generic;

namespace System.Drawing;

/// <summary>
/// Switches that make rendering independent of the machine, for golden images that must come out the
/// same on Windows and Linux (docs/PLAN.md, Ф6). Off by default: an application draws with the OS
/// palette and the OS glyph rasterizer, as WinForms does.
/// </summary>
internal static class HermeticRendering
{
    /// <summary>
    /// System colors (<see cref="SystemColors.Control"/>…) are painted from this palette instead of the
    /// OS's. Null: the OS (what <see cref="Color.ToArgb"/> says).
    /// </summary>
    public static IReadOnlyDictionary<KnownColor, Color>? Palette { get; set; }

    /// <summary>
    /// Text is drawn as glyph outlines filled by Skia's own rasterizer. Glyph drawing proper goes
    /// through DirectWrite on Windows and FreeType on Linux, whose antialiasing differs - and changes
    /// with OS updates; the outlines come from the font file alone.
    /// </summary>
    public static bool OutlineText { get; set; }

    /// <summary>
    /// Font files that stand in for a family the machine does not have ("Segoe UI" off Windows), instead of
    /// whatever the OS has installed (Noto Sans on one Linux, DejaVu on another, with other line heights).
    /// Regular first, then bold. Null: the OS fallback chain.
    /// </summary>
    public static string[]? FallbackFontFiles { get; set; }

    /// <summary>The color that is painted for <paramref name="color"/>: a system color through the palette.</summary>
    public static Color Resolve(Color color) =>
        color.IsSystemColor && Palette != null && Palette.TryGetValue(color.ToKnownColor(), out var fixedColor) ? fixedColor : color;

    /// <summary>
    /// The default light palette of Windows 10 and 11, as GetSysColor reports it - what a WinForms user
    /// sees out of the box. (The BCL's own table off Windows is the Windows XP one: Control #ECE9D8.)
    /// </summary>
    public static readonly IReadOnlyDictionary<KnownColor, Color> WindowsDefaultPalette = new Dictionary<KnownColor, Color>
    {
        [KnownColor.ActiveBorder] = Color.FromArgb(unchecked((int)0xFFB4B4B4)),
        [KnownColor.ActiveCaption] = Color.FromArgb(unchecked((int)0xFF99B4D1)),
        [KnownColor.ActiveCaptionText] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.AppWorkspace] = Color.FromArgb(unchecked((int)0xFFABABAB)),
        [KnownColor.Control] = Color.FromArgb(unchecked((int)0xFFF0F0F0)),
        [KnownColor.ControlDark] = Color.FromArgb(unchecked((int)0xFFA0A0A0)),
        [KnownColor.ControlDarkDark] = Color.FromArgb(unchecked((int)0xFF696969)),
        [KnownColor.ControlLight] = Color.FromArgb(unchecked((int)0xFFE3E3E3)),
        [KnownColor.ControlLightLight] = Color.FromArgb(unchecked((int)0xFFFFFFFF)),
        [KnownColor.ControlText] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.Desktop] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.GrayText] = Color.FromArgb(unchecked((int)0xFF6D6D6D)),
        [KnownColor.Highlight] = Color.FromArgb(unchecked((int)0xFF0078D7)),
        [KnownColor.HighlightText] = Color.FromArgb(unchecked((int)0xFFFFFFFF)),
        [KnownColor.HotTrack] = Color.FromArgb(unchecked((int)0xFF0066CC)),
        [KnownColor.InactiveBorder] = Color.FromArgb(unchecked((int)0xFFF4F7FC)),
        [KnownColor.InactiveCaption] = Color.FromArgb(unchecked((int)0xFFBFCDDB)),
        [KnownColor.InactiveCaptionText] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.Info] = Color.FromArgb(unchecked((int)0xFFFFFFE1)),
        [KnownColor.InfoText] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.Menu] = Color.FromArgb(unchecked((int)0xFFF0F0F0)),
        [KnownColor.MenuText] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.ScrollBar] = Color.FromArgb(unchecked((int)0xFFC8C8C8)),
        [KnownColor.Window] = Color.FromArgb(unchecked((int)0xFFFFFFFF)),
        [KnownColor.WindowFrame] = Color.FromArgb(unchecked((int)0xFF646464)),
        [KnownColor.WindowText] = Color.FromArgb(unchecked((int)0xFF000000)),
        [KnownColor.ButtonFace] = Color.FromArgb(unchecked((int)0xFFF0F0F0)),
        [KnownColor.ButtonHighlight] = Color.FromArgb(unchecked((int)0xFFFFFFFF)),
        [KnownColor.ButtonShadow] = Color.FromArgb(unchecked((int)0xFFA0A0A0)),
        [KnownColor.GradientActiveCaption] = Color.FromArgb(unchecked((int)0xFFB9D1EA)),
        [KnownColor.GradientInactiveCaption] = Color.FromArgb(unchecked((int)0xFFD7E4F2)),
        [KnownColor.MenuBar] = Color.FromArgb(unchecked((int)0xFFF0F0F0)),
        [KnownColor.MenuHighlight] = Color.FromArgb(unchecked((int)0xFF0078D7)),
    };
}
