using System.Drawing;

namespace System.Windows.Forms;

public enum ScreenOrientation
{
    Angle0 = 0,
    Angle90 = 1,
    Angle180 = 2,
    Angle270 = 3,
}

[Flags]
public enum ArrangeDirection
{
    Down = 0x0004,
    Left = 0x0000,
    Right = 0x0000,
    Up = 0x0004,
}

[Flags]
public enum ArrangeStartingPosition
{
    BottomLeft = 0x0000,
    BottomRight = 0x0001,
    Hide = 0x0008,
    TopLeft = 0x0002,
    TopRight = 0x0003,
}

public enum BootMode
{
    Normal = 0,
    FailSafe = 1,
    FailSafeWithNetwork = 2,
}

/// <summary>
/// The system metrics (Ф6.К, decision 118): the sizes NetForms itself draws with - its scroll bars, borders,
/// check boxes - and the Windows 10/11 defaults for what the platform layer does not report (caption,
/// menu, double-click, keyboard); the screens come from the platform.
/// </summary>
public static class SystemInformation
{
    public static int VerticalScrollBarWidth => ScrollBarCore.Thickness;
    public static int HorizontalScrollBarHeight => ScrollBarCore.Thickness;
    public static int VerticalScrollBarArrowHeight => ScrollBarCore.Thickness;
    public static int HorizontalScrollBarArrowWidth => ScrollBarCore.Thickness;
    public static int VerticalScrollBarThumbHeight => ScrollBarCore.Thickness;
    public static int HorizontalScrollBarThumbWidth => ScrollBarCore.Thickness;

    public static int GetVerticalScrollBarWidthForDpi(int dpi) => ScrollBarCore.Thickness * dpi / 96;
    public static int GetHorizontalScrollBarHeightForDpi(int dpi) => ScrollBarCore.Thickness * dpi / 96;
    public static int GetHorizontalScrollBarArrowWidthForDpi(int dpi) => ScrollBarCore.Thickness * dpi / 96;
    public static int VerticalScrollBarArrowHeightForDpi(int dpi) => ScrollBarCore.Thickness * dpi / 96;
    public static Size GetBorderSizeForDpi(int dpi) => new(Math.Max(1, dpi / 96), Math.Max(1, dpi / 96));
    public static Font GetMenuFontForDpi(int dpi) => MenuFont;

    public static Size Border3DSize => new(2, 2);
    public static Size BorderSize => new(1, 1);
    public static int BorderMultiplierFactor => 1;
    public static Size FixedFrameBorderSize => new(3, 3);
    public static Size FrameBorderSize => new(8, 8);
    public static int SizingBorderWidth => 1;
    public static int HorizontalResizeBorderThickness => 8;
    public static int VerticalResizeBorderThickness => 8;
    public static int HorizontalFocusThickness => 1;
    public static int VerticalFocusThickness => 1;

    public static int CaptionHeight => 23;
    public static Size CaptionButtonSize => new(36, 22);
    public static Size SmallCaptionButtonSize => new(22, 22);
    public static int ToolWindowCaptionHeight => 23;
    public static Size ToolWindowCaptionButtonSize => new(22, 22);

    public static int MenuHeight => 20;
    public static Font MenuFont => Control.DefaultFont;
    public static Size MenuButtonSize => new(19, 19);
    public static Size MenuBarButtonSize => new(19, 19);
    public static Size MenuCheckSize => new(15, 15);
    public static int MenuShowDelay => 400;
    public static bool MenuAccessKeysUnderlined => false;
    public static bool RightAlignedMenus => false;
    public static LeftRightAlignment PopupMenuAlignment => LeftRightAlignment.Left;
    public static bool IsFlatMenuEnabled => true;
    public static bool IsMenuAnimationEnabled => false;
    public static bool IsMenuFadeEnabled => false;

