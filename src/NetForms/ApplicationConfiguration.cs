using System.Windows.Forms;

/// <summary>
/// In a real WinForms project this class is generated into the application assembly by
/// the SDK (global namespace, from the ApplicationHighDpiMode/ApplicationVisualStyles
/// properties). NetForms ships it ready-made so the template's Program.cs compiles as is.
/// </summary>
public static class ApplicationConfiguration
{
    public static void Initialize()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    }
}
