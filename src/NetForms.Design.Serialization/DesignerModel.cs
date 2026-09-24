using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.CodeAnalysis.CSharp;

namespace NetForms.Design.Serialization;

/// <summary>
/// What <see cref="DesignerCodeReader"/> made of a <c>*.Designer.cs</c> file: the live objects that
/// <c>InitializeComponent()</c> builds, plus everything the objects themselves cannot remember —
/// field names, event handler names and the statements in the order they were written.
/// </summary>
/// <remarks>
/// The objects are real NetForms instances (the root is an instance of the form's <em>base</em>
/// class, as in the Visual Studio designer, because the user's class is never compiled), so the
/// canvas is painted by the same code that paints the application.
/// </remarks>
public sealed class DesignerModel
{
    private readonly List<DesignerComponent> _components = new();
    private readonly List<DesignerField> _fields = new();
    private readonly List<DesignerStatement> _statements = new();
    private readonly HashSet<string> _componentFieldNames = new(StringComparer.Ordinal);

    internal DesignerModel(string? ns, string className, string rootTypeName, DesignerComponent root, string? filePath)
    {
        Namespace = ns;
        ClassName = className;
        RootTypeName = rootTypeName;
        Root = root;
        FilePath = filePath;
    }

    /// <summary>The namespace of the designed class, or null for the global namespace.</summary>
    public string? Namespace { get; }

    /// <summary>The designed class, e.g. <c>MainForm</c>.</summary>
    public string ClassName { get; }

    /// <summary>The base class as written in source, e.g. <c>Form</c> or <c>System.Windows.Forms.Form</c>.</summary>
    public string RootTypeName { get; }

    /// <summary>The type the root was instantiated as (the resolved base class).</summary>
    public Type RootType => Root.Type;

    /// <summary>The root component: <c>this</c> inside <c>InitializeComponent</c>.</summary>
    public DesignerComponent Root { get; }

    /// <summary>The file the model was read from, if it was read from a file.</summary>
    public string? FilePath { get; }

    /// <summary>
    /// Every object <c>InitializeComponent</c> created, root excluded, in creation order: the ones
    /// stored in fields (<c>button1 = new Button()</c>) and the ones created inline
    /// (<c>Controls.Add(new Button { Text = "One" })</c>, <see cref="DesignerComponent.Name"/> null).
    /// </summary>
    public IReadOnlyList<DesignerComponent> Components => _components;

    /// <summary>Field declarations of the designed class in the designer file, in source order.</summary>
    public IReadOnlyList<DesignerField> Fields => _fields;

    /// <summary>The statements of <c>InitializeComponent</c>, in source order.</summary>
    public IReadOnlyList<DesignerStatement> Statements => _statements;

    /// <summary>Every event binding of every component, root first, then in component order.</summary>
    public IEnumerable<DesignerEventBinding> Events =>
        _components.Prepend(Root).SelectMany(c => c.Events);

    /// <summary>
    /// The fields of the file that held components when it was read. The writer rewrites or drops
    /// their declarations; every other field (<c>components</c>, the user's own) is left alone.
    /// </summary>
    internal IReadOnlyCollection<string> ComponentFieldNames => _componentFieldNames;

    /// <summary>The component stored in the named field, or the root if <paramref name="name"/> is the class name.</summary>
    public DesignerComponent? Find(string name)
    {
        if (name == ClassName) return Root;
        foreach (var c in _components)
            if (c.Name == name) return c;
        return null;
    }

    /// <summary>The component that owns <paramref name="instance"/>, if the reader created it.</summary>
    public DesignerComponent? FindByInstance(object instance)
    {
        if (ReferenceEquals(Root.Instance, instance)) return Root;
        foreach (var c in _components)
            if (ReferenceEquals(c.Instance, instance)) return c;
        return null;
    }

    internal void AddComponentCore(DesignerComponent c)
    {
        _components.Add(c);
        if (c.Name != null)
        {
            c.OriginalName = c.Name;
            _componentFieldNames.Add(c.Name);
        }
    }

    // --- editing (what the designer does to a form) -------------------------------------------------

    /// <summary>
    /// Adds a component the designer created (a control dropped from the toolbox). It gets a field
    /// named <paramref name="name"/>, or the next free <c>button1</c>, <c>button2</c>, … as the
    /// Visual Studio designer names them; a control or tool strip item also gets that name in its
    /// <c>Name</c> property. Putting a control into its parent is the caller's business, as it is
    /// the designer's: this only makes the object part of the form's code.
    /// </summary>
    public DesignerComponent AddComponent(IComponent instance, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(instance);
        if (FindByInstance(instance) != null) throw new ArgumentException("The object is already a component of this form.", nameof(instance));
        name ??= CreateUniqueName(instance.GetType());
        CheckNewName(name);
        var type = instance.GetType();
        var component = new DesignerComponent(name, type.Name, type, instance, isRoot: false, isPlaceholder: false);
        _components.Add(component);
        SetNameProperty(instance, name);
        return component;
    }

