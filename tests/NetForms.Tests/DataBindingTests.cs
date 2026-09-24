using System.Data;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Binding to ADO.NET data (decision 141): BindingContext and CurrencyManager, a DataSet with a table as the
/// DataMember, master-detail through a DataRelation, DisplayMember/ValueMember over a DataTable. The same
/// cases run against real WinForms in CompatScenarios ("exact/binding/*").
/// </summary>
public class DataBindingTests
{
    private static DataSet Shop()
    {
        var ds = new DataSet("Shop");
        var customers = ds.Tables.Add("Customers");
        customers.Columns.Add("Id", typeof(int));
        customers.Columns.Add("Name", typeof(string));
        customers.Rows.Add(1, "Ann");
        customers.Rows.Add(2, "Bob");
        customers.Rows.Add(3, "Cid");
        var orders = ds.Tables.Add("Orders");
        orders.Columns.Add("Id", typeof(int));
        orders.Columns.Add("CustomerId", typeof(int));
        orders.Columns.Add("Item", typeof(string));
        orders.Rows.Add(10, 1, "Tea");
        orders.Rows.Add(11, 1, "Jam");
        orders.Rows.Add(12, 2, "Bread");
        ds.Relations.Add("CustomerOrders", customers.Columns["Id"]!, orders.Columns["CustomerId"]!);
        return ds;
    }

    private static Form ShowForm(params Control[] controls)
    {
        TestPlatform.Install();
        var form = new Form { ClientSize = new Size(600, 400) };
        form.Controls.AddRange(controls);
        form.Show();
        return form;
    }

