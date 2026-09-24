using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using NetForms.Platform;

namespace System.Windows.Forms;

public static partial class Application
{
    private static IPlatform? s_platform;
    private static readonly List<Form> s_openForms = new();
    private static Form? s_mainForm;
    private static int s_messageLoopDepth;
    private static int s_uiThreadId = -1;

    /// <summary>The platform layer in use. Avalonia by default; tests and other backends may replace it before the first form is created.</summary>
    internal static IPlatform Platform
    {
        get => s_platform ??= new NetForms.Platform.Avalonia.AvaloniaPlatform();
        set => s_platform = value;
    }

    /// <summary>The platform if one was created or installed; null before first use (the designer installs its own).</summary>
    internal static IPlatform? PlatformIfCreated => s_platform;

    internal static bool IsUIThread => s_uiThreadId == -1 || s_uiThreadId == Environment.CurrentManagedThreadId;

    internal static void Post(Action action)
    {
        if (s_platform == null) action();
        else s_platform.Post(action);
    }

    public static bool MessageLoop => s_messageLoopDepth > 0;

    public static FormCollection OpenForms => new FormCollection(s_openForms);

    public static event EventHandler? Idle;
    public static event EventHandler? ApplicationExit;
    public static event EventHandler? ThreadExit;

    internal static void RaiseIdle() => RaiseIdle(EventArgs.Empty);

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Advanced)]
    public static void RaiseIdle(EventArgs e) => Idle?.Invoke(null, e);

    internal static void RegisterForm(Form form)
    {
        if (!s_openForms.Contains(form)) s_openForms.Add(form);
    }

    internal static void UnregisterForm(Form form)
    {
        s_openForms.Remove(form);
        if (s_mainForm == form && s_messageLoopDepth > 0)
        {
            s_mainForm = null;
            ExitThread();
        }
    }

    // --- configuration (the ApplicationConfiguration.Initialize() trio) -------------

    public static void EnableVisualStyles() => UseVisualStyles = true;

    public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }

    public static bool SetHighDpiMode(HighDpiMode highDpiMode) => true;

    public static HighDpiMode HighDpiMode => HighDpiMode.PerMonitorV2;

    public static void SetDefaultFont(Font font)
    {
        ArgumentNullException.ThrowIfNull(font);
        Control.DefaultFont = font;
    }

    public static Font DefaultFont => Control.DefaultFont;

    public static bool UseWaitCursor { get; set; }

    // --- running ----------------------------------------------------------------------

    public static void Run()
    {
        var platform = Platform;
        platform.Initialize();
        RunLoop(platform);
    }

    public static void Run(Form mainForm)
    {
        ArgumentNullException.ThrowIfNull(mainForm);
        var platform = Platform;
        platform.Initialize();
        s_mainForm = mainForm;
        mainForm.Show();
        if (mainForm.IsDisposed)
        {
            // Closed during Load: nothing to run.
            s_mainForm = null;
            return;
        }
        RunLoop(platform);
        s_mainForm = null;
    }

    private static void RunLoop(IPlatform platform)
    {
        s_uiThreadId = Environment.CurrentManagedThreadId;
        WindowsFormsSynchronizationContext.InstallIfNeeded();
        s_messageLoopDepth++;
        try
        {
            platform.RunMessageLoop();
        }
        finally
        {
            s_messageLoopDepth--;
            if (s_messageLoopDepth == 0)
            {
                ThreadExit?.Invoke(null, EventArgs.Empty);
                ApplicationExit?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static void Exit()
    {
        foreach (var form in s_openForms.ToArray())
        {
            form.CloseFromApplication();
        }
        if (s_messageLoopDepth > 0) Platform.ExitMessageLoop();
    }

    public static void ExitThread()
    {
        if (s_messageLoopDepth > 0) Platform.ExitMessageLoop();
    }

    public static void DoEvents()
    {
        if (s_platform != null) s_platform.DoEvents();
    }

    public static void Restart() => throw new NotSupportedException();

    // --- metadata ---------------------------------------------------------------------

    public static string ProductName
    {
        get
        {
            var asm = Assembly.GetEntryAssembly();
            return asm?.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? asm?.GetName().Name ?? "NetForms";
        }
    }

    public static string ProductVersion
    {
        get
        {
            var asm = Assembly.GetEntryAssembly();
            return asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? asm?.GetName().Version?.ToString() ?? "1.0.0.0";
        }
    }

    public static string CompanyName
    {
        get
        {
            var asm = Assembly.GetEntryAssembly();
            return asm?.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? ProductName;
        }
    }

    public static string ExecutablePath => Environment.ProcessPath ?? Assembly.GetEntryAssembly()?.Location ?? string.Empty;

    public static string StartupPath => AppContext.BaseDirectory;

    public static string UserAppDataPath
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), CompanyName, ProductName, ProductVersion);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string CommonAppDataPath
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), CompanyName, ProductName, ProductVersion);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string LocalUserAppDataPath
    {
        get
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), CompanyName, ProductName, ProductVersion);
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static System.Globalization.CultureInfo CurrentCulture
    {
        get => System.Globalization.CultureInfo.CurrentCulture;
        set => System.Threading.Thread.CurrentThread.CurrentCulture = value;
    }
}

/// <summary>Read-only view of the forms that currently have a window.</summary>
public class FormCollection : IReadOnlyList<Form>, IEnumerable
{
    private readonly List<Form> _forms;

    internal FormCollection(List<Form> forms) => _forms = forms;

    public int Count => _forms.Count;

    public Form this[int index] => _forms[index];

    public Form? this[string name]
    {
        get
        {
            foreach (var f in _forms)
            {
                if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f;
            }
            return null;
        }
    }

    public IEnumerator<Form> GetEnumerator() => _forms.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _forms.GetEnumerator();
}
