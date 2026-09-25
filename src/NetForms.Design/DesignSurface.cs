using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using NetForms.Design.Serialization;

namespace NetForms.Design;

/// <summary>An edit the designer refuses, with a message for the user.</summary>
public sealed class DesignerEditException : Exception
{
    public DesignerEditException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// One form open in the designer (Ф5.3, docs/PLAN.md): the live object tree built from its
/// <c>*.Designer.cs</c>, the edits the client asks for, undo/redo and saving.
/// </summary>
/// <remarks>
/// <para>
/// <b>The file is the truth.</b> Every batch of edits is applied to the live objects, written with
/// <see cref="DesignerCodeWriter"/> and read back with <see cref="DesignerCodeReader"/>; the model the
/// client sees next is the one the new file gives. A batch that fails anywhere - an edit, the write,
/// the read-back - leaves source and model exactly as they were. Undo and redo are snapshots of the
/// two files (the designer file and its code-behind, which receives handler stubs), like the history
/// GoFormsDesigner keeps.
/// </para>
/// <para>
/// <b>Nothing runs.</b> The form lives on a headless platform: no window, no timers ticking, no input
/// reaching the controls - a click in the designer selects, it never presses. <c>Visible</c> and
/// <c>Enabled</c> are design-time shadows, as in the VS ControlDesigner: a control the code hides is
/// still drawn and selectable, and the value to write is kept on the model.
/// </para>
/// </remarks>
public sealed class DesignSurface : IDisposable
{
    private sealed record Snapshot(string Source, string? Companion);

    private readonly DesignerCodeReader _reader = new();
    private readonly DesignerCodeWriter _writer = new();
    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();
    private DesignerHandlerLocation? _lastHandler;
    private DesignerModel? _model;

    private DesignSurface(string source, string? companion, string? path, string? companionPath)
    {
        HeadlessPlatform.Install();
        Source = source;
        CompanionSource = companion;
        FilePath = path;
        CompanionPath = companionPath;
        Reload();
    }

    /// <summary>Opens <c>MainForm.Designer.cs</c> (and <c>MainForm.cs</c> beside it, when there is one).</summary>
    public static DesignSurface Open(string designerPath)
    {
        var companionPath = DesignerCodeReader.CompanionPath(designerPath);
        var companion = companionPath != null && File.Exists(companionPath) ? File.ReadAllText(companionPath) : null;
        return new DesignSurface(File.ReadAllText(designerPath), companion, Path.GetFullPath(designerPath), companion != null ? Path.GetFullPath(companionPath!) : null);
    }

    /// <summary>A form from memory; <see cref="Save"/> is then a no-op.</summary>
    public static DesignSurface Load(string designerSource, string? companionSource = null) =>
        new(designerSource, companionSource, null, null);

    public string? FilePath { get; }
    public string? CompanionPath { get; }

    /// <summary>The designer file as it stands after the last edit.</summary>
    public string Source { get; private set; }

    /// <summary>The code-behind file (handler stubs are added to it), or null when there is none.</summary>
    public string? CompanionSource { get; private set; }

    public DesignerModel Model => _model!;

    /// <summary>Write both files after every edit, undo and redo (the VS Code editor does not keep a buffer of its own).</summary>
    public bool AutoSave { get; set; }

    /// <summary>Edited since the last <see cref="Save"/>.</summary>
    public bool IsDirty { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    private Control Root => (Control)Model.Root.Instance;

    // --- editing ---------------------------------------------------------------------------------

    /// <summary>Applies a batch of edits as one undoable step. On any failure nothing changes.</summary>
    public void Apply(IEnumerable<DesignerOp> ops)
    {
        ArgumentNullException.ThrowIfNull(ops);
        var before = new Snapshot(Source, CompanionSource);
        try
        {
            foreach (var op in ops) ApplyOne(op);
            try { Source = _writer.Write(Model, Source); }
            catch (NotSupportedException ex) { throw new DesignerEditException(ex.Message, ex); }
            Reload();
        }
        catch
        {
            Source = before.Source;
            CompanionSource = before.Companion;
            Reload();
            throw;
        }
        if (Source != before.Source || CompanionSource != before.Companion)
        {
            _undo.Add(before);
            _redo.Clear();
            Changed();
        }
    }

    public bool Undo() => Step(_undo, _redo);

    public bool Redo() => Step(_redo, _undo);

    private bool Step(List<Snapshot> from, List<Snapshot> to)
    {
        if (from.Count == 0) return false;
        to.Add(new Snapshot(Source, CompanionSource));
        var target = from[^1];
        from.RemoveAt(from.Count - 1);
        Source = target.Source;
        CompanionSource = target.Companion;
        Reload();
        Changed();
        return true;
    }

    private void Changed()
    {
        IsDirty = true;
        if (AutoSave) Save();
    }

    /// <summary>Writes the designer file and the code-behind, keeping a byte-order mark where there was one.</summary>
    public void Save()
    {
        if (FilePath == null) { IsDirty = false; return; }
        WriteKeepingBom(FilePath, Source);
        if (CompanionPath != null && CompanionSource != null) WriteKeepingBom(CompanionPath, CompanionSource);
        IsDirty = false;
    }

    private static void WriteKeepingBom(string path, string text)
    {
        bool bom = false;
        if (File.Exists(path))
        {
            if (File.ReadAllText(path) == text) return;
            var head = new byte[3];
            using (var s = File.OpenRead(path)) bom = s.Read(head, 0, 3) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
        }
        File.WriteAllText(path, text, new UTF8Encoding(bom));
    }

    private void Reload()
    {
        var old = _model?.Root.Instance as IDisposable;
        _model = _reader.Read(Source, CompanionSource != null ? new[] { CompanionSource } : Array.Empty<string>(), FilePath);
        PrepareForDesign();
        old?.Dispose();
    }

    /// <summary>
    /// Sites every component (DesignMode is true, as for a component on a VS design surface - a control
    /// that behaves differently while designed sees it) and moves Visible/Enabled into design-time shadows,
    /// so everything is drawn and selectable.
    /// </summary>
    private void PrepareForDesign()
    {
        foreach (var c in Model.Components.Prepend(Model.Root))
            if (c.Instance is IComponent component && component.Site == null)
                component.Site = new DesignSite(component, c.IsRoot ? Model.ClassName : c.Name ?? "");
        foreach (var c in Model.Components)
        {
            if (c.Instance is not Control control || !IsShadowable(control)) continue;
            if (!control.VisibleOwn)
            {
                c.SetShadowProperty("Visible", false);
                control.Visible = true;
            }
            if (control.ShouldSerializeEnabled())
            {
                c.SetShadowProperty("Enabled", false);
                control.Enabled = true;
            }
        }
        if (Root.ShouldSerializeEnabled())
        {
            Model.Root.SetShadowProperty("Enabled", false);
            Root.Enabled = true;
        }
        Settle(Root);
    }

    /// <summary>Tab pages and drop-downs are shown and hidden by their owners; they are not shadowed.</summary>
    private static bool IsShadowable(Control control) => control is not TabPage and not ToolStripDropDown;

    private static void Settle(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) Settle(child);
        if (control is ToolStrip strip) strip.EnsureItemLayout();
    }

