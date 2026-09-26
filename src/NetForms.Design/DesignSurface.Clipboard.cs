using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using NetForms.Design.Serialization;

namespace NetForms.Design;

/// <summary>
/// Copy, paste and duplicate: components as the clipboard carries them between forms. A copied component
/// is its type, the properties the designer would write for it (the same <c>ShouldSerialize</c> rules),
/// its collections, children and event bindings - so a pasted one is written exactly as a copy made by
/// hand would be. As in Visual Studio, a pasted component keeps its name when the form has it free
/// (another form) and gets the next free one otherwise (<c>button2</c>); its event handlers stay bound
/// when it is pasted into the form it came from.
/// </summary>
public sealed partial class DesignSurface
{
    /// <summary>The marker that tells a designer clipboard from any other text.</summary>
    public const string ClipboardFormat = "netforms/components-1";

    private static readonly JsonSerializerOptions s_clipJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>The ids a paste or duplicate created, for the client to select (in <see cref="DesignerView.Select"/>).</summary>
    private List<string>? _pasted;

    /// <summary>True when <paramref name="text"/> is what <see cref="Copy"/> produces.</summary>
    public static bool IsClipboardText(string? text) =>
        text != null && text.TrimStart().StartsWith("{", StringComparison.Ordinal) && text.Contains(ClipboardFormat, StringComparison.Ordinal);

    /// <summary>The components named by <paramref name="ids"/> (with everything inside them) as clipboard text.</summary>
    public string Copy(IEnumerable<string> ids)
    {
        var targets = new List<object>();
        foreach (var id in ids)
        {
            var target = Resolve(id);
            if (ReferenceEquals(target, Root)) continue;
            if (Model.FindByInstance(target) == null) throw new DesignerEditException($"'{id}' is a part of its control; copy the control instead.");
            if (!targets.Contains(target)) targets.Add(target);
        }
        // A control inside another copied one goes along with it anyway.
        targets = targets.Where(t => !targets.Any(o => !ReferenceEquals(o, t) && Contains(o, t))).ToList();
        if (targets.Count == 0) throw new DesignerEditException("Select the controls to copy.");
        var clip = new ClipBoard { Format = ClipboardFormat, ClassName = Model.ClassName, Components = targets.Select(t => Capture(t)).ToList() };
        return JsonSerializer.Serialize(clip, s_clipJson);
    }

    private static bool Contains(object outer, object inner)
    {
        var all = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Collect(outer, all);
        return all.Contains(inner);
    }

    // --- capture ------------------------------------------------------------------------------------

    private ClipNode Capture(object instance)
    {
        var component = Model.FindByInstance(instance);
        if (component is { IsPlaceholder: true })
            throw new DesignerEditException($"{component.Name} ({component.TypeName}) is not a NetForms component; the designer cannot copy it.");
        var node = new ClipNode { Type = instance.GetType().FullName!, Name = component?.Name };
        CaptureProperties(instance, component, node);
        if (component != null)
            foreach (var e in component.Events.Where(e => e.Path == e.EventName))
                (node.Events ??= new())[e.EventName] = e.HandlerName;
        return node;
    }

    private void CaptureProperties(object instance, DesignerComponent? component, ClipNode node)
    {
        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(instance).Sort())
        {
            if (pd.Name == "Name") continue;
            var visibility = pd.SerializationVisibility;
            if (visibility == DesignerSerializationVisibility.Hidden) continue;
            object? value;
            try { value = pd.GetValue(instance); }
            catch (Exception) { continue; }

            if (visibility == DesignerSerializationVisibility.Content)
            {
                CaptureContent(instance, pd, value, node);
                continue;
            }
            if (pd.IsReadOnly) continue;
            if (component != null && component.ShadowProperties.TryGetValue(pd.Name, out var shadow))
            {
                if (shadow is false) (node.Props ??= new())[pd.Name] = "False";
                continue;
            }
            bool force = instance is Control and not ToolStripDropDown && pd.Name is "Location" or "Size";
            if (!force && !ShouldSerialize(pd, instance)) continue;
            if (value is IComponent referenced)
            {
                if (Model.FindByInstance(referenced)?.Name is { } name) (node.Refs ??= new())[pd.Name] = name;
                continue;
            }
            if (ClipText(pd, value, out var text)) (node.Props ??= new())[pd.Name] = text;
        }