    public static Size IconSize => new(32, 32);
    public static Size SmallIconSize => new(16, 16);
    public static Size IconSpacingSize => new(75, 75);
    public static int IconHorizontalSpacing => 75;
    public static int IconVerticalSpacing => 75;
    public static bool IsIconTitleWrappingEnabled => true;
    public static Size CursorSize => new(32, 32);

    public static int DoubleClickTime => 500;
    public static Size DoubleClickSize => new(4, 4);
    public static Size DragSize => new(4, 4);
    public static bool DragFullWindows => true;
    public static int MouseButtons => 3;
    public static bool MouseButtonsSwapped => false;
    public static bool MousePresent => true;
    public static bool MouseWheelPresent => true;
    public static bool NativeMouseWheelSupport => true;
    public static int MouseWheelScrollDelta => 120;
    public static int MouseWheelScrollLines => 3;
    public static int MouseSpeed => 10;
    public static int MouseHoverTime => 400;
    public static Size MouseHoverSize => new(4, 4);
    public static int CaretBlinkTime => 530;
    public static int CaretWidth => 1;
    public static int KeyboardDelay => 1;
    public static int KeyboardSpeed => 31;
    public static bool IsKeyboardPreferred => false;

    public static Size MinimumWindowSize => new(136, 39);
    public static Size MinWindowTrackSize => new(136, 39);
    public static Size MaxWindowTrackSize => new(VirtualScreen.Width + 12, VirtualScreen.Height + 12);
    public static Size MinimizedWindowSize => new(160, 28);
    public static Size MinimizedWindowSpacingSize => new(160, 28);
    public static Size PrimaryMonitorSize => Screen.PrimaryScreen?.Bounds.Size ?? new Size(1920, 1080);
    public static Size PrimaryMonitorMaximizedWindowSize => WorkingArea.Size;
    public static Rectangle WorkingArea => Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1040);
    public static Rectangle VirtualScreen
    {
        get
        {
            var r = Rectangle.Empty;
            foreach (var s in Screen.AllScreens) r = r.IsEmpty ? s.Bounds : Rectangle.Union(r, s.Bounds);
            return r.IsEmpty ? new Rectangle(0, 0, 1920, 1080) : r;
        }
    }

    public static int MonitorCount => Math.Max(1, Screen.AllScreens.Length);
    public static bool MonitorsSameDisplayFormat => true;
    public static ScreenOrientation ScreenOrientation => ScreenOrientation.Angle0;
    public static ArrangeDirection ArrangeDirection => ArrangeDirection.Left;
    public static ArrangeStartingPosition ArrangeStartingPosition => ArrangeStartingPosition.BottomLeft;

    public static string ComputerName => Environment.MachineName;
    public static string UserName => Environment.UserName;
    public static string UserDomainName => Environment.UserDomainName;
    public static bool UserInteractive => Environment.UserInteractive;
    public static bool Network => true;
    public static bool Secure => false;
    public static bool DebugOS => false;
    public static bool PenWindows => false;
    public static bool DbcsEnabled => false;
    public static bool MidEastEnabled => false;
    public static bool ShowSounds => false;
    public static bool TerminalServerSession => false;
    public static BootMode BootMode => BootMode.Normal;
    public static int KanjiWindowHeight => 0;

    public static bool HighContrast => false;
    public static bool UIEffectsEnabled => true;
    public static bool IsDropShadowEnabled => true;
    public static bool IsFontSmoothingEnabled => true;
    public static int FontSmoothingContrast => 1200;
    public static int FontSmoothingType => 2;
    public static bool IsHotTrackingEnabled => true;
    public static bool IsComboBoxAnimationEnabled => false;
    public static bool IsListBoxSmoothScrollingEnabled => true;
    public static bool IsMinimizeRestoreAnimationEnabled => true;
    public static bool IsSelectionFadeEnabled => false;
    public static bool IsSnapToDefaultEnabled => false;
    public static bool IsTitleBarGradientEnabled => false;
    public static bool IsToolTipAnimationEnabled => false;
    public static bool IsActiveWindowTrackingEnabled => false;
    public static int ActiveWindowTrackingDelay => 0;
}
