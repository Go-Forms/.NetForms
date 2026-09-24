using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetForms.Converter;

/// <summary>
/// An old-style (.NET Framework, non-SDK) project read into what the SDK-style one needs: the
/// properties that still mean something, the files it compiles and embeds, the references that are
/// not the framework's own (Ф6, decision 111). The SDK project globs its folder, so the new file
/// keeps the metadata of the listed items as <c>Update</c> and <c>Remove</c>s what the old project
/// did not list - the set of compiled files stays exactly the old one, while a form added later (the
/// templates, the designer) is picked up with no edit to the project.
/// </summary>
internal sealed class LegacyProject
{
    /// <summary>Properties carried over from the unconditional groups, in the order they are written.</summary>
    private static readonly string[] s_carried =
    {
        "OutputType", "RootNamespace", "AssemblyName", "ApplicationIcon", "ApplicationManifest", "StartupObject",
        "LangVersion", "AllowUnsafeBlocks", "SignAssembly", "AssemblyOriginatorKeyFile", "NoWarn", "TreatWarningsAsErrors",
    };

    /// <summary>Item metadata that still means something in an SDK project (SubType is VS's cache of "is it a form").</summary>
    private static readonly HashSet<string> s_metadata = new(StringComparer.OrdinalIgnoreCase)
    {
        "DependentUpon", "AutoGen", "DesignTime", "DesignTimeSharedInput", "Generator", "LastGenOutput",
        "CopyToOutputDirectory", "Link", "LogicalName", "CustomToolNamespace",
    };

    /// <summary>The standard imports every old C# project has; anything else is a custom build step.</summary>
    private static readonly Regex s_standardImport = new(@"Microsoft\.(Common\.props|CSharp\.targets)$|Microsoft\.CSharp\.targets|Microsoft\.Common\.props", RegexOptions.IgnoreCase);

    public string Path { get; }
    public string Directory { get; }
    public XElement Root { get; }
    public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Absolute paths of the C# files the old project compiles.</summary>
    public List<string> SourceFiles { get; } = new();

    /// <summary>What the analysis says about the move, before and independent of the compilation.</summary>
    public List<ConvertIssue> Issues { get; } = new();

    /// <summary>The planned changes, one line each, for the report.</summary>
    public List<string> Actions { get; } = new();

    private readonly List<Item> _items = new();
    private readonly List<string> _outputPaths = new();
    private readonly List<(string Id, string Version)> _packages = new();
    private readonly List<(string Include, string? HintPath)> _references = new();
    private readonly List<string> _projectReferences = new();

    private sealed record Item(string Kind, string Include, string FullPath, Dictionary<string, string> Metadata);

    public LegacyProject(string path, XElement root)
    {
        Path = path;
        Directory = System.IO.Path.GetDirectoryName(path)!;
        Root = root;
        Read();
    }

    public bool UsesWindowsForms => _references.Any(r => AssemblyName(r.Include).Equals("System.Windows.Forms", StringComparison.OrdinalIgnoreCase));

    private static string AssemblyName(string include) => include.Split(',')[0].Trim();

