namespace HelloForms
{
    public partial class MainForm : Form
    {
        private int clicks;

        public MainForm()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            clicks++;
            label1.Text = $"Hello from NetForms! Clicked {clicks} time(s).";
        }
    }
}
