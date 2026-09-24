namespace MdiDemo
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
            newWindowMenuItem = new ToolStripMenuItem();
            closeMenuItem = new ToolStripMenuItem();
            fileSeparator = new ToolStripSeparator();
            exitMenuItem = new ToolStripMenuItem();
            windowMenu = new ToolStripMenuItem();
            cascadeMenuItem = new ToolStripMenuItem();
            tileHorizontalMenuItem = new ToolStripMenuItem();
            tileVerticalMenuItem = new ToolStripMenuItem();
            arrangeIconsMenuItem = new ToolStripMenuItem();
            windowListSeparator = new ToolStripSeparator();
            toolStrip1 = new ToolStrip();
            newButton = new ToolStripButton();
            toolStripSeparator1 = new ToolStripSeparator();
            cascadeButton = new ToolStripButton();
            tileHorizontalButton = new ToolStripButton();
            tileVerticalButton = new ToolStripButton();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            springLabel = new ToolStripStatusLabel();
            countLabel = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip1
            //
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenu, windowMenu });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(760, 24);
            menuStrip1.TabIndex = 0;
            //
            // fileMenu
            //
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { newMenuItem, newWindowMenuItem, closeMenuItem, fileSeparator, exitMenuItem });
            fileMenu.Name = "fileMenu";
            fileMenu.Text = "&File";
            //
            // newMenuItem
            //
            newMenuItem.Name = "newMenuItem";
            newMenuItem.ShortcutKeys = Keys.Control | Keys.N;
            newMenuItem.Text = "&New document (inside)";
            newMenuItem.Click += newDocument_Click;
            //
            // newWindowMenuItem
            //
            newWindowMenuItem.Name = "newWindowMenuItem";
            newWindowMenuItem.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;
            newWindowMenuItem.Text = "New &separate window";
            newWindowMenuItem.Click += newSeparateWindow_Click;
            //
            // closeMenuItem
            //
            closeMenuItem.Name = "closeMenuItem";
            closeMenuItem.ShortcutKeys = Keys.Control | Keys.W;
            closeMenuItem.Text = "&Close document";
            closeMenuItem.Click += closeDocument_Click;
            //
            // fileSeparator
            //
            fileSeparator.Name = "fileSeparator";
            //
            // exitMenuItem
            //
            exitMenuItem.Name = "exitMenuItem";
            exitMenuItem.Text = "E&xit";
            exitMenuItem.Click += exit_Click;
            //
            // windowMenu
            //
            windowMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                cascadeMenuItem, tileHorizontalMenuItem, tileVerticalMenuItem, arrangeIconsMenuItem, windowListSeparator,
            });
            windowMenu.Name = "windowMenu";
            windowMenu.Text = "&Window";
            windowMenu.DropDownOpening += windowMenu_DropDownOpening;
            //
            // cascadeMenuItem
            //
            cascadeMenuItem.Name = "cascadeMenuItem";
            cascadeMenuItem.Text = "&Cascade";
            cascadeMenuItem.Click += cascade_Click;
            //
            // tileHorizontalMenuItem
            //
            tileHorizontalMenuItem.Name = "tileHorizontalMenuItem";
            tileHorizontalMenuItem.Text = "Tile &Horizontal";
            tileHorizontalMenuItem.Click += tileHorizontal_Click;
            //
            // tileVerticalMenuItem
            //
            tileVerticalMenuItem.Name = "tileVerticalMenuItem";
            tileVerticalMenuItem.Text = "Tile &Vertical";
            tileVerticalMenuItem.Click += tileVertical_Click;
            //
            // arrangeIconsMenuItem
            //
            arrangeIconsMenuItem.Name = "arrangeIconsMenuItem";
            arrangeIconsMenuItem.Text = "&Arrange Icons";
            arrangeIconsMenuItem.Click += arrangeIcons_Click;
            //
            // windowListSeparator
            //
            windowListSeparator.Name = "windowListSeparator";
            //
            // toolStrip1
            //
            toolStrip1.Items.AddRange(new ToolStripItem[]
            {
                newButton, toolStripSeparator1, cascadeButton, tileHorizontalButton, tileVerticalButton,
            });
            toolStrip1.Location = new Point(0, 24);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(760, 25);
            toolStrip1.TabIndex = 1;
            //
            // newButton
            //
            newButton.Name = "newButton";
            newButton.Text = "New";
            newButton.ToolTipText = "New document (Ctrl+N)";
            newButton.Click += newDocument_Click;
            //
            // toolStripSeparator1
            //
            toolStripSeparator1.Name = "toolStripSeparator1";
            //
            // cascadeButton
            //
            cascadeButton.Name = "cascadeButton";
            cascadeButton.Text = "Cascade";
            cascadeButton.Click += cascade_Click;
            //
            // tileHorizontalButton
            //
            tileHorizontalButton.Name = "tileHorizontalButton";
            tileHorizontalButton.Text = "Tile H";
            tileHorizontalButton.Click += tileHorizontal_Click;
            //
            // tileVerticalButton
            //
            tileVerticalButton.Name = "tileVerticalButton";
            tileVerticalButton.Text = "Tile V";
            tileVerticalButton.Click += tileVertical_Click;
            //
            // statusStrip1
            //
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, springLabel, countLabel });
            statusStrip1.Location = new Point(0, 458);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(760, 22);
            statusStrip1.TabIndex = 2;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Text = "Ready";
            //
            // springLabel
            //
            springLabel.Name = "springLabel";
            springLabel.Spring = true;
            springLabel.Text = "";
            //
            // countLabel
            //
            countLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
            countLabel.Name = "countLabel";
            countLabel.Text = "0 documents";
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(760, 480);
            IsMdiContainer = true;
            Controls.Add(toolStrip1);
            Controls.Add(menuStrip1);
            Controls.Add(statusStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            Text = "NetForms MDI";
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
        private ToolStripMenuItem newWindowMenuItem;
        private ToolStripMenuItem closeMenuItem;
        private ToolStripSeparator fileSeparator;
        private ToolStripMenuItem exitMenuItem;
        private ToolStripMenuItem windowMenu;
        private ToolStripMenuItem cascadeMenuItem;
        private ToolStripMenuItem tileHorizontalMenuItem;
        private ToolStripMenuItem tileVerticalMenuItem;
        private ToolStripMenuItem arrangeIconsMenuItem;
        private ToolStripSeparator windowListSeparator;
        private ToolStrip toolStrip1;
        private ToolStripButton newButton;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripButton cascadeButton;
        private ToolStripButton tileHorizontalButton;
        private ToolStripButton tileVerticalButton;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel springLabel;
        private ToolStripStatusLabel countLabel;
    }
}
