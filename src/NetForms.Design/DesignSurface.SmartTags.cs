using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace NetForms.Design;

/// <summary>
/// Smart tags (decision 186): the "Tasks" panel Visual Studio opens from the arrow at a control's corner - its most
/// used properties (a TextBox's MultiLine, a grid's data source and Enable Adding…), its editors (Edit Items…,
/// Edit Columns…) and its verbs (Dock in Parent Container, Add Tab, Add Column, Embed in ToolStripContainer,
/// Insert Standard Items). The view lists what each control offers; the client takes the property rows from the
/// grid and sends a verb as <c>{ op: "verb", id, value }</c>.
/// </summary>
public sealed partial class DesignSurface
{
    private static DesignerSmartAction Prop(string label, string prop, bool invert = false) => new() { Label = label, Prop = prop, Invert = invert };

    private static DesignerSmartAction Edit(string label, string prop) => new() { Label = label, Edit = prop };

    private static DesignerSmartAction Verb(string label, string verb) => new() { Label = label, Verb = verb };

    /// <summary>What the Tasks panel of a control offers, as VS's designers do; null for a control that has none (a Button).</summary>
    private static List<DesignerSmartAction>? SmartTagFor(Control control)
    {
        var actions = new List<DesignerSmartAction>();
        switch (control)
        {
            case TextBox:
                actions.Add(Prop("MultiLine", "Multiline"));
                break;
            case ListControl:
                if (control is ComboBox or ListBox) actions.Add(Edit("Edit Items…", "Items"));
                actions.Add(Prop("Data Source", "DataSource"));
                actions.Add(Prop("Display Member", "DisplayMember"));
                actions.Add(Prop("Value Member", "ValueMember"));
                break;
            case DataGridView:
                actions.Add(Prop("Choose Data Source", "DataSource"));
                actions.Add(Edit("Edit Columns…", "Columns"));
                actions.Add(Prop("Enable Adding", "AllowUserToAddRows"));
                actions.Add(Prop("Enable Editing", "ReadOnly", invert: true));
                actions.Add(Prop("Enable Deleting", "AllowUserToDeleteRows"));
                actions.Add(Prop("Enable Column Reordering", "AllowUserToOrderColumns"));
                break;
            case PictureBox:
                actions.Add(Edit("Choose Image…", "Image"));
                actions.Add(Prop("Size Mode", "SizeMode"));
                break;
            case ListView:
                actions.Add(Edit("Edit Items…", "Items"));
                actions.Add(Edit("Edit Columns…", "Columns"));
                actions.Add(Prop("View", "View"));
                actions.Add(Prop("Small Image List", "SmallImageList"));
                actions.Add(Prop("Large Image List", "LargeImageList"));
                break;
            case TreeView:
                actions.Add(Edit("Edit Nodes…", "Nodes"));
                actions.Add(Prop("Choose ImageList", "ImageList"));
                break;
            case TabControl:
                actions.Add(Verb("Add Tab", "AddTab"));
                if (((TabControl)control).TabCount > 0) actions.Add(Verb("Remove Tab", "RemoveTab"));
                break;
            case SplitContainer:
                actions.Add(Prop("Splitter Orientation", "Orientation"));
                break;
            case TableLayoutPanel:
                actions.Add(Verb("Add Column", "AddColumn"));
                actions.Add(Verb("Add Row", "AddRow"));
                actions.Add(Verb("Remove Last Column", "RemoveColumn"));
                actions.Add(Verb("Remove Last Row", "RemoveRow"));
                actions.Add(Edit("Edit Rows and Columns…", "Columns"));
                break;
            case ToolStripContainer container:
                if (container.Dock != DockStyle.Fill) actions.Add(Verb("Dock Fill in Form", "DockFill"));
                actions.Add(Prop("Top", "TopToolStripPanelVisible"));
                actions.Add(Prop("Bottom", "BottomToolStripPanelVisible"));
                actions.Add(Prop("Left", "LeftToolStripPanelVisible"));
                actions.Add(Prop("Right", "RightToolStripPanelVisible"));
                return actions;
            case ToolStrip strip and not ToolStripDropDown:
                if (control.Parent is not ToolStripPanel) actions.Add(Verb("Embed in ToolStripContainer", "EmbedInToolStripContainer"));
                if (strip is MenuStrip && strip.Items.Count == 0) actions.Add(Verb("Insert Standard Items", "InsertStandardItems"));
                actions.Add(Prop("RenderMode", "RenderMode"));
                if (control.Parent is not ToolStripPanel) actions.Add(Prop("Dock", "Dock"));
                actions.Add(Prop("GripStyle", "GripStyle"));
                actions.Add(Edit("Edit Items…", "Items"));
                return actions;
        }
        // Containers and the big controls fill their parent in one click, as VS's "Dock in Parent Container".
        if (control is DataGridView or PictureBox or ListView or TreeView or TabControl or SplitContainer or TableLayoutPanel
            or Panel or GroupBox or RichTextBox or UserControl && control is not TabPage && control is not SplitterPanel && control is not ToolStripContentPanel
            && control.Parent is not (TableLayoutPanel or FlowLayoutPanel or ToolStripPanel))
        {
            actions.Add(control.Dock == DockStyle.Fill ? Verb("Undock in Parent Container", "Undock") : Verb("Dock in Parent Container", "DockFill"));
        }
        return actions.Count == 0 ? null : actions;
    }

