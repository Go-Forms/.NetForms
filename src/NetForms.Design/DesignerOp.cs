namespace NetForms.Design;

/// <summary>
/// One edit the designer client asks for. The vocabulary is GoFormsDesigner's (Ф5.4 ports its webview),
/// addressed by component names: <c>""</c> (or the class name) is the root, <c>splitContainer1.Panel1</c>
/// a nested component.
/// </summary>
/// <remarks>
/// <list type="table">
/// <item><term>setBounds</term><description><c>id</c>, <c>x</c>/<c>y</c>/<c>w</c>/<c>h</c> in the parent's client coordinates (any may be left out); on the root it sets the client size.</description></item>
/// <item><term>setForm</term><description><c>w</c>/<c>h</c>: the root's client size.</description></item>
/// <item><term>setProp</term><description><c>id</c>, <c>prop</c>, <c>value</c> as the property grid shows it (the converter's invariant text; a component by name; "" for none).</description></item>
/// <item><term>resetProp</term><description><c>id</c>, <c>prop</c>: back to the default.</description></item>
/// <item><term>setItems</term><description><c>id</c>, <c>prop</c>, <c>values</c>: replace a string collection (ListBox.Items). For a collection of components (TabPages, a strip's Items, Columns) also <c>ids</c>: the element each value is the caption of, "" for a new one; elements left out are removed.</description></item>
/// <item><term>setEvent</term><description><c>id</c>, <c>event</c>, <c>handler</c> ("" unbinds). A missing handler method is added to the code-behind file.</description></item>
/// <item><term>add</term><description><c>type</c> (full name), <c>parent</c>, <c>x</c>/<c>y</c>, optional <c>w</c>/<c>h</c> and <c>id</c>; into a TableLayoutPanel <c>column</c>/<c>row</c> instead of x/y (the first free cell from there; none - the first free one); into a FlowLayoutPanel <c>index</c>, the place in its flow (none - at the end).</description></item>
/// <item><term>remove</term><description><c>id</c>, with everything inside it.</description></item>
/// <item><term>setParent</term><description><c>id</c>, <c>parent</c>, <c>x</c>/<c>y</c> (into a TableLayoutPanel: <c>column</c>/<c>row</c>; into a FlowLayoutPanel: <c>index</c>).</description></item>
/// <item><term>reorder</term><description><c>id</c> (a container), <c>ids</c>: its controls in the new order (a FlowLayoutPanel's flow); those left out follow, as they were.</description></item>
/// <item><term>setCell</term><description><c>id</c> (a control in a TableLayoutPanel), <c>column</c>, <c>row</c>: moves it there; a control already there swaps with it.</description></item>
/// <item><term>setStyles</term><description><c>id</c> (a TableLayoutPanel), <c>prop</c> = <c>ColumnStyles</c> or <c>RowStyles</c>, <c>values</c> = the tracks' styles (<c>Percent 50</c>, <c>Absolute 120</c>, <c>AutoSize</c>), optional <c>ids</c> = the track each was (<c>#2</c>, "" for a new one). Controls move with their tracks; those of a removed track take free cells.</description></item>
/// <item><term>rename</term><description><c>id</c>, <c>value</c> = the new name.</description></item>
/// <item><term>paste</term><description><c>value</c> = clipboard text from the <c>copy</c> request, <c>parent</c> = what is selected (a container, or a control whose container takes the paste; a strip or menu item for strip items); optional <c>x</c>/<c>y</c>.</description></item>
/// <item><term>duplicate</term><description><c>ids</c>: a copy of each beside the original, in its container.</description></item>
/// <item><term>bringToFront / sendToBack</term><description><c>id</c>.</description></item>
/// </list>
/// </remarks>
public sealed class DesignerOp
{
    public string Op { get; set; } = "";
    public string? Id { get; set; }
    public string? Type { get; set; }
    public string? Parent { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? W { get; set; }
    public int? H { get; set; }
    public string? Prop { get; set; }
    public string? Value { get; set; }
    public string[]? Values { get; set; }
    public string?[]? Ids { get; set; }
    public string? Event { get; set; }
    public string? Handler { get; set; }
    public int? Column { get; set; }
    public int? Row { get; set; }
    public int? Index { get; set; }
}
