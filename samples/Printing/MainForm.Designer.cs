namespace Printing
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
            menuStrip1 = new MenuStrip();
            fileMenu = new ToolStripMenuItem();
            pageSetupMenuItem = new ToolStripMenuItem();
            printPreviewMenuItem = new ToolStripMenuItem();
            printMenuItem = new ToolStripMenuItem();
            fileSeparator = new ToolStripSeparator();
            exitMenuItem = new ToolStripMenuItem();
            editor = new TextBox();
            printDocument1 = new System.Drawing.Printing.PrintDocument();
            printDialog1 = new PrintDialog();
            pageSetupDialog1 = new PageSetupDialog();
            printPreviewDialog1 = new PrintPreviewDialog();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip1
            //
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenu });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(584, 24);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            //
            // fileMenu
            //
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { pageSetupMenuItem, printPreviewMenuItem, printMenuItem, fileSeparator, exitMenuItem });
            fileMenu.Name = "fileMenu";
            fileMenu.Size = new Size(37, 20);
            fileMenu.Text = "&File";
            //
            // pageSetupMenuItem
            //
            pageSetupMenuItem.Name = "pageSetupMenuItem";
            pageSetupMenuItem.Size = new Size(180, 22);
            pageSetupMenuItem.Text = "Page Set&up...";
            pageSetupMenuItem.Click += pageSetupMenuItem_Click;
            //
            // printPreviewMenuItem
            //
            printPreviewMenuItem.Name = "printPreviewMenuItem";
            printPreviewMenuItem.Size = new Size(180, 22);
            printPreviewMenuItem.Text = "Print Pre&view...";
            printPreviewMenuItem.Click += printPreviewMenuItem_Click;
            //
            // printMenuItem
            //
            printMenuItem.Name = "printMenuItem";
            printMenuItem.ShortcutKeys = Keys.Control | Keys.P;
            printMenuItem.Size = new Size(180, 22);
            printMenuItem.Text = "&Print...";
            printMenuItem.Click += printMenuItem_Click;
            //
            // fileSeparator
            //
            fileSeparator.Name = "fileSeparator";
            fileSeparator.Size = new Size(177, 6);
            //
            // exitMenuItem
            //
            exitMenuItem.Name = "exitMenuItem";
            exitMenuItem.Size = new Size(180, 22);
            exitMenuItem.Text = "E&xit";
            exitMenuItem.Click += exitMenuItem_Click;
            //
            // editor
            //
            editor.Dock = DockStyle.Fill;
            editor.Font = new Font("Consolas", 10F);
            editor.Location = new Point(0, 24);
            editor.Multiline = true;
            editor.Name = "editor";
            editor.ScrollBars = ScrollBars.Vertical;
            editor.Size = new Size(584, 337);
            editor.TabIndex = 1;
            //
            // printDocument1
            //
            printDocument1.DocumentName = "Printing sample";
            printDocument1.BeginPrint += printDocument1_BeginPrint;
            printDocument1.PrintPage += printDocument1_PrintPage;
            //
            // printDialog1
            //
            printDialog1.AllowSomePages = true;
            printDialog1.Document = printDocument1;
            printDialog1.UseEXDialog = true;
            //
            // pageSetupDialog1
            //
            pageSetupDialog1.Document = printDocument1;
            pageSetupDialog1.EnableMetric = true;
            //
            // printPreviewDialog1
            //
            printPreviewDialog1.AutoScrollMargin = new Size(0, 0);
            printPreviewDialog1.AutoScrollMinSize = new Size(0, 0);
            printPreviewDialog1.ClientSize = new Size(400, 300);
            printPreviewDialog1.Document = printDocument1;
            printPreviewDialog1.Enabled = true;
            printPreviewDialog1.Name = "printPreviewDialog1";
            printPreviewDialog1.Visible = false;
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(584, 361);
            Controls.Add(editor);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            Text = "Printing";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileMenu;
        private ToolStripMenuItem pageSetupMenuItem;
        private ToolStripMenuItem printPreviewMenuItem;
        private ToolStripMenuItem printMenuItem;
        private ToolStripSeparator fileSeparator;
        private ToolStripMenuItem exitMenuItem;
        private TextBox editor;
        private System.Drawing.Printing.PrintDocument printDocument1;
        private PrintDialog printDialog1;
        private PageSetupDialog pageSetupDialog1;
        private PrintPreviewDialog printPreviewDialog1;
    }
}
