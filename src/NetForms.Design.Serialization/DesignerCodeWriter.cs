using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace NetForms.Design.Serialization;

/// <summary>
/// Writes a <see cref="DesignerModel"/> back into its <c>*.Designer.cs</c> (Ф5.2, docs/PLAN.md):
/// <c>InitializeComponent()</c> is regenerated from the live objects in the canonical order of the
/// Visual Studio designer, the field declarations follow the components, and everything else in the
/// file - usings, <c>Dispose</c>, comments, <c>#region</c> - is left exactly as it was.
/// </summary>
/// <remarks>
/// <para>
/// What gets written is decided the way the VS serializer decides it: a property is written when its
/// <see cref="PropertyDescriptor.ShouldSerializeValue"/> says so (hence the <c>ShouldSerializeXxx</c>
/// methods on the controls, checked against WinForms by <c>SerializationDiffTests</c>), values are
/// spelled through their <see cref="TypeConverter"/>'s <see cref="InstanceDescriptor"/>, content
/// collections become <c>Add</c>/<c>AddRange</c>, extender properties become <c>provider.SetXxx(control, value)</c>.
/// On top of that come the few rules of the VS ControlDesigner: a control's Location and Size are
/// always written, the root's Visible never is.
/// </para>
/// <para>
/// Names, event handlers and the assignments of placeholder components (types the designer could not
/// load) come from the model; the latter are written back verbatim, so nothing the reader could not
/// interpret is lost.
/// </para>
/// </remarks>
public sealed class DesignerCodeWriter
{
    /// <summary>
    /// The usings a WinForms project gets implicitly (<c>ImplicitUsings</c> + <c>UseWindowsForms</c>).
    /// A type is written by its short name only if these, plus the file's own usings, make it
    /// unambiguous - which is why <c>System.Windows.Forms.Timer</c> stays qualified (System.Threading.Timer).
    /// </summary>
    private static readonly string[] s_implicitUsings =
    {
        "System", "System.Collections.Generic", "System.Drawing", "System.IO", "System.Linq",
        "System.Net.Http", "System.Threading", "System.Threading.Tasks", "System.Windows.Forms",
    };

    private readonly List<Assembly> _assemblies;
    private TypeResolver? _types;

    public DesignerCodeWriter() : this(TypeResolver.DefaultAssemblies()) { }

    public DesignerCodeWriter(IEnumerable<Assembly> assemblies)
    {
        _assemblies = assemblies.ToList();
    }

    /// <summary>Rewrites the file the model was read from.</summary>
    public void WriteFile(DesignerModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var path = model.FilePath ?? throw new InvalidOperationException("The model was not read from a file; use Write(model, source).");
        var source = File.ReadAllText(path);
        var written = Write(model, source);
        if (written != source) File.WriteAllText(path, written, new UTF8Encoding(encoderShouldEmitUTF8Identifier: HasBom(path)));
    }

    private static bool HasBom(string path)
    {
        var head = new byte[3];
        using var stream = File.OpenRead(path);
        return stream.Read(head, 0, 3) == 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF;
    }

    /// <summary>
    /// <paramref name="designerSource"/> with <c>InitializeComponent</c> and the component fields
    /// rewritten from <paramref name="model"/>. Inline objects of the model get field names first
    /// (<see cref="DesignerModel.NameInlineComponents"/>): the designer format has no other kind.
    /// </summary>
    public string Write(DesignerModel model, string designerSource)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(designerSource);
        _types ??= new TypeResolver(_assemblies);

        var tree = CSharpSyntaxTree.ParseText(designerSource);
        var root = tree.GetRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent" && m.ParameterList.Parameters.Count == 0 && m.Body != null)
            ?? throw new DesignerCodeException("The file has no InitializeComponent() method.", model.FilePath, 1, 1);
        var cls = (ClassDeclarationSyntax)method.Parent!;

        // A localizable form keeps its properties in the .resx, one per culture, set by ApplyResources;
        // writing it means writing .resx files, which the writer does not do (yet). Refuse rather than
        // turn the form into a non-localizable one by dropping the calls.
        var localized = model.Statements.FirstOrDefault(s => s.Kind == DesignerStatementKind.Call && s.Source.Contains(".ApplyResources(", StringComparison.Ordinal));
        if (localized != null)
            throw new NotSupportedException("This is a localizable form (Localizable = true): its properties live in the .resx, set by " +
                "resources.ApplyResources(...), and the designer does not write .resx files yet. It can be viewed here; edit it as text or in Visual Studio.");

        model.NameInlineComponents();
        var style = DesignerCodeStyle.Detect(designerSource, method);
        var namer = new TypeNamer(_types, style.Classic, VisibleNamespaces(cls));
        var body = new InitializeComponentBuilder(model, style, namer).Build();

        var edits = new List<(TextSpan Span, string Text)>();

        // The method body: everything between the braces.
        var close = method.Body!.CloseBraceToken;
        var bodyText = new StringBuilder(style.NewLine);
        foreach (var statement in body)
            foreach (var line in statement.Split('\n'))
                bodyText.Append(style.Indent).Append(line).Append(style.NewLine);
        bodyText.Append(DesignerCodeStyle.LeadingWhitespace(designerSource, close.SpanStart));
        edits.Add((TextSpan.FromBounds(method.Body.OpenBraceToken.Span.End, close.SpanStart), bodyText.ToString()));

        edits.AddRange(FieldEdits(model, cls, method, designerSource, style, namer));

