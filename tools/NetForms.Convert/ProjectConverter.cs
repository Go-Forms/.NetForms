using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NetForms.Design.Serialization;

namespace NetForms.Converter;

/// <summary>One finding of the analysis, with a place to jump to when it has one.</summary>
public sealed record ConvertIssue(string Severity, string Category, string Message, string? File = null, int? Line = null, int? Column = null);

public sealed record ConvertForm(string File, bool Opens, string? Message);

public sealed class ConvertReport
{
    public string Project { get; set; } = "";
    public bool SdkStyle { get; set; }
    public List<string> TargetFrameworks { get; set; } = new();
    public bool UsesWindowsForms { get; set; }
    public bool AlreadyNetForms { get; set; }
    public List<string> Actions { get; set; } = new();
    public List<ConvertIssue> Issues { get; set; } = new();
    public List<ConvertForm> Forms { get; set; } = new();

    /// <summary>Edits to sources and resources the conversion makes itself (decision 113).</summary>
    public List<ConvertFix> Fixes { get; set; } = new();

    /// <summary>NuGet packages the conversion references ("Id Version"): assemblies .NET moved out of the framework.</summary>
    public List<string> Packages { get; set; } = new();

    /// <summary>The sources compile against NetForms as they are (before any hand edits).</summary>
    public bool Compiled { get; set; }
    public bool Applied { get; set; }
    public string? Backup { get; set; }
}

public enum ConvertTarget
{
    /// <summary><c>net10.0</c>: builds and runs on Windows and Linux.</summary>
    Cross,

    /// <summary><c>net10.0-windows</c>: NetForms instead of WinForms, still a Windows project.</summary>
    Windows,
}

/// <summary>
/// Moves a WinForms project to NetForms (Ф5.7, docs/PLAN.md). Since NetForms is a drop-in copy of the
/// API, the move is mostly the project file: <c>UseWindowsForms</c> becomes a reference to NetForms,
/// the TFM loses <c>-windows</c> (for Linux), and the implicit WinForms usings are spelled out. The
/// analysis says beforehand what will not carry over: code that does not compile against NetForms
/// (API NetForms does not have yet), Windows-only calls, forms the designer cannot open.
/// </summary>
public sealed class ProjectConverter
{
    public const string PackageVersion = "0.0.1";

    /// <summary>The usings the .NET SDK adds with ImplicitUsings (Microsoft.NET.Sdk), plus the two UseWindowsForms adds.</summary>
    private static readonly string[] s_implicitUsings =
    {
        "System", "System.Collections.Generic", "System.IO", "System.Linq", "System.Net.Http",
        "System.Threading", "System.Threading.Tasks", "System.Drawing", "System.Windows.Forms",
    };

    private static readonly Regex s_windowsAbsolutePath = new(@"^[A-Za-z]:\\[^\r\n\t""<>|*?]*$");
    private static readonly Regex s_windowsRelativePath = new(@"\\[\w .-]+\.[A-Za-z0-9]{1,5}$|^\.{0,2}\\?[\w .-]+\\[\w .-]+\\");

    /// <summary>The APIs of NetForms that open a file and read a Windows path on Linux too (decision 112).</summary>
    private static readonly HashSet<string> s_netFormsFileApis = new(StringComparer.Ordinal)
    {
        "FromFile", "Bitmap", "Icon", "Image", "Save", "AddFontFile", "Load",
    };

    private static readonly Regex s_windowsDlls = new(@"^(user32|gdi32|comctl32|kernel32|shell32|uxtheme|dwmapi|advapi32|ole32|comdlg32)(\.dll)?$", RegexOptions.IgnoreCase);