    private void ApplyVerb(object target, string? verb)
    {
        switch (verb, target)
        {
            case ("DockFill", Control control):
                control.Dock = DockStyle.Fill;
                break;
            case ("Undock", Control control):
                control.Dock = DockStyle.None;
                break;
            case ("AddTab", TabControl tabs):
            {
                var page = new TabPage();
                var component = Model.AddComponent(page);
                Place(page, tabs, new DesignerOp());
                InitializeNew(page, component, tabs);
                tabs.SelectedTab = page;
                break;
            }
            case ("RemoveTab", TabControl tabs):
                if (tabs.SelectedTab is { } selected) Remove(selected);
                break;
            case ("AddColumn" or "AddRow" or "RemoveColumn" or "RemoveRow", TableLayoutPanel table):
            {
                bool columns = verb.EndsWith("Column", StringComparison.Ordinal);
                var grid = new TableGrid(table);
                int count = (columns ? grid.Columns : grid.Rows) + (verb.StartsWith("Add", StringComparison.Ordinal) ? 1 : -1);
                if (count < 1) throw new DesignerEditException($"A table keeps at least one {(columns ? "column" : "row")}.");
                SetTrackCount(table, columns ? "ColumnCount" : "RowCount", count.ToString(CultureInfo.InvariantCulture));
                break;
            }
            case ("EmbedInToolStripContainer", ToolStrip strip):
                EmbedInToolStripContainer(strip);
                break;
            case ("InsertStandardItems", MenuStrip menu):
                InsertStandardItems(menu);
                break;
            default:
                throw new DesignerEditException($"'{target.GetType().Name}' has no task '{verb}'.");
        }
    }

    /// <summary>
    /// VS's "Embed in ToolStripContainer": a ToolStripContainer fills the strip's container; the strip goes into its top
    /// panel - every tool strip there into the panel of its side - and the other controls into its middle.
    /// </summary>
    private void EmbedInToolStripContainer(ToolStrip strip)
    {
        var parent = strip.Parent ?? throw new DesignerEditException("The strip is not on the form.");
        if (parent is ToolStripPanel) throw new DesignerEditException("The strip is in a ToolStripContainer already.");
        var container = new ToolStripContainer();
        var component = Model.AddComponent(container);
        container.Text = component.Name;
        var others = parent.Controls.Cast<Control>().ToList();
        parent.Controls.Add(container);
        container.Dock = DockStyle.Fill;
        parent.Controls.SetChildIndex(container, 0);
        foreach (var control in others)
        {
            if (control is ToolStrip s and not ToolStripDropDown && Model.FindByInstance(s) != null)
            {
                var panel = s == strip || s.Dock == DockStyle.Top ? container.TopToolStripPanel
                    : s.Dock == DockStyle.Bottom ? container.BottomToolStripPanel
                    : s.Dock == DockStyle.Left ? container.LeftToolStripPanel
                    : s.Dock == DockStyle.Right ? container.RightToolStripPanel
                    : container.TopToolStripPanel;
                parent.Controls.Remove(s);
                s.Dock = DockStyle.None;
                panel.Join(s, panel.Rows.Length);
                continue;
            }
            if (Model.FindByInstance(control) == null) continue;
            var location = control.Location;
            parent.Controls.Remove(control);
            container.ContentPanel.Controls.Add(control);
            control.Location = location;
        }
    }

