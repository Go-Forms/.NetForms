using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// ImageIndex in the property grid: an integer, or "(none)" for -1, with the indices of the owner's
/// ImageList as the standard values.
/// </summary>
public class ImageIndexConverter : Int32Converter
{
    protected virtual bool IncludeNoneAsStandardValue => true;

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string s && string.Compare(s, "(none)", true, culture) == 0) return -1;
        return base.ConvertFrom(context, culture, value);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is int i && i == -1) return "(none)";
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = new List<object>();
        if (context?.Instance != null && TypeDescriptor.GetProperties(context.Instance)["ImageList"]?.GetValue(context.Instance) is ImageList list)
            for (int i = 0; i < list.Images.Count; i++) values.Add(i);
        if (IncludeNoneAsStandardValue) values.Add(-1);
        return new StandardValuesCollection(values);
    }

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
}

/// <summary>ImageKey in the property grid: a string, or "(none)" for the empty key, with the ImageList's keys as standard values.</summary>
public class ImageKeyConverter : StringConverter
{
    protected virtual bool IncludeNoneAsStandardValue => true;

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string s ? (s == "(none)" ? string.Empty : s) : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is string s && s.Length == 0) return "(none)";
        return base.ConvertTo(context, culture, value, destinationType);
    }

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var values = new List<object>();
        if (context?.Instance != null && TypeDescriptor.GetProperties(context.Instance)["ImageList"]?.GetValue(context.Instance) is ImageList list)
            foreach (string? key in list.Images.Keys) if (!string.IsNullOrEmpty(key)) values.Add(key);
        if (IncludeNoneAsStandardValue) values.Add(string.Empty);
        return new StandardValuesCollection(values);
    }

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;
}