    private void ApplyOne(DesignerOp op)
    {
        switch (op.Op)
        {
            case "setBounds": SetBounds(Resolve(op.Id), op); break;
            case "setForm": SetBounds(Root, op); break;
            case "setProp": SetProperty(Resolve(op.Id), Required(op.Prop, "prop"), op.Value); break;
            case "resetProp": ResetProperty(Resolve(op.Id), Required(op.Prop, "prop")); break;
            case "setItems": SetItems(Resolve(op.Id), Required(op.Prop, "prop"), op.Values ?? Array.Empty<string>(), op.Ids); break;
            case "setEvent": SetEvent(Resolve(op.Id), Required(op.Event, "event"), op.Handler); break;
            case "add": Add(op); break;
            case "remove": Remove(Resolve(op.Id)); break;
            case "setParent": SetParent(Resolve(op.Id), Resolve(op.Parent), op); break;
            case "rename": SetProperty(Resolve(op.Id), "Name", Required(op.Value, "value")); break;
            case "bringToFront": AsControl(Resolve(op.Id)).BringToFront(); break;
            case "sendToBack": AsControl(Resolve(op.Id)).SendToBack(); break;
            default: throw new DesignerEditException($"Unknown edit '{op.Op}'.");
        }
    }

    private static string Required(string? value, string name) =>
        value ?? throw new DesignerEditException($"The edit needs '{name}'.");

    private static Control AsControl(object target) =>
        target as Control ?? throw new DesignerEditException($"'{target.GetType().Name}' is not a control.");

    /// <summary>"" or the class name → the root; a field name; <c>owner.Property</c> for a nested component.</summary>
    public object Resolve(string? id)
    {
        if (string.IsNullOrEmpty(id) || id == Model.ClassName) return Root;
        var component = Model.Find(id);
        if (component != null) return component.Instance;
        int dot = id.LastIndexOf('.');
        if (dot > 0)
        {
            var owner = Resolve(id.Substring(0, dot));
            if (TypeDescriptor.GetProperties(owner)[id.Substring(dot + 1)]?.GetValue(owner) is IComponent nested) return nested;
        }
        throw new DesignerEditException($"There is no component '{id}' on the form.");
    }

    private void SetBounds(object target, DesignerOp op)
    {
        switch (target)
        {
            case Control c when ReferenceEquals(c, Root):
                c.ClientSize = new Size(op.W ?? c.ClientSize.Width, op.H ?? c.ClientSize.Height);
                break;
            case Control c:
                c.SetBounds(op.X ?? c.Left, op.Y ?? c.Top, op.W ?? c.Width, op.H ?? c.Height);
                break;
            case ToolStripItem item when op.W != null || op.H != null:
                item.Size = new Size(op.W ?? item.Width, op.H ?? item.Height);
                break;
            default:
                throw new DesignerEditException($"'{target.GetType().Name}' has no bounds to set.");
        }
    }

    /// <summary>The site of a designed component: its name, and DesignMode = true. It offers no services.</summary>
    private sealed class DesignSite : ISite
    {
        public DesignSite(IComponent component, string name) => (Component, Name) = (component, name);
        public IComponent Component { get; }
        public IContainer? Container => null;
        public bool DesignMode => true;
        public string? Name { get; set; }
        public object? GetService(Type serviceType) => null;
    }

    private static bool IsShadowed(object target, PropertyDescriptor pd, bool isRoot) =>
        pd.Name is "Visible" or "Enabled" && target is Control control && IsShadowable(control) && !(isRoot && pd.Name == "Visible");