    public ConvertReport Analyze(string projectPath, ConvertTarget target = ConvertTarget.Cross, string? frameworkPath = null)
    {
        projectPath = Path.GetFullPath(projectPath);
        var report = new ConvertReport { Project = projectPath };
        var doc = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
        var root = doc.Root ?? throw new InvalidDataException("The project file is empty.");
        var ns = root.Name.Namespace;

        report.SdkStyle = root.Attribute("Sdk") != null || root.Elements(ns + "Sdk").Any();
        report.TargetFrameworks = Properties(root, "TargetFramework").Concat(Properties(root, "TargetFrameworks").SelectMany(v => v.Split(';')))
            .Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();
        if (!report.SdkStyle)
        {
            // .NET Framework: the project file is rewritten in the SDK style as part of the move (decision 111).
            report.TargetFrameworks.AddRange(Properties(root, "TargetFrameworkVersion"));
            var legacy = new LegacyProject(projectPath, root);
            report.UsesWindowsForms = legacy.UsesWindowsForms;
            // A library without WinForms is converted too: a WinForms project that references it builds only
            // when it is an SDK project as well (a .NET Framework one needs the Framework's reference assemblies).
            if (!report.UsesWindowsForms)
                report.Issues.Add(new ConvertIssue("info", "Project", "The project does not use Windows Forms; it is moved to the SDK style and .NET 10 so that the projects referencing it build."));
            legacy.ToSdkProject(NewTfm("", report.UsesWindowsForms ? target : ConvertTarget.Cross), report.UsesWindowsForms ? ReferenceElement(frameworkPath, Path.GetDirectoryName(projectPath)!) : null, report.UsesWindowsForms && frameworkPath != null);
            report.Actions.AddRange(legacy.Actions);
            report.Actions.Add($"Target framework {string.Join(", ", report.TargetFrameworks)} → {NewTfm("", target)}{(target == ConvertTarget.Cross ? " (so it builds and runs on Linux too)" : "")}.");
            if (report.UsesWindowsForms) report.Actions.Add($"Reference NetForms: {Reference(frameworkPath, Path.GetDirectoryName(projectPath)!)}");
            report.Issues.AddRange(legacy.Issues);
            AnalyzeSources(projectPath, ReadSources(projectPath), report, target);
            return report;
        }

        report.UsesWindowsForms = Properties(root, "UseWindowsForms").Any(v => v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase));
        report.AlreadyNetForms = References(root, "PackageReference").Any(r => r.Equals("NetForms", StringComparison.OrdinalIgnoreCase))
            || References(root, "ProjectReference").Any(r => Path.GetFileName(r).Equals("NetForms.csproj", StringComparison.OrdinalIgnoreCase));
        if (!report.UsesWindowsForms && !report.AlreadyNetForms)
            report.Issues.Add(new ConvertIssue("info", "Project", "The project does not use Windows Forms (no <UseWindowsForms>true</UseWindowsForms>); there is nothing to convert."));

