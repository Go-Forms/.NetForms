namespace Gallery
{
    partial class ColumnsForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            dataGridView1 = new DataGridView();
            Column1 = new DataGridViewTextBoxColumn();
            Column2 = new DataGridViewCheckBoxColumn();
            listView1 = new ListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            //
            // dataGridView1
            //
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { Column1, Column2 });
            dataGridView1.Location = new Point(12, 12);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(260, 120);
            dataGridView1.TabIndex = 0;
            //
            // Column1
            //
            Column1.HeaderText = "Name";
            Column1.Name = "Column1";
            //
            // Column2
            //
            Column2.HeaderText = "Done";
            Column2.Name = "Column2";
            Column2.Width = 60;
            //
            // listView1
            //
            listView1.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2 });
            listView1.Location = new Point(12, 140);
            listView1.Name = "listView1";
            listView1.Size = new Size(260, 97);
            listView1.TabIndex = 1;
            listView1.UseCompatibleStateImageBehavior = false;
            listView1.View = View.Details;
            //
            // columnHeader1
            //
            columnHeader1.Name = "columnHeader1";
            columnHeader1.Text = "Name";
            columnHeader1.Width = 120;
            //
            // columnHeader2
            //
            columnHeader2.Name = "columnHeader2";
            columnHeader2.Text = "Size";
            columnHeader2.TextAlign = HorizontalAlignment.Right;
            //
            // ColumnsForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(284, 250);
            Controls.Add(listView1);
            Controls.Add(dataGridView1);
            Name = "ColumnsForm";
            Text = "Columns";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView dataGridView1;
        private DataGridViewTextBoxColumn Column1;
        private DataGridViewCheckBoxColumn Column2;
        private ListView listView1;
        private ColumnHeader columnHeader1;
        private ColumnHeader columnHeader2;
    }
}
