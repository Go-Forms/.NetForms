using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace System.Windows.Forms;

/// <summary>
/// Application's WinForms surface beyond running forms (Ф6.К): exceptions of the message loop
/// (ThreadException, decision 115), application contexts, message filters, and the settings code
/// commonly reads (visual styles, culture, color mode).
/// </summary>
public static partial class Application
{
    private static UnhandledExceptionMode s_exceptionMode = UnhandledExceptionMode.Automatic;
    private static readonly List<IMessageFilter> s_messageFilters = new();

    /// <summary>
    /// An exception thrown by the application's code while the message loop dispatched to it - an event
    /// handler, a paint, a timer tick. Handled here, the application continues.
    /// </summary>
    public static event ThreadExceptionEventHandler? ThreadException;

    public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode) => SetUnhandledExceptionMode(mode, threadScope: true);

    public static void SetUnhandledExceptionMode(UnhandledExceptionMode mode, bool threadScope)
    {
        if (!Enum.IsDefined(mode)) throw new InvalidEnumArgumentException(nameof(mode), (int)mode, typeof(UnhandledExceptionMode));
        if (s_openForms.Count > 0)
            throw new InvalidOperationException("Thread exception mode cannot be changed once any Controls are created on the thread.");
        s_exceptionMode = mode;
    }

    /// <summary>Raises ThreadException; without a handler, the WinForms dialog - Continue or Quit.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static void OnThreadException(Exception t)
    {
        ArgumentNullException.ThrowIfNull(t);
        if (ThreadException is { } handler)
        {
            handler(Thread.CurrentThread, new ThreadExceptionEventArgs(t));
            return;
        }
        var answer = MessageBox.Show(
            "Unhandled exception has occurred in your application. If you click OK, the application will ignore this error and attempt to continue. If you click Cancel, the application will close immediately.\n\n"
            + t.Message + "\n\n" + t,
            ProductName, MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
        if (answer == DialogResult.Cancel) Environment.Exit(1);
    }

    /// <summary>
    /// Whether the message loop takes <paramref name="ex"/> (true: reported through ThreadException, the
    /// dispatch goes on). ThrowException mode, or no handler and no screen to show the dialog on (tests,
    /// the designer), lets it propagate - what happened before decision 115.
    /// </summary>
    internal static bool CatchThreadException(Exception ex)
    {
        if (s_exceptionMode == UnhandledExceptionMode.ThrowException) return false;
        if (ThreadException == null && !IsInteractive) return false;
        OnThreadException(ex);
        return true;
    }

    /// <summary>Runs a dispatch from the platform into the application's code (decision 115).</summary>
    internal static void Dispatch(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex) when (CatchThreadException(ex))
        {
        }
    }

    internal static T Dispatch<T>(Func<T> action, T onException)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (CatchThreadException(ex))
        {
            return onException;
        }
    }

    /// <summary>A real screen (the Avalonia platform), where a dialog can be shown.</summary>
    private static bool IsInteractive => s_platform is NetForms.Platform.Avalonia.AvaloniaPlatform;

    // --- application contexts ----------------------------------------------------------------------------

    private static ApplicationContext? s_context;

    /// <summary>Runs the message loop until <paramref name="context"/> exits (its main form closing, or ExitThread).</summary>
    public static void Run(ApplicationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var platform = Platform;
        platform.Initialize();
        s_context = context;
        context.ThreadExit += OnContextExit;
        try
        {
            if (context.MainForm is { } form)
            {
                s_mainForm = form;
                form.Show();
                if (form.IsDisposed) return;
            }
            RunLoop(platform);
        }
        finally
        {
            context.ThreadExit -= OnContextExit;
            s_context = null;
            s_mainForm = null;
        }
    }

    private static void OnContextExit(object? sender, EventArgs e) => ExitThread();

    // --- message filters: keyboard messages are offered to them (decision 115) -----------------------------

    public static void AddMessageFilter(IMessageFilter value)
    {
        ArgumentNullException.ThrowIfNull(value);
        s_messageFilters.Add(value);
    }

    public static void RemoveMessageFilter(IMessageFilter value) => s_messageFilters.Remove(value);

    /// <summary>Offers a message to the filters; true when one of them took it.</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public static bool FilterMessage(ref Message message)
    {
        foreach (var filter in s_messageFilters.ToArray())
            if (filter.PreFilterMessage(ref message)) return true;
        return false;
    }

    internal static bool HasMessageFilters => s_messageFilters.Count > 0;

    // --- settings ------------------------------------------------------------------------------------------

    public static bool UseVisualStyles { get; private set; }

    public static bool RenderWithVisualStyles => UseVisualStyles;

    public static VisualStyles.VisualStyleState VisualStyleState { get; set; } = VisualStyles.VisualStyleState.ClientAndNonClientAreasEnabled;

    public static bool AllowQuit => true;

    public static string SafeTopLevelCaptionFormat { get; set; } = "{1} - {0} - {2}";

    private static SystemColorMode s_colorMode = SystemColorMode.Classic;

    /// <summary>NetForms paints its own light theme; the mode is kept for code that asks (decision 115).</summary>
    public static SystemColorMode ColorMode => s_colorMode;

    public static SystemColorMode SystemColorMode => SystemColorMode.Classic;

    public static bool IsDarkModeEnabled => false;

    public static void SetColorMode(SystemColorMode systemColorMode)
    {
        if (!Enum.IsDefined(systemColorMode)) throw new InvalidEnumArgumentException(nameof(systemColorMode), (int)systemColorMode, typeof(SystemColorMode));
        s_colorMode = systemColorMode;
    }

    public static ApartmentState OleRequired() => Thread.CurrentThread.GetApartmentState();

    public static void Exit(CancelEventArgs? e)
    {
        Exit();
        if (e != null) e.Cancel = s_openForms.Count > 0;
    }

    public static event EventHandler? EnterThreadModal;

    public static event EventHandler? LeaveThreadModal;

    internal static void RaiseEnterThreadModal() => EnterThreadModal?.Invoke(null, EventArgs.Empty);

    internal static void RaiseLeaveThreadModal() => LeaveThreadModal?.Invoke(null, EventArgs.Empty);
}

