using System;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// Maps a WinForms family name to an installed typeface. Windows names that are absent on
/// Linux ("Segoe UI", "Microsoft Sans Serif", "Tahoma") fall back through a list of
/// metric-compatible or at least similar sans-serif faces, then to Skia's default.
/// </summary>
internal static class FontResolver
{
    private static readonly Dictionary<(string, FontStyle), SKTypeface> s_cache = new();
    private static readonly List<SKTypeface> s_private = new();

    private static readonly string[] s_sansFallbacks =
    {
        "Segoe UI", "Noto Sans", "Open Sans", "Cantarell", "Ubuntu", "DejaVu Sans", "Liberation Sans", "Arial", "Helvetica",
    };

    private static readonly string[] s_serifFallbacks =
    {
        "Times New Roman", "Liberation Serif", "DejaVu Serif", "Noto Serif",
    };

    private static readonly string[] s_monoFallbacks =
    {
        "Consolas", "Courier New", "DejaVu Sans Mono", "Liberation Mono", "Noto Sans Mono",
    };

    /// <summary>Make a typeface loaded by <see cref="Text.PrivateFontCollection"/> resolvable by its family name.</summary>
    public static void RegisterPrivate(SKTypeface typeface)
    {
        lock (s_cache)
        {
            s_private.Add(typeface);
            // Forget cached resolutions for that family so the private face wins from now on.
            var stale = new List<(string, FontStyle)>();
            foreach (var k in s_cache.Keys)
            {
                if (string.Equals(k.Item1, typeface.FamilyName, StringComparison.OrdinalIgnoreCase)) stale.Add(k);
            }
            foreach (var k in stale) s_cache.Remove(k);
        }
    }

    private static SKTypeface? MatchPrivate(string family, FontStyle style)
    {
        SKTypeface? best = null;
        int bestScore = -1;
        foreach (var tf in s_private)
        {
            if (!string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase)) continue;
            int score = (tf.IsBold == style.HasFlag(FontStyle.Bold) ? 2 : 0) + (tf.IsItalic == style.HasFlag(FontStyle.Italic) ? 1 : 0);
            if (score > bestScore)
            {
                best = tf;
                bestScore = score;
            }
        }
        return best;
    }

    public static SKTypeface Resolve(string family, FontStyle style)
    {
        var key = (family, style & (FontStyle.Bold | FontStyle.Italic));
        lock (s_cache)
        {
            if (s_cache.TryGetValue(key, out var cached)) return cached;
            var priv = MatchPrivate(family, style);
            if (priv != null)
            {
                s_cache[key] = priv;
                return priv;
            }
        }

        var skStyle = new SKFontStyle(
            style.HasFlag(FontStyle.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
            SKFontStyleWidth.Normal,
            style.HasFlag(FontStyle.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        SKTypeface? tf = Match(family, skStyle);
        if (tf == null && HermeticRendering.FallbackFontFiles is { Length: > 0 } files)
        {
            // Hermetic: the bundled face, not the machine's choice.
            var file = style.HasFlag(FontStyle.Bold) && files.Length > 1 ? files[1] : files[0];
            tf = SKTypeface.FromFile(file);
        }
        if (tf == null)
        {
            foreach (var candidate in FallbacksFor(family))
            {
                tf = Match(candidate, skStyle);
                if (tf != null) break;
            }
        }
        tf ??= SKFontManager.Default.MatchFamily(SKTypeface.Default.FamilyName, skStyle) ?? SKTypeface.Default;

        lock (s_cache)
        {
            s_cache[key] = tf;
        }
        return tf;
    }

    private static SKTypeface? Match(string family, SKFontStyle style)
    {
        // MatchFamily returns the default face for unknown families on some platforms,
        // so check the name it actually resolved to.
        var tf = SKFontManager.Default.MatchFamily(family, style);
        if (tf == null) return null;
        if (!string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase))
        {
            tf.Dispose();
            return null;
        }
        return tf;
    }

    private static string[] FallbacksFor(string family)
    {
        var f = family.ToLowerInvariant();
        if (f.Contains("mono") || f.Contains("courier") || f.Contains("consolas") || f.Contains("lucida console"))
            return s_monoFallbacks;
        if (f.Contains("serif") && !f.Contains("sans") || f.Contains("times") || f.Contains("georgia") || f.Contains("cambria"))
            return s_serifFallbacks;
        return s_sansFallbacks;
    }
}
