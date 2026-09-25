// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
using System.ComponentModel;

namespace System.Drawing.Printing;

/// <summary>A printer resolution: one of the driver's qualities, or X by Y dots per inch.</summary>
public class PrinterResolution
{
    private PrinterResolutionKind _kind;

    public PrinterResolution()
    {
        _kind = PrinterResolutionKind.Custom;
    }

    internal PrinterResolution(PrinterResolutionKind kind, int x, int y)
    {
        _kind = kind;
        X = x;
        Y = y;
    }

    public PrinterResolutionKind Kind
    {
        get => _kind;
        set
        {
            if (value is < PrinterResolutionKind.High or > PrinterResolutionKind.Custom)
            {
                throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(PrinterResolutionKind));
            }

            _kind = value;
        }
    }

    public int X { get; set; }

    public int Y { get; set; }

    public override string ToString() => _kind != PrinterResolutionKind.Custom
        ? $"[PrinterResolution {Kind}]"
        : FormattableString.Invariant($"[PrinterResolution X={X} Y={Y}]");
}
