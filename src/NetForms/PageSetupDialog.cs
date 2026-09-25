using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// Paper, tray, orientation and margins of a page. Members and what OK writes back are WinForms' (PageSetupDlg);
/// the dialog is NetForms' own form. Margins are shown in millimetres where the region is metric and
/// <see cref="EnableMetric"/> asks for it, else in inches, and are kept in hundredths of an inch.
/// </summary>
[DefaultProperty(nameof(Document))]
[Description("Displays a dialog box for modifying page settings, margins, and printer settings for a document.")]
public sealed class PageSetupDialog : CommonDialog
{
    private PrintDocument? _printDocument;
    private PageSettings? _pageSettings;
    private PrinterSettings? _printerSettings;
    private Margins? _minMargins;

    public PageSetupDialog() => Reset();

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the margin section of the dialog box.")]
    public bool AllowMargins { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the orientation section of the dialog box.")]
    public bool AllowOrientation { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the paper section of the dialog box.")]
    public bool AllowPaper { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the Printer button.")]
    public bool AllowPrinter { get; set; }

    [Category("Data")]
    [DefaultValue(null)]
    [Description("The PrintDocument to get PrinterSettings from.")]
    public PrintDocument? Document
    {
        get => _printDocument;
        set
        {
            _printDocument = value;
            if (_printDocument != null)
            {
                _pageSettings = _printDocument.DefaultPageSettings;
                _printerSettings = _printDocument.PrinterSettings;
            }
        }
    }

    [DefaultValue(false)]
    [Description("Indicates whether margins are shown in millimeters where the region uses the metric system.")]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    public bool EnableMetric { get; set; }

    [Category("Data")]
    [Description("The minimum margins the user is allowed to select, in hundredths of an inch.")]
    public Margins? MinMargins
    {
        get => _minMargins;
        set => _minMargins = value ?? new Margins(0, 0, 0, 0);
    }

    [Category("Data")]
    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The PageSettings modified by the dialog box.")]
    public PageSettings? PageSettings
    {
        get => _pageSettings;
        set
        {
            _pageSettings = value;
            _printDocument = null;
        }
    }

    [Category("Data")]
    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The PrinterSettings modified when the user clicks the Printer button.")]
    public PrinterSettings? PrinterSettings
    {
        get => _printerSettings;
        set
        {
            _printerSettings = value;
            _printDocument = null;
        }
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Enables and disables the Help button.")]
    public bool ShowHelp { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the Network button.")]
    public bool ShowNetwork { get; set; }

    public override void Reset()
    {
        AllowMargins = true;
        AllowOrientation = true;
        AllowPaper = true;
        AllowPrinter = true;
        MinMargins = null; // all zeros
        _pageSettings = null;
        _printDocument = null;
        _printerSettings = null;
        ShowHelp = false;
        ShowNetwork = true;
    }

    private void ResetMinMargins() => MinMargins = null;

    private bool ShouldSerializeMinMargins() =>
        _minMargins != null && (_minMargins.Left != 0 || _minMargins.Right != 0 || _minMargins.Top != 0 || _minMargins.Bottom != 0);

    /// <summary>Millimetres or inches, as WinForms decides: millimetres only with EnableMetric on a metric system.</summary>
    internal bool UsesMillimeters
    {
        get
        {
            if (!EnableMetric) return false;
            try
            {
                return RegionInfo.CurrentRegion.IsMetric;
            }
            catch (ArgumentException)
            {
                return true;
            }
        }
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        if (_pageSettings == null) throw new ArgumentException("PageSettings must be set to show the dialog box.");
        using var form = new PageSetupForm(this, _pageSettings);
        if (form.ShowDialog(OwnerWindow) != DialogResult.OK) return false;
        form.Apply();
        return true;
    }

    /// <summary>The dialog's form; internal so the tests can fill it in.</summary>
    internal sealed class PageSetupForm : Form
    {
        private readonly PageSetupDialog _owner;
        private readonly PageSettings _page;
        private readonly PaperSize[] _sizes;
        private readonly PaperSource[] _sources;
        internal readonly ComboBox PaperBox;
        internal readonly ComboBox SourceBox;
        internal readonly RadioButton Portrait;
        internal readonly RadioButton Landscape;
        internal readonly NumericUpDown LeftMargin;
        internal readonly NumericUpDown RightMargin;
        internal readonly NumericUpDown TopMargin;
        internal readonly NumericUpDown BottomMargin;
        internal readonly Panel Sample;
        internal readonly Button Ok;
        internal readonly Button Cancel;
        private readonly PrinterUnit _unit;

        public PageSetupForm(PageSetupDialog owner, PageSettings page)
        {
            _owner = owner;
            _page = page;
            var printer = owner._printerSettings ?? page.PrinterSettings;
            _sizes = printer.Get_PaperSizes();
            _sources = printer.Get_PaperSources();
            _unit = owner.UsesMillimeters ? PrinterUnit.HundredthsOfAMillimeter : PrinterUnit.ThousandthsOfAnInch;

            Text = "Page Setup";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 360);

            Sample = new Panel { Location = new Point(140, 8), Size = new Size(160, 120) };
            Sample.Paint += PaintSample;

            var paperBox = new GroupBox { Text = "Paper", Location = new Point(12, 136), Size = new Size(416, 84), Enabled = owner.AllowPaper };
            paperBox.Controls.Add(new Label { Text = "Si&ze:", Location = new Point(10, 24), AutoSize = true });
            PaperBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 20), Size = new Size(326, 23) };
            foreach (var s in _sizes) PaperBox.Items.Add(s.PaperName);
            PaperBox.SelectedIndex = Math.Max(0, IndexOfPaper(page.PaperSize));
            paperBox.Controls.Add(new Label { Text = "&Source:", Location = new Point(10, 54), AutoSize = true });
            SourceBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 50), Size = new Size(326, 23) };
            foreach (var s in _sources) SourceBox.Items.Add(s.SourceName);
            int sourceIndex = Array.FindIndex(_sources, s => s.RawKind == page.PaperSource.RawKind);
            if (SourceBox.Items.Count > 0) SourceBox.SelectedIndex = Math.Max(0, sourceIndex);
            paperBox.Controls.Add(PaperBox);
            paperBox.Controls.Add(SourceBox);

            var orientationBox = new GroupBox { Text = "Orientation", Location = new Point(12, 228), Size = new Size(120, 84), Enabled = owner.AllowOrientation };
            Portrait = new RadioButton { Text = "P&ortrait", Location = new Point(10, 24), AutoSize = true, Checked = !page.Landscape };
            Landscape = new RadioButton { Text = "L&andscape", Location = new Point(10, 50), AutoSize = true, Checked = page.Landscape };
            orientationBox.Controls.Add(Portrait);
            orientationBox.Controls.Add(Landscape);

            string unitName = _unit == PrinterUnit.HundredthsOfAMillimeter ? "millimeters" : "inches";
            var marginsBox = new GroupBox { Text = $"Margins ({unitName})", Location = new Point(142, 228), Size = new Size(286, 84), Enabled = owner.AllowMargins };
            var margins = PrinterUnitConvert.Convert(page.Margins, PrinterUnit.Display, _unit);
            LeftMargin = MarginBox(margins.Left, new Point(60, 20));
            RightMargin = MarginBox(margins.Right, new Point(200, 20));
            TopMargin = MarginBox(margins.Top, new Point(60, 50));
            BottomMargin = MarginBox(margins.Bottom, new Point(200, 50));
            marginsBox.Controls.Add(new Label { Text = "&Left:", Location = new Point(10, 23), AutoSize = true });
            marginsBox.Controls.Add(LeftMargin);
            marginsBox.Controls.Add(new Label { Text = "&Right:", Location = new Point(144, 23), AutoSize = true });
            marginsBox.Controls.Add(RightMargin);
            marginsBox.Controls.Add(new Label { Text = "&Top:", Location = new Point(10, 53), AutoSize = true });
            marginsBox.Controls.Add(TopMargin);
            marginsBox.Controls.Add(new Label { Text = "&Bottom:", Location = new Point(144, 53), AutoSize = true });
            marginsBox.Controls.Add(BottomMargin);

            Ok = new Button { Text = "OK", Size = new Size(80, 26), Location = new Point(262, 324), DialogResult = DialogResult.OK };
            Cancel = new Button { Text = "Cancel", Size = new Size(80, 26), Location = new Point(348, 324), DialogResult = DialogResult.Cancel };

            Controls.Add(Sample);
            Controls.Add(paperBox);
            Controls.Add(orientationBox);
            Controls.Add(marginsBox);
            Controls.Add(Ok);
            Controls.Add(Cancel);
            AcceptButton = Ok;
            CancelButton = Cancel;

            PaperBox.SelectedIndexChanged += (_, _) => Sample.Invalidate();
            Portrait.CheckedChanged += (_, _) => Sample.Invalidate();
            foreach (var box in new[] { LeftMargin, RightMargin, TopMargin, BottomMargin }) box.ValueChanged += (_, _) => Sample.Invalidate();
        }

        private NumericUpDown MarginBox(int value, Point location)
        {
            // Shown in inches or millimetres with two decimals; the values are thousandths of an inch or hundredths of a mm.
            var box = new NumericUpDown
            {
                Location = location,
                Size = new Size(70, 23),
                DecimalPlaces = 2,
                Increment = 0.1m,
                Maximum = 1000,
            };
            box.Value = Math.Min(box.Maximum, value / (_unit == PrinterUnit.HundredthsOfAMillimeter ? 100m : 1000m));
            return box;
        }

        private int IndexOfPaper(PaperSize paper)
        {
            for (int i = 0; i < _sizes.Length; i++)
            {
                if (_sizes[i].RawKind == paper.RawKind && (paper.Kind != PaperKind.Custom || _sizes[i].PaperName == paper.PaperName)) return i;
            }
            return -1;
        }

        private int ToUnit(NumericUpDown box) => (int)Math.Round(box.Value * (_unit == PrinterUnit.HundredthsOfAMillimeter ? 100m : 1000m));

        private Margins CurrentMargins()
        {
            var margins = PrinterUnitConvert.Convert(new Margins(ToUnit(LeftMargin), ToUnit(RightMargin), ToUnit(TopMargin), ToUnit(BottomMargin)), _unit, PrinterUnit.Display);
            if (_owner.MinMargins is { } min)
            {
                margins.Left = Math.Max(margins.Left, min.Left);
                margins.Right = Math.Max(margins.Right, min.Right);
                margins.Top = Math.Max(margins.Top, min.Top);
                margins.Bottom = Math.Max(margins.Bottom, min.Bottom);
            }
            return margins;
        }

        private void PaintSample(object? sender, PaintEventArgs e)
        {
            // The page, to scale, with the margins dotted in.
            var paper = _sizes.Length > 0 ? _sizes[Math.Max(0, PaperBox.SelectedIndex)] : _page.PaperSize;
            float w = Landscape.Checked ? paper.Height : paper.Width;
            float h = Landscape.Checked ? paper.Width : paper.Height;
            if (w <= 0 || h <= 0) return;
            var area = Sample.ClientRectangle;
            area.Inflate(-4, -4);
            float k = Math.Min(area.Width / w, area.Height / h);
            var page = new RectangleF(area.X + (area.Width - w * k) / 2, area.Y + (area.Height - h * k) / 2, w * k, h * k);
            e.Graphics.FillRectangle(Brushes.Gray, page.X + 3, page.Y + 3, page.Width, page.Height);
            e.Graphics.FillRectangle(Brushes.White, page);
            e.Graphics.DrawRectangle(Pens.Black, page.X, page.Y, page.Width, page.Height);
            var m = CurrentMargins();
            using var pen = new Pen(Color.DarkGray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            e.Graphics.DrawRectangle(pen, page.X + m.Left * k, page.Y + m.Top * k,
                Math.Max(0, page.Width - (m.Left + m.Right) * k), Math.Max(0, page.Height - (m.Top + m.Bottom) * k));
        }

        /// <summary>OK: the choices into the PageSettings, as WinForms' UpdateSettings.</summary>
        public void Apply()
        {
            if (_owner.AllowPaper && _sizes.Length > 0 && PaperBox.SelectedIndex >= 0) _page.PaperSize = _sizes[PaperBox.SelectedIndex];
            if (_owner.AllowPaper && _sources.Length > 0 && SourceBox.SelectedIndex >= 0) _page.PaperSource = _sources[SourceBox.SelectedIndex];
            if (_owner.AllowOrientation) _page.Landscape = Landscape.Checked;
            if (_owner.AllowMargins) _page.Margins = CurrentMargins();
        }
    }
}
