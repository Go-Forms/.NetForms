using System;
using SkiaSharp;

namespace System.Drawing;

public sealed class FontFamily : IDisposable
{
    public FontFamily(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    public FontFamily(Text.GenericFontFamilies genericFamily)
    {
        Name = genericFamily switch
        {
            Text.GenericFontFamilies.Serif => "Times New Roman",
            Text.GenericFontFamilies.Monospace => "Courier New",
            _ => "Microsoft Sans Serif",
        };
    }

    public string Name { get; }

    public static FontFamily GenericSansSerif => new FontFamily(Text.GenericFontFamilies.SansSerif);
    public static FontFamily GenericSerif => new FontFamily(Text.GenericFontFamilies.Serif);
    public static FontFamily GenericMonospace => new FontFamily(Text.GenericFontFamilies.Monospace);

    /// <summary>Every font family installed on the machine.</summary>
    public static FontFamily[] Families => new Text.InstalledFontCollection().Families;

    /// <summary>The same list; GDI+ keeps both spellings.</summary>
    public static FontFamily[] GetFamilies(Graphics? graphics) => Families;

    public bool IsStyleAvailable(FontStyle style) => true;

    /// <summary>Line spacing in font design units for 2048 units per em, as GDI+ reports.</summary>
    public int GetLineSpacing(FontStyle style)
    {
        using var f = new SKFont(FontResolver.Resolve(Name, style), 2048f);
        var m = f.Metrics;
        return (int)Math.Round(m.Descent - m.Ascent + m.Leading);
    }

    public int GetEmHeight(FontStyle style) => 2048;

    public int GetCellAscent(FontStyle style)
    {
        using var f = new SKFont(FontResolver.Resolve(Name, style), 2048f);
        return (int)Math.Round(-f.Metrics.Ascent);
    }

    public int GetCellDescent(FontStyle style)
    {
        using var f = new SKFont(FontResolver.Resolve(Name, style), 2048f);
        return (int)Math.Round(f.Metrics.Descent);
    }

    public string GetName(int language) => Name;

    public void Dispose() { }

    public override string ToString() => $"[FontFamily: Name={Name}]";

    public override bool Equals(object? obj) => obj is FontFamily other && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => Name.ToLowerInvariant().GetHashCode();
}
