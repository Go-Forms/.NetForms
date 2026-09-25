// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Drawing.Printing;

/// <summary>Converts <see cref="Margins"/> to and from "left, right, top, bottom" (the culture's list separator).</summary>
public class MarginsConverter : ExpandableObjectConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, [NotNullWhen(true)] Type? destinationType)
        => destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string strValue)
        {
            return base.ConvertFrom(context, culture, value);
        }

        string text = strValue.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        culture ??= CultureInfo.CurrentCulture;
        char sep = culture.TextInfo.ListSeparator[0];
        string[] tokens = text.Split(sep);
        int[] values = new int[tokens.Length];
        TypeConverter intConverter = TypeDescriptor.GetConverter(typeof(int));
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = (int)intConverter.ConvertFromString(context, culture, tokens[i])!;
        }

        if (values.Length != 4)
        {
            throw new ArgumentException($"Text \"{text}\" cannot be parsed. The expected text format is \"left, right, top, bottom\".");
        }

        return new Margins(values[0], values[1], values[2], values[3]);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(destinationType);

        if (value is Margins margins)
        {
            if (destinationType == typeof(string))
            {
                culture ??= CultureInfo.CurrentCulture;
                string sep = culture.TextInfo.ListSeparator + " ";
                TypeConverter intConverter = TypeDescriptor.GetConverter(typeof(int));
                string?[] args =
                [
                    intConverter.ConvertToString(context, culture, margins.Left),
                    intConverter.ConvertToString(context, culture, margins.Right),
                    intConverter.ConvertToString(context, culture, margins.Top),
                    intConverter.ConvertToString(context, culture, margins.Bottom),
                ];
                return string.Join(sep, args);
            }

            if (destinationType == typeof(InstanceDescriptor)
                && typeof(Margins).GetConstructor([typeof(int), typeof(int), typeof(int), typeof(int)]) is { } constructor)
            {
                return new InstanceDescriptor(constructor, new object[] { margins.Left, margins.Right, margins.Top, margins.Bottom });
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary propertyValues)
    {
        ArgumentNullException.ThrowIfNull(propertyValues);

        object? left = propertyValues["Left"];
        object? right = propertyValues["Right"];
        object? top = propertyValues["Top"];
        object? bottom = propertyValues["Bottom"];

        return left is not int || right is not int || bottom is not int || top is not int
            ? throw new ArgumentException("Invalid property value entry.")
            : new Margins((int)left, (int)right, (int)top, (int)bottom);
    }
}
