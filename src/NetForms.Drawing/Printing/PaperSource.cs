// Vendored from dotnet/winforms (src/System.Drawing.Common/src/System/Drawing/Printing), MIT - THIRD-PARTY-NOTICES.md.
namespace System.Drawing.Printing;

/// <summary>The input tray a page is fed from.</summary>
public class PaperSource
{
    // DMBIN_USER: from here on the ids are the driver's own.
    private const int UserBin = 256;

    private string _name;
    private PaperSourceKind _kind;

    public PaperSource()
    {
        _kind = PaperSourceKind.Custom;
        _name = string.Empty;
    }

    internal PaperSource(PaperSourceKind kind, string name)
    {
        _kind = kind;
        _name = name;
    }

    public PaperSourceKind Kind => (int)_kind >= UserBin ? PaperSourceKind.Custom : _kind;

    public int RawKind
    {
        get => (int)_kind;
        set => _kind = (PaperSourceKind)value;
    }

    public string SourceName
    {
        get => _name;
        set => _name = value;
    }

    public override string ToString() => $"[PaperSource {SourceName} Kind={Kind}]";
}
