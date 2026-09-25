using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing.Printing;

/// <summary>
/// What a printer is and can do, as the platform reports it: WinForms reads this from the driver
/// (DeviceCapabilities and the default DEVMODE); CUPS reports the same through its PPD options.
/// Sizes are in hundredths of an inch.
/// </summary>
internal sealed class PrinterInfo
{
    public required string Name { get; init; }
    public PaperSize[] PaperSizes { get; init; } = PrintBackend.StandardPaperSizes();
    public PaperSource[] PaperSources { get; init; } = [new PaperSource(PaperSourceKind.AutomaticFeed, "Automatically Select")];
    public PrinterResolution[] Resolutions { get; init; } = [new PrinterResolution(PrinterResolutionKind.Custom, 300, 300)];
    public int DefaultPaperIndex { get; init; }
    public int DefaultSourceIndex { get; init; }
    public int DefaultResolutionIndex { get; init; }
    public bool SupportsColor { get; init; } = true;
    public bool DefaultColor { get; init; } = true;
    public bool CanDuplex { get; init; }
    public Duplex DefaultDuplex { get; init; } = Duplex.Simplex;
    public bool DefaultLandscape { get; init; }
    public bool DefaultCollate { get; init; }
    public int MaximumCopies { get; init; } = 999;
    public int LandscapeAngle { get; init; } = 90;
    /// <summary>The unprintable edge, 1/100 inch (GDI's PHYSICALOFFSETX/Y); zero for PDF and CUPS.</summary>
    public float HardMarginX { get; init; }
    public float HardMarginY { get; init; }
}

/// <summary>
/// A print job being written: pages are drawn on an SKCanvas whose unit is 1/100 inch and whose origin is
/// the top-left corner of the printable area, as on a WinForms printer Graphics.
/// </summary>
internal abstract class PrintJob : IDisposable
{
    /// <summary>The resolution the Graphics reports (Graphics.DpiX on a printer page).</summary>
    public virtual float Dpi => 300f;

    public abstract SKCanvas BeginPage(PageSettings settings, Rectangle pageBounds);

    public abstract void EndPage();

    /// <summary>Everything is drawn: close the document and hand it to the printer.</summary>
    public abstract void Finish();

    /// <summary>The print was cancelled: nothing reaches the printer.</summary>
    public abstract void Abort();

    public virtual void Dispose()
    {
    }
}

/// <summary>A PDF document written to a stream. Print to file, and what CUPS is given to print.</summary>
internal class PdfPrintJob : PrintJob
{
    private readonly Stream _stream;
    private readonly bool _ownsStream;
    private SKDocument? _document;

    public PdfPrintJob(Stream stream, string title, bool ownsStream)
    {
        _stream = stream;
        _ownsStream = ownsStream;
        var metadata = new SKDocumentPdfMetadata
        {
            Title = title,
            Creator = "NetForms",
            Producer = "NetForms (SkiaSharp)",
            RasterDpi = 300,
        };
        _document = SKDocument.CreatePdf(stream, metadata);
    }

    /// <summary>Pages written so far (for the tests and the status dialog).</summary>
    public int PageCount { get; private set; }

    public override SKCanvas BeginPage(PageSettings settings, Rectangle pageBounds)
    {
        var document = _document ?? throw new ObjectDisposedException(nameof(PdfPrintJob));
        // PDF points are 1/72 inch; the page's own unit is 1/100 inch.
        var canvas = document.BeginPage(pageBounds.Width * 0.72f, pageBounds.Height * 0.72f);
        canvas.Scale(0.72f);
        return canvas;
    }

    public override void EndPage()
    {
        _document?.EndPage();
        PageCount++;
    }

    public override void Finish()
    {
        _document?.Close();
        _document?.Dispose();
        _document = null;
        _stream.Flush();
        if (_ownsStream) _stream.Dispose();
    }

    public override void Abort()
    {
        _document?.Abort();
        _document?.Dispose();
        _document = null;
        if (_ownsStream) _stream.Dispose();
    }

    public override void Dispose()
    {
        if (_document != null) Abort();
    }
}

/// <summary>
/// The printers of the machine. WinForms asks winspool and the driver; NetForms asks the same on Windows and
/// CUPS (lpstat, lpoptions, lp) on Linux and macOS - see docs/PLAN.md, decision 153.
/// </summary>
internal abstract class PrintBackend
{
    private static PrintBackend? s_current;