    private void SetProperty(object target, string name, string? value)
    {
        var component = Model.FindByInstance(target);
        if (name == "Name" && component != null)
        {
            if (component.IsRoot) throw new DesignerEditException("The form is named by its class; rename the class instead.");
            var oldName = component.Name;
            try { Model.Rename(component, value ?? ""); }
            catch (ArgumentException ex) { throw new DesignerEditException(ex.Message, ex); }
            if (oldName != null && oldName != component.Name) RenameInCode(component, oldName, component.Name!);
            return;
        }

        var pd = TypeDescriptor.GetProperties(target)[name]
            ?? throw new DesignerEditException($"'{target.GetType().Name}' has no property '{name}'.");
        if (component != null && IsShadowed(target, pd, component.IsRoot))
        {
            if (!bool.TryParse(value, out var flag)) throw new DesignerEditException($"'{value}' is not a valid value for {name}.");
            if (flag) component.RemoveShadowProperty(name);
            else component.SetShadowProperty(name, false);
            return;
        }
        if (pd.IsReadOnly) throw new DesignerEditException($"{name} is read-only.");

        object? converted;
        try { converted = Convert(pd, value); }
        catch (Exception ex) when (ex is not DesignerEditException)
        {
            throw new DesignerEditException($"'{value}' is not a valid value for {name}: {Unwrap(ex).Message}", ex);
        }
        try { pd.SetValue(target, converted); }
        catch (Exception ex) { throw new DesignerEditException($"{name} cannot be set to '{value}': {Unwrap(ex).Message}", ex); }
    }

    private object? Convert(PropertyDescriptor pd, string? text)
    {
        var type = pd.PropertyType;
        if (typeof(IComponent).IsAssignableFrom(type))
        {
            if (string.IsNullOrEmpty(text)) return null;
            var component = Resolve(text);
            if (!type.IsInstanceOfType(component)) throw new DesignerEditException($"'{text}' is not a {type.Name}.");
            return component;
        }
        if (type == typeof(string)) return text ?? "";
        if (text == null) return null;
        return pd.Converter.ConvertFromInvariantString(text);
    }

    private void ResetProperty(object target, string name)
    {
        var component = Model.FindByInstance(target);
        if (component != null && component.RemoveShadowProperty(name)) return;
        var pd = TypeDescriptor.GetProperties(target)[name]
            ?? throw new DesignerEditException($"'{target.GetType().Name}' has no property '{name}'.");
        if (pd.CanResetValue(target)) pd.ResetValue(target);
        else if (pd.Attributes[typeof(DefaultValueAttribute)] is DefaultValueAttribute dv && !pd.IsReadOnly) pd.SetValue(target, dv.Value);
        else throw new DesignerEditException($"{name} has no default to go back to.");
    }

    private void SetItems(object target, string name, string[] values, string?[]? ids)
    {
        var pd = TypeDescriptor.GetProperties(target)[name]
            ?? throw new DesignerEditException($"'{target.GetType().Name}' has no property '{name}'.");
        var value = pd.GetValue(target);
        if (value is IList components && ComponentElementType(components) != null)
        {
            SetComponentItems(target, components, values, ids);
            return;
        }
        switch (value)
        {
            case TreeNodeCollection nodes:
                SetNodes(nodes, values);
                return;
            case ListView.ListViewItemCollection items:
                SetListViewItems(items, values);
                return;
            case IList list when !list.IsReadOnly:
                list.Clear();
                foreach (var v in values) list.Add(v);
                return;
            default:
                throw new DesignerEditException($"{name} is not an editable list.");
        }
    }

    /// <summary>A collection as the lines the grid's editor shows, and how to read them.</summary>
    private static (List<string> Items, string Format) CollectionText(IList list)
    {
        switch (list)
        {
            case TreeNodeCollection nodes:
            {
                var lines = new List<string>();
                void Walk(TreeNodeCollection level, int depth)
                {
                    foreach (TreeNode node in level)
                    {
                        lines.Add(new string(' ', depth * 2) + node.Text);
                        Walk(node.Nodes, depth + 1);
                    }
                }
                Walk(nodes, 0);
                return (lines, "tree");
            }
            case ListView.ListViewItemCollection items:
                return (items.Cast<ListViewItem>()
                    .Select(i => string.Join(" | ", i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text))).ToList(), "columns");
            case var _ when ComponentElementType(list) != null:
                return (list.Cast<object>().Select(ItemText).ToList(), "components");
            default:
                return (list.Cast<object?>().Select(o => o?.ToString() ?? "").ToList(), "lines");
        }
    }

    // --- collections of components: TabPages, tool strip items, columns -----------------------------

    /// <summary>
    /// The kind of component a collection holds when the designer edits its elements as components of
    /// the form (a tab page, a menu item, a column), as VS's collection editors do; null for any other list.
    /// </summary>
    private static Type? ComponentElementType(IList list) => list switch
    {
        TabControl.TabPageCollection => typeof(TabPage),
        ToolStripItemCollection => typeof(ToolStripItem),
        ListView.ColumnHeaderCollection => typeof(ColumnHeader),
        DataGridViewColumnCollection => typeof(DataGridViewColumn),
        _ => null,
    };

    /// <summary>What the collection editor shows for an element: its caption, "-" for a separator.</summary>
    private static string ItemText(object item) => item switch
    {
        ToolStripSeparator => "-",
        ToolStripItem i => i.Text ?? "",
        ColumnHeader h => h.Text ?? "",
        DataGridViewColumn c => c.HeaderText ?? "",
        Control c => c.Text ?? "",
        _ => item.ToString() ?? "",
    };

    private static void SetItemText(object item, string text)
    {
        switch (item)
        {
            case ToolStripSeparator: break;
            case ToolStripItem i: i.Text = text; break;
            case ColumnHeader h: h.Text = text; break;
            case DataGridViewColumn c: c.HeaderText = text; break;
            case Control c: c.Text = text; break;
        }
    }

