using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public enum ToolTipIcon
{
    None = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

public delegate void PopupEventHandler(object? sender, PopupEventArgs e);
public delegate void DrawToolTipEventHandler(object? sender, DrawToolTipEventArgs e);

public class PopupEventArgs : CancelEventArgs
{
    public PopupEventArgs(IWin32Window? associatedWindow, Control? associatedControl, bool isBalloon, Size size)
    {
        AssociatedWindow = associatedWindow;
        AssociatedControl = associatedControl;
        IsBalloon = isBalloon;
        ToolTipSize = size;
    }

    public IWin32Window? AssociatedWindow { get; }
    public Control? AssociatedControl { get; }
    public bool IsBalloon { get; }
    public Size ToolTipSize { get; set; }
}

public class DrawToolTipEventArgs : EventArgs
{
    public DrawToolTipEventArgs(Graphics graphics, IWin32Window? associatedWindow, Control? associatedControl, Rectangle bounds, string? toolTipText, Color backColor, Color foreColor, Font? font)
    {
        Graphics = graphics;
        AssociatedWindow = associatedWindow;
        AssociatedControl = associatedControl;
        Bounds = bounds;
        ToolTipText = toolTipText;
        BackColor = backColor;
        ForeColor = foreColor;
        Font = font;
    }

    public Graphics Graphics { get; }
    public IWin32Window? AssociatedWindow { get; }
    public Control? AssociatedControl { get; }
    public Rectangle Bounds { get; }
    public string? ToolTipText { get; }
    public Color BackColor { get; }
    public Color ForeColor { get; }
    public Font? Font { get; }

    public void DrawBackground()
    {
        using var b = new SolidBrush(BackColor);
        Graphics.FillRectangle(b, Bounds);
    }

    public void DrawBorder()
    {
        using var p = new Pen(Theme.ScrollThumbHot);
        Graphics.DrawRectangle(p, Bounds.X, Bounds.Y, Bounds.Width - 1, Bounds.Height - 1);
    }

    public void DrawText() => DrawText(TextFormatFlags.Default);

    public void DrawText(TextFormatFlags flags)
    {
        var r = Bounds;
        r.Inflate(-4, -3);
        TextRenderer.DrawText(Graphics, ToolTipText, Font ?? Control.DefaultFont, r, ForeColor, flags);
    }
}

/// <summary>A hover tip for any number of controls, shown in a non-activating popup after InitialDelay.</summary>
/// <remarks>An extender provider, as in WinForms: "ToolTip on toolTip1" is a property it gives every control.</remarks>
[ProvideProperty("ToolTip", typeof(Control))]
[DefaultEvent("Popup")]
public class ToolTip : Component, IExtenderProvider
{
    /// <summary>Every control but a form's own tool tip windows can carry a tip.</summary>
    public virtual bool CanExtend(object target) => target is Control and not Form;

    private readonly Dictionary<Control, string> _texts = new();
    private readonly Timer _showTimer = new();
    private readonly Timer _hideTimer = new();
    private TipPopup? _popup;
    private Control? _hoverControl;
    private Point _lastMouse;
    private bool _active = true;

    public ToolTip()
    {
        _showTimer.Tick += (_, _) =>
        {
            _showTimer.Stop();
            if (_hoverControl != null) ShowFor(_hoverControl, _texts.GetValueOrDefault(_hoverControl), _lastMouse, AutoPopDelay);
        };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            HidePopup();
        };
    }

    public ToolTip(IContainer container) : this()
    {
        ArgumentNullException.ThrowIfNull(container);
        container.Add(this);
    }

    [Category("Behavior")]
    [Description("Occurs whenever a ToolTip is about to be shown.")]
    public event PopupEventHandler? Popup;

    [Category("Behavior")]
    [Description("Occurs in OwnerDraw mode when the ToolTip needs to be drawn.")]
    public event DrawToolTipEventHandler? Draw;

    [Description("Determines if the ToolTip is active. A tip will only appear if the ToolTip has been activated.")]
    [DefaultValue(true)]
    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            if (!value) HidePopup();
        }
    }

    [Description("Determines the length of time the pointer must remain stationary within a ToolTip region before the ToolTip window appears.")]
    public int InitialDelay { get; set; } = 500;

    [Description("Determines the length of time the ToolTip window remains visible if the pointer is stationary inside a ToolTip region.")]
    public int AutoPopDelay { get; set; } = 5000;

    [Description("Determines the length of time it takes for subsequent ToolTip windows to appear as the pointer moves from one ToolTip region to another.")]
    public int ReshowDelay { get; set; } = 100;

    [Description("Sets the values of AutoPopDelay, InitialDelay, and ReshowDelay to the appropriate values.")]
    [DefaultValue(500)]
    public int AutomaticDelay
    {
        get => InitialDelay;
        set
        {
            InitialDelay = value;
            AutoPopDelay = value * 10;
            ReshowDelay = value / 5;
        }
    }

    internal bool ShouldSerializeInitialDelay() => InitialDelay != AutomaticDelay;

    internal bool ShouldSerializeAutoPopDelay() => AutoPopDelay != AutomaticDelay * 10;

    internal bool ShouldSerializeReshowDelay() => ReshowDelay != AutomaticDelay / 5;

    [Description("Determines if the tool tip will be displayed always, even if the parent window is not active.")]
    [DefaultValue(false)]
    public bool ShowAlways { get; set; }

    [Description("Indicates whether the ToolTip will take on a balloon form.")]
    [DefaultValue(false)]
    public bool IsBalloon { get; set; }

    [Description("When set to true, animations are used when the ToolTip is shown or hidden.")]
    [DefaultValue(true)]
    public bool UseAnimation { get; set; } = true;

    [Description("When set to true, a fade effect is used when ToolTips are shown or hidden.")]
    [DefaultValue(true)]
    public bool UseFading { get; set; } = true;

    [Description("When set to true, any ampersands (&) in the Text property are not displayed.")]
    [DefaultValue(false)]
    public bool StripAmpersands { get; set; }

    [Category("Behavior")]
    [Description("Controls whether the system or the user paints items/sub items.")]
    [DefaultValue(false)]
    public bool OwnerDraw { get; set; }

    [Description("Determines the icon that is shown on the ToolTip.")]
    [DefaultValue(ToolTipIcon.None)]
    public ToolTipIcon ToolTipIcon { get; set; }

    [Description("Determines the title of the ToolTip.")]
    [DefaultValue("")]
    public string ToolTipTitle { get; set; } = string.Empty;

    [Description("The background color of the ToolTip control.")]
    [DefaultValue(typeof(Color), "Info")]
    public Color BackColor { get; set; } = SystemColors.Info;

    [Description("The foreground color of the ToolTip control.")]
    [DefaultValue(typeof(Color), "InfoText")]
    public Color ForeColor { get; set; } = SystemColors.InfoText;

    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    public void SetToolTip(Control control, string? caption)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (string.IsNullOrEmpty(caption))
        {
            if (_texts.Remove(control)) Detach(control);
            return;
        }
        if (!_texts.ContainsKey(control)) Attach(control);
        _texts[control] = caption;
    }

    [DefaultValue("")]
    [Localizable(true)]
    public string GetToolTip(Control? control) => control != null && _texts.TryGetValue(control, out var t) ? t : string.Empty;

    public void RemoveAll()
    {
        foreach (var c in _texts.Keys) Detach(c);
        _texts.Clear();
        HidePopup();
    }

    public void Show(string? text, IWin32Window window) => Show(text, window, 0);

    public void Show(string? text, IWin32Window window, int duration)
    {
        if (window is not Control control) return;
        var p = control.PointToClient(Control.MousePosition);
        ShowFor(control, text, new Point(p.X, control.Height), duration);
    }

    public void Show(string? text, IWin32Window window, Point point) => Show(text, window, point, 0);

    public void Show(string? text, IWin32Window window, Point point, int duration)
    {
        if (window is not Control control) return;
        ShowFor(control, text, point, duration);
    }

    public void Show(string? text, IWin32Window window, int x, int y) => Show(text, window, new Point(x, y), 0);

    public void Show(string? text, IWin32Window window, int x, int y, int duration) => Show(text, window, new Point(x, y), duration);

    public void Hide(IWin32Window window) => HidePopup();

    protected virtual void OnPopup(PopupEventArgs e) => Popup?.Invoke(this, e);
    protected virtual void OnDraw(DrawToolTipEventArgs e) => Draw?.Invoke(this, e);

    private void Attach(Control c)
    {
        c.MouseEnter += ControlMouseEnter;
        c.MouseMove += ControlMouseMove;
        c.MouseLeave += ControlMouseLeave;
        c.MouseDown += ControlMouseDown;
    }

    private void Detach(Control c)
    {
        c.MouseEnter -= ControlMouseEnter;
        c.MouseMove -= ControlMouseMove;
        c.MouseLeave -= ControlMouseLeave;
        c.MouseDown -= ControlMouseDown;
    }

    private void ControlMouseEnter(object? sender, EventArgs e)
    {
        if (!_active || sender is not Control c) return;
        _hoverControl = c;
        _showTimer.Interval = Math.Max(1, InitialDelay);
        _showTimer.Start();
    }

    private void ControlMouseMove(object? sender, MouseEventArgs e)
    {
        _lastMouse = e.Location;
        // Restart the delay while the pointer keeps moving before the tip is up.
        if ((_popup == null || !_popup.Visible) && _hoverControl == sender && _showTimer.Enabled)
        {
            _showTimer.Stop();
            _showTimer.Start();
        }
    }

    private void ControlMouseLeave(object? sender, EventArgs e)
    {
        if (_hoverControl == sender) _hoverControl = null;
        _showTimer.Stop();
        HidePopup();
    }

    private void ControlMouseDown(object? sender, MouseEventArgs e)
    {
        _showTimer.Stop();
        HidePopup();
    }

    private void ShowFor(Control control, string? text, Point clientPoint, int duration)
    {
        if (string.IsNullOrEmpty(text) || control.IsDisposed) return;
        var form = control.FindForm();
        if (form == null || !form.IsWindowCreated) return;
        if (!ShowAlways && !form.IsWindowActive) return;

        var font = control.Font;
        var textSize = TextRenderer.MeasureText(text, font, new Size(400, int.MaxValue), TextFormatFlags.WordBreak | (StripAmpersands ? 0 : TextFormatFlags.NoPrefix));
        var size = new Size(textSize.Width + 8, textSize.Height + 6);
        var args = new PopupEventArgs(form, control, IsBalloon, size);
        OnPopup(args);
        if (args.Cancel) return;
        size = args.ToolTipSize;

        _popup ??= new TipPopup(this);
        _popup.Configure(control, text, font, size);
        var screen = control.PointToScreen(new Point(clientPoint.X, clientPoint.Y + 20));
        _popup.ShowAt(control, screen, size);

        _hideTimer.Stop();
        int life = duration > 0 ? duration : AutoPopDelay;
        if (life > 0)
        {
            _hideTimer.Interval = life;
            _hideTimer.Start();
        }
    }

    private void HidePopup()
    {
        _hideTimer.Stop();
        _popup?.Dismiss();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RemoveAll();
            _showTimer.Dispose();
            _hideTimer.Dispose();
            _popup?.Dispose();
            _popup = null;
        }
        base.Dispose(disposing);
    }

    public override string ToString() => base.ToString() + " InitialDelay: " + InitialDelay + ", ShowAlways: " + ShowAlways;

    private sealed class TipPopup : PopupForm
    {
        private readonly ToolTip _owner;
        private Control? _control;
        private string _text = string.Empty;
        private Font? _font;

        public TipPopup(ToolTip owner) => _owner = owner;

        public void Configure(Control control, string text, Font font, Size size)
        {
            _control = control;
            _text = text;
            _font = font;
            ClientSize = size;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var args = new DrawToolTipEventArgs(e.Graphics, _control?.FindForm(), _control, ClientRectangle, _text, _owner.BackColor, _owner.ForeColor, _font);
            if (_owner.OwnerDraw)
            {
                _owner.OnDraw(args);
                return;
            }
            args.DrawBackground();
            args.DrawBorder();
            args.DrawText(TextFormatFlags.WordBreak | (_owner.StripAmpersands ? TextFormatFlags.Default : TextFormatFlags.NoPrefix));
        }
    }
}
