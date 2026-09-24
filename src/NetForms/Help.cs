using System.Diagnostics;
using System.Drawing;

namespace System.Windows.Forms;

public enum HelpNavigator
{
    Topic = unchecked((int)0x80000001),
    TableOfContents = unchecked((int)0x80000002),
    Index = unchecked((int)0x80000003),
    Find = unchecked((int)0x80000004),
    AssociateIndex = unchecked((int)0x80000005),
    KeywordIndex = unchecked((int)0x80000006),
    TopicId = unchecked((int)0x80000007),
}

/// <summary>
/// Help files and pages. There is no HTML Help (.chm) viewer off Windows (decision 116): the file or URL
/// is opened with the system's handler for it - a browser for a page, whatever reads .chm where one is
/// installed. A popup shows as a tooltip at the point.
/// </summary>
public static class Help
{
    public static void ShowHelp(Control? parent, string? url) => ShowHelp(parent, url, HelpNavigator.TableOfContents, null);

    public static void ShowHelp(Control? parent, string? url, HelpNavigator navigator) => ShowHelp(parent, url, navigator, null);

    public static void ShowHelp(Control? parent, string? url, string? keyword) => ShowHelp(parent, url, HelpNavigator.KeywordIndex, keyword);

    public static void ShowHelp(Control? parent, string? url, HelpNavigator command, object? parameter)
    {
        if (string.IsNullOrEmpty(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // No handler for this kind of file on this system.
        }
    }

    public static void ShowHelpIndex(Control? parent, string? url) => ShowHelp(parent, url, HelpNavigator.Index, null);

    public static void ShowPopup(Control? parent, string caption, Point location)
    {
        if (parent == null) return;
        var tip = new ToolTip();
        tip.Show(caption, parent, parent.PointToClient(location), 5000);
    }
}