public enum UnhandledExceptionMode
{
    Automatic = 0,
    ThrowException = 1,
    CatchException = 2,
}

public enum SystemColorMode
{
    Classic = 0,
    System = 1,
    Dark = 2,
}

public interface IMessageFilter
{
    bool PreFilterMessage(ref Message m);
}

/// <summary>What a message loop runs for: a main form, or code that calls ExitThread.</summary>
public class ApplicationContext : IDisposable
{
    private Form? _mainForm;

    public ApplicationContext() : this(null) { }

    public ApplicationContext(Form? mainForm) => MainForm = mainForm;

    ~ApplicationContext() => Dispose(false);

    public Form? MainForm
    {
        get => _mainForm;
        set
        {
            if (_mainForm != null) _mainForm.HandleDestroyed -= OnMainFormDestroy;
            _mainForm = value;
            if (_mainForm != null) _mainForm.HandleDestroyed += OnMainFormDestroy;
        }
    }

    [Localizable(false)]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    public event EventHandler? ThreadExit;

    public void ExitThread() => ExitThreadCore();

    protected virtual void ExitThreadCore() => ThreadExit?.Invoke(this, EventArgs.Empty);

    protected virtual void OnMainFormClosed(object? sender, EventArgs e) => ExitThreadCore();

    private void OnMainFormDestroy(object? sender, EventArgs e)
    {
        if (sender is Form form && !form.RecreatingHandle)
        {
            form.HandleDestroyed -= OnMainFormDestroy;
            OnMainFormClosed(sender, e);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && _mainForm != null)
        {
            if (!_mainForm.IsDisposed) _mainForm.Dispose();
            _mainForm = null;
        }
    }
}
