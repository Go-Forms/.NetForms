namespace NetFormsNamespace
{
    /// <summary>
    ///  A sign-in dialog:
    ///  <code>
    ///  using var login = new LoginForm1();
    ///  if (login.ShowDialog(this) == DialogResult.OK) { var user = login.UserName; var password = login.Password; ... }
    ///  </code>
    /// </summary>
    public partial class LoginForm1 : Form
    {
        public LoginForm1()
        {
            InitializeComponent();
        }

        /// <summary>The user name typed.</summary>
        public string UserName
        {
            get => userNameTextBox.Text;
            set => userNameTextBox.Text = value;
        }

        /// <summary>The password typed.</summary>
        public string Password => passwordTextBox.Text;
    }
}
