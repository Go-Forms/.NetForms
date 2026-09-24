// Second pass of the phase 5.0 markup: the re-declarations.
//
// WinForms hides inherited members that make no sense on a derived control (a ProgressBar has no
// Text, a Label is no tab stop) by re-declaring them with different ComponentModel attributes.
// There is no other way: TypeDescriptor reads attributes off the member, so changing what a
// derived type advertises means the derived type must declare the member.
//
// This pass compares the metadata of the real control with ours (both produced by
// tests/Shared/AttributeDump.cs), finds every member whose advertised metadata differs, pushes
// each difference as far up our own hierarchy as it will go (so subclasses inherit it), and
// writes the re-declaration - copying the type and accessor shape from our own base declaration,
// so the shadow forwards to exactly what it shadows.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

static class Shadow
{
    public sealed record Need(string TypeName, string MemberKey, MemberDump Desired);

    public static int Run(
        Dictionary<string, TypeDump> winforms,
        Dictionary<string, TypeDump> ours,
        Dictionary<string, SyntaxTree> trees,
        HashSet<string>? only,
        bool apply)
    {
        var classes = IndexClasses(trees);
        var needs = Collect(winforms, ours, classes, only);
        needs = PushUp(needs, ours, classes);

        var byFile = needs.GroupBy(n => classes[n.TypeName].File);
        int written = 0;

        foreach (var group in byFile)
        {
            var file = group.Key;
            var source = File.ReadAllText(file);
            var inserts = new List<(int Position, string Text)>();

            foreach (var perClass in group.GroupBy(n => n.TypeName))
            {
                var info = classes[perClass.Key];
                var body = new StringBuilder();
                body.Append(Environment.NewLine);
                body.Append("    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them").Append(Environment.NewLine);
                body.Append("    // off the derived type, so they have to be re-declared here to be advertised differently.").Append(Environment.NewLine);

                int count = 0;
                foreach (var need in perClass.OrderBy(n => n.MemberKey, StringComparer.Ordinal))
                {
                    var text = Declaration(need, classes, info);
                    if (text == null)
                    {
                        Console.WriteLine($"  skipped {need.TypeName}.{need.MemberKey}: no base declaration found in our sources");
                        continue;
                    }
                    body.Append(Environment.NewLine).Append(text);
                    count++;
                    written++;
                }

                if (count > 0)
                {
                    inserts.Add((info.CloseBrace, body.ToString()));
                    if (!apply) Console.WriteLine($"--- {perClass.Key} ---{Environment.NewLine}{body}");
                }
            }

            if (inserts.Count == 0) continue;
            var sb = new StringBuilder(source);
            foreach (var (position, text) in inserts.OrderByDescending(i => i.Position)) sb.Insert(position, text);
            if (apply) File.WriteAllText(file, sb.ToString());
        }

        return written;
    }

    // --- what needs shadowing -------------------------------------------------------------

    private static List<Need> Collect(
        Dictionary<string, TypeDump> winforms,
        Dictionary<string, TypeDump> ours,
        Dictionary<string, ClassInfo> classes,
        HashSet<string>? only)
    {
        var needs = new List<Need>();
        foreach (var (typeName, theirType) in winforms)
        {
            if (!ours.TryGetValue(typeName, out var ourType)) continue;
            if (!classes.ContainsKey(typeName)) continue;
            if (only != null && !only.Contains(typeName[(typeName.LastIndexOf('.') + 1)..])) continue;

            foreach (var (key, theirs) in theirType.Members)
            {
                if (!ourType.Members.TryGetValue(key, out var mine)) continue;
                if (Same(theirs, mine)) continue;
                needs.Add(new Need(typeName, key, theirs));
            }
        }
        return needs;
    }

    private static bool Same(MemberDump a, MemberDump b)
        => a.Browsable == b.Browsable
        && a.Serialization == b.Serialization
        && a.Localizable == b.Localizable
        && a.Category == b.Category
        && a.HasDefaultValue == b.HasDefaultValue
        && a.DefaultValue == b.DefaultValue;

    /// <summary>
    /// A difference that every subclass shares belongs on the shared base: declaring
    /// <c>ToolStripDropDown.CanOverflow</c> once saves declaring it on three subclasses.
    /// </summary>
    private static List<Need> PushUp(List<Need> needs, Dictionary<string, TypeDump> ours, Dictionary<string, ClassInfo> classes)
    {
        var index = needs.ToDictionary(n => (n.TypeName, n.MemberKey));
        var kept = new List<Need>();

        foreach (var need in needs)
        {
            // Drop it when a base class of ours carries the very same difference.
            bool coveredByBase = false;
            for (var b = BaseOf(need.TypeName, classes); b != null; b = BaseOf(b, classes))
            {
                if (index.TryGetValue((b, need.MemberKey), out var baseNeed) && Same(baseNeed.Desired, need.Desired))
                {
                    coveredByBase = true;
                    break;
                }
            }
            if (!coveredByBase) kept.Add(need);
        }
        return kept;
    }

    private static string? BaseOf(string typeName, Dictionary<string, ClassInfo> classes)
        => classes.TryGetValue(typeName, out var info) ? info.BaseTypeName : null;

    // --- writing the declaration ----------------------------------------------------------

