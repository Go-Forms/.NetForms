using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.ApiDiff;

/// <summary>One missing type, or one missing member of a type NetForms has, and how much real code uses it.</summary>
/// <param name="Type">Full name, as in the coverage tables (<c>System.Windows.Forms.ListView</c>).</param>
/// <param name="Member">Null when the whole type is missing; otherwise the member's <see cref="ApiUsage.Key(MemberInfo)"/>.</param>
/// <param name="TypeDisplay">How C# code names the type: <c>ListView</c>, <c>ImageList.ImageCollection</c>.</param>
/// <param name="Display">How C# code names the row: <c>ListView.VirtualMode</c>, <c>DomainUpDown</c>.</param>
/// <param name="Repositories">The repositories whose code uses it, ordered by name.</param>
/// <param name="Uses">References in the code, all repositories together.</param>
public sealed record UsageRow(string Type, string? Member, string TypeDisplay, string Display, IReadOnlyList<string> Repositories, int Uses);

/// <summary>The ranked rows, and how much code they were found in: the repositories, their C# projects, and every
/// reference to a WinForms type or member (there or not) - the size of what was checked.</summary>
public sealed record UsageScan(List<UsageRow> Rows, IReadOnlyList<string> Repositories, int Projects, int References);

/// <summary>
/// Decision 146: which of the API NetForms lacks real projects use. Every C# project under a folder of
/// repositories (the corpus clones, one folder each) is compiled by Roslyn against the <b>real</b>
/// WinForms reference assemblies, and every reference to a WinForms type or member is looked up:
/// missing type, or missing member of a type NetForms has, or there. Compiling against the real
/// assemblies (not against NetForms) is what makes the answer precise: the symbol is bound as the C#
/// compiler binds it - the overload, the declaring type, the overridden member - instead of guessed from
/// names in the text, and code NetForms cannot compile yet is read just as well as code it can.
/// </summary>
public static class ApiUsage
{
    /// <summary>The assemblies whose surface the coverage tables describe (Program.cs, realTypes).</summary>
    private static readonly HashSet<string> s_winFormsAssemblies = new(StringComparer.Ordinal)
    {
        "System.Windows.Forms", "System.Windows.Forms.Primitives", "System.Drawing.Common",
    };

    /// <summary>The Microsoft.WindowsDesktop.App.Ref directory this build downloaded (see the .csproj).</summary>
    public static string DefaultReferencePack => typeof(ApiUsage).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .First(a => a.Key == "WindowsDesktopRef").Value!;

