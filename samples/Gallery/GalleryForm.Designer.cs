namespace Gallery
{
    partial class GalleryForm
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
            toolTip1 = new ToolTip(components);
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            tabPage2 = new TabPage();
            tabPage3 = new TabPage();
            flowLayoutPanel1 = new FlowLayoutPanel();
            tableLayoutPanel1 = new TableLayoutPanel();
            scrollPanel = new Panel();
            groupBox1 = new GroupBox();
            checkBox1 = new CheckBox();
            checkBox2 = new CheckBox();
            radioButton1 = new RadioButton();
            radioButton2 = new RadioButton();
            textBox1 = new TextBox();
            textBox2 = new TextBox();
            comboBox1 = new ComboBox();
            comboBox2 = new ComboBox();
            numericUpDown1 = new NumericUpDown();
            trackBar1 = new TrackBar();
            progressBar1 = new ProgressBar();
            button1 = new Button();
            linkLabel1 = new LinkLabel();
            listBox1 = new ListBox();
            pictureBox1 = new PictureBox();
            splitContainer1 = new SplitContainer();
            textBox3 = new TextBox();
            panel1 = new Panel();
            statusLabel = new Label();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            tabPage2.SuspendLayout();
            tabPage3.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            tableLayoutPanel1.SuspendLayout();
            scrollPanel.SuspendLayout();
            groupBox1.SuspendLayout();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            panel1.SuspendLayout();
            SuspendLayout();
            //
            // tabControl1
            //
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Controls.Add(tabPage3);
            tabControl1.Location = new Point(12, 12);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(560, 316);
            tabControl1.TabIndex = 0;
            //
            // tabPage1
            //
            tabPage1.Controls.Add(groupBox1);
            tabPage1.Controls.Add(textBox1);
            tabPage1.Controls.Add(textBox2);
            tabPage1.Controls.Add(comboBox1);
            tabPage1.Controls.Add(comboBox2);
            tabPage1.Controls.Add(numericUpDown1);
            tabPage1.Controls.Add(trackBar1);
            tabPage1.Controls.Add(progressBar1);
            tabPage1.Controls.Add(button1);
            tabPage1.Controls.Add(linkLabel1);
            tabPage1.Controls.Add(listBox1);
            tabPage1.Controls.Add(pictureBox1);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(3);
            tabPage1.Size = new Size(552, 288);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Controls";
            tabPage1.UseVisualStyleBackColor = true;
            //
            // tabPage2
            //
            tabPage2.Controls.Add(splitContainer1);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(3);
            tabPage2.Size = new Size(552, 288);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Layout";
            tabPage2.UseVisualStyleBackColor = true;
            //
            // tabPage3
            //
            tabPage3.Controls.Add(flowLayoutPanel1);
            tabPage3.Controls.Add(tableLayoutPanel1);
            tabPage3.Controls.Add(scrollPanel);
            tabPage3.Location = new Point(4, 24);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(3);
            tabPage3.Size = new Size(552, 288);
            tabPage3.TabIndex = 2;
            tabPage3.Text = "Panels";
            tabPage3.UseVisualStyleBackColor = true;
            //
            // flowLayoutPanel1
            //
            flowLayoutPanel1.BorderStyle = BorderStyle.FixedSingle;
            flowLayoutPanel1.Controls.Add(new Button { Text = "One", AutoSize = true });
            flowLayoutPanel1.Controls.Add(new Button { Text = "Two", AutoSize = true });
            flowLayoutPanel1.Controls.Add(new Button { Text = "Three", AutoSize = true });
            flowLayoutPanel1.Controls.Add(new Button { Text = "Four", AutoSize = true });
            flowLayoutPanel1.Controls.Add(new CheckBox { Text = "Wrapped", AutoSize = true });
            flowLayoutPanel1.Location = new Point(6, 6);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(260, 90);
            flowLayoutPanel1.TabIndex = 0;
            //
            // tableLayoutPanel1
            //
            tableLayoutPanel1.CellBorderStyle = TableLayoutPanelCellBorderStyle.Single;
            tableLayoutPanel1.ColumnCount = 3;
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            tableLayoutPanel1.Controls.Add(new Label { Text = "Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            tableLayoutPanel1.Controls.Add(new TextBox { Dock = DockStyle.Fill, Text = "spans two columns" }, 1, 0);
            tableLayoutPanel1.Controls.Add(new Label { Text = "Kind", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            tableLayoutPanel1.Controls.Add(new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList }, 1, 1);
            tableLayoutPanel1.Controls.Add(new Button { Text = "OK", Anchor = AnchorStyles.Right }, 2, 1);
            tableLayoutPanel1.Location = new Point(6, 102);
            tableLayoutPanel1.Name = "tableLayoutPanel1";
            tableLayoutPanel1.RowCount = 2;
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tableLayoutPanel1.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            tableLayoutPanel1.Size = new Size(260, 70);
            tableLayoutPanel1.TabIndex = 1;
            tableLayoutPanel1.SetColumnSpan(tableLayoutPanel1.GetControlFromPosition(1, 0), 2);
            //
            // scrollPanel
            //
            scrollPanel.AutoScroll = true;
            scrollPanel.BackColor = SystemColors.Window;
            scrollPanel.BorderStyle = BorderStyle.FixedSingle;
            scrollPanel.Controls.Add(new Label { Text = "Top left", Location = new Point(8, 8), AutoSize = true });
            scrollPanel.Controls.Add(new Button { Text = "Far away", Location = new Point(300, 320) });
            scrollPanel.Location = new Point(280, 6);
            scrollPanel.Name = "scrollPanel";
            scrollPanel.Size = new Size(260, 166);
            scrollPanel.TabIndex = 2;
            //
            // groupBox1
            //
            groupBox1.Controls.Add(checkBox1);
            groupBox1.Controls.Add(checkBox2);
            groupBox1.Controls.Add(radioButton1);
            groupBox1.Controls.Add(radioButton2);
            groupBox1.Location = new Point(6, 6);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(200, 120);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Text = "Options";
            //
            // checkBox1
            //
            checkBox1.AutoSize = true;
            checkBox1.Location = new Point(12, 22);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(120, 19);
            checkBox1.TabIndex = 0;
            checkBox1.Text = "Disable second box";
            checkBox1.UseVisualStyleBackColor = true;
            checkBox1.CheckedChanged += checkBox1_CheckedChanged;
            //
            // checkBox2
            //
            checkBox2.AutoSize = true;
            checkBox2.Checked = true;
            checkBox2.CheckState = CheckState.Checked;
            checkBox2.Location = new Point(12, 45);
            checkBox2.Name = "checkBox2";
            checkBox2.Size = new Size(84, 19);
            checkBox2.TabIndex = 1;
            checkBox2.Text = "Checked one";
            checkBox2.ThreeState = true;
            checkBox2.UseVisualStyleBackColor = true;
            //
            // radioButton1
            //
            radioButton1.AutoSize = true;
            radioButton1.Checked = true;
            radioButton1.Location = new Point(12, 70);
            radioButton1.Name = "radioButton1";
            radioButton1.Size = new Size(60, 19);
            radioButton1.TabIndex = 2;
            radioButton1.TabStop = true;
            radioButton1.Text = "Option A";
            radioButton1.UseVisualStyleBackColor = true;
            //
            // radioButton2
            //
            radioButton2.AutoSize = true;
            radioButton2.Location = new Point(12, 93);
            radioButton2.Name = "radioButton2";
            radioButton2.Size = new Size(60, 19);
            radioButton2.TabIndex = 3;
            radioButton2.Text = "Option B";
            radioButton2.UseVisualStyleBackColor = true;
            //
            // textBox1
            //
            textBox1.Location = new Point(212, 12);
            textBox1.Name = "textBox1";
            textBox1.PlaceholderText = "Type here";
            textBox1.Size = new Size(160, 23);
            textBox1.TabIndex = 1;
            textBox1.Text = "Hello, NetForms";
            //
            // textBox2
            //
            textBox2.Location = new Point(212, 41);
            textBox2.Name = "textBox2";
            textBox2.PasswordChar = '*';
            textBox2.Size = new Size(160, 23);
            textBox2.TabIndex = 2;
            textBox2.Text = "secret";
            //
            // comboBox1
            //
            comboBox1.FormattingEnabled = true;
            comboBox1.Location = new Point(212, 70);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(160, 23);
            comboBox1.TabIndex = 3;
            //
            // comboBox2
            //
            comboBox2.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox2.FormattingEnabled = true;
            comboBox2.Location = new Point(212, 99);
            comboBox2.Name = "comboBox2";
            comboBox2.Size = new Size(160, 23);
            comboBox2.TabIndex = 4;
            //
            // numericUpDown1
            //
            numericUpDown1.Location = new Point(212, 128);
            numericUpDown1.Maximum = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDown1.Name = "numericUpDown1";
            numericUpDown1.Size = new Size(80, 23);
            numericUpDown1.TabIndex = 5;
            numericUpDown1.Value = new decimal(new int[] { 3, 0, 0, 0 });
            numericUpDown1.ValueChanged += numericUpDown1_ValueChanged;
            //
            // trackBar1
            //
            trackBar1.Location = new Point(6, 132);
            trackBar1.Name = "trackBar1";
            trackBar1.Size = new Size(200, 45);
            trackBar1.TabIndex = 6;
            trackBar1.Value = 3;
            trackBar1.ValueChanged += trackBar1_ValueChanged;
            //
            // progressBar1
            //
            progressBar1.Location = new Point(6, 183);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(200, 23);
            progressBar1.TabIndex = 7;
            progressBar1.Value = 30;
            //
            // button1
            //
            button1.Location = new Point(212, 157);
            button1.Name = "button1";
            button1.Size = new Size(160, 23);
            button1.TabIndex = 8;
            button1.Text = "Show message";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            //
            // linkLabel1
            //
            linkLabel1.AutoSize = true;
            linkLabel1.Location = new Point(212, 190);
            linkLabel1.Name = "linkLabel1";
            linkLabel1.Size = new Size(70, 15);
            linkLabel1.TabIndex = 9;
            linkLabel1.TabStop = true;
            linkLabel1.Text = "Next tab page";
            linkLabel1.LinkClicked += linkLabel1_LinkClicked;
            //
            // listBox1
            //
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(384, 12);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(160, 109);
            listBox1.TabIndex = 10;
            listBox1.SelectedIndexChanged += listBox1_SelectedIndexChanged;
            //
            // pictureBox1
            //
            pictureBox1.BorderStyle = BorderStyle.FixedSingle;
            pictureBox1.Location = new Point(384, 128);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(160, 78);
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox1.TabIndex = 11;
            pictureBox1.TabStop = false;
            //
            // splitContainer1
            //
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(3, 3);
            splitContainer1.Name = "splitContainer1";
            //
            // splitContainer1.Panel1
            //
            splitContainer1.Panel1.Controls.Add(textBox3);
            //
            // splitContainer1.Panel2
            //
            splitContainer1.Panel2.Controls.Add(panel1);
            splitContainer1.Size = new Size(546, 282);
            splitContainer1.SplitterDistance = 180;
            splitContainer1.TabIndex = 0;
            //
            // textBox3
            //
            textBox3.Dock = DockStyle.Fill;
            textBox3.Location = new Point(0, 0);
            textBox3.Multiline = true;
            textBox3.Name = "textBox3";
            textBox3.ScrollBars = ScrollBars.Vertical;
            textBox3.Size = new Size(180, 282);
            textBox3.TabIndex = 0;
            textBox3.Text = "A multi-line text box.\r\nIt wraps long lines and scrolls when the text does not fit.";
            //
            // panel1
            //
            panel1.BorderStyle = BorderStyle.Fixed3D;
            panel1.Controls.Add(statusLabel);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(362, 282);
            panel1.TabIndex = 0;
            //
            // statusLabel
            //
            statusLabel.Dock = DockStyle.Bottom;
            statusLabel.Location = new Point(2, 257);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(358, 23);
            statusLabel.TabIndex = 0;
            statusLabel.Text = "Ready";
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // GalleryForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(584, 340);
            Controls.Add(tabControl1);
            Name = "GalleryForm";
            Text = "NetForms Gallery";
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            tabPage1.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage3.ResumeLayout(false);
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            tableLayoutPanel1.ResumeLayout(false);
            tableLayoutPanel1.PerformLayout();
            scrollPanel.ResumeLayout(false);
            scrollPanel.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel1.PerformLayout();
            splitContainer1.Panel2.ResumeLayout(false);
            splitContainer1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private ToolTip toolTip1;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private TabPage tabPage3;
        private FlowLayoutPanel flowLayoutPanel1;
        private TableLayoutPanel tableLayoutPanel1;
        private Panel scrollPanel;
        private GroupBox groupBox1;
        private CheckBox checkBox1;
        private CheckBox checkBox2;
        private RadioButton radioButton1;
        private RadioButton radioButton2;
        private TextBox textBox1;
        private TextBox textBox2;
        private ComboBox comboBox1;
        private ComboBox comboBox2;
        private NumericUpDown numericUpDown1;
        private TrackBar trackBar1;
        private ProgressBar progressBar1;
        private Button button1;
        private LinkLabel linkLabel1;
        private ListBox listBox1;
        private PictureBox pictureBox1;
        private SplitContainer splitContainer1;
        private TextBox textBox3;
        private Panel panel1;
        private Label statusLabel;
    }
}
