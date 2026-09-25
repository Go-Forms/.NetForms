// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
using System.ComponentModel;

namespace System.Drawing.Printing;

/// <summary>BeginPrint and EndPrint: which kind of print is running, and a way to cancel it.</summary>
public class PrintEventArgs : CancelEventArgs
{
    public PrintEventArgs()
    {
    }

    internal PrintEventArgs(PrintAction action) => PrintAction = action;

    public PrintAction PrintAction { get; }
}

public delegate void PrintEventHandler(object sender, PrintEventArgs e);

/// <summary>One page to draw: its Graphics (unit 1/100 inch), the page and the margin rectangles.</summary>
public class PrintPageEventArgs : EventArgs
{
    public PrintPageEventArgs(Graphics? graphics, Rectangle marginBounds, Rectangle pageBounds, PageSettings pageSettings)
    {
        Graphics = graphics;
        MarginBounds = marginBounds;
        PageBounds = pageBounds;
        PageSettings = pageSettings;
    }

    public bool Cancel { get; set; }

    public Graphics? Graphics { get; private set; }

    public bool HasMorePages { get; set; }

    public Rectangle MarginBounds { get; }

    public Rectangle PageBounds { get; }

    public PageSettings PageSettings { get; }

    internal void Dispose() => Graphics?.Dispose();

    internal void SetGraphics(Graphics? value) => Graphics = value;
}

public delegate void PrintPageEventHandler(object sender, PrintPageEventArgs e);

/// <summary>Before each page: the settings it prints with, which the handler may change.</summary>
public class QueryPageSettingsEventArgs : PrintEventArgs
{
    private PageSettings _pageSettings;

    public QueryPageSettingsEventArgs(PageSettings pageSettings) : base() => _pageSettings = pageSettings;

    public PageSettings PageSettings
    {
        get
        {
            PageSettingsChanged = true;
            return _pageSettings;
        }
        set
        {
            value ??= new PageSettings();
            _pageSettings = value;
            PageSettingsChanged = true;
        }
    }

    internal bool PageSettingsChanged { get; set; }
}

public delegate void QueryPageSettingsEventHandler(object sender, QueryPageSettingsEventArgs e);

/// <summary>One page of a print preview: its image and its size in hundredths of an inch.</summary>
public sealed class PreviewPageInfo
{
    public PreviewPageInfo(Image image, Size physicalSize)
    {
        Image = image;
        PhysicalSize = physicalSize;
    }

    public Image Image { get; }

    public Size PhysicalSize { get; }
}

/// <summary>Thrown when printing with a printer that does not exist (or with no printer installed at all).</summary>
[Serializable]
public class InvalidPrinterException : SystemException
{
    public InvalidPrinterException(PrinterSettings settings) : base(GenerateMessage(settings))
    {
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    protected InvalidPrinterException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }

    [Obsolete("This API supports obsolete formatter-based serialization. It should not be called or extended by application code.", DiagnosticId = "SYSLIB0051", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        => base.GetObjectData(info, context);

    private static string GenerateMessage(PrinterSettings settings) => settings.IsDefaultPrinter
        ? "No printers are installed."
        : $"Settings to access printer '{settings.PrinterName}' are not valid.";
}
