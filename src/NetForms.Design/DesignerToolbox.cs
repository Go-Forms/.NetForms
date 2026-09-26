using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// The toolbox, grouped the way Visual Studio groups it. Types NetForms does not have yet are simply
/// left out (the list is the WinForms one, so they appear as they are implemented); "All Windows Forms"
/// lists every designable control and component, as in VS.
/// </summary>
public static class DesignerToolbox
{
    private static readonly (string Category, string[] Types)[] s_groups =
    {
        ("Common Controls", new[]
        {
            "Button", "CheckBox", "CheckedListBox", "ComboBox", "DateTimePicker", "Label", "LinkLabel", "ListBox",
            "ListView", "MaskedTextBox", "MonthCalendar", "NotifyIcon", "NumericUpDown", "PictureBox", "ProgressBar",
            "RadioButton", "RichTextBox", "TextBox", "ToolTip", "TreeView",
        }),
        ("Containers", new[] { "FlowLayoutPanel", "GroupBox", "Panel", "SplitContainer", "TabControl", "TableLayoutPanel" }),
        ("Menus & Toolbars", new[] { "ContextMenuStrip", "MenuStrip", "StatusStrip", "ToolStrip", "ToolStripContainer" }),
        ("Data", new[] { "BindingSource", "DataGridView" }),
        ("Components", new[] { "ErrorProvider", "HelpProvider", "ImageList", "Timer" }),
        ("Printing", new[] { "PageSetupDialog", "PrintDialog", "System.Drawing.Printing.PrintDocument", "PrintPreviewControl", "PrintPreviewDialog" }),
        ("Dialogs", new[] { "ColorDialog", "FolderBrowserDialog", "FontDialog", "OpenFileDialog", "SaveFileDialog" }),
    };

    /// <summary>Designable, but only in "All Windows Forms" (as in VS).</summary>
    private static readonly string[] s_allOnly = { "DomainUpDown", "HScrollBar", "PropertyGrid", "TrackBar", "VScrollBar" };

    public static List<DesignerToolboxCategory> Categories() => Categories(null, null);

    /// <summary>
    /// One group per control library of the project (decision 157), then the groups of NetForms:
    /// what the scan of its assemblies found, narrowed to the namespaces and types the library entry names.
    /// An item whose type is not loaded (the workspace is not trusted, the build is older than the scan)
    /// comes with the reason instead of being left out.
    /// </summary>
    public static List<DesignerToolboxCategory> Categories(IEnumerable<DesignerToolboxLibrary>? libraries, DesignerLibraries? loaded)
    {
        var result = new List<DesignerToolboxCategory>();
        var all = new DesignerToolboxCategory { Name = "All Windows Forms" };
        foreach (var (category, names) in s_groups)
        {
            var group = new DesignerToolboxCategory { Name = category };
            foreach (var name in names)
            {
                var type = ResolveType(name);
                if (type != null) group.Items.Add(Item(type));
            }
            if (group.Items.Count > 0) result.Add(group);
            all.Items.AddRange(group.Items);
        }
        foreach (var name in s_allOnly)
        {
            var type = ResolveType(name);
            if (type != null) all.Items.Add(Item(type));
        }
        all.Items = all.Items.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        result.Insert(0, all);
        // The project's groups come first, as "<Project> Components" does in Visual Studio.
        result.InsertRange(0, (libraries ?? Enumerable.Empty<DesignerToolboxLibrary>()).Select(l => LibraryCategory(l, loaded ?? DesignerLibraries.None)));
        return result;
    }

