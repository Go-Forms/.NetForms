using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.Design.Serialization;

/// <summary>
/// Reads a <c>*.Designer.cs</c> file without compiling the user's project: parses it with Roslyn and
/// <em>interprets</em> <c>InitializeComponent()</c> with reflection over NetForms, so the designer
/// opens a form instantly and offline (Ф5.1, docs/PLAN.md).
/// </summary>
/// <remarks>
/// <para>
/// <c>InitializeComponent</c> is a very small subset of C#: object creation, property assignment,
/// method calls (<c>Controls.Add</c>, <c>SuspendLayout</c>, <c>AddRange</c>), event subscription and
/// the occasional local. That subset is executed for real — constructors run, setters run, layout
/// runs — so the result is the same object tree that calling <c>InitializeComponent()</c> in the
/// compiled application gives. Anything outside the subset (loops, conditions, lambdas) is not
/// guessed at: it stops the read with a <see cref="DesignerCodeException"/> pointing at the line.
/// </para>
/// <para>
/// Event handlers are recorded, never subscribed: they are methods of the user's class, which is
/// not compiled. The root is an instance of the form's base class (<c>Form</c>, <c>UserControl</c>),
/// exactly as in the Visual Studio designer.
/// </para>
/// </remarks>
public sealed class DesignerCodeReader
{
    private readonly List<Assembly> _assemblies;
    private TypeResolver? _types;

    /// <summary>A reader over NetForms, System.Drawing and the BCL.</summary>
    public DesignerCodeReader() : this(TypeResolver.DefaultAssemblies()) { }

    /// <summary>NetForms, System.Drawing and the BCL: what a designer file can name without the user's project.</summary>
    public static IEnumerable<Assembly> DefaultAssemblies() => TypeResolver.DefaultAssemblies();

    /// <summary>A reader that resolves types from <paramref name="assemblies"/> (<see cref="DefaultAssemblies"/> and the project's).</summary>
    public DesignerCodeReader(IEnumerable<Assembly> assemblies)
    {
        _assemblies = assemblies.ToList();
    }

    /// <summary>
    /// Reads <paramref name="designerPath"/> (<c>MainForm.Designer.cs</c>). The companion
    /// <c>MainForm.cs</c> next to it is read too when it exists: that is where the base class is
    /// usually declared (<c>public partial class MainForm : Form</c>).
    /// </summary>
    public DesignerModel ReadFile(string designerPath)
    {
        var source = File.ReadAllText(designerPath);
        var companions = new List<string>();
        var companion = CompanionPath(designerPath);
        if (companion != null && File.Exists(companion)) companions.Add(File.ReadAllText(companion));
        return Read(source, companions, designerPath);
    }