    /// <summary>
    /// Scans <paramref name="root"/>: each subfolder is a repository, each <c>.csproj</c> in it a compilation of
    /// the <c>.cs</c> files below it (a file belongs to the nearest project above it; files without one form a
    /// compilation of their own). <paramref name="isMissing"/> answers for a type (member null) whether NetForms
    /// lacks the type, and for a member of a type NetForms has whether it lacks the member - declared there or
    /// inherited. Rows are ranked by the number of repositories, then by uses.
    /// </summary>
    public static UsageScan Scan(string root, string referencePack, Func<string, string?, bool> isMissing)
    {
        var references = References(referencePack);
        var found = new Dictionary<(string Type, string? Member), (string TypeDisplay, string Display, SortedSet<string> Repos, int Uses)>();
        var repositories = new List<string>();
        int projects = 0, apiReferences = 0;
        foreach (var repo in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal))
        {
            var repoName = Path.GetFileName(repo);
            repositories.Add(repoName);
            foreach (var (name, files, usings) in Compilations(repo))
            {
                projects++;
                var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), s_parse, f)).ToList();
                if (usings.Count > 0)
                    trees.Add(CSharpSyntaxTree.ParseText(string.Concat(usings.Select(u => $"global using global::{u};\n")), s_parse, "GlobalUsings.g.cs"));
                var compilation = CSharpCompilation.Create(name, trees, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
                foreach (var tree in trees)
                {
                    var model = compilation.GetSemanticModel(tree);
                    foreach (var (symbol, node) in References(tree.GetRoot(), model))
                    {
                        if (Classify(symbol) is not var (type, typeDisplay, member, display)) continue;
                        apiReferences++;
                        (string, string?) entry;
                        if (isMissing(type, null))
                        {
                            // `panel.Controls.Count`: WinForms declares Count on ArrangedElementCollection, which NetForms
                            // lacks, but NetForms' Control.ControlCollection has it - the code compiles.
                            if (member != null && AvailableThroughReceiver(Receiver(node, model), symbol.ContainingType.OriginalDefinition, member, isMissing)) continue;
                            entry = (type, null);
                            display = typeDisplay;
                        }
                        else if (member != null && isMissing(type, member)) entry = (type, member);
                        else continue;
                        if (!found.TryGetValue(entry, out var row)) row = (typeDisplay, display, new SortedSet<string>(StringComparer.Ordinal), 0);
                        row.Repos.Add(repoName);
                        found[entry] = (row.TypeDisplay, row.Display, row.Repos, row.Uses + 1);
                    }
                }
            }
        }
        var rows = found.Select(p => new UsageRow(p.Key.Type, p.Key.Member, p.Value.TypeDisplay, p.Value.Display, p.Value.Repos.ToList(), p.Value.Uses))
            .OrderByDescending(r => r.Repositories.Count).ThenByDescending(r => r.Uses)
            .ThenBy(r => r.Type, StringComparer.Ordinal).ThenBy(r => r.Member, StringComparer.Ordinal).ToList();
        return new UsageScan(rows, repositories, projects, apiReferences);
    }

    private static readonly CSharpParseOptions s_parse = new(LanguageVersion.Preview);

    /// <summary>
    /// The projects of one repository: name, source files, global usings (those <c>ImplicitUsings</c> of a
    /// WinForms SDK project gives, plus its <c>&lt;Using Include&gt;</c> items).
    /// </summary>
    private static IEnumerable<(string Name, List<string> Files, List<string> Usings)> Compilations(string repo)
    {
        static bool Skipped(string path) => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(p => p is "bin" or "obj" or ".git" or "packages");
        var projects = Directory.EnumerateFiles(repo, "*.csproj", SearchOption.AllDirectories).Where(p => !Skipped(p))
            .Select(p => (Dir: Path.GetDirectoryName(p)!, File: p)).OrderByDescending(p => p.Dir.Length).ToList();
        var byProject = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(repo, "*.cs", SearchOption.AllDirectories).Where(f => !Skipped(Path.GetRelativePath(repo, f))))
        {
            var owner = projects.FirstOrDefault(p => file.StartsWith(p.Dir + Path.DirectorySeparatorChar, StringComparison.Ordinal)).File ?? "";
            if (!byProject.TryGetValue(owner, out var list)) byProject[owner] = list = new List<string>();
            list.Add(file);
        }
        foreach (var (project, files) in byProject.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var usings = new List<string>();
            if (project.Length > 0)
            {
                var text = File.ReadAllText(project);
                if (Regex.IsMatch(text, @"<ImplicitUsings>\s*(enable|true)\s*</ImplicitUsings>", RegexOptions.IgnoreCase))
                    usings.AddRange(new[] { "System", "System.Collections.Generic", "System.Drawing", "System.IO", "System.Linq", "System.Net.Http", "System.Threading", "System.Threading.Tasks", "System.Windows.Forms" });
                foreach (Match m in Regex.Matches(text, @"<Using\s+Include=""([\w.]+)""\s*/>"))
                    if (!usings.Contains(m.Groups[1].Value)) usings.Add(m.Groups[1].Value);
            }
            files.Sort(StringComparer.Ordinal);
            yield return (project.Length > 0 ? Path.GetFileNameWithoutExtension(project) : Path.GetFileName(repo), files, usings);
        }
    }

    /// <summary>The reference pack plus the running .NET's own assemblies (the reference pack wins a name both have).</summary>
    private static List<MetadataReference> References(string referencePack)
    {
        var pack = Directory.GetFiles(referencePack, "*.dll");
        var packNames = pack.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return pack.Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll").Where(f => !packNames.Contains(Path.GetFileName(f))))
            .Where(HasMetadata).Select(f => (MetadataReference)MetadataReference.CreateFromFile(f)).ToList();

        static bool HasMetadata(string file)
        {
            try
            {
                using var reader = new PEReader(File.OpenRead(file));
                return reader.HasMetadata;
            }
            catch (BadImageFormatException) { return false; }
        }
    }

    /// <summary>
    /// Every API symbol the code refers to, once per reference: names (types, members, enum values), object
    /// creations and constructor initializers (the constructor), indexers, user-defined operators, attributes,
    /// and the members user code overrides (overriding needs the member).
    /// </summary>
    private static IEnumerable<(ISymbol Symbol, SyntaxNode Node)> References(SyntaxNode root, SemanticModel model)
    {
        foreach (var node in root.DescendantNodes())
        {
            ISymbol? symbol = null;
            switch (node)
            {
                case SimpleNameSyntax name:
                    // `var` binds to the type it stands for, which the initializer already names.
                    if (name is IdentifierNameSyntax { IsVar: true }) continue;
                    // The type in `new T(...)` and `[T(...)]` is counted through its constructor.
                    if (name.Parent is ObjectCreationExpressionSyntax creation && creation.Type == name) continue;
                    if (name.Parent is QualifiedNameSyntax { Parent: ObjectCreationExpressionSyntax c2 } q && c2.Type == q && q.Right == name) continue;
                    if (name.Parent is AttributeSyntax || name.Parent is QualifiedNameSyntax { Parent: AttributeSyntax }) continue;
                    symbol = Bound(model.GetSymbolInfo(name));
                    break;
                case BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax or AttributeSyntax:
                    symbol = Bound(model.GetSymbolInfo(node));
                    if (symbol == null && node is ObjectCreationExpressionSyntax oc) symbol = model.GetTypeInfo(oc).Type;
                    if (symbol == null && node is AttributeSyntax at) symbol = Bound(model.GetSymbolInfo(at.Name))?.ContainingType;
                    break;
                case ElementAccessExpressionSyntax or BinaryExpressionSyntax or PrefixUnaryExpressionSyntax:
                    symbol = Bound(model.GetSymbolInfo(node));
                    if (symbol is IMethodSymbol { MethodKind: MethodKind.BuiltinOperator }) symbol = null;
                    break;
                case MemberDeclarationSyntax declaration when declaration.Modifiers.Any(SyntaxKind.OverrideKeyword):
                    symbol = model.GetDeclaredSymbol(declaration) switch
                    {
                        IMethodSymbol m => m.OverriddenMethod,
                        IPropertySymbol p => p.OverriddenProperty,
                        IEventSymbol e => e.OverriddenEvent,
                        _ => null,
                    };
                    // Through the user's own classes to the first member the framework declares.
                    while (symbol != null && !IsWinForms(symbol.ContainingType))
                        symbol = symbol switch { IMethodSymbol m => m.OverriddenMethod, IPropertySymbol p => p.OverriddenProperty, IEventSymbol e => e.OverriddenEvent, _ => null };
                    break;
            }
            if (symbol != null) yield return (symbol, node);
        }
    }

    /// <summary>
    /// For a member whose declaring type NetForms lacks: whether the most derived type between the one the code
    /// reaches it through and the declaring type that NetForms has, has the member (declared or inherited).
    /// </summary>
    private static bool AvailableThroughReceiver(ITypeSymbol? receiver, INamedTypeSymbol declaring, string member, Func<string, string?, bool> isMissing)
    {
        for (var t = receiver as INamedTypeSymbol; t != null && !SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, declaring); t = t.BaseType)
        {
            if (IsWinForms(t) && !isMissing(FullName(t.OriginalDefinition), null)) return !isMissing(FullName(t.OriginalDefinition), member);
        }
        return false;
    }

    /// <summary>The static type a member is reached through: <c>x</c>'s in <c>x.M</c> and <c>x?.M</c>, the enclosing class's for a plain <c>M</c>.</summary>
    private static ITypeSymbol? Receiver(SyntaxNode node, SemanticModel model) => node switch
    {
        SimpleNameSyntax { Parent: MemberAccessExpressionSyntax access } name when access.Name == name => model.GetTypeInfo(access.Expression).Type,
        SimpleNameSyntax { Parent: MemberBindingExpressionSyntax binding } => binding.FirstAncestorOrSelf<ConditionalAccessExpressionSyntax>() is { } conditional ? model.GetTypeInfo(conditional.Expression).Type : null,
        SimpleNameSyntax => model.GetEnclosingSymbol(node.SpanStart)?.ContainingType,
        _ => null,
    };

    /// <summary>The bound symbol, or the only candidate when binding failed for a reason around it.</summary>
    private static ISymbol? Bound(SymbolInfo info) => info.Symbol ?? (info.CandidateSymbols.Length == 1 ? info.CandidateSymbols[0] : null);

    private static bool IsWinForms(ITypeSymbol? type) => type?.ContainingAssembly is { } a && s_winFormsAssemblies.Contains(a.Name);

    /// <summary>A WinForms type (member null) or member: the declaring type's full and C# names, the member key, a C# name.</summary>
    private static (string Type, string TypeDisplay, string? Member, string Display)? Classify(ISymbol symbol)
    {
        switch (symbol)
        {
            case INamedTypeSymbol type when IsWinForms(type):
                type = type.OriginalDefinition;
                return (FullName(type), ShortName(type), null, ShortName(type));
            case IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet } accessor when accessor.AssociatedSymbol != null:
                return Classify(accessor.AssociatedSymbol);
            case IMethodSymbol { MethodKind: MethodKind.EventAdd or MethodKind.EventRemove } accessor when accessor.AssociatedSymbol != null:
                return Classify(accessor.AssociatedSymbol);
            case IMethodSymbol { ReducedFrom: { } extension }:
                return Classify(extension);
            case IMethodSymbol or IPropertySymbol or IEventSymbol or IFieldSymbol when IsWinForms(symbol.ContainingType):
                var declaring = symbol.ContainingType.OriginalDefinition;
                var member = symbol.OriginalDefinition;
                var name = member is IMethodSymbol { MethodKind: MethodKind.Constructor } ? "new " + ShortName(declaring) + "(" + string.Join(", ", ((IMethodSymbol)member).Parameters.Select(p => p.Type.Name)) + ")"
                    : member is IPropertySymbol { IsIndexer: true } ? ShortName(declaring) + "[]"
                    : member is IMethodSymbol m && m.Parameters.Length > 0 && declaring.GetMembers(m.Name).Length > 1 ? ShortName(declaring) + "." + m.Name + "(" + string.Join(", ", m.Parameters.Select(p => p.Type.Name)) + ")"
                    : ShortName(declaring) + "." + member.Name;
                return (FullName(declaring), ShortName(declaring), Key(member), name);
            default:
                return null;
        }
    }

    private static string ShortName(INamedTypeSymbol type) =>
        (type.ContainingType is { } outer ? ShortName(outer) + "." : "") + type.Name + (type.Arity > 0 ? "<" + string.Join(",", type.TypeParameters.Select(t => t.Name)) + ">" : "");

    /// <summary>As <c>Type.FullName</c> with '.' for nesting - the key of the coverage tables.</summary>
    private static string FullName(INamedTypeSymbol type) => type.ContainingType is { } outer
        ? FullName(outer) + "." + type.MetadataName
        : (type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() + "." : "") + type.MetadataName;

    // Member keys: kind, name and the parameter types - the same string for a member read by reflection from
    // the reference pack (to list what NetForms lacks) and for the symbol Roslyn binds in user code. Return
    // types count for methods only (conversion operators differ by nothing else).

    /// <summary>The key of a member Roslyn bound (its original definition).</summary>
    public static string Key(ISymbol member) => member switch
    {
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } c => $"C:({Parameters(c.Parameters)})",
        IMethodSymbol m => $"M:{TypeRef(m.ReturnType)} {m.Name}{(m.Arity > 0 ? "``" + m.Arity : "")}({Parameters(m.Parameters)})",
        IPropertySymbol p => $"P:{p.MetadataName}{(p.Parameters.Length > 0 ? "[" + Parameters(p.Parameters) + "]" : "")}",
        IEventSymbol e => $"E:{e.Name}",
        IFieldSymbol f => $"F:{f.Name}",
        _ => member.Name,
    };

    /// <summary>The key of a member read by reflection (MetadataLoadContext).</summary>
    public static string Key(MemberInfo member) => member switch
    {
        ConstructorInfo c => $"C:({Parameters(c.GetParameters())})",
        MethodInfo m => $"M:{TypeRef(m.ReturnType)} {m.Name}{(m.IsGenericMethodDefinition ? "``" + m.GetGenericArguments().Length : "")}({Parameters(m.GetParameters())})",
        PropertyInfo p => $"P:{p.Name}{(p.GetIndexParameters().Length > 0 ? "[" + Parameters(p.GetIndexParameters()) + "]" : "")}",
        EventInfo e => $"E:{e.Name}",
        FieldInfo f => $"F:{f.Name}",
        _ => member.Name,
    };

    private static string Parameters(IEnumerable<IParameterSymbol> parameters) =>
        string.Join(",", parameters.Select(p => (p.RefKind != RefKind.None ? "ref " : "") + TypeRef(p.Type)));

    private static string Parameters(IEnumerable<ParameterInfo> parameters) => string.Join(",", parameters.Select(p => TypeRef(p.ParameterType)));

    private static string TypeRef(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol a => TypeRef(a.ElementType) + "[" + new string(',', a.Rank - 1) + "]",
        IPointerTypeSymbol p => TypeRef(p.PointedAtType) + "*",
        ITypeParameterSymbol t => t.Name,
        INamedTypeSymbol { Arity: > 0 } g => GenericName(g) + "<" + string.Join(",", g.TypeArguments.Select(TypeRef)) + ">",
        INamedTypeSymbol n => FullName(n),
        IDynamicTypeSymbol => "System.Object",
        _ => type.ToDisplayString(),
    };

    private static string GenericName(INamedTypeSymbol type)
    {
        var name = FullName(type);
        return name.Substring(0, name.LastIndexOf('`'));
    }

    private static string TypeRef(Type type)
    {
        if (type.IsByRef) return "ref " + TypeRef(type.GetElementType()!);
        if (type.IsArray) return TypeRef(type.GetElementType()!) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
        if (type.IsPointer) return TypeRef(type.GetElementType()!) + "*";
        if (type.IsGenericParameter) return type.Name;
        if (type.IsGenericType)
        {
            var def = (type.GetGenericTypeDefinition().FullName ?? type.Name).Replace('+', '.');
            return def.Substring(0, def.LastIndexOf('`')) + "<" + string.Join(",", type.GetGenericArguments().Select(TypeRef)) + ">";
        }
        return (type.FullName ?? type.Name).Replace('+', '.');
    }

    /// <summary>The ranking as a Markdown page (docs/api/usage.md).</summary>
    public static string Markdown(UsageScan scan, Func<string, string?, string> status)
    {
        var (rows, repositories) = (scan.Rows, scan.Repositories);
        var lines = new List<string>
        {
            "# Missing API by use",
            "",
            "Generated by `dotnet run --project tools/NetForms.ApiDiff -- --usage <corpus clones> --out docs/api/usage.md` - do not edit by hand.",
            "[All namespaces](README.md).",
            "",
            $"Which of the API NetForms lacks real WinForms code uses, and how widely: the {repositories.Count} repositories of",
            "the project corpus (`tests/corpus/corpus.json`, cloned at their pinned commits by `CorpusTests` with",
            "`NETFORMS_CORPUS=1`) are compiled against the real WinForms reference assemblies, and every reference to a",
            "type or member NetForms does not have is counted. *Repositories* - in how many of them the code uses it; *uses* -",
            "references in the code. Ranked by repositories, then uses. A member of a type NetForms lacks entirely counts",
            "towards the type.",
            "",
            $"Checked: {scan.Projects} C# projects, {scan.References} references to WinForms types and members; {rows.Sum(r => r.Uses)} of",
            $"them are to API NetForms lacks ({rows.Count} types and members). Repositories: " + string.Join(", ", repositories) + ".",
            "",
        };
        var byType = rows.GroupBy(r => r.Type)
            .Select(g => (Type: g.Key, Repos: g.SelectMany(r => r.Repositories).Distinct().Count(), Uses: g.Sum(r => r.Uses), Rows: g.ToList()))
            .OrderByDescending(t => t.Repos).ThenByDescending(t => t.Uses).ThenBy(t => t.Type, StringComparer.Ordinal).ToList();
        lines.Add("## By type");
        lines.Add("");
        lines.Add($"{byType.Count} types: {byType.Count(t => t.Rows.Any(r => r.Member == null))} missing, {byType.Count(t => t.Rows.All(r => r.Member != null))} partial.");
        lines.Add("");
        lines.Add("| Type | Status | Repositories | Uses | Used and missing |");
        lines.Add("|---|---|---:|---:|---|");
        foreach (var t in byType)
        {
            var whole = t.Rows.Any(r => r.Member == null);
            var link = $"[{t.Type}](https://learn.microsoft.com/dotnet/api/{t.Type.ToLowerInvariant().Replace('`', '-')})";
            var what = whole ? "the type" : string.Join(", ", t.Rows.Take(6).Select(r => $"`{MemberPart(r)}`")) + (t.Rows.Count > 6 ? $", +{t.Rows.Count - 6}" : "");
            lines.Add($"| {link} | {status(t.Type, null)} | {t.Repos} | {t.Uses} | {what} |");
        }
        lines.Add("");
        lines.Add("## By member");
        lines.Add("");
        lines.Add("| Type or member | Repositories | Uses | Where |");
        lines.Add("|---|---:|---:|---|");
        foreach (var r in rows)
            lines.Add($"| `{r.Display}` | {r.Repositories.Count} | {r.Uses} | {string.Join(", ", r.Repositories.Take(4))}{(r.Repositories.Count > 4 ? ", …" : "")} |");
        return string.Join("\n", lines) + "\n";

        // `ImageList.ImageCollection.Add(String, Icon)` under its type is `Add(String, Icon)`; a constructor stays `new T(...)`.
        static string MemberPart(UsageRow r) => r.Display.StartsWith(r.TypeDisplay + ".", StringComparison.Ordinal) ? r.Display.Substring(r.TypeDisplay.Length + 1) : r.Display;
    }
}
