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
/// <item><term>add</term><description><c>type</c> (full name), <c>parent</c>, <c>x</c>/<c>y</c>, optional <c>w</c>/<c>h</c> and <c>id</c>.</description></item>
/// <item><term>remove</term><description><c>id</c>, with everything inside it.</description></item>
/// <item><term>setParent</term><description><c>id</c>, <c>parent</c>, <c>x</c>/<c>y</c>.</description></item>
/// <item><term>rename</term><description><c>id</c>, <c>value</c> = the new name.</description></item>
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
}
