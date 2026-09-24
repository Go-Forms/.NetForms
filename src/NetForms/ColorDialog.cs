using System.ComponentModel;
using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The colour picker. Windows has a system dialog for this and Linux does not, so - unlike the
/// file pickers - this one is our own form, built from our own controls, and looks the same on
/// both platforms (docs/PLAN.md).
/// </summary>
[DefaultProperty("Color")]
public class ColorDialog : CommonDialog
{
    private static readonly Color[] s_basicColors =
    {
        Color.FromArgb(255, 128, 128), Color.FromArgb(255, 255, 128), Color.FromArgb(128, 255, 128),
        Color.FromArgb(0, 255, 128), Color.FromArgb(128, 255, 255), Color.FromArgb(0, 128, 255),
        Color.FromArgb(255, 128, 192), Color.FromArgb(255, 128, 255),
        Color.FromArgb(255, 0, 0), Color.FromArgb(255, 255, 0), Color.FromArgb(128, 255, 0),
        Color.FromArgb(0, 255, 64), Color.FromArgb(0, 255, 255), Color.FromArgb(0, 128, 192),
        Color.FromArgb(128, 128, 192), Color.FromArgb(255, 0, 255),
        Color.FromArgb(128, 64, 64), Color.FromArgb(255, 128, 64), Color.FromArgb(0, 255, 0),
        Color.FromArgb(0, 128, 128), Color.FromArgb(0, 64, 128), Color.FromArgb(128, 128, 255),
        Color.FromArgb(128, 0, 64), Color.FromArgb(255, 0, 128),
        Color.FromArgb(128, 0, 0), Color.FromArgb(255, 128, 0), Color.FromArgb(0, 128, 0),
        Color.FromArgb(0, 128, 64), Color.FromArgb(0, 0, 255), Color.FromArgb(0, 0, 160),
        Color.FromArgb(128, 0, 128), Color.FromArgb(128, 0, 255),
        Color.FromArgb(64, 0, 0), Color.FromArgb(128, 64, 0), Color.FromArgb(0, 64, 0),
        Color.FromArgb(0, 64, 64), Color.FromArgb(0, 0, 128), Color.FromArgb(0, 0, 64),
        Color.FromArgb(64, 0, 64), Color.FromArgb(64, 0, 128),
        Color.Black, Color.FromArgb(128, 128, 0), Color.FromArgb(128, 128, 64),
        Color.FromArgb(128, 128, 128), Color.FromArgb(64, 128, 128), Color.FromArgb(192, 192, 192),
        Color.FromArgb(64, 0, 64), Color.White,
    };

    public ColorDialog() => Reset();

    [Category("Data")]
    [Description("The color selected in the dialog box.")]
    public Color Color { get; set; } = Color.Black;

    internal bool ShouldSerializeColor() => Color != Color.Black;

