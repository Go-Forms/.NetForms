using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.Design.Serialization;

/// <summary>
/// Resolves type names the way the C# compiler would for a designer file, but against a fixed set
/// of assemblies (NetForms, System.Drawing, the BCL) instead of a compilation.
/// </summary>
internal sealed class TypeResolver
{
    /// <summary>
    /// Namespaces searched for simple names before the file's own usings. A WinForms project gets
    /// <c>System.Windows.Forms</c> and <c>System.Drawing</c> as implicit global usings, and a designer
    /// file relies on them; putting them first also settles <c>Timer</c> the way a WinForms author means it.
    /// </summary>
    private static readonly string[] s_implicitUsings =
    {
        "System.Windows.Forms", "System.Drawing", "System", "System.ComponentModel",
        "System.Collections.Generic", "System.IO", "System.Linq", "System.Threading", "System.Threading.Tasks",
    };

    private readonly Dictionary<string, Type> _byFullName = new(StringComparer.Ordinal);
    private readonly HashSet<string> _namespaces = new(StringComparer.Ordinal);
    private readonly List<string> _usings = new();
    private readonly Dictionary<string, string> _aliases = new(StringComparer.Ordinal);

    public TypeResolver(IEnumerable<Assembly> assemblies)
    {
        foreach (var asm in assemblies)
        {
            Type[] types;
            try { types = asm.GetExportedTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
            catch (Exception) { continue; } // a library whose dependencies are missing: its types stay unknown (placeholders)
            foreach (var t in types)
            {
                var name = (t.FullName ?? t.Name).Replace('+', '.');
                int tick = name.IndexOf('`');
                if (tick >= 0) name = name.Substring(0, tick) + "`" + t.GetGenericArguments().Length;
                _byFullName.TryAdd(name, t);
                var ns = t.Namespace;
                while (!string.IsNullOrEmpty(ns))
                {
                    _namespaces.Add(ns);
                    int dot = ns.LastIndexOf('.');
                    ns = dot < 0 ? null : ns.Substring(0, dot);
                }
            }
        }
    }

    /// <summary>The usings in effect for the file being read, innermost scope last.</summary>
    public void SetScope(IEnumerable<string> usings, IEnumerable<KeyValuePair<string, string>> aliases, string? currentNamespace)
    {
        _usings.Clear();
        _aliases.Clear();
        _usings.AddRange(s_implicitUsings);
        foreach (var u in usings)
            if (!_usings.Contains(u)) _usings.Add(u);
        // Types of the enclosing namespaces are visible without a using, innermost first.
        var ns = currentNamespace;
        while (!string.IsNullOrEmpty(ns))
        {
            if (!_usings.Contains(ns)) _usings.Add(ns);
            int dot = ns.LastIndexOf('.');
            ns = dot < 0 ? null : ns.Substring(0, dot);
        }
        foreach (var kv in aliases) _aliases[kv.Key] = kv.Value;
    }

    public bool IsNamespace(string dottedName) => _namespaces.Contains(dottedName);

    /// <summary>The type with exactly this full name (dots for nesting), ignoring usings; null if none.</summary>
    public Type? ResolveFullName(string fullName) => _byFullName.TryGetValue(fullName, out var t) ? t : null;

    /// <summary>A dotted name, fully qualified or relative to the usings; null if unknown.</summary>
    public Type? Resolve(string dottedName, int genericArity = 0)
    {
        if (dottedName.StartsWith("global::", StringComparison.Ordinal)) dottedName = dottedName.Substring(8);
        string key(string n) => genericArity == 0 ? n : n + "`" + genericArity;

        int firstDot = dottedName.IndexOf('.');
        string head = firstDot < 0 ? dottedName : dottedName.Substring(0, firstDot);
        if (_aliases.TryGetValue(head, out var target))
        {
            var aliased = firstDot < 0 ? target : target + dottedName.Substring(firstDot);
            if (_byFullName.TryGetValue(key(aliased), out var at)) return at;
        }

        if (_byFullName.TryGetValue(key(dottedName), out var t)) return t;
        foreach (var u in _usings)
            if (_byFullName.TryGetValue(key(u + "." + dottedName), out t)) return t;
        return null;
    }

    /// <summary>Resolves a type as written in source; null if it names no known type.</summary>
    public Type? Resolve(TypeSyntax syntax)
    {
        switch (syntax)
        {
            case PredefinedTypeSyntax p:
                return Predefined(p.Keyword.Kind());
            case NullableTypeSyntax n:
                var inner = Resolve(n.ElementType);
                return inner == null ? null : inner.IsValueType ? typeof(Nullable<>).MakeGenericType(inner) : inner;
            case ArrayTypeSyntax a:
                var element = Resolve(a.ElementType);
                if (element == null) return null;
                foreach (var rank in a.RankSpecifiers)
                    element = rank.Rank == 1 ? element.MakeArrayType() : element.MakeArrayType(rank.Rank);
                return element;
            case GenericNameSyntax g:
                return MakeGeneric(Resolve(g.Identifier.ValueText, g.TypeArgumentList.Arguments.Count), g.TypeArgumentList);
            case QualifiedNameSyntax q when q.Right is GenericNameSyntax qg:
                return MakeGeneric(Resolve(Dotted(q.Left) + "." + qg.Identifier.ValueText, qg.TypeArgumentList.Arguments.Count), qg.TypeArgumentList);
            case AliasQualifiedNameSyntax aq:
                return Resolve(aq.Name);
            case NameSyntax name:
                return Resolve(Dotted(name));
            default:
                return null;
        }
    }

    private Type? MakeGeneric(Type? definition, TypeArgumentListSyntax args)
    {
        if (definition == null) return null;
        var types = new Type[args.Arguments.Count];
        for (int i = 0; i < types.Length; i++)
        {
            var t = Resolve(args.Arguments[i]);
            if (t == null) return null;
            types[i] = t;
        }
        return definition.MakeGenericType(types);
    }

    /// <summary><c>System.Windows.Forms.Button</c> for a qualified name syntax; aliases like <c>global::</c> dropped.</summary>
    public static string Dotted(NameSyntax name) => name switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        QualifiedNameSyntax q => Dotted(q.Left) + "." + Dotted(q.Right),
        AliasQualifiedNameSyntax aq => Dotted(aq.Name),
        GenericNameSyntax g => g.Identifier.ValueText,
        _ => name.ToString(),
    };