        if (instance is Control { Parent: TableLayoutPanel table } cell && component != null)
            node.Cell = new[] { table.GetColumn(cell), table.GetRow(cell), table.GetColumnSpan(cell), table.GetRowSpan(cell) };
    }

    private void CaptureContent(object owner, PropertyDescriptor pd, object? value, ClipNode node)
    {
        switch (value)
        {
            case null:
                return;
            case Control.ControlCollection controls:
                foreach (Control child in controls)
                    if (Model.FindByInstance(child) != null) (node.Children ??= new()).Add(Capture(child));
                return;
            case Control nested when Model.FindByInstance(nested) == null:
            {
                // A part with a place in the code of its own (SplitContainer.Panel1): its properties and controls.
                var inner = new ClipNode { Type = nested.GetType().FullName! };
                CaptureProperties(nested, null, inner);
                (node.Nested ??= new())[pd.Name] = inner;
                return;
            }
            case IComponent:
                return; // a component of its own
            case TableLayoutStyleCollection styles:
                (node.Styles ??= new())[pd.Name] = styles.Cast<TableLayoutStyle>()
                    .Select(s => s.SizeType + " " + StyleSize(s).ToString(CultureInfo.InvariantCulture)).ToList();
                return;
            case ControlBindingsCollection:
                return;
            case IList list when ComponentElementType(list) != null:
                (node.Elements ??= new())[pd.Name] = list.Cast<object>().Select(Capture).ToList();
                return;
            case IList list:
            {
                // Items as the collection editor spells them: only lists it can read back.
                if (list.Count == 0) return;
                var (items, format) = CollectionText(list);
                if (format == "lines" && !list.Cast<object?>().All(o => o is string)) return;
                (node.Lists ??= new())[pd.Name] = new ClipList { Format = format, Items = items };
                return;
            }
            case IEnumerable:
                return;
        }

        // An object inside the component (Button.FlatAppearance): its properties, by path.
        foreach (PropertyDescriptor inner in TypeDescriptor.GetProperties(value))
        {
            if (inner.SerializationVisibility != DesignerSerializationVisibility.Visible || inner.IsReadOnly) continue;
            if (!ShouldSerialize(inner, value)) continue;
            object? innerValue;
            try { innerValue = inner.GetValue(value); }
            catch (Exception) { continue; }
            if (ClipText(inner, innerValue, out var text)) (node.Props ??= new())[pd.Name + "." + inner.Name] = text;
        }
    }

    private static float StyleSize(TableLayoutStyle style) => style switch
    {
        ColumnStyle c => c.Width,
        RowStyle r => r.Height,
        _ => 0,
    };

    private static bool ShouldSerialize(PropertyDescriptor pd, object instance)
    {
        try { return pd.ShouldSerializeValue(instance); }
        catch (Exception) { return false; }
    }

    /// <summary>A value as the text its converter reads back; false for what does not round-trip (an image).</summary>
    private static bool ClipText(PropertyDescriptor pd, object? value, out string? text)
    {
        text = null;
        if (value == null) return pd.PropertyType == typeof(string);
        if (value is string s) { text = s; return pd.PropertyType == typeof(string) || pd.Converter.CanConvertFrom(typeof(string)); }
        try
        {
            var converter = pd.Converter;
            if (!converter.CanConvertFrom(typeof(string)) || !converter.CanConvertTo(typeof(string))) return false;
            text = converter.ConvertToInvariantString(value);
            return text != null;
        }
        catch (Exception) { return false; }
    }

    // --- paste --------------------------------------------------------------------------------------

    /// <summary>
    /// <c>paste</c>: the clipboard's components into the form. <c>parent</c> is what is selected: a container
    /// takes the controls (a TabControl on its shown page), a control hands them to its container, a strip or
    /// a menu item takes tool strip items; components without a place go to the tray.
    /// </summary>
    private void Paste(DesignerOp op)
    {
        ClipBoard? clip;
        try { clip = JsonSerializer.Deserialize<ClipBoard>(Required(op.Value, "value"), s_clipJson); }
        catch (JsonException ex) { throw new DesignerEditException("The clipboard does not hold NetForms controls.", ex); }
        if (clip?.Format != ClipboardFormat || clip.Components == null) throw new DesignerEditException("The clipboard does not hold NetForms controls.");
        var target = string.IsNullOrEmpty(op.Parent) ? Root : Resolve(op.Parent);
        PasteNodes(clip, clip.Components.Select(n => (n, target)).ToList(), op);
    }

    /// <summary><c>duplicate</c>: a copy of each of <c>ids</c> beside the original, in the same container.</summary>
    private void Duplicate(DesignerOp op)
    {
        var ids = (op.Ids ?? Array.Empty<string?>()).Where(i => !string.IsNullOrEmpty(i)).Select(i => i!).ToList();
        var clip = JsonSerializer.Deserialize<ClipBoard>(Copy(ids), s_clipJson)!;
        var placed = new List<(ClipNode, object)>();
        foreach (var node in clip.Components!)
        {
            var original = Model.Find(node.Name!)!.Instance;
            object where = original switch
            {
                Control { Parent: { } parent } => parent,
                ToolStripItem { Owner: { } owner } => owner,
                ColumnHeader { ListView: { } list } => list,
                DataGridViewColumn { DataGridView: { } grid } => grid,
                _ => Root,
            };
            placed.Add((node, where));
        }
        PasteNodes(clip, placed, op);
    }

    private void PasteNodes(ClipBoard clip, List<(ClipNode Node, object Target)> nodes, DesignerOp op)
    {
        bool sameForm = clip.ClassName == Model.ClassName;
        var renames = new Dictionary<string, string>(StringComparer.Ordinal);
        var references = new List<(object Target, string Property, string Name)>();
        var created = new List<(object Instance, ClipNode Node)>();
        _pasted = new List<string>();

        foreach (var (node, target) in nodes)
        {
            var instance = Create(node, renames, out var component);
            var parent = PasteTarget(instance, target);
            Place((IComponent)instance, parent, new DesignerOp());
            if (instance is Control control && control.Parent is TableLayoutPanel && node.Cell != null)
                SetCell((TableLayoutPanel)control.Parent, control, node.Cell);
            created.Add((instance, node));
            _pasted.Add(component.Name!);
        }
        foreach (var (instance, node) in created)
        {
            // A pasted control comes last in the tab order of its container, as a dropped one does.
            int tabIndex = instance is Control c ? c.TabIndex : 0;
            Fill(instance, node, sameForm, renames, references);
            if (instance is Control pasted) pasted.TabIndex = tabIndex;
        }

        // Where the pasted controls would lie exactly on a control already there, they move down and right
        // together, as far as it takes (a copy pasted into its own container lands beside the original).
        var controls = created.Select(c => c.Instance).OfType<Control>().Where(c => c.Parent is not TableLayoutPanel and not FlowLayoutPanel && c is not TabPage).ToList();
        foreach (var control in controls)
            if (op.X != null || op.Y != null) control.Location = new Point(op.X ?? control.Left, op.Y ?? control.Top);
        for (int step = 0; step < 50 && controls.Any(Overlaps); step++)
            foreach (var control in controls) control.Location = new Point(control.Left + 8, control.Top + 8);

        bool Overlaps(Control c) => c.Parent!.Controls.Cast<Control>()
            .Any(o => !ReferenceEquals(o, c) && !controls.Contains(o) && o.Location == c.Location);

        foreach (var (instance, property, name) in references)
        {
            var resolved = renames.TryGetValue(name, out var renamed) ? renamed : Model.Find(name) != null ? name : null;
            if (resolved == null) continue;
            try { SetProperty(instance, property, resolved); }
            catch (DesignerEditException) { }
        }
    }

    /// <summary>A new component of the node's type, named as the original when that name is free.</summary>
    private object Create(ClipNode node, Dictionary<string, string> renames, out DesignerComponent component)
    {
        var type = DesignerToolbox.ResolveType(node.Type ?? "", _libraries)
            ?? throw new DesignerEditException($"'{node.Type}' is not a component the designer knows.");
        IComponent instance;
        try { instance = (IComponent)Activator.CreateInstance(type)!; }
        catch (Exception ex) { throw new DesignerEditException($"Creating a {type.Name} failed: {Unwrap(ex).Message}", ex); }
        component = null!;
        if (node.Name != null)
        {
            try { component = Model.AddComponent(instance, node.Name); }
            catch (ArgumentException) { }
        }
        component ??= Model.AddComponent(instance);
        if (node.Name != null) renames[node.Name] = component.Name!;
        return instance;
    }

    /// <summary>Where a pasted component goes, given what is selected.</summary>
    private object? PasteTarget(object instance, object target)
    {
        switch (instance)
        {
            case TabPage:
                return target switch
                {
                    TabControl tabs => tabs,
                    TabPage { Parent: TabControl tabs } => tabs,
                    Control c when Ancestor<TabControl>(c) is { } tabs => tabs,
                    _ => throw new DesignerEditException("Select a TabControl to paste a tab page into."),
                };
            case ToolStripDropDown:
                return null;
            case Control:
                // The selected container, or the container of the selected control (a strip item's: the form).
                for (var c = target as Control ?? Root; ; c = c.Parent ?? Root)
                {
                    if (c is TabControl tabs && tabs.SelectedTab is { } page) return page;
                    if (ReferenceEquals(c, Root) || c is Panel or GroupBox) return c;
                }
            case ToolStripItem:
                return target switch
                {
                    ToolStrip strip => strip,
                    ToolStripDropDownItem menu when menu is ToolStripMenuItem => menu,
                    ToolStripItem { Owner: ToolStripDropDown { OwnerItem: { } ownerItem } } => ownerItem,
                    ToolStripItem { Owner: { } owner } => owner,
                    _ => throw new DesignerEditException("Select a menu, a tool strip or a status strip to paste its items into."),
                };
            case ColumnHeader:
                return target as ListView ?? throw new DesignerEditException("Select a ListView to paste a column into.");
            case DataGridViewColumn:
                return target as DataGridView ?? throw new DesignerEditException("Select a DataGridView to paste a column into.");
            default:
                return null;
        }
    }

    /// <summary>The node's properties, collections, children and handlers onto a new instance.</summary>
    private void Fill(object instance, ClipNode node, bool sameForm, Dictionary<string, string> renames,
        List<(object, string, string)> references)
    {
        var component = Model.FindByInstance(instance);
        if (node.Props != null)
        {
            foreach (var (key, text) in node.Props)
            {
                try { SetPasted(instance, key, text); }
                catch (DesignerEditException) { }
            }
        }
        if (node.Styles != null && instance is TableLayoutPanel table)
        {
            foreach (var (name, styles) in node.Styles)
            {
                var list = name == "ColumnStyles" ? (TableLayoutStyleCollection)table.ColumnStyles : table.RowStyles;
                list.Clear();
                foreach (var s in styles)
                {
                    var parts = s.Split(' ');
                    var sizeType = Enum.Parse<SizeType>(parts[0]);
                    float size = parts.Length > 1 ? float.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
                    if (name == "ColumnStyles") table.ColumnStyles.Add(new ColumnStyle(sizeType, size));
                    else table.RowStyles.Add(new RowStyle(sizeType, size));
                }
            }
        }
        if (node.Lists != null)
            foreach (var (name, items) in node.Lists)
                SetItems(instance, name, items.Items?.ToArray() ?? Array.Empty<string>(), null);
        if (node.Elements != null)
        {
            foreach (var (name, elements) in node.Elements)
            {
                if (TypeDescriptor.GetProperties(instance)[name]?.GetValue(instance) is not IList list) continue;
                foreach (var element in elements)
                {
                    var created = Create(element, renames, out _);
                    list.Add(created);
                    Fill(created, element, sameForm, renames, references);
                }
            }
        }
        if (node.Children != null && instance is Control parent)
        {
            foreach (var child in node.Children)
            {
                var created = (Control)Create(child, renames, out _);
                parent.Controls.Add(created);
                if (parent is TableLayoutPanel cells && child.Cell != null) SetCell(cells, created, child.Cell);
                Fill(created, child, sameForm, renames, references);
            }
        }
        if (node.Nested != null)
        {
            foreach (var (name, nested) in node.Nested)
                if (TypeDescriptor.GetProperties(instance)[name]?.GetValue(instance) is { } part)
                    Fill(part, nested, sameForm, renames, references);
        }
        if (node.Refs != null)
            foreach (var (name, referenced) in node.Refs) references.Add((instance, name, referenced));
        if (node.Events != null && sameForm && component != null)
        {
            foreach (var (eventName, handler) in node.Events)
            {
                try { Model.BindEvent(component, eventName, handler); }
                catch (ArgumentException) { }
            }
        }
    }

    private static T? Ancestor<T>(Control control) where T : Control
    {
        for (var c = control.Parent; c != null; c = c.Parent)
            if (c is T found) return found;
        return null;
    }

    private static void SetCell(TableLayoutPanel table, Control control, int[] cell)
    {
        if (cell.Length < 4) return;
        table.SetColumn(control, cell[0]);
        table.SetRow(control, cell[1]);
        table.SetColumnSpan(control, cell[2]);
        table.SetRowSpan(control, cell[3]);
    }

    /// <summary>A copied value back onto an object; <c>FlatAppearance.BorderSize</c> through the object it names.</summary>
    private void SetPasted(object instance, string key, string? text)
    {
        int dot = key.IndexOf('.');
        if (dot > 0)
        {
            var owner = TypeDescriptor.GetProperties(instance)[key.Substring(0, dot)]?.GetValue(instance);
            var inner = owner == null ? null : TypeDescriptor.GetProperties(owner)[key.Substring(dot + 1)];
            if (owner == null || inner == null || inner.IsReadOnly) return;
            try { inner.SetValue(owner, Convert(inner, text)); }
            catch (Exception ex) when (ex is not DesignerEditException) { }
            return;
        }
        if (Model.FindByInstance(instance) != null)
        {
            SetProperty(instance, key, text);
            return;
        }
        var pd = TypeDescriptor.GetProperties(instance)[key];
        if (pd == null || pd.IsReadOnly) return;
        try { pd.SetValue(instance, Convert(pd, text)); }
        catch (Exception ex) when (ex is not DesignerEditException) { }
    }

    // --- the clipboard's shape --------------------------------------------------------------------

    private sealed class ClipBoard
    {
        public string? Format { get; set; }
        /// <summary>The class the components were copied from: handlers stay bound when pasted back into it.</summary>
        public string? ClassName { get; set; }
        public List<ClipNode>? Components { get; set; }
    }

    private sealed class ClipNode
    {
        public string? Type { get; set; }
        public string? Name { get; set; }
        /// <summary>Property values as their converters write them, in the order the designer writes them.</summary>
        public Dictionary<string, string?>? Props { get; set; }
        /// <summary>Properties that point at another component, by its name.</summary>
        public Dictionary<string, string>? Refs { get; set; }
        /// <summary>Collections the grid edits as text (ListBox.Items, TreeView.Nodes, ListView.Items).</summary>
        public Dictionary<string, ClipList>? Lists { get; set; }
        /// <summary>TableLayoutPanel.ColumnStyles / RowStyles: "Percent 50".</summary>
        public Dictionary<string, List<string>>? Styles { get; set; }
        /// <summary>Collections of components (a strip's Items, a menu item's DropDownItems, Columns).</summary>
        public Dictionary<string, List<ClipNode>>? Elements { get; set; }
        /// <summary>Child controls, in the order of Controls.</summary>
        public List<ClipNode>? Children { get; set; }
        /// <summary>Parts of the control (SplitContainer.Panel1) with their properties and children.</summary>
        public Dictionary<string, ClipNode>? Nested { get; set; }
        /// <summary>The cell in a TableLayoutPanel: column, row, column span, row span.</summary>
        public int[]? Cell { get; set; }
        /// <summary>Event → handler.</summary>
        public Dictionary<string, string>? Events { get; set; }
    }

    private sealed class ClipList
    {
        public string? Format { get; set; }
        public List<string>? Items { get; set; }
    }
}