    [Description("The custom set of colors shown in the dialog box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int[] CustomColors { get; set; } = new int[16];

    [Category("Behavior")]
    [Description("Enables and disables the Define Custom Colors button.")]
    [DefaultValue(true)]
    public bool AllowFullOpen { get; set; } = true;

    [Category("Appearance")]
    [Description("Controls whether the custom color section of the dialog box is initially displayed.")]
    [DefaultValue(false)]
    public bool FullOpen { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the dialog box displays all available colors in the set of basic colors.")]
    [DefaultValue(false)]
    public bool AnyColor { get; set; }

    [Category("Behavior")]
    [Description("Indicates whether the dialog box will restrict users to only selecting solid colors.")]
    [DefaultValue(false)]
    public bool SolidColorOnly { get; set; }

    [Category("Behavior")]
    [Description("Controls whether the Help button is displayed.")]
    [DefaultValue(false)]
    public bool ShowHelp { get; set; }

    public override void Reset()
    {
        Color = Color.Black;
        CustomColors = new int[16];
        AllowFullOpen = true;
        FullOpen = false;
        AnyColor = false;
        SolidColorOnly = false;
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        using var form = new ColorPickerForm(this);
        return form.ShowDialog(OwnerWindow) == DialogResult.OK;
    }

    public override string ToString() => base.ToString() + ",  Color: " + Color;

    /// <summary>The dialog's form: a palette of basic colours, custom slots and RGB boxes.</summary>
    private sealed class ColorPickerForm : Form
    {
        private const int SwatchSize = 20;
        private const int Columns = 8;

        private readonly ColorDialog _owner;
        private readonly Panel _preview;
        private readonly NumericUpDown _red;
        private readonly NumericUpDown _green;
        private readonly NumericUpDown _blue;
        private readonly Button _addCustom;
        private Color _selected;
        private bool _updating;

        public ColorPickerForm(ColorDialog owner)
        {
            _owner = owner;
            _selected = owner.Color;

            Text = "Color";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(Columns * (SwatchSize + 4) + 210, 6 * (SwatchSize + 4) + 96);

            var basicLabel = new Label { Text = "&Basic colors:", Location = new Point(10, 8), AutoSize = true };
            var customLabel = new Label { Text = "&Custom colors:", Location = new Point(10, 4 * (SwatchSize + 4) + 34), AutoSize = true };
            Controls.Add(basicLabel);
            Controls.Add(customLabel);

            int right = Columns * (SwatchSize + 4) + 20;
            _preview = new Panel
            {
                Location = new Point(right, 28),
                Size = new Size(120, 48),
                BorderStyle = BorderStyle.Fixed3D,
                BackColor = _selected,
            };
            Controls.Add(new Label { Text = "Co&lor:", Location = new Point(right, 8), AutoSize = true });
            Controls.Add(_preview);

            _red = AddChannel("&Red:", right, 88, _selected.R);
            _green = AddChannel("&Green:", right, 116, _selected.G);
            _blue = AddChannel("Bl&ue:", right, 144, _selected.B);

            _addCustom = new Button
            {
                Text = "&Add to Custom Colors",
                Location = new Point(10, ClientSize.Height - 66),
                Size = new Size(right - 30, 24),
            };
            _addCustom.Click += (_, _) => AddToCustom();
            Controls.Add(_addCustom);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(80, 26) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(80, 26) };
            ok.Location = new Point(ClientSize.Width - 176, ClientSize.Height - 36);
            cancel.Location = new Point(ClientSize.Width - 90, ClientSize.Height - 36);
            ok.Click += (_, _) => { _owner.Color = _selected; Close(); };
            cancel.Click += (_, _) => Close();
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        private NumericUpDown AddChannel(string label, int x, int y, int value)
        {
            Controls.Add(new Label { Text = label, Location = new Point(x, y + 3), AutoSize = true });
            var box = new NumericUpDown
            {
                Location = new Point(x + 52, y),
                Size = new Size(64, 23),
                Minimum = 0,
                Maximum = 255,
                Value = value,
            };
            box.ValueChanged += (_, _) => ChannelChanged();
            Controls.Add(box);
            return box;
        }

        private void ChannelChanged()
        {
            if (_updating) return;
            SetSelected(Color.FromArgb((int)_red.Value, (int)_green.Value, (int)_blue.Value), fromChannels: true);
        }

        private void SetSelected(Color color, bool fromChannels = false)
        {
            _selected = color;
            _preview.BackColor = color;
            if (!fromChannels)
            {
                _updating = true;
                _red.Value = color.R;
                _green.Value = color.G;
                _blue.Value = color.B;
                _updating = false;
            }
            Invalidate();
        }

        private void AddToCustom()
        {
            var slots = _owner.CustomColors;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != 0 && i < slots.Length - 1) continue;
                slots[i] = _selected.ToArgb() & 0xFFFFFF;
                break;
            }
            Invalidate();
        }

        private Rectangle BasicSwatch(int index) =>
            new Rectangle(10 + index % Columns * (SwatchSize + 4), 28 + index / Columns * (SwatchSize + 4), SwatchSize, SwatchSize);

        private Rectangle CustomSwatch(int index) =>
            new Rectangle(10 + index % Columns * (SwatchSize + 4), 4 * (SwatchSize + 4) + 54 + index / Columns * (SwatchSize + 4), SwatchSize, SwatchSize);

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < s_basicColors.Length; i++)
            {
                if (BasicSwatch(i).Contains(e.Location))
                {
                    SetSelected(s_basicColors[i]);
                    return;
                }
            }
            for (int i = 0; i < _owner.CustomColors.Length; i++)
            {
                if (CustomSwatch(i).Contains(e.Location))
                {
                    SetSelected(Color.FromArgb(0xFF, Color.FromArgb(_owner.CustomColors[i])));
                    return;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using var border = new Pen(Theme.ButtonBorder);
            using var selectedBorder = new Pen(Theme.Accent, 2f);

            for (int i = 0; i < s_basicColors.Length; i++)
            {
                PaintSwatch(g, BasicSwatch(i), s_basicColors[i], border, selectedBorder);
            }
            for (int i = 0; i < _owner.CustomColors.Length; i++)
            {
                PaintSwatch(g, CustomSwatch(i), Color.FromArgb(0xFF, Color.FromArgb(_owner.CustomColors[i])), border, selectedBorder);
            }
            base.OnPaint(e);
        }

        private void PaintSwatch(Graphics g, Rectangle bounds, Color color, Pen border, Pen selectedBorder)
        {
            using (var brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, bounds);
            }
            bool isSelected = color.ToArgb() == _selected.ToArgb();
            g.DrawRectangle(isSelected ? selectedBorder : border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }
    }
}
