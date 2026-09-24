using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace System.Windows.Forms;

/// <summary>
/// Cursors by the name of their <see cref="Cursors"/> property ("Hand", "WaitCursor"), as WinForms'
/// CursorConverter shows them in the property grid; the code form is <c>Cursors.Hand</c>.
/// </summary>
public class CursorConverter : TypeConverter
{
    private static readonly Lazy<(string Name, PropertyInfo Property, Cursor Cursor)[]> s_known = new(() =>
        typeof(Cursors).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(Cursor))
            .Select(p => (p.Name, p, (Cursor)p.GetValue(null)!))
            .ToArray());

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            text = text.Trim();
            foreach (var (name, _, cursor) in s_known.Value)
                if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase)) return cursor;
            throw new ArgumentException($"'{text}' is not a cursor. Use one of: {string.Join(", ", s_known.Value.Select(k => k.Name))}.");
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is Cursor cursor)
        {
            foreach (var (name, property, known) in s_known.Value)
            {
                if (!known.Equals(cursor)) continue;
                if (destinationType == typeof(string)) return name;
                if (destinationType == typeof(InstanceDescriptor)) return new InstanceDescriptor(property, null);
            }
            if (destinationType == typeof(string)) return cursor.ToString();
        }
        if (value == null && destinationType == typeof(string)) return "(none)";
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new(s_known.Value.Select(k => k.Cursor).ToArray());
}
