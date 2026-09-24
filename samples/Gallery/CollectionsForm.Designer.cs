namespace Gallery
{
    partial class CollectionsForm
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
            TreeNode treeNode1 = new TreeNode("Child");
            TreeNode treeNode2 = new TreeNode("Root", new TreeNode[] { treeNode1 });
            TreeNode treeNode3 = new TreeNode("Second");
            ListViewGroup listViewGroup1 = new ListViewGroup("Fruit", HorizontalAlignment.Left);
            ListViewItem listViewItem1 = new ListViewItem("Apple");
            ListViewItem listViewItem2 = new ListViewItem(new string[] { "Pear", "green" }, -1);
            ListViewItem listViewItem3 = new ListViewItem(new ListViewItem.ListViewSubItem[] { new ListViewItem.ListViewSubItem(null, "Plum"), new ListViewItem.ListViewSubItem(null, "red", Color.Red, SystemColors.Window, new Font("Segoe UI", 9F)) }, -1);
            ListViewItem listViewItem4 = new ListViewItem(new string[] { "Kiwi", "x" }, -1, Color.Green, Color.Empty, null);
            ListViewItem listViewItem5 = new ListViewItem("Fig", "fig");
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            treeView1 = new TreeView();
            listView1 = new ListView();
            dataGridView1 = new DataGridView();
            richTextBox1 = new RichTextBox();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            //
            // treeView1
            //
            treeView1.Location = new Point(12, 12);
            treeView1.Name = "treeView1";
            treeNode1.Name = "Child";
            treeNode1.Text = "Child";
            treeNode2.Name = "Root";
            treeNode2.Text = "Root";
            treeNode3.Name = "Second";
            treeNode3.Text = "Second";
            treeView1.Nodes.AddRange(new TreeNode[] { treeNode2, treeNode3 });
            treeView1.Size = new Size(121, 97);
            treeView1.TabIndex = 0;
            //
            // listView1
            //
            listViewGroup1.Header = "Fruit";
            listViewGroup1.Name = "listViewGroup1";
            listView1.Groups.AddRange(new ListViewGroup[] { listViewGroup1 });
            listViewItem1.Group = listViewGroup1;
            listViewItem2.Group = listViewGroup1;
            listViewItem3.UseItemStyleForSubItems = false;
            listView1.Items.AddRange(new ListViewItem[] { listViewItem1, listViewItem2, listViewItem3, listViewItem4, listViewItem5 });
            listView1.Location = new Point(151, 12);
            listView1.Name = "listView1";
            listView1.Size = new Size(121, 97);
            listView1.TabIndex = 1;
            listView1.UseCompatibleStateImageBehavior = false;
            //
            // dataGridView1
            //
            dataGridViewCellStyle1.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = Color.Yellow;
            dataGridViewCellStyle1.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle1.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = DataGridViewTriState.True;
            dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            dataGridView1.Location = new Point(12, 121);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.Size = new Size(260, 128);
            dataGridView1.TabIndex = 2;
            //
            // richTextBox1
            //
            richTextBox1.Location = new Point(12, 261);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.ReadOnly = true;
            richTextBox1.ScrollBars = RichTextBoxScrollBars.Vertical;
            richTextBox1.Size = new Size(260, 60);
            richTextBox1.TabIndex = 3;
            richTextBox1.Text = "";
            //
            // CollectionsForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(284, 333);
            Controls.Add(richTextBox1);
            Controls.Add(dataGridView1);
            Controls.Add(listView1);
            Controls.Add(treeView1);
            Name = "CollectionsForm";
            Text = "Collections";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private TreeView treeView1;
        private ListView listView1;
        private DataGridView dataGridView1;
        private RichTextBox richTextBox1;
    }
}