    /// <summary>The type a new element of the collection is created as: what VS's collection editor adds first.</summary>
    private static Type NewElementType(object owner, IList list, string text) => list switch
    {
        TabControl.TabPageCollection => typeof(TabPage),
        ToolStripItemCollection when text.Trim() == "-" => typeof(ToolStripSeparator),
        ToolStripItemCollection => owner switch
        {
            StatusStrip => typeof(ToolStripStatusLabel),
            MenuStrip or ToolStripDropDown or ToolStripDropDownItem => typeof(ToolStripMenuItem),
            _ => typeof(ToolStripButton),
        },
        ListView.ColumnHeaderCollection => typeof(ColumnHeader),
        DataGridViewColumnCollection => typeof(DataGridViewTextBoxColumn),
        _ => throw new DesignerEditException("The designer cannot add elements to this collection."),
    };

    /// <summary>
    /// The elements of a component collection, in the order given: <paramref name="ids"/> names the
    /// element each line stands for (its name, <c>#index</c> for one without a name, null or "" for a
    /// new one), <paramref name="values"/> their captions.
    /// An element left out is removed from the form with everything in it (a tab page's controls); a new
    /// one gets the name VS gives it (<c>tabPage3</c>) and the caption typed, or its name when that is empty.
    /// Without ids (a plain list of captions), each line keeps the element that already reads the same.
    /// </summary>
    private void SetComponentItems(object owner, IList list, string[] values, string?[]? ids)
    {
        if (ids != null && ids.Length != values.Length) throw new DesignerEditException("The edit needs one id for each value.");
        var old = list.Cast<object>().ToList();
        var byText = old.GroupBy(ItemText).ToDictionary(g => g.Key, g => new Queue<object>(g));
        var used = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var wanted = new List<(object? Item, string Text)>();
        for (int i = 0; i < values.Length; i++)
        {
            var text = values[i] ?? "";
            object? item = null;
            if (ids != null)
            {
                var id = ids[i];
                if (!string.IsNullOrEmpty(id))
                {
                    // "#2": the element that was third, for one the file declares inline (it has no name yet).
                    item = id[0] == '#' && int.TryParse(id.AsSpan(1), out int at) && at >= 0 && at < old.Count ? old[at] : Resolve(id);
                    if (!old.Contains(item)) throw new DesignerEditException($"'{id}' is not in this collection.");
                }
            }
            else
            {
                if (text.Trim().Length == 0) continue;
                if (byText.TryGetValue(text, out var same)) while (same.TryDequeue(out var candidate)) if (used.Add(candidate)) { item = candidate; break; }
            }
            if (item != null && ids != null && !used.Add(item)) throw new DesignerEditException($"'{ids[i]}' is listed twice.");
            wanted.Add((item, text));
        }

        // What is gone goes first, with its children and the references to it.
        foreach (var item in old.Where(o => !used.Contains(o)).ToList()) Remove(item);

        for (int i = 0; i < wanted.Count; i++)
        {
            var (item, text) = wanted[i];
            if (item == null)
            {
                var type = NewElementType(owner, list, text);
                IComponent instance;
                try { instance = (IComponent)Activator.CreateInstance(type)!; }
                catch (Exception ex) { throw new DesignerEditException($"Creating a {type.Name} failed: {Unwrap(ex).Message}", ex); }
                var component = Model.AddComponent(instance);
                list.Insert(Math.Min(i, list.Count), instance);
                InitializeNew(instance, component, owner);
                if (text.Trim().Length > 0 && instance is not ToolStripSeparator) SetItemText(instance, text);
                wanted[i] = (instance, text);
                continue;
            }
            if (list.IndexOf(item) != i)
            {
                list.Remove(item);
                list.Insert(i, item);
            }
            if (ItemText(item) != text && item is not ToolStripSeparator) SetItemText(item, text);
        }

        if (owner is TabControl tabs)
        {
            // VS numbers the pages in their order.
            for (int i = 0; i < tabs.TabPages.Count; i++) tabs.TabPages[i].TabIndex = i;
            if (tabs.SelectedIndex < 0 && tabs.TabCount > 0) tabs.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Tree nodes from indented lines (two spaces a level). A node whose text and place are unchanged is
    /// kept as it was (its name, image, check state); a new one is named after its text, as VS names
    /// the nodes its editor adds after their text.
    /// </summary>
    private static void SetNodes(TreeNodeCollection nodes, string[] lines)
    {
        var parsed = new List<(int Depth, string Text)>();
        foreach (var raw in lines)
        {
            if (raw.Trim().Length == 0) continue;
            int indent = raw.Length - raw.TrimStart(' ').Length;
            int depth = indent / 2;
            if (depth > (parsed.Count == 0 ? 0 : parsed[^1].Depth + 1))
                throw new DesignerEditException($"'{raw.Trim()}' is indented deeper than a child of the line above.");
            parsed.Add((depth, raw.Trim()));
        }

        int index = 0;
        void Fill(TreeNodeCollection level, int depth)
        {
            var old = level.Cast<TreeNode>().ToList();
            level.Clear();
            int i = 0;
            while (index < parsed.Count && parsed[index].Depth == depth)
            {
                var text = parsed[index++].Text;
                var node = i < old.Count && old[i].Text == text ? old[i] : new TreeNode(text) { Name = text };
                level.Add(node);
                i++;
                Fill(node.Nodes, depth + 1);
            }
        }
        Fill(nodes, 0);
    }

    /// <summary>ListView items from lines of sub-items separated by '|'; an item whose line is unchanged is kept.</summary>
    private static void SetListViewItems(ListView.ListViewItemCollection items, string[] lines)
    {
        // Several items may read the same: each line takes the next unused one.
        var keep = items.Cast<ListViewItem>()
            .GroupBy(i => string.Join(" | ", i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text)))
            .ToDictionary(g => g.Key, g => new Queue<ListViewItem>(g));
        items.Clear();
        foreach (var line in lines.Where(l => l.Trim().Length > 0))
        {
            var parts = line.Split('|').Select(p => p.Trim()).ToArray();
            var normal = string.Join(" | ", parts);
            if (keep.TryGetValue(normal, out var same) && same.TryDequeue(out var item)) items.Add(item);
            else items.Add(parts.Length == 1 ? new ListViewItem(parts[0]) : new ListViewItem(parts));
        }
    }

    /// <summary>
    /// A renamed component takes its references in the form's code along, and the handlers named after
    /// it (<c>button1_Click</c> → <c>okButton_Click</c>, wherever else they are bound), when the code
    /// declares them and the new name is free.
    /// </summary>
    private void RenameInCode(DesignerComponent component, string oldName, string newName)
    {
        var renames = new Dictionary<string, string>(StringComparer.Ordinal) { [oldName] = newName };
        if (CompanionSource == null) return;
        foreach (var e in component.Events)
        {
            if (!e.HandlerName.StartsWith(oldName + "_", StringComparison.Ordinal) || renames.ContainsKey(e.HandlerName)) continue;
            var handler = newName + e.HandlerName.Substring(oldName.Length);
            if (!CodeRenamer.Declares(CompanionSource, Model.ClassName, e.HandlerName)) continue;
            if (Model.Events.Any(b => b.HandlerName == handler) || CodeRenamer.Declares(CompanionSource, Model.ClassName, handler)) continue;
            renames[e.HandlerName] = handler;
        }
        foreach (var (from, to) in renames)
            if (from != oldName) Model.RenameHandler(from, to);
        CompanionSource = CodeRenamer.Rename(CompanionSource, Source, Model.ClassName, renames);
    }

    private void SetEvent(object target, string eventName, string? handler)
    {
        var component = Model.FindByInstance(target)
            ?? throw new DesignerEditException("Only components of the form have events the designer can bind.");
        if (string.IsNullOrEmpty(handler))
        {
            Model.UnbindEvent(component, eventName);
            return;
        }
        try { Model.BindEvent(component, eventName, handler); }
        catch (ArgumentException ex) { throw new DesignerEditException(ex.Message, ex); }

        if (CompanionSource == null) return;
        var delegateType = TypeDescriptor.GetEvents(target)[eventName]?.EventType;
        if (delegateType == null) return;
        CompanionSource = EventHandlerWriter.EnsureHandler(CompanionSource, Model.ClassName, handler, delegateType, out bool created, out int line);
        _lastHandler = new DesignerHandlerLocation { File = CompanionPath ?? "", Method = handler, Line = line, Created = created };
    }

    // --- adding and removing ---------------------------------------------------------------------

    private void Add(DesignerOp op)
    {
        var type = DesignerToolbox.ResolveType(Required(op.Type, "type"))
            ?? throw new DesignerEditException($"'{op.Type}' is not a component the designer knows.");
        IComponent instance;
        try { instance = (IComponent)Activator.CreateInstance(type)!; }
        catch (Exception ex) { throw new DesignerEditException($"Creating a {type.Name} failed: {Unwrap(ex).Message}", ex); }

        DesignerComponent component;
        try { component = Model.AddComponent(instance, string.IsNullOrEmpty(op.Id) ? null : op.Id); }
        catch (ArgumentException ex) { throw new DesignerEditException(ex.Message, ex); }

        var parent = instance is Control and not ToolStripDropDown || op.Parent != null ? Resolve(op.Parent) : null;
        Place(instance, parent, op);
        InitializeNew(instance, component, parent);
    }

    /// <summary>Puts a new component where it belongs: a control into its container (on top, as VS does), an item into its strip or menu.</summary>
    private void Place(IComponent instance, object? parent, DesignerOp op)
    {
        switch (instance)
        {
            case TabPage page:
                if (parent is not TabControl tabs) throw new DesignerEditException("A TabPage goes into a TabControl.");
                page.TabIndex = tabs.TabPages.Count;
                tabs.TabPages.Add(page);
                break;
            case ToolStripDropDown:
                break; // the component tray
            case Control control:
                var container = parent as Control ?? throw new DesignerEditException("A control needs a container.");
                if (container is TabControl) throw new DesignerEditException("Only tab pages go directly into a TabControl; drop the control on a page.");
                if (container is SplitContainer) throw new DesignerEditException("Drop the control into one of the SplitContainer's panels.");
                control.TabIndex = container.Controls.Count;
                container.Controls.Add(control);
                container.Controls.SetChildIndex(control, 0);
                control.SetBounds(op.X ?? control.Left, op.Y ?? control.Top, op.W ?? control.Width, op.H ?? control.Height);
                break;
            case ToolStripItem item:
                switch (parent)
                {
                    case ToolStrip strip: strip.Items.Add(item); break;
                    case ToolStripDropDownItem owner: owner.DropDownItems.Add(item); break;
                    default: throw new DesignerEditException("A tool strip item goes into a ToolStrip, a MenuStrip, a StatusStrip or a menu item.");
                }
                break;
            case ColumnHeader header:
                (parent as ListView ?? throw new DesignerEditException("A ColumnHeader goes into a ListView.")).Columns.Add(header);
                break;
            case DataGridViewColumn column:
                (parent as DataGridView ?? throw new DesignerEditException("A column goes into a DataGridView.")).Columns.Add(column);
                break;
        }
    }

    /// <summary>What the VS designers set on a component they have just created (ControlDesigner.InitializeNewComponent and friends).</summary>
    private void InitializeNew(IComponent instance, DesignerComponent component, object? parent)
    {
        var name = component.Name!;
        switch (instance)
        {
            case TabPage page:
                page.Text = name;
                page.Padding = new Padding(3);
                page.UseVisualStyleBackColor = true;
                break;
            case TabControl tabs:
                for (int i = 0; i < 2; i++)
                {
                    var page = new TabPage();
                    var pageComponent = Model.AddComponent(page);
                    Place(page, tabs, new DesignerOp());
                    InitializeNew(page, pageComponent, tabs);
                }
                break;
            case ButtonBase button:
                button.Text = name;
                button.UseVisualStyleBackColor = true;
                if (button is CheckBox or RadioButton) button.AutoSize = true;
                break;
            case Label label:
                label.Text = name;
                label.AutoSize = true;
                break;
            case GroupBox group:
                group.Text = name;
                break;
            case ErrorProvider provider when Root is ContainerControl root:
                // ErrorProvider.Site does this through IDesignerHost.RootComponent in VS.
                provider.ContainerControl = root;
                break;
            case MenuStrip menu when Root is Form form && form.MainMenuStrip == null:
                form.MainMenuStrip = menu;
                break;
            case ToolStripItem item and not ToolStripSeparator and not ToolStripControlHost:
                item.Text = name;
                break;
        }
    }

    private void Remove(object target)
    {
        if (ReferenceEquals(target, Root)) throw new DesignerEditException("The form itself cannot be removed.");
        var removed = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Collect(target, removed);

        switch (target)
        {
            case Control control: control.Parent?.Controls.Remove(control); break;
            case ToolStripItem item: item.Owner?.Items.Remove(item); break;
            case ColumnHeader header: header.ListView?.Columns.Remove(header); break;
            case DataGridViewColumn column: column.DataGridView?.Columns.Remove(column); break;
        }

        foreach (var c in Model.Components.Where(c => removed.Contains(c.Instance)).ToList())
            Model.RemoveComponent(c);

        // Nothing may keep pointing at what is gone (MainMenuStrip, a control's ContextMenuStrip, AcceptButton).
        foreach (var c in Model.Components.Append(Model.Root))
        {
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(c.Instance))
            {
                if (pd.IsReadOnly || !typeof(IComponent).IsAssignableFrom(pd.PropertyType)) continue;
                object? value;
                try { value = pd.GetValue(c.Instance); }
                catch (Exception) { continue; }
                if (value != null && removed.Contains(value)) pd.SetValue(c.Instance, null);
            }
        }
    }