    private static DesignerToolboxCategory LibraryCategory(DesignerToolboxLibrary library, DesignerLibraries loaded)
    {
        var group = new DesignerToolboxCategory { Name = library.Name, Library = true, Id = library.Id };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var report in ScanCached(library.Assemblies))
        {
            if (report.Verdict == "error") group.Errors.AddRange(report.Findings.Where(f => f.Severity == "error").Select(f => $"{report.Name}: {f.Message}"));
            foreach (var item in report.Items)
            {
                if (!library.Includes(item) || !seen.Add(item.Type)) continue;
                var type = loaded.FindType(item.Type);
                group.Items.Add(new DesignerToolboxItem
                {
                    Type = item.Type,
                    Name = item.Name,
                    Tray = item.Tray,
                    Icon = item.Icon,
                    Unavailable = type != null ? null : loaded.Assemblies.Count == 0
                        ? "The project's assemblies are not loaded (the workspace is not trusted, or the project is not built)."
                        : "Not in the loaded build of the project: build it again.",
                });
            }
        }
        group.Items = group.Items.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
        return group;
    }

    private static readonly Dictionary<string, (DateTime Stamp, ControlLibraryReport Report)> s_scans = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A scan per assembly file, again only when the file has changed (a toolbox refresh per keystroke is not a rescan).</summary>
    private static IEnumerable<ControlLibraryReport> ScanCached(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var full = System.IO.Path.GetFullPath(path);
            var stamp = System.IO.File.Exists(full) ? System.IO.File.GetLastWriteTimeUtc(full) : DateTime.MinValue;
            lock (s_scans)
            {
                if (!s_scans.TryGetValue(full, out var cached) || cached.Stamp != stamp)
                    s_scans[full] = cached = (stamp, ControlLibraryScanner.Scan(new[] { full })[0]);
                yield return cached.Report;
            }
        }
    }

    private static DesignerToolboxItem Item(Type type) => new()
    {
        Type = type.FullName!,
        Name = type.Name,
        // Components, drop-downs and forms (PrintPreviewDialog) go to the component tray, as in VS.
        Tray = !typeof(Control).IsAssignableFrom(type) || typeof(ToolStripDropDown).IsAssignableFrom(type) || typeof(Form).IsAssignableFrom(type),
    };

    /// <summary>
    /// A component type by full or short name - toolbox controls, and what goes inside them (TabPage,
    /// tool strip items, ColumnHeader, grid columns). Null for anything that is not a public, creatable
    /// component of NetForms.
    /// </summary>
    public static Type? ResolveType(string name) => ResolveType(name, null);

    /// <summary>As <see cref="ResolveType(string)"/>, and the components of the project's libraries.</summary>
    public static Type? ResolveType(string name, DesignerLibraries? libraries)
    {
        var own = ResolveOwn(name);
        if (own != null || libraries == null) return own;
        var type = libraries.FindType(name);
        if (type == null || !type.IsPublic && !type.IsNestedPublic || type.IsAbstract || type.IsGenericTypeDefinition || !typeof(IComponent).IsAssignableFrom(type)) return null;
        return type.GetConstructor(Type.EmptyTypes) == null ? null : type;
    }

    private static Type? ResolveOwn(string name)
    {
        var assembly = typeof(Control).Assembly;
        // PrintDocument lives in System.Drawing (NetForms.Drawing), as in WinForms.
        var type = assembly.GetType(name) ?? assembly.GetType("System.Windows.Forms." + name) ?? typeof(System.Drawing.Graphics).Assembly.GetType(name);
        if (type == null || !type.IsPublic || type.IsAbstract || !typeof(IComponent).IsAssignableFrom(type)) return null;
        if (type.GetConstructor(Type.EmptyTypes) == null) return null;
        return type;
    }
}

/// <summary>
/// A control library of the project as the client describes it (from <c>.vscode/netforms.json</c>): a toolbox
/// group over some assemblies, optionally narrowed to namespaces (a folder of the project) or to picked types.
/// </summary>
public sealed class DesignerToolboxLibrary
{
    /// <summary>The client's key for it (to remove it again).</summary>
    public string? Id { get; set; }

    /// <summary>The toolbox group.</summary>
    public string Name { get; set; } = "";

    /// <summary>The assemblies to scan (paths of the build output).</summary>
    public List<string> Assemblies { get; set; } = new();

    /// <summary>Only types in these namespaces (or below them); empty for all.</summary>
    public List<string>? Namespaces { get; set; }

    /// <summary>Not types in these namespaces (they have a group of their own).</summary>
    public List<string>? ExcludeNamespaces { get; set; }

    /// <summary>Only these types (full names), as ticked when the library was added; null for all.</summary>
    public List<string>? Types { get; set; }

    /// <summary>Never these types (the form being designed, types moved to another group).</summary>
    public List<string>? ExcludeTypes { get; set; }

    internal bool Includes(ControlLibraryItem item)
    {
        static bool under(string ns, string prefix) => ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal);
        if (Types != null && !Types.Contains(item.Type)) return false;
        if (ExcludeTypes != null && ExcludeTypes.Contains(item.Type)) return false;
        if (Namespaces is { Count: > 0 } && !Namespaces.Any(n => under(item.Namespace, n))) return false;
        if (ExcludeNamespaces != null && ExcludeNamespaces.Any(n => under(item.Namespace, n))) return false;
        return true;
    }
}