    private void Read()
    {
        foreach (var group in Root.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
        {
            bool conditional = group.Attribute("Condition") != null;
            foreach (var p in group.Elements())
            {
                if (p.Name.LocalName == "OutputPath") _outputPaths.Add(Normalize(p.Value));
                // Configuration groups set these per configuration; one set anywhere counts.
                if (conditional && p.Name.LocalName is "AllowUnsafeBlocks" && p.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
                    Properties["AllowUnsafeBlocks"] = "true";
                if (conditional || p.Attribute("Condition") != null) continue;
                Properties[p.Name.LocalName] = p.Value.Trim();
            }
        }
        if (_outputPaths.Count == 0) _outputPaths.AddRange(new[] { "bin/Debug/", "bin/Release/" });

        foreach (var e in Root.Elements())
        {
            if (e.Name.LocalName == "Import")
            {
                var project = (string?)e.Attribute("Project") ?? "";
                if (!s_standardImport.IsMatch(project))
                    Issues.Add(new ConvertIssue("warning", "Project format", $"A custom import ({project}) is not carried over; add it back by hand if the build needs it.", Path));
            }
            if (e.Name.LocalName != "ItemGroup") continue;
            foreach (var i in e.Elements())
            {
                var include = (string?)i.Attribute("Include");
                if (include == null) continue;
                var metadata = i.Elements().Where(m => s_metadata.Contains(m.Name.LocalName)).ToDictionary(m => m.Name.LocalName, m => m.Value, StringComparer.OrdinalIgnoreCase);
                switch (i.Name.LocalName)
                {
                    case "Reference":
                        _references.Add((include, i.Elements().FirstOrDefault(m => m.Name.LocalName == "HintPath")?.Value));
                        break;
                    case "ProjectReference":
                        _projectReferences.Add(include);
                        break;
                    case "COMReference":
                        Issues.Add(new ConvertIssue("error", "COM", $"COM reference {include}: COM interop exists only on Windows and is not carried over.", Path));
                        break;
                    case "Compile" or "EmbeddedResource" or "None" or "Content":
                        if (include.Contains('*'))
                            foreach (var f in Glob(include)) _items.Add(new Item(i.Name.LocalName, Relative(f), f, metadata));
                        else
                        {
                            // Windows forgives case (Form1.designer.cs for Form1.Designer.cs); the SDK project names the file as it is.
                            var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory, Normalize(include)));
                            if (!File.Exists(full) && CasePath.Resolve(full) is { } actual && File.Exists(actual))
                            {
                                if (Inside(actual)) include = Relative(actual);
                                full = actual;
                            }
                            _items.Add(new Item(i.Name.LocalName, include, full, metadata));
                        }
                        break;
                }
            }
        }

        var packagesConfig = System.IO.Path.Combine(Directory, "packages.config");
        if (File.Exists(packagesConfig))
        {
            foreach (var p in XDocument.Load(packagesConfig).Root!.Elements("package"))
                _packages.Add(((string)p.Attribute("id")!, (string)p.Attribute("version")!));
            if (_packages.Count > 0)
                Issues.Add(new ConvertIssue("info", "Packages", $"packages.config becomes PackageReference ({string.Join(", ", _packages.Select(p => p.Id))}); a package built only for .NET Framework may not restore for .NET 10.", packagesConfig));
        }

        // A reference into NuGet's old packages folder (..\packages\Id.1.2.3\lib\...) is a package, even
        // without a packages.config beside the project (a solution-level one, or none committed).
        foreach (var (include, hint) in _references)
        {
            if (hint == null || s_packagesFolder.Match(Normalize(hint)) is not { Success: true } m) continue;
            var id = m.Groups["id"].Value;
            if (_packages.Any(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))) continue;
            _packages.Add((id, m.Groups["version"].Value));
        }

        SourceFiles.AddRange(_items.Where(i => i.Kind == "Compile" && i.FullPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Select(i => i.FullPath).Distinct());
        foreach (var missing in SourceFiles.Where(f => !File.Exists(f)))
            Issues.Add(new ConvertIssue("warning", "Project format", $"The project lists {Relative(missing)}, which does not exist.", Path));
        SourceFiles.RemoveAll(f => !File.Exists(f));
    }

    private static readonly Regex s_packagesFolder = new(@"(^|/)packages/(?<id>[A-Za-z_][\w.-]*?)\.(?<version>\d+(\.\d+){1,3}(-[\w.-]+)?)/", RegexOptions.IgnoreCase);

    private static string Normalize(string path) => path.Trim().Replace('\\', '/');

    private string Relative(string full) => System.IO.Path.GetRelativePath(Directory, full).Replace('/', '\\');

    private bool Inside(string full) => !System.IO.Path.GetRelativePath(Directory, full).StartsWith("..", StringComparison.Ordinal);

