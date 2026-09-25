using System.Reflection;

namespace NetFormsNamespace
{
    /// <summary>
    ///  A splash screen, shown while the application starts - in Program.cs, before Application.Run:
    ///  <code>
    ///  var splash = new SplashScreen1();
    ///  splash.Show();
    ///  Application.DoEvents();
    ///  var main = new MainForm();   // the slow start-up work
    ///  splash.Close();
    ///  Application.Run(main);
    ///  </code>
    /// </summary>
    public partial class SplashScreen1 : Form
    {
        public SplashScreen1()
        {
            InitializeComponent();
            var assembly = Assembly.GetEntryAssembly() ?? typeof(SplashScreen1).Assembly;
            applicationTitle.Text = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title is { Length: > 0 } title
                ? title
                : Path.GetFileNameWithoutExtension(assembly.Location);
            versionLabel.Text = $"Version {assembly.GetName().Version}";
            copyrightLabel.Text = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright ?? "";
        }
    }
}