    /// <summary>The backend of this OS. Tests replace it with one whose printers they own.</summary>
    public static PrintBackend Current
    {
        get => s_current ??= OperatingSystem.IsWindows() ? new WindowsPrintBackend() : new CupsPrintBackend();
        set => s_current = value;
    }

    public abstract string[] InstalledPrinters { get; }

    /// <summary>The default printer, or null when none is set.</summary>
    public abstract string? DefaultPrinter { get; }

    /// <summary>What <paramref name="printer"/> can do, or null when there is no such printer.</summary>
    public abstract PrinterInfo? GetPrinter(string printer);

    /// <summary>Start a job on <paramref name="settings"/>' printer.</summary>
    public abstract PrintJob StartJob(PrinterSettings settings, string documentName);

    /// <summary>
    /// Where PrintToFile writes when PrintFileName is not set - WinForms' spooler asks with a "Save Print Output As"
    /// dialog. NetForms (the WinForms layer) plugs its SaveFileDialog in here; null means cancelled.
    /// </summary>
    public static Func<string, string?>? PrintFilePrompt { get; set; }

    // --- paper -------------------------------------------------------------------------------------------

    private static readonly (PaperKind Kind, string Name, int Width, int Height, string[] Aliases)[] s_paper =
    [
        (PaperKind.Letter, "Letter", 850, 1100, ["Letter", "na_letter_8.5x11in", "LetterSmall"]),
        (PaperKind.Legal, "Legal", 850, 1400, ["Legal", "na_legal_8.5x14in"]),
        (PaperKind.Executive, "Executive", 725, 1050, ["Executive", "na_executive_7.25x10.5in"]),
        (PaperKind.A3, "A3", 1169, 1654, ["A3", "iso_a3_297x420mm"]),
        (PaperKind.A4, "A4", 827, 1169, ["A4", "iso_a4_210x297mm", "A4Small"]),
        (PaperKind.A5, "A5", 583, 827, ["A5", "iso_a5_148x210mm"]),
        (PaperKind.A6, "A6", 413, 583, ["A6", "iso_a6_105x148mm"]),
        (PaperKind.B4, "B4 (JIS)", 1012, 1433, ["B4", "jis_b4_257x364mm"]),
        (PaperKind.B5, "B5 (JIS)", 717, 1012, ["B5", "jis_b5_182x257mm"]),
        (PaperKind.Tabloid, "Tabloid", 1100, 1700, ["Tabloid", "na_ledger_11x17in", "11x17"]),
        (PaperKind.Ledger, "Ledger", 1700, 1100, ["Ledger"]),
        (PaperKind.Statement, "Statement", 550, 850, ["Statement", "na_invoice_5.5x8.5in"]),
        (PaperKind.Folio, "Folio", 850, 1300, ["Folio", "na_foolscap_8.5x13in", "FanFoldGermanLegal"]),
        (PaperKind.Standard10x14, "10x14", 1000, 1400, ["10x14"]),
        (PaperKind.Number10Envelope, "Envelope #10", 413, 950, ["Env10", "na_number-10_4.125x9.5in"]),
        (PaperKind.DLEnvelope, "Envelope DL", 433, 866, ["EnvDL", "iso_dl_110x220mm", "DL"]),
        (PaperKind.C5Envelope, "Envelope C5", 638, 902, ["EnvC5", "iso_c5_162x229mm"]),
        (PaperKind.C6Envelope, "Envelope C6", 449, 638, ["EnvC6", "iso_c6_114x162mm"]),
        (PaperKind.MonarchEnvelope, "Envelope Monarch", 388, 750, ["EnvMonarch", "na_monarch_3.875x7.5in"]),
        (PaperKind.JapanesePostcard, "Japanese Postcard", 394, 583, ["Postcard", "jpn_hagaki_100x148mm"]),
    ];

    /// <summary>The paper sizes offered when the printer does not list its own: the common ones.</summary>
    public static PaperSize[] StandardPaperSizes()
    {
        var result = new PaperSize[s_paper.Length];
        for (int i = 0; i < s_paper.Length; i++) result[i] = new PaperSize(s_paper[i].Kind, s_paper[i].Name, s_paper[i].Width, s_paper[i].Height);
        return result;
    }

