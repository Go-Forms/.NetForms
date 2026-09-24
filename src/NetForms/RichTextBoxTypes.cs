using System;
using System.Drawing;

namespace System.Windows.Forms;

[Flags]
public enum RichTextBoxFinds
{
    None = 0x00000000,
    WholeWord = 0x00000002,
    MatchCase = 0x00000004,
    NoHighlight = 0x00000008,
    Reverse = 0x00000010,
}

public enum RichTextBoxScrollBars
{
    None = 0,
    Horizontal = 1,
    Vertical = 2,
    Both = 3,
    ForcedHorizontal = 0x10 | Horizontal,
    ForcedVertical = 0x10 | Vertical,
    ForcedBoth = 0x10 | Both,
}

public enum RichTextBoxStreamType
{
    RichText = 0,
    PlainText = 1,
    RichNoOleObjs = 2,
    TextTextOleObjs = 3,
    UnicodePlainText = 4,
}

[Flags]
public enum RichTextBoxSelectionTypes
{
    Empty = 0,
    Text = 1,
    Object = 2,
    MultiChar = 4,
    MultiObject = 8,
}

[Flags]
public enum RichTextBoxLanguageOptions
{
    AutoKeyboard = 0x0001,
    AutoFont = 0x0002,
    ImeCancelComplete = 0x0004,
    ImeAlwaysSendNotify = 0x0008,
    AutoFontSizeAdjust = 0x0010,
    UIFonts = 0x0020,
    DualFont = 0x0080,
}

public enum RichTextBoxSelectionAttribute
{
    Mixed = -1,
    None = 0,
    All = 1,
}

public enum RichTextBoxWordPunctuations
{
    Level1 = 0x080,
    Level2 = 0x100,
    Custom = 0x200,
    All = Level1 | Level2 | Custom,
}

public delegate void LinkClickedEventHandler(object? sender, LinkClickedEventArgs e);

public class LinkClickedEventArgs : EventArgs
{
    public LinkClickedEventArgs(string? linkText) => LinkText = linkText;

    public LinkClickedEventArgs(string? linkText, int linkStart, int linkLength)
    {
        LinkText = linkText;
        LinkStart = linkStart;
        LinkLength = linkLength;
    }

    public string? LinkText { get; }

    public int LinkStart { get; }

    public int LinkLength { get; }
}

public delegate void ContentsResizedEventHandler(object? sender, ContentsResizedEventArgs e);

public class ContentsResizedEventArgs : EventArgs
{
    public ContentsResizedEventArgs(Rectangle newRectangle) => NewRectangle = newRectangle;

    public Rectangle NewRectangle { get; }
}
