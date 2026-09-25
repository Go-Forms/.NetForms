// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
using System.Globalization;

namespace System.Drawing.Printing;

/// <summary>A paper size, in hundredths of an inch.</summary>
public class PaperSize
{
    // DMPAPER_LAST, and the two ids Win32 reserves inside the range.
    private const int LastKind = 118;
    private const int Reserved48 = 48;
    private const int Reserved49 = 49;

    private PaperKind _kind;
    private string _name;
    private int _width;
    private int _height;
    private readonly bool _createdByDefaultConstructor;

    public PaperSize()
    {
        _kind = PaperKind.Custom;
        _name = string.Empty;
        _createdByDefaultConstructor = true;
    }

    internal PaperSize(PaperKind kind, string name, int width, int height)
    {
        _kind = kind;
        _name = name;
        _width = width;
        _height = height;
    }

    public PaperSize(string name, int width, int height)
    {
        _kind = PaperKind.Custom;
        _name = name;
        _width = width;
        _height = height;
    }

    public int Height
    {
        get => _height;
        set
        {
            if (_kind != PaperKind.Custom && !_createdByDefaultConstructor)
            {
                throw new ArgumentException("PaperSize cannot be changed unless the Kind property is set to Custom.", nameof(value));
            }

            _height = value;
        }
    }

    public PaperKind Kind
        => (int)_kind <= LastKind && (int)_kind is not (Reserved48 or Reserved49) ? _kind : PaperKind.Custom;

    public string PaperName
    {
        get => _name;
        set
        {
            if (_kind != PaperKind.Custom && !_createdByDefaultConstructor)
            {
                throw new ArgumentException("PaperSize cannot be changed unless the Kind property is set to Custom.", nameof(value));
            }

            _name = value;
        }
    }

    public int RawKind
    {
        get => (int)_kind;
        set => _kind = (PaperKind)value;
    }

    public int Width
    {
        get => _width;
        set
        {
            if (_kind != PaperKind.Custom && !_createdByDefaultConstructor)
            {
                throw new ArgumentException("PaperSize cannot be changed unless the Kind property is set to Custom.", nameof(value));
            }

            _width = value;
        }
    }

    public override string ToString() =>
        $"[PaperSize {PaperName} Kind={Kind} Height={Height.ToString(CultureInfo.InvariantCulture)} Width={Width.ToString(CultureInfo.InvariantCulture)}]";
}
