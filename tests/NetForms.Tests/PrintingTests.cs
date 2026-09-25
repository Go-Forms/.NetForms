using System.Drawing.Printing;
using System.Runtime.InteropServices;
using NetForms.Platform;
using SkiaSharp;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// System.Drawing.Printing and the WinForms print dialogs (decision 153): the page loop and its events, page and
/// margin geometry in hundredths of an inch, preview pages, PDF output, the printers a backend reports, CUPS'
/// option parsing, DEVMODE round trips, PrintPreviewControl/Dialog, PrintDialog and PageSetupDialog.
/// </summary>
public class PrintingTests
{
    /// <summary>A print backend with printers the test owns; jobs are PDFs kept in memory.</summary>
    private sealed class FakeBackend : PrintBackend
    {
        public List<(string Printer, string Document, int Pages, short Copies, bool Aborted)> Jobs { get; } = new();

        public Dictionary<string, PrinterInfo> Printers { get; } = new()
        {
            ["Office"] = new PrinterInfo
            {
                Name = "Office",
                PaperSizes = [new PaperSize(PaperKind.A4, "A4", 827, 1169), new PaperSize(PaperKind.Letter, "Letter", 850, 1100)],
                DefaultPaperIndex = 0,
                Resolutions = [new PrinterResolution(PrinterResolutionKind.Custom, 600, 600)],
                CanDuplex = true,
                MaximumCopies = 99,
            },
            ["Plotter"] = new PrinterInfo { Name = "Plotter", SupportsColor = false, DefaultColor = false, HardMarginX = 25, HardMarginY = 20 },
        };

        public override string[] InstalledPrinters => Printers.Keys.ToArray();

        public override string? DefaultPrinter => "Office";

        public override PrinterInfo? GetPrinter(string printer) => Printers.GetValueOrDefault(printer);

        public override PrintJob StartJob(PrinterSettings settings, string documentName) => new Job(this, settings, documentName);

        private sealed class Job : PdfPrintJob
        {
            private readonly FakeBackend _owner;
            private readonly PrinterSettings _settings;
            private readonly string _document;

            public Job(FakeBackend owner, PrinterSettings settings, string document) : base(new MemoryStream(), document, ownsStream: true)
            {
                _owner = owner;
                _settings = settings;
                _document = document;
            }

            public override void Finish()
            {
                base.Finish();
                _owner.Jobs.Add((_settings.PrinterName, _document, PageCount, _settings.Copies, false));
            }

            public override void Abort()
            {
                base.Abort();
                _owner.Jobs.Add((_settings.PrinterName, _document, PageCount, _settings.Copies, true));
            }
        }
    }

    private static FakeBackend Install(out PrintBackend previous)
    {
        previous = PrintBackend.Current;
        var fake = new FakeBackend();
        PrintBackend.Current = fake;
        PrintControllerWithStatusDialog.ShowStatus = false;
        return fake;
    }

    /// <summary>A document of <paramref name="pages"/> pages; each page fills a one-inch square at the top-left margin.</summary>
    private static PrintDocument Document(int pages, List<string>? log = null)
    {
        var doc = new PrintDocument { DocumentName = "Report" };
        int page = 0;
        doc.BeginPrint += (_, e) => log?.Add("BeginPrint " + e.PrintAction);
        doc.QueryPageSettings += (_, _) => log?.Add("QueryPageSettings");
        doc.PrintPage += (_, e) =>
        {
            page++;
            log?.Add($"PrintPage {page}");
            e.Graphics!.FillRectangle(Brushes.Black, e.MarginBounds.Left, e.MarginBounds.Top, 100, 100);
            e.HasMorePages = page < pages;
        };
        doc.EndPrint += (_, e) => log?.Add("EndPrint " + e.Cancel);
        return doc;
    }

    // --- the page loop ---------------------------------------------------------------------------------

