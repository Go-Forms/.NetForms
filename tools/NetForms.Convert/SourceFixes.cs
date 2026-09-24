using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.Converter;

/// <summary>
/// An edit the conversion makes to a file other than the project file (decision 113): on line
/// <paramref name="Line"/>, <paramref name="Old"/> becomes <paramref name="New"/> (a line left blank by
/// a removal goes). Planned by <see cref="ProjectConverter.Analyze"/>, made by <see cref="ProjectConverter.Apply"/>,
/// which keeps the original next to the file (<c>.winforms.bak</c>).
/// </summary>
public sealed record ConvertFix(string File, int Line, string Description, string Old, string New);

/// <summary>
/// What the conversion can mend by itself because the mend cannot change what the program does: a
/// <c>using</c> of a namespace .NET does not have (it imports nothing - removing it removes nothing), a
/// file reference whose case only Windows forgives, an assembly .NET moved into a NuGet package.
/// </summary>
internal static class SourceFixes
{
    /// <summary>
    /// Assemblies of the .NET Framework that .NET ships as NuGet packages (the compiler names them in
    /// CS1069, "forwarded to assembly"), with the version to reference and whether they run only on Windows.
    /// System.Drawing.Common is not here: NetForms is it (decision 107), so a type forwarded there is one
    /// NetForms does not have yet.
    /// </summary>
    private static readonly Dictionary<string, (string Version, bool WindowsOnly)> s_packages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Data.SqlClient"] = ("4.9.0", false),
        ["System.Data.Odbc"] = ("10.0.0", false),
        ["System.Data.OleDb"] = ("10.0.0", true),
        ["System.IO.Ports"] = ("10.0.0", false),
        ["System.Runtime.Caching"] = ("10.0.0", false),
        ["System.CodeDom"] = ("10.0.0", false),
        ["System.ComponentModel.Composition"] = ("10.0.0", false),
        ["System.Security.Permissions"] = ("10.0.0", false),
        ["System.Security.Cryptography.Xml"] = ("10.0.0", false),
        ["System.Security.Cryptography.Pkcs"] = ("10.0.0", false),
        ["System.ServiceModel.Syndication"] = ("10.0.0", false),
        ["System.DirectoryServices"] = ("10.0.0", true),
        ["System.DirectoryServices.AccountManagement"] = ("10.0.0", true),
        ["System.Management"] = ("10.0.0", true),
        ["System.ServiceProcess.ServiceController"] = ("10.0.0", true),
        ["System.Speech"] = ("10.0.0", true),
        ["System.Diagnostics.EventLog"] = ("10.0.0", true),
        ["System.Diagnostics.PerformanceCounter"] = ("10.0.0", true),
        ["System.Threading.AccessControl"] = ("10.0.0", true),
    };

    /// <summary>
    /// Namespaces of the .NET Framework that .NET does not have, and no cross-platform package brings back:
    /// WPF's (a WinForms project picks them up from a stray IDE using), Remoting, ClickOnce, COM+, Web Forms.
    /// </summary>
    private static readonly string[] s_goneNamespaces =
    {
        "System.Windows.Controls", "System.Windows.Media", "System.Windows.Shapes", "System.Windows.Navigation",
        "System.Windows.Documents", "System.Windows.Markup", "System.Windows.Data", "System.Windows.Ink",
        "System.Windows.Interop", "System.Windows.Threading", "System.Windows.Automation",
        "System.Runtime.Remoting", "System.Deployment", "System.EnterpriseServices", "System.Web.UI",
        "System.Workflow", "System.Data.Linq", "Microsoft.Office.Interop",
    };

    internal static bool IsGoneFromDotNet(string ns)
    {
        ns = ns.StartsWith("global::", StringComparison.Ordinal) ? ns.Substring(8) : ns;
        if (ns.StartsWith("static ", StringComparison.Ordinal)) return false;
        return s_goneNamespaces.Any(g => ns == g || ns.StartsWith(g + ".", StringComparison.Ordinal));
    }

    private static readonly Regex s_forwardedTo = new(@"forwarded to assembly '([^,']+)");

    /// <summary>A compile error the conversion mends: an unresolvable using, or a type in a known package.</summary>
    public static bool TryMend(Diagnostic d, ConvertReport report)
    {
        if (d.Id is "CS0246" or "CS0234" && d.Location.SourceTree is { } tree)
        {
            var directive = tree.GetRoot().FindNode(d.Location.SourceSpan).AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
            // Only namespaces .NET is known not to have: an unresolved using may just as well come from a
            // NuGet package the analysis did not restore (Newtonsoft.Json), and that one must stay.
            if (directive != null && directive.Alias == null && IsGoneFromDotNet(directive.NamespaceOrType.ToString()))
            {
                var line = directive.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var text = directive.WithoutTrivia().ToFullString();
                if (!report.Fixes.Any(f => f.File == tree.FilePath && f.Line == line))
                {
                    report.Fixes.Add(new ConvertFix(tree.FilePath, line, $"Remove `{text}`: .NET has no such namespace, so it imports nothing.", text, ""));
                    report.Issues.Add(new ConvertIssue("info", "Unused using", $"`{text}` names a namespace .NET does not have; the conversion removes it.", tree.FilePath, line, 1));
                }
                return true;
            }
        }
        if (d.Id == "CS1069" && s_forwardedTo.Match(d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)) is { Success: true } m
            && s_packages.TryGetValue(m.Groups[1].Value, out var package))
        {
            var id = m.Groups[1].Value;
            if (!report.Packages.Any(p => p.StartsWith(id + " ", StringComparison.OrdinalIgnoreCase)))
            {
                report.Packages.Add($"{id} {package.Version}");
                report.Actions.Add($"Reference the package {id} {package.Version} (in .NET it is not part of the framework).");
                if (package.WindowsOnly)
                    report.Issues.Add(new ConvertIssue("warning", "Windows only", $"{id} works only on Windows.", d.Location.GetLineSpan().Path, d.Location.GetLineSpan().StartLinePosition.Line + 1));
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// File references in a .resx (<c>ResXFileRef</c>: "..\Resources\pawn.png;System.Drawing.Bitmap, ...")
    /// that name an existing file only when case is ignored - as Windows ignores it - are mended; ones that
    /// name no file at all are reported, since the build stops on them.
    /// </summary>
    public static void CheckResxFileRefs(string resx, XDocument document, ConvertReport report)
    {
        var dir = Path.GetDirectoryName(resx)!;
        foreach (var data in document.Root!.Elements("data"))
        {
            if (((string?)data.Attribute("type"))?.StartsWith("System.Resources.ResXFileRef", StringComparison.Ordinal) != true) continue;
            var value = data.Element("value");
            var path = value?.Value.Split(';')[0].Trim();
            if (string.IsNullOrEmpty(path)) continue;
            var full = Path.GetFullPath(Path.Combine(dir, path.Replace('\\', '/')));
            if (File.Exists(full)) continue;
            int line = ((System.Xml.IXmlLineInfo)value!).LineNumber;
            var actual = CasePath.Resolve(full);
            if (actual == null)
            {
                report.Issues.Add(new ConvertIssue("error", "Resources", $"The resource {(string?)data.Attribute("name")} refers to {path}, which does not exist; the build stops on it.", resx, line));
                continue;
            }
            var mended = Path.GetRelativePath(dir, actual).Replace('/', '\\');
            report.Fixes.Add(new ConvertFix(resx, line, $"Refer to {mended} as it is spelled on disk (not {path}).", path, mended));
        }
    }

    /// <summary>Makes the fixes, file by file, keeping each original as <c>.winforms.bak</c>.</summary>
    public static void Apply(IEnumerable<ConvertFix> fixes)
    {
        foreach (var group in fixes.GroupBy(f => f.File))
        {
            var bytes = File.ReadAllBytes(group.Key);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));
            var lines = text.Split('\n').ToList();
            var remove = new HashSet<int>();
            foreach (var fix in group)
            {
                int i = fix.Line - 1;
                if (i < 0 || i >= lines.Count || !lines[i].Contains(fix.Old, StringComparison.Ordinal)) continue;
                lines[i] = ReplaceFirst(lines[i], fix.Old, fix.New);
                if (fix.New.Length == 0 && lines[i].Trim().Length == 0) remove.Add(i);
            }
            var backup = group.Key + ".winforms.bak";
            if (!File.Exists(backup)) File.Copy(group.Key, backup);
            var result = string.Join("\n", lines.Where((_, i) => !remove.Contains(i)));
            File.WriteAllText(group.Key, result, new UTF8Encoding(bom));
        }
    }

    private static string ReplaceFirst(string s, string old, string @new)
    {
        int i = s.IndexOf(old, StringComparison.Ordinal);
        return s.Substring(0, i) + @new + s.Substring(i + old.Length);
    }
}

/// <summary>A path as Windows would find it: each name matched ignoring case (decision 112's rule, for the converter).</summary>
internal static class CasePath
{
    public static string? Resolve(string fullPath)
    {
        if (File.Exists(fullPath) || Directory.Exists(fullPath)) return fullPath;
        var root = Path.GetPathRoot(fullPath) ?? "/";
        var current = root;
        foreach (var part in fullPath.Substring(root.Length).Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var exact = Path.Combine(current, part);
            if (File.Exists(exact) || Directory.Exists(exact)) { current = exact; continue; }
            if (!Directory.Exists(current)) return null;
            var match = Directory.EnumerateFileSystemEntries(current).FirstOrDefault(e => string.Equals(Path.GetFileName(e), part, StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            current = match;
        }
        return current;
    }
}
