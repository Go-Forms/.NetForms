// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing: PrintDocument.cs, PrintController.cs,
// StandardPrintController.cs, PreviewPrintController.cs), MIT - THIRD-PARTY-NOTICES.md. The device contexts and
// metafiles of the originals are PrintJob (PrintBackend.cs) and Skia pictures here.
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using SkiaSharp;

namespace System.Drawing.Printing;

/// <summary>A document to print: raise <see cref="PrintPage"/> once per page, through a <see cref="PrintController"/>.</summary>
[DefaultProperty("DocumentName")]
[DefaultEvent("PrintPage")]
[Description("Defines an object that sends output to a printer.")]
public class PrintDocument : Component
{
    private string _documentName = "document";

    private PrintEventHandler? _beginPrintHandler;
    private PrintEventHandler? _endPrintHandler;
    private PrintPageEventHandler? _printPageHandler;
    private QueryPageSettingsEventHandler? _queryHandler;

    private PrinterSettings _printerSettings = new();
    private PageSettings _defaultPageSettings;

    private PrintController? _printController;

    private bool _originAtMargins;
    private bool _userSetPageSettings;

    public PrintDocument() => _defaultPageSettings = new PageSettings(_printerSettings);

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The default page settings for the document being printed.")]
    public PageSettings DefaultPageSettings
    {
        get => _defaultPageSettings;
        set
        {
            _defaultPageSettings = value ?? new PageSettings();
            _userSetPageSettings = true;
        }
    }

    [DefaultValue("document")]
    [Description("The name of the document being printed.")]
    public string DocumentName
    {
        get => _documentName;
        set => _documentName = value ?? "";
    }

    [DefaultValue(false)]
    [Description("Positions the origin of the graphics object associated with the page at the point just inside the user-specified margins.")]
    public bool OriginAtMargins
    {
        get => _originAtMargins;
        set => _originAtMargins = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Retrieves the print controller for this document.")]
    public PrintController PrintController
    {
        get => _printController ??= new StandardPrintController();
        set => _printController = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Retrieves the settings for the printer the document is going to be printed to.")]
    public PrinterSettings PrinterSettings
    {
        get => _printerSettings;
        set
        {
            value ??= new PrinterSettings();
            _printerSettings = value;
            // Follow the printer only while the page settings are the ones created for the previous printer.
            if (!_userSetPageSettings)
            {
                _defaultPageSettings = _printerSettings.DefaultPageSettings;
            }
        }
    }

    [Description("Occurs when the document is about to be printed.")]
    public event PrintEventHandler BeginPrint
    {
        add => _beginPrintHandler += value;
        remove => _beginPrintHandler -= value;
    }

    [Description("Occurs after the document has been printed.")]
    public event PrintEventHandler EndPrint
    {
        add => _endPrintHandler += value;
        remove => _endPrintHandler -= value;
    }

    [Description("Occurs for each page to be printed.")]
    public event PrintPageEventHandler PrintPage
    {
        add => _printPageHandler += value;
        remove => _printPageHandler -= value;
    }

    [Description("Occurs before each page is printed. Useful for changing PageSettings for a particular page.")]
    public event QueryPageSettingsEventHandler QueryPageSettings
    {
        add => _queryHandler += value;
        remove => _queryHandler -= value;
    }

    protected internal virtual void OnBeginPrint(PrintEventArgs e) => _beginPrintHandler?.Invoke(this, e);

    protected internal virtual void OnEndPrint(PrintEventArgs e) => _endPrintHandler?.Invoke(this, e);

    protected internal virtual void OnPrintPage(PrintPageEventArgs e) => _printPageHandler?.Invoke(this, e);

    protected internal virtual void OnQueryPageSettings(QueryPageSettingsEventArgs e) => _queryHandler?.Invoke(this, e);

    public void Print()
    {
        PrintController controller = PrintController;
        controller.Print(this);
    }

    public override string ToString() => $"[PrintDocument {DocumentName}]";
}

/// <summary>Drives the printing of a document: the page loop, and what each page is drawn on.</summary>
public abstract class PrintController
{
    protected PrintController()
    {
    }

    public virtual bool IsPreview => false;

    public virtual Graphics? OnStartPage(PrintDocument document, PrintPageEventArgs e) => null;

    public virtual void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
    }

    public virtual void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
    }

