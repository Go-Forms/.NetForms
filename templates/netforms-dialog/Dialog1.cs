namespace NetFormsNamespace
{
    /// <summary>
    ///  A dialog box. OK and Cancel close it and set its DialogResult (Enter and Esc press them):
    ///  <code>
    ///  using var dialog = new Dialog1();
    ///  if (dialog.ShowDialog(this) == DialogResult.OK) { ... }
    ///  </code>
    /// </summary>
    public partial class Dialog1 : Form
    {
        public Dialog1()
        {
            InitializeComponent();
        }
    }
}
