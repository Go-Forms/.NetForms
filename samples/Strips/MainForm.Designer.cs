namespace Strips
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            menuStrip1 = new MenuStrip();
            fileMenu = new ToolStripMenuItem();
            newMenuItem = new ToolStripMenuItem();
            openMenuItem = new ToolStripMenuItem();
            recentMenu = new ToolStripMenuItem();
            recentAMenuItem = new ToolStripMenuItem();
            recentBMenuItem = new ToolStripMenuItem();
            fileSeparator = new ToolStripSeparator();
            exitMenuItem = new ToolStripMenuItem();
            editMenu = new ToolStripMenuItem();
            cutMenuItem = new ToolStripMenuItem();
            copyMenuItem = new ToolStripMenuItem();
            pasteMenuItem = new ToolStripMenuItem();
            viewMenu = new ToolStripMenuItem();
            wordWrapMenuItem = new ToolStripMenuItem();
            statusBarMenuItem = new ToolStripMenuItem();
            toolStrip1 = new ToolStrip();
            newButton = new ToolStripButton();
            openButton = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            boldButton = new ToolStripButton();
            italicButton = new ToolStripButton();
            toolStripSeparator2 = new ToolStripSeparator();
            zoomComboBox = new ToolStripComboBox();
            viewDropDownButton = new ToolStripDropDownButton();
            detailsMenuItem = new ToolStripMenuItem();
            tilesMenuItem = new ToolStripMenuItem();
            undoSplitButton = new ToolStripSplitButton();
            undoAllMenuItem = new ToolStripMenuItem();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            springLabel = new ToolStripStatusLabel();
            progressStatus = new ToolStripProgressBar();
            contextMenuStrip1 = new ContextMenuStrip(components);
            contextCutMenuItem = new ToolStripMenuItem();
            contextCopyMenuItem = new ToolStripMenuItem();
            editor = new TextBox();
            menuStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip1
            //
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, viewMenu });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(584, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            //
            // fileMenu
            //
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { newMenuItem, openMenuItem, recentMenu, fileSeparator, exitMenuItem });
            fileMenu.Name = "fileMenu";
            fileMenu.Text = "&File";
            //
            // newMenuItem
            //
            newMenuItem.Name = "newMenuItem";
            newMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newMenuItem.Text = "&New";
            newMenuItem.Click += fileNew_Click;
            //
            // openMenuItem
            //
            openMenuItem.Name = "openMenuItem";
            openMenuItem.ShortcutKeys = Keys.Control | Keys.O;
            openMenuItem.Text = "&Open...";
            openMenuItem.Click += fileOpen_Click;
            //
            // recentMenu
            //
            recentMenu.DropDownItems.AddRange(new ToolStripItem[] { recentAMenuItem, recentBMenuItem });
            recentMenu.Name = "recentMenu";
            recentMenu.Text = "&Recent";
            //
            // recentAMenuItem
            //
            recentAMenuItem.Name = "recentAMenuItem";
            recentAMenuItem.Text = "notes.txt";
            recentAMenuItem.Click += recent_Click;
            //
            // recentBMenuItem
            //
            recentBMenuItem.Name = "recentBMenuItem";
            recentBMenuItem.Text = "readme.md";
            recentBMenuItem.Click += recent_Click;
            //
            // fileSeparator
            //
            fileSeparator.Name = "fileSeparator";
            //
            // exitMenuItem
            //
            exitMenuItem.Name = "exitMenuItem";
            exitMenuItem.ShortcutKeys = Keys.Alt | Keys.F4;
            exitMenuItem.Text = "E&xit";
            exitMenuItem.Click += fileExit_Click;
            //
            // editMenu
            //
            editMenu.DropDownItems.AddRange(new ToolStripItem[] { cutMenuItem, copyMenuItem, pasteMenuItem });
            editMenu.Name = "editMenu";
            editMenu.Text = "&Edit";
            //
            // cutMenuItem
            //
            cutMenuItem.Name = "cutMenuItem";
            cutMenuItem.ShortcutKeys = Keys.Control | Keys.X;
            cutMenuItem.Text = "Cu&t";
            cutMenuItem.Click += editCut_Click;
            //
            // copyMenuItem
            //
            copyMenuItem.Name = "copyMenuItem";
            copyMenuItem.ShortcutKeys = Keys.Control | Keys.C;
            copyMenuItem.Text = "&Copy";
            copyMenuItem.Click += editCopy_Click;
            //
            // pasteMenuItem
            //
            pasteMenuItem.Name = "pasteMenuItem";
            pasteMenuItem.ShortcutKeys = Keys.Control | Keys.V;
            pasteMenuItem.Text = "&Paste";
            pasteMenuItem.Click += editPaste_Click;
            //
            // viewMenu
            //
            viewMenu.DropDownItems.AddRange(new ToolStripItem[] { wordWrapMenuItem, statusBarMenuItem });
            viewMenu.Name = "viewMenu";
            viewMenu.Text = "&View";
            //
            // wordWrapMenuItem
            //
            wordWrapMenuItem.Checked = true;
            wordWrapMenuItem.CheckOnClick = true;
            wordWrapMenuItem.Name = "wordWrapMenuItem";
            wordWrapMenuItem.Text = "&Word wrap";
            wordWrapMenuItem.CheckedChanged += wordWrap_CheckedChanged;
            //
            // statusBarMenuItem
            //
            statusBarMenuItem.Checked = true;
            statusBarMenuItem.CheckOnClick = true;
            statusBarMenuItem.Name = "statusBarMenuItem";
            statusBarMenuItem.Text = "&Status bar";
            statusBarMenuItem.CheckedChanged += statusBar_CheckedChanged;
            //
            // toolStrip1
            //
            toolStrip1.Items.AddRange(new ToolStripItem[]
            {
                newButton, openButton, toolStripSeparator1, boldButton, italicButton,
                toolStripSeparator2, zoomComboBox, viewDropDownButton, undoSplitButton,
            });
            toolStrip1.Location = new Point(0, 24);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(584, 25);
            toolStrip1.TabIndex = 1;
            //
            // newButton
            //
            newButton.Name = "newButton";
            newButton.Text = "New";
            newButton.ToolTipText = "New file (Ctrl+N)";
            newButton.Click += fileNew_Click;
            //
            // openButton
            //
            openButton.Name = "openButton";
            openButton.Text = "Open";
            openButton.Click += fileOpen_Click;
            //
            // toolStripSeparator1
            //
            toolStripSeparator1.Name = "toolStripSeparator1";
            //
            // boldButton
            //
            boldButton.CheckOnClick = true;
            boldButton.Name = "boldButton";
            boldButton.Text = "B";
            boldButton.CheckedChanged += style_CheckedChanged;
            //
            // italicButton
            //
            italicButton.CheckOnClick = true;
            italicButton.Name = "italicButton";
            italicButton.Text = "I";
            italicButton.CheckedChanged += style_CheckedChanged;
            //
            // toolStripSeparator2
            //
            toolStripSeparator2.Name = "toolStripSeparator2";
            //
            // zoomComboBox
            //
            zoomComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            zoomComboBox.Name = "zoomComboBox";
            zoomComboBox.SelectedIndexChanged += zoom_SelectedIndexChanged;
            //
            // viewDropDownButton
            //
            viewDropDownButton.DropDownItems.AddRange(new ToolStripItem[] { detailsMenuItem, tilesMenuItem });
            viewDropDownButton.Name = "viewDropDownButton";
            viewDropDownButton.Text = "View";
            //
            // detailsMenuItem
            //
            detailsMenuItem.Name = "detailsMenuItem";
            detailsMenuItem.Text = "Details";
            detailsMenuItem.Click += view_Click;
            //
            // tilesMenuItem
            //
            tilesMenuItem.Name = "tilesMenuItem";
            tilesMenuItem.Text = "Tiles";
            tilesMenuItem.Click += view_Click;
            //
            // undoSplitButton
            //
            undoSplitButton.DropDownItems.AddRange(new ToolStripItem[] { undoAllMenuItem });
            undoSplitButton.Name = "undoSplitButton";
            undoSplitButton.Text = "Undo";
            undoSplitButton.ButtonClick += undo_ButtonClick;
            //
            // undoAllMenuItem
            //
            undoAllMenuItem.Name = "undoAllMenuItem";
            undoAllMenuItem.Text = "Undo all";
            undoAllMenuItem.Click += undoAll_Click;
            //
            // statusStrip1
            //
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, springLabel, progressStatus });
            statusStrip1.Location = new Point(0, 318);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(584, 22);
            statusStrip1.TabIndex = 3;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Text = "Ready";
            //
            // springLabel
            //
            springLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
            springLabel.Name = "springLabel";
            springLabel.Spring = true;
            springLabel.Text = "";
            //
            // progressStatus
            //
            progressStatus.Name = "progressStatus";
            progressStatus.Value = 30;
            //
            // contextMenuStrip1
            //
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { contextCutMenuItem, contextCopyMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            //
            // contextCutMenuItem
            //
            contextCutMenuItem.Name = "contextCutMenuItem";
            contextCutMenuItem.Text = "Cut";
            contextCutMenuItem.Click += editCut_Click;
            //
            // contextCopyMenuItem
            //
            contextCopyMenuItem.Name = "contextCopyMenuItem";
            contextCopyMenuItem.Text = "Copy";
            contextCopyMenuItem.Click += editCopy_Click;
            //
            // editor
            //
            editor.ContextMenuStrip = contextMenuStrip1;
            editor.Dock = DockStyle.Fill;
            editor.Multiline = true;
            editor.Name = "editor";
            editor.ScrollBars = ScrollBars.Vertical;
            editor.TabIndex = 2;
            editor.Text = "Right-click for a context menu.\r\nAlt+F opens the File menu; Ctrl+O is a shortcut.";
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(584, 340);
            Controls.Add(editor);
            Controls.Add(toolStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(statusStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            Text = "NetForms Strips";
            menuStrip1.ResumeLayout(false);
            toolStrip1.ResumeLayout(false);
            statusStrip1.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileMenu;
        private ToolStripMenuItem newMenuItem;
        private ToolStripMenuItem openMenuItem;
        private ToolStripMenuItem recentMenu;
        private ToolStripMenuItem recentAMenuItem;
        private ToolStripMenuItem recentBMenuItem;
        private ToolStripSeparator fileSeparator;
        private ToolStripMenuItem exitMenuItem;
        private ToolStripMenuItem editMenu;
        private ToolStripMenuItem cutMenuItem;
        private ToolStripMenuItem copyMenuItem;
        private ToolStripMenuItem pasteMenuItem;
        private ToolStripMenuItem viewMenu;
        private ToolStripMenuItem wordWrapMenuItem;
        private ToolStripMenuItem statusBarMenuItem;
        private ToolStrip toolStrip1;
        private ToolStripButton newButton;
        private ToolStripButton openButton;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton boldButton;
        private ToolStripButton italicButton;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripComboBox zoomComboBox;
        private ToolStripDropDownButton viewDropDownButton;
        private ToolStripMenuItem detailsMenuItem;
        private ToolStripMenuItem tilesMenuItem;
        private ToolStripSplitButton undoSplitButton;
        private ToolStripMenuItem undoAllMenuItem;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel springLabel;
        private ToolStripProgressBar progressStatus;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem contextCutMenuItem;
        private ToolStripMenuItem contextCopyMenuItem;
        private TextBox editor;
    }
}
