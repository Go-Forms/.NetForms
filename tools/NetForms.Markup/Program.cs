// Generates the phase 5.0 ComponentModel markup for NetForms sources out of metadata dumps taken
// from the real System.Windows.Forms and from ourselves (tests/Shared/AttributeDump.cs).
//
//   MarkupTool mark   <winforms-attrs-text.json> <src/NetForms> [--apply] [--only A,B]
//   MarkupTool shadow <winforms-attrs-text.json> <netforms-attrs.json> <src/NetForms> [--apply] [--only A,B]
//
// "mark" puts attributes on the members we declare; "shadow" re-declares the inherited members
// whose advertised metadata has to differ on a derived control. Without --apply both only report.

using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

var mode = args[0];
var apply = args.Contains("--apply");
var onlyArg = Array.IndexOf(args, "--only");
var only = onlyArg >= 0 ? args[onlyArg + 1].Split(',').ToHashSet(StringComparer.Ordinal) : null;
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

var winforms = JsonSerializer.Deserialize<Dictionary<string, TypeDump>>(File.ReadAllText(args[1]), json)!;
var srcPath = mode == "shadow" ? args[3] : args[2];

var files = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
             && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToArray();

var trees = new Dictionary<string, SyntaxTree>(StringComparer.Ordinal);
foreach (var file in files)
{
    var tree = CSharpSyntaxTree.ParseText(SourceText.From(File.ReadAllText(file)), path: file);
    trees[file] = tree;
    foreach (var e in tree.GetRoot().DescendantNodes().OfType<EnumDeclarationSyntax>())
    {
        Gen.Enums[e.Identifier.Text] = e.Members.Select(m => m.Identifier.Text).ToHashSet(StringComparer.Ordinal);
    }
}

if (mode == "shadow")
{
    var ours = JsonSerializer.Deserialize<Dictionary<string, TypeDump>>(File.ReadAllText(args[2]), json)!;
    var count = Shadow.Run(winforms, ours, trees, only, apply);
    Console.WriteLine($"{count} members re-declared{(apply ? " (written)" : " (dry run)")}");
    return;
}

int changedFiles = 0, markedMembers = 0;
var report = new StringBuilder();

foreach (var file in files)
{
    var root = trees[file].GetRoot();
    var edits = new List<(int Position, string Text)>();
    bool needsComponentModel = false;

    foreach (var cls in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
    {
        if (cls is not (ClassDeclarationSyntax or RecordDeclarationSyntax)) continue;
        if (cls.Parent is TypeDeclarationSyntax) continue;          // nested: never a component
        var ns = NamespaceOf(cls);
        if (ns is null) continue;
        var fullName = ns + "." + cls.Identifier.Text;
        if (!winforms.TryGetValue(fullName, out var typeDump)) continue;
        if (only != null && !only.Contains(cls.Identifier.Text)) continue;

        foreach (var member in cls.Members)
        {
            switch (member)
            {
                case PropertyDeclarationSyntax p when IsPublicInstance(p.Modifiers):
                    TryMark(p, p.Identifier.Text, typeDump);
                    break;
                case EventFieldDeclarationSyntax ev when IsPublicInstance(ev.Modifiers):
                    if (ev.Declaration.Variables.Count == 1)
                        TryMark(ev, "event:" + ev.Declaration.Variables[0].Identifier.Text, typeDump);
                    break;
                case EventDeclarationSyntax ev2 when IsPublicInstance(ev2.Modifiers):
                    TryMark(ev2, "event:" + ev2.Identifier.Text, typeDump);
                    break;
            }
        }

        void TryMark(MemberDeclarationSyntax node, string key, TypeDump td)
        {
            if (!td.Members.TryGetValue(key, out var meta)) return;
            if (node.AttributeLists.Count > 0) return;               // already marked

            // A member we re-declare has to restate what it inherits (see Gen.AttributesFor).
            var modifiers = node switch
            {
                PropertyDeclarationSyntax p2 => p2.Modifiers,
                EventFieldDeclarationSyntax e2 => e2.Modifiers,
                EventDeclarationSyntax e3 => e3.Modifiers,
                _ => default,
            };
            var redeclared = modifiers.Any(SyntaxKind.OverrideKeyword) || modifiers.Any(SyntaxKind.NewKeyword);

            var attrs = Gen.AttributesFor(meta, redeclared);
            if (attrs.Count == 0) return;

            var indent = IndentOf(node);
            // Inserted at the declaration keyword, i.e. after the doc comment and after the
            // existing indent - so the first attribute needs no indent and the declaration that
            // follows needs one. A run of one-line properties would become unreadable once each
            // grows a four-line attribute block, so a blank separator goes in too.
            var text = string.Join(Environment.NewLine + indent, attrs) + Environment.NewLine + indent;
            edits.Add((node.GetLocation().SourceSpan.Start, text));
            if (LineStartNeedingBlank(node) is int lineStart) edits.Add((lineStart, Environment.NewLine));

            needsComponentModel = true;
            markedMembers++;
            report.AppendLine($"{fullName}.{key}: {string.Join(" ", attrs)}");
        }
    }

    if (edits.Count == 0) continue;

    var sb = new StringBuilder(File.ReadAllText(file));
    foreach (var (position, text) in edits.OrderByDescending(e => e.Position)) sb.Insert(position, text);
    var updated = sb.ToString();
    if (needsComponentModel) updated = EnsureUsing(updated, "System.ComponentModel");

    changedFiles++;
    if (apply) File.WriteAllText(file, updated);
}

Console.WriteLine(report.ToString());
Console.WriteLine($"{markedMembers} members in {changedFiles} files{(apply ? " (written)" : " (dry run)")}");
return;

static bool IsPublicInstance(SyntaxTokenList modifiers)
    => modifiers.Any(SyntaxKind.PublicKeyword) && !modifiers.Any(SyntaxKind.StaticKeyword);

static string? NamespaceOf(SyntaxNode node)
{
    for (var n = node.Parent; n != null; n = n.Parent)
    {
        if (n is BaseNamespaceDeclarationSyntax nd) return nd.Name.ToString();
    }
    return null;
}

/// <summary>
/// Position of the start of the member's line when a blank line has to go in front of the
/// attribute block, or null when the line above is already blank, a comment, or the opening
/// brace of the type.
/// </summary>
static int? LineStartNeedingBlank(SyntaxNode node)
{
    var text = node.SyntaxTree.GetText();
    var line = text.Lines.GetLineFromPosition(node.GetLocation().SourceSpan.Start);
    if (line.LineNumber == 0) return null;

    var previous = text.Lines[line.LineNumber - 1].ToString().Trim();
    if (previous.Length == 0 || previous.EndsWith('{') || previous.StartsWith("//")) return null;
    return line.Start;
}

/// <summary>The whitespace the member itself starts its line with.</summary>
static string IndentOf(SyntaxNode node)
{
    var line = node.SyntaxTree.GetText().Lines.GetLineFromPosition(node.GetLocation().SourceSpan.Start);
    var text = line.ToString();
    return text[..(text.Length - text.TrimStart().Length)];
}

static string EnsureUsing(string source, string ns)
{
    if (source.Contains($"using {ns};", StringComparison.Ordinal)) return source;
    var firstUsing = source.IndexOf("using ", StringComparison.Ordinal);
    if (firstUsing < 0) return $"using {ns};{Environment.NewLine}{Environment.NewLine}{source}";
    return source.Insert(firstUsing, $"using {ns};{Environment.NewLine}");
}
