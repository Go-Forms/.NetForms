using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

[DefaultEvent("CheckedChanged")]
[DefaultProperty("Checked")]
public class RadioButton : ButtonBase
{
    private const int CircleSize = 13;

    private bool _checked;
    private bool _autoCheck = true;
    private ContentAlignment _checkAlign = ContentAlignment.MiddleLeft;
    private Appearance _appearance = Appearance.Normal;

    public RadioButton()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        TextAlign = ContentAlignment.MiddleLeft;
        TabStop = false;
    }

    protected override Size DefaultSize => new Size(104, 24);

    [Description("Occurs whenever the 'checked' property changes value.")]
    public event EventHandler? CheckedChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Appearance property is changed on RadioButton.")]
    public event EventHandler? AppearanceChanged;

    [Category("Appearance")]
    [Description("Indicates whether the radio button is checked or not.")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            TabStop = value;
            Invalidate();
            if (value && _autoCheck) UncheckSiblings();
            OnCheckedChanged(EventArgs.Empty);
        }
    }

    [Category("Behavior")]
    [Description("Causes the radio button to automatically change state when clicked.")]
    [DefaultValue(true)]
    public bool AutoCheck
    {
        get => _autoCheck;
        set => _autoCheck = value;
    }

    [Category("Appearance")]
    [Description("Determines the location of the check box inside the control.")]
    [DefaultValue(typeof(ContentAlignment), "MiddleLeft")]
    [Localizable(true)]
    public ContentAlignment CheckAlign
    {
        get => _checkAlign;
        set
        {
            _checkAlign = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Controls whether the RadioButton appears as normal or as a Windows PushButton.")]
    [DefaultValue(Appearance.Normal)]
    [Localizable(true)]
    public Appearance Appearance
    {
        get => _appearance;
        set
        {
            if (_appearance == value) return;
            _appearance = value;
            OnAppearanceChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    protected virtual void OnCheckedChanged(EventArgs e) => CheckedChanged?.Invoke(this, e);
    protected virtual void OnAppearanceChanged(EventArgs e) => AppearanceChanged?.Invoke(this, e);

    /// <summary>Radio buttons in the same container form a group: checking one unchecks the others.</summary>
    private void UncheckSiblings()
    {
        var parent = Parent;
        if (parent == null) return;
        foreach (var sibling in parent.Controls)
        {
            if (sibling != this && sibling is RadioButton rb && rb._autoCheck && rb._checked)
            {
                rb.Checked = false;
            }
        }
    }

    public void PerformClick()
    {
        if (CanSelect) OnClick(EventArgs.Empty);
    }

    protected override void OnClick(EventArgs e)
    {
        if (_autoCheck) Checked = true;
        base.OnClick(e);
    }

    protected override void OnEnter(EventArgs e)
    {
        // Keyboard navigation onto a radio button checks it, as in WinForms.
        if (_autoCheck && !_checked && FindForm()?.KeyboardFocusCuesShown == true) Checked = true;
        base.OnEnter(e);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, proposedSize, CreateTextFormatFlags() & ~TextFormatFlags.WordBreak);
        if (_appearance == Appearance.Button) return base.GetPreferredSize(proposedSize);
        // As CheckBox, one pixel narrower (decision 110): the measured text + 18 across, + 4 down; no text 14x13.
        if (string.IsNullOrEmpty(Text)) return new Size(14 + Padding.Horizontal, 13 + Padding.Vertical);
        return new Size(text.Width + 18 + Padding.Horizontal, text.Height + 4 + Padding.Vertical);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var pad = Padding;
        var client = new Rectangle(pad.Left, pad.Top, Math.Max(0, Width - pad.Horizontal), Math.Max(0, Height - pad.Vertical));
        bool enabled = Enabled;
        bool pressed = enabled && IsPressedVisual;
        bool hot = enabled && MouseIsOver;

        if (_appearance == Appearance.Button)
        {
            var rect = ClientRectangle;
            bool down = _checked || pressed;
            Color face = !enabled ? Theme.ButtonFaceDisabled : down ? Theme.ButtonFacePressed : hot ? Theme.ButtonFaceHot : Theme.ButtonFace;
            Color border = !enabled ? Theme.ButtonBorderDisabled : down || hot ? Theme.ButtonBorderHot : Theme.ButtonBorder;
            using (var b = new SolidBrush(face)) g.FillRectangle(b, rect);
            using (var p = new Pen(border)) g.DrawRectangle(p, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            TextRenderer.DrawText(g, Text, Font, rect, enabled ? ForeColor : Theme.DisabledText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            base.OnPaint(pevent);
            return;
        }

        bool onRight = _checkAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight;
        int cx = onRight ? client.Right - CircleSize : client.X;
        int cy = _checkAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => client.Y,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => client.Bottom - CircleSize,
            _ => client.Y + (client.Height - CircleSize) / 2,
        };
        var circle = new Rectangle(cx, cy, CircleSize, CircleSize);

        Color fill = !enabled ? Theme.ButtonFaceDisabled : pressed ? Theme.CheckFillPressed : hot ? Theme.ButtonFaceHot : Theme.CheckFill;
        Color borderColor = !enabled ? Theme.ButtonBorderDisabled : hot || pressed ? Theme.CheckBorderHot : FlatStyle == FlatStyle.Flat ? ForeColor : Theme.CheckBorder;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(fill)) g.FillEllipse(b, circle);
        using (var p = new Pen(borderColor)) g.DrawEllipse(p, circle.X, circle.Y, circle.Width - 1, circle.Height - 1);
        if (_checked)
        {
            using var dot = new SolidBrush(enabled ? Theme.CheckMark : Theme.DisabledText);
            g.FillEllipse(dot, circle.X + 4, circle.Y + 4, circle.Width - 8, circle.Height - 8);
        }
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;

        var textRect = onRight
            ? new Rectangle(client.X, client.Y, Math.Max(0, client.Width - CircleSize - CheckBox.BoxTextGap), client.Height)
            : new Rectangle(circle.Right + CheckBox.BoxTextGap, client.Y, Math.Max(0, client.Right - circle.Right - CheckBox.BoxTextGap), client.Height);
        TextRenderer.DrawText(g, Text, Font, textRect, enabled ? ForeColor : Theme.DisabledText, CreateTextFormatFlags() | TextFormatFlags.NoPadding);

        if (Focused && ShowFocusCues && !string.IsNullOrEmpty(Text))
        {
            var textSize = TextRenderer.MeasureText(Text, Font, textRect.Size, CreateTextFormatFlags() | TextFormatFlags.NoPadding);
            var focus = new Rectangle(textRect.X - 1, textRect.Y + (textRect.Height - textSize.Height) / 2 - 1, Math.Min(textRect.Width, textSize.Width) + 2, textSize.Height + 2);
            ControlPaint.DrawFocusRectangle(g, focus, ForeColor, BackColor);
        }
        base.OnPaint(pevent);
    }

    public override string ToString() => base.ToString() + ", Checked: " + _checked;

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(false)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool TabStop { get => base.TabStop; set => base.TabStop = value; }

    [Category("Appearance")]
    [DefaultValue(typeof(ContentAlignment), "MiddleLeft")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override ContentAlignment TextAlign { get => base.TextAlign; set => base.TextAlign = value; }

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
