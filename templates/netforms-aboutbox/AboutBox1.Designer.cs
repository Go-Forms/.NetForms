namespace NetFormsNamespace
{
    partial class AboutBox1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private Label labelProductName;
        private Label labelVersion;
        private Label labelCopyright;
        private Label labelCompanyName;
        private TextBox textBoxDescription;
        private Button okButton;

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
            labelProductName = new Label();
            labelVersion = new Label();
            labelCopyright = new Label();
            labelCompanyName = new Label();
            textBoxDescription = new TextBox();
            okButton = new Button();
            SuspendLayout();
            //
            // labelProductName
            //
            labelProductName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelProductName.Location = new Point(12, 12);
            labelProductName.Name = "labelProductName";
            labelProductName.Size = new Size(410, 20);
            labelProductName.TabIndex = 0;
            labelProductName.Text = "Product Name";
            labelProductName.TextAlign = ContentAlignment.MiddleLeft;
            //
            // labelVersion
            //
            labelVersion.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelVersion.Location = new Point(12, 38);
            labelVersion.Name = "labelVersion";
            labelVersion.Size = new Size(410, 20);
            labelVersion.TabIndex = 1;
            labelVersion.Text = "Version";
            labelVersion.TextAlign = ContentAlignment.MiddleLeft;
            //
            // labelCopyright
            //
            labelCopyright.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelCopyright.Location = new Point(12, 64);
            labelCopyright.Name = "labelCopyright";
            labelCopyright.Size = new Size(410, 20);
            labelCopyright.TabIndex = 2;
            labelCopyright.Text = "Copyright";
            labelCopyright.TextAlign = ContentAlignment.MiddleLeft;
            //
            // labelCompanyName
            //
            labelCompanyName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelCompanyName.Location = new Point(12, 90);
            labelCompanyName.Name = "labelCompanyName";
            labelCompanyName.Size = new Size(410, 20);
            labelCompanyName.TabIndex = 3;
            labelCompanyName.Text = "Company Name";
            labelCompanyName.TextAlign = ContentAlignment.MiddleLeft;
            //
            // textBoxDescription
            //
            textBoxDescription.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxDescription.Location = new Point(12, 116);
            textBoxDescription.Multiline = true;
            textBoxDescription.Name = "textBoxDescription";
            textBoxDescription.ReadOnly = true;
            textBoxDescription.ScrollBars = ScrollBars.Vertical;
            textBoxDescription.Size = new Size(410, 98);
            textBoxDescription.TabIndex = 4;
            textBoxDescription.TabStop = false;
            textBoxDescription.Text = "Description";
            //
            // okButton
            //
            okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            okButton.DialogResult = DialogResult.Cancel;
            okButton.Location = new Point(347, 226);
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.TabIndex = 5;
            okButton.Text = "&OK";
            okButton.UseVisualStyleBackColor = true;
            //
            // AboutBox1
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AcceptButton = okButton;
            CancelButton = okButton;
            ClientSize = new Size(434, 261);
            Controls.Add(okButton);
            Controls.Add(textBoxDescription);
            Controls.Add(labelCompanyName);
            Controls.Add(labelCopyright);
            Controls.Add(labelVersion);
            Controls.Add(labelProductName);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "AboutBox1";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "About";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
