using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;

namespace System.Windows.Forms;

/// <summary>
/// The font picker. Like <see cref="ColorDialog"/> this is our own form rather than a system
/// dialog, so it looks and behaves the same on Windows and Linux.
/// </summary>
[DefaultEvent("Apply")]
[DefaultProperty("Font")]
public class FontDialog : CommonDialog
{
    private Font? _font;

    public FontDialog() => Reset();

    [Category("Data")]
    [Description("The font selected in the dialog box.")]
    public Font Font
    {
        get => _font ??= Control.DefaultFont;
        set => _font = value;
    }

    internal bool ShouldSerializeFont() => !Font.Equals(Control.DefaultFont);

    [Category("Data")]
    [Description("The color selected in the dialog box.")]
    [DefaultValue(typeof(Color), "Black")]
    public Color Color { get; set; } = Color.Black;

    [Category("Behavior")]
    [Description("Controls whether to show a color choice.")]
    [DefaultValue(false)]
    public bool ShowColor { get; set; }

    [Category("Behavior")]
    [Description("Controls whether to show the underline, strikeout, and font color selections.")]
    [DefaultValue(true)]
    public bool ShowEffects { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether to show the Apply button.")]
    [DefaultValue(false)]
    public bool ShowApply { get; set; }

    [Category("Behavior")]
    [Description("Controls whether to show the Help button.")]
    [DefaultValue(false)]
    public bool ShowHelp { get; set; }

    [Category("Behavior")]
    [Description("Controls whether GDI font simulations are allowed.")]
    [DefaultValue(true)]
    public bool AllowSimulations { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether vector fonts can be selected.")]
    [DefaultValue(true)]
    public bool AllowVectorFonts { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether vertical fonts can be selected.")]
    [DefaultValue(true)]
    public bool AllowVerticalFonts { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether the character set of the font can be changed.")]
    [DefaultValue(true)]
    public bool AllowScriptChange { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether only fixed pitch fonts can be selected.")]
    [DefaultValue(false)]
    public bool FixedPitchOnly { get; set; }

    [Category("Behavior")]
    [Description("Controls whether to report an error if the selected font does not exist.")]
    [DefaultValue(false)]
    public bool FontMustExist { get; set; }

    [Category("Behavior")]
    [Description("Controls whether to exclude OEM and Symbol character sets.")]
    [DefaultValue(false)]
    public bool ScriptsOnly { get; set; }

    [Category("Data")]
    [Description("The minimum point size that can be Selected (or zero to disable).")]
    [DefaultValue(0)]
    public int MinSize { get; set; }

    [Category("Data")]
    [Description("The maximum point size that can be Selected (or zero to disable).")]
    [DefaultValue(0)]
    public int MaxSize { get; set; }

    [Description("Occurs when the user clicks the Apply button.")]
    public event EventHandler? Apply;

    protected virtual void OnApply(EventArgs e) => Apply?.Invoke(this, e);

    public override void Reset()
    {
        _font = null;
        Color = Color.Black;
        ShowColor = false;
        ShowEffects = true;
        ShowApply = false;
        MinSize = 0;
        MaxSize = 0;
        FixedPitchOnly = false;
        FontMustExist = false;
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        using var form = new FontPickerForm(this);
        return form.ShowDialog(OwnerWindow) == DialogResult.OK;
    }

    public override string ToString() => base.ToString() + ", Font: " + Font;

    private sealed class FontPickerForm : Form
    {
        private static readonly float[] s_sizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };

        private readonly FontDialog _owner;
        private readonly ListBox _families;
        private readonly ListBox _styles;
        private readonly ListBox _sizes;
        private readonly TextBox _familyBox;
        private readonly TextBox _styleBox;
        private readonly TextBox _sizeBox;
        private readonly CheckBox _strikeout;
        private readonly CheckBox _underline;
        private readonly Panel _sample;
        private Font _selected;

        public FontPickerForm(FontDialog owner)
        {
            _owner = owner;
            _selected = owner.Font;

            Text = "Font";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(440, 330);

            Controls.Add(new Label { Text = "&Font:", Location = new Point(12, 8), AutoSize = true });
            Controls.Add(new Label { Text = "Font st&yle:", Location = new Point(212, 8), AutoSize = true });
            Controls.Add(new Label { Text = "&Size:", Location = new Point(338, 8), AutoSize = true });

            _familyBox = new TextBox { Location = new Point(12, 26), Size = new Size(190, 23), Text = _selected.Name };
            _styleBox = new TextBox { Location = new Point(212, 26), Size = new Size(118, 23), Text = StyleName(_selected.Style) };
            _sizeBox = new TextBox { Location = new Point(338, 26), Size = new Size(88, 23), Text = _selected.Size.ToString(System.Globalization.CultureInfo.CurrentCulture) };
            Controls.Add(_familyBox);
            Controls.Add(_styleBox);
            Controls.Add(_sizeBox);

            _families = new ListBox { Location = new Point(12, 52), Size = new Size(190, 130) };
            _styles = new ListBox { Location = new Point(212, 52), Size = new Size(118, 130) };
            _sizes = new ListBox { Location = new Point(338, 52), Size = new Size(88, 130) };
            Controls.Add(_families);
            Controls.Add(_styles);
            Controls.Add(_sizes);

            foreach (var family in InstalledFamilies()) _families.Items.Add(family);
            _styles.Items.AddRange(new object[] { "Regular", "Bold", "Italic", "Bold Italic" });
            foreach (var size in s_sizes)
            {
                if (owner.MinSize > 0 && size < owner.MinSize) continue;
                if (owner.MaxSize > 0 && size > owner.MaxSize) continue;
                _sizes.Items.Add(size.ToString(System.Globalization.CultureInfo.CurrentCulture));
            }

            _families.SelectedIndexChanged += (_, _) => Recompose();
            _styles.SelectedIndexChanged += (_, _) => Recompose();
            _sizes.SelectedIndexChanged += (_, _) => Recompose();

            _underline = new CheckBox { Text = "&Underline", Location = new Point(12, 190), AutoSize = true, Checked = _selected.Underline, Visible = owner.ShowEffects };
            _strikeout = new CheckBox { Text = "Stri&keout", Location = new Point(112, 190), AutoSize = true, Checked = _selected.Strikeout, Visible = owner.ShowEffects };
            _underline.CheckedChanged += (_, _) => Recompose();
            _strikeout.CheckedChanged += (_, _) => Recompose();
            Controls.Add(_underline);
            Controls.Add(_strikeout);

            _sample = new Panel
            {
                Location = new Point(12, 216),
                Size = new Size(414, 66),
                BorderStyle = BorderStyle.Fixed3D,
                BackColor = SystemColors.Window,
            };
            _sample.Paint += PaintSample;
            Controls.Add(_sample);

            var ok = new Button { Text = "OK", Size = new Size(80, 26), Location = new Point(ClientSize.Width - 176, ClientSize.Height - 36) };
            var cancel = new Button { Text = "Cancel", Size = new Size(80, 26), Location = new Point(ClientSize.Width - 90, ClientSize.Height - 36) };
            ok.Click += (_, _) =>
            {
                _owner.Font = _selected;
                DialogResult = DialogResult.OK;
                Close();
            };
            cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;

            if (owner.ShowApply)
            {
                var apply = new Button { Text = "&Apply", Size = new Size(80, 26), Location = new Point(ClientSize.Width - 262, ClientSize.Height - 36) };
                apply.Click += (_, _) => { _owner.Font = _selected; _owner.OnApply(EventArgs.Empty); };
                Controls.Add(apply);
            }

            SelectCurrent();
        }

        private IEnumerable<string> InstalledFamilies()
        {
            var names = new List<string>();
            foreach (var family in FontFamily.Families) names.Add(family.Name);
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        private void SelectCurrent()
        {
            int family = _families.FindStringExact(_selected.Name);
            if (family >= 0) _families.SelectedIndex = family;
            _styles.SelectedIndex = _styles.Items.IndexOf(StyleName(_selected.Style));
            int size = _sizes.Items.IndexOf(_selected.Size.ToString(System.Globalization.CultureInfo.CurrentCulture));
            if (size >= 0) _sizes.SelectedIndex = size;
        }

        private static string StyleName(FontStyle style)
        {
            bool bold = (style & FontStyle.Bold) != 0;
            bool italic = (style & FontStyle.Italic) != 0;
            return bold && italic ? "Bold Italic" : bold ? "Bold" : italic ? "Italic" : "Regular";
        }

        private void Recompose()
        {
            string family = _families.SelectedItem as string ?? _selected.Name;
            string styleName = _styles.SelectedItem as string ?? "Regular";
            float size = _selected.Size;
            if (_sizes.SelectedItem is string text && float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out float parsed))
            {
                size = parsed;
            }

            var style = styleName switch
            {
                "Bold" => FontStyle.Bold,
                "Italic" => FontStyle.Italic,
                "Bold Italic" => FontStyle.Bold | FontStyle.Italic,
                _ => FontStyle.Regular,
            };
            if (_underline.Checked) style |= FontStyle.Underline;
            if (_strikeout.Checked) style |= FontStyle.Strikeout;

            _selected = new Font(family, size, style);
            _familyBox.Text = family;
            _styleBox.Text = styleName;
            _sizeBox.Text = size.ToString(System.Globalization.CultureInfo.CurrentCulture);
            _sample.Invalidate();
        }

        private void PaintSample(object? sender, PaintEventArgs e)
        {
            var rect = _sample.ClientRectangle;
            TextRenderer.DrawText(e.Graphics, "AaBbYyZz", _selected, rect, _owner.ShowColor ? _owner.Color : SystemColors.ControlText,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }
}
