// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
namespace System.Drawing.Printing;

/// <summary>Converts between the units printing uses: 1/100 inch (Display), 1/1000 inch, 1/100 mm and 1/10 mm.</summary>
public sealed class PrinterUnitConvert
{
    private PrinterUnitConvert()
    {
    }

    public static double Convert(double value, PrinterUnit fromUnit, PrinterUnit toUnit)
    {
        double fromUnitsPerDisplay = UnitsPerDisplay(fromUnit);
        double toUnitsPerDisplay = UnitsPerDisplay(toUnit);
        return value * toUnitsPerDisplay / fromUnitsPerDisplay;
    }

    public static int Convert(int value, PrinterUnit fromUnit, PrinterUnit toUnit) =>
        (int)Math.Round(Convert((double)value, fromUnit, toUnit));

    public static Point Convert(Point value, PrinterUnit fromUnit, PrinterUnit toUnit) =>
        new(Convert(value.X, fromUnit, toUnit), Convert(value.Y, fromUnit, toUnit));

    public static Size Convert(Size value, PrinterUnit fromUnit, PrinterUnit toUnit) =>
        new(Convert(value.Width, fromUnit, toUnit), Convert(value.Height, fromUnit, toUnit));

    public static Rectangle Convert(Rectangle value, PrinterUnit fromUnit, PrinterUnit toUnit) => new(
        Convert(value.X, fromUnit, toUnit),
        Convert(value.Y, fromUnit, toUnit),
        Convert(value.Width, fromUnit, toUnit),
        Convert(value.Height, fromUnit, toUnit));

    public static Margins Convert(Margins value, PrinterUnit fromUnit, PrinterUnit toUnit) => new()
    {
        DoubleLeft = Convert(value.DoubleLeft, fromUnit, toUnit),
        DoubleRight = Convert(value.DoubleRight, fromUnit, toUnit),
        DoubleTop = Convert(value.DoubleTop, fromUnit, toUnit),
        DoubleBottom = Convert(value.DoubleBottom, fromUnit, toUnit),
    };

    private static double UnitsPerDisplay(PrinterUnit unit) => unit switch
    {
        PrinterUnit.ThousandthsOfAnInch => 10.0,
        PrinterUnit.HundredthsOfAMillimeter => 25.4,
        PrinterUnit.TenthsOfAMillimeter => 2.54,
        _ => 1.0,
    };
}
