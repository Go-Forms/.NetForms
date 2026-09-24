using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

public interface IButtonControl
{
    DialogResult DialogResult { get; set; }
    void NotifyDefault(bool value);
    void PerformClick();
}

public class Button : ButtonBase, IButtonControl
{
    private DialogResult _dialogResult = DialogResult.None;

    public Button()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
    }

    [Category("Behavior")]
    [Description("The dialog-box result produced in a modal form by clicking the button.")]
    [DefaultValue(DialogResult.None)]
    public virtual DialogResult DialogResult
    {
        get => _dialogResult;
        set => _dialogResult = value;
    }

    public virtual void NotifyDefault(bool value) => IsDefault = value;

    public void PerformClick()
    {
        if (CanSelect) OnClick(EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        var form = FindForm();
        if (form != null && _dialogResult != DialogResult.None)
        {
            form.DialogResult = _dialogResult;
        }
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        bool enabled = Enabled;
        bool pressed = enabled && IsPressedVisual;
        bool hot = enabled && MouseIsOver && !pressed;

        Color face, border;
        if (!enabled)
        {
            face = Theme.ButtonFaceDisabled;
            border = Theme.ButtonBorderDisabled;
        }
        else if (pressed)
        {
            face = Theme.ButtonFacePressed;
            border = Theme.ButtonBorderPressed;
        }
        else if (hot)
        {
            face = Theme.ButtonFaceHot;
            border = Theme.ButtonBorderHot;
        }
        else
        {
            face = UseVisualStyleBackColor ? Theme.ButtonFace : BackColor;
            border = IsDefault || Focused ? Theme.ButtonBorderDefault : Theme.ButtonBorder;
        }

        bool flat = FlatStyle == FlatStyle.Flat;
        int borderSize = 1;
        if (flat)
        {
            // FlatAppearance: its colors where set, else shades of BackColor; a BorderSize of 0 draws no border.
            var look = FlatAppearance;
            face = pressed ? (look.MouseDownBackColor.IsEmpty ? ControlPaint.Dark(BackColor, 0.1f) : look.MouseDownBackColor)
                : hot ? (look.MouseOverBackColor.IsEmpty ? ControlPaint.Light(BackColor, 0.2f) : look.MouseOverBackColor)
                : BackColor;
            border = look.BorderColor.IsEmpty ? ForeColor : look.BorderColor;
            borderSize = look.BorderSize;
        }

        using (var brush = new SolidBrush(face))
        {
            g.FillRectangle(brush, rect);
        }
        using (var pen = new Pen(border))
        {
            for (int i = 0; i < borderSize && i * 2 < Math.Min(rect.Width, rect.Height); i++)
                g.DrawRectangle(pen, rect.X + i, rect.Y + i, rect.Width - 1 - 2 * i, rect.Height - 1 - 2 * i);
            if (enabled && (IsDefault || Focused) && !hot && !pressed && !flat)
            {
                // WinForms draws the default button with a doubled accent border.
                g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
            }
        }

        var textRect = rect;
        textRect.Inflate(-3, -3);
        if (pressed) textRect.Offset(0, 0);
        var pad = Padding;
        textRect = new Rectangle(textRect.X + pad.Left, textRect.Y + pad.Top, Math.Max(0, textRect.Width - pad.Horizontal), Math.Max(0, textRect.Height - pad.Vertical));
        if (Image is { } image)
        {
            var (imageRect, rest) = LayoutImageAndText(textRect, image.Size);
            if (enabled) g.DrawImage(image, imageRect);
            else ControlPaint.DrawImageDisabled(g, image, imageRect.X, imageRect.Y, face);
            textRect = rest;
        }
        var textColor = enabled ? ForeColor : Theme.DisabledText;
        TextRenderer.DrawText(g, Text, Font, textRect, textColor, CreateTextFormatFlags() | TextFormatFlags.NoPadding);

        if (Focused && ShowFocusCues)
        {
            var focus = rect;
            focus.Inflate(-3, -3);
            ControlPaint.DrawFocusRectangle(g, focus, ForeColor, face);
        }

        base.OnPaint(pevent);
    }

    public override string ToString() => base.ToString();

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    private AutoSizeMode _autoSizeMode = AutoSizeMode.GrowOnly;

    /// <summary>
    /// Only Button has it (not ButtonBase): an AutoSize button keeps the size it was given if that is
    /// larger (GrowOnly), while check boxes and radio buttons always fit their content.
    /// </summary>
    [Category("Layout")]
    [DefaultValue(AutoSizeMode.GrowOnly)]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public AutoSizeMode AutoSizeMode
    {
        get => _autoSizeMode;
        set
        {
            if (_autoSizeMode == value) return;
            _autoSizeMode = value;
            if (AutoSize) AdjustSizeToPreferred();
        }
    }

    internal override AutoSizeMode AutoSizeModeCore => _autoSizeMode;

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? DoubleClick
    {
        add => base.DoubleClick += value;
        remove => base.DoubleClick -= value;
    }

    [Category("Action")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event MouseEventHandler? MouseDoubleClick
    {
        add => base.MouseDoubleClick += value;
        remove => base.MouseDoubleClick -= value;
    }
}
