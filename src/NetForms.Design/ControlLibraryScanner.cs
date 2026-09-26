using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NetForms.Design;

/// <summary>
/// Looks into a control library <b>without running any of its code</b> (<see cref="MetadataLoadContext"/>):
/// whether the designer can use it, and which controls and components it offers the toolbox
/// (decision 157, docs/designer-control-libraries.md, "Проверка перед добавлением").
/// </summary>
/// <remarks>
/// A toolbox item is what Visual Studio would offer: a public, non-abstract, non-generic component with a
/// public parameterless constructor, not marked <c>[ToolboxItem(false)]</c> or <c>[DesignTimeVisible(false)]</c>
/// (both inherited, as in the component model), and not a form. Its icon comes from <c>[ToolboxBitmap]</c>, or
/// from the <c>Namespace.Type.bmp</c> resource the attribute's default looks for.
/// </remarks>
public static class ControlLibraryScanner
{
    /// <summary>Scans assemblies (<c>.dll</c> files); each is judged on its own.</summary>
    public static List<ControlLibraryReport> Scan(IEnumerable<string> assemblyPaths)
    {
        ArgumentNullException.ThrowIfNull(assemblyPaths);
        return assemblyPaths.Select(p => ScanAssembly(Path.GetFullPath(p))).ToList();
    }

    /// <summary>
    /// Scans a NuGet package - a <c>.nupkg</c> file or an extracted package folder - before it is added: the
    /// <c>lib/</c> folder a net10.0 project would take, native assets only for Windows, and every assembly in it.
    /// </summary>
    public static ControlLibraryPackageReport ScanPackage(string package)
    {
        ArgumentNullException.ThrowIfNull(package);
        string root = Path.GetFullPath(package);
        string? temp = null;
        try
        {
            if (File.Exists(root))
            {
                temp = Path.Combine(Path.GetTempPath(), "netforms-package-" + Guid.NewGuid().ToString("N"));
                ZipFile.ExtractToDirectory(root, temp);
                root = temp;
            }
            if (!Directory.Exists(root)) throw new FileNotFoundException($"{package} does not exist.", package);

            var report = new ControlLibraryPackageReport { Path = Path.GetFullPath(package) };
            ReadNuspec(root, report);
            var lib = Path.Combine(root, "lib");
            var frameworks = Directory.Exists(lib) ? Directory.GetDirectories(lib).Select(Path.GetFileName).ToList() : new List<string?>();
            report.Frameworks = frameworks.Where(f => f != null).Select(f => f!).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            var best = BestFramework(report.Frameworks);
            report.Framework = best;

            var runtimes = Path.Combine(root, "runtimes");
            if (Directory.Exists(runtimes))
            {
                var nativeRids = Directory.GetDirectories(runtimes)
                    .Where(d => Directory.Exists(Path.Combine(d, "native")) && Directory.EnumerateFiles(Path.Combine(d, "native"), "*", SearchOption.AllDirectories).Any())
                    .Select(d => Path.GetFileName(d)!).ToList();
                if (nativeRids.Count > 0 && nativeRids.All(r => r.StartsWith("win", StringComparison.OrdinalIgnoreCase)))
                    report.Findings.Add(new ControlLibraryFinding("windowsOnly", "warning",
                        $"Its native libraries are for Windows only ({string.Join(", ", nativeRids)}): the controls will not work on Linux."));
            }

            if (best == null)
            {
                report.Findings.Add(new ControlLibraryFinding("noFramework", "error", report.Frameworks.Count == 0
                    ? "The package has no assemblies (no lib/ folder)."
                    : $"The package has no assemblies for .NET 10 or a compatible framework (it has {string.Join(", ", report.Frameworks)})."));
            }
            else
            {
                if (best.Contains("-windows", StringComparison.OrdinalIgnoreCase))
                    report.Findings.Add(new ControlLibraryFinding("windowsOnly", "warning",
                        $"Its assemblies are for Windows only ({best}): the project needs a Windows target framework to reference them, and they will not run on Linux."));
                foreach (var dll in Directory.EnumerateFiles(Path.Combine(lib, best), "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                {
                    var a = ScanAssembly(dll);
                    a.Path = Path.GetRelativePath(root, dll).Replace('\\', '/');
                    report.Assemblies.Add(a);
                }
            }
            report.Verdict = Worst(report.Findings.Select(f => f.Severity).Concat(report.Assemblies.Select(a => a.Verdict)));
            return report;
        }
        finally
        {
            if (temp != null) try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
        }
    }

    /// <summary>The package's id and version, from the .nuspec at its root.</summary>
    private static void ReadNuspec(string root, ControlLibraryPackageReport report)
    {
        var nuspec = Directory.EnumerateFiles(root, "*.nuspec").FirstOrDefault();
        if (nuspec == null) return;
        try
        {
            var metadata = System.Xml.Linq.XDocument.Load(nuspec).Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
            report.Id = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value.Trim();
            report.Version = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "version")?.Value.Trim();
        }
        catch (System.Xml.XmlException) { }
    }

