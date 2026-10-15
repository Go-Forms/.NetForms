using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NetForms.Design.Serialization;

namespace NetForms.Design;

/// <summary>
/// Data binding in the property grid (decision 182), as Visual Studio offers it: a <c>DataSource</c> is picked from the
/// form's binding sources or the project's classes (an object data source: a class picked for a grid or a list makes a
/// <c>customerBindingSource</c> for it, and a grid gets a column per property); <c>DisplayMember</c>,
/// <c>ValueMember</c>, <c>DataMember</c> and a column's <c>DataPropertyName</c> list the data source's properties;
/// and <c>(DataBindings)</c> binds a control's bindable properties (<c>Text</c>, <c>Checked</c>, <c>Value</c>…) to a
/// property of a binding source.
/// </summary>
public sealed partial class DesignSurface
{
    internal const string BindingsCategory = "(DataBindings)";
    private const string BindingPrefix = "DataBindings.";
    private const string TypePrefix = "typeof(";

    /// <summary>A DataSource property: the object-typed one of BindingSource, DataGridView, ListBox, ComboBox and the like.</summary>
    private static bool IsDataSourceProperty(PropertyDescriptor pd) =>
        pd.Name == "DataSource" && pd.PropertyType == typeof(object) && !pd.IsReadOnly;

    /// <summary>
    /// The project's classes that can be a data source (VS's "object data sources"): public, concrete classes of the
    /// project's own assemblies with a public property - not components, forms, attributes, exceptions, event args.
    /// </summary>
    internal IReadOnlyList<Type> DataSourceTypes()
    {
        var types = new List<Type>();
        foreach (var assembly in _libraries.Assemblies)
        {
            Type[] exported;
            try { exported = assembly.GetExportedTypes(); }
            catch (Exception) { continue; }
            foreach (var t in exported)
            {
                if (!t.IsClass || t.IsAbstract || t.IsGenericTypeDefinition || t.IsNested && !t.IsNestedPublic) continue;
                if (typeof(IComponent).IsAssignableFrom(t) || typeof(Delegate).IsAssignableFrom(t) || typeof(Attribute).IsAssignableFrom(t)
                    || typeof(Exception).IsAssignableFrom(t) || typeof(EventArgs).IsAssignableFrom(t)) continue;
                if (t.IsDefined(typeof(CompilerGeneratedAttribute), false)) continue;
                if (!t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Any(p => p.CanRead && p.GetIndexParameters().Length == 0)) continue;
                types.Add(t);
            }
        }
        return types.OrderBy(t => t.FullName, StringComparer.Ordinal).Take(500).ToList();
    }

    private static string TypeOption(Type type) => TypePrefix + type.FullName + ")";

    /// <summary>Components of the form a DataSource can point at: binding sources, lists, data sets.</summary>
    private IEnumerable<DesignerComponent> ListComponents(object except) =>
        Model.Components.Where(c => c.Name != null && !ReferenceEquals(c.Instance, except) && c.Instance is not Control
            && c.Instance is BindingSource or IListSource or IList or ITypedList);

    private void DataSourceRow(DesignerPropertyRow row, object target, object? value)
    {
        row.Editor = "component";
        row.Value = value switch
        {
            null => "",
            Type t => TypeOption(t),
            _ => Model.FindByInstance(value)?.Name ?? "(" + value.GetType().Name + ")",
        };
        row.Options = new List<string> { "" };
        row.Options.AddRange(ListComponents(target).Select(c => c.Name!));
        row.Options.AddRange(DataSourceTypes().Select(TypeOption));
        if (row.Value.Length > 0 && !row.Options.Contains(row.Value)) row.Options.Add(row.Value);
    }

    /// <summary>
    /// The member rows: DisplayMember/ValueMember list the properties of the items, DataMember the lists in the data
    /// source, a column's DataPropertyName the properties of its grid's items. Free text stays allowed.
    /// </summary>
    private void MemberOptions(DesignerPropertyRow row, object target, string name)
    {
        List<string>? options = null;
        switch (target, name)
        {
            case (ListControl list, "DisplayMember" or "ValueMember"):
                options = ItemPropertyNames(DataSourceOf(list), "");
                break;
            case (DataGridView grid, "DataMember"):
                options = ListPropertyNames(grid.DataSource);
                break;
            case (BindingSource source, "DataMember"):
                options = ListPropertyNames(source.DataSource);
                break;
            case (DataGridViewColumn { DataGridView: { } grid }, "DataPropertyName"):
                options = ItemPropertyNames(grid.DataSource, grid.DataMember);
                break;
        }
        if (options is { Count: > 0 }) row.Options = options;
    }

