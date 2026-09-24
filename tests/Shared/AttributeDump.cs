using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace NetForms.Compat;

/// <summary>
/// Dumps the design-time metadata (<see cref="CategoryAttribute"/>, <see cref="DescriptionAttribute"/>,
/// <see cref="DefaultValueAttribute"/>, <see cref="BrowsableAttribute"/>,
/// <see cref="DesignerSerializationVisibilityAttribute"/>) of every component in
/// <c>System.Windows.Forms</c>, as <see cref="TypeDescriptor"/> - and therefore the designer -
/// sees it.
///
/// The file is compiled into both <c>NetForms.Compat</c> (the REAL System.Windows.Forms, Windows
/// only) and the NetForms test suite, so the two dumps can be diffed key by key. That diff is the
/// gate of phase 5.0: our controls must describe themselves to a designer exactly as WinForms
/// controls do.
/// </summary>
public static class AttributeDump
{
    public sealed class MemberDump
    {
        public string Kind { get; set; } = "property";
        public string Type { get; set; } = "";
        public string DeclaredBy { get; set; } = "";
        public string Category { get; set; } = "";
        public bool Browsable { get; set; } = true;
        public bool ReadOnly { get; set; }
        public bool Localizable { get; set; }
        public string Serialization { get; set; } = "Visible";
        public bool HasDefaultValue { get; set; }
        public string? DefaultValue { get; set; }

        /// <summary>
        /// What the property actually reads back on a freshly constructed instance ("!" plus the
        /// exception name when the getter throws, absent when the type cannot be constructed).
        /// This is what makes a declared default checkable, and it catches plain behavioural
        /// differences - a Label whose TabStop starts out true.
        /// </summary>
        public string? Actual { get; set; }
        public bool HasActual { get; set; }

        /// <summary>
        /// <see cref="PropertyDescriptor.ShouldSerializeValue"/> on a freshly constructed instance
        /// (absent when the type cannot be constructed or the property is not serialized at all).
        /// A fresh control must not ask for any line of designer code beyond what WinForms writes
        /// for it; a missing <c>ShouldSerializeXxx</c> method shows up here as a spurious "true".
        /// </summary>
        public bool? ShouldSerialize { get; set; }
        public bool HasDescription { get; set; }

        /// <summary>
        /// Only filled when the dump is asked for it (<c>--attrs --text</c>): the help-pane text.
        /// The diff never looks at it - it is there so the markup of our own controls can be
        /// generated from the reference implementation instead of invented.
        /// </summary>
        public string? Description { get; set; }
    }

    public sealed class TypeDump
    {
        public string BaseType { get; set; } = "";
        public bool IsAbstract { get; set; }

        /// <summary>[DefaultEvent]: what a double-click in the designer wires (Click, Load, CheckedChanged…).</summary>
        public string? DefaultEvent { get; set; }

        /// <summary>[DefaultProperty]: the property the grid selects first.</summary>
        public string? DefaultProperty { get; set; }
        public SortedDictionary<string, MemberDump> Members { get; set; } = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Every public component type of System.Windows.Forms, keyed by full name.
    ///
    /// Category and description come out of <see cref="PropertyDescriptor"/> already localized, so
    /// the UI culture is pinned to invariant first: the dump must read the same on a Russian and on
    /// an English machine, and the generated markup must say <c>[Category("Layout")]</c>, not
    /// <c>[Category("Макет")]</c>. Both sides of the diff then localize the same way at runtime.
    /// </summary>
    public static SortedDictionary<string, TypeDump> Run(bool includeText = false)
    {
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        var result = new SortedDictionary<string, TypeDump>(StringComparer.Ordinal);
        foreach (var type in ComponentTypes())
        {
            result[type.FullName!] = DumpType(type, includeText);
        }
        return result;
    }

