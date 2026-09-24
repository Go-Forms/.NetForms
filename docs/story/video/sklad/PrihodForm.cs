namespace Sklad
{
    public partial class PrihodForm : Form
    {
        public PrihodForm(int number)
        {
            InitializeComponent();
            txtNumber.Text = number.ToString();
            cmbSupplier.SelectedIndex = 0;
            ShowGoods(string.Empty);
        }

        internal Stock.Item? SelectedItem { get; private set; }

        internal int Quantity => (int)numQty.Value;

        internal string Supplier => cmbSupplier.Text;

        private void ShowGoods(string filter)
        {
            gridGoods.Rows.Clear();
            foreach (var i in Stock.Items.Where(i => i.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)))
            {
                int row = gridGoods.Rows.Add(i.Code, i.Name, i.Unit, i.Price.ToString("N2", MainForm.Ru));
                gridGoods.Rows[row].Tag = i;
            }
        }

        private void txtSearch_TextChanged(object? sender, EventArgs e)
        {
            ShowGoods(txtSearch.Text);
            if (gridGoods.Rows.Count > 0)
            {
                gridGoods.Rows[0].Selected = true;
                gridGoods.CurrentCell = gridGoods.Rows[0].Cells[0];
            }
        }

        private void PrihodForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F2)
            {
                Save();
                e.Handled = true;
            }
        }

        private void btnSave_Click(object? sender, EventArgs e) => Save();

        private void Save()
        {
            if (gridGoods.CurrentRow?.Tag is not Stock.Item item || numQty.Value <= 0)
            {
                return;
            }
            SelectedItem = item;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
