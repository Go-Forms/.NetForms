using System.Collections.Generic;

namespace NetForms.Design;

/// <summary>What the client draws: the form as NetForms paints it, and where every component is on it.</summary>
public sealed class DesignerView
{
    /// <summary>The designed class (<c>MainForm</c>).</summary>
    public string ClassName { get; set; } = "";

    /// <summary>The root's type (<c>System.Windows.Forms.Form</c>).</summary>
    public string RootType { get; set; } = "";

    /// <summary>The root's caption, for the title bar the client draws around the canvas.</summary>
    public string Text { get; set; } = "";

    /// <summary>The client area - the size of the picture.</summary>
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>The client area as a base64 PNG.</summary>
    public string Png { get; set; } = "";

    /// <summary>Everything on the canvas, parents before children, in paint order among siblings.</summary>
    public List<DesignerViewItem> Items { get; set; } = new();

    /// <summary>Components without a place on the form (Timer, ToolTip, ContextMenuStrip): the component tray.</summary>
    public List<DesignerTrayItem> Tray { get; set; } = new();

    /// <summary>Whether undo/redo have anything to do.</summary>
    public bool CanUndo { get; set; }
    public bool CanRedo { get; set; }

    /// <summary>Set by an edit that wrote a handler stub: where it went, for "go to the handler".</summary>
    public DesignerHandlerLocation? Handler { get; set; }

    /// <summary>Set by a paste or duplicate: the new components, for the client to select.</summary>
    public List<string>? Select { get; set; }
}

public sealed class DesignerViewItem
{
    public string Id { get; set; } = "";

    /// <summary>Full type name.</summary>
    public string Type { get; set; } = "";

    /// <summary>The containing component's id ("" for the root).</summary>
    public string Parent { get; set; } = "";

    /// <summary><c>control</c>, <c>nested</c> (SplitContainer.Panel1: selectable, not movable) or <c>item</c> (a tool strip item).</summary>
    public string Kind { get; set; } = "control";

    /// <summary>Where it is on the canvas (root client coordinates).</summary>
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }

    /// <summary>Its own Location/Size, relative to the parent's client area (what setBounds takes).</summary>
    public int Left { get; set; }
    public int Top { get; set; }

    /// <summary>Where its own client area starts on the canvas: children's Left/Top count from here.</summary>
    public int ClientX { get; set; }
    public int ClientY { get; set; }

    /// <summary>False when it is not on screen (a tab page that is not selected, an item on the overflow).</summary>
    public bool Visible { get; set; }

    /// <summary>Other controls can be dropped into it (Panel, GroupBox, TabPage, …).</summary>
    public bool Container { get; set; }

    /// <summary>The user may drag it / pull its edges (not a Fill-docked control, not a nested panel).</summary>
    public bool Movable { get; set; }
    public bool Resizable { get; set; }

    public string Dock { get; set; } = "None";
    public string Anchor { get; set; } = "";

    /// <summary>For the tab-order view (-1 for tool strip items).</summary>
    public int TabIndex { get; set; } = -1;

    /// <summary>For snap lines: the control's Margin and Padding (left, top, right, bottom).</summary>
    public int[] Margin { get; set; } = new int[4];
    public int[] Padding { get; set; } = new int[4];

    /// <summary>A TabControl's tab headers on the canvas (x, y, w, h), in page order; null for other controls.</summary>
    public List<int[]>? Tabs { get; set; }

    /// <summary>A TabControl's selected page (-1 for none; 0 for other controls).</summary>
    public int SelectedIndex { get; set; }
}

public sealed class DesignerTrayItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
}

public sealed class DesignerHandlerLocation
{
    public string File { get; set; } = "";
    public string Method { get; set; } = "";

    /// <summary>1-based line of the method name.</summary>
    public int Line { get; set; }

    /// <summary>False when the method was already there.</summary>
    public bool Created { get; set; }
}

/// <summary>One row of the property grid.</summary>
public sealed class DesignerPropertyRow
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>The property's type, short (<c>Size</c>, <c>AnchorStyles</c>).</summary>
    public string Type { get; set; } = "";

    /// <summary>The value as the grid shows it (the converter's invariant text; a component's name).</summary>
    public string? Value { get; set; }

    /// <summary><c>bool</c>, <c>enum</c>, <c>flags</c>, <c>number</c>, <c>text</c>, <c>color</c>, <c>font</c>, <c>component</c>, <c>collection</c>, <c>readonly</c>.</summary>
    public string Editor { get; set; } = "text";

    /// <summary>The values to pick from (enum members, standard values, compatible components).</summary>
    public List<string>? Options { get; set; }

    public bool ReadOnly { get; set; }

    /// <summary>True when the value would be written to the file (bold in the VS grid).</summary>
    public bool Modified { get; set; }

    /// <summary>For a collection (ListBox.Items, TreeView.Nodes): its elements as lines of text.</summary>
    public List<string>? Items { get; set; }

    /// <summary>
    /// How <see cref="Items"/> spell the elements: <c>lines</c> (one per line), <c>tree</c> (a node per
    /// line, two spaces of indent per level), <c>columns</c> (a ListViewItem's sub-items separated by <c>|</c>),
    /// <c>components</c> (the captions of tab pages, strip items or columns; <c>-</c> for a separator).
    /// </summary>
    public string? ItemsFormat { get; set; }

    /// <summary>For a collection of components (<c>components</c>): the id of each element (<c>#index</c> for one without a name), beside its caption in <see cref="Items"/>.</summary>
    public List<string>? ItemIds { get; set; }
}

/// <summary>One row of the Events tab.</summary>
public sealed class DesignerEventRow
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>The bound handler method, or null.</summary>
    public string? Handler { get; set; }

    /// <summary>The handler's parameter list (<c>object sender, MouseEventArgs e</c>).</summary>
    public string Parameters { get; set; } = "";

    /// <summary>The handler name the designer proposes: <c>button1_Click</c>.</summary>
    public string DefaultHandler { get; set; } = "";

    /// <summary>The component's [DefaultEvent]: what a double-click on it in the designer wires.</summary>
    public bool IsDefault { get; set; }
}

/// <summary>A toolbox group, as Visual Studio groups them.</summary>
public sealed class DesignerToolboxCategory
{
    public string Name { get; set; } = "";

    /// <summary>A group of the project's control libraries (decision 157), not of NetForms.</summary>
    public bool Library { get; set; }

    /// <summary>The library's key in the client's configuration.</summary>
    public string? Id { get; set; }

    /// <summary>Why assemblies of the library cannot be used (built for the .NET Framework, …).</summary>
    public List<string> Errors { get; set; } = new();
    public List<DesignerToolboxItem> Items { get; set; } = new();
}

public sealed class DesignerToolboxItem
{
    /// <summary>Full type name, what <c>add</c> takes.</summary>
    public string Type { get; set; } = "";
    public string Name { get; set; } = "";

    /// <summary>It lands in the component tray, not on the form.</summary>
    public bool Tray { get; set; }

    /// <summary>16×16 PNG (base64) from the type's <c>[ToolboxBitmap]</c>; null for the default icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Why it cannot be added now (its assembly is not loaded); null when it can.</summary>
    public string? Unavailable { get; set; }
}