    /// <summary>Is the file a build product or under one (the old OutputPath, bin/, obj/)?</summary>
    private bool InOutput(string full)
    {
        var rel = System.IO.Path.GetRelativePath(Directory, full).Replace('\\', '/');
        return rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) || rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase)
            || _outputPaths.Any(o => o.Length > 0 && !o.StartsWith("..", StringComparison.Ordinal) && rel.StartsWith(o.TrimStart('/'), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>An MSBuild wildcard include, relative to the project (*, ?, and ** for any depth).</summary>
    private IEnumerable<string> Glob(string include)
    {
        var pattern = Normalize(include);
        var regex = new Regex("^" + Regex.Escape(pattern).Replace(@"\*\*/", "(.*/)?").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]") + "$", RegexOptions.IgnoreCase);
        return System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories)
            .Where(f => regex.IsMatch(System.IO.Path.GetRelativePath(Directory, f).Replace('\\', '/')) && !InOutput(f));
    }

    // --- the SDK-style project ----------------------------------------------------------------------------

    /// <summary>
    /// The SDK-style project that builds what the old one built, against NetForms. <paramref name="reference"/>
    /// is the NetForms reference element, <paramref name="tfm"/> the new target framework.
    /// </summary>
    public XDocument ToSdkProject(string tfm, XElement? reference, bool preserializedResources, IEnumerable<string>? packages = null)
    {
        Actions.Clear();
        var project = new XElement("Project", new XAttribute("Sdk", "Microsoft.NET.Sdk"));

        var props = new XElement("PropertyGroup");
        props.Add(new XElement("OutputType", Properties.GetValueOrDefault("OutputType", "Library")));
        props.Add(new XElement("TargetFramework", tfm));
        foreach (var name in s_carried.Skip(1))
            if (Properties.TryGetValue(name, out var value) && value.Length > 0 && CarriedValue(name, value) is { } carried) props.Add(new XElement(name, carried));
        if (SourceFiles.Any(HasAssemblyAttributes))
        {
            props.Add(new XComment(" Properties\\AssemblyInfo.cs has the attributes the SDK would generate. "));
            props.Add(new XElement("GenerateAssemblyInfo", "false"));
            Actions.Add("Keep AssemblyInfo.cs: <GenerateAssemblyInfo>false</GenerateAssemblyInfo>.");
        }
        if (SourceFiles.Any(f => Regex.IsMatch(File.ReadAllText(f), @"^\s*\[\s*assembly\s*:\s*(System\.Reflection\.)?Assembly(File)?Version\s*\(\s*""[^""]*\*", RegexOptions.Multiline)))
        {
            props.Add(new XComment(" AssemblyVersion(\"1.0.*\"): a wildcard version needs a non-deterministic build. "));
            props.Add(new XElement("Deterministic", "false"));
        }
        if (preserializedResources) props.Add(new XElement("GenerateResourceUsePreserializedResources", "true"));
        project.Add(props);

        var refs = new XElement("ItemGroup");
        if (reference != null) refs.Add(reference);
        foreach (var p in _projectReferences) refs.Add(new XElement("ProjectReference", new XAttribute("Include", p)));
        foreach (var (id, version) in _packages) refs.Add(new XElement("PackageReference", new XAttribute("Include", id), new XAttribute("Version", version)));
        foreach (var package in packages ?? Enumerable.Empty<string>())
            if (!_packages.Any(p => p.Id.Equals(package.Split(' ')[0], StringComparison.OrdinalIgnoreCase)))
                refs.Add(new XElement("PackageReference", new XAttribute("Include", package.Split(' ')[0]), new XAttribute("Version", package.Split(' ')[1])));
        foreach (var (include, hint) in _references)
        {
            if (hint == null) continue; // the framework's own: .NET has it, or the compilation says what is gone
            if (_packages.Any(p => Normalize(hint).Contains($"packages/{p.Id}.{p.Version}/", StringComparison.OrdinalIgnoreCase))) continue;
            refs.Add(new XElement("Reference", new XAttribute("Include", AssemblyName(include)), new XElement("HintPath", hint)));
            Actions.Add($"Keep the reference to {hint}.");
        }
        if (refs.HasElements) project.Add(refs);

        var items = new XElement("ItemGroup");
        AddItems(items);
        if (items.HasElements) project.Add(items);

        Actions.Insert(0, $"Rewrite the project in the SDK style ({_items.Count(i => i.Kind == "Compile")} compiled files, {_items.Count(i => i.Kind == "EmbeddedResource")} resources; the framework references are implicit in .NET).");
        if (_projectReferences.Count > 0)
            Issues.Add(new ConvertIssue("info", "Project references", $"Referenced projects ({string.Join(", ", _projectReferences.Select(System.IO.Path.GetFileName))}) are converted separately.", Path));
        return new XDocument(project);
    }

    /// <summary>
    /// A property's value in the new project, or null when it is dropped: a file property naming no file
    /// (the old build would have stopped on it too), a PFX signing key (.NET signs with .snk only).
    /// </summary>
    private string? CarriedValue(string name, string value)
    {
        if (name is "SignAssembly" && Properties.GetValueOrDefault("AssemblyOriginatorKeyFile", "").EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
            return null;
        if (name is not ("ApplicationIcon" or "ApplicationManifest" or "AssemblyOriginatorKeyFile")) return value;
        if (name == "AssemblyOriginatorKeyFile" && value.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase))
        {
            Issues.Add(new ConvertIssue("warning", "Signing", $"The assembly was signed with {value}, a PFX key .NET cannot sign with; it is left unsigned (extract an .snk to sign it again).", Path));
            return null;
        }
        var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory, Normalize(value)));
        var actual = File.Exists(full) ? full : CasePath.Resolve(full);
        if (actual == null || !File.Exists(actual))
        {
            Issues.Add(new ConvertIssue("warning", "Project format", $"<{name}> names {value}, which does not exist; it is left out.", Path));
            return null;
        }
        return Inside(actual) ? Relative(actual) : value;
    }

    private static bool HasAssemblyAttributes(string file) =>
        Regex.IsMatch(File.ReadAllText(file), @"^\s*\[\s*assembly\s*:\s*(System\.Reflection\.)?Assembly(Title|Version|FileVersion|Company|Product|Description|Configuration|Copyright|Trademark|Culture)\b", RegexOptions.Multiline);

    private void AddItems(XElement group)
    {
        XElement Element(string kind, string verb, string include, IEnumerable<KeyValuePair<string, string>> metadata)
        {
            var e = new XElement(kind, new XAttribute(verb, include));
            foreach (var (k, v) in metadata) e.Add(new XElement(k, v));
            return e;
        }

        var listed = new HashSet<string>(_items.Select(i => i.FullPath), StringComparer.OrdinalIgnoreCase);
        var nested = System.IO.Directory.EnumerateFiles(Directory, "*.*proj", SearchOption.AllDirectories)
            .Select(System.IO.Path.GetDirectoryName).Where(d => !string.Equals(d, Directory, StringComparison.OrdinalIgnoreCase) && !InOutput(d + "/x"))
            .Distinct().ToList();

        // Another project in a subfolder owns its files: the glob of this one must not take them.
        foreach (var d in nested)
        {
            var rel = Relative(d!) + "\\**";
            foreach (var kind in new[] { "Compile", "EmbeddedResource", "None" })
                group.Add(Element(kind, "Remove", rel, Array.Empty<KeyValuePair<string, string>>()));
        }
        bool InNested(string f) => nested.Any(d => f.StartsWith(d + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        // What the old project did not compile or embed, the new one does not either.
        var unlisted = System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".resx", StringComparison.OrdinalIgnoreCase))
                        && !InOutput(f) && !InNested(f) && !listed.Contains(f))
            .OrderBy(f => f, StringComparer.Ordinal).ToList();
        foreach (var f in unlisted)
            group.Add(Element(f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ? "Compile" : "EmbeddedResource", "Remove", Relative(f), Array.Empty<KeyValuePair<string, string>>()));
        if (unlisted.Count > 0) Actions.Add($"Exclude {unlisted.Count} file(s) the old project did not list ({string.Join(", ", unlisted.Take(5).Select(Relative))}{(unlisted.Count > 5 ? ", …" : "")}).");

        var copied = FilesTheOldOutputHad();
        foreach (var item in _items)
        {
            if (InOutput(item.FullPath)) continue; // bin\Debug\app.exe.config and the like: build products
            if (!File.Exists(item.FullPath))
            {
                if (item.Kind != "Compile") // a missing source is reported with the sources
                    Issues.Add(new ConvertIssue("warning", "Project format", $"The project lists {item.Include}, which does not exist; it is left out.", Path));
                continue;
            }
            var ext = System.IO.Path.GetExtension(item.FullPath);
            var metadata = item.Metadata.Where(m => !(m.Key.Equals("DependentUpon", StringComparison.OrdinalIgnoreCase) && item.Kind == "EmbeddedResource" && ext.Equals(".resx", StringComparison.OrdinalIgnoreCase))).ToList();
            bool globbed = Inside(item.FullPath) && item.Kind switch
            {
                "Compile" => ext.Equals(".cs", StringComparison.OrdinalIgnoreCase),
                "EmbeddedResource" => ext.Equals(".resx", StringComparison.OrdinalIgnoreCase),
                "None" => true,
                _ => false,
            };
            if (copied.Contains(item.FullPath) && !metadata.Any(m => m.Key.Equals("CopyToOutputDirectory", StringComparison.OrdinalIgnoreCase)))
                metadata.Add(new("CopyToOutputDirectory", "PreserveNewest"));
            if (!globbed) group.Add(Element(item.Kind, "Include", item.Include, metadata));
            else if (metadata.Count > 0) group.Add(Element(item.Kind, "Update", item.Include, metadata));
        }
        // A twin the old project did not list is still in the None glob: it only needs copying.
        foreach (var f in copied.Where(f => !listed.Contains(f)).OrderBy(f => f, StringComparer.Ordinal))
            group.Add(Element("None", "Update", Relative(f), new[] { new KeyValuePair<string, string>("CopyToOutputDirectory", "PreserveNewest") }));
        if (copied.Count > 0)
            Actions.Add($"Copy {copied.Count} file(s) to the output folder, as the old one had them there ({string.Join(", ", copied.Take(3).Select(Relative))}{(copied.Count > 3 ? ", …" : "")}).");
    }

    /// <summary>
    /// Files of the project that sat, committed, in the old output folder too (bin\Debug\Picture\bomb.png next
    /// to Picture\bomb.png): the program reads them from beside the executable, and the SDK's output folder
    /// (bin\Debug\net10.0) will not have them unless they are copied. Output files with no project twin are
    /// reported: they exist only in the old build.
    /// </summary>
    private HashSet<string> FilesTheOldOutputHad()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orphans = new List<string>();
        var assembly = Properties.GetValueOrDefault("AssemblyName", System.IO.Path.GetFileNameWithoutExtension(Path));
        foreach (var output in _outputPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Directory, output));
            if (!System.IO.Directory.Exists(dir) || !Inside(dir)) continue;
            foreach (var f in System.IO.Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var rel = System.IO.Path.GetRelativePath(dir, f);
                var name = System.IO.Path.GetFileName(f);
                if (name.StartsWith(assembly + ".", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".exe.config", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll.config", StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase)) continue; // build products, an older name's too
                var twin = System.IO.Path.Combine(Directory, rel);
                if (File.Exists(twin)) result.Add(System.IO.Path.GetFullPath(twin));
                else orphans.Add(System.IO.Path.Combine(output, rel));
            }
        }
        if (orphans.Count > 0)
            Issues.Add(new ConvertIssue("warning", "Output files", $"Files that exist only in the old output folder ({string.Join(", ", orphans.Distinct().Take(5))}{(orphans.Count > 5 ? ", …" : "")}): if the program reads them, move them into the project with CopyToOutputDirectory.", Path));
        return result;
    }
}
