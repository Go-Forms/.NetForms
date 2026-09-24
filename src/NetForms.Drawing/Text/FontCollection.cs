using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace System.Drawing.Text;

public abstract class FontCollection : IDisposable
{
    public abstract FontFamily[] Families { get; }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing) { }
}

/// <summary>The fonts installed on the system, as Skia's font manager sees them.</summary>
public sealed class InstalledFontCollection : FontCollection
{
    public override FontFamily[] Families
    {
        get
        {
            var names = SKFontManager.Default.GetFontFamilies();
            var result = new FontFamily[names.Length];
            for (int i = 0; i < names.Length; i++) result[i] = new FontFamily(names[i]);
            return result;
        }
    }
}

/// <summary>
/// Fonts loaded from files or memory. Families added here take precedence over installed
/// ones of the same name for every Font created afterwards, which is how deterministic
/// golden tests get the same glyphs on every OS.
/// </summary>
public sealed class PrivateFontCollection : FontCollection
{
    private readonly List<FontFamily> _families = new();

    public override FontFamily[] Families => _families.ToArray();

    public void AddFontFile(string filename)
    {
        ArgumentNullException.ThrowIfNull(filename);
        filename = WindowsPath.ForReading(filename);
        if (!File.Exists(filename)) throw new FileNotFoundException("Font file not found.", filename);
        var typeface = SKTypeface.FromFile(filename) ?? throw new FileNotFoundException("Not a font file.", filename);
        Register(typeface);
    }

    public void AddMemoryFont(IntPtr memory, int length)
    {
        var bytes = new byte[length];
        System.Runtime.InteropServices.Marshal.Copy(memory, bytes, 0, length);
        using var data = SKData.CreateCopy(bytes);
        var typeface = SKTypeface.FromData(data) ?? throw new ArgumentException("Not a font.", nameof(memory));
        Register(typeface);
    }

    public void AddMemoryFont(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var typeface = SKTypeface.FromStream(stream) ?? throw new ArgumentException("Not a font.", nameof(stream));
        Register(typeface);
    }

    private void Register(SKTypeface typeface)
    {
        FontResolver.RegisterPrivate(typeface);
        foreach (var f in _families)
        {
            if (string.Equals(f.Name, typeface.FamilyName, StringComparison.OrdinalIgnoreCase)) return;
        }
        _families.Add(new FontFamily(typeface.FamilyName));
    }
}
