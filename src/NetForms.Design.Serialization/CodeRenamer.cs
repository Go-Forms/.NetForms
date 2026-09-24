using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.Design.Serialization;

/// <summary>
/// Renames members of a form class in its hand-written half (<c>MainForm.cs</c>): the field of a renamed
/// component and the handlers named after it. Symbols are resolved by Roslyn against the designer file
/// that still declares them, so only what really refers to the member changes - a local variable or a
/// parameter that happens to share the name, a string, a comment, another class's member, stay as they are.
/// </summary>
public static class CodeRenamer
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> s_references = new(() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator)
            .Concat(new[] { typeof(System.Windows.Forms.Control).Assembly.Location, typeof(System.Drawing.Graphics).Assembly.Location })
            .Where(p => p.Length > 0 && File.Exists(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToList());

    /// <summary>The implicit usings of a WinForms (and NetForms) project.</summary>
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Drawing;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using System.Windows.Forms;
        """;

    /// <summary>
    /// Returns <paramref name="code"/> with every reference to the members of <paramref name="className"/>
    /// named by the keys of <paramref name="renames"/> - and the declarations of those declared in it -
    /// renamed to the values. <paramref name="designerSource"/> is the other half of the class, before the
    /// rename. The text is unchanged where nothing refers to them.
    /// </summary>
    public static string Rename(string code, string designerSource, string className, IReadOnlyDictionary<string, string> renames)
    {
        if (renames.Count == 0) return code;
        var codeTree = CSharpSyntaxTree.ParseText(code, path: "Code.cs");
        var trees = new[] { CSharpSyntaxTree.ParseText(GlobalUsings), CSharpSyntaxTree.ParseText(designerSource, path: "Designer.cs"), codeTree };
        var compilation = CSharpCompilation.Create("NetFormsRename", trees, s_references.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var type = compilation.GetSymbolsWithName(className, SymbolFilter.Type).OfType<INamedTypeSymbol>()
            .FirstOrDefault(t => t.DeclaringSyntaxReferences.Any(r => r.SyntaxTree == codeTree));
        if (type == null) return code;
        var targets = type.GetMembers().Where(m => renames.ContainsKey(m.Name) && m is IFieldSymbol or IMethodSymbol or IPropertySymbol)
            .ToHashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (targets.Count == 0) return code;

        var model = compilation.GetSemanticModel(codeTree);
        var root = codeTree.GetRoot();
        var tokens = new List<SyntaxToken>();
        foreach (var name in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (!renames.ContainsKey(name.Identifier.ValueText)) continue;
            var info = model.GetSymbolInfo(name);
            var symbol = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
            // A method group (button1_Click passed as a delegate) binds to the method itself.
            if (symbol is IMethodSymbol { ReducedFrom: { } reduced }) symbol = reduced;
            if (symbol != null && targets.Contains(symbol.OriginalDefinition)) tokens.Add(name.Identifier);
        }
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            if (renames.ContainsKey(method.Identifier.ValueText) && model.GetDeclaredSymbol(method) is { } declared && targets.Contains(declared))
                tokens.Add(method.Identifier);

        if (tokens.Count == 0) return code;
        var renamed = root.ReplaceTokens(tokens, (original, _) =>
            SyntaxFactory.Identifier(original.LeadingTrivia, renames[original.ValueText], original.TrailingTrivia));
        return renamed.ToFullString();
    }

    /// <summary>Whether the hand-written half declares a member called <paramref name="name"/> in <paramref name="className"/>.</summary>
    public static bool Declares(string code, string className, string name) =>
        CSharpSyntaxTree.ParseText(code).GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t.Identifier.ValueText == className)
            .SelectMany(t => t.Members)
            .Any(m => m switch
            {
                MethodDeclarationSyntax method => method.Identifier.ValueText == name,
                PropertyDeclarationSyntax property => property.Identifier.ValueText == name,
                FieldDeclarationSyntax field => field.Declaration.Variables.Any(v => v.Identifier.ValueText == name),
                _ => false,
            });
}
