using System;
using System.Threading;

namespace System.Windows.Forms;

/// <summary>Marshals continuations (await, Post/Send) back onto the message loop thread.</summary>
public sealed class WindowsFormsSynchronizationContext : SynchronizationContext, IDisposable
{
    public static bool AutoInstall { get; set; } = true;

    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        Application.Post(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Application.IsUIThread)
        {
            d(state);
            return;
        }
        using var done = new ManualResetEventSlim();
        Exception? error = null;
        Application.Post(() =>
        {
            try { d(state); }
            catch (Exception ex) { error = ex; }
            finally { done.Set(); }
        });
        done.Wait();
        if (error != null) throw new AggregateException(error);
    }

    public override SynchronizationContext CreateCopy() => new WindowsFormsSynchronizationContext();

    public void Dispose() { }

    public static void Uninstall() => Uninstall(true);

    public static void Uninstall(bool turnOffAutoInstall)
    {
        if (Current is WindowsFormsSynchronizationContext) SetSynchronizationContext(null);
        if (turnOffAutoInstall) AutoInstall = false;
    }

    internal static void InstallIfNeeded()
    {
        if (AutoInstall && Current is not WindowsFormsSynchronizationContext)
        {
            SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        }
    }
}
