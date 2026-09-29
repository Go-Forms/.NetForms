using System.Drawing;
using System.Drawing.Printing;

namespace System.Windows.Forms;

/// <summary>
/// The printer's document properties, which the Properties button of <see cref="PrintDialog"/> and the Printer
/// button of <see cref="PageSetupDialog"/> open: paper, tray, orientation, colour, duplex and print quality, from
/// what the print backend reports for the printer (the driver's DocumentProperties on Windows, the PPD options
/// under CUPS). NetForms' own form, the same on Windows and Linux (decision 153).
/// </summary>
internal sealed class PrinterPropertiesForm : Form
{
    private readonly PageSettings _page;
    private readonly PaperSize[] _sizes;
    private readonly PaperSource[] _sources;
    private readonly PrinterResolution[] _resolutions;
    internal readonly ComboBox PaperBox;
    internal readonly ComboBox SourceBox;
    internal readonly RadioButton Portrait;
    internal readonly RadioButton Landscape;
    internal readonly CheckBox ColorBox;
    internal readonly ComboBox DuplexBox;
    internal readonly ComboBox QualityBox;
    internal readonly Button Ok;
    internal readonly Button Cancel;

    private static readonly Duplex[] s_duplex = [Duplex.Simplex, Duplex.Vertical, Duplex.Horizontal];

    public PrinterPropertiesForm(string printerName, PageSettings page, Duplex duplex)
    {
        _page = page;
        var printer = page.PrinterSettings;
        _sizes = printer.Get_PaperSizes();
        _sources = printer.Get_PaperSources();
        _resolutions = printer.Get_PrinterResolutions();
        static string S(string text) => SystemStrings.Get(text);

        Text = string.Format(S("{0} Document Properties"), printerName);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(400, 292);

        var paperBox = new GroupBox { Text = S("Paper"), Location = new Point(12, 8), Size = new Size(376, 84) };
        paperBox.Controls.Add(new Label { Text = S("Paper si&ze:"), Location = new Point(10, 24), AutoSize = true });
        PaperBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(140, 20), Size = new Size(226, 23) };
        foreach (var s in _sizes) PaperBox.Items.Add(s.PaperName);
        int paper = Array.FindIndex(_sizes, s => s.RawKind == page.PaperSize.RawKind && (s.Kind != PaperKind.Custom || s.PaperName == page.PaperSize.PaperName));
        if (PaperBox.Items.Count > 0) PaperBox.SelectedIndex = Math.Max(0, paper);
        paperBox.Controls.Add(new Label { Text = S("Paper &source:"), Location = new Point(10, 54), AutoSize = true });
        SourceBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(140, 50), Size = new Size(226, 23) };
        foreach (var s in _sources) SourceBox.Items.Add(s.SourceName);
        int source = Array.FindIndex(_sources, s => s.RawKind == page.PaperSource.RawKind);
        if (SourceBox.Items.Count > 0) SourceBox.SelectedIndex = Math.Max(0, source);
        paperBox.Controls.Add(PaperBox);
        paperBox.Controls.Add(SourceBox);

        var orientationBox = new GroupBox { Text = S("Orientation"), Location = new Point(12, 100), Size = new Size(140, 84) };
        Portrait = new RadioButton { Text = S("P&ortrait"), Location = new Point(10, 24), AutoSize = true, Checked = !page.Landscape };
        Landscape = new RadioButton { Text = S("L&andscape"), Location = new Point(10, 50), AutoSize = true, Checked = page.Landscape };
        orientationBox.Controls.Add(Portrait);
        orientationBox.Controls.Add(Landscape);

        ColorBox = new CheckBox { Text = S("Print in co&lor"), Location = new Point(166, 108), AutoSize = true, Checked = page.Color && printer.SupportsColor, Enabled = printer.SupportsColor };

        Controls.Add(new Label { Text = S("Print on &both sides:"), Location = new Point(166, 138), AutoSize = true });
        DuplexBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(166, 158), Size = new Size(222, 23), Enabled = printer.CanDuplex };
        DuplexBox.Items.AddRange([S("None"), S("Flip on long edge"), S("Flip on short edge")]);
        DuplexBox.SelectedIndex = Math.Max(0, Array.IndexOf(s_duplex, printer.CanDuplex ? duplex : Duplex.Simplex));

        Controls.Add(new Label { Text = S("Print &quality:"), Location = new Point(12, 198), AutoSize = true });
        QualityBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(166, 194), Size = new Size(222, 23) };
        foreach (var r in _resolutions) QualityBox.Items.Add(Describe(r));
        int quality = Array.FindIndex(_resolutions, r => r.Kind == page.PrinterResolution.Kind && r.X == page.PrinterResolution.X && r.Y == page.PrinterResolution.Y);
        if (QualityBox.Items.Count > 0) QualityBox.SelectedIndex = Math.Max(0, quality);
        QualityBox.Enabled = QualityBox.Items.Count > 1;

        Ok = new Button { Text = S("OK"), Size = new Size(84, 26), Location = new Point(210, 254), DialogResult = DialogResult.OK };
        Cancel = new Button { Text = S("Cancel"), Size = new Size(84, 26), Location = new Point(304, 254), DialogResult = DialogResult.Cancel };

        Controls.Add(paperBox);
        Controls.Add(orientationBox);
        Controls.Add(ColorBox);
        Controls.Add(DuplexBox);
        Controls.Add(QualityBox);
        Controls.Add(Ok);
        Controls.Add(Cancel);
        AcceptButton = Ok;
        CancelButton = Cancel;
    }

    /// <summary>The duplex chosen; it belongs to the printer settings, not to the page.</summary>
    public Duplex ChosenDuplex => DuplexBox.SelectedIndex >= 0 ? s_duplex[DuplexBox.SelectedIndex] : Duplex.Simplex;

    private static string Describe(PrinterResolution r) => r.Kind == PrinterResolutionKind.Custom
        ? r.X == r.Y ? $"{r.X} dpi" : $"{r.X} x {r.Y} dpi"
        : r.Kind.ToString();

    /// <summary>OK: the choices into the page settings (the DEVMODE the driver returns).</summary>
    public void Apply()
    {
        if (_sizes.Length > 0 && PaperBox.SelectedIndex >= 0) _page.PaperSize = _sizes[PaperBox.SelectedIndex];
        if (_sources.Length > 0 && SourceBox.SelectedIndex >= 0) _page.PaperSource = _sources[SourceBox.SelectedIndex];
        _page.Landscape = Landscape.Checked;
        if (ColorBox.Enabled) _page.Color = ColorBox.Checked;
        if (_resolutions.Length > 0 && QualityBox.SelectedIndex >= 0) _page.PrinterResolution = _resolutions[QualityBox.SelectedIndex];
    }
}