        if (report.UsesWindowsForms) PlanActions(root, report, target, frameworkPath, Path.GetDirectoryName(projectPath)!);
        if (Properties(root, "UseWPF").Any(v => v.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)))
            report.Issues.Add(new ConvertIssue("error", "WPF", "The project also uses WPF (UseWPF), which NetForms does not replace."));
        foreach (var p in new[] { "ApplicationHighDpiMode", "ApplicationVisualStyles", "ApplicationDefaultFont", "ApplicationUseCompatibleTextRendering" })
            if (Properties(root, p).Any())
                report.Issues.Add(new ConvertIssue("info", "Application configuration", $"<{p}> configures the ApplicationConfiguration class WinForms generates; NetForms' ApplicationConfiguration.Initialize() uses its own defaults (Segoe UI 9pt, visual styles on)."));

        AnalyzeSources(projectPath, ReadSources(projectPath), report, target);
        return report;
    }

    /// <summary>Analyses, then rewrites the project file (keeping the original next to it) when there is something to convert.</summary>
    public ConvertReport Apply(string projectPath, ConvertTarget target = ConvertTarget.Cross, string? frameworkPath = null)
    {
        var report = Analyze(projectPath, target, frameworkPath);
        if ((report.SdkStyle && !report.UsesWindowsForms) || report.AlreadyNetForms) return report;

        projectPath = Path.GetFullPath(projectPath);
        var backup = projectPath + ".winforms.bak";
        if (!File.Exists(backup)) File.Copy(projectPath, backup);
        report.Backup = backup;

        var doc = XDocument.Load(projectPath, LoadOptions.PreserveWhitespace);
        if (report.SdkStyle)
        {
            Rewrite(doc, target, frameworkPath, Path.GetDirectoryName(projectPath)!, report.Packages);
            var settings = new System.Xml.XmlWriterSettings { OmitXmlDeclaration = doc.Declaration == null, Indent = false, Encoding = new System.Text.UTF8Encoding(false) };
            using (var writer = System.Xml.XmlWriter.Create(projectPath, settings)) doc.Save(writer);
        }
        else
        {
            var sdk = new LegacyProject(projectPath, doc.Root!).ToSdkProject(NewTfm("", report.UsesWindowsForms ? target : ConvertTarget.Cross),
                report.UsesWindowsForms ? ReferenceElement(frameworkPath, Path.GetDirectoryName(projectPath)!) : null, report.UsesWindowsForms && frameworkPath != null, report.Packages);
            var settings = new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true, Indent = true, IndentChars = "  ", Encoding = new System.Text.UTF8Encoding(false) };
            var text = new System.Text.StringBuilder();
            using (var writer = System.Xml.XmlWriter.Create(text, settings)) sdk.Save(writer);
            // Laid out as Visual Studio lays out a new project: a blank line around each group.
            var laidOut = Regex.Replace(text.ToString(), @"\n  <(PropertyGroup|ItemGroup)>", "\n\n  <$1>").Replace("\n</Project>", "\n\n</Project>");
            File.WriteAllText(projectPath, laidOut.ReplaceLineEndings() + Environment.NewLine, new System.Text.UTF8Encoding(false));
        }
        SourceFixes.Apply(report.Fixes);
        report.Applied = true;
        return report;
    }

    // --- the project file ---------------------------------------------------------------------------

    private static IEnumerable<string> Properties(XElement root, string name) =>
        root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").Elements().Where(e => e.Name.LocalName == name).Select(e => e.Value);

    private static IEnumerable<string> References(XElement root, string kind) =>
        root.Descendants().Where(e => e.Name.LocalName == kind).Select(e => (string?)e.Attribute("Include") ?? "");

    private static string NewTfm(string tfm, ConvertTarget target)
    {
        // NetForms targets net10.0: anything older moves up; -windows goes for Linux.
        return target == ConvertTarget.Windows ? "net10.0-windows" : "net10.0";
    }

    private static string Reference(string? frameworkPath, string projectDir) => frameworkPath == null
        ? $"<PackageReference Include=\"NetForms\" Version=\"{PackageVersion}\" />"
        : $"<ProjectReference Include=\"{Path.GetRelativePath(projectDir, Path.Combine(frameworkPath, "src", "NetForms", "NetForms.csproj")).Replace('\\', '/')}\" />";

    private static XElement ReferenceElement(string? frameworkPath, string projectDir) => XElement.Parse(Reference(frameworkPath, projectDir));

    private static void PlanActions(XElement root, ConvertReport report, ConvertTarget target, string? frameworkPath, string projectDir)
    {
        report.Actions.Add("Remove <UseWindowsForms>true</UseWindowsForms>.");
        report.Actions.Add($"Reference NetForms: {Reference(frameworkPath, projectDir)}");
        foreach (var tfm in report.TargetFrameworks.Distinct())
        {
            var to = NewTfm(tfm, target);
            if (tfm != to) report.Actions.Add($"Target framework {tfm} → {to}{(target == ConvertTarget.Cross ? " (so it builds and runs on Linux too)" : "")}.");
        }
        if (Properties(root, "ImplicitUsings").Any(v => v.Trim() is "enable" or "true"))
            report.Actions.Add("Add <Using Include=\"System.Drawing\" /> and <Using Include=\"System.Windows.Forms\" /> (the implicit usings UseWindowsForms gave).");
        if (frameworkPath != null)
            report.Actions.Add("Set <GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources> (the .resx images; the NetForms package sets it itself).");
    }

    private static void Rewrite(XDocument doc, ConvertTarget target, string? frameworkPath, string projectDir, IEnumerable<string> packages)
    {
        var root = doc.Root!;
        var ns = root.Name.Namespace;
        foreach (var e in root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").Elements().Where(e => e.Name.LocalName == "UseWindowsForms").ToList())
        {
            // A project reference brings no props: say what UseWindowsForms said about .resx resources.
            if (frameworkPath != null) e.AddBeforeSelf(new XElement(ns + "GenerateResourceUsePreserializedResources", "true"), new XText("\n    "));
            RemoveWithWhitespace(e);
        }
        foreach (var e in root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").Elements().Where(e => e.Name.LocalName is "TargetFramework"))
            e.Value = NewTfm(e.Value, target);
        foreach (var e in root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").Elements().Where(e => e.Name.LocalName is "TargetFrameworks"))
            e.Value = string.Join(";", e.Value.Split(';').Select(v => v.Trim()).Where(v => v.Length > 0).Select(v => NewTfm(v, target)).Distinct());

        bool implicitUsings = root.Elements().Where(e => e.Name.LocalName == "PropertyGroup").Elements()
            .Any(e => e.Name.LocalName == "ImplicitUsings" && e.Value.Trim() is "enable" or "true");
        var group = new XElement(ns + "ItemGroup");
        group.Add(new XText("\n    "));
        group.Add(XElement.Parse(Reference(frameworkPath, projectDir)).WithNamespace(ns));
        foreach (var package in packages)
        {
            group.Add(new XText("\n    "));
            group.Add(new XElement(ns + "PackageReference", new XAttribute("Include", package.Split(' ')[0]), new XAttribute("Version", package.Split(' ')[1])));
        }
        if (implicitUsings)
        {
            group.Add(new XText("\n    "));
            group.Add(new XComment(" What <UseWindowsForms>true</UseWindowsForms> added: the WinForms namespaces as implicit usings. "));
            foreach (var u in new[] { "System.Drawing", "System.Windows.Forms" })
            {
                group.Add(new XText("\n    "));
                group.Add(new XElement(ns + "Using", new XAttribute("Include", u)));
            }
        }
        group.Add(new XText("\n  "));
        var last = root.Elements().LastOrDefault();
        if (last != null) last.AddAfterSelf(new XText("\n\n  "), group);
        else root.Add(new XText("\n  "), group, new XText("\n"));
    }

    private static void RemoveWithWhitespace(XElement e)
    {
        if (e.PreviousNode is XText t && string.IsNullOrWhiteSpace(t.Value)) t.Remove();
        e.Remove();
    }

    // --- the sources ------------------------------------------------------------------------------------

    /// <summary>A project's C# files as its build sees them, its implicit usings, and the projects it references.</summary>
    private sealed record ProjectSources(List<string> Files, bool ImplicitUsings, List<string> ProjectReferences);

    private static ProjectSources ReadSources(string projectPath)
    {
        var root = XDocument.Load(projectPath).Root!;
        var dir = Path.GetDirectoryName(projectPath)!;
        var references = References(root, "ProjectReference").Where(r => r.Length > 0)
            .Select(r => Path.GetFullPath(Path.Combine(dir, r.Replace('\\', '/')))).Where(File.Exists).Distinct().ToList();
        if (root.Attribute("Sdk") == null && !root.Elements().Any(e => e.Name.LocalName == "Sdk"))
            return new ProjectSources(new LegacyProject(projectPath, root).SourceFiles, false, references); // the listed files
        var files = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories).Where(f => !IsBuildOutput(dir, f)).ToList();
        return new ProjectSources(files, Properties(root, "ImplicitUsings").Any(v => v.Trim() is "enable" or "true"), references);
    }

    private static readonly CSharpCompilationOptions s_libraryOptions =
        new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable, allowUnsafe: true);

    private static List<SyntaxTree> Parse(ProjectSources sources)
    {
        var trees = new List<SyntaxTree>();
        if (sources.ImplicitUsings)
            trees.Add(CSharpSyntaxTree.ParseText(string.Join("\n", s_implicitUsings.Select(u => $"global using {u};")), path: "(implicit usings)"));
        foreach (var f in sources.Files) trees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(f), new CSharpParseOptions(LanguageVersion.Latest), path: f));
        return trees;
    }

    /// <summary>
    /// The referenced projects, compiled from their sources (recursively; a cycle stops where it closes): a
    /// solution's WinForms project uses its libraries' types, and without them every use is a false error.
    /// </summary>
    private static List<MetadataReference> CompileReferences(IEnumerable<string> projects, Dictionary<string, MetadataReference?> done)
    {
        var result = new List<MetadataReference>();
        foreach (var project in projects)
        {
            if (!done.TryGetValue(project, out var reference))
            {
                done[project] = null;
                var sources = ReadSources(project);
                var compilation = CSharpCompilation.Create(Path.GetFileNameWithoutExtension(project), Parse(sources),
                    References().Concat(CompileReferences(sources.ProjectReferences, done)), s_libraryOptions);
                done[project] = reference = compilation.ToMetadataReference();
            }
            if (reference != null) result.Add(reference);
        }
        return result;
    }

    private static void AnalyzeSources(string projectPath, ProjectSources sources, ConvertReport report, ConvertTarget target)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var files = sources.Files;
        bool implicitUsings = sources.ImplicitUsings;
        var trees = Parse(sources);
        var done = new Dictionary<string, MetadataReference?>(StringComparer.Ordinal) { [projectPath] = null };

        // Compiled as a library: whether the sources compile is the question, not whether Main is there.
        var compilation = CSharpCompilation.Create("NetFormsConvertCheck", trees, References().Concat(CompileReferences(sources.ProjectReferences, done)), s_libraryOptions);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => !SourceFixes.TryMend(d, report)).ToList();
        report.Compiled = errors.Count == 0;
        foreach (var d in errors.Take(2000))
        {
            var pos = d.Location.GetLineSpan();
            report.Issues.Add(new ConvertIssue("error", Categorize(d), d.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
                pos.Path, pos.StartLinePosition.Line + 1, pos.StartLinePosition.Character + 1));
        }

        foreach (var tree in trees.Skip(implicitUsings ? 1 : 0)) ScanForWindowsOnly(tree, report, target);

        foreach (var resx in Directory.EnumerateFiles(projectDir, "*.resx", SearchOption.AllDirectories).Where(f => !IsBuildOutput(projectDir, f)))
        {
            // Byte arrays and strings carry over (images and icons included, decision 107), and so do
            // BinaryFormatter records of NetForms' own serializable types - an ImageList's ImageStream:
            // System.Resources.Extensions rebuilds them without BinaryFormatter (decision 123). Other
            // records (custom serializable objects) are named.
            List<string> binary;
            try
            {
                var document = XDocument.Load(resx, LoadOptions.SetLineInfo);
                SourceFixes.CheckResxFileRefs(resx, document, report);
                binary = document.Root!.Elements("data")
                    .Where(d => ((string?)d.Attribute("mimetype"))?.Contains("binary.base64", StringComparison.Ordinal) == true
                             || ((string?)d.Attribute("mimetype"))?.Contains("soap.base64", StringComparison.Ordinal) == true)
                    .Where(d => !IsReadableBinaryRecord(d))
                    .Select(d => (string)d.Attribute("name")!).ToList();
            }
            catch (System.Xml.XmlException ex)
            {
                report.Issues.Add(new ConvertIssue("warning", "Resources", $"The .resx is not valid XML: {ex.Message}", resx));
                continue;
            }
            if (binary.Count > 0)
                report.Issues.Add(new ConvertIssue("warning", "Resources",
                    $"BinaryFormatter-serialized resources NetForms cannot read: {string.Join(", ", binary)}. Set them in code or re-add them as images.", resx));
        }

        foreach (var file in report.Fixes.GroupBy(f => f.File))
            report.Actions.Add($"Mend {Path.GetFileName(file.Key)}: {string.Join("; ", file.Select(f => f.Description))}");

        var reader = new DesignerCodeReader();
        foreach (var designer in files.Where(f => f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)))
        {
            if (!File.ReadAllText(designer).Contains("InitializeComponent", StringComparison.Ordinal)) continue;
            try
            {
                reader.ReadFile(designer);
                report.Forms.Add(new ConvertForm(designer, true, null));
            }
            catch (DesignerCodeException ex)
            {
                report.Forms.Add(new ConvertForm(designer, false, $"line {ex.Line}: {ex.Reason}"));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A form the reader cannot take (its .resx, a type it cannot build) is one finding, not the end of the run.
                report.Forms.Add(new ConvertForm(designer, false, $"{ex.GetType().Name}: {ex.Message}"));
            }
        }
    }

    private static bool IsBuildOutput(string projectDir, string file)
    {
        var rel = Path.GetRelativePath(projectDir, file).Replace('\\', '/');
        return rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) || rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) || rel.Contains("/bin/") || rel.Contains("/obj/");
    }

    /// <summary>The runtime's own assemblies and NetForms (the copy this tool runs with).</summary>
    private static IEnumerable<MetadataReference> References()
    {
        var tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator);
        var netforms = new[] { typeof(System.Windows.Forms.Control).Assembly.Location, typeof(System.Drawing.Graphics).Assembly.Location };
        return tpa.Concat(netforms).Where(p => p.Length > 0 && File.Exists(p)).Distinct(StringComparer.OrdinalIgnoreCase).Select(p => MetadataReference.CreateFromFile(p));
    }

    /// <summary>
    /// The public types of the real System.Windows.Forms and System.Drawing, by full and by simple name
    /// (winforms-types.txt, dumped from the real assemblies by tests/NetForms.Compat --types).
    /// </summary>
    private static readonly Lazy<HashSet<string>> s_winformsTypes = new(() =>
    {
        using var stream = typeof(ProjectConverter).Assembly.GetManifestResourceStream("NetForms.Converter.winforms-types.txt")
            ?? throw new InvalidOperationException("winforms-types.txt is not embedded.");
        using var reader = new StreamReader(stream);
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (string? line; (line = reader.ReadLine()) != null;)
        {
            if (line.Length == 0) continue;
            names.Add(line);
            names.Add(line.Substring(line.LastIndexOf('.') + 1));
        }
        return names;
    });

    /// <summary>
    /// What an error means for the move: a WinForms type or member that NetForms does not have (yet),
    /// told apart by the names the message quotes - is the missing type, or the type missing a member,
    /// one of WinForms' own?
    /// </summary>
    private static string Categorize(Diagnostic d)
    {
        var text = d.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
        var quoted = Regex.Matches(text, "'([^']+)'").Select(m => m.Groups[1].Value).ToList();
        bool winforms = quoted.Any(q => s_winformsTypes.Value.Contains(q) || s_winformsTypes.Value.Contains(q.Split('(')[0].Split('.').Last()))
            || text.Contains("System.Windows.Forms", StringComparison.Ordinal) || text.Contains("System.Drawing", StringComparison.Ordinal);
        return d.Id switch
        {
            "CS0246" or "CS0234" => winforms ? "Missing in NetForms (type)" : "Unresolved type",
            "CS0117" or "CS1061" => winforms ? "Missing in NetForms (member)" : "Missing member",
            "CS1501" or "CS1503" or "CS7036" => winforms ? "Different signature in NetForms" : "Compile error",
            _ => "Compile error",
        };
    }

    private static void ScanForWindowsOnly(SyntaxTree tree, ConvertReport report, ConvertTarget target)
    {
        var root = tree.GetRoot();
        void Add(string severity, string category, string message, SyntaxNode at)
        {
            var pos = at.GetLocation().GetLineSpan();
            report.Issues.Add(new ConvertIssue(severity, category, message, tree.FilePath, pos.StartLinePosition.Line + 1, pos.StartLinePosition.Character + 1));
        }
        string crossSeverity = target == ConvertTarget.Cross ? "warning" : "info";

        foreach (var attr in root.DescendantNodes().OfType<AttributeSyntax>())
        {
            var name = attr.Name.ToString();
            if (name is not ("DllImport" or "DllImportAttribute" or "LibraryImport" or "LibraryImportAttribute")) continue;
            var dll = attr.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax lit ? lit.Token.ValueText : "";
            if (s_windowsDlls.IsMatch(dll))
                Add(crossSeverity, "Windows API", $"P/Invoke into {dll}: works only on Windows, and handles/messages of NetForms controls are not Win32 ones.", attr);
        }
        foreach (var m in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            if (m.Identifier.ValueText is "WndProc" or "DefWndProc" && m.Modifiers.Any(SyntaxKind.OverrideKeyword))
                Add("warning", "Win32 messages", "WndProc override: NetForms controls have no window procedure; this code is never called.", m);
        foreach (var p in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            if (p.Identifier.ValueText == "CreateParams" && p.Modifiers.Any(SyntaxKind.OverrideKeyword))
                Add("warning", "Win32 messages", "CreateParams override: there is no native window class to configure in NetForms.", p);
        // Paths written for Windows: "\\Picture\\closed.png", @"C:\Data". NetForms' own file APIs read them on Linux
        // too (decision 112); File.*, StreamReader and the rest of the BCL do not.
        foreach (var node in root.DescendantNodes().Where(n => n is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } or InterpolatedStringExpressionSyntax))
        {
            var text = node is LiteralExpressionSyntax lit ? lit.Token.ValueText
                : string.Concat(((InterpolatedStringExpressionSyntax)node).Contents.Select(c => c is InterpolatedStringTextSyntax t ? t.TextToken.ValueText : "x"));
            bool absolute = s_windowsAbsolutePath.IsMatch(text);
            if (!absolute && !s_windowsRelativePath.IsMatch(text)) continue;
            var call = node.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault()?.Parent?.Parent;
            var callee = call switch
            {
                InvocationExpressionSyntax i => i.Expression is MemberAccessExpressionSyntax m ? m.Name.Identifier.ValueText : i.Expression.ToString(),
                BaseObjectCreationExpressionSyntax o => o is ObjectCreationExpressionSyntax oc ? oc.Type.ToString().Split('.').Last() : "",
                _ => "",
            };
            if (absolute)
                Add(crossSeverity, "Windows path", $"An absolute Windows path (\"{text}\"): it does not exist on Linux.", node);
            else if (s_netFormsFileApis.Contains(callee))
                Add("info", "Windows path", $"A path with backslashes, opened by {callee}: NetForms reads it on Linux too.", node);
            else
                Add(crossSeverity, "Windows path", $"A path with backslashes (\"{text}\"): on Linux '\\' is not a separator for {(callee.Length > 0 ? callee : "the BCL")}; use Path.Combine or '/'.", node);
        }

        foreach (var id in root.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var name = id.Identifier.ValueText;
            if (name == "Registry" && id.Parent is MemberAccessExpressionSyntax)
                Add(crossSeverity, "Windows only", "Microsoft.Win32.Registry exists only on Windows.", id);
            else if (name == "AxHost")
                Add("error", "ActiveX", "ActiveX controls (AxHost) cannot be hosted by NetForms.", id);
        }
    }

    /// <summary>A BinaryFormatter record whose type is a [Serializable] type of NetForms, found by the name the record gives.</summary>
    private static bool IsReadableBinaryRecord(XElement data)
    {
        if (((string?)data.Attribute("mimetype"))?.Contains("binary.base64", StringComparison.Ordinal) != true) return false;
        try
        {
            var bytes = Convert.FromBase64String(((string?)data.Element("value"))?.Trim() ?? "");
            var name = System.Formats.Nrbf.NrbfDecoder.Decode(new MemoryStream(bytes)).TypeName.AssemblyQualifiedName;
            var type = Type.GetType(name, throwOnError: false);
            return type != null && type.IsSerializable && (type.Assembly == typeof(System.Windows.Forms.Control).Assembly || type.Assembly == typeof(System.Drawing.Bitmap).Assembly);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

internal static class XmlExtensions
{
    /// <summary>Puts an element parsed without a namespace into the project's (old-style projects use the MSBuild one).</summary>
    public static XElement WithNamespace(this XElement e, XNamespace ns)
    {
        foreach (var x in e.DescendantsAndSelf()) x.Name = ns + x.Name.LocalName;
        return e;
    }
}
