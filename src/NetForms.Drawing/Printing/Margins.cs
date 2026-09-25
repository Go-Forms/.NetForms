// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace System.Drawing.Printing;

/// <summary>The margins of a printed page, in hundredths of an inch.</summary>
[TypeConverter(typeof(MarginsConverter))]
public class Margins : ICloneable
{
    private int _left;
    private int _right;
    private int _bottom;
    private int _top;

    [OptionalField]
    private double _doubleLeft;

    [OptionalField]
    private double _doubleRight;

    [OptionalField]
    private double _doubleTop;

    [OptionalField]
    private double _doubleBottom;

    /// <summary>One-inch margins.</summary>
    public Margins() : this(100, 100, 100, 100)
    {
    }

    public Margins(int left, int right, int top, int bottom)
    {
        CheckMargin(left, nameof(left));
        CheckMargin(right, nameof(right));
        CheckMargin(top, nameof(top));
        CheckMargin(bottom, nameof(bottom));

        _left = left;
        _right = right;
        _top = top;
        _bottom = bottom;

        _doubleLeft = left;
        _doubleRight = right;
        _doubleTop = top;
        _doubleBottom = bottom;
    }

    public int Left
    {
        get => _left;
        set
        {
            CheckMargin(value, nameof(value));
            _left = value;
            _doubleLeft = value;
        }
    }

    public int Right
    {
        get => _right;
        set
        {
            CheckMargin(value, nameof(value));
            _right = value;
            _doubleRight = value;
        }
    }

    public int Top
    {
        get => _top;
        set
        {
            CheckMargin(value, nameof(value));
            _top = value;
            _doubleTop = value;
        }
    }

    public int Bottom
    {
        get => _bottom;
        set
        {
            CheckMargin(value, nameof(value));
            _bottom = value;
            _doubleBottom = value;
        }
    }

    // The exact values behind the rounded ones, so converting units back and forth does not drift.

    internal double DoubleLeft
    {
        get => _doubleLeft;
        set
        {
            Left = (int)Math.Round(value);
            _doubleLeft = value;
        }
    }

    internal double DoubleRight
    {
        get => _doubleRight;
        set
        {
            Right = (int)Math.Round(value);
            _doubleRight = value;
        }
    }

    internal double DoubleTop
    {
        get => _doubleTop;
        set
        {
            Top = (int)Math.Round(value);
            _doubleTop = value;
        }
    }

    internal double DoubleBottom
    {
        get => _doubleBottom;
        set
        {
            Bottom = (int)Math.Round(value);
            _doubleBottom = value;
        }
    }

    private static void CheckMargin(int margin, string name)
    {
        if (margin < 0)
        {
            throw new ArgumentOutOfRangeException(name, margin, $"Value of '{margin}' is not valid for '{name}'. '{name}' must be greater than or equal to 0.");
        }
    }

    public object Clone() => MemberwiseClone();

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is Margins margins
            && margins.Left == Left
            && margins.Right == Right
            && margins.Top == Top
            && margins.Bottom == Bottom;

    public override int GetHashCode() => HashCode.Combine(Left, Right, Top, Bottom);

    public static bool operator ==(Margins? m1, Margins? m2)
    {
        if (m1 is null) return m2 is null;
        if (m2 is null) return false;
        return m1.Equals(m2);
    }

    public static bool operator !=(Margins? m1, Margins? m2) => !(m1 == m2);

    public override string ToString() => $"[Margins Left={Left} Right={Right} Top={Top} Bottom={Bottom}]";
}
