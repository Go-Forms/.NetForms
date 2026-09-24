namespace Strips
{
    /// <summary>
    /// The WinForms idiom for a main window: a menu bar, a tool bar, a status bar and a
    /// context menu, all written the way the designer writes them.
    /// </summary>
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            zoomComboBox.Items.AddRange(new object[] { "50%", "100%", "150%", "200%" });
            zoomComboBox.SelectedIndex = 1;
        }

        private void fileNew_Click(object sender, EventArgs e)
        {
            editor.Clear();
            Say("New file");
        }

        private void fileOpen_Click(object sender, EventArgs e) => Say("Open...");

        private void recent_Click(object sender, EventArgs e) => Say("Opening " + ((ToolStripItem)sender).Text);

        private void fileExit_Click(object sender, EventArgs e) => Close();

        private void editCut_Click(object sender, EventArgs e)
        {
            editor.Cut();
            Say("Cut");
        }

        private void editCopy_Click(object sender, EventArgs e)
        {
            editor.Copy();
            Say("Copy");
        }

        private void editPaste_Click(object sender, EventArgs e)
        {
            editor.Paste();
            Say("Paste");
        }

        private void wordWrap_CheckedChanged(object sender, EventArgs e)
        {
            editor.WordWrap = wordWrapMenuItem.Checked;
            Say("Word wrap " + (wordWrapMenuItem.Checked ? "on" : "off"));
        }

        private void statusBar_CheckedChanged(object sender, EventArgs e)
        {
            statusStrip1.Visible = statusBarMenuItem.Checked;
        }

        private void style_CheckedChanged(object sender, EventArgs e)
        {
            var style = FontStyle.Regular;
            if (boldButton.Checked) style |= FontStyle.Bold;
            if (italicButton.Checked) style |= FontStyle.Italic;
            editor.Font = new Font(editor.Font, style);
            Say("Style: " + style);
        }

        private void zoom_SelectedIndexChanged(object sender, EventArgs e) => Say("Zoom " + zoomComboBox.SelectedItem);

        private void view_Click(object sender, EventArgs e)
        {
            viewDropDownButton.Text = ((ToolStripItem)sender).Text;
            Say("View: " + viewDropDownButton.Text);
        }

        private void undo_ButtonClick(object sender, EventArgs e)
        {
            editor.Undo();
            Say("Undo");
        }

        private void undoAll_Click(object sender, EventArgs e) => Say("Undo all");

        private void Say(string text) => statusLabel.Text = text;
    }
}
