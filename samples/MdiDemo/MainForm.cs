namespace MdiDemo
{
    /// <summary>
    /// The MDI parent: a menu bar, a tool bar, a status bar and a client area that holds the
    /// document windows. The Window menu lists the open documents, as an MDI application does.
    /// </summary>
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            MdiChildActivate += (_, _) => UpdateStatus();
            NewDocument();
            NewDocument();
            LayoutMdi(MdiLayout.Cascade);
        }

        public DocumentForm NewDocument()
        {
            var document = new DocumentForm { MdiParent = this };
            document.FormClosed += (_, _) => UpdateStatus();
            document.Show();
            UpdateStatus();
            return document;
        }

        public void ReportLength(int length) => statusLabel.Text = length + " characters";

        private void UpdateStatus()
        {
            int count = MdiChildren.Length;
            countLabel.Text = count == 1 ? "1 document" : count + " documents";
            statusLabel.Text = ActiveMdiChild != null ? "Active: " + ActiveMdiChild.Text : "No document";
        }

        /// <summary>
        /// The same form class, opened as an independent top-level window instead of an MDI child:
        /// it gets its own OS window, its own place in the task bar and its own title bar.
        /// </summary>
        public DocumentForm NewSeparateWindow()
        {
            var window = new DocumentForm();
            window.Text += " (separate window)";
            window.Show();
            return window;
        }

        private void newDocument_Click(object sender, EventArgs e) => NewDocument();

        private void newSeparateWindow_Click(object sender, EventArgs e) => NewSeparateWindow();

        private void closeDocument_Click(object sender, EventArgs e) => ActiveMdiChild?.Close();

        private void exit_Click(object sender, EventArgs e) => Close();

        private void cascade_Click(object sender, EventArgs e) => LayoutMdi(MdiLayout.Cascade);

        private void tileHorizontal_Click(object sender, EventArgs e) => LayoutMdi(MdiLayout.TileHorizontal);

        private void tileVertical_Click(object sender, EventArgs e) => LayoutMdi(MdiLayout.TileVertical);

        private void arrangeIcons_Click(object sender, EventArgs e) => LayoutMdi(MdiLayout.ArrangeIcons);

        /// <summary>Rebuilds the list of open documents under the separator each time the menu opens.</summary>
        private void windowMenu_DropDownOpening(object sender, EventArgs e)
        {
            int fixedItems = windowMenu.DropDownItems.IndexOf(windowListSeparator) + 1;
            while (windowMenu.DropDownItems.Count > fixedItems)
            {
                windowMenu.DropDownItems.RemoveAt(windowMenu.DropDownItems.Count - 1);
            }

            var children = MdiChildren;
            windowListSeparator.Visible = children.Length > 0;
            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                var item = new ToolStripMenuItem($"&{i + 1} {child.Text}")
                {
                    Checked = ReferenceEquals(child, ActiveMdiChild),
                    Tag = child,
                };
                item.Click += (s, _) =>
                {
                    if (((ToolStripMenuItem)s!).Tag is Form target) ActivateChild(target);
                };
                windowMenu.DropDownItems.Add(item);
            }
        }

        private void ActivateChild(Form child)
        {
            child.Activate();
            UpdateStatus();
        }
    }
}
