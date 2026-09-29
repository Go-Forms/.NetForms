using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetForms.ExtraControls;

/// <summary>
/// An on/off switch: a rounded track with a thumb that slides to the side of the state. A click, the space bar or
/// <see cref="Checked"/> changes it; <see cref="CheckedChanged"/> tells. The text, if any, stands to the right.
/// </summary>
[ToolboxBitmap(typeof(ToggleSwitch), "ToggleSwitch.png")]
[DefaultEvent(nameof(CheckedChanged))]
[DefaultProperty(nameof(Checked))]
[DefaultBindingProperty(nameof(Checked))]
[Description("An on/off switch.")]
public class ToggleSwitch : Control
{
    private bool _checked;
    private Color _onColor = Color.DodgerBlue;
    private Color _offColor = Color.DarkGray;
    private Color _thumbColor = Color.White;
    private bool _hot;

    public ToggleSwitch()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.StandardClick, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
        TabStop = true;
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override Size DefaultSize => new(48, 24);

    /// <summary>Transparent by default: the control sits on whatever its parent paints (a GradientPanel, say).</summary>
    [DefaultValue(typeof(Color), "Transparent")]
    public override Color BackColor
    {
        get => base.BackColor;
        set => base.BackColor = value;
    }

    /// <summary>On (true) or off.</summary>
    [Category("Appearance")]
    [DefaultValue(false)]
    [Bindable(true)]
    [Description("Whether the switch is on.")]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
            OnCheckedChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "DodgerBlue")]
    [Description("The colour of the track when the switch is on.")]
    public Color OnColor
    {
        get => _onColor;
        set { _onColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "DarkGray")]
    [Description("The colour of the track when the switch is off.")]
    public Color OffColor
    {
        get => _offColor;
        set { _offColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "White")]
    [Description("The colour of the sliding thumb.")]
    public Color ThumbColor
    {
        get => _thumbColor;
        set { _thumbColor = value; Invalidate(); }
    }

    [Category("Property Changed")]
    [Description("Occurs when Checked changes.")]
    public event EventHandler? CheckedChanged;

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Space)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hot = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    /// <summary>The track: as tall as the control, twice as wide as tall (or the whole control when there is no text).</summary>
    private Rectangle TrackBounds
    {
        get
        {
            int h = Math.Max(4, ClientSize.Height - 2);
            int w = string.IsNullOrEmpty(Text) ? ClientSize.Width - 2 : Math.Min(ClientSize.Width - 2, h * 2);
            return new Rectangle(1, (ClientSize.Height - h) / 2, Math.Max(h, w), h);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var track = TrackBounds;
        var color = !Enabled ? Color.FromArgb(204, 204, 204) : _checked ? _onColor : _offColor;
        if (_hot && Enabled) color = ControlPaint.Light(color, 0.2f);
        using (var path = RoundRect(track, track.Height / 2f))
        using (var brush = new SolidBrush(color))
        {
            g.FillPath(brush, path);
        }

        int d = track.Height - 6;
        int x = _checked ? track.Right - d - 3 : track.Left + 3;
        using (var thumb = new SolidBrush(_thumbColor))
        {
            g.FillEllipse(thumb, x, track.Top + 3, d, d);
        }

        if (!string.IsNullOrEmpty(Text))
        {
            var textBounds = new Rectangle(track.Right + 6, 0, Math.Max(0, ClientSize.Width - track.Right - 6), ClientSize.Height);
            TextRenderer.DrawText(g, Text, Font, textBounds, Enabled ? ForeColor : SystemColors.GrayText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, ClientRectangle);
    }

    private static GraphicsPath RoundRect(Rectangle r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 90, 180);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 180);
        path.CloseFigure();
        return path;
    }
}
