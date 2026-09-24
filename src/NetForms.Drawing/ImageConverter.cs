using System.ComponentModel;
using System.Globalization;
using System.IO;

namespace System.Drawing;

/// <summary>
/// Converts an <see cref="Image"/> to and from the bytes of an image file - the form a .resx keeps it in
/// (<c>type="System.Drawing.Bitmap, System.Drawing" mimetype="application/x-microsoft.net.object.bytearray.base64"</c>),
/// read back by <c>ComponentResourceManager.GetObject</c>. Bytes are written as PNG.
/// </summary>
public class ImageConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(byte[]) || sourceType == typeof(Icon) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(byte[]) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => value switch
    {
        byte[] bytes => Image.FromStream(new MemoryStream(bytes)),
        Icon icon => icon.ToBitmap(),
        _ => base.ConvertFrom(context, culture, value),
    };

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value == null) return "(none)";
        if (destinationType == typeof(string) && value is Image) return value.ToString();
        if (destinationType == typeof(byte[]) && value is Image image)
        {
            using var stream = new MemoryStream();
            image.Save(stream, Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>Converts an <see cref="Icon"/> to and from the bytes of an .ico (or .png) file, as a .resx keeps a form's icon.</summary>
public class IconConverter : ExpandableObjectConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(byte[]) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(byte[]) || destinationType == typeof(Image) || destinationType == typeof(Bitmap) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is byte[] bytes ? new Icon(new MemoryStream(bytes)) : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value == null) return "(none)";
        if (value is Icon icon)
        {
            if (destinationType == typeof(Image) || destinationType == typeof(Bitmap)) return icon.ToBitmap();
            if (destinationType == typeof(byte[]))
            {
                using var stream = new MemoryStream();
                icon.Save(stream);
                return stream.ToArray();
            }
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
