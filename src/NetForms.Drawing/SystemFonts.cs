namespace System.Drawing;

/// <summary>
/// The fonts WinForms takes from the OS. On .NET Core WinForms these all resolve to
/// "Segoe UI, 9pt" on a stock Windows 10/11; we report the same so layouts match, and
/// <see cref="FontResolver"/> substitutes a real face where Segoe UI is not installed.
/// </summary>
public static class SystemFonts
{
    public static Font DefaultFont => new Font("Segoe UI", 9f);
    public static Font MessageBoxFont => new Font("Segoe UI", 9f);
    public static Font CaptionFont => new Font("Segoe UI", 9f);
    public static Font DialogFont => new Font("Segoe UI", 9f);
    public static Font IconTitleFont => new Font("Segoe UI", 9f);
    public static Font MenuFont => new Font("Segoe UI", 9f);
    public static Font SmallCaptionFont => new Font("Segoe UI", 9f);
    public static Font StatusFont => new Font("Segoe UI", 9f);

    public static Font? GetFontByName(string systemFontName) => systemFontName switch
    {
        nameof(DefaultFont) => DefaultFont,
        nameof(MessageBoxFont) => MessageBoxFont,
        nameof(CaptionFont) => CaptionFont,
        nameof(DialogFont) => DialogFont,
        nameof(IconTitleFont) => IconTitleFont,
        nameof(MenuFont) => MenuFont,
        nameof(SmallCaptionFont) => SmallCaptionFont,
        nameof(StatusFont) => StatusFont,
        _ => null,
    };
}