    private static IEnumerable<Type> ComponentTypes()
    {
        var assembly = typeof(Control).Assembly;
        Type[] types;
        try { types = assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

        return types
            .Where(t => t.IsPublic
                        && !t.IsGenericTypeDefinition
                        && t.Namespace == "System.Windows.Forms"
                        && (typeof(IComponent).IsAssignableFrom(t) || DesignerValueTypes.Contains(t.FullName!)))
            .OrderBy(t => t.FullName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Objects that are not components but that the designer writes property by property (as locals in
    /// the VS format: <c>treeNode1.Name = "Node1";</c>), so their defaults matter just the same.
    /// </summary>
    private static readonly HashSet<string> DesignerValueTypes = new(StringComparer.Ordinal)
    {
        "System.Windows.Forms.TreeNode",
        "System.Windows.Forms.ListViewItem",
        "System.Windows.Forms.ListViewGroup",
        "System.Windows.Forms.DataGridViewCellStyle",
        "System.Windows.Forms.DataGridViewRow",
    };

    private static TypeDump DumpType(Type type, bool includeText)
    {
        var dump = new TypeDump
        {
            BaseType = type.BaseType?.FullName ?? "",
            IsAbstract = type.IsAbstract,
            DefaultEvent = TypeDescriptor.GetDefaultEvent(type)?.Name,
            DefaultProperty = TypeDescriptor.GetDefaultProperty(type)?.Name,
        };

        var fresh = FreshInstance(type);
        using (fresh as IDisposable)
        {
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(type))
            {
                dump.Members[pd.Name] = new MemberDump
                {
                    Kind = "property",
                    Type = TypeName(pd.PropertyType),
                    DeclaredBy = pd.ComponentType.FullName ?? "",
                    Category = pd.Category ?? "",
                    Browsable = pd.IsBrowsable,
                    ReadOnly = pd.IsReadOnly,
                    Localizable = pd.IsLocalizable,
                    Serialization = SerializationOf(pd.Attributes),
                    HasDefaultValue = DefaultOf(pd.Attributes, out var value),
                    DefaultValue = value,
                    HasActual = fresh != null,
                    Actual = fresh == null ? null : ActualOf(pd, fresh),
                    ShouldSerialize = fresh == null ? null : ShouldSerializeOf(pd, fresh),
                    HasDescription = !string.IsNullOrEmpty(pd.Description),
                    Description = includeText ? pd.Description : null,
                };
            }
        }

        foreach (EventDescriptor ed in TypeDescriptor.GetEvents(type))
        {
            dump.Members["event:" + ed.Name] = new MemberDump
            {
                Kind = "event",
                Type = TypeName(ed.EventType),
                DeclaredBy = ed.ComponentType.FullName ?? "",
                Category = ed.Category ?? "",
                Browsable = ed.Attributes[typeof(BrowsableAttribute)] is not BrowsableAttribute b || b.Browsable,
                HasDescription = !string.IsNullOrEmpty(ed.Description),
                Description = includeText ? ed.Description : null,
            };
        }

        return dump;
    }

    /// <summary>A freshly constructed instance, or null for abstract types and types without a public default constructor.</summary>
    private static object? FreshInstance(Type type)
    {
        if (type.IsAbstract) return null;
        if (type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) == null) return null;
        try { return Activator.CreateInstance(type); }
        catch (Exception) { return null; }
    }

    private static string? ActualOf(PropertyDescriptor pd, object instance)
    {
        try { return Format(pd.GetValue(instance)); }
        catch (Exception ex) { return "!" + ex.GetBaseException().GetType().Name; }
    }

    private static bool? ShouldSerializeOf(PropertyDescriptor pd, object instance)
    {
        if (pd.SerializationVisibility != DesignerSerializationVisibility.Visible || pd.IsReadOnly) return null;
        try { return pd.ShouldSerializeValue(instance); }
        catch (Exception) { return null; }
    }

    private static string SerializationOf(AttributeCollection attributes)
        => attributes[typeof(DesignerSerializationVisibilityAttribute)] is DesignerSerializationVisibilityAttribute a
            ? a.Visibility.ToString()
            : nameof(DesignerSerializationVisibility.Visible);

    private static bool DefaultOf(AttributeCollection attributes, out string? text)
    {
        text = null;
        if (attributes[typeof(DefaultValueAttribute)] is not DefaultValueAttribute a) return false;
        text = Format(a.Value);
        return true;
    }

    /// <summary>
    /// A stable, culture-independent rendering of a default value. Struct values print through
    /// their <see cref="TypeConverter"/> so that <c>Padding</c>, <c>Size</c> and <c>Color</c> read
    /// the same on both sides of the diff.
    /// </summary>
    public static string? Format(object? value)
    {
        switch (value)
        {
            case null: return null;
            case string s: return s;
            case bool b: return b ? "true" : "false";
            case Enum e: return e.ToString();
            case IFormattable f: return f.ToString(null, CultureInfo.InvariantCulture);
        }

        var converter = TypeDescriptor.GetConverter(value.GetType());
        if (converter.CanConvertTo(typeof(string)))
        {
            try { return converter.ConvertToInvariantString(value); }
            catch (Exception) { /* fall through to ToString */ }
        }
        return value.ToString();
    }

    /// <summary>Short, assembly-independent name of a type, for diffing.</summary>
    private static string TypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null) return TypeName(underlying) + "?";
        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];
            return name + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
        }
        return type.Name;
    }
}