    public virtual void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
    }

    /// <remarks>If controllers are nested, only the outer one runs this; the inner one sees the On* calls.</remarks>
    internal void Print(PrintDocument document)
    {
        PrintAction printAction = IsPreview
            ? PrintAction.PrintToPreview
            : document.PrinterSettings.PrintToFile ? PrintAction.PrintToFile : PrintAction.PrintToPrinter;

        PrintEventArgs printEvent = new(printAction);
        document.OnBeginPrint(printEvent);
        if (printEvent.Cancel)
        {
            document.OnEndPrint(printEvent);
            return;
        }

        OnStartPrint(document, printEvent);
        if (printEvent.Cancel)
        {
            document.OnEndPrint(printEvent);
            OnEndPrint(document, printEvent);
            return;
        }

        bool canceled = true;
        try
        {
            canceled = PrintLoop(document);
        }
        finally
        {
            try
            {
                document.OnEndPrint(printEvent);
                printEvent.Cancel = canceled | printEvent.Cancel;
            }
            finally
            {
                OnEndPrint(document, printEvent);
            }
        }
    }

    /// <summary>Returns true if the print was aborted.</summary>
    private bool PrintLoop(PrintDocument document)
    {
        QueryPageSettingsEventArgs queryEvent = new((PageSettings)document.DefaultPageSettings.Clone());
        while (true)
        {
            document.OnQueryPageSettings(queryEvent);
            if (queryEvent.Cancel)
            {
                return true;
            }

            PrintPageEventArgs pageEvent = CreatePrintPageEvent(queryEvent.PageSettings);
            Graphics? graphics = OnStartPage(document, pageEvent);
            pageEvent.SetGraphics(graphics);

            try
            {
                document.OnPrintPage(pageEvent);
                OnEndPage(document, pageEvent);
            }
            finally
            {
                pageEvent.Dispose();
            }

            if (pageEvent.Cancel)
            {
                return true;
            }
            else if (!pageEvent.HasMorePages)
            {
                return false;
            }
        }
    }

    private static PrintPageEventArgs CreatePrintPageEvent(PageSettings pageSettings)
    {
        Rectangle pageBounds = pageSettings.Bounds;
        Rectangle marginBounds = new(
            pageSettings.Margins.Left,
            pageSettings.Margins.Top,
            pageBounds.Width - (pageSettings.Margins.Left + pageSettings.Margins.Right),
            pageBounds.Height - (pageSettings.Margins.Top + pageSettings.Margins.Bottom));

        return new PrintPageEventArgs(null, marginBounds, pageBounds, pageSettings);
    }

    /// <summary>
    /// The Graphics of a page drawn on <paramref name="canvas"/> (unit 1/100 inch, origin at the printable area):
    /// text at its physical size, the printer's resolution, and the origin moved to the margins when asked.
    /// </summary>
    private protected static Graphics CreatePageGraphics(SKCanvas canvas, PrintDocument document, PrintPageEventArgs e, float dpi)
    {
        var g = Graphics.FromCanvas(canvas);
        g.SetDpi(dpi, dpi);
        g.TextScale = 100f / 96f;
        if (document.OriginAtMargins)
        {
            var page = e.PageSettings;
            float hardX = page.Landscape ? page.HardMarginY : page.HardMarginX;
            float hardY = page.Landscape ? page.HardMarginX : page.HardMarginY;
            g.TranslateTransform(-hardX, -hardY);
            g.TranslateTransform(document.DefaultPageSettings.Margins.Left, document.DefaultPageSettings.Margins.Top);
        }
        return g;
    }
}

/// <summary>Prints to the printer (or, with PrintToFile, to a PDF file).</summary>
public class StandardPrintController : PrintController
{
    private PrintJob? _job;
    private Graphics? _graphics;

