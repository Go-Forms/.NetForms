using System;
using System.IO;
using System.Linq;

namespace System.Drawing;

/// <summary>The image a designer's toolbox shows for a component: a file, or a .bmp/.png embedded next to the type.</summary>
[AttributeUsage(AttributeTargets.Class)]
public class ToolboxBitmapAttribute : Attribute
{
    private readonly string? _imageFile;
    private readonly Type? _imageType;
    private readonly string? _imageName;

    public static readonly ToolboxBitmapAttribute Default = new((string?)null);

    public ToolboxBitmapAttribute(string? imageFile) => _imageFile = imageFile;

    public ToolboxBitmapAttribute(Type t) => _imageType = t;

    public ToolboxBitmapAttribute(Type t, string name)
    {
        _imageType = t;
        _imageName = name;
    }

    public Image? GetImage(object? component) => GetImage(component, true);

    public Image? GetImage(object? component, bool large) => component == null ? null : GetImage(component.GetType(), large);

    public Image? GetImage(Type type) => GetImage(type, false);

    public Image? GetImage(Type type, bool large) => GetImage(type, null, large);

    public Image? GetImage(Type type, string? imgName, bool large)
    {
        if (_imageFile != null && File.Exists(WindowsPath.ForReading(_imageFile))) return Image.FromFile(_imageFile);
        var t = _imageType ?? type;
        return GetImageFromResource(t, imgName ?? _imageName ?? t.Name + ".bmp", large);
    }

    public static Image? GetImageFromResource(Type t, string? imageName, bool large)
    {
        ArgumentNullException.ThrowIfNull(t);
        var name = imageName ?? t.Name + ".bmp";
        var asm = t.Module.Assembly;
        var resource = asm.GetManifestResourceNames().FirstOrDefault(r => r == (t.Namespace + "." + name) || r.EndsWith("." + name, StringComparison.OrdinalIgnoreCase));
        if (resource == null) return null;
        using var stream = asm.GetManifestResourceStream(resource);
        return stream == null ? null : new Bitmap(stream);
    }

    public override bool Equals(object? value) =>
        value is ToolboxBitmapAttribute other && other._imageFile == _imageFile && other._imageType == _imageType && other._imageName == _imageName;

    public override int GetHashCode() => HashCode.Combine(_imageFile, _imageType, _imageName);
}
