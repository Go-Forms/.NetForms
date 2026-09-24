namespace Gallery
{
    public partial class GalleryForm : Form
    {
        public GalleryForm()
        {
            InitializeComponent();
            toolTip1.SetToolTip(button1, "Shows a message box");
            toolTip1.SetToolTip(trackBar1, "Drag me");
            listBox1.Items.AddRange(new object[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta", "Iota", "Kappa" });
            comboBox1.Items.AddRange(new object[] { "Apple", "Banana", "Cherry", "Date" });
            comboBox1.SelectedIndex = 0;
            comboBox2.Items.AddRange(new object[] { "Red", "Green", "Blue" });
            comboBox2.SelectedIndex = 1;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(this, $"Text box says: {textBox1.Text}\nList selection: {listBox1.SelectedItem}", "Gallery", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            statusLabel.Text = "MessageBox returned " + result;
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            progressBar1.Value = trackBar1.Value * 10;
            numericUpDown1.Value = trackBar1.Value;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            int v = (int)numericUpDown1.Value;
            if (v >= trackBar1.Minimum && v <= trackBar1.Maximum) trackBar1.Value = v;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox2.Enabled = !checkBox1.Checked;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            statusLabel.Text = "Selected: " + listBox1.SelectedItem;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tabControl1.SelectedIndex = (tabControl1.SelectedIndex + 1) % tabControl1.TabCount;
        }
    }
}
