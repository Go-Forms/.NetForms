using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using NetForms.Design.Serialization;

namespace NetForms.Design;

/// <summary>
/// The assemblies of the user's project - its own build output and the libraries it references (NuGet
/// packages, other projects, <c>.dll</c> files) - loaded for the designer (decision 157,
/// docs/designer-control-libraries.md).
/// </summary>
/// <remarks>
/// <para>
/// Nothing is resolved here that MSBuild has not resolved already: the input is the output of a successful
/// <c>dotnet build</c> (<c>bin/Debug/net10.0/App.dll</c> with its <c>App.deps.json</c>), and dependencies are
/// found by <see cref="AssemblyDependencyResolver"/> exactly as when the application runs.
/// </para>
/// <para>
/// The files are loaded from a <b>shadow copy</b>, so the next <c>dotnet build</c> is never stopped by a
/// locked <c>.dll</c> (Windows), into a <b>collectible</b> <see cref="AssemblyLoadContext"/>, so a rebuilt
/// project replaces the old types without restarting the host. What the host has itself - NetForms,
/// System.Drawing, the BCL, SkiaSharp, Avalonia - is always the host's: the project's copy of NetForms is
/// never loaded next to it, or its <c>Control</c> would not be the designer's <c>Control</c>.
/// </para>
/// </remarks>
public sealed class DesignerLibraries : IDisposable
{
    private static readonly object s_gate = new();
    private static HashSet<string>? s_hostAssemblies;
    private static HashSet<string>? s_hostNatives;
    private static int s_counter;

    private readonly LibraryLoadContext? _context;
    private readonly List<Assembly> _assemblies = new();
    private readonly List<string> _errors = new();
    private readonly Dictionary<string, string> _shadows = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _shadowRoot;
    private bool _disposed;

    /// <summary>No libraries: the designer's own types only.</summary>
    public static DesignerLibraries None { get; } = new();

    private DesignerLibraries() { }

