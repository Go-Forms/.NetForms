namespace NetFormsNamespace
{
    partial class SplashScreen1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private Label applicationTitle;
        private Label versionLabel;
        private Label copyrightLabel;
        private ProgressBar progressBar;

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
            applicationTitle = new Label();
            versionLabel = new Label();
            copyrightLabel = new Label();
            progressBar = new ProgressBar();
            SuspendLayout();
            //
            // applicationTitle
            //
            applicationTitle.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            applicationTitle.Location = new Point(24, 40);
            applicationTitle.Name = "applicationTitle";
            applicationTitle.Size = new Size(432, 50);
            applicationTitle.TabIndex = 0;
            applicationTitle.Text = "Application Title";
            applicationTitle.TextAlign = ContentAlignment.MiddleLeft;
            //
            // versionLabel
            //
            versionLabel.Location = new Point(24, 100);
            versionLabel.Name = "versionLabel";
            versionLabel.Size = new Size(432, 20);
            versionLabel.TabIndex = 1;
            versionLabel.Text = "Version";
            //
            // copyrightLabel
            //
            copyrightLabel.Location = new Point(24, 180);
            copyrightLabel.Name = "copyrightLabel";
            copyrightLabel.Size = new Size(432, 20);
            copyrightLabel.TabIndex = 2;
            copyrightLabel.Text = "Copyright";
            //
            // progressBar
            //
            progressBar.Location = new Point(24, 208);
            progressBar.MarqueeAnimationSpeed = 30;
            progressBar.Name = "progressBar";
            progressBar.Size = new Size(432, 10);
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.TabIndex = 3;
            //
            // SplashScreen1
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = SystemColors.Window;
            ClientSize = new Size(480, 240);
            Controls.Add(progressBar);
            Controls.Add(copyrightLabel);
            Controls.Add(versionLabel);
            Controls.Add(applicationTitle);
            FormBorderStyle = FormBorderStyle.None;
            Name = "SplashScreen1";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "SplashScreen1";
            ResumeLayout(false);
        }

        #endregion
    }
}
