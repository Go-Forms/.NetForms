using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing.Printing;

/// <summary>winspool and GDI, for the Windows print backend. Only ever called on Windows.</summary>
internal static class Win32Printing
{
    private const uint PRINTER_ENUM_LOCAL = 0x2;
    private const uint PRINTER_ENUM_CONNECTIONS = 0x4;

    private const ushort DC_PAPERS = 2;
    private const ushort DC_PAPERSIZE = 3;
    private const ushort DC_BINS = 6;
    private const ushort DC_DUPLEX = 7;
    private const ushort DC_BINNAMES = 12;
    private const ushort DC_ENUMRESOLUTIONS = 13;
    private const ushort DC_PAPERNAMES = 16;
    private const ushort DC_ORIENTATION = 17;
    private const ushort DC_COPIES = 18;
    private const ushort DC_COLORDEVICE = 32;

    internal const int HORZRES = 8;
    internal const int VERTRES = 10;
    internal const int LOGPIXELSX = 88;
    internal const int LOGPIXELSY = 90;
    internal const int PHYSICALOFFSETX = 112;
    internal const int PHYSICALOFFSETY = 113;

    private const uint DM_OUT_BUFFER = 2;
    private const uint DM_IN_BUFFER = 8;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_4
    {
        public IntPtr pPrinterName;
        public IntPtr pServerName;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DOCINFO
    {
        public int cbSize;
        public string lpszDocName;
        public string? lpszOutput;
        public string? lpszDatatype;
        public uint fwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "EnumPrintersW")]
    private static extern bool EnumPrintersNative(uint flags, string? name, uint level, IntPtr buffer, uint size, out uint needed, out uint returned);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetDefaultPrinterW")]
    private static extern bool GetDefaultPrinterNative(char[]? buffer, ref uint size);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DeviceCapabilitiesW")]
    private static extern int DeviceCapabilities(string device, string? port, ushort capability, IntPtr output, IntPtr devmode);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "OpenPrinterW")]
    private static extern bool OpenPrinter(string name, out IntPtr handle, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr handle);

    [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "DocumentPropertiesW")]
    private static extern int DocumentProperties(IntPtr hwnd, IntPtr printer, string device, IntPtr output, IntPtr input, uint mode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateDCW")]
    internal static extern IntPtr CreateDC(string? driver, string device, string? port, IntPtr devmode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateICW")]
    private static extern IntPtr CreateIC(string? driver, string device, string? port, IntPtr devmode);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "ResetDCW")]
    internal static extern IntPtr ResetDC(IntPtr hdc, IntPtr devmode);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    internal static extern int GetDeviceCaps(IntPtr hdc, int index);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "StartDocW")]
    internal static extern int StartDoc(IntPtr hdc, ref DOCINFO info);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int StartPage(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int EndPage(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int EndDoc(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int AbortDoc(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int wDest, int hDest, int xSrc, int ySrc, int wSrc, int hSrc,
        IntPtr bits, ref BITMAPINFOHEADER info, uint usage, uint rop);

    public static string[] EnumPrinters()
    {
        const uint level = 4;
        uint flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;
        EnumPrintersNative(flags, null, level, IntPtr.Zero, 0, out uint needed, out _);
        if (needed == 0) return [];
        var buffer = Marshal.AllocHGlobal((int)needed);
        try
        {
            if (!EnumPrintersNative(flags, null, level, buffer, needed, out _, out uint count)) throw new Win32Exception();
            var names = new string[count];
            int size = Marshal.SizeOf<PRINTER_INFO_4>();
            for (int i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PRINTER_INFO_4>(buffer + i * size);
                names[i] = Marshal.PtrToStringUni(info.pPrinterName) ?? string.Empty;
            }
            return names;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public static string? GetDefaultPrinter()
    {
        uint size = 0;
        GetDefaultPrinterNative(null, ref size);
        if (size == 0) return null;
        var buffer = new char[size];
        if (!GetDefaultPrinterNative(buffer, ref size)) return null;
        return new string(buffer, 0, (int)Math.Max(0, size - 1));
    }

    public static PrinterInfo? GetPrinterInfo(string printer)
    {
        if (string.IsNullOrEmpty(printer)) return null;
        int copies = DeviceCapabilities(printer, null, DC_COPIES, IntPtr.Zero, IntPtr.Zero);
        if (copies == -1) return null;

        var papers = ReadWords(printer, DC_PAPERS);
        var paperNames = ReadNames(printer, DC_PAPERNAMES, 64, papers.Length);
        var paperDims = ReadPoints(printer, DC_PAPERSIZE, papers.Length);
        var sizes = new List<PaperSize>();
        for (int i = 0; i < papers.Length; i++)
        {
            // DC_PAPERSIZE is in tenths of a millimetre.
            int w = PrinterUnitConvert.Convert(paperDims[i].X, PrinterUnit.TenthsOfAMillimeter, PrinterUnit.Display);
            int h = PrinterUnitConvert.Convert(paperDims[i].Y, PrinterUnit.TenthsOfAMillimeter, PrinterUnit.Display);
            sizes.Add(new PaperSize((PaperKind)papers[i], i < paperNames.Length ? paperNames[i] : "", w, h));
        }

        var bins = ReadWords(printer, DC_BINS);
        var binNames = ReadNames(printer, DC_BINNAMES, 24, bins.Length);
        var sources = new List<PaperSource>();
        for (int i = 0; i < bins.Length; i++) sources.Add(new PaperSource((PaperSourceKind)bins[i], i < binNames.Length ? binNames[i] : ""));

        var resolutions = new List<PrinterResolution>
        {
            new(PrinterResolutionKind.High, -4, -1),
            new(PrinterResolutionKind.Medium, -3, -1),
            new(PrinterResolutionKind.Low, -2, -1),
            new(PrinterResolutionKind.Draft, -1, -1),
        };
        int resolutionCount = DeviceCapabilities(printer, null, DC_ENUMRESOLUTIONS, IntPtr.Zero, IntPtr.Zero);
        if (resolutionCount > 0)
        {
            var buffer = Marshal.AllocHGlobal(resolutionCount * 8);
            try
            {
                DeviceCapabilities(printer, null, DC_ENUMRESOLUTIONS, buffer, IntPtr.Zero);
                for (int i = 0; i < resolutionCount; i++)
                    resolutions.Add(new PrinterResolution(PrinterResolutionKind.Custom, Marshal.ReadInt32(buffer, i * 8), Marshal.ReadInt32(buffer, i * 8 + 4)));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // The driver's defaults, from its DEVMODE.
        using var mode = DevMode.ForPrinter(printer);
        int defaultPaper = 0, defaultSource = 0, defaultResolution = resolutions.Count - 1;
        if (mode != null)
        {
            for (int i = 0; i < sizes.Count; i++) if (sizes[i].RawKind == mode.PaperSize) defaultPaper = i;
            for (int i = 0; i < sources.Count; i++) if (sources[i].RawKind == mode.DefaultSource) defaultSource = i;
            for (int i = 0; i < resolutions.Count; i++)
                if (resolutions[i].Kind == PrinterResolutionKind.Custom && resolutions[i].X == mode.PrintQuality && resolutions[i].Y == mode.YResolution) defaultResolution = i;
        }

        float hardX = 0, hardY = 0;
        var ic = CreateIC(null, printer, null, mode?.Handle ?? IntPtr.Zero);
        if (ic != IntPtr.Zero)
        {
            try
            {
                int dpiX = Math.Max(1, GetDeviceCaps(ic, LOGPIXELSX));
                int dpiY = Math.Max(1, GetDeviceCaps(ic, LOGPIXELSY));
                hardX = GetDeviceCaps(ic, PHYSICALOFFSETX) * 100f / dpiX;
                hardY = GetDeviceCaps(ic, PHYSICALOFFSETY) * 100f / dpiY;
            }
            finally
            {
                DeleteDC(ic);
            }
        }

        var paperArray = sizes.Count > 0 ? sizes.ToArray() : PrintBackend.StandardPaperSizes();
        return new PrinterInfo
        {
            Name = printer,
            PaperSizes = paperArray,
            DefaultPaperIndex = sizes.Count > 0 ? defaultPaper : PrintBackend.DefaultPaperIndex(paperArray),
            PaperSources = sources.Count > 0 ? sources.ToArray() : [new PaperSource(PaperSourceKind.AutomaticFeed, "Automatically Select")],
            DefaultSourceIndex = defaultSource,
            Resolutions = resolutions.ToArray(),
            DefaultResolutionIndex = defaultResolution,
            MaximumCopies = copies,
            CanDuplex = DeviceCapabilities(printer, null, DC_DUPLEX, IntPtr.Zero, IntPtr.Zero) == 1,
            SupportsColor = DeviceCapabilities(printer, null, DC_COLORDEVICE, IntPtr.Zero, IntPtr.Zero) == 1,
            LandscapeAngle = Math.Max(0, DeviceCapabilities(printer, null, DC_ORIENTATION, IntPtr.Zero, IntPtr.Zero)),
            DefaultColor = mode == null || mode.Color != 1,
            DefaultDuplex = mode != null && mode.Duplex is 2 or 3 ? (Duplex)mode.Duplex : Duplex.Simplex,
            DefaultLandscape = mode != null && mode.Orientation == 2,
            DefaultCollate = mode != null && mode.Collate == 1,
            HardMarginX = hardX,
            HardMarginY = hardY,
        };
    }

    private static ushort[] ReadWords(string printer, ushort capability)
    {
        int count = DeviceCapabilities(printer, null, capability, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0) return [];
        var buffer = Marshal.AllocHGlobal(count * 2);
        try
        {
            DeviceCapabilities(printer, null, capability, buffer, IntPtr.Zero);
            var result = new ushort[count];
            for (int i = 0; i < count; i++) result[i] = (ushort)Marshal.ReadInt16(buffer, i * 2);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string[] ReadNames(string printer, ushort capability, int length, int expected)
    {
        int count = DeviceCapabilities(printer, null, capability, IntPtr.Zero, IntPtr.Zero);
        if (count <= 0) return [];
        var buffer = Marshal.AllocHGlobal(count * length * 2);
        try
        {
            DeviceCapabilities(printer, null, capability, buffer, IntPtr.Zero);
            var result = new string[count];
            for (int i = 0; i < count; i++)
            {
                var s = Marshal.PtrToStringUni(buffer + i * length * 2, length);
                int nul = s.IndexOf('\0');
                result[i] = nul >= 0 ? s[..nul] : s;
            }
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static Point[] ReadPoints(string printer, ushort capability, int expected)
    {
        int count = DeviceCapabilities(printer, null, capability, IntPtr.Zero, IntPtr.Zero);
        var result = new Point[Math.Max(count, expected)];
        if (count <= 0) return result;
        var buffer = Marshal.AllocHGlobal(count * 8);
        try
        {
            DeviceCapabilities(printer, null, capability, buffer, IntPtr.Zero);
            for (int i = 0; i < count; i++) result[i] = new Point(Marshal.ReadInt32(buffer, i * 8), Marshal.ReadInt32(buffer, i * 8 + 4));
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>A driver's DEVMODE for a printer (DocumentProperties), freed on Dispose.</summary>
    internal sealed class DevMode : IDisposable
    {
        public IntPtr Handle { get; private set; }

        private DevMode(IntPtr handle) => Handle = handle;

        public static DevMode? ForPrinter(string printer)
        {
            if (!OpenPrinter(printer, out var hPrinter, IntPtr.Zero)) return null;
            try
            {
                int size = DocumentProperties(IntPtr.Zero, hPrinter, printer, IntPtr.Zero, IntPtr.Zero, 0);
                if (size <= 0) return null;
                var buffer = Marshal.AllocHGlobal(size);
                if (DocumentProperties(IntPtr.Zero, hPrinter, printer, buffer, IntPtr.Zero, DM_OUT_BUFFER) < 0)
                {
                    Marshal.FreeHGlobal(buffer);
                    return null;
                }
                return new DevMode(buffer);
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }

        /// <summary>Hand the edited fields back to the driver so it validates and merges them.</summary>
        public void Merge(string printer)
        {
            if (!OpenPrinter(printer, out var hPrinter, IntPtr.Zero)) return;
            try
            {
                DocumentProperties(IntPtr.Zero, hPrinter, printer, Handle, Handle, DM_IN_BUFFER | DM_OUT_BUFFER);
            }
            finally
            {
                ClosePrinter(hPrinter);
            }
        }

        public short Orientation { get => DevModeLayout.Read(Handle, DevModeLayout.Orientation); set => DevModeLayout.Write(Handle, DevModeLayout.Orientation, value, DevModeLayout.DM_ORIENTATION); }
        public short PaperSize { get => DevModeLayout.Read(Handle, DevModeLayout.PaperSize); set => DevModeLayout.Write(Handle, DevModeLayout.PaperSize, value, DevModeLayout.DM_PAPERSIZE); }
        public short Copies { get => DevModeLayout.Read(Handle, DevModeLayout.Copies); set => DevModeLayout.Write(Handle, DevModeLayout.Copies, value, DevModeLayout.DM_COPIES); }
        public short DefaultSource { get => DevModeLayout.Read(Handle, DevModeLayout.DefaultSource); set => DevModeLayout.Write(Handle, DevModeLayout.DefaultSource, value, DevModeLayout.DM_DEFAULTSOURCE); }
        public short PrintQuality { get => DevModeLayout.Read(Handle, DevModeLayout.PrintQuality); set => DevModeLayout.Write(Handle, DevModeLayout.PrintQuality, value, DevModeLayout.DM_PRINTQUALITY); }
        public short Color { get => DevModeLayout.Read(Handle, DevModeLayout.Color); set => DevModeLayout.Write(Handle, DevModeLayout.Color, value, DevModeLayout.DM_COLOR); }
        public short Duplex { get => DevModeLayout.Read(Handle, DevModeLayout.Duplex); set => DevModeLayout.Write(Handle, DevModeLayout.Duplex, value, DevModeLayout.DM_DUPLEX); }
        public short YResolution { get => DevModeLayout.Read(Handle, DevModeLayout.YResolution); set => DevModeLayout.Write(Handle, DevModeLayout.YResolution, value, DevModeLayout.DM_YRESOLUTION); }
        public short Collate { get => DevModeLayout.Read(Handle, DevModeLayout.Collate); set => DevModeLayout.Write(Handle, DevModeLayout.Collate, value, DevModeLayout.DM_COLLATE); }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero) Marshal.FreeHGlobal(Handle);
            Handle = IntPtr.Zero;
        }
    }
}

/// <summary>
/// The byte layout of DEVMODEW (wingdi.h), which is the same on every OS: GetHdevmode/SetHdevmode hand
/// one out and read one back on Linux too, so settings survive a round trip there as on Windows.
/// </summary>
internal static class DevModeLayout
{
    public const int DeviceName = 0;      // WCHAR[32]
    public const int SpecVersion = 64;
    public const int Size = 68;
    public const int DriverExtra = 70;
    public const int Fields = 72;
    public const int Orientation = 76;
    public const int PaperSize = 78;
    public const int PaperLength = 80;
    public const int PaperWidth = 82;
    public const int Copies = 86;
    public const int DefaultSource = 88;
    public const int PrintQuality = 90;
    public const int Color = 92;
    public const int Duplex = 94;
    public const int YResolution = 96;
    public const int Collate = 100;
    public const int TotalSize = 220;

    public const int DM_ORIENTATION = 0x1;
    public const int DM_PAPERSIZE = 0x2;
    public const int DM_PAPERLENGTH = 0x4;
    public const int DM_PAPERWIDTH = 0x8;
    public const int DM_COPIES = 0x100;
    public const int DM_DEFAULTSOURCE = 0x200;
    public const int DM_PRINTQUALITY = 0x400;
    public const int DM_COLOR = 0x800;
    public const int DM_DUPLEX = 0x1000;
    public const int DM_YRESOLUTION = 0x2000;
    public const int DM_COLLATE = 0x8000;

    public static bool Has(IntPtr mode, int field) => (Marshal.ReadInt32(mode, Fields) & field) != 0;

    public static short Read(IntPtr mode, int offset) => Marshal.ReadInt16(mode, offset);

    public static void Write(IntPtr mode, int offset, short value, int field)
    {
        Marshal.WriteInt16(mode, offset, value);
        Marshal.WriteInt32(mode, Fields, Marshal.ReadInt32(mode, Fields) | field);
    }

    /// <summary>A fresh DEVMODEW with no driver-private bytes.</summary>
    public static IntPtr Allocate(string deviceName)
    {
        var mode = Marshal.AllocHGlobal(TotalSize);
        for (int i = 0; i < TotalSize; i++) Marshal.WriteByte(mode, i, 0);
        var name = deviceName.Length > 31 ? deviceName[..31] : deviceName;
        for (int i = 0; i < name.Length; i++) Marshal.WriteInt16(mode, DeviceName + i * 2, name[i]);
        Marshal.WriteInt16(mode, SpecVersion, 0x0401);
        Marshal.WriteInt16(mode, Size, TotalSize);
        return mode;
    }

    public static string ReadDeviceName(IntPtr mode)
    {
        var s = Marshal.PtrToStringUni(mode + DeviceName, 32);
        int nul = s.IndexOf('\0');
        return nul >= 0 ? s[..nul] : s;
    }
}

/// <summary>A job on a Windows printer through GDI: StartDoc, per page a Skia bitmap blitted with StretchDIBits.</summary>
internal sealed class GdiPrintJob : PrintJob
{
    private const int MaxRenderDpi = 300;
    private readonly string _printer;
    private readonly Win32Printing.DevMode? _mode;
    private IntPtr _hdc;
    private SKBitmap? _page;
    private SKCanvas? _canvas;
    private bool _landscape;
    private float _dpi = 300f;

    public GdiPrintJob(PrinterSettings settings, string documentName)
    {
        _printer = settings.PrinterName;
        _mode = Win32Printing.DevMode.ForPrinter(_printer);
        var page = settings.DefaultPageSettings;
        if (_mode != null)
        {
            _mode.Copies = settings.Copies;
            _mode.Collate = (short)(settings.Collate ? 1 : 0);
            if (settings.Duplex != Duplex.Default) _mode.Duplex = (short)settings.Duplex;
            ApplyPage(page);
            _mode.Merge(_printer);
        }
        _landscape = page.Landscape;
        _hdc = Win32Printing.CreateDC(null, _printer, null, _mode?.Handle ?? IntPtr.Zero);
        if (_hdc == IntPtr.Zero) throw new InvalidPrinterException(settings);
        var info = new Win32Printing.DOCINFO
        {
            cbSize = Marshal.SizeOf<Win32Printing.DOCINFO>(),
            lpszDocName = documentName,
            lpszOutput = settings.PrintToFile && !string.IsNullOrEmpty(settings.PrintFileName) ? settings.PrintFileName : null,
        };
        if (Win32Printing.StartDoc(_hdc, ref info) <= 0)
        {
            Win32Printing.DeleteDC(_hdc);
            _hdc = IntPtr.Zero;
            throw new Win32Exception();
        }
    }

    public override float Dpi => _dpi;

    private void ApplyPage(PageSettings page)
    {
        if (_mode == null) return;
        _mode.Orientation = (short)(page.Landscape ? 2 : 1);
        _mode.PaperSize = (short)page.PaperSize.RawKind;
        _mode.DefaultSource = (short)page.PaperSource.RawKind;
        _mode.Color = (short)(page.Color ? 2 : 1);
    }

    public override SKCanvas BeginPage(PageSettings settings, Rectangle pageBounds)
    {
        if (_mode != null && settings.Landscape != _landscape)
        {
            // A page of another orientation (QueryPageSettings): reset the DC between pages, as WinForms does.
            ApplyPage(settings);
            _mode.Merge(_printer);
            Win32Printing.ResetDC(_hdc, _mode.Handle);
            _landscape = settings.Landscape;
        }
        Win32Printing.StartPage(_hdc);
        int deviceDpi = Math.Max(1, Win32Printing.GetDeviceCaps(_hdc, Win32Printing.LOGPIXELSX));
        _dpi = deviceDpi;
        float renderDpi = Math.Min(deviceDpi, MaxRenderDpi);
        int width = Win32Printing.GetDeviceCaps(_hdc, Win32Printing.HORZRES);
        int height = Win32Printing.GetDeviceCaps(_hdc, Win32Printing.VERTRES);
        int bw = Math.Max(1, (int)Math.Ceiling(width * renderDpi / deviceDpi));
        int bh = Math.Max(1, (int)Math.Ceiling(height * renderDpi / deviceDpi));
        _page = new SKBitmap(new SKImageInfo(bw, bh, SKColorType.Bgra8888, SKAlphaType.Premul));
        _canvas = new SKCanvas(_page);
        _canvas.Clear(SKColors.White);
        _canvas.Scale(renderDpi / 100f);
        return _canvas;
    }

    public override void EndPage()
    {
        if (_page != null)
        {
            _canvas?.Flush();
            int width = Win32Printing.GetDeviceCaps(_hdc, Win32Printing.HORZRES);
            int height = Win32Printing.GetDeviceCaps(_hdc, Win32Printing.VERTRES);
            var header = new Win32Printing.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32Printing.BITMAPINFOHEADER>(),
                biWidth = _page.Width,
                biHeight = -_page.Height, // top-down
                biPlanes = 1,
                biBitCount = 32,
            };
            Win32Printing.StretchDIBits(_hdc, 0, 0, width, height, 0, 0, _page.Width, _page.Height, _page.GetPixels(), ref header, 0, 0x00CC0020);
        }
        _canvas?.Dispose();
        _canvas = null;
        _page?.Dispose();
        _page = null;
        Win32Printing.EndPage(_hdc);
    }

    public override void Finish()
    {
        if (_hdc == IntPtr.Zero) return;
        Win32Printing.EndDoc(_hdc);
        Win32Printing.DeleteDC(_hdc);
        _hdc = IntPtr.Zero;
        _mode?.Dispose();
    }

    public override void Abort()
    {
        if (_hdc == IntPtr.Zero) return;
        Win32Printing.AbortDoc(_hdc);
        Win32Printing.DeleteDC(_hdc);
        _hdc = IntPtr.Zero;
        _mode?.Dispose();
    }

    public override void Dispose()
    {
        _canvas?.Dispose();
        _page?.Dispose();
        if (_hdc != IntPtr.Zero) Abort();
    }
}
