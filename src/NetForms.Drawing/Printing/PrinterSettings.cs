// The public surface and the defaults follow dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing/PrinterSettings.cs,
// MIT - THIRD-PARTY-NOTICES.md); what WinForms reads from the driver's DEVMODE comes from PrintBackend here.
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;

namespace System.Drawing.Printing;

/// <summary>Which printer a document goes to, and how: copies, pages, duplex, collation, print to file.</summary>
public class PrinterSettings : ICloneable
{
    private string? _printerName; // null: the default printer
    private short _copies = -1;
    private Duplex _duplex = Duplex.Default;
    private bool? _collate;
    private PageSettings _defaultPageSettings;
    private int _fromPage;
    private int _toPage;
    private int _maxPage = 9999;
    private int _minPage;
    private PrintRange _printRange;

    public PrinterSettings()
    {
        _defaultPageSettings = new PageSettings(this);
    }

    /// <summary>What the backend knows about the printer; null when it does not exist.</summary>
    internal PrinterInfo? Info => PrintBackend.Current.GetPrinter(PrinterName);

    public bool CanDuplex => Info?.CanDuplex ?? false;

    public short Copies
    {
        get => _copies != -1 ? _copies : (short)1;
        set
        {
            if (value < 0) throw new ArgumentException($"Value of '{value}' is not valid for 'value'. 'value' must be greater than or equal to 0.");
            _copies = value;
        }
    }

    public bool Collate
    {
        get => _collate ?? Info?.DefaultCollate ?? false;
        set => _collate = value;
    }

    public PageSettings DefaultPageSettings => _defaultPageSettings;