    /// <summary>A ListControl's DataSource (ours holds it privately when it has no binding context yet).</summary>
    private static object? DataSourceOf(ListControl list) => list.DataSource;

    private static PropertyDescriptorCollection ItemProperties(object? dataSource, string? dataMember)
    {
        if (dataSource == null) return PropertyDescriptorCollection.Empty;
        try { return ListBindingHelper.GetListItemProperties(dataSource, dataMember ?? "", null); }
        catch (Exception) { return PropertyDescriptorCollection.Empty; }
    }

    private static bool IsListProperty(PropertyDescriptor pd) =>
        typeof(IList).IsAssignableFrom(pd.PropertyType) || typeof(IListSource).IsAssignableFrom(pd.PropertyType)
        || pd.PropertyType.IsGenericType && pd.PropertyType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) && pd.PropertyType != typeof(string);

    private static List<string> ItemPropertyNames(object? dataSource, string? dataMember) =>
        ItemProperties(dataSource, dataMember).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable && !IsListProperty(p)).Select(p => p.Name).ToList();

    private static List<string> ListPropertyNames(object? dataSource) =>
        ItemProperties(dataSource, "").Cast<PropertyDescriptor>().Where(p => p.IsBrowsable && IsListProperty(p)).Select(p => p.Name).ToList();

    /// <summary>Sets a DataSource from the grid: "" (none), a component's name, or <c>typeof(Full.Name)</c> of a project class.</summary>
    private void SetDataSource(object target, PropertyDescriptor pd, string? value)
    {
        object? source;
        if (string.IsNullOrEmpty(value)) source = null;
        else if (value.StartsWith(TypePrefix, StringComparison.Ordinal) && value.EndsWith(')'))
        {
            var fullName = value.Substring(TypePrefix.Length, value.Length - TypePrefix.Length - 1);
            var type = DataSourceTypes().FirstOrDefault(t => t.FullName == fullName)
                ?? throw new DesignerEditException($"'{fullName}' is not a class of the project. Build the project, then pick it again.");
            // A binding source takes the class itself; anything else gets a binding source for it, as in VS.
            source = target is BindingSource ? type : BindingSourceFor(type);
        }
        else
        {
            source = Resolve(value);
            if (ReferenceEquals(source, target)) throw new DesignerEditException("A component cannot be its own data source.");
        }
        try { pd.SetValue(target, source); }
        catch (Exception ex) { throw new DesignerEditException($"DataSource cannot be set to '{value}': {Unwrap(ex).Message}", ex); }
        if (target is DataGridView grid) AddBoundColumns(grid);
    }

    /// <summary>The form's binding source for a class (one already there), or a new one named as VS names it: customerBindingSource.</summary>
    private BindingSource BindingSourceFor(Type type)
    {
        foreach (var c in Model.Components)
            if (c.Instance is BindingSource { DataSource: Type t } existing && t == type && string.IsNullOrEmpty(existing.DataMember)) return existing;
        var source = new BindingSource();
        var baseName = char.ToLowerInvariant(type.Name[0]) + type.Name.Substring(1) + "BindingSource";
        var name = baseName;
        for (int i = 1; Model.Find(name) != null; i++) name = baseName + i;
        Model.AddComponent(source, name);
        source.DataSource = type;
        return source;
    }

    /// <summary>
    /// What VS's DataGridViewDesigner does when a grid gets a data source: a column for each property no column shows
    /// yet - a check box column for a bool, an image column for a picture, a text box column otherwise - named
    /// <c>nameDataGridViewTextBoxColumn</c>, its DataPropertyName and HeaderText the property's.
    /// </summary>
    private void AddBoundColumns(DataGridView grid)
    {
        foreach (PropertyDescriptor property in ItemProperties(grid.DataSource, grid.DataMember))
        {
            if (!property.IsBrowsable || IsListProperty(property)) continue;
            if (grid.Columns.Cast<DataGridViewColumn>().Any(c => string.Equals(c.DataPropertyName, property.Name, StringComparison.OrdinalIgnoreCase))) continue;
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            DataGridViewColumn column = type == typeof(bool) ? new DataGridViewCheckBoxColumn()
                : typeof(Image).IsAssignableFrom(type) ? new DataGridViewImageColumn()
                : new DataGridViewTextBoxColumn();
            var baseName = char.ToLowerInvariant(property.Name[0]) + property.Name.Substring(1) + column.GetType().Name;
            var name = baseName;
            for (int i = 1; Model.Find(name) != null; i++) name = baseName + i;
            Model.AddComponent(column, name);
            column.Name = name;
            column.DataPropertyName = property.Name;
            column.HeaderText = property.DisplayName;
            grid.Columns.Add(column);
        }
    }

    // --- (DataBindings) -------------------------------------------------------------------------------

    /// <summary>
    /// The (DataBindings) rows of a control: its bindable properties ([Bindable(true)]: Text, Tag, Checked, Value…)
    /// and whatever is bound already, each with the binding sources' properties to pick from ("customerBindingSource - Name").
    /// </summary>
    private void BindingRows(object target, List<DesignerPropertyRow> rows)
    {
        if (target is not IBindableComponent bindable) return;
        var bound = bindable.DataBindings.Cast<Binding>().ToDictionary(b => b.PropertyName, StringComparer.Ordinal);
        var names = TypeDescriptor.GetProperties(target, new Attribute[] { BindableAttribute.Yes }).Cast<PropertyDescriptor>()
            .Where(p => !p.IsReadOnly).Select(p => p.Name).Concat(bound.Keys).Distinct().ToList();
        if (names.Count == 0) return;
        var options = new List<string> { "" };
        foreach (var c in ListComponents(target).Where(c => c.Instance is BindingSource or IListSource or ITypedList))
            options.AddRange(ItemPropertyNames(c.Instance, "").Select(p => c.Name + " - " + p));
        foreach (var name in names)
        {
            bound.TryGetValue(name, out var binding);
            var value = binding == null ? "" : BindingText(binding);
            var row = new DesignerPropertyRow
            {
                Name = BindingPrefix + name,
                Label = name,
                Category = BindingsCategory,
                Description = $"Binds {name} to a property of a data source on the form.",
                Type = "Binding",
                Editor = "component",
                Value = value,
                Modified = binding != null,
                Options = options.Contains(value) ? options : options.Append(value).ToList(),
            };
            rows.Add(row);
        }
    }

    private string BindingText(Binding binding)
    {
        var source = binding.DataSource == null ? "" : Model.FindByInstance(binding.DataSource)?.Name ?? "(" + binding.DataSource.GetType().Name + ")";
        var member = binding.BindingMemberInfo.BindingMember;
        return member.Length == 0 ? source : source + " - " + member;
    }

    private static bool IsBindingProperty(string name, out string property)
    {
        property = name.StartsWith(BindingPrefix, StringComparison.Ordinal) ? name.Substring(BindingPrefix.Length) : "";
        return property.Length > 0;
    }

    /// <summary>
    /// "customerBindingSource - Name": the control's property bound to that member of that source, with formatting on,
    /// as VS binds (<c>new Binding("Text", customerBindingSource, "Name", true)</c>). "" removes the binding.
    /// </summary>
    private void SetBinding(object target, string property, string? value)
    {
        if (target is not IBindableComponent bindable) throw new DesignerEditException($"'{target.GetType().Name}' cannot be bound.");
        var old = bindable.DataBindings[property];
        if (string.IsNullOrEmpty(value))
        {
            if (old != null) bindable.DataBindings.Remove(old);
            return;
        }
        int dash = value.IndexOf(" - ", StringComparison.Ordinal);
        var sourceName = dash < 0 ? value : value.Substring(0, dash);
        var member = dash < 0 ? "" : value.Substring(dash + 3);
        var source = Resolve(sourceName);
        if (TypeDescriptor.GetProperties(target)[property] == null) throw new DesignerEditException($"'{target.GetType().Name}' has no property '{property}'.");
        var binding = new Binding(property, source, member, true);
        if (old != null)
        {
            binding.DataSourceUpdateMode = old.DataSourceUpdateMode;
            binding.FormatString = old.FormatString;
            binding.NullValue = old.NullValue;
            bindable.DataBindings.Remove(old);
        }
        try { bindable.DataBindings.Add(binding); }
        catch (Exception ex) { throw new DesignerEditException($"{property} cannot be bound to '{value}': {Unwrap(ex).Message}", ex); }
    }

    /// <summary>
    /// While designed, a grid makes no columns of its own (VS's DataGridViewDesigner turns AutoGenerateColumns off and
    /// keeps the real value aside): the columns are the form's components. The ones made already go.
    /// </summary>
    private void PrepareGridForDesign(DesignerComponent component, DataGridView grid)
    {
        if (grid.AutoGenerateColumns)
        {
            component.SetShadowProperty("AutoGenerateColumns", true);
            grid.AutoGenerateColumns = false;
        }
        for (int i = grid.Columns.Count - 1; i >= 0; i--)
            if (grid.Columns[i].IsAutoGenerated) grid.Columns.RemoveAt(i);
    }
}