    [Fact]
    public void ThePageLoopRaisesTheWinFormsEventsInOrder()
    {
        var fake = Install(out var previous);
        try
        {
            var log = new List<string>();
            var doc = Document(3, log);
            var preview = new PreviewPrintController();
            doc.PrintController = preview;
            doc.Print();

            Assert.Equal(new[]
            {
                "BeginPrint PrintToPreview",
                "QueryPageSettings", "PrintPage 1",
                "QueryPageSettings", "PrintPage 2",
                "QueryPageSettings", "PrintPage 3",
                "EndPrint False",
            }, log);
            Assert.Equal(3, preview.GetPreviewPageInfo().Length);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void PageAndMarginBoundsAreHundredthsOfAnInchAndFollowLandscape()
    {
        Install(out var previous);
        try
        {
            var doc = new PrintDocument();
            Rectangle page = default, margins = default;
            doc.PrintPage += (_, e) => { page = e.PageBounds; margins = e.MarginBounds; };
            doc.PrintController = new PreviewPrintController();
            doc.Print();
            // The default printer's paper is A4; one-inch margins.
            Assert.Equal(new Rectangle(0, 0, 827, 1169), page);
            Assert.Equal(new Rectangle(100, 100, 627, 969), margins);

            doc.DefaultPageSettings.Landscape = true;
            doc.DefaultPageSettings.Margins = new Margins(50, 50, 25, 25);
            doc.Print();
            Assert.Equal(new Rectangle(0, 0, 1169, 827), page);
            Assert.Equal(new Rectangle(50, 25, 1069, 777), margins);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void APreviewPageIsTheDrawingAtOnePixelPerHundredthOfAnInch()
    {
        Install(out var previous);
        try
        {
            var doc = Document(1);
            var preview = new PreviewPrintController();
            doc.PrintController = preview;
            doc.Print();
            var info = Assert.Single(preview.GetPreviewPageInfo());
            Assert.Equal(new Size(827, 1169), info.PhysicalSize);
            var bitmap = Assert.IsType<Bitmap>(info.Image);
            Assert.Equal(new Size(827, 1169), bitmap.Size);
            // The square drawn at the margin (100, 100) is one inch: 100 pixels; the paper is white.
            Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(150, 150).ToArgb());
            Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(50, 50).ToArgb());
            Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(205, 150).ToArgb());
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void TextOnAPageHasItsPhysicalSize()
    {
        Install(out var previous);
        try
        {
            var doc = new PrintDocument();
            SizeF measured = default;
            float dpi = 0;
            doc.PrintPage += (_, e) =>
            {
                using var font = new Font("DejaVu Sans", 72f);
                measured = e.Graphics!.MeasureString("M", font);
                dpi = e.Graphics.DpiX;
            };
            doc.PrintController = new PreviewPrintController();
            doc.Print();
            // 72 points is one inch, 100 units; a line of text is a little taller than the em.
            Assert.InRange(measured.Height, 100f, 140f);
            Assert.Equal(600f, dpi);

            using var measure = doc.PrinterSettings.CreateMeasurementGraphics();
            using var font2 = new Font("DejaVu Sans", 72f);
            Assert.Equal(measured.Height, measure.MeasureString("M", font2).Height, 0.5f);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void OriginAtMarginsMovesTheGraphicsOrigin()
    {
        Install(out var previous);
        try
        {
            var doc = new PrintDocument { OriginAtMargins = true };
            doc.PrintPage += (_, e) => e.Graphics!.FillRectangle(Brushes.Black, 0, 0, 10, 10);
            var preview = new PreviewPrintController();
            doc.PrintController = preview;
            doc.Print();
            var bitmap = (Bitmap)preview.GetPreviewPageInfo()[0].Image;
            Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(105, 105).ToArgb());
            Assert.Equal(Color.White.ToArgb(), bitmap.GetPixel(5, 5).ToArgb());
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void CancellingInPrintPageAbortsTheJob()
    {
        var fake = Install(out var previous);
        try
        {
            var doc = new PrintDocument();
            bool? endCancel = null;
            int pages = 0;
            doc.PrintPage += (_, e) =>
            {
                pages++;
                e.HasMorePages = true;
                e.Cancel = pages == 2;
            };
            doc.EndPrint += (_, e) => endCancel = e.Cancel;
            doc.Print();
            Assert.Equal(2, pages);
            var job = Assert.Single(fake.Jobs);
            Assert.True(job.Aborted);
            Assert.Equal(2, job.Pages);
            // As WinForms: EndPrint runs before the controller learns of the cancel, and sees Cancel false.
            Assert.False(endCancel);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void BeginPrintCanCancelBeforeAnyPage()
    {
        var fake = Install(out var previous);
        try
        {
            var doc = new PrintDocument();
            doc.BeginPrint += (_, e) => e.Cancel = true;
            int pages = 0;
            doc.PrintPage += (_, _) => pages++;
            doc.Print();
            Assert.Equal(0, pages);
            Assert.Empty(fake.Jobs);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    // --- printers ----------------------------------------------------------------------------------------

    [Fact]
    public void ThePrinterSettingsComeFromThePrinter()
    {
        var fake = Install(out var previous);
        try
        {
            Assert.Equal(new[] { "Office", "Plotter" }, PrinterSettings.InstalledPrinters.Cast<string>().ToArray());
            var settings = new PrinterSettings();
            Assert.Equal("Office", settings.PrinterName);
            Assert.True(settings.IsDefaultPrinter);
            Assert.True(settings.IsValid);
            Assert.True(settings.CanDuplex);
            Assert.Equal(99, settings.MaximumCopies);
            Assert.Equal(2, settings.PaperSizes.Count);
            Assert.Equal(PaperKind.A4, settings.DefaultPageSettings.PaperSize.Kind);
            Assert.Equal(600, settings.DefaultPageSettings.PrinterResolution.X);

            settings.PrinterName = "Plotter";
            Assert.False(settings.IsDefaultPrinter);
            Assert.False(settings.SupportsColor);
            Assert.False(settings.DefaultPageSettings.Color);
            Assert.Equal(25f, settings.DefaultPageSettings.HardMarginX);
            Assert.Equal(new RectangleF(25, 20, settings.DefaultPageSettings.Bounds.Width - 50, settings.DefaultPageSettings.Bounds.Height - 40),
                settings.DefaultPageSettings.PrintableArea);

            settings.PrinterName = "Nowhere";
            Assert.False(settings.IsValid);
            var doc = new PrintDocument { PrinterSettings = settings };
            doc.PrintPage += (_, _) => { };
            var ex = Assert.Throws<InvalidPrinterException>(doc.Print);
            Assert.Contains("Nowhere", ex.Message);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void PrintingGoesToThePrinterWithItsCopies()
    {
        var fake = Install(out var previous);
        try
        {
            var doc = Document(2);
            doc.PrinterSettings.Copies = 3;
            doc.Print();
            var job = Assert.Single(fake.Jobs);
            Assert.Equal(("Office", "Report", 2, (short)3, false), job);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void PrintToFileWritesAPdfWithOnePagePerPrintPage()
    {
        var fake = Install(out var previous);
        var path = Path.Combine(Path.GetTempPath(), "netforms-print-test-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var log = new List<string>();
            var doc = Document(3, log);
            // A printer without a driver behind it: NetForms writes the pages as PDF itself, on every OS. (On Windows a
            // real printer's print to file goes through its driver, as in WinForms: PrintToFileOfARealPrinter...)
            doc.PrinterSettings.PrinterName = "Nowhere";
            // Without a printer the paper follows the region (Letter in the US, A4 elsewhere): pin it.
            doc.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
            doc.PrinterSettings.PrintToFile = true;
            doc.PrinterSettings.PrintFileName = path;
            doc.Print();
            Assert.Equal("BeginPrint PrintToFile", log[0]);
            Assert.Empty(fake.Jobs);
            var bytes = File.ReadAllBytes(path);
            var text = System.Text.Encoding.Latin1.GetString(bytes);
            Assert.StartsWith("%PDF", text);
            Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page\b").Count);
            // A4 in points: 827 * 0.72 = 595.44.
            Assert.Contains("595.44", text);
        }
        finally
        {
            PrintBackend.Current = previous;
            File.Delete(path);
        }
    }

    [Fact]
    public void PrintToFileOfARealPrinterGoesThroughItsDriverOnWindowsOnly()
    {
        var fake = Install(out var previous);
        var path = Path.Combine(Path.GetTempPath(), "netforms-print-test-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var doc = Document(1);
            doc.PrinterSettings.PrintToFile = true;
            doc.PrinterSettings.PrintFileName = path;
            doc.Print();
            if (OperatingSystem.IsWindows())
            {
                // The spooler writes the driver's output to PrintFileName (the job carries it as DOCINFO.lpszOutput).
                Assert.Single(fake.Jobs);
            }
            else
            {
                Assert.Empty(fake.Jobs);
                Assert.StartsWith("%PDF", System.Text.Encoding.Latin1.GetString(File.ReadAllBytes(path)));
            }
        }
        finally
        {
            PrintBackend.Current = previous;
            File.Delete(path);
        }
    }

    [Fact]
    public void AMachineWithoutPrintersStillPreviewsAndPrintsToFile()
    {
        var previous = PrintBackend.Current;
        PrintBackend.Current = new NoPrintBackend();
        PrintControllerWithStatusDialog.ShowStatus = false;
        var path = Path.Combine(Path.GetTempPath(), "netforms-print-test-" + Guid.NewGuid().ToString("N") + ".pdf");
        try
        {
            var settings = new PrinterSettings();
            Assert.False(settings.IsValid);
            Assert.Equal("Default printer is not set.", settings.PrinterName);
            Assert.Empty(PrinterSettings.InstalledPrinters.Cast<string>());

            var preview = new PreviewPrintController();
            var doc = Document(1);
            doc.PrintController = preview;
            doc.Print();
            Assert.Single(preview.GetPreviewPageInfo());

            doc.PrintController = new StandardPrintController();
            Assert.Throws<InvalidPrinterException>(doc.Print);

            doc.PrinterSettings.PrintToFile = true;
            doc.PrinterSettings.PrintFileName = path;
            doc.Print();
            Assert.True(new FileInfo(path).Length > 0);
        }
        finally
        {
            PrintBackend.Current = previous;
            File.Delete(path);
        }
    }

    [Fact]
    public void CupsOptionsBecomePaperTraysResolutionsDuplexAndColour()
    {
        const string options = """
            PageSize/Media Size: Letter Legal *A4 A5 Env10 Custom.WIDTHxHEIGHT
            InputSlot/Media Source: *Auto Tray1 Manual
            Resolution/Resolution: 300dpi *600dpi 1200x600dpi
            Duplex/2-Sided Printing: *None DuplexNoTumble DuplexTumble
            ColorModel/Output Mode: Gray *RGB
            """;
        var info = CupsPrintBackend.Parse("Laser", options);
        Assert.Equal(new[] { "Letter", "Legal", "A4", "A5", "Envelope #10" }, info.PaperSizes.Select(p => p.PaperName).ToArray());
        Assert.Equal(PaperKind.A4, info.PaperSizes[info.DefaultPaperIndex].Kind);
        Assert.Equal(new Size(827, 1169), new Size(info.PaperSizes[2].Width, info.PaperSizes[2].Height));
        Assert.Equal(new[] { "Auto", "Tray1", "Manual" }, info.PaperSources.Select(s => s.SourceName).ToArray());
        Assert.Equal(PaperSourceKind.AutomaticFeed, info.PaperSources[0].Kind);
        Assert.Equal(PaperSourceKind.Upper, info.PaperSources[1].Kind);
        Assert.Equal(PaperSourceKind.ManualFeed, info.PaperSources[2].Kind);
        Assert.Equal(600, info.Resolutions[info.DefaultResolutionIndex].X);
        Assert.Equal((1200, 600), (info.Resolutions[2].X, info.Resolutions[2].Y));
        Assert.True(info.CanDuplex);
        Assert.Equal(Duplex.Simplex, info.DefaultDuplex);
        Assert.True(info.SupportsColor);
        Assert.True(info.DefaultColor);

        // IPP-everywhere names describe their own size.
        var ipp = CupsPrintBackend.Parse("Driverless", "media/Media: *iso_a4_210x297mm na_letter_8.5x11in om_small-photo_100x150mm\nsides/Sides: one-sided *two-sided-long-edge\nprint-color-mode/Color: *monochrome");
        Assert.Equal(new[] { PaperKind.A4, PaperKind.Letter, PaperKind.Custom }, ipp.PaperSizes.Select(p => p.Kind).ToArray());
        Assert.Equal(new Size(394, 591), new Size(ipp.PaperSizes[2].Width, ipp.PaperSizes[2].Height));
        Assert.Equal(Duplex.Vertical, ipp.DefaultDuplex);
        Assert.False(ipp.SupportsColor);
    }

    [Fact]
    public void PageSettingsSurviveADevModeRoundTrip()
    {
        Install(out var previous);
        try
        {
            var settings = new PrinterSettings { Copies = 4, Collate = true, Duplex = Duplex.Horizontal };
            var page = new PageSettings(settings) { Landscape = true, PaperSize = settings.PaperSizes[1], Color = false };
            var mode = settings.GetHdevmode(page);
            try
            {
                var back = new PageSettings(settings);
                back.SetHdevmode(mode);
                Assert.True(back.Landscape);
                Assert.False(back.Color);
                Assert.Equal(PaperKind.Letter, back.PaperSize.Kind);

                var settingsBack = new PrinterSettings();
                settingsBack.SetHdevmode(mode);
                Assert.Equal(4, settingsBack.Copies);
                Assert.True(settingsBack.Collate);
                Assert.Equal(Duplex.Horizontal, settingsBack.Duplex);
            }
            finally
            {
                Marshal.FreeHGlobal(mode);
            }

            var names = settings.GetHdevnames();
            try
            {
                var other = new PrinterSettings();
                other.SetHdevnames(names);
                Assert.Equal("Office", other.PrinterName);
            }
            finally
            {
                Marshal.FreeHGlobal(names);
            }
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void MarginsUnitsAndPaperBehaveAsInWinForms()
    {
        var margins = new Margins();
        Assert.Equal("[Margins Left=100 Right=100 Top=100 Bottom=100]", margins.ToString());
        Assert.Throws<ArgumentOutOfRangeException>(() => new Margins(-1, 0, 0, 0));
        Assert.True(new Margins(1, 2, 3, 4) == new Margins(1, 2, 3, 4));
        var clone = (Margins)margins.Clone();
        clone.Left = 5;
        Assert.Equal(100, margins.Left);

        var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(Margins));
        Assert.IsType<MarginsConverter>(converter);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        Assert.Equal("10, 20, 30, 40", converter.ConvertToString(null, culture, new Margins(10, 20, 30, 40)));
        Assert.Equal(new Margins(1, 2, 3, 4), converter.ConvertFromString(null, culture, "1, 2, 3, 4"));

        Assert.Equal(254, PrinterUnitConvert.Convert(100, PrinterUnit.Display, PrinterUnit.TenthsOfAMillimeter));
        Assert.Equal(1000, PrinterUnitConvert.Convert(100, PrinterUnit.Display, PrinterUnit.ThousandthsOfAnInch));
        var mm = PrinterUnitConvert.Convert(new Margins(100, 100, 100, 100), PrinterUnit.Display, PrinterUnit.HundredthsOfAMillimeter);
        Assert.Equal(2540, mm.Left);

        var custom = new PaperSize("Card", 300, 500);
        Assert.Equal(PaperKind.Custom, custom.Kind);
        Assert.Equal("[PaperSize Card Kind=Custom Height=500 Width=300]", custom.ToString());
        custom.RawKind = 300;
        Assert.Equal(PaperKind.Custom, custom.Kind);
        Assert.Throws<ArgumentException>(() => PrintBackend.StandardPaperSizes()[0].Width = 1);
        Assert.Equal("[PrinterResolution X=600 Y=300]", new PrinterResolution { X = 600, Y = 300 }.ToString());
    }

    // --- preview control and dialog ------------------------------------------------------------------------

    [Fact]
    public void PrintPreviewControlShowsThePagesAndLaysThemOut()
    {
        var platform = TestPlatform.Install();
        Install(out var previous);
        try
        {
            using var form = new Form { ClientSize = new Size(600, 400) };
            var control = new PrintPreviewControl { Dock = DockStyle.Fill, Document = Document(4) };
            int startChanged = 0;
            control.StartPageChanged += (_, _) => startChanged++;
            form.Controls.Add(control);
            form.Show();
            var window = platform.Windows.Last();

            using (window.Paint()) { }
            Assert.Equal(4, control.PageInfo!.Length);
            var one = Assert.Single(control.PageRectangles);
            // AutoZoom fits the page to the height: A4 is taller than wide, and centred.
            Assert.True(one.Height > one.Width);
            Assert.InRange(one.X + one.Width / 2, 280, 310);

            control.Columns = 2;
            control.Rows = 1;
            using (window.Paint()) { }
            Assert.Equal(2, control.PageRectangles.Length);
            Assert.True(control.PageRectangles[1].Left > control.PageRectangles[0].Right);

            control.StartPage = 3;
            Assert.Equal(2, control.StartPage); // at most Pages - Rows * Columns
            Assert.Equal(1, startChanged);

            control.Zoom = 1.0;
            Assert.False(control.AutoZoom);
            using var shot = window.Paint();
            // At full size the pages outgrow the control: the scroll bars show (their track is not the page white).
            Assert.NotEqual(Color.White.ToArgb(), shot.GetPixel(300, 390).ToArgb());
            var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(outDir);
            shot.Save(Path.Combine(outDir, "print-preview.png"));
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void PrintPreviewDialogCarriesTheWinFormsToolBar()
    {
        TestPlatform.Install();
        Install(out var previous);
        try
        {
            using var dialog = new PrintPreviewDialog { Document = Document(6) };
            Assert.Equal("Print preview", dialog.Text);
            Assert.False(dialog.ShowInTaskbar);
            Assert.False(dialog.MinimizeBox);
            Assert.Same(dialog.Document, dialog.PrintPreviewControl.Document);
            var buttons = dialog.ToolStrip.Items.OfType<ToolStripButton>().Where(b => b.DisplayStyle == ToolStripItemDisplayStyle.Image).ToArray();
            Assert.Equal(6, buttons.Length); // print, then one to six pages
            buttons[4].PerformClick();       // four pages
            Assert.Equal((2, 2), (dialog.PrintPreviewControl.Rows, dialog.PrintPreviewControl.Columns));
            dialog.PageCounter.Value = 3;
            Assert.Equal(2, dialog.PrintPreviewControl.StartPage);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void PrintControllerWithStatusDialogShowsProgressAndCloses()
    {
        TestPlatform.Install();
        Install(out var previous);
        PrintControllerWithStatusDialog.ShowStatus = true;
        try
        {
            var preview = new PreviewPrintController();
            var status = new PrintControllerWithStatusDialog(preview, "Generating Previews");
            var doc = Document(2);
            string? title = null;
            doc.PrintPage += (_, _) => title ??= status.StatusDialog?.Text;
            doc.PrintController = status;
            Assert.True(status.IsPreview);
            doc.Print();
            Assert.Equal("Generating Previews", title);
            Assert.Null(status.StatusDialog);
            Assert.Equal(2, preview.GetPreviewPageInfo().Length);
        }
        finally
        {
            PrintControllerWithStatusDialog.ShowStatus = false;
            PrintBackend.Current = previous;
        }
    }

    // --- dialogs ---------------------------------------------------------------------------------------------

    [Fact]
    public void PrintDialogWritesTheChoicesBackToThePrinterSettings()
    {
        var platform = TestPlatform.Install();
        Install(out var previous);
        try
        {
            var doc = new PrintDocument();
            doc.PrinterSettings.MinimumPage = 1;
            doc.PrinterSettings.MaximumPage = 10;
            doc.PrinterSettings.FromPage = 1;
            doc.PrinterSettings.ToPage = 10;
            using var dialog = new PrintDialog { Document = doc, AllowSomePages = true };
            Assert.Same(doc.PrinterSettings, dialog.PrinterSettings);

            platform.OnMessageLoop = () =>
            {
                var form = (PrintDialog.PrintForm)Application.OpenForms[Application.OpenForms.Count - 1];
                Assert.Equal(new[] { "Office", "Plotter" }, form.Printer.Items.Cast<string>().ToArray());
                Assert.Equal("Office", form.Printer.SelectedItem);
                Assert.False(form.Selection.Enabled);
                form.Printer.SelectedIndex = 1;
                form.SomePages.Checked = true;
                form.FromPage.Value = 2;
                form.ToPage.Value = 4;
                form.Copies.Value = 3;
                form.Collate.Checked = true;
                form.Ok.PerformClick();
            };
            Assert.Equal(DialogResult.OK, dialog.ShowDialog());

            var s = doc.PrinterSettings;
            Assert.Equal("Plotter", s.PrinterName);
            Assert.Equal(PrintRange.SomePages, s.PrintRange);
            Assert.Equal((2, 4), (s.FromPage, s.ToPage));
            Assert.Equal(3, s.Copies);
            Assert.True(s.Collate);

            platform.OnMessageLoop = () => ((PrintDialog.PrintForm)Application.OpenForms[Application.OpenForms.Count - 1]).Cancel.PerformClick();
            Assert.Equal(DialogResult.Cancel, dialog.ShowDialog());

            dialog.Reset();
            Assert.Null(dialog.Document);
            Assert.True(dialog.AllowPrintToFile);
            Assert.False(dialog.AllowSomePages);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    [Fact]
    public void PageSetupDialogChangesPaperOrientationAndMargins()
    {
        var platform = TestPlatform.Install();
        Install(out var previous);
        try
        {
            using var dialog = new PageSetupDialog();
            Assert.Throws<ArgumentException>(() => dialog.ShowDialog());

            var doc = new PrintDocument();
            dialog.Document = doc;
            Assert.Same(doc.DefaultPageSettings, dialog.PageSettings);
            platform.OnMessageLoop = () =>
            {
                var form = (PageSetupDialog.PageSetupForm)Application.OpenForms[Application.OpenForms.Count - 1];
                Assert.Equal(new[] { "A4", "Letter" }, form.PaperBox.Items.Cast<string>().ToArray());
                // Inches (EnableMetric is off): one-inch margins show as 1.00.
                Assert.Equal(1.00m, form.LeftMargin.Value);
                form.PaperBox.SelectedIndex = 1;
                form.Landscape.Checked = true;
                form.LeftMargin.Value = 0.5m;
                form.TopMargin.Value = 0.75m;
                form.Ok.PerformClick();
            };
            Assert.Equal(DialogResult.OK, dialog.ShowDialog());
            var page = doc.DefaultPageSettings;
            Assert.Equal(PaperKind.Letter, page.PaperSize.Kind);
            Assert.True(page.Landscape);
            Assert.Equal(new Margins(50, 100, 75, 100), page.Margins);

            dialog.MinMargins = new Margins(60, 60, 60, 60);
            platform.OnMessageLoop = () => ((PageSetupDialog.PageSetupForm)Application.OpenForms[Application.OpenForms.Count - 1]).Ok.PerformClick();
            dialog.ShowDialog();
            Assert.Equal(new Margins(60, 100, 75, 100), page.Margins);
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }

    // --- the designer ------------------------------------------------------------------------------------------

    [Fact]
    public void ThePrintingComponentsRoundTripThroughTheDesigner()
    {
        const string source = """
            namespace Demo
            {
                partial class TestForm
                {
                    private System.ComponentModel.IContainer components = null;

                    private void InitializeComponent()
                    {
                        printDocument1 = new System.Drawing.Printing.PrintDocument();
                        printDialog1 = new PrintDialog();
                        pageSetupDialog1 = new PageSetupDialog();
                        // 
                        // printDocument1
                        // 
                        printDocument1.DocumentName = "Invoice";
                        // 
                        // printDialog1
                        // 
                        printDialog1.Document = printDocument1;
                        printDialog1.UseEXDialog = true;
                        // 
                        // pageSetupDialog1
                        // 
                        pageSetupDialog1.Document = printDocument1;
                        // 
                        // TestForm
                        // 
                        AutoScaleDimensions = new SizeF(7F, 15F);
                        AutoScaleMode = AutoScaleMode.Font;
                        ClientSize = new Size(284, 261);
                        Name = "TestForm";
                        Text = "TestForm";
                    }

                    private System.Drawing.Printing.PrintDocument printDocument1;
                    private PrintDialog printDialog1;
                    private PageSetupDialog pageSetupDialog1;
                }
            }
            """;
        TestPlatform.Install();
        Install(out var previous);
        try
        {
            var model = new NetForms.Design.Serialization.DesignerCodeReader().Read(source, new[] { "namespace Demo { public partial class TestForm : Form { } }" });
            var document = (PrintDocument)model.Find("printDocument1")!.Instance;
            Assert.Equal("Invoice", document.DocumentName);
            Assert.Same(document, ((PrintDialog)model.Find("printDialog1")!.Instance).Document);
            Assert.Same(document, ((PageSetupDialog)model.Find("pageSetupDialog1")!.Instance).Document);
            Assert.Equal(source, new NetForms.Design.Serialization.DesignerCodeWriter().Write(model, source));

            // A PrintPreviewDialog dropped on the form: the lines VS writes for one, except the Icon (a .resx entry).
            model.AddComponent(new PrintPreviewDialog());
            var written = new NetForms.Design.Serialization.DesignerCodeWriter().Write(model, source);
            Assert.Contains("printPreviewDialog1.AutoScrollMargin = new Size(0, 0);", written);
            Assert.Contains("printPreviewDialog1.Enabled = true;", written);
            Assert.Contains("printPreviewDialog1.Name = \"printPreviewDialog1\";", written);
            Assert.DoesNotContain("printPreviewDialog1.Icon", written);

            var printing = NetForms.Design.DesignerToolbox.Categories().Single(c => c.Name == "Printing");
            Assert.Equal(new[] { "PageSetupDialog", "PrintDialog", "PrintDocument", "PrintPreviewControl", "PrintPreviewDialog" },
                printing.Items.Select(i => i.Name).ToArray());
            Assert.Equal(new[] { true, true, true, false, true }, printing.Items.Select(i => i.Tray).ToArray());
        }
        finally
        {
            PrintBackend.Current = previous;
        }
    }
}