        var result = new StringBuilder(designerSource);
        foreach (var (span, text) in edits.OrderByDescending(e => e.Span.Start))
        {
            result.Remove(span.Start, span.Length);
            result.Insert(span.Start, text);
        }
        return result.ToString();
    }

    private static List<string> VisibleNamespaces(ClassDeclarationSyntax cls)
    {
        var result = new List<string>(s_implicitUsings);
        foreach (var node in cls.Ancestors())
        {
            var usings = node switch
            {
                CompilationUnitSyntax cu => cu.Usings,
                BaseNamespaceDeclarationSyntax nd => nd.Usings,
                _ => default,
            };
            foreach (var u in usings)
                if (u.Name != null && u.Alias == null && u.StaticKeyword == default) result.Add(TypeResolver.Dotted(u.Name));
            if (node is BaseNamespaceDeclarationSyntax ns)
            {
                var name = ns.Name.ToString();
                while (name.Length > 0)
                {
                    result.Add(name);
                    int dot = name.LastIndexOf('.');
                    name = dot < 0 ? "" : name.Substring(0, dot);
                }
            }
        }
        return result.Distinct().ToList();
    }

    // --- fields ----------------------------------------------------------------------------------

    /// <summary>
    /// Field declarations follow the components: a renamed component renames its field, a removed one
    /// loses it, a new one gets <c>private Button button2;</c> after the other component fields. Fields
    /// that never held a component (<c>components</c>, the user's own) are not touched.
    /// </summary>
    private static IEnumerable<(TextSpan, string)> FieldEdits(DesignerModel model, ClassDeclarationSyntax cls, MethodDeclarationSyntax method,
        string source, DesignerCodeStyle style, TypeNamer namer)
    {
        var edits = new List<(TextSpan, string)>();
        var byOriginal = model.Components.Where(c => c.OriginalName != null).ToDictionary(c => c.OriginalName!, StringComparer.Ordinal);
        FieldDeclarationSyntax? lastComponentField = null, lastField = null;

        foreach (var field in cls.Members.OfType<FieldDeclarationSyntax>())
        {
            lastField = field;
            var variables = field.Declaration.Variables;
            if (!variables.Any(v => model.ComponentFieldNames.Contains(v.Identifier.ValueText))) continue;
            lastComponentField = field;

            var kept = new List<VariableDeclaratorSyntax>();
            foreach (var v in variables)
            {
                var name = v.Identifier.ValueText;
                if (!model.ComponentFieldNames.Contains(name)) { kept.Add(v); continue; }
                if (!byOriginal.TryGetValue(name, out var component)) continue;          // removed
                kept.Add(component.Name == name ? v : v.WithIdentifier(SyntaxFactory.Identifier(component.Name!)));
            }
            if (kept.Count == variables.Count && kept.SequenceEqual(variables)) continue;
            if (kept.Count == 0)
            {
                edits.Add((WholeLines(source, field.Span), ""));
            }
            else
            {
                var declaration = field.Declaration.WithVariables(SyntaxFactory.SeparatedList(kept));
                edits.Add((field.Declaration.Span, declaration.ToString()));
            }
        }

        var newFields = model.Components.Where(c => c.Name != null && c.OriginalName == null).ToList();
        if (newFields.Count > 0)
        {
            var anchor = (SyntaxNode?)lastComponentField ?? lastField;
            string indent;
            int insertAt;
            var text = new StringBuilder();
            if (anchor != null)
            {
                indent = DesignerCodeStyle.LeadingWhitespace(source, anchor.SpanStart);
                insertAt = LineEnd(source, anchor.Span.End);
            }
            else
            {
                // No fields yet: after the #endregion that closes InitializeComponent, or after the method,
                // separated by a blank line.
                indent = DesignerCodeStyle.LeadingWhitespace(source, method.SpanStart);
                var endRegion = cls.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndRegionDirectiveTrivia) && t.SpanStart > method.Span.End);
                insertAt = LineEnd(source, endRegion != default ? endRegion.Span.End : method.Span.End);
                text.Append(style.NewLine);
            }
            foreach (var c in newFields)
                text.Append(indent).Append("private ").Append(c.IsPlaceholder ? c.TypeName : namer.Name(c.Type)).Append(' ').Append(c.Name).Append(';').Append(style.NewLine);
            edits.Add((new TextSpan(insertAt, 0), text.ToString()));
        }
        return edits;
    }

    /// <summary>The position just after the line break that ends the line containing <paramref name="position"/>.</summary>
    private static int LineEnd(string source, int position)
    {
        int nl = source.IndexOf('\n', position);
        return nl < 0 ? source.Length : nl + 1;
    }

    /// <summary>A span widened to whole lines, so deleting it leaves no blank line behind.</summary>
    private static TextSpan WholeLines(string source, TextSpan span)
    {
        int start = source.LastIndexOf('\n', Math.Max(0, span.Start - 1)) + 1;
        if (source.Substring(start, span.Start - start).Trim().Length != 0) start = span.Start;
        return TextSpan.FromBounds(start, LineEnd(source, Math.Max(span.Start, span.End - 1)));
    }

    // =============================================================================================

    /// <summary>Spells type names: short where the project's usings make that unambiguous, else qualified.</summary>
    private sealed class TypeNamer
    {
        private readonly TypeResolver _types;
        private readonly bool _classic;
        private readonly List<string> _namespaces;

        public TypeNamer(TypeResolver types, bool classic, List<string> namespaces)
        {
            _types = types;
            _classic = classic;
            _namespaces = namespaces;
        }

        public string Name(Type type)
        {
            var keyword = Keyword(type);
            if (keyword != null) return keyword;
            if (type.IsArray) return Name(type.GetElementType()!) + "[]";
            var full = (type.FullName ?? type.Name).Replace('+', '.');
            if (_classic || type.IsGenericType || type.Namespace == null) return full;
            var relative = full.Substring(type.Namespace.Length + 1);
            var head = relative.Split('.')[0];
            int visible = _namespaces.Count(ns => _types.ResolveFullName(ns + "." + head) != null);
            return visible == 1 && _namespaces.Contains(type.Namespace) ? relative : full;
        }

        private static string? Keyword(Type t) =>
            t == typeof(int) ? "int" : t == typeof(string) ? "string" : t == typeof(object) ? "object" : t == typeof(bool) ? "bool"
            : t == typeof(byte) ? "byte" : t == typeof(sbyte) ? "sbyte" : t == typeof(short) ? "short" : t == typeof(ushort) ? "ushort"
            : t == typeof(uint) ? "uint" : t == typeof(long) ? "long" : t == typeof(ulong) ? "ulong" : t == typeof(float) ? "float"
            : t == typeof(double) ? "double" : t == typeof(decimal) ? "decimal" : t == typeof(char) ? "char" : null;
    }

    // =============================================================================================

    /// <summary>One statement of a component block and the key it is sorted by.</summary>
    private sealed record Item(int Group, string Key, int Order, string Text);

    private sealed class Nested
    {
        public required object Owner;
        public required string Property;
        public required object Instance;
        public required string Header;
    }

    /// <summary>Generates the statements of <c>InitializeComponent</c>, in the order the VS designer writes them.</summary>
    private sealed class InitializeComponentBuilder
    {
        private readonly DesignerModel _model;
        private readonly DesignerCodeStyle _style;
        private readonly TypeNamer _namer;
        private readonly Dictionary<object, DesignerComponent> _components = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<object, Nested> _nested = new(ReferenceEqualityComparer.Instance);
        private readonly List<IExtenderProvider> _providers = new();
        private readonly List<DesignerComponent> _ordered;

        // Objects VS writes as local variables (TreeNode treeNode1 = new TreeNode("Node1");).
        private readonly Dictionary<object, string> _objectLocals = new(ReferenceEqualityComparer.Instance);
        private readonly List<string> _localDeclarations = new();
        private readonly Dictionary<string, int> _localCounters = new(StringComparer.Ordinal);
        private HashSet<string>? _takenNames;

        public InitializeComponentBuilder(DesignerModel model, DesignerCodeStyle style, TypeNamer namer)
        {
            _model = model;
            _style = style;
            _namer = namer;
            // The container (components = new Container()) is written by its own rule, not as a component.
            _ordered = model.Components.Where(c => c.Name != null && c.Instance is IComponent).ToList();
            foreach (var c in _ordered) _components[c.Instance] = c;
            _components[model.Root.Instance] = model.Root;
            foreach (var c in _ordered.Append(model.Root))
            {
                FindNested(c.Instance, c.Name!);
                if (c.Instance is IExtenderProvider p) _providers.Add(p);
            }
        }

        private bool Classic => _style.Classic;

        public List<string> Build()
        {
            var statements = new List<string>();

            // The blocks first: writing them declares the locals (tree nodes, list items, cell styles)
            // that go on top of the method.
            var blocks = _ordered.Append(_model.Root).SelectMany(Block).ToList();

            // Locals the file declared that the writer does not regenerate (ComponentResourceManager): kept, first.
            foreach (var local in KeptLocals()) statements.Add(local);
            statements.AddRange(_localDeclarations);

            bool usesContainer = _model.Fields.Any(f => f.Name == "components") && _ordered.Any(UsesContainer);
            if (usesContainer) statements.Add($"{Member(_model.Root.Instance, "components")} = new {_namer.Name(typeof(Container))}();");

            foreach (var c in _ordered) statements.Add(Creation(c));

            foreach (var c in _ordered.Append(_model.Root)) statements.AddRange(BeginStatements(c));
            statements.AddRange(blocks);
            foreach (var c in _ordered.Append(_model.Root)) statements.AddRange(EndStatements(c));
            return statements;
        }

        // --- references ---------------------------------------------------------------------------

        /// <summary>How a statement addresses the object: <c>button1</c> / <c>this.button1</c>; the root is implicit in modern code.</summary>
        private string Target(object instance)
        {
            if (ReferenceEquals(instance, _model.Root.Instance)) return Classic ? "this" : "";
            if (_components.TryGetValue(instance, out var c)) return Classic ? "this." + c.Name : c.Name!;
            if (_nested.TryGetValue(instance, out var n)) return Member(n.Owner, n.Property);
            throw new InvalidOperationException("Not a component of the form: " + instance);
        }

        private string Member(object owner, string member)
        {
            var target = Target(owner);
            return target.Length == 0 ? member : target + "." + member;
        }

        /// <summary>A component used as a value (<c>MainMenuStrip = menuStrip1</c>); null for anything else.</summary>
        private string? Reference(object value)
        {
            if (ReferenceEquals(value, _model.Root.Instance)) return "this";
            if (_objectLocals.TryGetValue(value, out var local)) return local;
            if (_components.ContainsKey(value) || _nested.ContainsKey(value)) return Target(value);
            return null;
        }

        private bool IsKnown(object instance) => _components.ContainsKey(instance) || _nested.ContainsKey(instance);

        /// <summary>SplitContainer.Panel1 and the like: components that belong to a component and are written inside its block.</summary>
        private void FindNested(object owner, string ownerName)
        {
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(owner))
            {
                if (pd.SerializationVisibility != DesignerSerializationVisibility.Content) continue;
                object? value;
                try { value = pd.GetValue(owner); }
                catch (Exception) { continue; }
                if (value is not IComponent nested || value is ICollection || _components.ContainsKey(nested) || _nested.ContainsKey(nested)) continue;
                _nested[nested] = new Nested { Owner = owner, Property = pd.Name, Instance = nested, Header = ownerName + "." + pd.Name };
            }
        }

        // --- creation, BeginInit / SuspendLayout, EndInit / ResumeLayout ----------------------------

        private static bool UsesContainer(DesignerComponent c) =>
            !c.IsPlaceholder && c.Type.GetConstructor(new[] { typeof(IContainer) }) != null;

        private string Creation(DesignerComponent c)
        {
            string type = c.IsPlaceholder ? c.TypeName : _namer.Name(c.Type);
            string args = UsesContainer(c) && _model.Fields.Any(f => f.Name == "components") ? Member(_model.Root.Instance, "components") : "";
            return $"{Target(c.Instance)} = new {type}({args});";
        }

        private string SupportInitialize(object instance, string method)
        {
            var iface = _namer.Name(typeof(ISupportInitialize));
            return Classic
                ? $"(({iface})({Target(instance)})).{method}();"
                : $"(({iface}){Target(instance)}).{method}();";
        }

        private IEnumerable<object> NestedOf(object owner) =>
            _nested.Values.Where(n => ReferenceEquals(n.Owner, owner)).Select(n => n.Instance);

        private IEnumerable<string> BeginStatements(DesignerComponent c)
        {
            if (!c.IsRoot && c.Instance is ISupportInitialize) yield return SupportInitialize(c.Instance, "BeginInit");
            foreach (var s in PlaceholderCalls(c, "BeginInit")) yield return s;
            foreach (var nested in NestedOf(c.Instance))
                if (nested is Control nc && HasLayoutChildren(nc)) yield return $"{Member(nested, "SuspendLayout")}();";
            if (c.Instance is Control control && HasLayoutChildren(control)) yield return $"{Member(c.Instance, "SuspendLayout")}();";
        }

        private IEnumerable<string> EndStatements(DesignerComponent c)
        {
            foreach (var nested in NestedOf(c.Instance))
                if (nested is Control nc && HasLayoutChildren(nc))
                    foreach (var s in Resume(nc)) yield return s;
            if (!c.IsRoot && c.Instance is ISupportInitialize) yield return SupportInitialize(c.Instance, "EndInit");
            foreach (var s in PlaceholderCalls(c, "EndInit")) yield return s;
            if (c.Instance is Control control && HasLayoutChildren(control))
                foreach (var s in Resume(control)) yield return s;
        }

        private IEnumerable<string> Resume(Control control)
        {
            yield return $"{Member(control, "ResumeLayout")}(false);";
            if (NeedsPerformLayout(control)) yield return $"{Member(control, "PerformLayout")}();";
        }

        /// <summary>A placeholder cannot say whether it implements ISupportInitialize; the file's own calls are kept.</summary>
        private IEnumerable<string> PlaceholderCalls(DesignerComponent c, string member) =>
            c.IsPlaceholder
                ? _model.Statements.Where(s => s.Kind == DesignerStatementKind.Call && s.Target == c && s.Member == member).Select(s => s.Source)
                : Enumerable.Empty<string>();

        /// <summary>SuspendLayout/ResumeLayout wrap a control whose children the code adds (or a strip with items).</summary>
        private bool HasLayoutChildren(Control control)
        {
            if (control is ToolStrip strip) return strip.Items.Cast<ToolStripItem>().Any(IsKnown);
            foreach (Control child in control.Controls)
                if (IsKnown(child)) return true;
            return false;
        }

        /// <summary>PerformLayout follows ResumeLayout when an auto-sized child must be measured (ControlCodeDomSerializer).</summary>
        private bool NeedsPerformLayout(Control control)
        {
            if (control is ToolStrip) return true;
            foreach (Control child in control.Controls)
                if (IsKnown(child) && child.AutoSize) return true;
            return false;
        }

        // --- component blocks ---------------------------------------------------------------------

        private IEnumerable<string> Block(DesignerComponent c)
        {
            var items = new List<Item>();
            Properties(c.Instance, c, items, topLevel: true);
            Extenders(c.Instance, items);

            if (c.Instance is Control or ToolStripItem)
                Add(items, 1, "Name", $"{Member(c.Instance, "Name")} = {Literal(c.Name!)};");

            if (c.IsPlaceholder)
            {
                foreach (var a in c.Assignments.Where(a => !a.Applied))
                    Add(items, 1, a.Path, $"{Member(c.Instance, a.Path)} = {a.Source};");
                foreach (var s in _model.Statements.Where(s => s.Kind == DesignerStatementKind.Call && s.Target == c
                             && s.Member is not ("BeginInit" or "EndInit" or "SuspendLayout" or "ResumeLayout" or "PerformLayout")))
                    Add(items, 1, s.Member, s.Source);
            }

            // ImageListCodeDomSerializer: the images are in ImageStream (the .resx); their keys follow the properties.
            if (c.Instance is ImageList { Images.Count: > 0 } list)
            {
                var keys = list.Images.Keys;
                for (int i = 0; i < keys.Count; i++)
                {
                    if (keys[i] is string key && key.Length > 0)
                        Add(items, 2, "", $"{Member(c.Instance, "Images")}.SetKeyName({i.ToString(CultureInfo.InvariantCulture)}, {Literal(key)});");
                }
            }

            foreach (var e in c.Events.OrderBy(e => e.EventName, StringComparer.Ordinal))
                Add(items, 3, e.EventName, EventStatement(c, e));

            if (items.Count == 0) yield break;
            foreach (var line in Header(c.IsRoot ? _model.ClassName : c.Name!)) yield return line;
            foreach (var item in Sort(items)) yield return item.Text;
        }

        private IEnumerable<string> Header(string name)
        {
            yield return _style.CommentLine;
            yield return "// " + name;
            yield return _style.CommentLine;
        }

        private static void Add(List<Item> items, int group, string key, string text) => items.Add(new Item(group, key, items.Count, text));

        /// <summary>By group, then by property name the way PropertyDescriptorCollection.Sort orders them (culture-aware, case second).</summary>
        private static IEnumerable<Item> Sort(List<Item> items) =>
            items.OrderBy(i => i.Group).ThenBy(i => i.Key, StringComparer.InvariantCulture).ThenBy(i => i.Order);

        /// <summary>The properties of one object, as statements on <paramref name="instance"/>.</summary>
        private void Properties(object instance, DesignerComponent? component, List<Item> items, bool topLevel)
        {
            bool isRoot = ReferenceEquals(instance, _model.Root.Instance);
            bool isControl = instance is Control;

            // In name order, as the VS serializer visits them: that order also numbers the locals
            // (the groups of a ListView exist before the items that refer to them).
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(instance).Sort())
            {
                if (pd.Name == "Name" && instance is Control or ToolStripItem) continue;
                var visibility = pd.SerializationVisibility;
                if (visibility == DesignerSerializationVisibility.Hidden) continue;

                object? value;
                try { value = pd.GetValue(instance); }
                catch (Exception) { continue; }

                if (visibility == DesignerSerializationVisibility.Content)
                {
                    Content(instance, component, pd, value, items);
                    continue;
                }
                if (pd.IsReadOnly) continue;

                // A design-time shadow (Visible = false kept apart from the visible live control) wins.
                if (topLevel && component != null && component.ShadowProperties.TryGetValue(pd.Name, out var shadow))
                {
                    if (shadow is true || pd.Name == "Visible" && (isRoot || instance is TabPage or ToolStripDropDown)) continue;
                    Add(items, 1, pd.Name, $"{Member(instance, pd.Name)} = {Expression(shadow, pd.PropertyType) ?? throw Unwritable(instance, pd.Name, shadow)};");
                    continue;
                }

                // The VS ControlDesigner's rules on top of the component's own answer: a control on the
                // design surface always has its Location and Size written (a drop-down lives in the
                // component tray and has not), and Visible is a design-time shadow - never written for
                // the root, nor for a tab page or a drop-down, which their owners show and hide.
                bool force = false;
                if (isControl && topLevel && !isRoot && instance is not ToolStripDropDown && pd.Name is "Location" or "Size") force = true;
                if (pd.Name == "Visible" && (isRoot || instance is TabPage or ToolStripDropDown)) continue;
                // The root's Location is the DocumentDesigner's shadow, whose default is (0, 0): a new
                // UserControl has none written, as a Form has none. Nor has a root a TabIndex: the root
                // is nobody's tab stop while it is designed, and VS writes none for a UserControl.
                if (isRoot && pd.Name == "Location" && value is Point { IsEmpty: true }) continue;
                if (isRoot && pd.Name == "TabIndex") continue;
                // Values WinForms reads back from the native control once its handle exists - and in the
                // VS designer it does: an unset TreeView.LineColor is the default there, never written.
                if (instance is TreeView && pd.Name == "LineColor" && value is Color { IsEmpty: true }) continue;
                // DataGridViewColumnDesigner: a column's Width is written only when it is not the default 100
                // (checked with the real VS serializer, decision 111).
                if (instance is DataGridViewColumn && pd.Name == "Width" && value is 100) continue;
                // ImageListDesigner writes ColorDepth even when the images (and so the depth) are in ImageStream,
                // where the list itself would not (ShouldSerializeColorDepth: empty lists only) - seen in VS output.
                if (instance is ImageList && pd.Name == "ColorDepth") force = true;
                // RichTextBoxDesigner shadows Text without a default, so VS writes it always - "" too (every
                // RichTextBox of a VS form has its Text = ""), where the control's own Text would not be.
                if (instance is RichTextBox && pd.Name == "Text") force = true;
                if (!force && !ShouldSerialize(pd, instance)) continue;
                // A fresh PrintPreviewDialog asks for its Icon (VS puts it in the .resx, which the designer does not
                // write yet): keep the line a file already has, write none for a new one.
                if (instance is PrintPreviewDialog && pd.Name == "Icon" && OriginalSource(component, pd.Name, value) == null) continue;

                var prelude = new List<string>();
                var expression = Expression(value, pd.PropertyType)
                    ?? (value is DataGridViewCellStyle ? LocalFor(value, prelude) : null)
                    ?? OriginalSource(component, pd.Name, value)
                    ?? throw Unwritable(instance, pd.Name, value);
                Add(items, 1, pd.Name, string.Join("\n", prelude.Append($"{Member(instance, pd.Name)} = {expression};")));
            }
            if (isRoot && instance is ContainerControl container) AutoScale(container, items);
        }

        /// <summary>
        /// AutoScaleDimensions and AutoScaleMode are hidden from ordinary serialization in WinForms; the
        /// ContainerControl serializer writes them first in the root's block, since they must be set
        /// before anything is scaled.
        /// </summary>
        private void AutoScale(ContainerControl root, List<Item> items)
        {
            if (root.AutoScaleMode == AutoScaleMode.Inherit) return;
            if (root.AutoScaleDimensions != SizeF.Empty)
                Add(items, 0, "0", $"{Member(root, "AutoScaleDimensions")} = {Expression(root.AutoScaleDimensions, typeof(SizeF))};");
            Add(items, 0, "1", $"{Member(root, "AutoScaleMode")} = {Expression(root.AutoScaleMode, typeof(AutoScaleMode))};");
        }

        private static bool ShouldSerialize(PropertyDescriptor pd, object instance)
        {
            try { return pd.ShouldSerializeValue(instance); }
            catch (Exception) { return false; }
        }

        /// <summary>A Content property: a collection, a nested component, or an object whose own properties are written.</summary>
        private void Content(object owner, DesignerComponent? component, PropertyDescriptor pd, object? value, List<Item> items)
        {
            if (value == null) return;
            if (value is IComponent nested && _nested.TryGetValue(nested, out var info))
            {
                var inner = new List<Item>();
                Properties(nested, null, inner, topLevel: false);
                if (inner.Count == 0) return;
                var text = string.Join("\n", Header(info.Header).Concat(Sort(inner).Select(i => i.Text)));
                Add(items, 1, pd.Name, text);
                return;
            }
            if (value is IComponent) return; // a component of its own: written in its own block
            if (value is IEnumerable collection && value is not string)
            {
                var text = Collection(owner, component, pd, collection);
                if (text != null) Add(items, 1, pd.Name, text);
                return;
            }
            // An object inside the component (Button.FlatAppearance): its properties, addressed through it.
            var sub = new List<Item>();
            foreach (PropertyDescriptor inner in TypeDescriptor.GetProperties(value))
            {
                if (inner.SerializationVisibility != DesignerSerializationVisibility.Visible || inner.IsReadOnly) continue;
                if (!ShouldSerialize(inner, value)) continue;
                object? innerValue;
                try { innerValue = inner.GetValue(value); }
                catch (Exception) { continue; }
                var path = pd.Name + "." + inner.Name;
                var prelude = new List<string>();
                var expression = Expression(innerValue, inner.PropertyType)
                    ?? (innerValue is DataGridViewCellStyle ? LocalFor(innerValue, prelude) : null)
                    ?? OriginalSource(component, path, innerValue)
                    ?? throw Unwritable(owner, path, innerValue);
                Add(sub, 1, inner.Name, string.Join("\n", prelude.Append($"{Member(owner, path)} = {expression};")));
            }
            if (sub.Count > 0) Add(items, 1, pd.Name, string.Join("\n", Sort(sub).Select(i => i.Text)));
        }

        /// <summary>
        /// A content collection. Controls are added one by one (in a TableLayoutPanel at their cell),
        /// tool strip items with one AddRange, anything else with AddRange where the collection has one
        /// (TableLayoutStyleCollection is written with Add, as VS does). Children that are not components
        /// of the form (the edit box inside a ComboBox) are not the code's business.
        /// </summary>
        private string? Collection(object owner, DesignerComponent? component, PropertyDescriptor pd, IEnumerable collection)
        {
            var target = Member(owner, pd.Name);
            if (collection is Control.ControlCollection controls)
            {
                var lines = new List<string>();
                foreach (Control child in controls)
                {
                    if (!IsKnown(child)) continue;
                    if (owner is TableLayoutPanel table && (table.GetColumn(child) != -1 || table.GetRow(child) != -1))
                        lines.Add($"{target}.Add({Target(child)}, {table.GetColumn(child)}, {table.GetRow(child)});");
                    else
                        lines.Add($"{target}.Add({Target(child)});");
                }
                return lines.Count == 0 ? null : string.Join("\n", lines);
            }

            var elements = collection.Cast<object?>().ToList();
            if (elements.Count == 0) return null;
            if (collection is ToolStripItemCollection)
            {
                elements = elements.Where(e => e != null && IsKnown(e)).ToList();
                if (elements.Count == 0) return null;
            }

            var type = collection.GetType();
            var addRange = collection is TableLayoutStyleCollection ? null
                : type.GetMethods().FirstOrDefault(m => m.Name == "AddRange" && m.GetParameters() is [{ ParameterType.IsArray: true }]);
            var add = type.GetMethods().Where(m => m.Name == "Add" && m.GetParameters().Length == 1)
                .OrderBy(m => m.GetParameters()[0].ParameterType == typeof(object) ? 1 : 0).FirstOrDefault();
            var elementType = addRange?.GetParameters()[0].ParameterType.GetElementType() ?? add?.GetParameters()[0].ParameterType ?? typeof(object);

            var expressions = new List<string>();
            var prelude = new List<string>();
            foreach (var element in elements)
            {
                var e = Expression(element, elementType) ?? LocalFor(element, prelude);
                if (e == null)
                {
                    // Nothing we can spell (a ListViewItem, a TreeNode): keep what the file said, if it said it.
                    var original = _model.Statements.Where(s => s.Target == component && component != null
                        && s.Member.StartsWith(pd.Name + ".", StringComparison.Ordinal)).Select(s => s.Source).ToList();
                    if (original.Count > 0) return string.Join("\n", original);
                    throw Unwritable(owner, pd.Name, element);
                }
                expressions.Add(e);
            }

            if (addRange != null)
            {
                var arrayType = _namer.Name(elementType);
                if (Classic)
                    return string.Join("\n", prelude.Append($"{target}.AddRange(new {arrayType}[] {{\n" + string.Join(",\n", expressions) + "});"));
                return string.Join("\n", prelude.Append($"{target}.AddRange(new {arrayType}[] {{ {string.Join(", ", expressions)} }});"));
            }
            if (add == null) throw Unwritable(owner, pd.Name, collection);
            return string.Join("\n", prelude.Concat(expressions.Select(e => $"{target}.Add({e});")));
        }

        // --- objects written as locals ---------------------------------------------------------------

        /// <summary>The types VS writes as locals; their declarations in the file are regenerated, not kept.</summary>
        private static readonly HashSet<string> s_localTypes = new(StringComparer.Ordinal)
        {
            nameof(TreeNode), nameof(ListViewItem), nameof(ListViewGroup), nameof(DataGridViewCellStyle),
        };

        /// <summary>The file's locals, except those of the types written as locals (they are written anew).</summary>
        private IEnumerable<string> KeptLocals()
        {
            var dropped = RegeneratedLocals();
            return _model.Statements.Where(s => s.Kind == DesignerStatementKind.Local && !dropped.Contains(s.Member)).Select(s => s.Source);
        }

        private HashSet<string>? _regenerated;

        /// <summary>The names of the file's locals whose type the writer writes as locals itself.</summary>
        private HashSet<string> RegeneratedLocals()
        {
            if (_regenerated != null) return _regenerated;
            var dropped = _regenerated = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in _model.Statements.Where(s => s.Kind == DesignerStatementKind.Local))
            {
                if (dropped.Contains(s.Member)) continue;
                if (SyntaxFactory.ParseStatement(s.Source) is LocalDeclarationStatementSyntax declaration)
                {
                    var type = declaration.Declaration.Type.IsVar
                        ? (declaration.Declaration.Variables.FirstOrDefault()?.Initializer?.Value as ObjectCreationExpressionSyntax)?.Type
                        : declaration.Declaration.Type;
                    var simple = type?.ToString().Split('.').Last();
                    if (simple != null && s_localTypes.Contains(simple)) dropped.Add(s.Member);
                }
            }
            return dropped;
        }

        /// <summary>A fresh local name: the type's name camel-cased and numbered per type, as VS numbers them.</summary>
        private string NewLocal(Type type)
        {
            _takenNames ??= _model.Fields.Select(f => f.Name)
                .Concat(_ordered.Select(c => c.Name!))
                .Concat(_model.Statements.Where(s => s.Kind == DesignerStatementKind.Local).Select(s => s.Member).Where(m => !RegeneratedLocals().Contains(m)))
                .ToHashSet(StringComparer.Ordinal);
            var stem = char.ToLowerInvariant(type.Name[0]) + type.Name[1..];
            int n = _localCounters.GetValueOrDefault(stem);
            string name;
            do name = stem + ++n; while (!_takenNames.Add(name));
            _localCounters[stem] = n;
            return name;
        }

        /// <summary>
        /// Declares <paramref name="value"/> as a local (on top of the method) and returns its name; the
        /// statements that set its properties go to <paramref name="prelude"/>, which the caller puts right
        /// before the statement using the local - where VS puts them. Null for a value of another type.
        /// </summary>
        private string? LocalFor(object? value, List<string> prelude)
        {
            if (value == null) return null;
            if (_objectLocals.TryGetValue(value, out var existing)) return existing;
            string creation;
            IEnumerable<string> skip = Array.Empty<string>();
            switch (value)
            {
                case TreeNode node:
                {
                    // Children first: the parent's constructor takes them (TreeNodeConverter).
                    var children = node.Nodes.Cast<TreeNode>().Select(child => LocalFor(child, prelude)!).ToList();
                    var args = new List<string> { Literal(node.Text) };
                    if (node.ImageIndex != -1 || node.SelectedImageIndex != -1)
                    {
                        args.Add(node.ImageIndex.ToString(CultureInfo.InvariantCulture));
                        args.Add(node.SelectedImageIndex.ToString(CultureInfo.InvariantCulture));
                    }
                    if (children.Count > 0) args.Add(ArrayOf(typeof(TreeNode), children));
                    creation = $"new {_namer.Name(typeof(TreeNode))}({string.Join(", ", args)})";
                    break;
                }
                case ListViewItem item:
                {
                    // ListViewItemConverter: a sub-item with a style of its own makes it a ListViewSubItem[]
                    // (each styled one with its resolved colors and font); a styled item, the string[]
                    // constructor with its own (raw) colors and font; otherwise the texts and the image.
                    var subs = item.SubItems.Cast<ListViewItem.ListViewSubItem>().ToList();
                    var texts = subs.Select(si => si.Text).ToList();
                    string image = item.ImageKey.Length > 0 ? Literal(item.ImageKey) : item.ImageIndex.ToString(CultureInfo.InvariantCulture);
                    string itemType = _namer.Name(typeof(ListViewItem));
                    if (subs.Skip(1).Any(si => si.HasCustomStyle))
                    {
                        string subType = _namer.Name(typeof(ListViewItem.ListViewSubItem));
                        var parts = subs.Select((si, i) => (i == 0 ? item.HasCustomStyle : si.HasCustomStyle)
                            ? $"new {subType}(null, {Literal(si.Text)}, {Expression(i == 0 ? item.ForeColor : si.ForeColor, typeof(Color))}, {Expression(i == 0 ? item.BackColor : si.BackColor, typeof(Color))}, {Expression(i == 0 ? item.Font : si.Font, typeof(Font))})"
                            : $"new {subType}(null, {Literal(si.Text)})").ToList();
                        creation = $"new {itemType}({ArrayOf(typeof(ListViewItem.ListViewSubItem), parts)}, {image})";
                    }
                    else if (item.HasCustomStyle)
                    {
                        creation = $"new {itemType}({ArrayOf(typeof(string), texts.Select(Literal).ToList())}, {image}, "
                            + $"{Expression(item.RawForeColor, typeof(Color))}, {Expression(item.RawBackColor, typeof(Color))}, {Expression(item.RawFont, typeof(Font))})";
                    }
                    else
                    {
                        string text = texts.Count > 1 ? ArrayOf(typeof(string), texts.Select(Literal).ToList()) : Literal(item.Text);
                        creation = texts.Count <= 1 && item.ImageIndex == -1 && item.ImageKey.Length == 0
                            ? $"new {itemType}({text})"
                            : $"new {itemType}({text}, {image})";
                    }
                    break;
                }
                case ListViewGroup group:
                    creation = $"new {_namer.Name(typeof(ListViewGroup))}({Literal(group.Header)}, {EnumExpression(group.HeaderAlignment)})";
                    break;
                case DataGridViewCellStyle:
                    creation = $"new {_namer.Name(typeof(DataGridViewCellStyle))}()";
                    break;
                default:
                    return null;
            }

            var name = NewLocal(value.GetType());
            _objectLocals[value] = name;
            _localDeclarations.Add($"{_namer.Name(value.GetType())} {name} = {creation};");

            // Then whatever the constructor did not say, as for any object (isComplete: false in VS).
            var sets = new List<Item>();
            foreach (PropertyDescriptor pd in TypeDescriptor.GetProperties(value))
            {
                if (pd.SerializationVisibility != DesignerSerializationVisibility.Visible || pd.IsReadOnly) continue;
                if (!ShouldSerialize(pd, value)) continue;
                object? v;
                try { v = pd.GetValue(value); }
                catch (Exception) { continue; }
                var expression = Expression(v, pd.PropertyType) ?? throw Unwritable(value, pd.Name, v);
                Add(sets, 1, pd.Name, $"{name}.{pd.Name} = {expression};");
            }
            prelude.AddRange(Sort(sets).Select(i => i.Text));
            return name;
        }

        private string ArrayOf(Type elementType, IReadOnlyList<string> items) =>
            Classic
                ? $"new {_namer.Name(elementType)}[] {{\n" + string.Join(",\n", items) + "}"
                : $"new {_namer.Name(elementType)}[] {{ {string.Join(", ", items)} }}";

        /// <summary>
        /// Extender properties this object receives from a provider on the form (a TableLayoutPanel's
        /// ColumnSpan, a ToolTip's text): <c>provider.SetXxx(control, value)</c> in the control's block,
        /// sorted by the property name, as VS does.
        /// </summary>
        private void Extenders(object instance, List<Item> items)
        {
            foreach (var provider in _providers)
            {
                if (ReferenceEquals(provider, instance) || !provider.CanExtend(instance)) continue;
                foreach (var attribute in provider.GetType().GetCustomAttributes<ProvidePropertyAttribute>(inherit: true))
                {
                    var receiver = Type.GetType(attribute.ReceiverTypeName);
                    if (receiver != null && !receiver.IsInstanceOfType(instance)) continue;
                    var name = attribute.PropertyName;
                    var getter = provider.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "Get" + name && m.GetParameters() is [var p] && p.ParameterType.IsInstanceOfType(instance));
                    if (getter == null) continue;
                    var visibility = getter.GetCustomAttribute<DesignerSerializationVisibilityAttribute>()?.Visibility ?? DesignerSerializationVisibility.Visible;
                    if (visibility == DesignerSerializationVisibility.Hidden) continue;

                    var value = getter.Invoke(provider, new[] { instance });
                    var shouldSerialize = provider.GetType().GetMethod("ShouldSerialize" + name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, new[] { getter.GetParameters()[0].ParameterType });
                    bool write = shouldSerialize != null
                        ? (bool)shouldSerialize.Invoke(provider, new[] { instance })!
                        : getter.GetCustomAttribute<DefaultValueAttribute>() is { } dv ? !Equals(dv.Value, value) : true;
                    if (!write) continue;

                    var expression = Expression(value, getter.ReturnType) ?? throw Unwritable(instance, name, value);
                    Add(items, 1, name, $"{Member(provider, "Set" + name)}({Target(instance)}, {expression});");
                }
            }
        }

        private string EventStatement(DesignerComponent c, DesignerEventBinding e)
        {
            var target = Member(c.Instance, e.Path);
            if (!Classic) return $"{target} += {e.HandlerName};";
            var eventType = c.IsPlaceholder ? null : TypeDescriptor.GetEvents(c.Instance)[e.EventName]?.EventType;
            if (eventType == null)
            {
                var original = _model.Statements.FirstOrDefault(s => s.Event == e);
                return original?.Source ?? $"{target} += this.{e.HandlerName};";
            }
            return $"{target} += new {_namer.Name(eventType)}(this.{e.HandlerName});";
        }

        /// <summary>For a value the writer cannot spell: what the file said, as long as the value is still what the file set.</summary>
        private static string? OriginalSource(DesignerComponent? component, string path, object? value)
        {
            var assignment = component?.Assignments.LastOrDefault(a => a.Path == path);
            return assignment != null && Equals(assignment.Value, value) ? assignment.Source : null;
        }

        private static Exception Unwritable(object owner, string member, object? value) =>
            new NotSupportedException($"The designer cannot write {owner.GetType().Name}.{member}: there is no code form for a value of type {value?.GetType().FullName ?? "null"}.");

        // --- values -------------------------------------------------------------------------------

        /// <summary>A C# expression for <paramref name="value"/> where <paramref name="declared"/> is expected; null if there is none.</summary>
        private string? Expression(object? value, Type declared)
        {
            if (value == null) return "null";
            var reference = Reference(value);
            if (reference != null) return reference;
            if (value is IComponent) return null; // a component that is not part of the form

            switch (value)
            {
                case string s: return Literal(s);
                case bool b: return b ? "true" : "false";
                case char ch: return SymbolDisplay.FormatLiteral(ch, quote: true);
                case Enum e: return EnumExpression(e);
                case Type t: return $"typeof({_namer.Name(t)})";
                case int i: return i.ToString(CultureInfo.InvariantCulture);
                case long l: return l.ToString(CultureInfo.InvariantCulture) + "L";
                case uint ui: return ui.ToString(CultureInfo.InvariantCulture) + "U";
                case ulong ul: return ul.ToString(CultureInfo.InvariantCulture) + "UL";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture) + "F";
                case double d: return d.ToString("R", CultureInfo.InvariantCulture) + "D";
                case byte or sbyte or short or ushort: return SmallInteger(value, declared);
                case Array array: return ArrayExpression(array);
                case TableLayoutStyle style: return StyleExpression(style);
                // VS writes tree nodes as locals (LocalFor), not through TreeNodeConverter's constructor call.
                case TreeNode: return null;
            }

            var converter = TypeDescriptor.GetConverter(value);
            if (!converter.CanConvertTo(typeof(InstanceDescriptor))) return null;
            InstanceDescriptor? descriptor;
            try { descriptor = converter.ConvertTo(value, typeof(InstanceDescriptor)) as InstanceDescriptor; }
            catch (Exception) { return null; }
            return descriptor is { IsComplete: true } ? DescriptorExpression(descriptor) : null;
        }

        private string? DescriptorExpression(InstanceDescriptor descriptor)
        {
            var args = descriptor.Arguments.Cast<object?>().ToList();
            switch (descriptor.MemberInfo)
            {
                case ConstructorInfo ctor:
                {
                    var list = Arguments(args, ctor.GetParameters());
                    return list == null ? null : $"new {_namer.Name(ctor.DeclaringType!)}({list})";
                }
                case MethodInfo { IsStatic: true } method:
                {
                    var list = Arguments(args, method.GetParameters());
                    return list == null ? null : $"{_namer.Name(method.DeclaringType!)}.{method.Name}({list})";
                }
                case PropertyInfo property:
                    return $"{_namer.Name(property.DeclaringType!)}.{property.Name}";
                case FieldInfo field:
                    return $"{_namer.Name(field.DeclaringType!)}.{field.Name}";
                default:
                    return null;
            }
        }

        private string? Arguments(List<object?> args, ParameterInfo[] parameters)
        {
            var parts = new List<string>();
            for (int i = 0; i < args.Count; i++)
            {
                var e = Expression(args[i], i < parameters.Length ? parameters[i].ParameterType : typeof(object));
                if (e == null) return null;
                parts.Add(e);
            }
            return string.Join(", ", parts);
        }

        private string ArrayExpression(Array array)
        {
            var elementType = array.GetType().GetElementType()!;
            var items = array.Cast<object?>().Select(x => Expression(x, elementType) ?? throw Unwritable(array, "[]", x));
            return $"new {_namer.Name(elementType)}[] {{ {string.Join(", ", items)} }}";
        }

        private string StyleExpression(TableLayoutStyle style)
        {
            var type = _namer.Name(style.GetType());
            if (style.SizeType == SizeType.AutoSize) return $"new {type}()";
            float size = style is ColumnStyle column ? column.Width : ((RowStyle)style).Height;
            return $"new {type}({EnumExpression(style.SizeType)}, {size.ToString("R", CultureInfo.InvariantCulture)}F)";
        }

        /// <summary>
        /// <c>204</c> where a byte (or any wider integer, as <c>Color.FromArgb(int, int, int)</c> takes the
        /// colour's bytes) is expected; a cast where the type must be kept (an <c>object</c> slot).
        /// CodeDom always cast, and cast again to a wider parameter: <c>((int)(((byte)(10))))</c>.
        /// </summary>
        private string SmallInteger(object value, Type declared)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture)!;
            var type = _namer.Name(value.GetType());
            bool numericSlot = declared.IsPrimitive && declared != typeof(bool) && declared != typeof(char);
            if (Classic)
            {
                var cast = $"(({type})({text}))";
                return numericSlot && declared != value.GetType() ? $"(({_namer.Name(declared)})({cast}))" : cast;
            }
            return numericSlot ? text : $"({type}){text}";
        }

        private string EnumExpression(Enum value)
        {
            var type = value.GetType();
            var typeName = _namer.Name(type);
            var parts = EnumParts(value);
            string Part(Enum part) => Enum.GetName(type, part) is { } name
                ? $"{typeName}.{name}"
                : Classic ? $"(({typeName})({Convert.ToInt64(part, CultureInfo.InvariantCulture)}))" : $"({typeName}){Convert.ToInt64(part, CultureInfo.InvariantCulture)}";

            if (parts.Count == 1) return Part(parts[0]);
            if (!Classic) return string.Join(" | ", parts.Select(Part));
            var acc = Part(parts[0]);
            foreach (var part in parts.Skip(1)) acc = "(" + acc + " | " + Part(part) + ")";
            return $"(({typeName})({acc}))";
        }

        /// <summary>
        /// The members a value is spelled with: EnumConverter's decomposition (defined values in
        /// ascending order, each taking its bits away), except for Keys, which KeysConverter writes
        /// modifiers first - <c>Keys.Control | Keys.Shift | Keys.N</c>.
        /// </summary>
        private static List<Enum> EnumParts(Enum value)
        {
            var type = value.GetType();
            if (!type.IsDefined(typeof(FlagsAttribute), inherit: false)) return new List<Enum> { value };

            if (value is Keys keys)
            {
                var result = new List<Enum>();
                foreach (var modifier in new[] { Keys.Control, Keys.Shift, Keys.Alt })
                    if ((keys & modifier) == modifier) result.Add(modifier);
                var code = keys & Keys.KeyCode;
                if (code != Keys.None || result.Count == 0) result.Add(code);
                return result;
            }

            ulong remaining = ToUInt64(value);
            var parts = new List<Enum>();
            var defined = Enum.GetValues(type).Cast<Enum>().Select(e => (Value: ToUInt64(e), Enum: e)).ToList();
            bool found = true;
            while (found)
            {
                found = false;
                foreach (var (v, e) in defined)
                {
                    if ((v != 0 && (v & remaining) == v) || v == remaining)
                    {
                        parts.Add(e);
                        found = true;
                        remaining &= ~v;
                        break;
                    }
                }
                if (remaining == 0) break;
            }
            if (!found && remaining != 0 || parts.Count == 0) parts.Add((Enum)Enum.ToObject(type, remaining));
            return parts;
        }

        private static ulong ToUInt64(Enum e) => Convert.ToUInt64(Convert.ChangeType(e, Enum.GetUnderlyingType(e.GetType()), CultureInfo.InvariantCulture) switch
        {
            sbyte sb => unchecked((ulong)sb),
            short sh => unchecked((ulong)sh),
            int i => unchecked((ulong)i),
            long l => unchecked((ulong)l),
            var other => other,
        }, CultureInfo.InvariantCulture);
    }

    private static string Literal(string s) => SymbolDisplay.FormatLiteral(s, quote: true);
}
