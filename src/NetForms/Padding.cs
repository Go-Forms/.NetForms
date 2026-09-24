using System;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

[TypeConverter(typeof(PaddingConverter))]
public struct Padding : IEquatable<Padding>
{
    public static readonly Padding Empty = new Padding(0);

    private bool _all;
    private int _top, _left, _right, _bottom;

    public Padding(int all)
    {
        _all = true;
        _top = _left = _right = _bottom = all;
    }

    public Padding(int left, int top, int right, int bottom)
    {
        _top = top;
        _left = left;
        _right = right;
        _bottom = bottom;
        _all = _top == _left && _top == _right && _top == _bottom;
    }

    public int All
    {
        readonly get => _all ? _top : -1;
        set
        {
            if (!_all || _top != value)
            {
                _all = true;
                _top = _left = _right = _bottom = value;
            }
        }
    }

    public int Bottom
    {
        readonly get => _all ? _top : _bottom;
        set { if (_all || _bottom != value) { _all = false; _bottom = value; } }
    }

    public int Left
    {
        readonly get => _all ? _top : _left;
        set { if (_all || _left != value) { _all = false; _left = value; } }
    }

    public int Right
    {
        readonly get => _all ? _top : _right;
        set { if (_all || _right != value) { _all = false; _right = value; } }
    }

    public int Top
    {
        readonly get => _top;
        set { if (_all || _top != value) { _all = false; _top = value; } }
    }

    public readonly int Horizontal => Left + Right;
    public readonly int Vertical => Top + Bottom;
    public readonly Size Size => new Size(Horizontal, Vertical);

    /// <summary>True when all four edges are equal, so the value can be written as <c>new Padding(n)</c>.</summary>
    internal readonly bool ShouldSerializeAll() => _all;

    public static Padding Add(Padding p1, Padding p2) => p1 + p2;
    public static Padding Subtract(Padding p1, Padding p2) => p1 - p2;

    public readonly bool Equals(Padding other) => Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;

    public override readonly bool Equals(object? obj) => obj is Padding p && Equals(p);

    public override readonly int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);

    public static Padding operator +(Padding p1, Padding p2) => new Padding(p1.Left + p2.Left, p1.Top + p2.Top, p1.Right + p2.Right, p1.Bottom + p2.Bottom);
    public static Padding operator -(Padding p1, Padding p2) => new Padding(p1.Left - p2.Left, p1.Top - p2.Top, p1.Right - p2.Right, p1.Bottom - p2.Bottom);
    public static bool operator ==(Padding p1, Padding p2) => p1.Equals(p2);
    public static bool operator !=(Padding p1, Padding p2) => !p1.Equals(p2);

    public override readonly string ToString() => $"{{Left={Left},Top={Top},Right={Right},Bottom={Bottom}}}";
}