    /// <summary><c>MainForm.cs</c> for <c>MainForm.Designer.cs</c>; null if the name has no <c>.Designer.cs</c> suffix.</summary>
    public static string? CompanionPath(string designerPath)
    {
        const string suffix = ".Designer.cs";
        var name = Path.GetFileName(designerPath);
        if (!name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;
        return Path.Combine(Path.GetDirectoryName(designerPath) ?? "", name.Substring(0, name.Length - suffix.Length) + ".cs");
    }

    /// <summary>Reads designer code from memory; <paramref name="companionSources"/> are the other parts of the class.</summary>
    public DesignerModel Read(string designerSource, IEnumerable<string>? companionSources = null, string? filePath = null)
    {
        _types ??= new TypeResolver(_assemblies);
        return new Session(_types, filePath).Run(designerSource, companionSources ?? Array.Empty<string>());
    }

    // =============================================================================================

    private abstract record Resolved;

    /// <summary>A value with its static type (the type C# would see: a cast's type, a field's declared type).</summary>
    private sealed record ValueR(object? Value, Type? Type) : Resolved;

    private sealed record TypeR(Type Type) : Resolved;

    private sealed record NamespaceR(string Name) : Resolved;

    private sealed class FieldSlot
    {
        public required string Name;
        public required Type? DeclaredType;
        public object? Value;
        public DesignerComponent? Component;
    }

    private sealed class Session
    {
        private readonly TypeResolver _types;
        private readonly string? _path;
        private readonly Dictionary<string, FieldSlot> _fields = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ValueR> _locals = new(StringComparer.Ordinal);
        private DesignerModel _model = null!;
        private object _root = null!;
        private string _className = null!;

        public Session(TypeResolver types, string? path)
        {
            _types = types;
            _path = path;
        }

        public DesignerModel Run(string designerSource, IEnumerable<string> companionSources)
        {
            var tree = CSharpSyntaxTree.ParseText(designerSource, path: _path ?? "");
            var error = tree.GetDiagnostics().FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            if (error != null) throw Error(error.GetMessage(CultureInfo.InvariantCulture), error.Location);

            var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent" && m.ParameterList.Parameters.Count == 0)
                ?? throw new DesignerCodeException("The file has no InitializeComponent() method.", _path, 1, 1);
            if (method.Body == null) throw Error("InitializeComponent() has no body.", method);
            var cls = method.Parent as ClassDeclarationSyntax
                ?? throw Error("InitializeComponent() must be declared in a class.", method);

            _className = cls.Identifier.ValueText;
            var ns = NamespaceOf(cls);
            SetScope(cls, ns);

            // The other parts of the class: the base class usually lives in MainForm.cs.
            var parts = new List<(ClassDeclarationSyntax Class, SyntaxTree Tree)> { (cls, tree) };
            foreach (var src in companionSources)
            {
                var ctree = CSharpSyntaxTree.ParseText(src);
                foreach (var c in ctree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
                    if (c.Identifier.ValueText == _className && NamespaceOf(c) == ns) parts.Add((c, ctree));
            }

            var (rootType, rootTypeName) = ResolveRootType(parts, ns, cls);
            SetScope(cls, ns);

            object root;
            try { root = Activator.CreateInstance(rootType)!; }
            catch (Exception ex) { throw Error($"Cannot create the root object '{rootType.FullName}': {Unwrap(ex).Message}", cls, ex); }
            _root = root;
            var rootComponent = new DesignerComponent(_className, rootTypeName, rootType, root, isRoot: true, isPlaceholder: false);
            _model = new DesignerModel(ns, _className, rootTypeName, rootComponent, _path);

            foreach (var part in parts)
            {
                foreach (var field in part.Class.Members.OfType<FieldDeclarationSyntax>())
                {
                    var declared = _types.Resolve(field.Declaration.Type);
                    foreach (var v in field.Declaration.Variables)
                    {
                        var name = v.Identifier.ValueText;
                        _fields.TryAdd(name, new FieldSlot { Name = name, DeclaredType = declared });
                        if (part.Tree == tree)
                            _model.AddField(new DesignerField(name, field.Declaration.Type.ToString(),
                                string.Join(" ", field.Modifiers.Select(m => m.Text)), LineOf(field)));
                    }
                }
            }

            foreach (var statement in method.Body.Statements) Execute(statement);
            return _model;
        }

        private (Type, string) ResolveRootType(List<(ClassDeclarationSyntax Class, SyntaxTree Tree)> parts, string? ns, ClassDeclarationSyntax designerClass)
        {
            foreach (var (part, _) in parts)
            {
                if (part.BaseList == null) continue;
                SetScope(part, ns);
                foreach (var b in part.BaseList.Types)
                {
                    var t = _types.Resolve(b.Type);
                    if (t == null)
                        throw Error($"The base class '{b.Type}' is not a type the designer knows. Forms deriving from a class of the same project are not supported yet.", b);
                    if (t.IsInterface) continue;
                    if (t.IsAbstract) throw Error($"The base class '{t.FullName}' is abstract and cannot be designed.", b);
                    if (!typeof(IComponent).IsAssignableFrom(t)) throw Error($"The base class '{t.FullName}' is not a component.", b);
                    return (t, b.Type.ToString());
                }
            }
            throw Error($"Cannot find the base class of '{_className}'. Pass the other part of the partial class (e.g. {_className}.cs) to the reader.", designerClass.Identifier);
        }

        private void SetScope(SyntaxNode node, string? ns)
        {
            var usings = new List<string>();
            var aliases = new List<KeyValuePair<string, string>>();
            var directives = node.AncestorsAndSelf().SelectMany(a => a switch
            {
                CompilationUnitSyntax cu => cu.Usings,
                BaseNamespaceDeclarationSyntax nd => nd.Usings,
                _ => default(SyntaxList<UsingDirectiveSyntax>),
            }).Reverse();
            foreach (var u in directives)
            {
                if (u.Name == null || u.StaticKeyword != default) continue;
                var name = TypeResolver.Dotted(u.Name);
                if (u.Alias != null) aliases.Add(new(u.Alias.Name.Identifier.ValueText, name));
                else usings.Add(name);
            }
            _types.SetScope(usings, aliases, ns);
        }

        private static string? NamespaceOf(SyntaxNode node)
        {
            var names = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()).ToList();
            return names.Count == 0 ? null : string.Join(".", names);
        }

        // --- statements --------------------------------------------------------------------------

        private void Execute(StatementSyntax statement)
        {
            switch (statement)
            {
                case EmptyStatementSyntax:
                    return;
                case LocalDeclarationStatementSyntax local:
                    ExecuteLocal(local);
                    return;
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax a } when a.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    ExecuteAssignment(a, statement);
                    return;
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax a } when a.IsKind(SyntaxKind.AddAssignmentExpression):
                    ExecuteAttachEvent(a, statement);
                    return;
                case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }:
                    EvalInvocation(call);
                    var (target, path) = Describe(call.Expression);
                    _model.AddStatement(new DesignerStatement(DesignerStatementKind.Call, target, path, statement.ToString(), LineOf(statement)));
                    return;
                default:
                    throw Error($"The designer does not understand this statement ({statement.Kind()}). InitializeComponent may only create objects, set properties, call methods and attach event handlers.", statement);
            }
        }

        private void ExecuteLocal(LocalDeclarationStatementSyntax local)
        {
            var declared = local.Declaration.Type.IsVar ? null : _types.Resolve(local.Declaration.Type);
            foreach (var v in local.Declaration.Variables)
            {
                if (v.Initializer == null) throw Error("A local must be initialized where it is declared.", v);
                var value = Eval(v.Initializer.Value, declared);
                if (declared != null) value = new ValueR(Convert(value, declared, v.Initializer.Value), declared);
                _locals[v.Identifier.ValueText] = value;
                _model.AddStatement(new DesignerStatement(DesignerStatementKind.Local, null, v.Identifier.ValueText, local.ToString(), LineOf(local)));
            }
        }

        private void ExecuteAssignment(AssignmentExpressionSyntax a, StatementSyntax statement)
        {
            // field = new T(...): this is where a component gets its name.
            var fieldName = a.Left switch
            {
                IdentifierNameSyntax id when !_locals.ContainsKey(id.Identifier.ValueText) && _fields.ContainsKey(id.Identifier.ValueText) => id.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax id } when _fields.ContainsKey(id.Identifier.ValueText) => id.Identifier.ValueText,
                _ => null,
            };
            if (fieldName != null)
            {
                var slot = _fields[fieldName];
                var value = a.Right is BaseObjectCreationExpressionSyntax creation
                    ? EvalCreation(creation, slot.DeclaredType, componentName: fieldName)
                    : Eval(a.Right, slot.DeclaredType);
                // A placeholder stands in for the declared type (a control whose constructor threw).
                slot.Value = slot.DeclaredType != null && value.Value is not DesignerPlaceholder ? Convert(value, slot.DeclaredType, a.Right) : value.Value;
                slot.Component = slot.Value == null ? null : _model.FindByInstance(slot.Value);
                if (slot.Component != null && slot.Component.Name != fieldName) slot.Component = null;
                _model.AddStatement(new DesignerStatement(DesignerStatementKind.Create, slot.Component, fieldName, statement.ToString(), LineOf(statement)));
                return;
            }

            if (a.Left is IdentifierNameSyntax localId && _locals.TryGetValue(localId.Identifier.ValueText, out var old))
            {
                var value = Eval(a.Right, old.Type);
                _locals[localId.Identifier.ValueText] = old.Type != null ? new ValueR(Convert(value, old.Type, a.Right), old.Type) : value;
                _model.AddStatement(new DesignerStatement(DesignerStatementKind.Local, null, localId.Identifier.ValueText, statement.ToString(), LineOf(statement)));
                return;
            }

            var (owner, memberName) = AssignmentTarget(a.Left);
            var (component, path) = Describe(a.Left);
            var assignment = SetMember(owner, memberName, a.Right, a.Left, path);
            component?.AddAssignment(assignment);
            _model.AddStatement(new DesignerStatement(DesignerStatementKind.Assign, component, path, statement.ToString(), LineOf(statement), Assignment: assignment));
        }

        /// <summary>The object whose member the left-hand side names, and the member.</summary>
        private (object Owner, SimpleNameSyntax Member) AssignmentTarget(ExpressionSyntax left)
        {
            switch (left)
            {
                case IdentifierNameSyntax id:
                    return (_root, id);
                case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } m:
                    return (_root, m.Name);
                case MemberAccessExpressionSyntax m:
                    var owner = Eval(m.Expression, null).Value
                        ?? throw Error($"'{m.Expression}' is null here.", m.Expression);
                    return (owner, m.Name);
                default:
                    throw Error($"The designer cannot assign to '{left}'.", left);
            }
        }

        private DesignerPropertyAssignment SetMember(object owner, SimpleNameSyntax nameSyntax, ExpressionSyntax right, SyntaxNode at, string path)
        {
            var name = nameSyntax.Identifier.ValueText;
            var member = FindValueMember(owner.GetType(), name, isStatic: false, nonPublic: ReferenceEquals(owner, _root));

            if (member == null || !CanWrite(member))
            {
                if (owner is DesignerPlaceholder)
                    return new DesignerPropertyAssignment(path, null, right.ToString(), LineOf(at), Applied: false);
                throw Error(member == null
                    ? $"'{owner.GetType().FullName}' has no property '{name}'."
                    : $"'{owner.GetType().FullName}.{name}' is read-only.", nameSyntax);
            }

            var memberType = MemberType(member);
            ValueR value;
            try { value = Eval(right, memberType); }
            catch (DesignerCodeException) when (owner is DesignerPlaceholder)
            {
                return new DesignerPropertyAssignment(path, null, right.ToString(), LineOf(at), Applied: false);
            }
            var converted = Convert(value, memberType, right);
            try
            {
                if (member is PropertyInfo p) p.SetValue(owner, converted);
                else ((FieldInfo)member).SetValue(owner, converted);
            }
            catch (Exception ex)
            {
                throw Error($"Setting '{name}' failed: {Unwrap(ex).Message}", at, Unwrap(ex));
            }
            return new DesignerPropertyAssignment(path, converted, right.ToString(), LineOf(at), Applied: true);
        }

        private void ExecuteAttachEvent(AssignmentExpressionSyntax a, StatementSyntax statement)
        {
            var (owner, eventNameSyntax) = AssignmentTarget(a.Left);
            var eventName = eventNameSyntax.Identifier.ValueText;
            var info = FindEvent(owner.GetType(), eventName);
            if (info == null && owner is not DesignerPlaceholder)
                throw Error($"'{owner.GetType().FullName}' has no event '{eventName}'.", eventNameSyntax);

            var handler = HandlerName(a.Right)
                ?? throw Error("An event handler must be a method of the form (button1_Click, this.button1_Click or new EventHandler(this.button1_Click)).", a.Right);

            var (component, path) = Describe(a.Left);
            var owningComponent = component ?? _model.FindByInstance(owner);
            DesignerEventBinding? binding = null;
            if (owningComponent != null)
            {
                binding = new DesignerEventBinding(owningComponent, path, eventName, handler, LineOf(statement));
                owningComponent.AddEvent(binding);
            }
            _model.AddStatement(new DesignerStatement(DesignerStatementKind.AttachEvent, component, path, statement.ToString(), LineOf(statement), Event: binding));
        }

        private static string? HandlerName(ExpressionSyntax e) => e switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax id } => id.Identifier.ValueText,
            ObjectCreationExpressionSyntax { ArgumentList.Arguments.Count: 1 } n => HandlerName(n.ArgumentList.Arguments[0].Expression),
            ParenthesizedExpressionSyntax p => HandlerName(p.Expression),
            _ => null,
        };

        /// <summary>
        /// Which component a statement is about and the member path relative to it:
        /// <c>splitContainer1.Panel1.Controls.Add</c> → (splitContainer1, "Panel1.Controls.Add"),
        /// <c>this.ClientSize</c> → (root, "ClientSize"), <c>((ISupportInitialize)pictureBox1).BeginInit</c> → (pictureBox1, "BeginInit").
        /// </summary>
        private (DesignerComponent? Component, string Path) Describe(ExpressionSyntax expression)
        {
            var names = new List<string>();
            var e = expression;
            while (true)
            {
                e = StripParensAndCasts(e);
                if (e is MemberAccessExpressionSyntax m)
                {
                    names.Insert(0, m.Name.Identifier.ValueText);
                    e = m.Expression;
                }
                else if (e is ElementAccessExpressionSyntax ea)
                {
                    names.Insert(0, ea.ArgumentList.ToString());
                    e = ea.Expression;
                }
                else break;
            }
            string Join(IEnumerable<string> parts) => string.Join(".", parts).Replace(".[", "[");

            switch (e)
            {
                case ThisExpressionSyntax or BaseExpressionSyntax:
                    if (names.Count > 1 && _fields.TryGetValue(names[0], out var thisSlot))
                        return (thisSlot.Component, Join(names.Skip(1)));
                    return (_model.Root, Join(names));
                case IdentifierNameSyntax id:
                    var name = id.Identifier.ValueText;
                    if (_locals.TryGetValue(name, out var local))
                        return (local.Value == null ? null : _model.FindByInstance(local.Value), Join(names));
                    if (_fields.TryGetValue(name, out var slot)) return (slot.Component, Join(names));
                    return (_model.Root, Join(names.Prepend(name)));
                default:
                    return (null, Join(names.Prepend(e.ToString())));
            }
        }

        private static ExpressionSyntax StripParensAndCasts(ExpressionSyntax e)
        {
            while (true)
            {
                if (e is ParenthesizedExpressionSyntax p) e = p.Expression;
                else if (e is CastExpressionSyntax c) e = c.Expression;
                else return e;
            }
        }

        // --- expressions -------------------------------------------------------------------------

        private ValueR Eval(ExpressionSyntax e, Type? expected)
        {
            var r = Resolve(e, expected);
            return r switch
            {
                ValueR v => v,
                TypeR t => throw Error($"'{e}' is a type, not a value.", e),
                NamespaceR n => throw Error($"'{e}' is a namespace, not a value.", e),
                _ => throw Error($"Cannot evaluate '{e}'.", e),
            };
        }

        private Resolved Resolve(ExpressionSyntax e, Type? expected)
        {
            switch (e)
            {
                case LiteralExpressionSyntax lit:
                    if (lit.IsKind(SyntaxKind.DefaultLiteralExpression))
                        return new ValueR(expected is { IsValueType: true } ? Activator.CreateInstance(expected) : null, expected);
                    var literal = lit.Token.Value;
                    return new ValueR(literal, literal?.GetType());
                case ParenthesizedExpressionSyntax p:
                    return Resolve(p.Expression, expected);
                case ThisExpressionSyntax or BaseExpressionSyntax:
                    return new ValueR(_root, _root.GetType());
                case IdentifierNameSyntax id:
                    return ResolveName(id);
                case PredefinedTypeSyntax pt:
                    return new TypeR(TypeResolver.Predefined(pt.Keyword.Kind()) ?? throw Error($"Unknown type '{pt}'.", pt));
                case QualifiedNameSyntax or AliasQualifiedNameSyntax:
                    return new TypeR(_types.Resolve((TypeSyntax)e) ?? throw Error($"Unknown type '{e}'.", e));
                case MemberAccessExpressionSyntax m when m.IsKind(SyntaxKind.SimpleMemberAccessExpression):
                    return ResolveMemberAccess(m);
                case BaseObjectCreationExpressionSyntax n:
                    return EvalCreation(n, expected, componentName: null);
                case ArrayCreationExpressionSyntax ac:
                    return EvalArray(ac);
                case ImplicitArrayCreationExpressionSyntax iac:
                    return EvalImplicitArray(iac);
                case InvocationExpressionSyntax call:
                    return EvalInvocation(call);
                case CastExpressionSyntax cast:
                    return EvalCast(cast);
                case BinaryExpressionSyntax bin:
                    return EvalBinary(bin);
                case PrefixUnaryExpressionSyntax un:
                    return EvalUnary(un);
                case TypeOfExpressionSyntax tof:
                    return new ValueR(ResolveTypeOf(tof.Type), typeof(Type));
                case DefaultExpressionSyntax def:
                    var dt = _types.Resolve(def.Type) ?? throw Error($"Unknown type '{def.Type}'.", def.Type);
                    return new ValueR(dt.IsValueType ? Activator.CreateInstance(dt) : null, dt);
                case ElementAccessExpressionSyntax ea:
                    return EvalElementAccess(ea);
                case CheckedExpressionSyntax ch:
                    return Resolve(ch.Expression, expected);
                case AnonymousFunctionExpressionSyntax:
                    throw Error("Lambdas are not supported in InitializeComponent; attach a named handler instead.", e);
                default:
                    throw Error($"The designer does not understand this expression ({e.Kind()}).", e);
            }
        }

        /// <summary>typeof(MainForm) means the designed class, which is not compiled: the root type stands in for it.</summary>
        private Type ResolveTypeOf(TypeSyntax t)
        {
            if (t is IdentifierNameSyntax id && id.Identifier.ValueText == _className) return _model.RootType;
            return _types.Resolve(t) ?? throw Error($"Unknown type '{t}'.", t);
        }

        private Resolved ResolveName(IdentifierNameSyntax id)
        {
            var name = id.Identifier.ValueText;
            if (_locals.TryGetValue(name, out var local)) return local;
            if (_fields.TryGetValue(name, out var slot)) return FieldValue(slot, id);
            var rootMember = FindValueMember(_root.GetType(), name, isStatic: false, nonPublic: true);
            if (rootMember != null) return new ValueR(GetValue(rootMember, _root, id), MemberType(rootMember));
            var type = _types.Resolve(name);
            if (type != null) return new TypeR(type);
            if (_types.IsNamespace(name)) return new NamespaceR(name);
            throw Error($"The name '{name}' does not exist here.", id);
        }

        private ValueR FieldValue(FieldSlot slot, SyntaxNode at) =>
            new(slot.Value, slot.DeclaredType ?? slot.Value?.GetType());

        private Resolved ResolveMemberAccess(MemberAccessExpressionSyntax m)
        {
            var name = m.Name.Identifier.ValueText;
            if (m.Name is GenericNameSyntax) throw Error($"Generic member access '{m}' is not supported.", m);

            Resolved left;
            if (m.Expression is ThisExpressionSyntax or BaseExpressionSyntax)
            {
                if (_fields.TryGetValue(name, out var slot)) return FieldValue(slot, m);
                left = new ValueR(_root, _root.GetType());
            }
            else if (m.Expression is IdentifierNameSyntax head && ColorColor(head, name) is TypeR colorColor)
            {
                left = colorColor;
            }
            else
            {
                left = Resolve(m.Expression, null);
            }

            switch (left)
            {
                case NamespaceR ns:
                {
                    var full = ns.Name + "." + name;
                    var type = _types.Resolve(full);
                    if (type != null) return new TypeR(type);
                    if (_types.IsNamespace(full)) return new NamespaceR(full);
                    throw Error($"'{full}' is neither a type nor a namespace the designer knows.", m);
                }
                case TypeR t:
                {
                    var member = FindValueMember(t.Type, name, isStatic: true, nonPublic: false);
                    if (member != null) return new ValueR(GetValue(member, null, m), MemberType(member));
                    var nested = t.Type.GetNestedType(name, BindingFlags.Public);
                    if (nested != null) return new TypeR(nested);
                    throw Error($"'{t.Type.FullName}' has no static member '{name}'.", m.Name);
                }
                case ValueR v:
                {
                    if (v.Value == null) throw Error($"'{m.Expression}' is null here.", m.Expression);
                    var member = FindValueMember(v.Value.GetType(), name, isStatic: false, nonPublic: ReferenceEquals(v.Value, _root))
                        ?? (v.Type is { IsInterface: true } ? FindInterfaceProperty(v.Type, name) : null)
                        ?? throw Error($"'{v.Value.GetType().FullName}' has no property '{name}'.", m.Name);
                    return new ValueR(GetValue(member, v.Value, m), MemberType(member));
                }
            }
            throw Error($"Cannot evaluate '{m}'.", m);
        }

        /// <summary>
        /// The C# "Color Color" rule: in <c>AutoScaleMode = AutoScaleMode.Font</c> the right-hand
        /// <c>AutoScaleMode</c> is both the form's property and the enum type, and the member decides.
        /// Returns the type when the name is such a value whose type has that name and the type has
        /// the static member; null otherwise.
        /// </summary>
        private TypeR? ColorColor(IdentifierNameSyntax head, string memberName)
        {
            var name = head.Identifier.ValueText;
            Type? valueType;
            if (_locals.TryGetValue(name, out var local)) valueType = local.Type;
            else if (_fields.TryGetValue(name, out var slot)) valueType = slot.DeclaredType;
            else
            {
                var m = FindValueMember(_root.GetType(), name, isStatic: false, nonPublic: true);
                if (m == null) return null;
                valueType = MemberType(m);
            }
            if (valueType == null || valueType.Name != name) return null;
            var type = _types.Resolve(name);
            if (type != valueType) return null;
            bool hasStatic = FindValueMember(type, memberName, isStatic: true, nonPublic: false) != null
                || type.GetNestedType(memberName, BindingFlags.Public) != null
                || type.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(mi => mi.Name == memberName);
            return hasStatic ? new TypeR(type) : null;
        }

        private ValueR EvalCreation(BaseObjectCreationExpressionSyntax n, Type? expected, string? componentName)
        {
            Type? type;
            string typeName;
            if (n is ObjectCreationExpressionSyntax explicitNew)
            {
                type = _types.Resolve(explicitNew.Type);
                typeName = explicitNew.Type.ToString();
            }
            else
            {
                type = expected ?? throw Error("'new(...)' needs a known target type.", n);
                typeName = type.Name;
            }

            object instance;
            bool placeholder = false;
            string? creationArguments = null;
            if (type == null)
            {
                // A control from the user's own project: stand in for it rather than refuse the file.
                // Its constructor arguments are not evaluated; they may name types we cannot resolve either.
                instance = new DesignerPlaceholder(typeName);
                type = typeof(DesignerPlaceholder);
                placeholder = true;
                creationArguments = n.ArgumentList?.Arguments.ToString();
            }
            else if (type == typeof(System.ComponentModel.ComponentResourceManager))
            {
                // new ComponentResourceManager(typeof(MainForm)): the form's resources, from its .resx.
                var resx = _path == null ? null : ResxResources.ResxPathOf(_path);
                if (resx == null || !File.Exists(resx))
                    throw Error($"The form keeps resources in a .resx, and {(resx == null ? "the designer file has no path" : Path.GetFileName(resx) + " is not next to it")}.", n);
                try { instance = ResxResources.Load(resx, name => _types.ResolveFullName(name)); }
                catch (System.Xml.XmlException ex) { throw Error($"{Path.GetFileName(resx)} is not a valid .resx: {ex.Message}", n, ex); }
            }
            else
            {
                if (typeof(Delegate).IsAssignableFrom(type))
                    throw Error("Delegates can only be created for event handlers (x.Click += new EventHandler(handler)).", n);
                var args = EvalArguments(n.ArgumentList);
                if (args.Length == 0 && type.IsValueType)
                {
                    instance = Activator.CreateInstance(type)!;
                }
                else
                {
                    if (type.IsAbstract || type.IsInterface) throw Error($"Cannot create an instance of '{type.FullName}'.", n);
                    var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                    var (ctor, converted) = PickOverload(ctors, args)
                        ?? throw Error($"'{type.FullName}' has no constructor that takes ({DescribeArgs(args)}).", n);
                    try { instance = ((ConstructorInfo)ctor).Invoke(converted); }
                    catch (Exception ex) when (IsLibraryControl(type))
                    {
                        // A control of the user's project or a library whose constructor throws: the form still
                        // opens, the control is a red cross saying why, and its statements are kept as written.
                        var inner = Unwrap(ex);
                        instance = new DesignerPlaceholder(typeName, $"{inner.GetType().Name}: {inner.Message}");
                        type = typeof(DesignerPlaceholder);
                        placeholder = true;
                        creationArguments = n.ArgumentList?.Arguments.ToString();
                    }
                    catch (Exception ex) { throw Error($"Creating '{type.FullName}' failed: {Unwrap(ex).Message}", n, Unwrap(ex)); }
                }
            }

            DesignerComponent? component = null;
            if (componentName != null || instance is IComponent)
            {
                component = new DesignerComponent(componentName, typeName, instance.GetType(), instance, isRoot: false, isPlaceholder: placeholder)
                {
                    CreationArguments = creationArguments,
                };
                _model.AddComponentCore(component);
            }

            if (n.Initializer != null) ApplyInitializer(instance, n.Initializer, component);
            return new ValueR(instance, type);
        }

        /// <summary>A control type that is not NetForms's or the BCL's: its failures are the user's code, not the file's.</summary>
        private static bool IsLibraryControl(Type type) =>
            typeof(System.Windows.Forms.Control).IsAssignableFrom(type) && !TypeResolver.DefaultAssemblies().Contains(type.Assembly);

        private void ApplyInitializer(object instance, InitializerExpressionSyntax init, DesignerComponent? component)
        {
            if (init.IsKind(SyntaxKind.ObjectInitializerExpression))
            {
                foreach (var e in init.Expressions)
                {
                    if (e is not AssignmentExpressionSyntax { Left: IdentifierNameSyntax prop } a || !a.IsKind(SyntaxKind.SimpleAssignmentExpression))
                        throw Error($"The designer does not understand this initializer ({e.Kind()}).", e);
                    var assignment = SetMember(instance, prop, a.Right, a, prop.Identifier.ValueText);
                    component?.AddAssignment(assignment);
                }
            }
            else if (init.IsKind(SyntaxKind.CollectionInitializerExpression))
            {
                foreach (var e in init.Expressions)
                {
                    var args = e is InitializerExpressionSyntax complex && complex.IsKind(SyntaxKind.ComplexElementInitializerExpression)
                        ? complex.Expressions.Select(x => Eval(x, null)).ToArray()
                        : new[] { Eval(e, null) };
                    Invoke(new ValueR(instance, instance.GetType()), "Add", args, e);
                }
            }
            else
            {
                throw Error($"The designer does not understand this initializer ({init.Kind()}).", init);
            }
        }

        private ValueR EvalArray(ArrayCreationExpressionSyntax ac)
        {
            var arrayType = _types.Resolve(ac.Type) ?? throw Error($"Unknown type '{ac.Type}'.", ac.Type);
            var elementType = arrayType.GetElementType()!;
            if (ac.Initializer == null)
            {
                var sizeExpr = ac.Type.RankSpecifiers[0].Sizes[0];
                var size = (int)System.Convert.ChangeType(Eval(sizeExpr, typeof(int)).Value!, typeof(int), CultureInfo.InvariantCulture);
                return new ValueR(Array.CreateInstance(elementType, size), arrayType);
            }
            return new ValueR(FillArray(elementType, ac.Initializer), arrayType);
        }

        private ValueR EvalImplicitArray(ImplicitArrayCreationExpressionSyntax iac)
        {
            var values = iac.Initializer.Expressions.Select(x => Eval(x, null)).ToList();
            var elementType = values.Select(v => v.Type).FirstOrDefault(t => t != null) ?? typeof(object);
            if (values.Any(v => v.Value != null && !elementType.IsInstanceOfType(v.Value))) elementType = typeof(object);
            var array = Array.CreateInstance(elementType, values.Count);
            for (int i = 0; i < values.Count; i++) array.SetValue(values[i].Value, i);
            return new ValueR(array, elementType.MakeArrayType());
        }

        private Array FillArray(Type elementType, InitializerExpressionSyntax init)
        {
            var array = Array.CreateInstance(elementType, init.Expressions.Count);
            for (int i = 0; i < init.Expressions.Count; i++)
            {
                var x = init.Expressions[i];
                array.SetValue(Convert(Eval(x, elementType), elementType, x), i);
            }
            return array;
        }

        private ValueR EvalInvocation(InvocationExpressionSyntax call)
        {
            var args = EvalArguments(call.ArgumentList);
            switch (call.Expression)
            {
                case IdentifierNameSyntax id:
                    return Invoke(new ValueR(_root, _root.GetType()), id.Identifier.ValueText, args, call);
                case MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax } m:
                    return Invoke(new ValueR(_root, _root.GetType()), m.Name.Identifier.ValueText, args, call);
                case MemberAccessExpressionSyntax m:
                {
                    var name = m.Name.Identifier.ValueText;
                    Resolved target = m.Expression is IdentifierNameSyntax head && ColorColor(head, name) is TypeR cc
                        ? cc : Resolve(m.Expression, null);
                    switch (target)
                    {
                        case TypeR t:
                            return InvokeStatic(t.Type, name, args, call);
                        case ValueR v:
                            return Invoke(v, name, args, call);
                        default:
                            throw Error($"'{m.Expression}' is a namespace.", m.Expression);
                    }
                }
                default:
                    throw Error($"The designer does not understand this call ({call.Expression.Kind()}).", call);
            }
        }

        private ValueR[] EvalArguments(ArgumentListSyntax? list)
        {
            if (list == null) return Array.Empty<ValueR>();
            var result = new ValueR[list.Arguments.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var arg = list.Arguments[i];
                if (arg.RefKindKeyword != default) throw Error("ref/out arguments are not supported in InitializeComponent.", arg);
                if (arg.NameColon != null) throw Error("Named arguments are not supported in InitializeComponent.", arg);
                result[i] = Eval(arg.Expression, null);
            }
            return result;
        }

        private ValueR Invoke(ValueR target, string name, ValueR[] args, SyntaxNode at)
        {
            if (target.Value == null) throw Error($"Calling '{name}' on a null reference.", at);
            bool isRoot = ReferenceEquals(target.Value, _root);
            var flags = BindingFlags.Public | BindingFlags.Instance | (isRoot ? BindingFlags.NonPublic : 0);
            var candidates = target.Value.GetType().GetMethods(flags)
                .Where(mi => mi.Name == name && (mi.IsPublic || mi.IsFamily || mi.IsFamilyOrAssembly))
                .Cast<MethodBase>().ToList();
            if (target.Type is { IsInterface: true } iface)
                candidates.AddRange(new[] { iface }.Concat(iface.GetInterfaces()).SelectMany(i => i.GetMethods()).Where(mi => mi.Name == name));

            if (target.Value is DesignerPlaceholder)
            {
                candidates = candidates.Where(m => m.DeclaringType!.IsInstanceOfType(target.Value)).ToList();
                if (candidates.Count == 0) return new ValueR(null, typeof(object));
            }

            var (method, converted) = PickOverload(candidates, args)
                ?? throw Error(candidates.Count == 0
                    ? $"'{target.Value.GetType().FullName}' has no method '{name}'."
                    : $"No overload of '{target.Value.GetType().FullName}.{name}' takes ({DescribeArgs(args)}).", at);
            var mi = (MethodInfo)method;
            try { return new ValueR(mi.Invoke(target.Value, converted), mi.ReturnType); }
            catch (Exception ex) { throw Error($"'{name}' failed: {Unwrap(ex).Message}", at, Unwrap(ex)); }
        }

        private ValueR InvokeStatic(Type type, string name, ValueR[] args, SyntaxNode at)
        {
            var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(mi => mi.Name == name).Cast<MethodBase>().ToList();
            var (method, converted) = PickOverload(candidates, args)
                ?? throw Error($"No static method '{type.FullName}.{name}' takes ({DescribeArgs(args)}).", at);
            var mi = (MethodInfo)method;
            try { return new ValueR(mi.Invoke(null, converted), mi.ReturnType); }
            catch (Exception ex) { throw Error($"'{name}' failed: {Unwrap(ex).Message}", at, Unwrap(ex)); }
        }

        private ValueR EvalElementAccess(ElementAccessExpressionSyntax ea)
        {
            var target = Eval(ea.Expression, null);
            if (target.Value == null) throw Error($"'{ea.Expression}' is null here.", ea.Expression);
            var args = EvalArguments(ea.ArgumentList.Arguments.Count == 0 ? null : SyntaxFactory.ArgumentList(ea.ArgumentList.Arguments));
            if (target.Value is Array array && args.Length == 1)
            {
                int index = (int)System.Convert.ChangeType(args[0].Value!, typeof(int), CultureInfo.InvariantCulture);
                return new ValueR(array.GetValue(index), array.GetType().GetElementType());
            }
            var getters = target.Value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == args.Length && p.GetMethod != null)
                .Select(p => (MethodBase)p.GetMethod!).ToList();
            var (getter, converted) = PickOverload(getters, args)
                ?? throw Error($"'{target.Value.GetType().FullName}' has no indexer that takes ({DescribeArgs(args)}).", ea);
            try { return new ValueR(getter.Invoke(target.Value, converted), ((MethodInfo)getter).ReturnType); }
            catch (Exception ex) { throw Error($"Indexing failed: {Unwrap(ex).Message}", ea, Unwrap(ex)); }
        }

        private ValueR EvalCast(CastExpressionSyntax cast)
        {
            var type = _types.Resolve(cast.Type) ?? throw Error($"Unknown type '{cast.Type}'.", cast.Type);
            var v = Eval(cast.Expression, type);
            if (v.Value == null)
            {
                if (type.IsValueType && Nullable.GetUnderlyingType(type) == null) throw Error($"Cannot cast null to '{type.FullName}'.", cast);
                return new ValueR(null, type);
            }
            var target = Nullable.GetUnderlyingType(type) ?? type;
            if (target.IsInstanceOfType(v.Value)) return new ValueR(v.Value, type);
            // ((ISupportInitialize)gauge1).BeginInit(): the placeholder cannot know what its real type implements.
            if (v.Value is DesignerPlaceholder) return new ValueR(v.Value, type);
            if (target.IsEnum && IsNumeric(v.Value.GetType())) return new ValueR(Enum.ToObject(target, ToInt64(v.Value)), type);
            if ((IsNumeric(target) || target == typeof(char)) && (IsNumeric(v.Value.GetType()) || v.Value is char || v.Value is Enum))
            {
                object source = v.Value is Enum ? ToInt64(v.Value) : v.Value;
                try { return new ValueR(System.Convert.ChangeType(source, target, CultureInfo.InvariantCulture), type); }
                catch (Exception ex) { throw Error($"Cannot convert {v.Value} to '{target.FullName}': {ex.Message}", cast); }
            }
            var op = FindConversionOperator(v.Value.GetType(), target);
            if (op != null) return new ValueR(op.Invoke(null, new[] { v.Value }), type);
            throw Error($"Cannot cast '{v.Value.GetType().FullName}' to '{type.FullName}'.", cast);
        }

        private ValueR EvalBinary(BinaryExpressionSyntax bin)
        {
            var l = Eval(bin.Left, null);
            var r = Eval(bin.Right, null);
            var kind = bin.Kind();

            if (kind == SyntaxKind.AddExpression && (l.Value is string || r.Value is string))
                return new ValueR(string.Concat(Invariant(l.Value), Invariant(r.Value)), typeof(string));

            if (kind is SyntaxKind.BitwiseOrExpression or SyntaxKind.BitwiseAndExpression or SyntaxKind.ExclusiveOrExpression)
            {
                if (l.Value is bool lb && r.Value is bool rb)
                    return new ValueR(kind switch { SyntaxKind.BitwiseOrExpression => lb | rb, SyntaxKind.BitwiseAndExpression => lb & rb, _ => lb ^ rb }, typeof(bool));
                if (l.Value == null || r.Value == null) throw Error("Bitwise operator on null.", bin);
                long a = ToInt64(l.Value), b = ToInt64(r.Value);
                long result = kind switch { SyntaxKind.BitwiseOrExpression => a | b, SyntaxKind.BitwiseAndExpression => a & b, _ => a ^ b };
                var enumType = l.Value is Enum ? l.Value.GetType() : r.Value is Enum ? r.Value.GetType() : null;
                if (enumType != null) return new ValueR(Enum.ToObject(enumType, result), enumType);
                var t = Promote(l.Value.GetType(), r.Value.GetType());
                return new ValueR(System.Convert.ChangeType(result, t, CultureInfo.InvariantCulture), t);
            }

            if (kind is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression)
            {
                if (l.Value == null || r.Value == null || !IsNumeric(l.Value.GetType()) || !IsNumeric(r.Value.GetType()))
                    throw Error($"The designer can only apply '{bin.OperatorToken.Text}' to numbers.", bin);
                var t = Promote(l.Value.GetType(), r.Value.GetType());
                object result;
                if (t == typeof(decimal))
                {
                    decimal a = System.Convert.ToDecimal(l.Value, CultureInfo.InvariantCulture), b = System.Convert.ToDecimal(r.Value, CultureInfo.InvariantCulture);
                    result = kind switch { SyntaxKind.AddExpression => a + b, SyntaxKind.SubtractExpression => a - b, SyntaxKind.MultiplyExpression => a * b, SyntaxKind.DivideExpression => a / b, _ => a % b };
                }
                else if (t == typeof(float) || t == typeof(double))
                {
                    double a = System.Convert.ToDouble(l.Value, CultureInfo.InvariantCulture), b = System.Convert.ToDouble(r.Value, CultureInfo.InvariantCulture);
                    result = kind switch { SyntaxKind.AddExpression => a + b, SyntaxKind.SubtractExpression => a - b, SyntaxKind.MultiplyExpression => a * b, SyntaxKind.DivideExpression => a / b, _ => a % b };
                }
                else
                {
                    long a = ToInt64(l.Value), b = ToInt64(r.Value);
                    if (b == 0 && kind is SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression) throw Error("Division by zero.", bin);
                    result = kind switch { SyntaxKind.AddExpression => a + b, SyntaxKind.SubtractExpression => a - b, SyntaxKind.MultiplyExpression => a * b, SyntaxKind.DivideExpression => a / b, _ => a % b };
                }
                return new ValueR(System.Convert.ChangeType(result, t, CultureInfo.InvariantCulture), t);
            }

            throw Error($"The designer does not understand the operator '{bin.OperatorToken.Text}'.", bin);
        }

        private ValueR EvalUnary(PrefixUnaryExpressionSyntax un)
        {
            var v = Eval(un.Operand, null);
            switch (un.Kind())
            {
                case SyntaxKind.UnaryPlusExpression when v.Value != null && IsNumeric(v.Value.GetType()):
                    return v;
                case SyntaxKind.UnaryMinusExpression when v.Value != null && IsNumeric(v.Value.GetType()):
                    object neg = v.Value switch
                    {
                        int i => -i,
                        long l => -l,
                        float f => -f,
                        double d => -d,
                        decimal m => -m,
                        short s => -s,
                        sbyte sb => -sb,
                        byte b => -b,
                        ushort us => -us,
                        uint ui => -(long)ui,
                        _ => throw Error($"Cannot negate '{un.Operand}'.", un),
                    };
                    return new ValueR(neg, neg.GetType());
                case SyntaxKind.LogicalNotExpression when v.Value is bool b:
                    return new ValueR(!b, typeof(bool));
                case SyntaxKind.BitwiseNotExpression when v.Value is Enum:
                    return new ValueR(Enum.ToObject(v.Value.GetType(), ~ToInt64(v.Value)), v.Value.GetType());
                case SyntaxKind.BitwiseNotExpression when v.Value is int i:
                    return new ValueR(~i, typeof(int));
                default:
                    throw Error($"The designer does not understand '{un}'.", un);
            }
        }

        // --- reflection helpers ------------------------------------------------------------------

        /// <summary>
        /// A property or field by name, most-derived first: NetForms re-declares many members with
        /// <c>new</c> (to change design-time attributes, as WinForms does), so a flat
        /// <c>GetProperty(name)</c> would be ambiguous.
        /// </summary>
        private static MemberInfo? FindValueMember(Type type, string name, bool isStatic, bool nonPublic)
        {
            var flags = BindingFlags.DeclaredOnly | BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance) | (nonPublic ? BindingFlags.NonPublic : 0);
            for (var t = type; t != null; t = t.BaseType)
            {
                foreach (var p in t.GetProperties(flags))
                {
                    if (p.Name != name || p.GetIndexParameters().Length != 0) continue;
                    var accessor = p.GetMethod ?? p.SetMethod!;
                    if (accessor.IsPublic || accessor.IsFamily || accessor.IsFamilyOrAssembly) return p;
                }
                var f = t.GetField(name, flags);
                if (f != null && (f.IsPublic || ((f.IsFamily || f.IsFamilyOrAssembly) && !isStatic))) return f;
            }
            return null;
        }

        private static PropertyInfo? FindInterfaceProperty(Type iface, string name) =>
            new[] { iface }.Concat(iface.GetInterfaces()).Select(i => i.GetProperty(name)).FirstOrDefault(p => p != null);

        private static EventInfo? FindEvent(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var e = t.GetEvent(name, BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (e != null) return e;
            }
            return null;
        }

        private static bool CanWrite(MemberInfo m) => m switch
        {
            PropertyInfo p => p.SetMethod != null && (p.SetMethod.IsPublic || p.SetMethod.IsFamily || p.SetMethod.IsFamilyOrAssembly),
            FieldInfo f => !f.IsInitOnly && !f.IsLiteral,
            _ => false,
        };

        private static Type MemberType(MemberInfo m) => m is PropertyInfo p ? p.PropertyType : ((FieldInfo)m).FieldType;

        private object? GetValue(MemberInfo m, object? owner, SyntaxNode at)
        {
            try { return m is PropertyInfo p ? p.GetValue(owner) : ((FieldInfo)m).GetValue(owner); }
            catch (Exception ex) { throw Error($"Reading '{m.Name}' failed: {Unwrap(ex).Message}", at, Unwrap(ex)); }
        }

        /// <summary>
        /// C#-style overload resolution, reduced to what designer code needs: arity (with optional
        /// and <c>params</c> parameters), then the best total match — identity beats a reference
        /// conversion, which beats a numeric one.
        /// </summary>
        private static (MethodBase Method, object?[] Args)? PickOverload(IEnumerable<MethodBase> candidates, ValueR[] args)
        {
            (MethodBase, object?[])? best = null;
            int bestScore = -1;
            foreach (var m in candidates)
            {
                var ps = m.GetParameters();
                if (TryBind(ps, args, expandParams: false, out var converted, out int score)
                    || (ps.Length > 0 && ps[^1].IsDefined(typeof(ParamArrayAttribute)) && TryBind(ps, args, expandParams: true, out converted, out score)))
                {
                    if (score > bestScore)
                    {
                        best = (m, converted);
                        bestScore = score;
                    }
                }
            }
            return best;
        }

        private static bool TryBind(ParameterInfo[] ps, ValueR[] args, bool expandParams, out object?[] converted, out int score)
        {
            converted = new object?[ps.Length];
            score = 0;
            int fixedCount = expandParams ? ps.Length - 1 : ps.Length;
            if (!expandParams && args.Length > ps.Length) return false;
            if (expandParams && args.Length < fixedCount) return false;

            for (int i = 0; i < fixedCount; i++)
            {
                if (i >= args.Length)
                {
                    if (!ps[i].HasDefaultValue) return false;
                    converted[i] = ps[i].DefaultValue is DBNull ? null : ps[i].DefaultValue;
                    continue;
                }
                if (!TryConvert(args[i], ps[i].ParameterType, out converted[i], out int s)) return false;
                score += s;
            }
            if (expandParams)
            {
                var elementType = ps[^1].ParameterType.GetElementType()!;
                var rest = Array.CreateInstance(elementType, args.Length - fixedCount);
                for (int i = fixedCount; i < args.Length; i++)
                {
                    if (!TryConvert(args[i], elementType, out var item, out int s)) return false;
                    rest.SetValue(item, i - fixedCount);
                    score += s;
                }
                converted[^1] = rest;
            }
            return true;
        }

        /// <summary>Converts to <paramref name="target"/> or stops the read, pointing at <paramref name="at"/>.</summary>
        private object? Convert(ValueR value, Type target, SyntaxNode at)
        {
            if (TryConvert(value, target, out var result, out _)) return result;
            var what = value.Value == null ? "null" : $"a value of type '{value.Value.GetType().FullName}'";
            throw Error($"Cannot use {what} where '{target.FullName}' is expected.", at);
        }

        /// <summary>
        /// Implicit conversions: identity (score 3), reference/boxing (2), user-defined or numeric (1),
        /// constant narrowing of an integer literal (0).
        /// </summary>
        private static bool TryConvert(ValueR value, Type target, out object? result, out int score)
        {
            result = null;
            score = 0;
            // ref structs (ReadOnlySpan<T> overloads of decimal, string, ...) cannot pass through reflection.
            if (target.IsByRef || target.IsByRefLike) return false;
            var v = value.Value;
            var underlying = Nullable.GetUnderlyingType(target);
            if (v == null)
            {
                if (target.IsValueType && underlying == null) return false;
                score = 2;
                return true;
            }
            if (underlying != null) target = underlying;
            var vt = v.GetType();
            if (vt == target) { result = v; score = 3; return true; }
            if (target.IsInstanceOfType(v)) { result = v; score = 2; return true; }
            if (IsNumeric(vt) && IsNumeric(target) && ImplicitNumeric(vt, target))
            {
                result = System.Convert.ChangeType(v, target, CultureInfo.InvariantCulture);
                score = 1;
                return true;
            }
            if (v is char c && IsNumeric(target) && target != typeof(sbyte) && target != typeof(byte) && target != typeof(short))
            {
                result = System.Convert.ChangeType((int)c, target, CultureInfo.InvariantCulture);
                score = 1;
                return true;
            }
            if (v is int i)
            {
                // Constant expression conversion: an int literal fits a narrower integral type.
                if (target.IsEnum && i == 0) { result = Enum.ToObject(target, 0); return true; }
                if (IsIntegral(target) && FitsIn(i, target))
                {
                    result = System.Convert.ChangeType(i, target, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            var op = FindConversionOperator(vt, target, implicitOnly: true);
            if (op != null)
            {
                result = op.Invoke(null, new[] { v });
                score = 1;
                return true;
            }
            return false;
        }

        private static MethodInfo? FindConversionOperator(Type from, Type to, bool implicitOnly = false)
        {
            foreach (var t in new[] { to, from })
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "op_Implicit" && (implicitOnly || m.Name != "op_Explicit")) continue;
                    if (m.ReturnType != to) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType.IsAssignableFrom(from)) return m;
                }
            return null;
        }

        private static readonly Type[] s_numeric =
        {
            typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
            typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal),
        };

        private static bool IsNumeric(Type t) => Array.IndexOf(s_numeric, t) >= 0;

        private static bool IsIntegral(Type t) => IsNumeric(t) && t != typeof(float) && t != typeof(double) && t != typeof(decimal);

        private static bool FitsIn(int value, Type t) => t switch
        {
            _ when t == typeof(sbyte) => value is >= sbyte.MinValue and <= sbyte.MaxValue,
            _ when t == typeof(byte) => value is >= byte.MinValue and <= byte.MaxValue,
            _ when t == typeof(short) => value is >= short.MinValue and <= short.MaxValue,
            _ when t == typeof(ushort) => value is >= ushort.MinValue and <= ushort.MaxValue,
            _ when t == typeof(uint) || t == typeof(ulong) => value >= 0,
            _ => true,
        };

        /// <summary>The C# implicit numeric conversion table (§10.2.3).</summary>
        private static bool ImplicitNumeric(Type from, Type to)
        {
            if (from == to) return true;
            Type[] targets = from switch
            {
                _ when from == typeof(sbyte) => new[] { typeof(short), typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(byte) => new[] { typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(short) => new[] { typeof(int), typeof(long), typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(ushort) => new[] { typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(int) => new[] { typeof(long), typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(uint) => new[] { typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(long) || from == typeof(ulong) => new[] { typeof(float), typeof(double), typeof(decimal) },
                _ when from == typeof(float) => new[] { typeof(double) },
                _ => Array.Empty<Type>(),
            };
            return Array.IndexOf(targets, to) >= 0;
        }

        /// <summary>The type of a binary numeric operation (binary numeric promotion, simplified).</summary>
        private static Type Promote(Type a, Type b)
        {
            if (a == typeof(decimal) || b == typeof(decimal)) return typeof(decimal);
            if (a == typeof(double) || b == typeof(double)) return typeof(double);
            if (a == typeof(float) || b == typeof(float)) return typeof(float);
            if (a == typeof(ulong) || b == typeof(ulong)) return typeof(ulong);
            if (a == typeof(long) || b == typeof(long)) return typeof(long);
            if (a == typeof(uint) || b == typeof(uint)) return typeof(uint);
            return typeof(int);
        }

        private static long ToInt64(object v) => v is Enum
            ? System.Convert.ToInt64(System.Convert.ChangeType(v, Enum.GetUnderlyingType(v.GetType()), CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
            : System.Convert.ToInt64(v, CultureInfo.InvariantCulture);

        private static string Invariant(object? v) => v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v?.ToString() ?? "";

        private static string DescribeArgs(ValueR[] args) =>
            string.Join(", ", args.Select(a => a.Value?.GetType().Name ?? "null"));

        private static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException { InnerException: { } inner }) ex = inner;
            return ex;
        }

        // --- positions ---------------------------------------------------------------------------

        private static int LineOf(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        private DesignerCodeException Error(string message, SyntaxNode node, Exception? inner = null) =>
            Error(message, node.GetLocation(), inner);

        private DesignerCodeException Error(string message, SyntaxToken token) => Error(message, token.GetLocation());

        private DesignerCodeException Error(string message, Location location, Exception? inner = null)
        {
            var pos = location.GetLineSpan().StartLinePosition;
            return new DesignerCodeException(message, _path, pos.Line + 1, pos.Character + 1, inner);
        }
    }
}
