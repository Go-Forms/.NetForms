using System;
using System.ComponentModel;
using NetForms.Platform;

namespace System.Windows.Forms;

/// <summary>A UI-thread timer: Tick is raised on the message loop, never concurrently with your code.</summary>
[DefaultEvent("Tick")]
[DefaultProperty("Interval")]
public class Timer : Component
{
    private IPlatformTimer? _timer;
    private int _interval = 100;
    private bool _enabled;

    public Timer() { }

    public Timer(IContainer container) : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    [Category("Behavior")]
    [Description("Occurs whenever the specified interval time elapses.")]
    public event EventHandler? Tick;

    [Category("Behavior")]
    [Description("The frequency of Elapsed events in milliseconds.")]
    [DefaultValue(100)]
    public int Interval
    {
        get => _interval;
        set
        {
            if (value < 1) throw new ArgumentOutOfRangeException(nameof(value), "Interval must be greater than zero.");
            _interval = value;
            if (_timer != null) _timer.Interval = TimeSpan.FromMilliseconds(value);
        }
    }

    [Category("Behavior")]
    [Description("Enables generation of Elapsed events.")]
    [DefaultValue(false)]
    public virtual bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            if (value)
            {
                _timer ??= Application.Platform.CreateTimer(() => Application.Dispatch(() => OnTick(EventArgs.Empty)));
                _timer.Interval = TimeSpan.FromMilliseconds(_interval);
                _timer.Start();
            }
            else
            {
                _timer?.Stop();
            }
        }
    }

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    public void Start() => Enabled = true;

    public void Stop() => Enabled = false;

    protected virtual void OnTick(EventArgs e) => Tick?.Invoke(this, e);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _enabled = false;
            _timer?.Dispose();
            _timer = null;
        }
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + ", Interval: " + Interval;
}
