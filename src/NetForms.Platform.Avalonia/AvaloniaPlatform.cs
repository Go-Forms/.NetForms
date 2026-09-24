using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace NetForms.Platform.Avalonia;

/// <summary>
/// The Avalonia 12 implementation of the platform layer. Avalonia plays the role Win32
/// played for WinForms: it owns the windows, the message loop and the raw input; nothing
/// above this assembly sees an Avalonia type.
/// </summary>
public sealed class AvaloniaPlatform : IPlatform
{
    private static bool s_initialized;
    private readonly Stack<DispatcherFrame> _frames = new();

    public void Initialize()
    {
        if (s_initialized) return;
        s_initialized = true;

        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown,
            Args = Array.Empty<string>(),
        };

        AppBuilder.Configure<NetFormsApp>()
            .UsePlatformDetect()
            .UseSkia()
            .SetupWithLifetime(lifetime);
    }

    public IPlatformWindow CreateWindow(IWindowHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Initialize();
        return new AvaloniaWindow(host);
    }

    /// <summary>Runs a nested dispatcher frame; nesting gives modal dialogs their own loop for free.</summary>
    public void RunMessageLoop()
    {
        var frame = new DispatcherFrame();
        _frames.Push(frame);
        try
        {
            Dispatcher.UIThread.PushFrame(frame);
        }
        finally
        {
            if (_frames.Count > 0 && ReferenceEquals(_frames.Peek(), frame)) _frames.Pop();
        }
    }

    public void ExitMessageLoop()
    {
        if (_frames.Count > 0)
        {
            _frames.Peek().Continue = false;
        }
    }

    public void DoEvents() => Dispatcher.UIThread.RunJobs();

    public bool IsUIThread => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public IPlatformTimer CreateTimer(Action tick) => new AvaloniaTimer(tick);

    public IPlatformTrayIcon? CreateTrayIcon(ITrayIconHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Initialize();
        return new AvaloniaTrayIcon(host);
    }

    private static global::Avalonia.Input.Platform.IClipboard? FindClipboard()
    {
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (lifetime == null) return null;
        foreach (var w in lifetime.Windows)
        {
            var c = w.Clipboard;
            if (c != null) return c;
        }
        return null;
    }

    /// <summary>Avalonia's clipboard is asynchronous; pump a nested frame until the task completes so the WinForms API can stay synchronous.</summary>
    private static T WaitOnUIThread<T>(System.Threading.Tasks.Task<T> task)
    {
        if (task.IsCompleted) return task.GetAwaiter().GetResult();
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, System.Threading.Tasks.TaskScheduler.Default);
        Dispatcher.UIThread.PushFrame(frame);
        return task.GetAwaiter().GetResult();
    }

    private static void WaitOnUIThread(System.Threading.Tasks.Task task)
    {
        if (task.IsCompleted) { task.GetAwaiter().GetResult(); return; }
        var frame = new DispatcherFrame();
        task.ContinueWith(_ => frame.Continue = false, System.Threading.Tasks.TaskScheduler.Default);
        Dispatcher.UIThread.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }

    public string? GetClipboardText()
    {
        var clipboard = FindClipboard();
        if (clipboard == null) return null;
        try
        {
            return WaitOnUIThread(global::Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clipboard));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void SetClipboardText(string text)
    {
        var clipboard = FindClipboard();
        if (clipboard == null) return;
        try
        {
            WaitOnUIThread(global::Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(clipboard, text));
        }
        catch (Exception)
        {
            // A clipboard that is unavailable (no display, no window yet) is not an error for the caller.
        }
    }

    // --- native file dialogs ---------------------------------------------------------------
    // Avalonia's StorageProvider is the portal on Linux and the common item dialog on Windows,
    // so these are the system's own pickers rather than a drawn imitation.

    public string[]? ShowOpenFileDialog(IPlatformWindow? owner, FileDialogOptions options)
    {
        var provider = StorageProviderFor(owner);
        if (provider == null) return null;

        var files = WaitOnUIThread(provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = options.Title,
            AllowMultiple = options.Multiselect,
            FileTypeFilter = ToFileTypes(options.Filter),
            SuggestedStartLocation = StartLocation(provider, options.InitialDirectory),
            SuggestedFileName = options.FileName,
        }));

        if (files == null || files.Count == 0) return null;
        var result = new string[files.Count];
        for (int i = 0; i < files.Count; i++) result[i] = files[i].Path.LocalPath;
        return result;
    }

    public string? ShowSaveFileDialog(IPlatformWindow? owner, FileDialogOptions options)
    {
        var provider = StorageProviderFor(owner);
        if (provider == null) return null;

        var file = WaitOnUIThread(provider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = options.Title,
            FileTypeChoices = ToFileTypes(options.Filter),
            SuggestedStartLocation = StartLocation(provider, options.InitialDirectory),
            SuggestedFileName = options.FileName,
            DefaultExtension = string.IsNullOrEmpty(options.DefaultExt) ? null : options.DefaultExt,
            ShowOverwritePrompt = true,
        }));
        return file?.Path.LocalPath;
    }

    public string? ShowFolderDialog(IPlatformWindow? owner, FolderDialogOptions options)
    {
        var provider = StorageProviderFor(owner);
        if (provider == null) return null;

        var folders = WaitOnUIThread(provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = options.Title,
            AllowMultiple = false,
            SuggestedStartLocation = StartLocation(provider, options.InitialDirectory),
        }));
        return folders is { Count: > 0 } ? folders[0].Path.LocalPath : null;
    }

    private IStorageProvider? StorageProviderFor(IPlatformWindow? owner)
    {
        Initialize();
        if (owner is AvaloniaWindow window) return window.Native.StorageProvider;
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (lifetime == null) return null;
        foreach (var w in lifetime.Windows) return w.StorageProvider;
        return null;
    }

    private static IStorageFolder? StartLocation(IStorageProvider provider, string directory)
    {
        if (string.IsNullOrEmpty(directory)) return null;
        try
        {
            return WaitOnUIThread(provider.TryGetFolderFromPathAsync(directory));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Turns a WinForms filter ("Text|*.txt|All|*.*") into Avalonia's file types.</summary>
    private static List<FilePickerFileType>? ToFileTypes(string filter)
    {
        if (string.IsNullOrEmpty(filter)) return null;
        var parts = filter.Split('|');
        var types = new List<FilePickerFileType>();
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = new List<string>(parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries));
            types.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
        }
        return types.Count > 0 ? types : null;
    }

    public ScreenInfo[] GetScreens()
    {
        Initialize();
        // Screens hang off a window in Avalonia; borrow the first one there is, or report a default.
        var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        Window? window = null;
        if (lifetime != null)
        {
            foreach (var w in lifetime.Windows) { window = w; break; }
        }
        var screens = window?.Screens;
        if (screens == null || screens.All.Count == 0)
        {
            return new[] { new ScreenInfo("default", new System.Drawing.Rectangle(0, 0, 1920, 1080), new System.Drawing.Rectangle(0, 0, 1920, 1080), true, 32) };
        }
        var result = new List<ScreenInfo>();
        foreach (var s in screens.All)
        {
            double scale = s.Scaling;
            System.Drawing.Rectangle Dip(PixelRect r) => new((int)Math.Round(r.X / scale), (int)Math.Round(r.Y / scale), (int)Math.Round(r.Width / scale), (int)Math.Round(r.Height / scale));
            result.Add(new ScreenInfo(s.DisplayName ?? "screen", Dip(s.Bounds), Dip(s.WorkingArea), s.IsPrimary, 32));
        }
        result.Sort((a, b) => b.Primary.CompareTo(a.Primary));
        return result.ToArray();
    }

    private sealed class AvaloniaTimer : IPlatformTimer
    {
        private readonly DispatcherTimer _timer;

        public AvaloniaTimer(Action tick)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
            _timer.Tick += (_, _) => tick();
        }

        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        public bool IsRunning => _timer.IsEnabled;
        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();
        public void Dispose() => _timer.Stop();
    }
}
