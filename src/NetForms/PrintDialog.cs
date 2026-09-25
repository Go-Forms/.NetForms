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

    /// <summary>The dialog's form; internal so the tests can fill it in.</summary>
    internal sealed class PrintForm : Form
    {
        private readonly PrintDialog _owner;
        internal readonly ComboBox Printer;
        internal readonly Label Status;
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

        public PrintForm(PrintDialog owner)
        {
            _owner = owner;
            var settings = owner.PrinterSettings;

            Text = "Print";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(440, 300);

            var printerBox = new GroupBox { Text = "Printer", Location = new Point(12, 8), Size = new Size(416, 96) };
            printerBox.Controls.Add(new Label { Text = "&Name:", Location = new Point(10, 26), AutoSize = true });
            Printer = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(80, 22), Size = new Size(326, 23) };
            foreach (var name in PrinterSettings.InstalledPrinters) Printer.Items.Add(name);
            int current = Printer.Items.IndexOf(settings.PrinterName);
            if (current >= 0) Printer.SelectedIndex = current;
            else if (Printer.Items.Count > 0) Printer.SelectedIndex = 0;
            Status = new Label { Location = new Point(80, 50), Size = new Size(230, 18) };
            ToFile = new CheckBox { Text = "Print to fi&le", Location = new Point(316, 52), AutoSize = true, Checked = owner.PrintToFile, Enabled = owner.AllowPrintToFile };
            printerBox.Controls.Add(Printer);
            printerBox.Controls.Add(Status);
            printerBox.Controls.Add(ToFile);
            Printer.SelectedIndexChanged += (_, _) => UpdateStatus();

            var rangeBox = new GroupBox { Text = "Page range", Location = new Point(12, 112), Size = new Size(230, 136) };
            AllPages = new RadioButton { Text = "&All", Location = new Point(10, 22), AutoSize = true };
            Selection = new RadioButton { Text = "Selectio&n", Location = new Point(10, 46), AutoSize = true, Enabled = owner.AllowSelection };
            CurrentPage = new RadioButton { Text = "Current pa&ge", Location = new Point(110, 46), AutoSize = true, Enabled = owner.AllowCurrentPage };
            SomePages = new RadioButton { Text = "Pa&ges", Location = new Point(10, 72), AutoSize = true, Enabled = owner.AllowSomePages };
            int min = Math.Max(0, settings.MinimumPage), max = Math.Max(min, settings.MaximumPage);
            FromPage = new NumericUpDown { Location = new Point(28, 98), Size = new Size(70, 23), Minimum = min, Maximum = max, Enabled = owner.AllowSomePages };
            ToPage = new NumericUpDown { Location = new Point(130, 98), Size = new Size(70, 23), Minimum = min, Maximum = max, Enabled = owner.AllowSomePages };
            FromPage.Value = Math.Clamp(settings.FromPage, min, max);
            ToPage.Value = Math.Clamp(settings.ToPage, min, max);
            rangeBox.Controls.Add(AllPages);
            rangeBox.Controls.Add(Selection);
            rangeBox.Controls.Add(CurrentPage);
            rangeBox.Controls.Add(SomePages);
            rangeBox.Controls.Add(new Label { Text = "from", Location = new Point(0, 101), AutoSize = true });
            rangeBox.Controls.Add(FromPage);
            rangeBox.Controls.Add(new Label { Text = "to", Location = new Point(106, 101), AutoSize = true });
            rangeBox.Controls.Add(ToPage);
            var range = settings.PrintRange;
            (range switch
            {
                PrintRange.Selection when owner.AllowSelection => Selection,
                PrintRange.CurrentPage when owner.AllowCurrentPage => CurrentPage,
                PrintRange.SomePages when owner.AllowSomePages => SomePages,
                _ => AllPages,
            }).Checked = true;

            var copiesBox = new GroupBox { Text = "Copies", Location = new Point(252, 112), Size = new Size(176, 136) };
            copiesBox.Controls.Add(new Label { Text = "Number of &copies:", Location = new Point(10, 26), AutoSize = true });
            Copies = new NumericUpDown { Location = new Point(10, 48), Size = new Size(70, 23), Minimum = 1, Maximum = Math.Max(1, settings.MaximumCopies) };
            Copies.Value = Math.Clamp(settings.Copies, 1, (int)Copies.Maximum);
            Collate = new CheckBox { Text = "C&ollate", Location = new Point(10, 82), AutoSize = true, Checked = settings.Collate };
            copiesBox.Controls.Add(Copies);
            copiesBox.Controls.Add(Collate);

            Ok = new Button { Text = "&Print", Size = new Size(80, 26), Location = new Point(262, 262), DialogResult = DialogResult.OK };
            Cancel = new Button { Text = "Cancel", Size = new Size(80, 26), Location = new Point(348, 262), DialogResult = DialogResult.Cancel };

            Controls.Add(printerBox);
            Controls.Add(rangeBox);
            Controls.Add(copiesBox);
            Controls.Add(Ok);
            Controls.Add(Cancel);
            AcceptButton = Ok;
            CancelButton = Cancel;
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            if (Printer.Items.Count == 0)
            {
                Status.Text = "No printers are installed.";
                Ok.Enabled = ToFile.Checked;
                ToFile.CheckedChanged -= OnToFileChanged;
                ToFile.CheckedChanged += OnToFileChanged;
                return;
            }
            Status.Text = Printer.SelectedItem as string == PrintBackend.Current.DefaultPrinter ? "Ready (default printer)" : "Ready";
        }

        private void OnToFileChanged(object? sender, EventArgs e) => Ok.Enabled = ToFile.Checked || Printer.Items.Count > 0;

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
        }
    }
}
