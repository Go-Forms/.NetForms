using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

public enum CheckState
{
    Unchecked = 0,
    Checked = 1,
    Indeterminate = 2,
}

public enum Appearance
{
    Normal = 0,
    Button = 1,
}

[DefaultEvent("CheckedChanged")]
[DefaultProperty("Checked")]
public class CheckBox : ButtonBase
{
    internal const int BoxSize = 13;
    internal const int BoxTextGap = 3;

    private CheckState _checkState = CheckState.Unchecked;
    private ContentAlignment _checkAlign = ContentAlignment.MiddleLeft;
    private Appearance _appearance = Appearance.Normal;
    private bool _autoCheck = true;
    private bool _threeState;

    public CheckBox()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        TextAlign = ContentAlignment.MiddleLeft;
    }

    protected override Size DefaultSize => new Size(104, 24);

    [Description("Occurs whenever the Check property is changed.")]
    public event EventHandler? CheckedChanged;

    [Description("Occurs whenever the CheckState property is changed.")]
    public event EventHandler? CheckStateChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the Appearance property is changed on CheckBox.")]
    public event EventHandler? AppearanceChanged;

    [Category("Appearance")]
    [Description("Indicates whether the component is in the checked state.")]
    [DefaultValue(false)]
    public bool Checked
    {
        get => _checkState != CheckState.Unchecked;
        set
        {
            if (value != Checked) CheckState = value ? CheckState.Checked : CheckState.Unchecked;
        }
    }

    [Category("Appearance")]
    [Description("Indicates the state of the component.")]
    [DefaultValue(CheckState.Unchecked)]
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value) return;
            bool oldChecked = Checked;
            _checkState = value;
            Invalidate();
            if (oldChecked != Checked) OnCheckedChanged(EventArgs.Empty);
            OnCheckStateChanged(EventArgs.Empty);
        }
    }

    [Category("Behavior")]
    [Description("Causes the check box to automatically change state when clicked.")]
    [DefaultValue(true)]
    public bool AutoCheck
    {
        get => _autoCheck;
        set => _autoCheck = value;
    }

    [Category("Behavior")]
    [Description("Indicates whether the CheckBox will allow three check states rather than two.")]
    [DefaultValue(false)]
    public bool ThreeState
    {
        get => _threeState;
        set => _threeState = value;
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
            if (_checkAlign == value) return;
            _checkAlign = value;
            Invalidate();
        }
    }

    [Category("Appearance")]
    [Description("Controls the appearance of the check box.")]
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
    protected virtual void OnCheckStateChanged(EventArgs e) => CheckStateChanged?.Invoke(this, e);
    protected virtual void OnAppearanceChanged(EventArgs e) => AppearanceChanged?.Invoke(this, e);

    protected override void OnClick(EventArgs e)
    {
        if (_autoCheck)
        {
            CheckState = _checkState switch
            {
                CheckState.Unchecked => CheckState.Checked,
                CheckState.Checked => _threeState ? CheckState.Indeterminate : CheckState.Unchecked,
                _ => CheckState.Unchecked,
            };
        }
        base.OnClick(e);
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        var text = TextRenderer.MeasureText(Text, Font, proposedSize, CreateTextFormatFlags() & ~TextFormatFlags.WordBreak);
        if (_appearance == Appearance.Button) return base.GetPreferredSize(proposedSize);
        // WinForms (visual styles, TextRenderer): the measured text + 19 across (box, its padding and the
        // gap) and + 4 down; no text: the box alone, 15x14. Checked against WinForms (decision 110):
        // "Check me" 79x19, "checkBox1" 83x19 as VS writes it.
        if (string.IsNullOrEmpty(Text)) return new Size(15 + Padding.Horizontal, 14 + Padding.Vertical);
        return new Size(text.Width + 19 + Padding.Horizontal, text.Height + 4 + Padding.Vertical);
    }

    /// <summary>Where the box sits for the current CheckAlign.</summary>
    internal Rectangle BoxRectangle(Rectangle client)
    {
        int x = _checkAlign switch
        {
            ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => client.X + (client.Width - BoxSize) / 2,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => client.Right - BoxSize,
            _ => client.X,
        };
        int y = _checkAlign switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => client.Y,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => client.Bottom - BoxSize,
            _ => client.Y + (client.Height - BoxSize) / 2,
        };
        return new Rectangle(x, y, BoxSize, BoxSize);
    }

    internal Rectangle TextRectangle(Rectangle client, Rectangle box)
    {
        bool boxOnRight = _checkAlign is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight;
        if (boxOnRight) return new Rectangle(client.X, client.Y, Math.Max(0, client.Width - BoxSize - BoxTextGap), client.Height);
        return new Rectangle(box.Right + BoxTextGap, client.Y, Math.Max(0, client.Right - box.Right - BoxTextGap), client.Height);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        if (_appearance == Appearance.Button)
        {
            PaintAsButton(pevent);
            return;
        }

        var g = pevent.Graphics;
        var pad = Padding;
        var client = new Rectangle(pad.Left, pad.Top, Math.Max(0, Width - pad.Horizontal), Math.Max(0, Height - pad.Vertical));
        var box = BoxRectangle(client);
        bool enabled = Enabled;
        bool pressed = enabled && IsPressedVisual;
        bool hot = enabled && MouseIsOver;

        PaintBox(g, box, _checkState, enabled, hot, pressed, FlatStyle == FlatStyle.Flat, ForeColor);

        var textRect = TextRectangle(client, box);
        var color = enabled ? ForeColor : Theme.DisabledText;
        TextRenderer.DrawText(g, Text, Font, textRect, color, CreateTextFormatFlags() | TextFormatFlags.NoPadding);

        if (Focused && ShowFocusCues && !string.IsNullOrEmpty(Text))
        {
            var textSize = TextRenderer.MeasureText(Text, Font, textRect.Size, CreateTextFormatFlags() | TextFormatFlags.NoPadding);
            var focus = new Rectangle(textRect.X - 1, textRect.Y + (textRect.Height - textSize.Height) / 2 - 1, Math.Min(textRect.Width, textSize.Width) + 2, textSize.Height + 2);
            ControlPaint.DrawFocusRectangle(g, focus, ForeColor, BackColor);
        }
        base.OnPaint(pevent);
    }

    internal static void PaintBox(Graphics g, Rectangle box, CheckState state, bool enabled, bool hot, bool pressed, bool flat, Color foreColor)
    {
        Color fill = !enabled ? Theme.ButtonFaceDisabled : pressed ? Theme.CheckFillPressed : hot ? Theme.ButtonFaceHot : Theme.CheckFill;
        Color border = !enabled ? Theme.ButtonBorderDisabled : hot || pressed ? Theme.CheckBorderHot : flat ? foreColor : Theme.CheckBorder;
        using (var b = new SolidBrush(fill)) g.FillRectangle(b, box);
        using (var p = new Pen(border)) g.DrawRectangle(p, box.X, box.Y, box.Width - 1, box.Height - 1);

        var mark = enabled ? Theme.CheckMark : Theme.DisabledText;
        if (state == CheckState.Checked)
        {
            using var pen = new Pen(mark, 2f);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.DrawLines(pen, new[] { new PointF(box.X + 3, box.Y + 6.5f), new PointF(box.X + 5.5f, box.Y + 9), new PointF(box.X + 10, box.Y + 3.5f) });
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
        }
        else if (state == CheckState.Indeterminate)
        {
            using var b = new SolidBrush(mark);
            g.FillRectangle(b, box.X + 3, box.Y + 3, box.Width - 6, box.Height - 6);
        }
    }

    private void PaintAsButton(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        var rect = ClientRectangle;
        bool enabled = Enabled;
        bool down = Checked || (enabled && IsPressedVisual);
        Color face = !enabled ? Theme.ButtonFaceDisabled : down ? Theme.ButtonFacePressed : enabled && MouseIsOver ? Theme.ButtonFaceHot : Theme.ButtonFace;
        Color border = !enabled ? Theme.ButtonBorderDisabled : down || MouseIsOver ? Theme.ButtonBorderHot : Theme.ButtonBorder;
        using (var b = new SolidBrush(face)) g.FillRectangle(b, rect);
        using (var p = new Pen(border)) g.DrawRectangle(p, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        var color = enabled ? ForeColor : Theme.DisabledText;
        TextRenderer.DrawText(g, Text, Font, rect, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        base.OnPaint(pevent);
    }

    public override string ToString() => base.ToString() + ", CheckState: " + (int)_checkState;

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

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
