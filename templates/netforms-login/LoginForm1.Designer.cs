namespace NetFormsNamespace
{
    partial class LoginForm1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private Label userNameLabel;
        private TextBox userNameTextBox;
        private Label passwordLabel;
        private TextBox passwordTextBox;
        private Button okButton;
        private Button cancelButton;

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
            userNameLabel = new Label();
            userNameTextBox = new TextBox();
            passwordLabel = new Label();
            passwordTextBox = new TextBox();
            okButton = new Button();
            cancelButton = new Button();
            SuspendLayout();
            //
            // userNameLabel
            //
            userNameLabel.Location = new Point(12, 12);
            userNameLabel.Name = "userNameLabel";
            userNameLabel.Size = new Size(90, 23);
            userNameLabel.TabIndex = 0;
            userNameLabel.Text = "&User name:";
            userNameLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // userNameTextBox
            //
            userNameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            userNameTextBox.Location = new Point(108, 12);
            userNameTextBox.Name = "userNameTextBox";
            userNameTextBox.Size = new Size(214, 23);
            userNameTextBox.TabIndex = 1;
            //
            // passwordLabel
            //
            passwordLabel.Location = new Point(12, 41);
            passwordLabel.Name = "passwordLabel";
            passwordLabel.Size = new Size(90, 23);
            passwordLabel.TabIndex = 2;
            passwordLabel.Text = "&Password:";
            passwordLabel.TextAlign = ContentAlignment.MiddleLeft;
            //
            // passwordTextBox
            //
            passwordTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            passwordTextBox.Location = new Point(108, 41);
            passwordTextBox.Name = "passwordTextBox";
            passwordTextBox.Size = new Size(214, 23);
            passwordTextBox.TabIndex = 3;
            passwordTextBox.UseSystemPasswordChar = true;
            //
            // okButton
            //
            okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            okButton.DialogResult = DialogResult.OK;
            okButton.Location = new Point(166, 76);
            okButton.Name = "okButton";
            okButton.Size = new Size(75, 23);
            okButton.TabIndex = 4;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            //
            // cancelButton
            //
            cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            cancelButton.DialogResult = DialogResult.Cancel;
            cancelButton.Location = new Point(247, 76);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(75, 23);
            cancelButton.TabIndex = 5;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            //
            // LoginForm1
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AcceptButton = okButton;
            CancelButton = cancelButton;
            ClientSize = new Size(334, 111);
            Controls.Add(cancelButton);
            Controls.Add(okButton);
            Controls.Add(passwordTextBox);
            Controls.Add(passwordLabel);
            Controls.Add(userNameTextBox);
            Controls.Add(userNameLabel);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "LoginForm1";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Sign in";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}