    private static string? Declaration(Need need, Dictionary<string, ClassInfo> classes, ClassInfo target)
    {
        var isEvent = need.MemberKey.StartsWith("event:", StringComparison.Ordinal);
        var name = isEvent ? need.MemberKey["event:".Length..] : need.MemberKey;

        // Already declared here? Then the difference is ours to fix by hand, not by a second
        // declaration - emitting one would not even compile.
        if (DeclaredOn(target.FullName, name, isEvent)) return null;

        var (declaringType, node) = FindInBases(target, name, isEvent, classes);
        if (node == null) return null;

        var attrs = Attributes(need.Desired);
        var sb = new StringBuilder();
        foreach (var a in attrs) sb.Append("    ").Append(a).Append(Environment.NewLine);

        if (isEvent)
        {
            var type = ((EventFieldDeclarationSyntax)node).Declaration.Type.ToString();
            sb.Append($"    public new event {type} {name}").Append(Environment.NewLine);
            sb.Append("    {").Append(Environment.NewLine);
            sb.Append($"        add => base.{name} += value;").Append(Environment.NewLine);
            sb.Append($"        remove => base.{name} -= value;").Append(Environment.NewLine);
            sb.Append("    }").Append(Environment.NewLine);
            return sb.ToString();
        }

        var property = (PropertyDeclarationSyntax)node;
        var kind = property.Modifiers.Any(SyntaxKind.VirtualKeyword) || property.Modifiers.Any(SyntaxKind.OverrideKeyword)
            ? "override"
            : "new";
        var settable = property.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration) || a.IsKind(SyntaxKind.InitAccessorDeclaration)) == true;

        sb.Append($"    public {kind} {property.Type} {name} ");
        sb.Append(settable
            ? $"{{ get => base.{name}; set => base.{name} = value; }}"
            : $"=> base.{name};");
        sb.Append(Environment.NewLine);
        _ = declaringType;
        return sb.ToString();
    }

    private static bool DeclaredOn(string typeName, string name, bool isEvent)
        => Partials.TryGetValue(typeName, out var parts) && parts.SelectMany(p => p.Members).Any(m =>
            (isEvent && m is EventFieldDeclarationSyntax ev && ev.Declaration.Variables.Any(v => v.Identifier.Text == name))
            || (isEvent && m is EventDeclarationSyntax ev2 && ev2.Identifier.Text == name)
            || (!isEvent && m is PropertyDeclarationSyntax p && p.Identifier.Text == name));

    private static (string?, MemberDeclarationSyntax?) FindInBases(ClassInfo start, string name, bool isEvent, Dictionary<string, ClassInfo> classes)
    {
        for (var b = start.BaseTypeName; b != null; b = BaseOf(b, classes))
        {
            if (!classes.TryGetValue(b, out _)) break;
            // Control is split over several files, so every partial part has to be searched.
            foreach (var member in Partials[b].SelectMany(p => p.Members))
            {
                if (isEvent && member is EventFieldDeclarationSyntax ev
                    && ev.Declaration.Variables.Count == 1
                    && ev.Declaration.Variables[0].Identifier.Text == name)
                {
                    return (b, ev);
                }
                if (!isEvent && member is PropertyDeclarationSyntax p && p.Identifier.Text == name)
                {
                    return (b, p);
                }
            }
        }
        return (null, null);
    }

    private static List<string> Attributes(MemberDump m)
    {
        // A shadow exists only to advertise something different, so it states everything
        // explicitly - see Gen.AttributesFor.
        var list = Gen.AttributesFor(m, explicitOverrides: true);
        return list.Where(a => !a.StartsWith("[Description", StringComparison.Ordinal)).ToList();
    }

    // --- our own class index --------------------------------------------------------------

    public sealed record ClassInfo(string FullName, string File, ClassDeclarationSyntax Node, string? BaseTypeName, int CloseBrace);

    private static Dictionary<string, ClassInfo> IndexClasses(Dictionary<string, SyntaxTree> trees)
    {
        var byName = new Dictionary<string, ClassInfo>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (file, tree) in trees)
        {
            foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.Parent is TypeDeclarationSyntax) continue;
                var ns = NamespaceOfNode(cls);
                if (ns is null) continue;
                declared.Add(ns + "." + cls.Identifier.Text);
            }
        }

        foreach (var (file, tree) in trees)
        {
            foreach (var cls in tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (cls.Parent is TypeDeclarationSyntax) continue;
                var ns = NamespaceOfNode(cls);
                if (ns is null) continue;
                var full = ns + "." + cls.Identifier.Text;

                // Control is split over several files; the one holding the base list wins, and the
                // others still have to be searchable for member declarations.
                var baseName = cls.BaseList?.Types
                    .Select(t => ns + "." + t.Type.ToString())
                    .FirstOrDefault(declared.Contains);

                if (byName.TryGetValue(full, out var existing))
                {
                    Partials.TryAdd(full, new List<ClassDeclarationSyntax>());
                    Partials[full].Add(cls);
                    if (baseName != null && existing.BaseTypeName == null)
                        byName[full] = existing with { BaseTypeName = baseName };
                    continue;
                }

                Partials[full] = new List<ClassDeclarationSyntax> { cls };
                byName[full] = new ClassInfo(full, file, cls, baseName, cls.CloseBraceToken.SpanStart);
            }
        }
        return byName;
    }

    public static readonly Dictionary<string, List<ClassDeclarationSyntax>> Partials = new(StringComparer.Ordinal);

    private static string? NamespaceOfNode(SyntaxNode node)
    {
        for (var n = node.Parent; n != null; n = n.Parent)
        {
            if (n is BaseNamespaceDeclarationSyntax nd) return nd.Name.ToString();
        }
        return null;
    }
}