    private static string Columns(DataGridView grid) => string.Join(",", grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Name));

    [Fact]
    public void ABindingSourceOverADataSetShowsTheTableNamedByDataMember()
    {
        var ds = Shop();
        using var source = new BindingSource { DataSource = ds, DataMember = "Customers" };
        Assert.Equal(3, source.Count);
        Assert.Equal("Ann", ((DataRowView)source.Current!)["Name"]);
        Assert.Equal("Name", source.GetItemProperties(null).Find("Name", false)?.Name);
    }

    [Fact]
    public void ADetailBindingSourceFollowsTheMastersCurrentRowThroughTheRelation()
    {
        var ds = Shop();
        using var master = new BindingSource { DataSource = ds, DataMember = "Customers" };
        using var detail = new BindingSource { DataSource = master, DataMember = "CustomerOrders" };
        Assert.Equal(2, detail.Count);
        master.Position = 1;
        Assert.Single(detail);
        Assert.Equal("Bread", ((DataRowView)detail.Current!)["Item"]);
        master.MoveLast();
        Assert.Empty(detail);
    }

    [Fact]
    public void MasterAndDetailGridsFollowEachOther()
    {
        var ds = Shop();
        var master = new BindingSource { DataSource = ds, DataMember = "Customers" };
        var detail = new BindingSource { DataSource = master, DataMember = "CustomerOrders" };
        var masterGrid = new DataGridView { Bounds = new Rectangle(0, 0, 300, 150), AllowUserToAddRows = false, DataSource = master };
        var detailGrid = new DataGridView { Bounds = new Rectangle(300, 0, 300, 150), AllowUserToAddRows = false, DataSource = detail };
        var name = new TextBox { Bounds = new Rectangle(0, 260, 100, 20) };
        name.DataBindings.Add("Text", master, "Name", true);
        using var form = ShowForm(masterGrid, detailGrid, name);

        // The relation is a child list, not a column.
        Assert.Equal("Id,Name", Columns(masterGrid));
        Assert.Equal("Id,CustomerId,Item", Columns(detailGrid));
        Assert.Equal((0, 2, "Ann"), (masterGrid.CurrentCellAddress.Y, detailGrid.Rows.Count, name.Text));

        master.Position = 1;
        Assert.Equal((1, 1, "Bob"), (masterGrid.CurrentCellAddress.Y, detailGrid.Rows.Count, name.Text));
        Assert.Equal("Bread", detailGrid.Rows[0].Cells["Item"].Value);

        // Moving in the grid moves the source, and everything bound to it.
        masterGrid.CurrentCell = masterGrid.Rows[2].Cells[1];
        Assert.Equal((2, 0, "Cid"), (master.Position, detailGrid.Rows.Count, name.Text));
    }

    [Fact]
    public void AGridTakesATableOrARelationPathAsItsDataMember()
    {
        var ds = Shop();
        var customers = new DataGridView { Bounds = new Rectangle(0, 0, 300, 150), AllowUserToAddRows = false, DataSource = ds, DataMember = "Customers" };
        var orders = new DataGridView { Bounds = new Rectangle(300, 0, 300, 150), AllowUserToAddRows = false, DataSource = ds, DataMember = "Customers.CustomerOrders" };
        using var form = ShowForm(customers, orders);

        Assert.Equal(3, customers.Rows.Count);
        Assert.Equal(2, orders.Rows.Count);
        // Both use BindingContext[ds, "Customers"]: the child grid follows the parent grid's current row.
        customers.CurrentCell = customers.Rows[1].Cells[0];
        Assert.Equal(1, form.BindingContext![ds, "Customers"].Position);
        Assert.Single(orders.Rows);
    }

    [Fact]
    public void ListControlsShowDisplayMemberAndSelectValueMemberOverADataTable()
    {
        var ds = Shop();
        var customers = ds.Tables["Customers"]!;
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        var list = new ListBox { Bounds = new Rectangle(0, 30, 100, 80) };
        using var form = ShowForm(combo, list);
        combo.DataSource = customers;
        combo.DisplayMember = "Name";
        combo.ValueMember = "Id";
        list.DataSource = ds;
        list.DisplayMember = "Customers.Name";

        Assert.Equal(new[] { "Ann", "Bob", "Cid" }, combo.Items.Cast<object>().Select(combo.GetItemText));
        Assert.Equal((0, 1), (combo.SelectedIndex, (int)combo.SelectedValue!));
        combo.SelectedValue = 3;
        Assert.Equal((2, "Cid"), (combo.SelectedIndex, combo.Text));
        Assert.Equal(2, form.BindingContext![customers].Position);

        Assert.Equal(new[] { "Ann", "Bob", "Cid" }, list.Items.Cast<object>().Select(list.GetItemText));
        list.SelectedIndex = 1;
        Assert.Equal(1, form.BindingContext[ds, "Customers"].Position);

        // The items belong to the data source now.
        Assert.Throws<ArgumentException>(() => combo.Items.Add("Dan"));
    }

    [Fact]
    public void ControlsBoundToTheSameSourceShareOneCurrencyManager()
    {
        var ds = Shop();
        var table = ds.Tables["Customers"]!;
        var id = new TextBox();
        var name = new TextBox();
        id.DataBindings.Add("Text", table, "Id");
        name.DataBindings.Add("Text", table, "Name");
        using var form = ShowForm(id, name);

        var manager = (CurrencyManager)form.BindingContext![table];
        Assert.Same(manager, id.DataBindings[0].BindingManagerBase);
        Assert.Same(manager, name.DataBindings[0].BindingManagerBase);
        Assert.True(id.DataBindings[0].IsBinding);
        Assert.Equal(("1", "Ann"), (id.Text, name.Text));

        manager.Position = 2;
        Assert.Equal(("3", "Cid"), (id.Text, name.Text));

        // A control not yet created does not bind (WinForms waits for the handle).
        var late = new TextBox();
        late.DataBindings.Add("Text", table, "Name");
        Assert.False(late.DataBindings[0].IsBinding);
        form.Controls.Add(late);
        Assert.True(late.DataBindings[0].IsBinding);
        Assert.Equal("Cid", late.Text);
    }
}