    /// <summary>VS's "Insert Standard Items" of a MenuStrip: File, Edit, Tools and Help with their items and shortcuts (without VS's pictures).</summary>
    private void InsertStandardItems(MenuStrip menu)
    {
        var menus = new (string Text, (string Text, Keys Keys)[] Items)[]
        {
            ("&File", new[] { ("&New", Keys.Control | Keys.N), ("&Open", Keys.Control | Keys.O), ("-", Keys.None), ("&Save", Keys.Control | Keys.S), ("Save &As", Keys.None), ("-", Keys.None), ("&Print", Keys.Control | Keys.P), ("Print Pre&view", Keys.None), ("-", Keys.None), ("E&xit", Keys.None) }),
            ("&Edit", new[] { ("&Undo", Keys.Control | Keys.Z), ("&Redo", Keys.Control | Keys.Y), ("-", Keys.None), ("Cu&t", Keys.Control | Keys.X), ("&Copy", Keys.Control | Keys.C), ("&Paste", Keys.Control | Keys.V), ("-", Keys.None), ("Select &All", Keys.None) }),
            ("&Tools", new[] { ("&Customize", Keys.None), ("&Options", Keys.None) }),
            ("&Help", new[] { ("&Contents", Keys.None), ("&Index", Keys.None), ("&Search", Keys.None), ("-", Keys.None), ("&About...", Keys.None) }),
        };
        foreach (var (text, items) in menus)
        {
            var top = NewMenuItem(text);
            menu.Items.Add(top);
            foreach (var (itemText, keys) in items)
            {
                ToolStripItem item;
                if (itemText == "-")
                {
                    item = new ToolStripSeparator();
                    var separator = Model.AddComponent(item, UniqueName("toolStripSeparator"));
                    item.Name = separator.Name;
                }
                else
                {
                    var menuItem = NewMenuItem(itemText);
                    if (keys != Keys.None) menuItem.ShortcutKeys = keys;
                    item = menuItem;
                }
                top.DropDownItems.Add(item);
            }
        }
    }

    /// <summary>A menu item named as VS names the standard ones: newToolStripMenuItem, saveAsToolStripMenuItem.</summary>
    private ToolStripMenuItem NewMenuItem(string text)
    {
        var words = text.Replace("&", "").Replace(".", "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var baseName = string.Concat(words.Select((w, i) => i == 0 ? w.ToLowerInvariant() : char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant())) + "ToolStripMenuItem";
        var item = new ToolStripMenuItem { Text = text };
        var component = Model.AddComponent(item, UniqueName(baseName));
        item.Name = component.Name;
        return item;
    }

    private string UniqueName(string baseName)
    {
        var name = baseName;
        for (int i = 1; Model.Find(name) != null; i++) name = baseName + i.ToString(CultureInfo.InvariantCulture);
        return name;
    }
}

/// <summary>One line of a control's smart tag panel: a property of its grid, an editor to open, or a verb.</summary>
public sealed class DesignerSmartAction
{
    public string Label { get; set; } = "";

    /// <summary>A property row of the grid, edited in the panel.</summary>
    public string? Prop { get; set; }

    /// <summary>A bool shown the other way round ("Enable Editing" is ReadOnly off).</summary>
    public bool Invert { get; set; }

    /// <summary>The property whose editor (…) the action opens: Items, Columns, Nodes, Image.</summary>
    public string? Edit { get; set; }

    /// <summary>A verb, sent as <c>{ op: "verb", id, value }</c>.</summary>
    public string? Verb { get; set; }
}
