using System;

namespace System.Windows.Forms;

[Flags]
public enum AnchorStyles
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
}

public enum DockStyle
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 3,
    Right = 4,
    Fill = 5,
}

[Flags]
public enum MouseButtons
{
    None = 0,
    Left = 0x00100000,
    Right = 0x00200000,
    Middle = 0x00400000,
    XButton1 = 0x00800000,
    XButton2 = 0x01000000,
}

[Flags]
public enum BoundsSpecified
{
    None = 0,
    X = 1,
    Y = 2,
    Width = 4,
    Height = 8,
    Location = X | Y,
    Size = Width | Height,
    All = Location | Size,
}

[Flags]
public enum ControlStyles
{
    ContainerControl = 0x00000001,
    UserPaint = 0x00000002,
    Opaque = 0x00000004,
    ResizeRedraw = 0x00000010,
    FixedWidth = 0x00000020,
    FixedHeight = 0x00000040,
    StandardClick = 0x00000100,
    Selectable = 0x00000200,
    UserMouse = 0x00000400,
    SupportsTransparentBackColor = 0x00000800,
    StandardDoubleClick = 0x00001000,
    AllPaintingInWmPaint = 0x00002000,
    CacheText = 0x00004000,
    EnableNotifyMessage = 0x00008000,
    DoubleBuffer = 0x00010000,
    OptimizedDoubleBuffer = 0x00020000,
    UseTextForAccessibility = 0x00040000,
    ApplyThemingImplicitly = 0x00080000,
}

public enum FlatStyle
{
    Flat = 0,
    Popup = 1,
    Standard = 2,
    System = 3,
}

public enum BorderStyle
{
    None = 0,
    FixedSingle = 1,
    Fixed3D = 2,
}

public enum AutoScaleMode
{
    None = 0,
    Font = 1,
    Dpi = 2,
    Inherit = 3,
}

public enum AutoSizeMode
{
    GrowAndShrink = 0,
    GrowOnly = 1,
}

/// <summary>How a container validates the control that is losing the focus.</summary>
public enum AutoValidate
{
    Inherit = -1,
    Disable = 0,
    EnablePreventFocusChange = 1,
    EnableAllowFocusChange = 2,
}

public enum FormStartPosition
{
    Manual = 0,
    CenterScreen = 1,
    WindowsDefaultLocation = 2,
    WindowsDefaultBounds = 3,
    CenterParent = 4,
}

public enum FormBorderStyle
{
    None = 0,
    FixedSingle = 1,
    Fixed3D = 2,
    FixedDialog = 3,
    Sizable = 4,
    FixedToolWindow = 5,
    SizableToolWindow = 6,
}

public enum FormWindowState
{
    Normal = 0,
    Minimized = 1,
    Maximized = 2,
}

public enum CloseReason
{
    None = 0,
    WindowsShutDown = 1,
    MdiFormClosing = 2,
    UserClosing = 3,
    TaskManagerClosing = 4,
    FormOwnerClosing = 5,
    ApplicationExitCall = 6,
}

public enum DialogResult
{
    None = 0,
    OK = 1,
    Cancel = 2,
    Abort = 3,
    Retry = 4,
    Ignore = 5,
    Yes = 6,
    No = 7,
    TryAgain = 10,
    Continue = 11,
}

public enum HighDpiMode
{
    DpiUnaware = 0,
    SystemAware = 1,
    PerMonitor = 2,
    PerMonitorV2 = 3,
    DpiUnawareGdiScaled = 4,
}

public enum RightToLeft
{
    No = 0,
    Yes = 1,
    Inherit = 2,
}

public enum ImeMode
{
    Inherit = -1,
    NoControl = 0,
    On = 1,
    Off = 2,
    Disable = 3,
}

[Flags]
public enum TextFormatFlags
{
    Default = 0x00000000,
    GlyphOverhangPadding = 0x00000000,
    Left = 0x00000000,
    Top = 0x00000000,
    HorizontalCenter = 0x00000001,
    Right = 0x00000002,
    VerticalCenter = 0x00000004,
    Bottom = 0x00000008,
    WordBreak = 0x00000010,
    SingleLine = 0x00000020,
    ExpandTabs = 0x00000040,
    NoClipping = 0x00000100,
    ExternalLeading = 0x00000200,
    NoPrefix = 0x00000800,
    Internal = 0x00001000,
    TextBoxControl = 0x00002000,
    PathEllipsis = 0x00004000,
    EndEllipsis = 0x00008000,
    ModifyString = 0x00010000,
    RightToLeft = 0x00020000,
    WordEllipsis = 0x00040000,
    NoFullWidthCharacterBreak = 0x00080000,
    HidePrefix = 0x00100000,
    PrefixOnly = 0x00200000,
    PreserveGraphicsClipping = 0x01000000,
    PreserveGraphicsTranslateTransform = 0x02000000,
    NoPadding = 0x10000000,
    LeftAndRightPadding = 0x20000000,
}
