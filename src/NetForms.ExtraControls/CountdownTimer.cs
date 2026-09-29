using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace NetForms.ExtraControls;

/// <summary>
/// A component that counts down <see cref="Duration"/>: <see cref="Tick"/> every <see cref="Interval"/> with the time
/// <see cref="Remaining"/>, then <see cref="Finished"/>. It has no place on the form: the designer puts it in the
/// component tray, like <see cref="System.Windows.Forms.Timer"/>, and writes <c>new CountdownTimer(components)</c>.
/// </summary>
[ToolboxBitmap(typeof(CountdownTimer), "CountdownTimer.png")]
[DefaultEvent(nameof(Finished))]
[DefaultProperty(nameof(Duration))]
[Description("Counts a time span down, with a tick every interval.")]
public class CountdownTimer : Component
{
    private readonly System.Windows.Forms.Timer _timer = new();
    private TimeSpan _duration = TimeSpan.FromMinutes(1);
    private DateTime _endsAt;
    private TimeSpan _remaining = TimeSpan.FromMinutes(1);

    public CountdownTimer()
    {
        _timer.Interval = 1000;
        _timer.Tick += (_, _) => OnTimerTick();
    }

    /// <summary>A countdown in the form's <paramref name="container"/> (disposed with the form).</summary>
    public CountdownTimer(IContainer container) : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    /// <summary>How long the countdown runs.</summary>
    [Category("Behavior")]
    [DefaultValue(typeof(TimeSpan), "00:01:00")]
    [Description("How long the countdown runs.")]
    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (value < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(value), value, "Duration cannot be negative.");
            _duration = value;
            if (!Running) _remaining = value;
        }
    }

    /// <summary>Milliseconds between two <see cref="Tick"/> events.</summary>
    [Category("Behavior")]
    [DefaultValue(1000)]
    [Description("Milliseconds between two Tick events.")]
    public int Interval
    {
        get => _timer.Interval;
        set => _timer.Interval = value;
    }

    /// <summary>The time left.</summary>
    [Browsable(false)]
    public TimeSpan Remaining => Running ? Max(TimeSpan.Zero, _endsAt - Now()) : _remaining;

    [Browsable(false)]
    public bool Running => _timer.Enabled;

    [Category("Behavior")]
    [Description("Occurs every Interval while the countdown runs.")]
    public event EventHandler? Tick;

    [Category("Behavior")]
    [Description("Occurs when the time is up.")]
    public event EventHandler? Finished;

    /// <summary>Starts (or goes on with) the countdown from <see cref="Remaining"/>.</summary>
    public void Start()
    {
        if (Running || _remaining <= TimeSpan.Zero) return;
        _endsAt = Now() + _remaining;
        _timer.Start();
    }

    /// <summary>Pauses: <see cref="Remaining"/> stays where it is.</summary>
    public void Stop()
    {
        if (!Running) return;
        _remaining = Remaining;
        _timer.Stop();
    }

    /// <summary>Stops and puts <see cref="Remaining"/> back to <see cref="Duration"/>.</summary>
    public void Reset()
    {
        _timer.Stop();
        _remaining = _duration;
    }

    /// <summary>The clock; a test replaces it.</summary>
    internal Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;

    internal void OnTimerTick()
    {
        var left = Remaining;
        OnTick(EventArgs.Empty);
        if (left > TimeSpan.Zero) return;
        _timer.Stop();
        _remaining = TimeSpan.Zero;
        OnFinished(EventArgs.Empty);
    }

    protected virtual void OnTick(EventArgs e) => Tick?.Invoke(this, e);

    protected virtual void OnFinished(EventArgs e) => Finished?.Invoke(this, e);

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
