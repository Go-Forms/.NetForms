using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// The colours of the built-in look: WinForms on Windows 10, but a little cleaner. Kept in
/// one place so it can become a pluggable theme later (docs/PLAN.md §2) without touching
/// the controls.
/// </summary>
internal static class Theme
{
    public static Color ButtonFace => Color.FromArgb(0xE1, 0xE1, 0xE1);
    public static Color ButtonFaceHot => Color.FromArgb(0xE5, 0xF1, 0xFB);
    public static Color ButtonFacePressed => Color.FromArgb(0xCC, 0xE4, 0xF7);
    public static Color ButtonFaceDisabled => Color.FromArgb(0xCC, 0xCC, 0xCC);
    public static Color ButtonBorder => Color.FromArgb(0xAD, 0xAD, 0xAD);
    public static Color ButtonBorderHot => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color ButtonBorderPressed => Color.FromArgb(0x00, 0x54, 0x99);
    public static Color ButtonBorderDefault => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color ButtonBorderDisabled => Color.FromArgb(0xBF, 0xBF, 0xBF);
    public static Color DisabledText => Color.FromArgb(0x83, 0x83, 0x83);
    public static Color GroupBoxBorder => Color.FromArgb(0xDC, 0xDC, 0xDC);
    public static Color CheckBorder => Color.FromArgb(0x33, 0x33, 0x33);
    public static Color CheckBorderHot => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color CheckFill => Color.White;
    public static Color CheckFillPressed => Color.FromArgb(0xCC, 0xE4, 0xF7);
    public static Color CheckMark => Color.FromArgb(0x00, 0x00, 0x00);
    public static Color Accent => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color ProgressTrack => Color.FromArgb(0xE6, 0xE6, 0xE6);
    public static Color ProgressTrackBorder => Color.FromArgb(0xBC, 0xBC, 0xBC);
    public static Color ProgressFill => Color.FromArgb(0x06, 0xB0, 0x25);
    public static Color ProgressFillError => Color.FromArgb(0xDA, 0x26, 0x26);
    public static Color ProgressFillPaused => Color.FromArgb(0xDA, 0xCB, 0x26);
    public static Color LinkText => Color.FromArgb(0x00, 0x66, 0xCC);
    public static Color LinkActive => Color.FromArgb(0xCC, 0x00, 0x00);
    public static Color LinkVisited => Color.FromArgb(0x80, 0x00, 0x80);
    public static Color ScrollTrack => Color.FromArgb(0xF0, 0xF0, 0xF0);
    public static Color ScrollThumb => Color.FromArgb(0xCD, 0xCD, 0xCD);
    public static Color ScrollThumbHot => Color.FromArgb(0xA6, 0xA6, 0xA6);
    public static Color ScrollThumbPressed => Color.FromArgb(0x60, 0x60, 0x60);
    public static Color ScrollThumbDisabled => Color.FromArgb(0xE0, 0xE0, 0xE0);
    public static Color ScrollArrow => Color.FromArgb(0x60, 0x60, 0x60);
    public static Color WindowBorder => Color.FromArgb(0x7A, 0x7A, 0x7A);
    public static Color WindowBorderFocused => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color Highlight => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color HighlightText => Color.White;
    /// <summary>Hot-tracked text (TreeView.HotTracking): the system's hot-track blue.</summary>
    public static Color HotTrackText => Color.FromArgb(0x00, 0x66, 0xCC);
    public static Color HighlightInactive => Color.FromArgb(0xCC, 0xCC, 0xCC);
    public static Color Selection => Color.FromArgb(0x00, 0x78, 0xD7);

    // --- strips and menus ---------------------------------------------------------
    // Given explicitly rather than through SystemColors so a ToolStrip looks the same on
    // Linux, where the BCL still reports the XP palette (see docs/PLAN.md, decision 10).

    public static Color StripBackground => Color.FromArgb(0xF0, 0xF0, 0xF0);
    public static Color StripBorder => Color.FromArgb(0xD6, 0xD6, 0xD6);
    public static Color StripGrip => Color.FromArgb(0xB0, 0xB0, 0xB0);
    public static Color StripItemHot => Color.FromArgb(0xE5, 0xF1, 0xFB);
    public static Color StripItemPressed => Color.FromArgb(0xCC, 0xE4, 0xF7);
    public static Color StripItemChecked => Color.FromArgb(0xCC, 0xE4, 0xF7);
    public static Color StripItemBorder => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color StripSeparator => Color.FromArgb(0xD7, 0xD7, 0xD7);
    public static Color MenuBackground => Color.White;
    public static Color MenuBorder => Color.FromArgb(0xB6, 0xB6, 0xB6);
    public static Color MenuImageMargin => Color.FromArgb(0xF6, 0xF6, 0xF6);
    public static Color MenuItemSelected => Color.FromArgb(0x91, 0xC9, 0xF7);
    public static Color MenuItemSelectedBorder => Color.FromArgb(0x91, 0xC9, 0xF7);
    public static Color MenuCheckBackground => Color.FromArgb(0xCC, 0xE4, 0xF7);
    public static Color MenuCheckBorder => Color.FromArgb(0x00, 0x78, 0xD7);
    public static Color StatusStripBackground => Color.FromArgb(0xF0, 0xF0, 0xF0);
    public static Color SizingGrip => Color.FromArgb(0xA0, 0xA0, 0xA0);
}