    public Duplex Duplex
    {
        get => _duplex != Duplex.Default ? _duplex : Info?.DefaultDuplex ?? Duplex.Simplex;
        set
        {
            if (value is < Duplex.Default or > Duplex.Horizontal) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(Duplex));
            _duplex = value;
        }
    }

    public int FromPage
    {
        get => _fromPage;
        set
        {
            if (value < 0) throw new ArgumentException($"Value of '{value}' is not valid for 'value'. 'value' must be greater than or equal to 0.");
            _fromPage = value;
        }
    }

    public static StringCollection InstalledPrinters => new(PrintBackend.Current.InstalledPrinters);

    public bool IsDefaultPrinter => _printerName is null || _printerName == DefaultPrinterName;

    public bool IsPlotter => false;

    public bool IsValid => Info != null;

    public int LandscapeAngle => Info?.LandscapeAngle ?? 0;

    public int MaximumCopies => Info?.MaximumCopies ?? 1;

    public int MaximumPage
    {
        get => _maxPage;
        set
        {
            if (value < 0) throw new ArgumentException($"Value of '{value}' is not valid for 'value'. 'value' must be greater than or equal to 0.");
            _maxPage = value;
        }
    }

    public int MinimumPage
    {
        get => _minPage;
        set
        {
            if (value < 0) throw new ArgumentException($"Value of '{value}' is not valid for 'value'. 'value' must be greater than or equal to 0.");
            _minPage = value;
        }
    }

    internal string OutputPort { get; set; } = "";

    public string PrintFileName
    {
        get => OutputPort;
        set
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentNullException(value);
            OutputPort = value;
        }
    }

    public PaperSizeCollection PaperSizes => new(Get_PaperSizes());

    public PaperSourceCollection PaperSources => new(Get_PaperSources());

    internal bool PrintDialogDisplayed { get; set; }

    public PrintRange PrintRange
    {
        get => _printRange;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(PrintRange));
            _printRange = value;
        }
    }

    public bool PrintToFile { get; set; }

    public string PrinterName
    {
        get => _printerName ?? DefaultPrinterName;
        set => _printerName = value;
    }

    private static string DefaultPrinterName => PrintBackend.Current.DefaultPrinter ?? "Default printer is not set.";

    public PrinterResolutionCollection PrinterResolutions => new(Get_PrinterResolutions());

    public bool SupportsColor => Info?.SupportsColor ?? false;

    public int ToPage
    {
        get => _toPage;
        set
        {
            if (value < 0) throw new ArgumentException($"Value of '{value}' is not valid for 'value'. 'value' must be greater than or equal to 0.");
            _toPage = value;
        }
    }

    internal PaperSize[] Get_PaperSizes() => Info?.PaperSizes ?? PrintBackend.StandardPaperSizes();

    internal PaperSource[] Get_PaperSources() => Info?.PaperSources ?? [new PaperSource(PaperSourceKind.AutomaticFeed, "Automatically Select")];

    internal PrinterResolution[] Get_PrinterResolutions() => Info?.Resolutions ?? [new PrinterResolution(PrinterResolutionKind.Custom, 300, 300)];

    /// <summary>JPEG and PNG passthrough to the printer is a driver feature (GDI escapes); NetForms always draws.</summary>
    public bool IsDirectPrintingSupported(ImageFormat imageFormat) => false;

    public bool IsDirectPrintingSupported(Image image) => false;

    public object Clone()
    {
        var clone = (PrinterSettings)MemberwiseClone();
        clone.PrintDialogDisplayed = false;
        return clone;
    }

    // --- measurement graphics ----------------------------------------------------------------------------

    public Graphics CreateMeasurementGraphics() => CreateMeasurementGraphics(DefaultPageSettings, false);

    public Graphics CreateMeasurementGraphics(bool honorOriginAtMargins) => CreateMeasurementGraphics(DefaultPageSettings, honorOriginAtMargins);

    public Graphics CreateMeasurementGraphics(PageSettings pageSettings) => CreateMeasurementGraphics(pageSettings, false);

    /// <summary>A Graphics that measures as a page of this printer does (unit 1/100 inch, the printer's resolution).</summary>
    public Graphics CreateMeasurementGraphics(PageSettings pageSettings, bool honorOriginAtMargins)
    {
        ArgumentNullException.ThrowIfNull(pageSettings);
        var bitmap = new Bitmap(1, 1);
        var g = Graphics.FromImage(bitmap);
        var resolution = pageSettings.PrinterResolution;
        float dpi = resolution.Kind == PrinterResolutionKind.Custom && resolution.X > 0 ? resolution.X : 300f;
        g.SetDpi(dpi, resolution.Kind == PrinterResolutionKind.Custom && resolution.Y > 0 ? resolution.Y : dpi);
        g.TextScale = 100f / 96f;
        if (honorOriginAtMargins)
        {
            g.TranslateTransform(pageSettings.Margins.Left - pageSettings.HardMarginX, pageSettings.Margins.Top - pageSettings.HardMarginY);
        }
        return g;
    }

    // --- DEVMODE and DEVNAMES ----------------------------------------------------------------------------
    //
    // Blocks in the Win32 layout, allocated with Marshal.AllocHGlobal and freed by the caller (GlobalFree on
    // Windows, Marshal.FreeHGlobal anywhere): what code passes between these calls and native print dialogs.

    public IntPtr GetHdevmode() => GetHdevmode(DefaultPageSettings);

    public IntPtr GetHdevmode(PageSettings pageSettings)
    {
        ArgumentNullException.ThrowIfNull(pageSettings);
        var mode = DevModeLayout.Allocate(PrinterName);
        DevModeLayout.Write(mode, DevModeLayout.Copies, Copies, DevModeLayout.DM_COPIES);
        DevModeLayout.Write(mode, DevModeLayout.Collate, (short)(Collate ? 1 : 0), DevModeLayout.DM_COLLATE);
        DevModeLayout.Write(mode, DevModeLayout.Duplex, (short)Duplex, DevModeLayout.DM_DUPLEX);
        pageSettings.CopyToHdevmode(mode);
        return mode;
    }

    public void SetHdevmode(IntPtr hdevmode)
    {
        if (hdevmode == IntPtr.Zero) throw new ArgumentException($"Handle {hdevmode} is not valid.");
        var name = DevModeLayout.ReadDeviceName(hdevmode);
        if (!string.IsNullOrEmpty(name) && name != PrinterName && name.Length < 31) _printerName = name;
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_COPIES)) _copies = DevModeLayout.Read(hdevmode, DevModeLayout.Copies);
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_COLLATE)) _collate = DevModeLayout.Read(hdevmode, DevModeLayout.Collate) == 1;
        if (DevModeLayout.Has(hdevmode, DevModeLayout.DM_DUPLEX))
        {
            short duplex = DevModeLayout.Read(hdevmode, DevModeLayout.Duplex);
            if (duplex is >= 1 and <= 3) _duplex = (Duplex)duplex;
        }
    }

    /// <summary>A DEVNAMES block: offsets (in characters) to the driver, device and port names.</summary>
    public IntPtr GetHdevnames()
    {
        string driver = "winspool", device = PrinterName, port = OutputPort;
        const int header = 8; // four WORDs
        int chars = driver.Length + 1 + device.Length + 1 + port.Length + 1;
        var names = Marshal.AllocHGlobal(header + chars * 2);
        int offset = header / 2;
        Marshal.WriteInt16(names, 0, (short)offset);
        offset = WriteString(names, offset, driver);
        Marshal.WriteInt16(names, 2, (short)offset);
        offset = WriteString(names, offset, device);
        Marshal.WriteInt16(names, 4, (short)offset);
        WriteString(names, offset, port);
        Marshal.WriteInt16(names, 6, (short)(IsDefaultPrinter ? 1 : 0));
        return names;

        static int WriteString(IntPtr block, int offsetChars, string s)
        {
            for (int i = 0; i < s.Length; i++) Marshal.WriteInt16(block, (offsetChars + i) * 2, s[i]);
            Marshal.WriteInt16(block, (offsetChars + s.Length) * 2, 0);
            return offsetChars + s.Length + 1;
        }
    }

    public void SetHdevnames(IntPtr hdevnames)
    {
        if (hdevnames == IntPtr.Zero) throw new ArgumentException($"Handle {hdevnames} is not valid.");
        int deviceOffset = Marshal.ReadInt16(hdevnames, 2);
        int portOffset = Marshal.ReadInt16(hdevnames, 4);
        _printerName = Marshal.PtrToStringUni(hdevnames + deviceOffset * 2);
        OutputPort = Marshal.PtrToStringUni(hdevnames + portOffset * 2) ?? "";
    }

    public override string ToString() =>
        $"[PrinterSettings {PrinterName} Copies={Copies} Collate={Collate} Duplex={Duplex} FromPage={FromPage} LandscapeAngle={LandscapeAngle} MaximumCopies={MaximumCopies} OutputPort={OutputPort} ToPage={ToPage}]";

    // --- the collections -------------------------------------------------------------------------------

    public class PaperSizeCollection : ICollection
    {
        private PaperSize[] _array;

        public PaperSizeCollection(PaperSize[] array) => _array = array;

        public int Count => _array.Length;

        public virtual PaperSize this[int index] => _array[index];

        public IEnumerator GetEnumerator() => _array.GetEnumerator();

        int ICollection.Count => Count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        void ICollection.CopyTo(Array array, int index) => Array.Copy(_array, 0, array, index, _array.Length);

        public void CopyTo(PaperSize[] paperSizes, int index) => Array.Copy(_array, 0, paperSizes, index, _array.Length);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int Add(PaperSize paperSize)
        {
            var newArray = new PaperSize[Count + 1];
            ((ICollection)this).CopyTo(newArray, 0);
            newArray[Count] = paperSize;
            _array = newArray;
            return Count;
        }
    }

    public class PaperSourceCollection : ICollection
    {
        private PaperSource[] _array;

        public PaperSourceCollection(PaperSource[] array) => _array = array;

        public int Count => _array.Length;

        public virtual PaperSource this[int index] => _array[index];

        public IEnumerator GetEnumerator() => _array.GetEnumerator();

        int ICollection.Count => Count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        void ICollection.CopyTo(Array array, int index) => Array.Copy(_array, 0, array, index, _array.Length);

        public void CopyTo(PaperSource[] paperSources, int index) => Array.Copy(_array, 0, paperSources, index, _array.Length);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int Add(PaperSource paperSource)
        {
            var newArray = new PaperSource[Count + 1];
            ((ICollection)this).CopyTo(newArray, 0);
            newArray[Count] = paperSource;
            _array = newArray;
            return Count;
        }
    }

    public class PrinterResolutionCollection : ICollection
    {
        private PrinterResolution[] _array;

        public PrinterResolutionCollection(PrinterResolution[] array) => _array = array;

        public int Count => _array.Length;

        public virtual PrinterResolution this[int index] => _array[index];

        public IEnumerator GetEnumerator() => _array.GetEnumerator();

        int ICollection.Count => Count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        void ICollection.CopyTo(Array array, int index) => Array.Copy(_array, 0, array, index, _array.Length);

        public void CopyTo(PrinterResolution[] printerResolutions, int index) => Array.Copy(_array, 0, printerResolutions, index, _array.Length);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int Add(PrinterResolution printerResolution)
        {
            var newArray = new PrinterResolution[Count + 1];
            ((ICollection)this).CopyTo(newArray, 0);
            newArray[Count] = printerResolution;
            _array = newArray;
            return Count;
        }
    }

    public class StringCollection : ICollection, IEnumerable<string>
    {
        private string[] _array;

        public StringCollection(string[] array) => _array = array;

        public int Count => _array.Length;

        public virtual string this[int index] => _array[index];

        public IEnumerator GetEnumerator() => _array.GetEnumerator();

        IEnumerator<string> IEnumerable<string>.GetEnumerator() => ((IEnumerable<string>)_array).GetEnumerator();

        int ICollection.Count => Count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => this;

        void ICollection.CopyTo(Array array, int index) => Array.Copy(_array, 0, array, index, _array.Length);

        public void CopyTo(string[] strings, int index) => Array.Copy(_array, 0, strings, index, _array.Length);

        [EditorBrowsable(EditorBrowsableState.Never)]
        public int Add(string value)
        {
            var newArray = new string[Count + 1];
            ((ICollection)this).CopyTo(newArray, 0);
            newArray[Count] = value;
            _array = newArray;
            return Count;
        }
    }
}