    /// <summary>The lib/ folder a net10.0 project takes (NuGet's nearest framework, simplified to what can occur).</summary>
    public static string? BestFramework(IEnumerable<string> frameworks)
    {
        string? best = null;
        int bestRank = int.MinValue;
        foreach (var f in frameworks)
        {
            int rank = Rank(f.ToLowerInvariant());
            if (rank > bestRank && rank > int.MinValue) { best = f; bestRank = rank; }
        }
        return best;

        static int Rank(string f)
        {
            // net10.0 > net9.0 > … > net5.0 > netcoreapp3.1 > … > netstandard2.1 > netstandard2.0 > …
            // A -windows flavour ranks just below the plain one: usable only by a Windows project.
            bool windows = false;
            int dash = f.IndexOf('-');
            if (dash >= 0)
            {
                windows = f.Substring(dash + 1).StartsWith("windows", StringComparison.Ordinal);
                if (!windows) return int.MinValue; // -android, -ios, …
                f = f.Substring(0, dash);
            }
            int score;
            if (f.StartsWith("netstandard", StringComparison.Ordinal) && TryVersion(f.Substring(11), out var sv)) score = 1000 + sv;
            else if (f.StartsWith("netcoreapp", StringComparison.Ordinal) && TryVersion(f.Substring(10), out var cv) && cv <= 31) score = 2000 + cv;
            else if (f.StartsWith("net", StringComparison.Ordinal) && f.Contains('.') && TryVersion(f.Substring(3), out var nv) && nv >= 50 && nv <= 100) score = 3000 + nv;
            else return int.MinValue; // net48, a newer .NET than the host, portable-…
            return score * 2 - (windows ? 1 : 0);
        }

        static bool TryVersion(string s, out int v)
        {
            v = 0;
            var parts = s.Split('.');
            if (parts.Length == 0 || !int.TryParse(parts[0], out var major)) return false;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out var m) ? m : 0;
            v = major * 10 + Math.Min(minor, 9);
            return true;
        }
    }

    private static string Worst(IEnumerable<string> verdicts)
    {
        var list = verdicts.ToList();
        return list.Contains("error") ? "error" : list.Contains("warning") ? "warning" : "ok";
    }

    // --- one assembly --------------------------------------------------------------------------------

    private static ControlLibraryReport ScanAssembly(string path)
    {
        var report = new ControlLibraryReport { Path = path };
        if (!File.Exists(path))
        {
            report.Findings.Add(new ControlLibraryFinding("missing", "error", $"{path} does not exist."));
            report.Verdict = "error";
            return report;
        }
        try { report.Name = AssemblyName.GetAssemblyName(path).Name ?? Path.GetFileNameWithoutExtension(path); }
        catch (Exception)
        {
            report.Name = Path.GetFileNameWithoutExtension(path);
            report.Findings.Add(new ControlLibraryFinding("notManaged", "error", $"{Path.GetFileName(path)} is not a .NET assembly."));
            report.Verdict = "error";
            return report;
        }

        using var context = new MetadataLoadContext(new PathAssemblyResolver(ResolverPaths(path)), "System.Private.CoreLib");
        Assembly assembly;
        try { assembly = context.LoadFromAssemblyPath(path); }
        catch (Exception ex)
        {
            report.Findings.Add(new ControlLibraryFinding("notManaged", "error", $"{Path.GetFileName(path)} cannot be read: {ex.Message}"));
            report.Verdict = "error";
            return report;
        }

        Judge(assembly, report);
        if (report.Findings.All(f => f.Severity != "error")) FindItems(assembly, report);
        report.Verdict = Worst(report.Findings.Select(f => f.Severity));
        return report;
    }

    /// <summary>The assembly's folder, the host's assemblies (NetForms, the facade) and the runtime's.</summary>
    private static IEnumerable<string> ResolverPaths(string path)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        void add(string p)
        {
            if (File.Exists(p) && seen.Add(Path.GetFileName(p))) list.Add(p);
        }
        // The host's own come first: the library's copy of NetForms is judged by the designer's NetForms.
        foreach (var a in Serialization.DesignerCodeReader.DefaultAssemblies().Append(typeof(ControlLibraryScanner).Assembly))
            if (!string.IsNullOrEmpty(a.Location)) add(a.Location);
        foreach (var f in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")) add(f);
        foreach (var f in Directory.EnumerateFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")) add(f);
        foreach (var f in Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.dll")) add(f);
        add(path);
        if (!list.Contains(path)) list.Add(path);
        return list;
    }

    private static void Judge(Assembly assembly, ControlLibraryReport report)
    {
        var framework = assembly.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")?
            .ConstructorArguments.FirstOrDefault().Value as string;
        report.TargetFramework = framework;
        var references = assembly.GetReferencedAssemblies();
        if (framework != null)
        {
            var name = new FrameworkNameParts(framework);
            if (name.Identifier == ".NETFramework")
                report.Findings.Add(new ControlLibraryFinding("noFramework", "error",
                    $"It is built for the .NET Framework ({framework}); NetForms needs a library built for .NET (net10.0 or compatible, or .NET Standard)."));
            else if (name.Identifier == ".NETCoreApp" && name.Version > new Version(10, 0))
                report.Findings.Add(new ControlLibraryFinding("noFramework", "error", $"It is built for a newer .NET ({framework}) than the designer runs on (.NET 10)."));
        }
        else if (references.Any(r => r.Name == "mscorlib"))
        {
            report.Findings.Add(new ControlLibraryFinding("noFramework", "error",
                "It is built for the .NET Framework; NetForms needs a library built for .NET (net10.0 or compatible, or .NET Standard)."));
        }

        bool netForms = references.Any(r => r.Name == "NetForms");
        var winForms = references.FirstOrDefault(r => r.Name == "System.Windows.Forms");
        report.NetForms = netForms;
        if (winForms != null && !netForms)
        {
            report.Findings.Add(new ControlLibraryFinding("winForms", "warning",
                "It is built for the real System.Windows.Forms, not for NetForms. The designer can show its controls, but a project that uses it may not compile " +
                "(CS0012, the strong name of System.Windows.Forms - decision 136) and is unlikely to build on Linux."));
        }
    }

    private readonly struct FrameworkNameParts
    {
        public FrameworkNameParts(string text)
        {
            var parts = text.Split(',');
            Identifier = parts[0].Trim();
            Version = new Version(0, 0);
            foreach (var p in parts.Skip(1))
            {
                var kv = p.Split('=', 2);
                if (kv.Length == 2 && kv[0].Trim() == "Version" && Version.TryParse(kv[1].Trim().TrimStart('v'), out var v)) Version = v;
            }
        }

        public string Identifier { get; }
        public Version Version { get; }
    }

    private static void FindItems(Assembly assembly, ControlLibraryReport report)
    {
        Type[] types;
        try { types = assembly.GetExportedTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }
        catch (Exception ex)
        {
            report.Findings.Add(new ControlLibraryFinding("unreadable", "warning", $"Its types cannot be listed: {ex.Message}"));
            return;
        }
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            try
            {
                if (!type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition || !type.IsVisible) continue;
                if (!Implements(type, "System.ComponentModel.IComponent")) continue;
                if (type.GetConstructor(BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null) == null) continue;
                if (IsHidden(type)) continue;
                bool control = DerivesFrom(type, "System.Windows.Forms.Control");
                bool form = DerivesFrom(type, "System.Windows.Forms.Form");
                if (form && !HasOwnToolboxItem(type)) continue; // a form is designed, not dropped (PrintPreviewDialog opts in)
                report.Items.Add(new ControlLibraryItem
                {
                    Type = type.FullName!,
                    Name = type.Name,
                    Namespace = type.Namespace ?? "",
                    Tray = !control || form || DerivesFrom(type, "System.Windows.Forms.ToolStripDropDown"),
                    Icon = Icon(type),
                });
            }
            catch (FileNotFoundException ex)
            {
                missing.Add(ex.FileName ?? ex.Message);
            }
            catch (Exception) { }
        }
        if (missing.Count > 0)
            report.Findings.Add(new ControlLibraryFinding("missingDependency", "warning",
                $"Some types need assemblies that are not beside it: {string.Join(", ", missing.Take(5))}{(missing.Count > 5 ? ", …" : "")}."));
    }

    private static bool Implements(Type type, string fullName) =>
        type.GetInterfaces().Any(i => i.FullName == fullName);

    private static bool DerivesFrom(Type type, string fullName)
    {
        for (var t = type; t != null; t = t.BaseType)
            if (t.FullName == fullName) return true;
        return false;
    }

    /// <summary><c>[ToolboxItem(false)]</c> or <c>[DesignTimeVisible(false)]</c> on the type or a base (both are inherited).</summary>
    private static bool IsHidden(Type type)
    {
        bool? toolbox = null, visible = null;
        for (var t = type; t != null && (toolbox == null || visible == null); t = t.BaseType)
        {
            foreach (var a in t.GetCustomAttributesData())
            {
                var name = a.AttributeType.FullName;
                if (name == "System.ComponentModel.ToolboxItemAttribute" && toolbox == null)
                    toolbox = !(a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is false);
                else if (name == "System.ComponentModel.DesignTimeVisibleAttribute" && visible == null)
                    visible = !(a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is false);
            }
        }
        return toolbox == false || visible == false;
    }

    private static bool HasOwnToolboxItem(Type type) =>
        type.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "System.ComponentModel.ToolboxItemAttribute"
            && !(a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is false));

    // --- icons ---------------------------------------------------------------------------------------

    /// <summary>A 16×16 PNG (base64) from <c>[ToolboxBitmap]</c> or the type's <c>.bmp</c> resource; null when there is none.</summary>
    private static string? Icon(Type type)
    {
        Assembly? source = type.Assembly;
        var names = new List<string>();
        string? file = null;
        var attribute = type.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.FullName == "System.Drawing.ToolboxBitmapAttribute");
        if (attribute != null)
        {
            var args = attribute.ConstructorArguments;
            if (args.Count == 1 && args[0].Value is string path) file = path;
            else if (args.Count >= 1 && args[0].Value is Type t)
            {
                source = t.Assembly;
                if (args.Count == 2 && args[1].Value is string resource)
                {
                    names.Add(t.Namespace == null ? resource : t.Namespace + "." + resource);
                    names.Add(resource);
                }
                else names.AddRange(Defaults(t));
            }
        }
        names.AddRange(Defaults(type));

        if (file != null && File.Exists(file)) return Png(File.OpenRead(file));
        foreach (var name in names.Distinct())
        {
            Stream? stream;
            try { stream = source.GetManifestResourceStream(name) ?? FindResource(source, name); }
            catch (Exception) { stream = null; }
            if (stream != null)
            {
                var png = Png(stream);
                if (png != null) return png;
            }
        }
        return null;

        static IEnumerable<string> Defaults(Type t)
        {
            var baseName = t.FullName!.Replace('+', '.');
            yield return baseName + ".bmp";
            yield return baseName + ".png";
            yield return baseName + ".ico";
        }
    }

    private static Stream? FindResource(Assembly assembly, string name)
    {
        // Resource names are case-sensitive; bitmaps added by hand are often not (Gauge.BMP).
        var match = assembly.GetManifestResourceNames().FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return match == null ? null : assembly.GetManifestResourceStream(match);
    }

    private static string? Png(Stream stream)
    {
        try
        {
            using (stream)
            {
                var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                buffer.Position = 0;
                bool bmp = buffer.Length > 2 && buffer.GetBuffer()[0] == (byte)'B' && buffer.GetBuffer()[1] == (byte)'M';
                using var image = Image.FromStream(buffer);
                using var icon = new Bitmap(16, 16);
                using (var g = Graphics.FromImage(icon))
                {
                    g.Clear(Color.Transparent);
                    g.DrawImage(image, new Rectangle(0, 0, 16, 16));
                }
                // The classic toolbox bitmap is transparent where its bottom-left pixel's colour is.
                if (bmp) icon.MakeTransparent(icon.GetPixel(0, 15));
                using var png = new MemoryStream();
                icon.Save(png, ImageFormat.Png);
                return Convert.ToBase64String(png.ToArray());
            }
        }
        catch (Exception)
        {
            return null;
        }
    }
}

