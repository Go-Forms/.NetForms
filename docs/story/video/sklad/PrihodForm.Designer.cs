namespace Sklad
{
    partial class PrihodForm
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
            lblNumber = new Label();
            txtNumber = new TextBox();
            lblDate = new Label();
            dtpDate = new DateTimePicker();
            lblSupplier = new Label();
            cmbSupplier = new ComboBox();
            lblSearch = new Label();
            txtSearch = new TextBox();
            gridGoods = new DataGridView();
            colCode = new DataGridViewTextBoxColumn();
            colName = new DataGridViewTextBoxColumn();
            colUnit = new DataGridViewTextBoxColumn();
            colPrice = new DataGridViewTextBoxColumn();
            lblQty = new Label();
            numQty = new NumericUpDown();
            btnSave = new Button();
            btnCancel = new Button();
            ((System.ComponentModel.ISupportInitialize)gridGoods).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numQty).BeginInit();
            SuspendLayout();
            //
            // lblNumber
            //
            lblNumber.AutoSize = true;
            lblNumber.Location = new Point(12, 15);
            lblNumber.Name = "lblNumber";
            lblNumber.Size = new Size(76, 15);
            lblNumber.TabIndex = 0;
            lblNumber.Text = "Документ №";
            //
            // txtNumber
            //
            txtNumber.Location = new Point(100, 12);
            txtNumber.Name = "txtNumber";
            txtNumber.ReadOnly = true;
            txtNumber.Size = new Size(80, 23);
            txtNumber.TabIndex = 1;
            //
            // lblDate
            //
            lblDate.AutoSize = true;
            lblDate.Location = new Point(196, 15);
            lblDate.Name = "lblDate";
            lblDate.Size = new Size(20, 15);
            lblDate.TabIndex = 2;
            lblDate.Text = "от";
            //
            // dtpDate
            //
            dtpDate.Format = DateTimePickerFormat.Short;
            dtpDate.Location = new Point(224, 12);
            dtpDate.Name = "dtpDate";
            dtpDate.Size = new Size(120, 23);
            dtpDate.TabIndex = 3;
            dtpDate.Value = new DateTime(2026, 9, 22, 0, 0, 0, 0);
            //
            // lblSupplier
            //
            lblSupplier.AutoSize = true;
            lblSupplier.Location = new Point(12, 47);
            lblSupplier.Name = "lblSupplier";
            lblSupplier.Size = new Size(72, 15);
            lblSupplier.TabIndex = 4;
            lblSupplier.Text = "Поставщик:";
            //
            // cmbSupplier
            //
            cmbSupplier.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbSupplier.Items.AddRange(new object[] { "ООО «Канцторг»", "ИП Сафонов А. В.", "ООО «Упаковка-Сервис»", "АО «ОфисСнаб»" });
            cmbSupplier.Location = new Point(100, 44);
            cmbSupplier.Name = "cmbSupplier";
            cmbSupplier.Size = new Size(244, 23);
            cmbSupplier.TabIndex = 5;
            //
            // lblSearch
            //
            lblSearch.AutoSize = true;
            lblSearch.Location = new Point(12, 83);
            lblSearch.Name = "lblSearch";
            lblSearch.Size = new Size(43, 15);
            lblSearch.TabIndex = 6;
            lblSearch.Text = "Найти:";
            //
            // txtSearch
            //
            txtSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSearch.Location = new Point(100, 80);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(508, 23);
            txtSearch.TabIndex = 7;
            txtSearch.TextChanged += txtSearch_TextChanged;
            //
            // gridGoods
            //
            gridGoods.AllowUserToAddRows = false;
            gridGoods.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            gridGoods.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gridGoods.Columns.AddRange(new DataGridViewColumn[] { colCode, colName, colUnit, colPrice });
            gridGoods.Location = new Point(12, 112);
            gridGoods.MultiSelect = false;
            gridGoods.Name = "gridGoods";
            gridGoods.ReadOnly = true;
            gridGoods.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridGoods.Size = new Size(596, 216);
            gridGoods.TabIndex = 8;
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
            colName.Width = 290;
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
            colPrice.Width = 100;
            //
            // lblQty
            //
            lblQty.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            lblQty.AutoSize = true;
            lblQty.Location = new Point(12, 345);
            lblQty.Name = "lblQty";
            lblQty.Size = new Size(75, 15);
            lblQty.TabIndex = 9;
            lblQty.Text = "Количество:";
            //
            // numQty
            //
            numQty.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            numQty.Location = new Point(100, 342);
            numQty.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
            numQty.Name = "numQty";
            numQty.Size = new Size(100, 23);
            numQty.TabIndex = 10;
            //
            // btnSave
            //
            btnSave.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnSave.Location = new Point(380, 338);
            btnSave.Name = "btnSave";
            btnSave.Size = new Size(130, 30);
            btnSave.TabIndex = 11;
            btnSave.Text = "Сохранить (F2)";
            btnSave.UseVisualStyleBackColor = true;
            btnSave.Click += btnSave_Click;
            //
            // btnCancel
            //
            btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnCancel.DialogResult = DialogResult.Cancel;
            btnCancel.Location = new Point(518, 338);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(90, 30);
            btnCancel.TabIndex = 12;
            btnCancel.Text = "Отмена";
            btnCancel.UseVisualStyleBackColor = true;
            //
            // PrihodForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new Size(620, 380);
            Controls.Add(lblNumber);
            Controls.Add(txtNumber);
            Controls.Add(lblDate);
            Controls.Add(dtpDate);
            Controls.Add(lblSupplier);
            Controls.Add(cmbSupplier);
            Controls.Add(lblSearch);
            Controls.Add(txtSearch);
            Controls.Add(gridGoods);
            Controls.Add(lblQty);
            Controls.Add(numQty);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "PrihodForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Приход товара";
            KeyDown += PrihodForm_KeyDown;
            ((System.ComponentModel.ISupportInitialize)gridGoods).EndInit();
            ((System.ComponentModel.ISupportInitialize)numQty).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblNumber;
        private TextBox txtNumber;
        private Label lblDate;
        private DateTimePicker dtpDate;
        private Label lblSupplier;
        private ComboBox cmbSupplier;
        private Label lblSearch;
        private TextBox txtSearch;
        private DataGridView gridGoods;
        private DataGridViewTextBoxColumn colCode;
        private DataGridViewTextBoxColumn colName;
        private DataGridViewTextBoxColumn colUnit;
        private DataGridViewTextBoxColumn colPrice;
        private Label lblQty;
        private NumericUpDown numQty;
        private Button btnSave;
        private Button btnCancel;
    }
}