    /// <summary>The target and everything that goes with it: child controls, strip items, menu items, columns.</summary>
    private static void Collect(object target, HashSet<object> into)
    {
        if (!into.Add(target)) return;
        if (target is Control control)
            foreach (Control child in control.Controls) Collect(child, into);
        if (target is ToolStrip strip)
            foreach (ToolStripItem item in strip.Items) Collect(item, into);
        if (target is ToolStripDropDownItem { HasDropDownItems: true } menu)
            foreach (ToolStripItem item in menu.DropDownItems) Collect(item, into);
        if (target is ListView list)
            foreach (ColumnHeader header in list.Columns) Collect(header, into);
        if (target is DataGridView grid)
            foreach (DataGridViewColumn column in grid.Columns) Collect(column, into);
    }

    private void SetParent(object target, object parent, DesignerOp op)
    {
        var control = AsControl(target);
        var container = AsControl(parent);
        if (ReferenceEquals(control, container) || IsAncestor(control, container))
            throw new DesignerEditException("A control cannot be put inside itself.");
        if (container is TabControl or SplitContainer) throw new DesignerEditException($"A control cannot go directly into a {container.GetType().Name}.");
        control.Parent?.Controls.Remove(control);
        container.Controls.Add(control);
        container.Controls.SetChildIndex(control, 0);
        control.Location = new Point(op.X ?? control.Left, op.Y ?? control.Top);
    }

