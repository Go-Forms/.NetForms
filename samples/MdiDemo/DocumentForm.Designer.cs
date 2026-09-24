namespace MdiDemo
{
    partial class DocumentForm
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
            editor = new TextBox();
            SuspendLayout();
            //
            // editor
            //
            editor.BorderStyle = BorderStyle.None;
            editor.Dock = DockStyle.Fill;
            editor.Multiline = true;
            editor.Name = "editor";
            editor.ScrollBars = ScrollBars.Vertical;
            editor.TabIndex = 0;
            editor.TextChanged += editor_TextChanged;
            //
            // DocumentForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(320, 200);
            Controls.Add(editor);
            Name = "DocumentForm";
            Text = "Document";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox editor;
    }
}