    /// <summary>Removes a component and its event bindings from the form's code. Detaching it from its parent is the caller's business.</summary>
    public void RemoveComponent(DesignerComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.IsRoot) throw new InvalidOperationException("The root component cannot be removed.");
        if (!_components.Remove(component)) throw new ArgumentException("Not a component of this form.", nameof(component));
    }

    /// <summary>Renames a component's field (and its <c>Name</c> property). Event handlers keep their names, as in the VS designer.</summary>
    public void Rename(DesignerComponent component, string newName)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.IsRoot) throw new InvalidOperationException("The root is named by its class; rename the class instead.");
        if (component.Name == newName) return;
        CheckNewName(newName);
        component.Name = newName;
        SetNameProperty(component.Instance, newName);
    }

    /// <summary>
    /// Binds <paramref name="eventName"/> of <paramref name="component"/> to the handler method
    /// <paramref name="handlerName"/>, replacing whatever the event was bound to (the Events tab of
    /// the designer shows one handler per event).
    /// </summary>
    public DesignerEventBinding BindEvent(DesignerComponent component, string eventName, string handlerName)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!SyntaxFacts.IsValidIdentifier(handlerName)) throw new ArgumentException($"'{handlerName}' is not a valid method name.", nameof(handlerName));
        if (!component.IsPlaceholder && TypeDescriptor.GetEvents(component.Instance)[eventName] == null)
            throw new ArgumentException($"'{component.Type.FullName}' has no event '{eventName}'.", nameof(eventName));
        component.RemoveEvents(eventName);
        var binding = new DesignerEventBinding(component, eventName, eventName, handlerName, 0);
        component.AddEvent(binding);
        return binding;
    }

    /// <summary>Removes the handlers of <paramref name="eventName"/>; false if there were none.</summary>
    public bool UnbindEvent(DesignerComponent component, string eventName)
    {
        ArgumentNullException.ThrowIfNull(component);
        return component.RemoveEvents(eventName) > 0;
    }

    /// <summary>Points every event bound to the handler <paramref name="oldName"/> at <paramref name="newName"/> (the method was renamed).</summary>
    public void RenameHandler(string oldName, string newName)
    {
        if (!SyntaxFacts.IsValidIdentifier(newName)) throw new ArgumentException($"'{newName}' is not a valid method name.", nameof(newName));
        foreach (var component in _components.Prepend(Root)) component.ReplaceHandler(oldName, newName);
    }

    /// <summary>
    /// The name the Visual Studio designer would give a new component of <paramref name="type"/>:
    /// the type name with a lower-case first letter and the first free number (<c>textBox1</c>).
    /// </summary>
    public string CreateUniqueName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var baseName = type.Name;
        int tick = baseName.IndexOf('`');
        if (tick >= 0) baseName = baseName.Substring(0, tick);
        baseName = char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);
        for (int i = 1; ; i++)
        {
            var candidate = baseName + i;
            if (IsNameFree(candidate)) return candidate;
        }
    }

    /// <summary>
    /// Gives every inline object (<c>Controls.Add(new Button { ... })</c>) a field name: the designer
    /// format has no inline objects, every component is a field (decision 67 in docs/PLAN.md).
    /// Returns the components that were named.
    /// </summary>
    public IReadOnlyList<DesignerComponent> NameInlineComponents()
    {
        var named = new List<DesignerComponent>();
        foreach (var c in _components)
        {
            if (!c.IsInline) continue;
            c.Name = CreateUniqueName(c.Type);
            SetNameProperty(c.Instance, c.Name);
            named.Add(c);
        }
        return named;
    }

    /// <summary>
    /// Free: not the class, not a component, and not a field of the file - except a field that held a
    /// component which has since been renamed or removed, because the writer drops that declaration.
    /// </summary>
    private bool IsNameFree(string name) =>
        name != ClassName
        && _components.All(c => c.Name != name)
        && (_componentFieldNames.Contains(name) || _fields.All(f => f.Name != name));

    private void CheckNewName(string name)
    {
        if (!SyntaxFacts.IsValidIdentifier(name) || SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None)
            throw new ArgumentException($"'{name}' is not a valid field name.", nameof(name));
        if (!IsNameFree(name)) throw new ArgumentException($"The name '{name}' is already in use.", nameof(name));
    }

    /// <summary>Controls and tool strip items carry their field name in <c>Name</c>; the designer writes it back.</summary>
    internal static void SetNameProperty(object instance, string name)
    {
        switch (instance)
        {
            case Control control: control.Name = name; break;
            case ToolStripItem item: item.Name = name; break;
        }
    }

    internal void AddField(DesignerField f) => _fields.Add(f);

    internal void AddStatement(DesignerStatement s) => _statements.Add(s);
}

/// <summary>One object built by <c>InitializeComponent</c>.</summary>
public sealed class DesignerComponent
{
    private readonly List<DesignerPropertyAssignment> _assignments = new();
    private readonly List<DesignerEventBinding> _events = new();

