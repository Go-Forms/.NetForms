using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NetForms.ExtraControls;

/// <summary>
/// A <see cref="Panel"/> whose background is a gradient from <see cref="StartColor"/> to <see cref="EndColor"/>.
/// A container: controls dropped on it in the designer become its children.
/// </summary>
[ToolboxBitmap(typeof(GradientPanel), "GradientPanel.png")]
[Description("A panel with a gradient background.")]
public class GradientPanel : Panel
{
    private Color _startColor = Color.SteelBlue;
    private Color _endColor = Color.MidnightBlue;
    private LinearGradientMode _mode = LinearGradientMode.Vertical;

    public GradientPanel()
    {
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "SteelBlue")]
    [Description("The colour the gradient starts with.")]
    public Color StartColor
    {
        get => _startColor;
        set { _startColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "MidnightBlue")]
    [Description("The colour the gradient ends with.")]
    public Color EndColor
    {
        get => _endColor;
        set { _endColor = value; Invalidate(); }
    }

    [Category("Appearance")]
    [DefaultValue(LinearGradientMode.Vertical)]
    [Description("The direction of the gradient.")]
    public LinearGradientMode GradientMode
    {
        get => _mode;
        set { _mode = value; Invalidate(); }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var r = ClientRectangle;
        if (r.Width <= 0 || r.Height <= 0) return;
        using var brush = new LinearGradientBrush(r, _startColor, _endColor, _mode);
        e.Graphics.FillRectangle(brush, r);
    }
}
