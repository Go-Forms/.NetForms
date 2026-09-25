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

    public static List<DesignerToolboxCategory> Categories()
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
        return result;
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
    public static Type? ResolveType(string name)
    {
        var assembly = typeof(Control).Assembly;
        // PrintDocument lives in System.Drawing (NetForms.Drawing), as in WinForms.
        var type = assembly.GetType(name) ?? assembly.GetType("System.Windows.Forms." + name) ?? typeof(System.Drawing.Graphics).Assembly.GetType(name);
        if (type == null || !type.IsPublic || type.IsAbstract || !typeof(IComponent).IsAssignableFrom(type)) return null;
        if (type.GetConstructor(Type.EmptyTypes) == null) return null;
        return type;
    }
}