    internal DesignerComponent(string? name, string typeName, Type type, object instance, bool isRoot, bool isPlaceholder)
    {
        Name = name;
        TypeName = typeName;
        Type = type;
        Instance = instance;
        IsRoot = isRoot;
        IsPlaceholder = isPlaceholder;
    }

    /// <summary>The field name (<c>button1</c>); the class name for the root; null for an inline object.</summary>
    public string? Name { get; internal set; }

    /// <summary>The field name as read from the file, before any rename (null for new and inline components).</summary>
    public string? OriginalName { get; internal set; }

    /// <summary>The type as written in source (<c>Button</c>, <c>System.Windows.Forms.Timer</c>).</summary>
    public string TypeName { get; }

    /// <summary>The runtime type of <see cref="Instance"/>.</summary>
    public Type Type { get; }

    /// <summary>The live object.</summary>
    public object Instance { get; }

    public bool IsRoot { get; }

    /// <summary>True for an inline object without a field (<c>Controls.Add(new Button { ... })</c>).</summary>
    public bool IsInline => Name == null;

    /// <summary>
    /// True when the type is not known to the designer (a user control from the user's own project):
    /// <see cref="Instance"/> is then a <see cref="DesignerPlaceholder"/> that paints the type name,
    /// and assignments the placeholder cannot take are kept with <see cref="DesignerPropertyAssignment.Applied"/> false.
    /// </summary>
    public bool IsPlaceholder { get; }

    /// <summary>Property assignments on this component, in source order (<c>Location</c>, <c>Panel1.BackColor</c>, …).</summary>
    public IReadOnlyList<DesignerPropertyAssignment> Assignments => _assignments;

    /// <summary>Event bindings on this component, in source order.</summary>
    public IReadOnlyList<DesignerEventBinding> Events => _events;

    internal void AddAssignment(DesignerPropertyAssignment a) => _assignments.Add(a);

    internal void AddEvent(DesignerEventBinding e) => _events.Add(e);

    private readonly Dictionary<string, object?> _shadows = new(StringComparer.Ordinal);

    /// <summary>
    /// Design-time values kept apart from the live object, as the VS ControlDesigner keeps them: a
    /// control with <c>Visible = false</c> must still be drawn and selectable on the canvas, so the live
    /// control stays visible and the value to write lives here. The writer prefers these over the live
    /// object. Only boolean properties whose default is <c>true</c> are shadowed (Visible, Enabled).
    /// </summary>
    public IReadOnlyDictionary<string, object?> ShadowProperties => _shadows;

    public void SetShadowProperty(string name, object? value) => _shadows[name] = value;

    public bool RemoveShadowProperty(string name) => _shadows.Remove(name);

    internal void ReplaceHandler(string oldName, string newName)
    {
        for (int i = 0; i < _events.Count; i++)
            if (_events[i].HandlerName == oldName) _events[i] = _events[i] with { HandlerName = newName };
    }

    internal int RemoveEvents(string eventName) => _events.RemoveAll(e => e.EventName == eventName && e.Path == eventName);

    public override string ToString() => (Name ?? "(inline)") + " : " + TypeName;
}

/// <summary>A field of the designed class (<c>private Button button1;</c>).</summary>
public sealed record DesignerField(string Name, string TypeName, string Modifiers, int Line);

/// <summary>
/// <c>component.Path = value;</c>. <see cref="Path"/> is relative to the component
/// (<c>Location</c>, or <c>Panel1.BackColor</c> for a nested object); <see cref="Source"/> is the
/// right-hand side exactly as written.
/// </summary>
public sealed record DesignerPropertyAssignment(string Path, object? Value, string Source, int Line, bool Applied);

/// <summary><c>component.Path += handler;</c> — recorded, never subscribed: the handler lives in code that is not compiled.</summary>
public sealed record DesignerEventBinding(DesignerComponent Component, string Path, string EventName, string HandlerName, int Line);

public enum DesignerStatementKind
{
    /// <summary><c>field = new T(...);</c></summary>
    Create,
    /// <summary><c>target.Property = value;</c></summary>
    Assign,
    /// <summary><c>target.Event += handler;</c></summary>
    AttachEvent,
    /// <summary>A method call: <c>SuspendLayout()</c>, <c>Controls.Add(x)</c>, <c>SetColumnSpan(...)</c>.</summary>
    Call,
    /// <summary>A local variable: <c>var resources = new ComponentResourceManager(typeof(MainForm));</c></summary>
    Local,
}

/// <summary>One statement of <c>InitializeComponent</c>.</summary>
/// <param name="Target">The component the statement is about, or null when it addresses a local.</param>
/// <param name="Member">The member path relative to <paramref name="Target"/> (<c>Controls.Add</c>, <c>Location</c>, <c>Click</c>).</param>
/// <param name="Source">The statement text as written, without trivia.</param>
public sealed record DesignerStatement(
    DesignerStatementKind Kind,
    DesignerComponent? Target,
    string Member,
    string Source,
    int Line,
    DesignerPropertyAssignment? Assignment = null,
    DesignerEventBinding? Event = null);
