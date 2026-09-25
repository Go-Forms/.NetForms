// The public surface and the semantics follow dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing/PageSettings.cs,
// MIT - THIRD-PARTY-NOTICES.md); what WinForms reads from the driver's DEVMODE comes from PrintBackend here.
using System.Runtime.InteropServices;

namespace System.Drawing.Printing;

/// <summary>How one page prints: paper, tray, orientation, margins, colour, resolution. Unset values are the printer's.</summary>
public class PageSettings : ICloneable
{
    private PrinterSettings _printerSettings;
    private bool? _color;
    private PaperSize? _paperSize;
    private PaperSource? _paperSource;
    private PrinterResolution? _printerResolution;
    private bool? _landscape;
    private Margins _margins = new();

    public PageSettings() : this(new PrinterSettings())
    {
    }

    public PageSettings(PrinterSettings printerSettings)
    {
        _printerSettings = printerSettings;
    }

    /// <summary>The page in hundredths of an inch, turned for landscape.</summary>
    public Rectangle Bounds
    {
        get
        {
            var size = PaperSize;
            return Landscape ? new Rectangle(0, 0, size.Height, size.Width) : new Rectangle(0, 0, size.Width, size.Height);
        }
    }

    public bool Color
    {
        get => _color ?? (_printerSettings.Info is { } info ? info.SupportsColor && info.DefaultColor : true);
        set => _color = value;
    }

    public float HardMarginX => _printerSettings.Info?.HardMarginX ?? 0f;

    public float HardMarginY => _printerSettings.Info?.HardMarginY ?? 0f;

    public bool Landscape
    {
        get => _landscape ?? _printerSettings.Info?.DefaultLandscape ?? false;
        set => _landscape = value;
    }

    public Margins Margins
    {
        get => _margins;
        set => _margins = value;
    }

    public PaperSize PaperSize
    {
        get
        {
            if (_paperSize != null) return _paperSize;
            var info = _printerSettings.Info;
            var sizes = info?.PaperSizes ?? PrintBackend.StandardPaperSizes();
            int index = info?.DefaultPaperIndex ?? PrintBackend.DefaultPaperIndex(sizes);
            return sizes[Math.Clamp(index, 0, sizes.Length - 1)];
        }
        set => _paperSize = value;
    }

    public PaperSource PaperSource
    {
        get
        {
            if (_paperSource != null) return _paperSource;
            var sources = _printerSettings.Get_PaperSources();
            return sources[Math.Clamp(_printerSettings.Info?.DefaultSourceIndex ?? 0, 0, sources.Length - 1)];
        }
        set => _paperSource = value;
    }

    /// <summary>The printable part of the page: inside the hard margins, in hundredths of an inch.</summary>
    public RectangleF PrintableArea
    {
        get
        {
            var bounds = Bounds;
            float x = HardMarginX, y = HardMarginY;
            if (Landscape) (x, y) = (y, x);
            return new RectangleF(x, y, Math.Max(0, bounds.Width - 2 * x), Math.Max(0, bounds.Height - 2 * y));
        }
    }

    public PrinterResolution PrinterResolution
    {
        get
        {
            if (_printerResolution != null) return _printerResolution;
            var resolutions = _printerSettings.Get_PrinterResolutions();
            return resolutions[Math.Clamp(_printerSettings.Info?.DefaultResolutionIndex ?? 0, 0, resolutions.Length - 1)];
        }
        set => _printerResolution = value;
    }

    public PrinterSettings PrinterSettings
    {
        get => _printerSettings;
        set => _printerSettings = value ?? new PrinterSettings();
    }

    public object Clone()
    {
        var result = (PageSettings)MemberwiseClone();
        result._margins = (Margins)_margins.Clone();
        return result;
    }

