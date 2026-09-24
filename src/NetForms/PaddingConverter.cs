using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Reflection;

namespace System.Windows.Forms;

/// <summary>
/// Makes <see cref="Padding"/> editable and serializable the way the designer needs: a property
/// grid row that reads "3, 3, 3, 3" and expands into All/Left/Top/Right/Bottom, and an
/// <see cref="InstanceDescriptor"/> so the code writer can emit <c>new Padding(3)</c>.
///
/// Without it a Padding shows up as its raw ToString ("{Left=3,Top=3,...}"), cannot be typed into,
/// and cannot be written back to <c>InitializeComponent</c>.
/// </summary>
public class PaddingConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);

        text = text.Trim();
        if (text.Length == 0) return null;

        culture ??= CultureInfo.CurrentCulture;
        var separator = culture.TextInfo.ListSeparator[0];
        var parts = text.Split(separator);
        var numbers = new int[parts.Length];
        var intConverter = TypeDescriptor.GetConverter(typeof(int));
        for (int i = 0; i < parts.Length; i++)
        {
            numbers[i] = (int)intConverter.ConvertFromString(context, culture, parts[i])!;
        }

        return numbers.Length switch
        {
            1 => new Padding(numbers[0]),
            4 => new Padding(numbers[0], numbers[1], numbers[2], numbers[3]),
            _ => throw new ArgumentException($"'{text}' is not a valid value for Padding: expected 'all' or 'left, top, right, bottom'."),
        };
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        ArgumentNullException.ThrowIfNull(destinationType);
        if (value is not Padding padding) return base.ConvertTo(context, culture, value, destinationType);

        if (destinationType == typeof(string))
        {
            culture ??= CultureInfo.CurrentCulture;
            var separator = culture.TextInfo.ListSeparator + " ";
            var intConverter = TypeDescriptor.GetConverter(typeof(int));
            return string.Join(separator,
                intConverter.ConvertToString(context, culture, padding.Left),
                intConverter.ConvertToString(context, culture, padding.Top),
                intConverter.ConvertToString(context, culture, padding.Right),
                intConverter.ConvertToString(context, culture, padding.Bottom));
        }

        if (destinationType == typeof(InstanceDescriptor))
        {
            // A uniform padding round-trips through the single-argument constructor, which is what
            // keeps generated code reading `new Padding(3)` rather than `new Padding(3, 3, 3, 3)`.
            if (padding.ShouldSerializeAll())
            {
                return new InstanceDescriptor(
                    typeof(Padding).GetConstructor(new[] { typeof(int) }),
                    new object[] { padding.All });
            }
            return new InstanceDescriptor(
                typeof(Padding).GetConstructor(new[] { typeof(int), typeof(int), typeof(int), typeof(int) }),
                new object[] { padding.Left, padding.Top, padding.Right, padding.Bottom });
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override object CreateInstance(ITypeDescriptorContext? context, IDictionary propertyValues)
    {
        ArgumentNullException.ThrowIfNull(propertyValues);

        var original = context?.PropertyDescriptor?.GetValue(context.Instance) as Padding? ?? Padding.Empty;
        var all = (int)(propertyValues[nameof(Padding.All)] ?? original.All);

        // "All" wins only when the user actually changed it; otherwise the four edges do.
        if (original.All != all) return new Padding(all);

        return new Padding(
            (int)(propertyValues[nameof(Padding.Left)] ?? original.Left),
            (int)(propertyValues[nameof(Padding.Top)] ?? original.Top),
            (int)(propertyValues[nameof(Padding.Right)] ?? original.Right),
            (int)(propertyValues[nameof(Padding.Bottom)] ?? original.Bottom));
    }

    public override bool GetCreateInstanceSupported(ITypeDescriptorContext? context) => true;

    public override PropertyDescriptorCollection GetProperties(ITypeDescriptorContext? context, object value, Attribute[]? attributes)
    {
        var properties = TypeDescriptor.GetProperties(typeof(Padding), attributes);
        return properties.Sort(new[] { nameof(Padding.All), nameof(Padding.Left), nameof(Padding.Top), nameof(Padding.Right), nameof(Padding.Bottom) });
    }

    public override bool GetPropertiesSupported(ITypeDescriptorContext? context) => true;
}