    public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnStartPrint(document, e);
        var settings = document.PrinterSettings;
        if (settings.PrintToFile && (!OperatingSystem.IsWindows() || !settings.IsValid))
        {
            // The spooler's "print to file" writes what the driver makes of the pages. Off Windows (and on
            // Windows without a driver) NetForms writes the pages themselves, as PDF: decision 153.
            var path = settings.PrintFileName;
            if (string.IsNullOrEmpty(path))
            {
                path = PrintBackend.PrintFilePrompt?.Invoke(document.DocumentName) ?? string.Empty;
                if (string.IsNullOrEmpty(path))
                {
                    e.Cancel = true;
                    return;
                }
            }
            _job = new PdfPrintJob(File.Create(path), document.DocumentName, ownsStream: true);
            return;
        }

        if (!settings.IsValid)
        {
            throw new InvalidPrinterException(settings);
        }

        _job = PrintBackend.Current.StartJob(settings, document.DocumentName);
    }

    public override Graphics OnStartPage(PrintDocument document, PrintPageEventArgs e)
    {
        base.OnStartPage(document, e);
        var job = _job ?? throw new InvalidOperationException("OnStartPrint was not called.");
        var canvas = job.BeginPage(e.PageSettings, e.PageBounds);
        _graphics = CreatePageGraphics(canvas, document, e, job.Dpi);
        return _graphics;
    }

    public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
        _graphics?.Dispose();
        _graphics = null;
        _job?.EndPage();
        base.OnEndPage(document, e);
    }

    public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
        var job = _job;
        _job = null;
        if (job != null)
        {
            try
            {
                if (e.Cancel) job.Abort();
                else job.Finish();
            }
            finally
            {
                job.Dispose();
            }
        }
        base.OnEndPrint(document, e);
    }
}

/// <summary>"Prints" each page to an image, for PrintPreviewControl.</summary>
public class PreviewPrintController : PrintController
{
    private readonly List<PreviewPageInfo> _list = [];
    private SKPictureRecorder? _recorder;
    private Graphics? _graphics;
    private Size _pageSize;

    public override bool IsPreview => true;

    public virtual bool UseAntiAlias { get; set; }

    public PreviewPageInfo[] GetPreviewPageInfo() => [.. _list];

    public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnStartPrint(document, e);
        // WinForms needs a printer's DC as the reference of its metafiles; the pages here need none, so a
        // preview works on a machine without printers too (decision 153).
    }

    public override Graphics OnStartPage(PrintDocument document, PrintPageEventArgs e)
    {
        base.OnStartPage(document, e);
        _pageSize = e.PageBounds.Size;
        _recorder = new SKPictureRecorder();
        var canvas = _recorder.BeginRecording(new SKRect(0, 0, _pageSize.Width, _pageSize.Height));
        // The printable area starts at the hard margins, as on the printer.
        var page = e.PageSettings;
        canvas.Translate(page.Landscape ? page.HardMarginY : page.HardMarginX, page.Landscape ? page.HardMarginX : page.HardMarginY);
        var resolution = page.PrinterResolution;
        float dpi = resolution.Kind == PrinterResolutionKind.Custom && resolution.X > 0 ? resolution.X : 300f;
        _graphics = CreatePageGraphics(canvas, document, e, dpi);
        if (UseAntiAlias)
        {
            _graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
            _graphics.SmoothingMode = SmoothingMode.AntiAlias;
        }
        return _graphics;
    }

    public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
        _graphics?.Dispose();
        _graphics = null;
        if (_recorder != null)
        {
            var picture = _recorder.EndRecording();
            _recorder.Dispose();
            _recorder = null;
            _list.Add(new PreviewPageInfo(CreatePageImage(picture, _pageSize), _pageSize));
        }
        base.OnEndPage(document, e);
    }

    /// <summary>
    /// The page as a Bitmap of one pixel per 1/100 inch on white paper, carrying the recorded drawing so a
    /// preview can replay it sharp at any zoom (the Metafile of WinForms).
    /// </summary>
    internal static Bitmap CreatePageImage(SKPicture picture, Size pageSize)
    {
        var bitmap = new SKBitmap(Math.Max(1, pageSize.Width), Math.Max(1, pageSize.Height));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawPicture(picture);
        }
        return new Bitmap(bitmap) { Picture = picture, HorizontalResolution = 100f, VerticalResolution = 100f };
    }

    public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
        _recorder?.Dispose();
        _recorder = null;
        base.OnEndPrint(document, e);
    }
}