/// <summary>What the scan found in one assembly.</summary>
public sealed class ControlLibraryReport
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>From <c>[TargetFramework]</c> (<c>.NETCoreApp,Version=v10.0</c>), when the assembly says.</summary>
    public string? TargetFramework { get; set; }

    /// <summary>It references NetForms (built for it).</summary>
    public bool NetForms { get; set; }

    /// <summary><c>ok</c>, <c>warning</c> (can be added, with the findings shown) or <c>error</c> (not added).</summary>
    public string Verdict { get; set; } = "ok";

    public List<ControlLibraryFinding> Findings { get; set; } = new();
    public List<ControlLibraryItem> Items { get; set; } = new();
}

/// <summary>What the scan found in a NuGet package.</summary>
public sealed class ControlLibraryPackageReport
{
    public string Path { get; set; } = "";

    /// <summary>From the package's .nuspec.</summary>
    public string? Id { get; set; }
    public string? Version { get; set; }

    /// <summary>The <c>lib/</c> folders of the package.</summary>
    public List<string> Frameworks { get; set; } = new();

    /// <summary>The one a net10.0 project takes; null when none fits.</summary>
    public string? Framework { get; set; }

    public string Verdict { get; set; } = "ok";
    public List<ControlLibraryFinding> Findings { get; set; } = new();
    public List<ControlLibraryReport> Assemblies { get; set; } = new();
}

/// <summary>One thing to tell the user: <c>winForms</c>, <c>windowsOnly</c>, <c>noFramework</c>, … with <c>warning</c> or <c>error</c>.</summary>
public sealed record ControlLibraryFinding(string Code, string Severity, string Message);

/// <summary>A control or component the library offers the toolbox.</summary>
public sealed class ControlLibraryItem
{
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public bool Tray { get; set; }

    /// <summary>16×16 PNG, base64; null for the default icon.</summary>
    public string? Icon { get; set; }
}
