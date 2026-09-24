using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;

namespace System.Drawing;

/// <summary>
/// Converts a <see cref="Font"/> to and from its text form ("Segoe UI, 9pt, style=Bold") and to an
/// <see cref="InstanceDescriptor"/>, so the designer code writer emits <c>new Font("Segoe UI", 9F, FontStyle.Bold)</c>.
/// Same formats as System.Drawing's FontConverter: the descriptor uses the shortest constructor that
/// keeps the value (charset 1 = DEFAULT_CHARSET and point units are the defaults).
/// </summary>
public class FontConverter : TypeConverter
{
    private const byte DefaultCharSet = 1;

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Font font)
        {
            if (destinationType == typeof(string))
            {
                culture ??= CultureInfo.CurrentCulture;
                var separator = culture.TextInfo.ListSeparator + " ";
                var text = font.Name + separator + font.Size.ToString(culture) + UnitText(font.Unit);
                if (font.Style != FontStyle.Regular) text += separator + "style=" + font.Style.ToString("G");
                return text;
            }
            if (destinationType == typeof(InstanceDescriptor))
            {
                return Describe(font);
            }
        }
        return base.ConvertTo(context, culture, value, destinationType);
    }

    private static InstanceDescriptor Describe(Font font)
    {
        var all = new object[] { font.Name, font.Size, font.Style, font.Unit, font.GdiCharSet, font.GdiVerticalFont };
        int count = font.GdiVerticalFont ? 6
            : font.GdiCharSet != DefaultCharSet ? 5
            : font.Unit != GraphicsUnit.Point ? 4
            : font.Style != FontStyle.Regular ? 3
            : 2;
        var types = new[] { typeof(string), typeof(float), typeof(FontStyle), typeof(GraphicsUnit), typeof(byte), typeof(bool) }[..count];
        var ctor = typeof(Font).GetConstructor(types)!;
        return new InstanceDescriptor(ctor, all[..count]);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);
        text = text.Trim();
        if (text.Length == 0) return null;
        culture ??= CultureInfo.CurrentCulture;

        var separator = culture.TextInfo.ListSeparator[0];
        var parts = text.Split(separator);
        var name = parts[0].Trim();
        float size = 8.25f;
        var unit = GraphicsUnit.Point;
        var style = FontStyle.Regular;
        for (int i = 1; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (part.StartsWith("style=", StringComparison.OrdinalIgnoreCase))
            {
                style = (FontStyle)Enum.Parse(typeof(FontStyle), part.Substring(6), ignoreCase: true);
                continue;
            }
            int digits = 0;
            while (digits < part.Length && (char.IsDigit(part[digits]) || part[digits] == '.' || part[digits] == ',')) digits++;
            if (digits == 0) throw new ArgumentException($"'{text}' is not a valid font: '{part}' is neither a size nor a style.");
            size = float.Parse(part.Substring(0, digits), NumberStyles.Float, culture);
            var suffix = part.Substring(digits).Trim();
            if (suffix.Length > 0) unit = ParseUnit(suffix);
        }
        return new Font(name, size, style, unit);
    }

    private static string UnitText(GraphicsUnit unit) => unit switch
    {
        GraphicsUnit.World => "world",
        GraphicsUnit.Display => "display",
        GraphicsUnit.Pixel => "px",
        GraphicsUnit.Point => "pt",
        GraphicsUnit.Inch => "in",
        GraphicsUnit.Document => "doc",
        GraphicsUnit.Millimeter => "mm",
        _ => "",
    };

    private static GraphicsUnit ParseUnit(string text)
    {
        foreach (GraphicsUnit unit in Enum.GetValues(typeof(GraphicsUnit)))
            if (string.Equals(UnitText(unit), text, StringComparison.OrdinalIgnoreCase)) return unit;
        throw new ArgumentException($"'{text}' is not a font unit.");
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => false;
}
