using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace NetForms.Compat;

/// <summary>
/// Which properties a code serializer would write, as <see cref="PropertyDescriptor.ShouldSerializeValue"/>
/// answers it, for every component of the sample forms right after their <c>InitializeComponent()</c>.
///
/// The file is compiled into both <c>NetForms.Compat</c> (the REAL System.Windows.Forms, running the
/// very same <c>samples/*/*.Designer.cs</c>) and the NetForms test suite, so the two dumps can be
/// diffed key by key. It is the oracle of phase 5.2: the designer code writer emits exactly the
/// properties whose descriptor says "serialize me", so a property that says it differently from
/// WinForms is a line the writer would add or drop.
///
/// This is the raw component answer. The Visual Studio designer overrides a handful of properties on
/// top of it (Location, Size and Name are always written for a control) - that is the writer's
/// business, and it is identical for both implementations.
/// </summary>
public static class SerializationDump
{
    /// <summary>The sample forms both sides compile.</summary>
    public static Type[] Forms => new[]
    {
        typeof(HelloForms.MainForm),
        typeof(Gallery.GalleryForm),
        typeof(Gallery.CollectionsForm),
        typeof(Gallery.ResourcesForm),
        typeof(Gallery.ColumnsForm),
        typeof(Strips.MainForm),
        typeof(MdiDemo.MainForm),
        typeof(MdiDemo.DocumentForm),
        typeof(Form1),
    };

    /// <summary>
    /// "Form/component.Property" → the value (through its converter) for every property that
    /// should be serialized; content collections record their element count instead.
    /// </summary>
    public static SortedDictionary<string, string?> Run()
    {
        var result = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var formType in Forms)
        {
            var form = DesignerInstance(formType);
            var prefix = formType.Name + "/";
            Component(result, prefix + "(root)", form, depth: 0);
            foreach (var f in formType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (f.GetValue(form) is IComponent c && f.FieldType != typeof(IContainer))
                    Component(result, prefix + f.Name, c, depth: 0);
            }
        }
        return result;
    }

    /// <summary>
    /// What the designer holds: the base class constructed, then <c>InitializeComponent()</c> - and
    /// nothing the form's own constructor does afterwards.
    /// </summary>
    public static Form DesignerInstance(Type formType)
    {
        var form = (Form)RuntimeHelpers.GetUninitializedObject(formType);
        formType.BaseType!.GetConstructor(Type.EmptyTypes)!.Invoke(form, null);
        formType.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        return form;
    }

    private static void Component(SortedDictionary<string, string?> result, string key, object component, int depth)
    {
        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(component))
        {
            // Members the user's own class declares are not the framework's business.
            if (pd.ComponentType.Assembly != typeof(Control).Assembly) continue;
            if (pd.SerializationVisibility == DesignerSerializationVisibility.Hidden) continue;

            object? value;
            try { value = pd.GetValue(component); }
            catch (Exception) { continue; }

            if (pd.SerializationVisibility == DesignerSerializationVisibility.Content)
            {
                if (value is ICollection collection)
                {
                    if (collection.Count > 0) result[key + "." + pd.Name] = "count=" + collection.Count;
                }
                else if (value is IComponent nested && depth == 0)
                {
                    Component(result, key + "." + pd.Name, nested, depth + 1);
                }
                continue;
            }

            bool should;
            try { should = pd.ShouldSerializeValue(component); }
            catch (Exception ex) { result[key + "." + pd.Name] = "!" + ex.GetBaseException().GetType().Name; continue; }
            if (!should) continue;
            result[key + "." + pd.Name] = value is IComponent ? "(component)" : AttributeDump.Format(value);
        }
    }
}