    private DesignerLibraries(IReadOnlyList<string> mainAssemblies)
    {
        _shadowRoot = Path.Combine(ShadowBase, $"{Environment.ProcessId}-{System.Threading.Interlocked.Increment(ref s_counter)}");
        _context = new LibraryLoadContext();
        foreach (var main in mainAssemblies)
        {
            var full = Path.GetFullPath(main);
            if (!File.Exists(full))
            {
                _errors.Add($"{full} does not exist. Build the project first.");
                continue;
            }
            string shadow;
            try { shadow = ShadowCopy(full); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _errors.Add($"Cannot copy {Path.GetDirectoryName(full)}: {ex.Message}");
                continue;
            }
            _context.AddMain(shadow);
            foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(shadow)!, "*.dll"))
            {
                AssemblyName name;
                try { name = AssemblyName.GetAssemblyName(file); }
                catch (Exception) { continue; } // native, or not an assembly
                if (IsHostAssembly(name.Name!)) continue;
                try
                {
                    var assembly = _context.LoadFromAssemblyName(name);
                    if (!_assemblies.Contains(assembly)) _assemblies.Add(assembly);
                }
                catch (Exception ex)
                {
                    _errors.Add($"{Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Loads the build output of each project: <paramref name="mainAssemblies"/> are the projects' own
    /// assemblies (<c>bin/Debug/net10.0/App.dll</c>); every managed assembly beside them that the host does
    /// not have comes along (the referenced packages and projects).
    /// </summary>
    public static DesignerLibraries Load(IEnumerable<string> mainAssemblies)
    {
        ArgumentNullException.ThrowIfNull(mainAssemblies);
        var list = mainAssemblies.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return list.Count == 0 ? None : new DesignerLibraries(list);
    }

    /// <summary>The assemblies loaded for the project (not the host's own).</summary>
    public IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>What could not be loaded, for the user to read.</summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>The host's assemblies and the project's: what the designer file can name.</summary>
    public IEnumerable<Assembly> AllAssemblies() => DesignerCodeReader.DefaultAssemblies().Concat(_assemblies).Distinct();

    /// <summary>True when <paramref name="assembly"/> is one of the project's (not NetForms, not the BCL).</summary>
    public bool Contains(Assembly assembly) => _assemblies.Contains(assembly);

    /// <summary>Where the designer loaded <paramref name="originalPath"/> from, or null when it is not part of the copy.</summary>
    public string? ShadowOf(string originalPath) =>
        _shadows.TryGetValue(Path.GetFullPath(originalPath), out var shadow) ? shadow : null;

    /// <summary>A type of the project's assemblies by its full name (dots or <c>+</c> for nesting).</summary>
    public Type? FindType(string fullName)
    {
        foreach (var assembly in _assemblies)
        {
            try
            {
                var type = assembly.GetType(fullName) ?? assembly.GetType(NestedName(fullName));
                if (type != null) return type;
            }
            catch (Exception) { }
        }
        return null;
    }

    private static string NestedName(string dotted)
    {
        int dot = dotted.LastIndexOf('.');
        return dot < 0 ? dotted : dotted.Substring(0, dot) + "+" + dotted.Substring(dot + 1);
    }

    // --- the shadow copy ---------------------------------------------------------------------------

    /// <summary>Under the temp folder; stale copies of hosts that are gone are removed on the first load.</summary>
    public static string ShadowBase { get; } = Path.Combine(Path.GetTempPath(), "netforms-designer-shadow");

    private static bool s_cleaned;

    private static void CleanStaleCopies()
    {
        if (s_cleaned) return;
        s_cleaned = true;
        if (!Directory.Exists(ShadowBase)) return;
        foreach (var dir in Directory.EnumerateDirectories(ShadowBase))
        {
            var name = Path.GetFileName(dir);
            int dash = name.IndexOf('-');
            if (dash <= 0 || !int.TryParse(name.AsSpan(0, dash), out var pid) || pid == Environment.ProcessId) continue;
            bool alive;
            try { using var p = System.Diagnostics.Process.GetProcessById(pid); alive = !p.HasExited; }
            catch (Exception) { alive = false; }
            if (!alive) TryDelete(dir);
        }
    }

    /// <summary>
    /// Copies the output folder of <paramref name="mainAssembly"/>, leaving out what the host has itself
    /// (its managed assemblies and native libraries: a NetForms application's output carries SkiaSharp and
    /// Avalonia natives for every platform, hundreds of megabytes). Returns the copy of the main assembly.
    /// </summary>
    private string ShadowCopy(string mainAssembly)
    {
        lock (s_gate) CleanStaleCopies();
        var sourceDir = Path.GetDirectoryName(mainAssembly)!;
        var target = Path.Combine(_shadowRoot!, _shadows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var natives = HostNatives();
        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var fileName = Path.GetFileName(file);
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (ext is ".exe" or ".xml" or ".config" or ".vshost" || relative.StartsWith("publish" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            if (relative.StartsWith("runtimes" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && relative.Contains(Path.DirectorySeparatorChar + "native" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                if (natives.Contains(fileName)) continue;
            }
            else if (ext is ".dll" or ".pdb")
            {
                var simple = Path.GetFileNameWithoutExtension(fileName);
                if (simple.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) simple = simple.Substring(0, simple.Length - ".resources".Length);
                if (IsHostAssembly(simple)) continue;
            }
            else if (ext is not (".json" or ".so" or ".dylib" or ".bmp" or ".png" or ".ico" or ".resources"))
            {
                // Content files next to the application (data, images) are not the designer's business,
                // except for extensionless natives (libfoo.so.1).
                if (!fileName.Contains(".so.", StringComparison.Ordinal)) continue;
            }
            var to = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(to)!);
            File.Copy(file, to, overwrite: true);
            _shadows[file] = to;
        }
        var main = Path.Combine(target, Path.GetFileName(mainAssembly));
        _shadows[mainAssembly] = main;
        return main;
    }

    /// <summary>Simple names of the assemblies the host itself runs with (its trusted platform assemblies, and what is loaded).</summary>
    internal static bool IsHostAssembly(string simpleName)
    {
        lock (s_gate)
        {
            if (s_hostAssemblies == null)
            {
                s_hostAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
                    foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                        s_hostAssemblies.Add(Path.GetFileNameWithoutExtension(path));
                // The designer's own assemblies, however the host was started.
                foreach (var a in DesignerCodeReader.DefaultAssemblies().Append(typeof(DesignerLibraries).Assembly))
                    s_hostAssemblies.Add(a.GetName().Name!);
                s_hostAssemblies.Add("System.Windows.Forms"); // the facade: forwards to NetForms
                s_hostAssemblies.Add("System.Drawing.Common");
            }
            if (s_hostAssemblies.Contains(simpleName)) return true;
        }
        return AssemblyLoadContext.Default.Assemblies.Any(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string> HostNatives()
    {
        lock (s_gate)
        {
            if (s_hostNatives != null) return s_hostNatives;
            s_hostNatives = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var runtimes = Path.Combine(AppContext.BaseDirectory, "runtimes");
            if (Directory.Exists(runtimes))
                foreach (var file in Directory.EnumerateFiles(runtimes, "*", SearchOption.AllDirectories))
                    s_hostNatives.Add(Path.GetFileName(file));
            // A published host keeps its natives beside it (single RID).
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".so" or ".dylib" || (ext == ".dll" && !IsManaged(file))) s_hostNatives.Add(Path.GetFileName(file));
            }
            // What every NetForms application carries: the Skia and HarfBuzz natives, Avalonia's ANGLE.
            foreach (var n in new[] { "libSkiaSharp.so", "libSkiaSharp.dylib", "libSkiaSharp.dll", "libHarfBuzzSharp.so", "libHarfBuzzSharp.dylib", "libHarfBuzzSharp.dll", "av_libglesv2.dll", "libAvaloniaNative.dylib" })
                s_hostNatives.Add(n);
            return s_hostNatives;
        }
    }

    private static bool IsManaged(string file)
    {
        try { AssemblyName.GetAssemblyName(file); return true; }
        catch (Exception) { return false; }
    }

    // --- unloading ---------------------------------------------------------------------------------

    /// <summary>
    /// Unloads the project's assemblies. The component model's caches forget their types first
    /// (<see cref="TypeDescriptor.Refresh(Assembly)"/>), or they would keep the collectible context alive.
    /// The shadow copy is removed when the runtime lets go of the files.
    /// </summary>
    public void Dispose()
    {
        if (_disposed || _context == null) return;
        _disposed = true;
        foreach (var assembly in _assemblies)
        {
            try { TypeDescriptor.Refresh(assembly); } catch (Exception) { }
        }
        ForgetTypes(_assemblies);
        _assemblies.Clear();
        ClearReflectionCaches();
        _context.Unload();
        if (_shadowRoot != null) TryDelete(_shadowRoot);
    }

    /// <summary>
    /// The component model keeps the properties, events and attributes of every type it has described in
    /// static caches that <see cref="TypeDescriptor.Refresh(Assembly)"/> does not reach; hot reload clears them
    /// through the handler the runtime calls after a metadata update, and so does the designer.
    /// </summary>
    private static void ClearReflectionCaches()
    {
        try
        {
            typeof(TypeConverter).Assembly.GetType("System.ComponentModel.ReflectionCachesUpdateHandler")?
                .GetMethod("ClearCache", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?
                .Invoke(null, new object?[] { null });
        }
        catch (Exception) { /* a runtime without it: the context stays alive until the host restarts */ }
    }

    /// <summary>
    /// <see cref="TypeDescriptor"/> also remembers, per type, that it has set up its default provider and which
    /// provider node describes it (<c>s_defaultProviderInitialized</c>, <c>s_providerTypeTable</c>), with strong
    /// references that no public API removes; the entries of the unloaded types are taken out.
    /// </summary>
    private static void ForgetTypes(IReadOnlyCollection<Assembly> assemblies)
    {
        bool ours(object key) => key is Type t && assemblies.Contains(t.Assembly);
        void purge(object? table)
        {
            if (table is not System.Collections.IDictionary dictionary) return;
            // ConcurrentDictionary has no SyncRoot (it throws); a Hashtable is only safe to read under its lock.
            List<object> keys;
            if (dictionary is System.Collections.Hashtable) lock (dictionary.SyncRoot) keys = dictionary.Keys.Cast<object>().Where(ours).ToList();
            else keys = dictionary.Keys.Cast<object>().Where(ours).ToList();
            foreach (var key in keys) dictionary.Remove(key);
        }
        const BindingFlags statics = BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags instance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        try
        {
            purge(typeof(TypeDescriptor).GetField("s_defaultProviderInitialized", statics)?.GetValue(null));
            purge(typeof(TypeDescriptor).GetField("s_providerTypeTable", statics)?.GetValue(null));
            // The reflection provider's per-type data (ReflectedTypeData), which Refresh(Assembly) leaves
            // behind for components that are not controls.
            if (typeof(TypeDescriptor).GetField("s_providerTable", statics)?.GetValue(null) is System.Collections.IDictionary providers)
            {
                List<object?> nodes;
                lock (providers.SyncRoot) nodes = providers.Values.Cast<object?>().ToList(); // a WeakHashtable
                foreach (var first in nodes)
                {
                    for (var node = first; node != null; node = node.GetType().GetField("Next", instance)?.GetValue(node))
                    {
                        var provider = node.GetType().GetField("Provider", instance)?.GetValue(node);
                        if (provider?.GetType().Name == "ReflectTypeDescriptionProvider")
                            purge(provider.GetType().GetField("_typeData", instance)?.GetValue(provider));
                    }
                }
            }
        }
        catch (Exception) { /* another runtime's internals: the context may stay alive until the host restarts */ }
    }

    /// <summary>For tests: the context, to see it collected after <see cref="Dispose"/>.</summary>
    internal WeakReference ContextReference() => new(_context);

    private static void TryDelete(string dir)
    {
        try { Directory.Delete(dir, recursive: true); }
        catch (Exception) { /* locked until the context is collected (Windows); the next host removes it */ }
    }

    private sealed class LibraryLoadContext : AssemblyLoadContext
    {
        private readonly List<(string Dir, AssemblyDependencyResolver? Resolver)> _mains = new();

        public LibraryLoadContext() : base("NetForms designer: project", isCollectible: true) { }

        public void AddMain(string mainPath)
        {
            AssemblyDependencyResolver? resolver;
            try { resolver = new AssemblyDependencyResolver(mainPath); }
            catch (Exception) { resolver = null; }
            _mains.Add((Path.GetDirectoryName(mainPath)!, resolver));
        }

        protected override Assembly? Load(AssemblyName name)
        {
            // The host's own: the default context gives it, whatever version the project was built against.
            if (IsHostAssembly(name.Name!))
            {
                var loaded = Default.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
                if (loaded != null) return loaded;
                try { return Default.LoadFromAssemblyName(new AssemblyName(name.Name!)); }
                catch (Exception) { return null; }
            }
            foreach (var (dir, resolver) in _mains)
            {
                var path = resolver?.ResolveAssemblyToPath(name);
                if (path == null)
                {
                    var probe = Path.Combine(dir, name.Name + ".dll");
                    if (File.Exists(probe)) path = probe;
                }
                if (path != null) return LoadFromAssemblyPath(path);
            }
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            foreach (var (_, resolver) in _mains)
            {
                var path = resolver?.ResolveUnmanagedDllToPath(unmanagedDllName);
                if (path != null) return LoadUnmanagedDllFromPath(path);
            }
            return IntPtr.Zero;
        }
    }
}