    /// <summary>The paper a printer without settings starts with: A4, or Letter where the region is not metric.</summary>
    public static int DefaultPaperIndex(PaperSize[] sizes)
    {
        var wanted = RegionUsesLetter() ? PaperKind.Letter : PaperKind.A4;
        for (int i = 0; i < sizes.Length; i++) if (sizes[i].Kind == wanted) return i;
        return 0;
    }

    private static bool RegionUsesLetter()
    {
        try
        {
            return !RegionInfo.CurrentRegion.IsMetric || RegionInfo.CurrentRegion.TwoLetterISORegionName is "US" or "CA" or "MX";
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>A paper named the CUPS/PPD way ("A4", "Letter", "iso_a4_210x297mm", "Custom.200x300mm").</summary>
    public static PaperSize? PaperFromCupsName(string name, string? displayName)
    {
        foreach (var p in s_paper)
        {
            foreach (var alias in p.Aliases)
            {
                if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
                    return new PaperSize(p.Kind, displayName ?? p.Name, p.Width, p.Height);
            }
        }
        // IPP self-describing names: class_name_WxHunit.
        int underscore = name.LastIndexOf('_');
        var dims = underscore >= 0 ? name[(underscore + 1)..] : null;
        if (dims != null && (dims.EndsWith("mm", StringComparison.Ordinal) || dims.EndsWith("in", StringComparison.Ordinal)))
        {
            bool mm = dims.EndsWith("mm", StringComparison.Ordinal);
            var parts = dims[..^2].Split('x');
            if (parts.Length == 2
                && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
            {
                double k = mm ? 100 / 25.4 : 100;
                return new PaperSize(PaperKind.Custom, displayName ?? name, (int)Math.Round(w * k), (int)Math.Round(h * k));
            }
        }
        return null;
    }
}

/// <summary>No printer at all: what a machine without a print system looks like.</summary>
internal sealed class NoPrintBackend : PrintBackend
{
    public override string[] InstalledPrinters => [];
    public override string? DefaultPrinter => null;
    public override PrinterInfo? GetPrinter(string printer) => null;
    public override PrintJob StartJob(PrinterSettings settings, string documentName) => throw new InvalidPrinterException(settings);
}

/// <summary>
/// CUPS, the print system of Linux and macOS: lpstat lists the queues, lpoptions -l tells what one can do
/// (page sizes, trays, resolutions, duplex, colour), and a job is a PDF handed to lp.
/// </summary>
internal sealed class CupsPrintBackend : PrintBackend
{
    private readonly Dictionary<string, (DateTime At, PrinterInfo? Info)> _cache = new(StringComparer.Ordinal);

    public override string[] InstalledPrinters
    {
        get
        {
            // lpstat -e (CUPS 2) prints one queue per line; older CUPS only has -a ("NAME accepting requests since ...").
            var names = Run("lpstat", "-e");
            if (names == null)
            {
                var accepting = Run("lpstat", "-a");
                if (accepting == null) return [];
                var list = new List<string>();
                foreach (var line in Lines(accepting))
                {
                    int space = line.IndexOf(' ');
                    list.Add(space > 0 ? line[..space] : line);
                }
                return list.ToArray();
            }
            return new List<string>(Lines(names)).ToArray();
        }
    }

    public override string? DefaultPrinter
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("PRINTER");
            if (!string.IsNullOrEmpty(env)) return env;
            var output = Run("lpstat", "-d");
            if (output == null) return null;
            // "system default destination: Name" (localized in some locales: take what follows the colon).
            foreach (var line in Lines(output))
            {
                int colon = line.IndexOf(':');
                if (colon > 0 && line.Contains("default", StringComparison.OrdinalIgnoreCase)) return line[(colon + 1)..].Trim();
            }
            return null;
        }
    }

    public override PrinterInfo? GetPrinter(string printer)
    {
        if (string.IsNullOrEmpty(printer)) return null;
        lock (_cache)
        {
            if (_cache.TryGetValue(printer, out var cached) && DateTime.UtcNow - cached.At < TimeSpan.FromSeconds(30)) return cached.Info;
        }
        PrinterInfo? info = null;
        if (Array.IndexOf(InstalledPrinters, printer) >= 0)
        {
            var options = Run("lpoptions", "-p", printer, "-l");
            info = Parse(printer, options ?? string.Empty);
        }
        lock (_cache) _cache[printer] = (DateTime.UtcNow, info);
        return info;
    }

    /// <summary>
    /// Reads "lpoptions -l": one option per line, "Keyword/Display name: value *default value ...".
    /// Internal so the tests can feed it a PPD's options without CUPS.
    /// </summary>
    internal static PrinterInfo Parse(string printer, string options)
    {
        var papers = new List<PaperSize>();
        var sources = new List<PaperSource>();
        var resolutions = new List<PrinterResolution>();
        int defaultPaper = -1, defaultSource = 0, defaultResolution = 0;
        bool canDuplex = false, color = true, defaultColor = true;
        var defaultDuplex = Duplex.Simplex;

        foreach (var line in Lines(options))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var keyword = line[..colon];
            int slash = keyword.IndexOf('/');
            if (slash >= 0) keyword = keyword[..slash];
            var values = line[(colon + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (keyword)
            {
                case "PageSize":
                case "media":
                    foreach (var raw in values)
                    {
                        bool isDefault = raw.StartsWith('*');
                        var value = isDefault ? raw[1..] : raw;
                        if (PaperFromCupsName(value, null) is not { } paper) continue;
                        if (isDefault) defaultPaper = papers.Count;
                        papers.Add(paper);
                    }
                    break;
                case "InputSlot":
                    foreach (var raw in values)
                    {
                        bool isDefault = raw.StartsWith('*');
                        var value = isDefault ? raw[1..] : raw;
                        var kind = value.ToLowerInvariant() switch
                        {
                            "auto" or "default" => PaperSourceKind.AutomaticFeed,
                            "upper" or "tray1" => PaperSourceKind.Upper,
                            "lower" or "tray2" => PaperSourceKind.Lower,
                            "middle" => PaperSourceKind.Middle,
                            "manual" or "manualfeed" => PaperSourceKind.ManualFeed,
                            "envelope" => PaperSourceKind.Envelope,
                            _ => (PaperSourceKind)(256 + sources.Count),
                        };
                        if (isDefault) defaultSource = sources.Count;
                        sources.Add(new PaperSource(kind, value));
                    }
                    break;
                case "Resolution":
                    foreach (var raw in values)
                    {
                        bool isDefault = raw.StartsWith('*');
                        var value = (isDefault ? raw[1..] : raw).Replace("dpi", "", StringComparison.OrdinalIgnoreCase);
                        var xy = value.Split('x');
                        if (!int.TryParse(xy[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)) continue;
                        int y = xy.Length > 1 && int.TryParse(xy[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var yy) ? yy : x;
                        if (isDefault) defaultResolution = resolutions.Count;
                        resolutions.Add(new PrinterResolution(PrinterResolutionKind.Custom, x, y));
                    }
                    break;
                case "Duplex":
                case "sides":
                    foreach (var raw in values)
                    {
                        bool isDefault = raw.StartsWith('*');
                        var value = isDefault ? raw[1..] : raw;
                        var duplex = value switch
                        {
                            "DuplexNoTumble" or "two-sided-long-edge" => Duplex.Vertical,
                            "DuplexTumble" or "two-sided-short-edge" => Duplex.Horizontal,
                            _ => Duplex.Simplex,
                        };
                        if (duplex != Duplex.Simplex) canDuplex = true;
                        if (isDefault) defaultDuplex = duplex;
                    }
                    break;
                case "ColorModel":
                case "print-color-mode":
                    {
                        bool anyColor = false;
                        foreach (var raw in values)
                        {
                            bool isDefault = raw.StartsWith('*');
                            var value = isDefault ? raw[1..] : raw;
                            bool isColor = !(value.Contains("Gray", StringComparison.OrdinalIgnoreCase)
                                || value.Contains("Mono", StringComparison.OrdinalIgnoreCase)
                                || value.Contains("Black", StringComparison.OrdinalIgnoreCase));
                            anyColor |= isColor;
                            if (isDefault) defaultColor = isColor;
                        }
                        color = anyColor;
                        if (!color) defaultColor = false;
                        break;
                    }
            }
        }

        var paperArray = papers.Count > 0 ? papers.ToArray() : StandardPaperSizes();
        return new PrinterInfo
        {
            Name = printer,
            PaperSizes = paperArray,
            DefaultPaperIndex = defaultPaper >= 0 ? defaultPaper : DefaultPaperIndex(paperArray),
            PaperSources = sources.Count > 0 ? sources.ToArray() : [new PaperSource(PaperSourceKind.AutomaticFeed, "Automatically Select")],
            DefaultSourceIndex = defaultSource,
            Resolutions = resolutions.Count > 0 ? resolutions.ToArray() : [new PrinterResolution(PrinterResolutionKind.Custom, 300, 300)],
            DefaultResolutionIndex = defaultResolution,
            CanDuplex = canDuplex,
            DefaultDuplex = defaultDuplex,
            SupportsColor = color,
            DefaultColor = defaultColor,
        };
    }

    public override PrintJob StartJob(PrinterSettings settings, string documentName)
    {
        var path = Path.Combine(Path.GetTempPath(), "netforms-print-" + Guid.NewGuid().ToString("N") + ".pdf");
        return new CupsPrintJob(path, settings, documentName);
    }

    private sealed class CupsPrintJob : PdfPrintJob
    {
        private readonly string _path;
        private readonly string _printer;
        private readonly string _title;
        private readonly int _copies;
        private readonly bool _collate;
        private readonly Duplex _duplex;
        private readonly bool _monochrome;

        public CupsPrintJob(string path, PrinterSettings settings, string title)
            : base(File.Create(path), title, ownsStream: true)
        {
            _path = path;
            _printer = settings.PrinterName;
            _title = title;
            _copies = Math.Max(1, (int)settings.Copies);
            _collate = settings.Collate;
            _duplex = settings.Duplex;
            _monochrome = !settings.DefaultPageSettings.Color;
        }

        public override void Finish()
        {
            base.Finish();
            try
            {
                // The pages are already the size and orientation of the paper: lp gets them as they are.
                var args = new List<string> { "-d", _printer, "-t", _title, "-n", _copies.ToString(CultureInfo.InvariantCulture) };
                if (_copies > 1) { args.Add("-o"); args.Add(_collate ? "collate=true" : "collate=false"); }
                args.Add("-o");
                args.Add(_duplex switch { Duplex.Vertical => "sides=two-sided-long-edge", Duplex.Horizontal => "sides=two-sided-short-edge", _ => "sides=one-sided" });
                if (_monochrome) { args.Add("-o"); args.Add("print-color-mode=monochrome"); }
                args.Add("--");
                args.Add(_path);
                var result = RunWithStatus("lp", args.ToArray());
                if (result.ExitCode != 0)
                    throw new InvalidOperationException($"lp could not print to '{_printer}': {result.Error.Trim()}");
            }
            finally
            {
                TryDelete(_path);
            }
        }

        public override void Abort()
        {
            base.Abort();
            TryDelete(_path);
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    private static IEnumerable<string> Lines(string text)
    {
        foreach (var line in text.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length > 0) yield return t;
        }
    }

    private static string? Run(string file, params string[] args)
    {
        var result = RunWithStatus(file, args);
        return result.ExitCode == 0 ? result.Output : null;
    }

    private static (int ExitCode, string Output, string Error) RunWithStatus(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);
            // The C locale: lpstat and lpoptions answer in English, which Parse understands.
            psi.Environment["LC_ALL"] = "C";
            using var process = Process.Start(psi);
            if (process == null) return (-1, "", "");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(10_000))
            {
                try { process.Kill(); } catch (InvalidOperationException) { }
                return (-1, "", "timed out");
            }
            return (process.ExitCode, output.Result, error.Result);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No CUPS client tools: no printers.
            return (-1, "", "");
        }
    }
}

/// <summary>
/// Windows: winspool lists the printers and the driver reports its capabilities (DeviceCapabilities), and a
/// job goes through GDI - each page is drawn by Skia into a bitmap at the printer's resolution (capped at
/// 300 dpi) and blitted to the printer DC, which is what the spooler then renders.
/// </summary>
internal sealed partial class WindowsPrintBackend : PrintBackend
{
    public override string[] InstalledPrinters => Win32Printing.EnumPrinters();

    public override string? DefaultPrinter => Win32Printing.GetDefaultPrinter();

    public override PrinterInfo? GetPrinter(string printer) => Win32Printing.GetPrinterInfo(printer);

    public override PrintJob StartJob(PrinterSettings settings, string documentName) => new GdiPrintJob(settings, documentName);
}
