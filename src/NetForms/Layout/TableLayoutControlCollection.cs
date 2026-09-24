namespace System.Windows.Forms;

/// <summary>
/// The <see cref="TableLayoutPanel.Controls"/> collection: <c>Add(control, column, row)</c> places the child in a
/// cell. A top-level type of <c>System.Windows.Forms</c>, as in WinForms (it was nested in the panel, decision 146).
/// </summary>
public class TableLayoutControlCollection : Control.ControlCollection
{
    private readonly TableLayoutPanel _owner;

    public TableLayoutControlCollection(TableLayoutPanel container) : base(container) => _owner = container;

    public TableLayoutPanel Container => _owner;

    public virtual void Add(Control control, int column, int row)
    {
        base.Add(control);
        _owner.SetCellPosition(control, new TableLayoutPanelCellPosition(column, row));
    }

    public override void Remove(Control? value)
    {
        base.Remove(value);
        if (value != null) _owner.ForgetControl(value);
    }
}
