namespace Sklad
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
            fileToolStripMenuItem = new ToolStripMenuItem();
            docsToolStripMenuItem = new ToolStripMenuItem();
            refsToolStripMenuItem = new ToolStripMenuItem();
            reportsToolStripMenuItem = new ToolStripMenuItem();
            serviceToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            panelTop = new Panel();
            btnPrihod = new Button();
            btnRashod = new Button();
            btnOstatki = new Button();
            btnPrint = new Button();
            lblWarehouse = new Label();
            cmbWarehouse = new ComboBox();
            tabControl1 = new TabControl();
            tabItems = new TabPage();
            gridItems = new DataGridView();
            colCode = new DataGridViewTextBoxColumn();
            colName = new DataGridViewTextBoxColumn();
            colUnit = new DataGridViewTextBoxColumn();
            colPrice = new DataGridViewTextBoxColumn();
            colQty = new DataGridViewTextBoxColumn();
            colPlace = new DataGridViewTextBoxColumn();
            tabPrihod = new TabPage();
            gridPrihod = new DataGridView();
            tabRashod = new TabPage();
            gridRashod = new DataGridView();
            tabOstatki = new TabPage();
            gridOstatki = new DataGridView();
            tabPartners = new TabPage();
            gridPartners = new DataGridView();
            tabReports = new TabPage();
            gridReports = new DataGridView();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            userLabel = new ToolStripStatusLabel();
            baseLabel = new ToolStripStatusLabel();
            menuStrip1.SuspendLayout();
            panelTop.SuspendLayout();
            tabControl1.SuspendLayout();
            tabItems.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridItems).BeginInit();
            tabPrihod.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridPrihod).BeginInit();
            tabRashod.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridRashod).BeginInit();
            tabOstatki.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridOstatki).BeginInit();
            tabPartners.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridPartners).BeginInit();
            tabReports.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridReports).BeginInit();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            //
            // menuStrip1
            //
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, docsToolStripMenuItem, refsToolStripMenuItem, reportsToolStripMenuItem, serviceToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(960, 24);
            menuStrip1.TabIndex = 0;
            //
            // fileToolStripMenuItem
            //
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Text = "&Файл";
            //
            // docsToolStripMenuItem
            //
            docsToolStripMenuItem.Name = "docsToolStripMenuItem";
            docsToolStripMenuItem.Text = "&Документы";
            //
            // refsToolStripMenuItem
            //
            refsToolStripMenuItem.Name = "refsToolStripMenuItem";
            refsToolStripMenuItem.Text = "&Справочники";
            //
            // reportsToolStripMenuItem
            //
            reportsToolStripMenuItem.Name = "reportsToolStripMenuItem";
            reportsToolStripMenuItem.Text = "&Отчёты";
            //
            // serviceToolStripMenuItem
            //
            serviceToolStripMenuItem.Name = "serviceToolStripMenuItem";
            serviceToolStripMenuItem.Text = "С&ервис";
            //
            // helpToolStripMenuItem
            //
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Text = "Спр&авка";
            //
            // panelTop
            //
            panelTop.Controls.Add(btnPrihod);
            panelTop.Controls.Add(btnRashod);
            panelTop.Controls.Add(btnOstatki);
            panelTop.Controls.Add(btnPrint);
            panelTop.Controls.Add(lblWarehouse);
            panelTop.Controls.Add(cmbWarehouse);
            panelTop.Dock = DockStyle.Top;
            panelTop.Location = new Point(0, 24);
            panelTop.Name = "panelTop";
            panelTop.Size = new Size(960, 46);
            panelTop.TabIndex = 1;
            //
            // btnPrihod
            //
            btnPrihod.Location = new Point(10, 9);
            btnPrihod.Name = "btnPrihod";
            btnPrihod.Size = new Size(110, 28);
            btnPrihod.TabIndex = 0;
            btnPrihod.Text = "Приход";
            btnPrihod.UseVisualStyleBackColor = true;
            btnPrihod.Click += btnPrihod_Click;
            //
            // btnRashod
            //
            btnRashod.Location = new Point(126, 9);
            btnRashod.Name = "btnRashod";
            btnRashod.Size = new Size(110, 28);
            btnRashod.TabIndex = 1;
            btnRashod.Text = "Расход";
            btnRashod.UseVisualStyleBackColor = true;
            btnRashod.Click += btnRashod_Click;
            //
            // btnOstatki
            //
            btnOstatki.Location = new Point(242, 9);
            btnOstatki.Name = "btnOstatki";
            btnOstatki.Size = new Size(110, 28);
            btnOstatki.TabIndex = 2;
            btnOstatki.Text = "Остатки";
            btnOstatki.UseVisualStyleBackColor = true;
            btnOstatki.Click += btnOstatki_Click;
            //
            // btnPrint
            //
            btnPrint.Location = new Point(368, 9);
            btnPrint.Name = "btnPrint";
            btnPrint.Size = new Size(110, 28);
            btnPrint.TabIndex = 3;
            btnPrint.Text = "Печать…";
            btnPrint.UseVisualStyleBackColor = true;
            //
            // lblWarehouse
            //
            lblWarehouse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            lblWarehouse.AutoSize = true;
            lblWarehouse.Location = new Point(706, 14);
            lblWarehouse.Name = "lblWarehouse";
            lblWarehouse.Size = new Size(45, 15);
            lblWarehouse.TabIndex = 4;
            lblWarehouse.Text = "Склад:";
            //
            // cmbWarehouse
            //
            cmbWarehouse.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cmbWarehouse.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbWarehouse.Items.AddRange(new object[] { "Основной склад", "Склад №2 (Промзона)" });
            cmbWarehouse.Location = new Point(758, 11);
            cmbWarehouse.Name = "cmbWarehouse";
            cmbWarehouse.Size = new Size(190, 23);
            cmbWarehouse.TabIndex = 5;
            //
            // tabControl1
            //
            tabControl1.Controls.Add(tabItems);
            tabControl1.Controls.Add(tabPrihod);
            tabControl1.Controls.Add(tabRashod);
            tabControl1.Controls.Add(tabOstatki);
            tabControl1.Controls.Add(tabPartners);
            tabControl1.Controls.Add(tabReports);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 70);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(960, 508);
            tabControl1.TabIndex = 2;
            //
            // tabItems
            //
            tabItems.Controls.Add(gridItems);
            tabItems.Location = new Point(4, 24);
            tabItems.Name = "tabItems";
            tabItems.Padding = new Padding(3);
            tabItems.Size = new Size(952, 480);
            tabItems.TabIndex = 0;
            tabItems.Text = "Номенклатура";
            tabItems.UseVisualStyleBackColor = true;
            //
            // gridItems
            //
            gridItems.AllowUserToAddRows = false;
            gridItems.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridItems.Columns.AddRange(new DataGridViewColumn[] { colCode, colName, colUnit, colPrice, colQty, colPlace });
            gridItems.Dock = DockStyle.Fill;
            gridItems.Location = new Point(3, 3);
            gridItems.Name = "gridItems";
            gridItems.ReadOnly = true;
            gridItems.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridItems.Size = new Size(946, 474);
            gridItems.TabIndex = 0;
            //
            // colCode
            //
            colCode.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colCode.HeaderText = "Код";
            colCode.Name = "colCode";
            colCode.Width = 90;
            //
            // colName
            //
            colName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colName.HeaderText = "Наименование";
            colName.Name = "colName";
            colName.Width = 330;
            //
            // colUnit
            //
            colUnit.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colUnit.HeaderText = "Ед.";
            colUnit.Name = "colUnit";
            colUnit.Width = 60;
            //
            // colPrice
            //
            colPrice.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colPrice.HeaderText = "Цена, руб.";
            colPrice.Name = "colPrice";
            colPrice.Width = 110;
            //
            // colQty
            //
            colQty.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colQty.HeaderText = "Остаток";
            colQty.Name = "colQty";
            colQty.Width = 100;
            //
            // colPlace
            //
            colPlace.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            colPlace.HeaderText = "Ячейка";
            colPlace.Name = "colPlace";
            colPlace.Width = 110;
            //
            // tabPrihod
            //
            tabPrihod.Controls.Add(gridPrihod);
            tabPrihod.Location = new Point(4, 24);
            tabPrihod.Name = "tabPrihod";
            tabPrihod.Padding = new Padding(3);
            tabPrihod.Size = new Size(952, 480);
            tabPrihod.TabIndex = 1;
            tabPrihod.Text = "Приход";
            tabPrihod.UseVisualStyleBackColor = true;
            //
            // gridPrihod
            //
            gridPrihod.AllowUserToAddRows = false;
            gridPrihod.Dock = DockStyle.Fill;
            gridPrihod.Location = new Point(3, 3);
            gridPrihod.Name = "gridPrihod";
            gridPrihod.ReadOnly = true;
            gridPrihod.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridPrihod.Size = new Size(946, 474);
            gridPrihod.TabIndex = 0;
            //
            // tabRashod
            //
            tabRashod.Controls.Add(gridRashod);
            tabRashod.Location = new Point(4, 24);
            tabRashod.Name = "tabRashod";
            tabRashod.Padding = new Padding(3);
            tabRashod.Size = new Size(952, 480);
            tabRashod.TabIndex = 2;
            tabRashod.Text = "Расход";
            tabRashod.UseVisualStyleBackColor = true;
            //
            // gridRashod
            //
            gridRashod.AllowUserToAddRows = false;
            gridRashod.Dock = DockStyle.Fill;
            gridRashod.Location = new Point(3, 3);
            gridRashod.Name = "gridRashod";
            gridRashod.ReadOnly = true;
            gridRashod.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridRashod.Size = new Size(946, 474);
            gridRashod.TabIndex = 0;
            //
            // tabOstatki
            //
            tabOstatki.Controls.Add(gridOstatki);
            tabOstatki.Location = new Point(4, 24);
            tabOstatki.Name = "tabOstatki";
            tabOstatki.Padding = new Padding(3);
            tabOstatki.Size = new Size(952, 480);
            tabOstatki.TabIndex = 3;
            tabOstatki.Text = "Остатки";
            tabOstatki.UseVisualStyleBackColor = true;
            //
            // gridOstatki
            //
            gridOstatki.AllowUserToAddRows = false;
            gridOstatki.Dock = DockStyle.Fill;
            gridOstatki.Location = new Point(3, 3);
            gridOstatki.Name = "gridOstatki";
            gridOstatki.ReadOnly = true;
            gridOstatki.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridOstatki.Size = new Size(946, 474);
            gridOstatki.TabIndex = 0;
            //
            // tabPartners
            //
            tabPartners.Controls.Add(gridPartners);
            tabPartners.Location = new Point(4, 24);
            tabPartners.Name = "tabPartners";
            tabPartners.Padding = new Padding(3);
            tabPartners.Size = new Size(952, 480);
            tabPartners.TabIndex = 4;
            tabPartners.Text = "Контрагенты";
            tabPartners.UseVisualStyleBackColor = true;
            //
            // gridPartners
            //
            gridPartners.AllowUserToAddRows = false;
            gridPartners.Dock = DockStyle.Fill;
            gridPartners.Location = new Point(3, 3);
            gridPartners.Name = "gridPartners";
            gridPartners.ReadOnly = true;
            gridPartners.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridPartners.Size = new Size(946, 474);
            gridPartners.TabIndex = 0;
            //
            // tabReports
            //
            tabReports.Controls.Add(gridReports);
            tabReports.Location = new Point(4, 24);
            tabReports.Name = "tabReports";
            tabReports.Padding = new Padding(3);
            tabReports.Size = new Size(952, 480);
            tabReports.TabIndex = 5;
            tabReports.Text = "Отчёты";
            tabReports.UseVisualStyleBackColor = true;
            //
            // gridReports
            //
            gridReports.AllowUserToAddRows = false;
            gridReports.Dock = DockStyle.Fill;
            gridReports.Location = new Point(3, 3);
            gridReports.Name = "gridReports";
            gridReports.ReadOnly = true;
            gridReports.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridReports.Size = new Size(946, 474);
            gridReports.TabIndex = 0;
            //
            // statusStrip1
            //
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, userLabel, baseLabel });
            statusStrip1.Location = new Point(0, 578);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(960, 22);
            statusStrip1.TabIndex = 3;
            //
            // statusLabel
            //
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(620, 17);
            statusLabel.Spring = true;
            statusLabel.Text = "Готово";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // userLabel
            //
            userLabel.Name = "userLabel";
            userLabel.Size = new Size(190, 17);
            userLabel.Text = "Пользователь: Марина Петровна";
            //
            // baseLabel
            //
            baseLabel.Name = "baseLabel";
            baseLabel.Size = new Size(95, 17);
            baseLabel.Text = "База: sklad.mdb";
            //
            // MainForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(960, 600);
            Controls.Add(tabControl1);
            Controls.Add(panelTop);
            Controls.Add(statusStrip1);
            Controls.Add(menuStrip1);
            KeyPreview = true;
            MainMenuStrip = menuStrip1;
            Name = "MainForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Склад — Основной склад";
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            panelTop.ResumeLayout(false);
            panelTop.PerformLayout();
            tabControl1.ResumeLayout(false);
            tabItems.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridItems).EndInit();
            tabPrihod.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridPrihod).EndInit();
            tabRashod.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridRashod).EndInit();
            tabOstatki.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridOstatki).EndInit();
            tabPartners.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridPartners).EndInit();
            tabReports.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridReports).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private MenuStrip menuStrip1;
        private ToolStripMenuItem fileToolStripMenuItem;
        private ToolStripMenuItem docsToolStripMenuItem;
        private ToolStripMenuItem refsToolStripMenuItem;
        private ToolStripMenuItem reportsToolStripMenuItem;
        private ToolStripMenuItem serviceToolStripMenuItem;
        private ToolStripMenuItem helpToolStripMenuItem;
        private Panel panelTop;
        private Button btnPrihod;
        private Button btnRashod;
        private Button btnOstatki;
        private Button btnPrint;
        private Label lblWarehouse;
        private ComboBox cmbWarehouse;
        private TabControl tabControl1;
        private TabPage tabItems;
        private DataGridView gridItems;
        private DataGridViewTextBoxColumn colCode;
        private DataGridViewTextBoxColumn colName;
        private DataGridViewTextBoxColumn colUnit;
        private DataGridViewTextBoxColumn colPrice;
        private DataGridViewTextBoxColumn colQty;
        private DataGridViewTextBoxColumn colPlace;
        private TabPage tabPrihod;
        private DataGridView gridPrihod;
        private TabPage tabRashod;
        private DataGridView gridRashod;
        private TabPage tabOstatki;
        private DataGridView gridOstatki;
        private TabPage tabPartners;
        private DataGridView gridPartners;
        private TabPage tabReports;
        private DataGridView gridReports;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel userLabel;
        private ToolStripStatusLabel baseLabel;
    }
}
