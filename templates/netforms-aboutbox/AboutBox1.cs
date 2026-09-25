using System.Reflection;

namespace NetFormsNamespace
{
    /// <summary>
    ///  An About box. Its texts come from the attributes of the application's assembly - set them in the
    ///  project file: &lt;Product&gt;, &lt;Version&gt;, &lt;Copyright&gt;, &lt;Company&gt;, &lt;Description&gt;.
    ///  <code>
    ///  using var about = new AboutBox1();
    ///  about.ShowDialog(this);
    ///  </code>
    /// </summary>
    public partial class AboutBox1 : Form
    {
        public AboutBox1()
        {
            InitializeComponent();
            Text = $"About {AssemblyTitle}";
            labelProductName.Text = AssemblyProduct;
            labelVersion.Text = $"Version {AssemblyVersion}";
            labelCopyright.Text = AssemblyCopyright;
            labelCompanyName.Text = AssemblyCompany;
            textBoxDescription.Text = AssemblyDescription;
        }

        #region Assembly Attribute Accessors

        private static Assembly ApplicationAssembly => Assembly.GetEntryAssembly() ?? typeof(AboutBox1).Assembly;

        public string AssemblyTitle =>
            ApplicationAssembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title is { Length: > 0 } title
                ? title
                : Path.GetFileNameWithoutExtension(ApplicationAssembly.Location);

        public string AssemblyVersion => ApplicationAssembly.GetName().Version?.ToString() ?? "";

        public string AssemblyDescription => ApplicationAssembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? "";

        public string AssemblyProduct => ApplicationAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "";

        public string AssemblyCopyright => ApplicationAssembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";

        public string AssemblyCompany => ApplicationAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";

        #endregion
    }
}