    public static Type? Predefined(SyntaxKind keyword) => keyword switch
    {
        SyntaxKind.BoolKeyword => typeof(bool),
        SyntaxKind.ByteKeyword => typeof(byte),
        SyntaxKind.SByteKeyword => typeof(sbyte),
        SyntaxKind.ShortKeyword => typeof(short),
        SyntaxKind.UShortKeyword => typeof(ushort),
        SyntaxKind.IntKeyword => typeof(int),
        SyntaxKind.UIntKeyword => typeof(uint),
        SyntaxKind.LongKeyword => typeof(long),
        SyntaxKind.ULongKeyword => typeof(ulong),
        SyntaxKind.FloatKeyword => typeof(float),
        SyntaxKind.DoubleKeyword => typeof(double),
        SyntaxKind.DecimalKeyword => typeof(decimal),
        SyntaxKind.CharKeyword => typeof(char),
        SyntaxKind.StringKeyword => typeof(string),
        SyntaxKind.ObjectKeyword => typeof(object),
        _ => null,
    };

    /// <summary>The assemblies a designer file can reach without the user's project.</summary>
    public static IEnumerable<Assembly> DefaultAssemblies() => new[]
    {
        typeof(System.Windows.Forms.Control).Assembly,   // NetForms
        typeof(System.Drawing.Graphics).Assembly,         // NetForms.Drawing
        typeof(System.Drawing.Point).Assembly,            // System.Drawing.Primitives
        typeof(object).Assembly,                          // System.Private.CoreLib
        typeof(Component).Assembly,                       // System.ComponentModel.Primitives
        typeof(TypeConverter).Assembly,                   // System.ComponentModel.TypeConverter
        typeof(Uri).Assembly,
    }.Distinct();
}
