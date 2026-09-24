namespace MdiDemo
{
    /// <summary>One MDI child: a text editor with its own document name.</summary>
    public partial class DocumentForm : Form
    {
        private static int s_counter;

        public DocumentForm()
        {
            InitializeComponent();
            Text = "Document " + ++s_counter;
            editor.Text = "This is " + Text + "." + Environment.NewLine
                + "Drag my caption, resize my border, maximise me - I live inside the parent window.";
        }

        public string DocumentText
        {
            get => editor.Text;
            set => editor.Text = value;
        }

        private void editor_TextChanged(object sender, EventArgs e)
        {
            if (MdiParent is MainForm main) main.ReportLength(editor.TextLength);
        }
    }
}