    public void CopyToHdevmode(IntPtr hdevmode)
    {
        if (hdevmode == IntPtr.Zero) throw new ArgumentNullException(nameof(hdevmode));
        if (_color is { } color) DevModeLayout.Write(hdevmode, DevModeLayout.Color, (short)(color ? 2 : 1), DevModeLayout.DM_COLOR);
        if (_landscape is { } landscape) DevModeLayout.Write(hdevmode, DevModeLayout.Orientation, (short)(landscape ? 2 : 1), DevModeLayout.DM_ORIENTATION);
        var paper = PaperSize;
        DevModeLayout.Write(hdevmode, DevModeLayout.PaperSize, (short)paper.RawKind, DevModeLayout.DM_PAPERSIZE);
        // dmPaperLength and dmPaperWidth are tenths of a millimetre.
        DevModeLayout.Write(hdevmode, DevModeLayout.PaperLength,
            (short)PrinterUnitConvert.Convert(paper.Height, PrinterUnit.Display, PrinterUnit.TenthsOfAMillimeter), DevModeLayout.DM_PAPERLENGTH);
        DevModeLayout.Write(hdevmode, DevModeLayout.PaperWidth,
            (short)PrinterUnitConvert.Convert(paper.Width, PrinterUnit.Display, PrinterUnit.TenthsOfAMillimeter), DevModeLayout.DM_PAPERWIDTH);
        if (_paperSource is { } source) DevModeLayout.Write(hdevmode, DevModeLayout.DefaultSource, (short)source.RawKind, DevModeLayout.DM_DEFAULTSOURCE);
        if (_printerResolution is { } resolution)
        {
            if (resolution.Kind == PrinterResolutionKind.Custom)
            {
                DevModeLayout.Write(hdevmode, DevModeLayout.PrintQuality, (short)resolution.X, DevModeLayout.DM_PRINTQUALITY);
                DevModeLayout.Write(hdevmode, DevModeLayout.YResolution, (short)resolution.Y, DevModeLayout.DM_YRESOLUTION);
            }
            else
            {
                DevModeLayout.Write(hdevmode, DevModeLayout.PrintQuality, (short)resolution.Kind, DevModeLayout.DM_PRINTQUALITY);
            }
        }
    }

    public void SetHdevmode(IntPtr hdevmode)
    {
        if (hdevmode == IntPtr.Zero) throw new ArgumentException($"Handle {hdevmode} is not valid.");
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_COLOR)) _color = DevModeLayout.Read(hdevmode, DevModeLayout.Color) == 2;
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_ORIENTATION)) _landscape = DevModeLayout.Read(hdevmode, DevModeLayout.Orientation) == 2;

        var sizes = _printerSettings.Get_PaperSizes();
        _paperSize = null;
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_PAPERSIZE))
        {
            short kind = DevModeLayout.Read(hdevmode, DevModeLayout.PaperSize);
            foreach (var s in sizes) if (s.RawKind == kind) _paperSize = s;
        }
        _paperSize ??= new PaperSize(PaperKind.Custom, "custom",
            PrinterUnitConvert.Convert(DevModeLayout.Read(hdevmode, DevModeLayout.PaperWidth), PrinterUnit.TenthsOfAMillimeter, PrinterUnit.Display),
            PrinterUnitConvert.Convert(DevModeLayout.Read(hdevmode, DevModeLayout.PaperLength), PrinterUnit.TenthsOfAMillimeter, PrinterUnit.Display));

        short sourceKind = DevModeLayout.Read(hdevmode, DevModeLayout.DefaultSource);
        _paperSource = null;
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_DEFAULTSOURCE))
        {
            foreach (var s in _printerSettings.Get_PaperSources()) if ((short)s.RawKind == sourceKind) _paperSource = s;
        }
        _paperSource ??= new PaperSource((PaperSourceKind)sourceKind, "unknown");

        short quality = DevModeLayout.Read(hdevmode, DevModeLayout.PrintQuality);
        short y = DevModeLayout.Read(hdevmode, DevModeLayout.YResolution);
        _printerResolution = quality < 0
            ? new PrinterResolution((PrinterResolutionKind)quality, -1, -1)
            : new PrinterResolution(PrinterResolutionKind.Custom, quality, y);
    }

    public override string ToString() =>
        $"[{nameof(PageSettings)}: Color={Color}, Landscape={Landscape}, Margins={Margins}, PaperSize={PaperSize}, PaperSource={PaperSource}, PrinterResolution={PrinterResolution}]";
}
