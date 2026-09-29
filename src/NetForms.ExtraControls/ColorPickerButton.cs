using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace NetForms.ExtraControls;

/// <summary>
/// A button that shows a colour swatch and, when clicked, asks for a colour with the <see cref="ColorDialog"/>.
/// <see cref="SelectedColorChanged"/> tells when the user picked another one.
/// </summary>
[ToolboxBitmap(typeof(ColorPickerButton), "ColorPickerButton.png")]
[DefaultEvent(nameof(SelectedColorChanged))]
[DefaultProperty(nameof(SelectedColor))]
[Description("A button for choosing a colour.")]
public class ColorPickerButton : Button
{
    private Color _selectedColor = Color.Black;

    public ColorPickerButton()
    {
        TextAlign = ContentAlignment.MiddleRight;
    }

    protected override Size DefaultSize => new(110, 26);

    /// <summary>The text stands right of the swatch.</summary>
    [DefaultValue(ContentAlignment.MiddleRight)]
    public override ContentAlignment TextAlign
    {
        get => base.TextAlign;
        set => base.TextAlign = value;
    }

    [Category("Appearance")]
    [DefaultValue(typeof(Color), "Black")]
    [Bindable(true)]
    [Description("The colour chosen.")]
    public Color SelectedColor
    {
        get => _selectedColor;
        set
        {
            if (_selectedColor == value) return;
            _selectedColor = value;
            Invalidate();
            OnSelectedColorChanged(EventArgs.Empty);
        }
    }

    /// <summary>Lets the colour dialog define custom colours.</summary>
    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Whether the colour dialog lets the user define custom colours.")]
    public bool AllowFullOpen { get; set; } = true;

    [Category("Property Changed")]
    [Description("Occurs when SelectedColor changes.")]
    public event EventHandler? SelectedColorChanged;

    protected virtual void OnSelectedColorChanged(EventArgs e) => SelectedColorChanged?.Invoke(this, e);

    /// <summary>The dialog the click shows; a test or a derived button can answer it another way.</summary>
    protected virtual Color? AskForColor(Color current)
    {
        using var dialog = new ColorDialog { Color = current, AllowFullOpen = AllowFullOpen, FullOpen = false };
        return dialog.ShowDialog(FindForm()) == DialogResult.OK ? dialog.Color : null;
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (DesignMode) return;
        if (AskForColor(_selectedColor) is { } color) SelectedColor = color;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);
        int side = Math.Max(8, ClientSize.Height - 10);
        var swatch = new Rectangle(6, (ClientSize.Height - side) / 2, side, side);
        using (var brush = new SolidBrush(Enabled ? _selectedColor : SystemColors.Control))
        {
            pevent.Graphics.FillRectangle(brush, swatch);
        }
        pevent.Graphics.DrawRectangle(SystemPens.ControlDark, swatch);
    }
}
