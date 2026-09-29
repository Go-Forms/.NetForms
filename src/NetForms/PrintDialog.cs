using System.Linq;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;

namespace System.Windows.Forms;

/// <summary>
/// Chooses the printer, the pages and the copies. The members and what OK writes back to
/// <see cref="PrinterSettings"/> are those of WinForms (PrintDlg); the dialog itself is NetForms' own form,
/// the same on Windows and Linux, listing the printers the print backend reports (decision 153).
/// </summary>
[DefaultProperty(nameof(Document))]
[Description("Displays a dialog box for selecting a printer and choosing which sections of the document to print.")]
public sealed class PrintDialog : CommonDialog
{
    private PrinterSettings? _printerSettings;
    private PrintDocument? _printDocument;

    public PrintDialog() => Reset();

    [DefaultValue(false)]
    [Description("Enables and disables the Current Page option button.")]
    public bool AllowCurrentPage { get; set; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Enables and disables the Pages option button.")]
    public bool AllowSomePages { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the Print to file check box.")]
    public bool AllowPrintToFile { get; set; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Enables and disables the Selection option button.")]
    public bool AllowSelection { get; set; }

    [Category("Data")]
    [DefaultValue(null)]
    [Description("The PrintDocument to get PrinterSettings from.")]
    public PrintDocument? Document
    {
        get => _printDocument;
        set
        {
            _printDocument = value;
            _printerSettings = _printDocument is null ? new PrinterSettings() : _printDocument.PrinterSettings;
        }
    }

    private PageSettings PageSettings => Document is null ? PrinterSettings.DefaultPageSettings : Document.DefaultPageSettings;

    [Category("Data")]
    [DefaultValue(null)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The PrinterSettings the dialog box will be modifying.")]
    public PrinterSettings PrinterSettings
    {
        get => _printerSettings ??= new PrinterSettings();
        set
        {
            if (value != PrinterSettings)
            {
                _printerSettings = value;
                _printDocument = null;
            }
        }
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Specifies whether the Print to file check box is checked.")]
    public bool PrintToFile { get; set; }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Enables and disables the Help button.")]
    public bool ShowHelp { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("Enables and disables the Network button.")]
    public bool ShowNetwork { get; set; }

    /// <summary>Win32 chooses between PrintDlg and PrintDlgEx with it; NetForms' dialog is the same either way.</summary>
    [DefaultValue(false)]
    [Description("Indicates whether the Windows XP style dialog should be used.")]
    public bool UseEXDialog { get; set; }

    public override void Reset()
    {
        AllowCurrentPage = false;
        AllowSomePages = false;
        AllowPrintToFile = true;
        AllowSelection = false;
        _printDocument = null;
        PrintToFile = false;
        _printerSettings = null;
        ShowHelp = false;
        ShowNetwork = true;
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        var settings = PrinterSettings;
        if (AllowSomePages)
        {
            // As PrintDlg: page numbers outside the allowed range are the caller's error.
            if (settings.FromPage < settings.MinimumPage || settings.FromPage > settings.MaximumPage)
                throw new ArgumentException("Value of FromPage is out of the range MinimumPage to MaximumPage.");
            if (settings.ToPage < settings.MinimumPage || settings.ToPage > settings.MaximumPage)
                throw new ArgumentException("Value of ToPage is out of the range MinimumPage to MaximumPage.");
            if (settings.ToPage < settings.FromPage)
                throw new ArgumentException("Value of FromPage is out of the range MinimumPage to MaximumPage.");
        }

        using var form = new PrintForm(this);
        if (form.ShowDialog(OwnerWindow) != DialogResult.OK) return false;
        form.Apply();
        return true;
    }

    /// <summary>The Help button: <see cref="CommonDialog.HelpRequest"/>, as the Win32 dialog's help hook raises it.</summary>
    internal void RaiseHelpRequest() => OnHelpRequest(EventArgs.Empty);

    /// <summary>The dialog's form; internal so the tests can fill it in.</summary>
    internal sealed class PrintForm : Form
    {
        private readonly PrintDialog _owner;
        internal readonly ComboBox Printer;
        internal readonly Label Status;
        internal readonly Button Properties;
        internal readonly CheckBox ToFile;
        internal readonly RadioButton AllPages;
        internal readonly RadioButton Selection;
        internal readonly RadioButton CurrentPage;
        internal readonly RadioButton SomePages;
        internal readonly NumericUpDown FromPage;
        internal readonly NumericUpDown ToPage;
        internal readonly NumericUpDown Copies;
        internal readonly CheckBox Collate;
        internal readonly Button Ok;
        internal readonly Button Cancel;
        internal readonly Button? Help;

        /// <summary>What the Properties button chose for the printer in <see cref="_propertiesPrinter"/>; applied on OK.</summary>
        private PageSettings? _properties;
        private Duplex _duplex;
        private string? _propertiesPrinter;

        /// <summary>The message a page range out of bounds shows; tests read it instead of a message box.</summary>
        internal static Action<string>? ShowError { get; set; }

        public PrintForm(PrintDialog owner)
        {
            _owner = owner;
            var settings = owner.PrinterSettings;
            static string S(string text) => SystemStrings.Get(text);

            Text = S("Print");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(476, 300);

            var printerBox = new GroupBox { Text = S("Printer"), Location = new Point(12, 8), Size = new Size(452, 96) };
            printerBox.Controls.Add(new Label { Text = S("&Name:"), Location = new Point(10, 26), AutoSize = true });
            Printer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(90, 22), Size = new Size(252, 23) };
            foreach (var name in PrinterSettings.InstalledPrinters) Printer.Items.Add(name);
            int current = Printer.Items.IndexOf(settings.PrinterName);
            if (current >= 0) Printer.SelectedIndex = current;
            else if (Printer.Items.Count > 0) Printer.SelectedIndex = 0;
            Properties = new Button { Text = S("P&roperties..."), Location = new Point(350, 21), Size = new Size(92, 25) };
            Properties.Click += (_, _) => ShowProperties();
            printerBox.Controls.Add(new Label { Text = S("Status:"), Location = new Point(10, 56), AutoSize = true });
            Status = new Label { Location = new Point(90, 56), Size = new Size(210, 18), AutoEllipsis = true };
            ToFile = new CheckBox { Text = S("Print to fi&le"), Location = new Point(326, 55), AutoSize = true, Checked = owner.PrintToFile, Enabled = owner.AllowPrintToFile };
            printerBox.Controls.Add(Printer);
            printerBox.Controls.Add(Properties);
            printerBox.Controls.Add(Status);
            printerBox.Controls.Add(ToFile);
            Printer.SelectedIndexChanged += (_, _) => UpdateStatus();
            ToFile.CheckedChanged += (_, _) => UpdateStatus();

            var rangeBox = new GroupBox { Text = S("Page range"), Location = new Point(12, 112), Size = new Size(266, 136) };
            AllPages = new RadioButton { Text = S("&All"), Location = new Point(10, 22), AutoSize = true };
            Selection = new RadioButton { Text = S("Selectio&n"), Location = new Point(10, 46), AutoSize = true, Enabled = owner.AllowSelection };
            CurrentPage = new RadioButton { Text = S("Current pa&ge"), Location = new Point(10, 70), AutoSize = true, Enabled = owner.AllowCurrentPage };
            SomePages = new RadioButton { Text = S("Pa&ges"), Location = new Point(10, 96), AutoSize = true, Enabled = owner.AllowSomePages };
            int min = Math.Max(0, settings.MinimumPage), max = Math.Max(min, settings.MaximumPage);
            FromPage = new NumericUpDown { Location = new Point(134, 94), Size = new Size(52, 23), Minimum = min, Maximum = max, Enabled = owner.AllowSomePages };
            ToPage = new NumericUpDown { Location = new Point(208, 94), Size = new Size(52, 23), Minimum = min, Maximum = max, Enabled = owner.AllowSomePages };
            FromPage.Value = Math.Clamp(settings.FromPage, min, max);
            ToPage.Value = Math.Clamp(settings.ToPage, min, max);
            rangeBox.Controls.Add(AllPages);
            rangeBox.Controls.Add(Selection);
            rangeBox.Controls.Add(CurrentPage);
            rangeBox.Controls.Add(SomePages);
            rangeBox.Controls.Add(new Label { Text = S("from"), Location = new Point(102, 97), AutoSize = true });
            rangeBox.Controls.Add(FromPage);
            rangeBox.Controls.Add(new Label { Text = S("to"), Location = new Point(188, 97), AutoSize = true });
            rangeBox.Controls.Add(ToPage);
            var range = settings.PrintRange;
            (range switch
            {
                PrintRange.Selection when owner.AllowSelection => Selection,
                PrintRange.CurrentPage when owner.AllowCurrentPage => CurrentPage,
                PrintRange.SomePages when owner.AllowSomePages => SomePages,
                _ => AllPages,
            }).Checked = true;
            // Typing a page number picks "Pages", as in the Win32 dialog.
            FromPage.ValueChanged += (_, _) => SomePages.Checked = true;
            ToPage.ValueChanged += (_, _) => SomePages.Checked = true;

            var copiesBox = new GroupBox { Text = S("Copies"), Location = new Point(288, 112), Size = new Size(176, 136) };
            copiesBox.Controls.Add(new Label { Text = S("Number of &copies:"), Location = new Point(10, 26), AutoSize = true });
            Copies = new NumericUpDown { Location = new Point(10, 48), Size = new Size(70, 23), Minimum = 1, Maximum = Math.Max(1, settings.MaximumCopies) };
            Copies.Value = Math.Clamp(settings.Copies, 1, (int)Copies.Maximum);
            Collate = new CheckBox { Text = S("C&ollate"), Location = new Point(10, 82), AutoSize = true, Checked = settings.Collate };
            copiesBox.Controls.Add(Copies);
            copiesBox.Controls.Add(Collate);
            copiesBox.Controls.Add(new CollateSample(Collate) { Location = new Point(92, 42), Size = new Size(80, 40) });

            Ok = new Button { Text = S("&Print"), Size = new Size(84, 26), Location = new Point(286, 262), DialogResult = DialogResult.OK };
            Cancel = new Button { Text = S("Cancel"), Size = new Size(84, 26), Location = new Point(380, 262), DialogResult = DialogResult.Cancel };
            Ok.Click += OnOkClick;

            Controls.Add(printerBox);
            Controls.Add(rangeBox);
            Controls.Add(copiesBox);
            Controls.Add(Ok);
            Controls.Add(Cancel);
            if (owner.ShowHelp)
            {
                Help = new Button { Text = S("&Help"), Size = new Size(84, 26), Location = new Point(12, 262) };
                Help.Click += (_, _) => _owner.RaiseHelpRequest();
                Controls.Add(Help);
            }
            AcceptButton = Ok;
            CancelButton = Cancel;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            bool any = Printer.Items.Count > 0;
            Status.Text = !any ? SystemStrings.Get("No printers are installed.")
                : Printer.SelectedItem as string == PrintBackend.Current.DefaultPrinter ? SystemStrings.Get("Ready (default printer)")
                : SystemStrings.Get("Ready");
            Properties.Enabled = any;
            Ok.Enabled = any || ToFile.Checked;
        }

        /// <summary>
        /// The Properties button: the printer's document properties (DocumentProperties on Windows), which WinForms
        /// hands back in the DEVMODE - paper, tray, orientation, colour, duplex and quality of this print.
        /// </summary>
        private void ShowProperties()
        {
            if (Printer.SelectedItem is not string name) return;
            if (_properties == null || _propertiesPrinter != name)
            {
                var printer = new PrinterSettings { PrinterName = name };
                _properties = name == _owner.PrinterSettings.PrinterName ? (PageSettings)_owner.PageSettings.Clone() : new PageSettings(printer);
                _properties.PrinterSettings = printer;
                _duplex = name == _owner.PrinterSettings.PrinterName ? _owner.PrinterSettings.Duplex : printer.Duplex;
                _propertiesPrinter = name;
            }
            using var form = new PrinterPropertiesForm(name, _properties, _duplex);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                form.Apply();
                _duplex = form.ChosenDuplex;
            }
        }

        private void OnOkClick(object? sender, EventArgs e)
        {
            // PrintDlg does not close on a page range it cannot print.
            if (SomePages.Checked && FromPage.Value > ToPage.Value)
            {
                Fail(SystemStrings.Get("The first page number must not be greater than the last."));
            }
        }

        private void Fail(string message)
        {
            DialogResult = DialogResult.None;
            if (ShowError != null) ShowError(message);
            else MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>OK: write the choices back, as WinForms' UpdatePrinterSettings does.</summary>
        public void Apply()
        {
            var settings = _owner.PrinterSettings;
            if (Printer.SelectedItem is string name) settings.PrinterName = name;
            settings.PrintRange = SomePages.Checked ? PrintRange.SomePages
                : Selection.Checked ? PrintRange.Selection
                : CurrentPage.Checked ? PrintRange.CurrentPage
                : PrintRange.AllPages;
            _owner.PrintToFile = ToFile.Checked;
            settings.PrintToFile = ToFile.Checked;
            if (_owner.AllowSomePages)
            {
                settings.FromPage = (int)FromPage.Value;
                settings.ToPage = (int)ToPage.Value;
            }
            settings.Copies = (short)Copies.Value;
            settings.Collate = Collate.Checked;

            // The DEVMODE the Properties button edited goes to the printer settings and to the document's page
            // settings (SetHdevmode on both), as it does in WinForms.
            if (_properties != null && _propertiesPrinter == settings.PrinterName)
            {
                var page = _owner.PageSettings;
                page.PaperSize = _properties.PaperSize;
                page.PaperSource = _properties.PaperSource;
                page.Landscape = _properties.Landscape;
                page.Color = _properties.Color;
                page.PrinterResolution = _properties.PrinterResolution;
                if (settings.CanDuplex) settings.Duplex = _duplex;
            }
        }

        /// <summary>The two stacks of pages next to "Collate", as the Win32 dialog draws them.</summary>
        private sealed class CollateSample : Control
        {
            private readonly CheckBox _collate;

            public CollateSample(CheckBox collate)
            {
                _collate = collate;
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
                collate.CheckedChanged += (_, _) => Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                using var font = new Font(Font.FontFamily, 7f);
                string[] order = _collate.Checked ? ["1", "2", "3"] : ["1", "1", "2"];
                for (int stack = 0; stack < 2; stack++)
                {
                    string[] labels = _collate.Checked ? order : stack == 0 ? ["1", "1", "1"] : ["2", "2", "2"];
                    for (int i = 0; i < 3; i++)
                    {
                        var r = new Rectangle(stack * 40 + i * 6, 12 - i * 6, 18, 24);
                        g.FillRectangle(Brushes.White, r);
                        g.DrawRectangle(Pens.Gray, r);
                        if (i == 2) TextRenderer.DrawText(g, labels[i], font, r, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
            }
        }
    }
}

/// <summary>
/// "Print to file" without a <see cref="PrinterSettings.PrintFileName"/>: the Windows spooler asks where to save
/// with its "Save Print Output As" dialog. System.Drawing (NetForms.Drawing) has no dialogs, so the WinForms
/// layer plugs a <see cref="SaveFileDialog"/> into the print backend as soon as it is loaded.
/// </summary>
internal static class PrintOutputPrompt
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Install() => PrintBackend.PrintFilePrompt ??= Ask;

    internal static string? Ask(string documentName)
    {
        using var dialog = new SaveFileDialog
        {
            Title = SystemStrings.Get("Save Print Output As"),
            Filter = SystemStrings.Get("PDF document (*.pdf)|*.pdf|All files (*.*)|*.*"),
            DefaultExt = "pdf",
            FileName = SafeName(documentName) + ".pdf",
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
    }

    internal static string SafeName(string documentName)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var name = new string((documentName ?? "").Select(c => Array.IndexOf(invalid, c) >= 0 || c == '\\' || c == '/' || c == ':' ? '_' : c).ToArray()).Trim();
        return name.Length == 0 ? "document" : name;
    }
}