    private static bool IsAncestor(Control ancestor, Control control)
    {
        for (var c = control.Parent; c != null; c = c.Parent)
            if (ReferenceEquals(c, ancestor)) return true;
        return false;
    }

    private static Exception Unwrap(Exception ex)
    {
        while (ex is System.Reflection.TargetInvocationException { InnerException: { } inner }) ex = inner;
        return ex;
    }

    // --- what the client sees ----------------------------------------------------------------------

    /// <summary>The canvas and where everything is on it.</summary>
    public DesignerView Render()
    {
        Settle(Root);
        var size = Root.ClientSize;
        var view = new DesignerView
        {
            ClassName = Model.ClassName,
            RootType = Model.RootType.FullName ?? Model.RootType.Name,
            Text = Root.Text,
            Width = size.Width,
            Height = size.Height,
            CanUndo = CanUndo,
            CanRedo = CanRedo,
            Handler = _lastHandler,
        };
        _lastHandler = null;

        using (var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height)))
        {
            if (size.Width > 0 && size.Height > 0) Root.DrawToBitmap(bitmap, new Rectangle(Point.Empty, size));
            using var png = new MemoryStream();
            bitmap.Save(png, ImageFormat.Png);
            view.Png = System.Convert.ToBase64String(png.ToArray());
        }

        var ids = Ids();
        Walk(Root, "", ids, view.Items);
        foreach (var c in Model.Components)
        {
            if (c.Name == null || c.Instance is not IComponent) continue;
            bool onSurface = c.Instance is Control and not ToolStripDropDown || c.Instance is ToolStripItem or ColumnHeader or DataGridViewColumn;
            if (!onSurface) view.Tray.Add(new DesignerTrayItem { Id = c.Name, Type = c.Type.FullName ?? c.Type.Name });
        }
        return view;
    }

    /// <summary>Every object the client can address, by id: components, and the nested ones (SplitContainer.Panel1).</summary>
    private Dictionary<object, string> Ids()
    {
        var ids = new Dictionary<object, string>(ReferenceEqualityComparer.Instance) { [Root] = "" };
        foreach (var c in Model.Components)
            if (c.Name != null) ids[c.Instance] = c.Name;
        foreach (var (owner, ownerId) in ids.ToList())
        {
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(owner))
            {
                if (pd.SerializationVisibility != DesignerSerializationVisibility.Content) continue;
                object? value;
                try { value = pd.GetValue(owner); }
                catch (Exception) { continue; }
                if (value is IComponent nested and not ICollection && !ids.ContainsKey(nested))
                    ids[nested] = (ownerId.Length == 0 ? Model.ClassName : ownerId) + "." + pd.Name;
            }
        }
        return ids;
    }

    private void Walk(Control parent, string parentId, Dictionary<object, string> ids, List<DesignerViewItem> into)
    {
        // Paint order: the back of the z-order first, so the client can hit-test from the end.
        for (int i = parent.Controls.Count - 1; i >= 0; i--)
        {
            var child = parent.Controls[i];
            if (!ids.TryGetValue(child, out var id)) continue; // a part of a control (ComboBox's edit box)
            bool nested = !Model.Components.Any(c => ReferenceEquals(c.Instance, child));
            var origin = parent.PointToForm(child.Location);
            var item = new DesignerViewItem
            {
                Id = id,
                Type = child.GetType().FullName ?? child.GetType().Name,
                Parent = parentId,
                Kind = nested ? "nested" : "control",
                X = origin.X,
                Y = origin.Y,
                W = child.Width,
                H = child.Height,
                Left = child.Left,
                Top = child.Top,
                ClientX = child.PointToForm(Point.Empty).X,
                ClientY = child.PointToForm(Point.Empty).Y,
                Visible = VisibleOnCanvas(child),
                Container = child is Panel or GroupBox,
                Movable = !nested && child is not TabPage && child.Dock == DockStyle.None,
                // An auto-sized label or check box follows its text; an AutoSize button only grows (GrowOnly).
                Resizable = !nested && child is not TabPage && child.Dock != DockStyle.Fill
                    && !(child.LayoutAutoSize && child.AutoSizeModeCore == AutoSizeMode.GrowAndShrink),
                Dock = child.Dock.ToString(),
                Anchor = child.Anchor.ToString(),
                TabIndex = child.TabIndex,
                Margin = new[] { child.Margin.Left, child.Margin.Top, child.Margin.Right, child.Margin.Bottom },
                Padding = new[] { child.Padding.Left, child.Padding.Top, child.Padding.Right, child.Padding.Bottom },
            };
            if (child is TabControl tabs)
            {
                // The tab headers, so a click on one can bring its page to the front, as in VS.
                item.SelectedIndex = tabs.SelectedIndex;
                item.Tabs = new List<int[]>();
                for (int t = 0; t < tabs.TabCount; t++)
                {
                    var r = tabs.GetTabRect(t);
                    item.Tabs.Add(new[] { origin.X + r.X, origin.Y + r.Y, r.Width, r.Height });
                }
            }
            into.Add(item);

            if (child is ToolStrip strip)
            {
                foreach (ToolStripItem stripItem in strip.Items)
                {
                    if (!ids.TryGetValue(stripItem, out var itemId)) continue;
                    var b2 = stripItem.Bounds;
                    into.Add(new DesignerViewItem
                    {
                        Id = itemId,
                        Type = stripItem.GetType().FullName ?? stripItem.GetType().Name,
                        Parent = id,
                        Kind = "item",
                        X = origin.X + b2.X,
                        Y = origin.Y + b2.Y,
                        W = b2.Width,
                        H = b2.Height,
                        Left = b2.X,
                        Top = b2.Y,
                        Visible = VisibleOnCanvas(child) && stripItem.Placement == ToolStripItemPlacement.Main,
                    });
                }
            }
            Walk(child, id, ids, into);
        }
    }

    /// <summary>Shown on the canvas: visible itself and all the way up - but not counting the root, which is never shown as a window.</summary>
    private bool VisibleOnCanvas(Control control)
    {
        for (var c = control; c != null && !ReferenceEquals(c, Root); c = c.Parent)
            if (!c.VisibleOwn) return false;
        return true;
    }

    // --- property grid and events ------------------------------------------------------------------

    /// <summary>The property grid of one component: browsable properties, by category, as the VS grid lists them.</summary>
    public List<DesignerPropertyRow> GetProperties(string? id)
    {
        var target = Resolve(id);
        var component = Model.FindByInstance(target);
        var rows = new List<DesignerPropertyRow>();
        if (component is { IsRoot: false } && target is Control or ToolStripItem)
            rows.Add(new DesignerPropertyRow { Name = "Name", Category = "Design", Description = "Indicates the name used in code to identify the object.", Type = "String", Value = component.Name, Editor = "text", Modified = true });

        foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(target, new Attribute[] { BrowsableAttribute.Yes }))
        {
            if (pd.Name == "Name") continue;
            object? value;
            try { value = pd.GetValue(target); }
            catch (Exception) { continue; }
            var row = new DesignerPropertyRow
            {
                Name = pd.Name,
                Category = pd.Category,
                Description = pd.Description,
                Type = pd.PropertyType.Name,
                ReadOnly = pd.IsReadOnly,
            };

            // Visible/Enabled show the design-time value; the root's Visible too - the form is
            // never shown as a window in the designer, and VS shows it as True.
            if (component != null && (IsShadowed(target, pd, component.IsRoot) || component.IsRoot && pd.Name == "Visible"))
            {
                bool shadow = !component.ShadowProperties.TryGetValue(pd.Name, out var s) || s is not false;
                row.Value = shadow ? "True" : "False";
                row.Editor = "bool";
                row.Modified = !shadow;
                rows.Add(row);
                continue;
            }

            try { row.Modified = pd.ShouldSerializeValue(target); } catch (Exception) { }
            var type = Nullable.GetUnderlyingType(pd.PropertyType) ?? pd.PropertyType;
            if (typeof(IComponent).IsAssignableFrom(type))
            {
                row.Editor = "component";
                row.Value = value == null ? "" : Model.FindByInstance(value)?.Name ?? "";
                row.Options = new List<string> { "" };
                row.Options.AddRange(Model.Components.Where(c => c.Name != null && type.IsInstanceOfType(c.Instance) && !ReferenceEquals(c.Instance, target)).Select(c => c.Name!));
            }
            else if (value is IList list && type != typeof(string))
            {
                row.Editor = "collection";
                (row.Items, row.ItemsFormat) = CollectionText(list);
                if (row.ItemsFormat == "components")
                    row.ItemIds = list.Cast<object>().Select((o, i) => Model.FindByInstance(o)?.Name ?? "#" + i).ToList();
                row.Value = $"({list.Count} items)";
            }
            else if (pd.SerializationVisibility == DesignerSerializationVisibility.Content)
            {
                // A content object the grid cannot edit inline (DataBindings, FlatAppearance): shown, not bold unless it holds something.
                row.Editor = "readonly";
                row.Value = value is ICollection c ? $"({c.Count})" : "(" + pd.PropertyType.Name + ")";
                row.Modified = value is ICollection { Count: > 0 };
            }
            else
            {
                row.Value = Text(pd, value);
                row.Editor = pd.IsReadOnly ? "readonly"
                    : type == typeof(bool) ? "bool"
                    : type.IsEnum ? (type.IsDefined(typeof(FlagsAttribute), false) ? "flags" : "enum")
                    : type == typeof(Color) ? "color"
                    : type == typeof(Font) ? "font"
                    : IsNumber(type) ? "number"
                    : "text";
                if (type.IsEnum) row.Options = Enum.GetNames(type).ToList();
                else if (!pd.IsReadOnly && type != typeof(bool) && pd.Converter.GetStandardValuesSupported())
                {
                    var standard = pd.Converter.GetStandardValues();
                    if (standard != null && standard.Count < 200)
                    {
                        row.Options = standard.Cast<object?>().Select(v => Text(pd, v) ?? "").ToList();
                        if (row.Editor == "text" && pd.Converter.GetStandardValuesExclusive()) row.Editor = "enum";
                    }
                }
            }
            rows.Add(row);
        }
        return rows.OrderBy(r => r.Category, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    private static string? Text(PropertyDescriptor pd, object? value)
    {
        if (value == null) return null;
        if (value is string s) return s;
        try
        {
            var converter = pd.Converter;
            if (converter.CanConvertTo(typeof(string))) return converter.ConvertToInvariantString(value);
        }
        catch (Exception) { }
        return System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsNumber(Type t) =>
        t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(float)
        || t == typeof(double) || t == typeof(decimal) || t == typeof(uint) || t == typeof(ushort) || t == typeof(ulong) || t == typeof(sbyte);

    /// <summary>The Events tab of one component.</summary>
    public List<DesignerEventRow> GetEvents(string? id)
    {
        var target = Resolve(id);
        var component = Model.FindByInstance(target) ?? throw new DesignerEditException("Only components of the form have events.");
        var name = component.IsRoot ? Model.ClassName : component.Name!;
        var rows = new List<DesignerEventRow>();
        var defaultEvent = TypeDescriptor.GetDefaultEvent(target)?.Name;
        foreach (EventDescriptor ed in TypeDescriptor.GetEvents(target, new Attribute[] { BrowsableAttribute.Yes }))
        {
            rows.Add(new DesignerEventRow
            {
                Name = ed.Name,
                Category = ed.Category,
                Description = ed.Description,
                Handler = component.Events.FirstOrDefault(e => e.EventName == ed.Name && e.Path == ed.Name)?.HandlerName,
                Parameters = EventHandlerWriter.Parameters(ed.EventType),
                DefaultHandler = name + "_" + ed.Name,
                IsDefault = ed.Name == defaultEvent,
            });
        }
        return rows.OrderBy(r => r.Category, StringComparer.Ordinal).ThenBy(r => r.Name, StringComparer.Ordinal).ToList();
    }

    public void Dispose() => (_model?.Root.Instance as IDisposable)?.Dispose();
}
