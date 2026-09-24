using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms;

public enum MessageBoxButtons
{
    OK = 0,
    OKCancel = 1,
    AbortRetryIgnore = 2,
    YesNoCancel = 3,
    YesNo = 4,
    RetryCancel = 5,
    CancelTryContinue = 6,
}

public enum MessageBoxIcon
{
    None = 0,
    Hand = 0x10,
    Stop = 0x10,
    Error = 0x10,
    Question = 0x20,
    Exclamation = 0x30,
    Warning = 0x30,
    Asterisk = 0x40,
    Information = 0x40,
}

public enum MessageBoxDefaultButton
{
    Button1 = 0,
    Button2 = 0x100,
    Button3 = 0x200,
    Button4 = 0x300,
}

[Flags]
public enum MessageBoxOptions
{
    ServiceNotification = 0x00200000,
    DefaultDesktopOnly = 0x00020000,
    RightAlign = 0x00080000,
    RtlReading = 0x00100000,
}

/// <summary>The modal message dialog, drawn with NetForms' own controls so it looks the same on every OS.</summary>
public static class MessageBox
{
    public static DialogResult Show(string? text) => Show(null, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(string? text, string? caption) => Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons) => Show(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon) => Show(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) => Show(null, text, caption, buttons, icon, defaultButton, 0);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options) => Show(null, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(IWin32Window? owner, string? text) => Show(owner, text, string.Empty, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption) => Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons) => Show(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon) => Show(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1, 0);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton) => Show(owner, text, caption, buttons, icon, defaultButton, 0);

    // With a Help button or a help file (decision 116: the help itself opens through Help.ShowHelp's handler; the box has no Help button yet).
    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, bool displayHelpButton) =>
        Show(null, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath) =>
        Show(null, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, string keyword) =>
        Show(null, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator) =>
        Show(null, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator, object? param) =>
        Show(null, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath) =>
        Show(owner, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, string keyword) =>
        Show(owner, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator) =>
        Show(owner, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options, string helpFilePath, HelpNavigator navigator, object? param) =>
        Show(owner, text, caption, buttons, icon, defaultButton, options);

    public static DialogResult Show(IWin32Window? owner, string? text, string? caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
    {
        using var form = new MessageBoxForm(text ?? string.Empty, caption ?? string.Empty, buttons, icon, defaultButton, options);
        return form.ShowDialog(owner);
    }

    /// <summary>What buttons a MessageBoxButtons value produces, with their results and texts, in WinForms' order.</summary>
    internal static (DialogResult result, string text)[] ButtonsFor(MessageBoxButtons buttons) => buttons switch
    {
        MessageBoxButtons.OKCancel => new[] { (DialogResult.OK, SystemStrings.Get("OK")), (DialogResult.Cancel, SystemStrings.Get("Cancel")) },
        MessageBoxButtons.AbortRetryIgnore => new[] { (DialogResult.Abort, SystemStrings.Get("&Abort")), (DialogResult.Retry, SystemStrings.Get("&Retry")), (DialogResult.Ignore, SystemStrings.Get("&Ignore")) },
        MessageBoxButtons.YesNoCancel => new[] { (DialogResult.Yes, SystemStrings.Get("&Yes")), (DialogResult.No, SystemStrings.Get("&No")), (DialogResult.Cancel, SystemStrings.Get("Cancel")) },
        MessageBoxButtons.YesNo => new[] { (DialogResult.Yes, SystemStrings.Get("&Yes")), (DialogResult.No, SystemStrings.Get("&No")) },
        MessageBoxButtons.RetryCancel => new[] { (DialogResult.Retry, SystemStrings.Get("&Retry")), (DialogResult.Cancel, SystemStrings.Get("Cancel")) },
        MessageBoxButtons.CancelTryContinue => new[] { (DialogResult.Cancel, SystemStrings.Get("Cancel")), (DialogResult.TryAgain, SystemStrings.Get("&Try Again")), (DialogResult.Continue, SystemStrings.Get("&Continue")) },
        _ => new[] { (DialogResult.OK, SystemStrings.Get("OK")) },
    };
}

/// <summary>Layout of the message box: icon, wrapped text, a button strip. Sized to content like the Win32 one.</summary>
internal sealed class MessageBoxForm : Form
{
    private const int Gap = 12;
    private const int IconSize = 32;
    private const int MaxTextWidth = 400;
    private const int ButtonWidth = 88;
    private const int ButtonHeight = 26;
    private const int ButtonGap = 8;
    private const int StripHeight = 42;

    private readonly Label _label;
    private readonly List<Button> _buttons = new();
    private readonly Icon? _icon;
    private readonly DialogResult _escapeResult;

    public MessageBoxForm(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton, MessageBoxOptions options)
    {
        Text = caption;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.White;

        _icon = icon switch
        {
            MessageBoxIcon.Error => SystemIcons.Error,
            MessageBoxIcon.Question => SystemIcons.Question,
            MessageBoxIcon.Warning => SystemIcons.Warning,
            MessageBoxIcon.Information => SystemIcons.Information,
            _ => null,
        };

        var rtl = (options & MessageBoxOptions.RightAlign) != 0;
        int textLeft = Gap + (_icon != null ? IconSize + Gap : 0);

        _label = new Label
        {
            AutoSize = false,
            Text = text,
            TextAlign = rtl ? ContentAlignment.TopRight : ContentAlignment.TopLeft,
            UseMnemonic = false,
            BackColor = Color.White,
        };
        var textSize = TextRenderer.MeasureText(text, Font, new Size(MaxTextWidth, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        if (string.IsNullOrEmpty(text)) textSize = new Size(0, Font.Height);
        _label.Bounds = new Rectangle(textLeft, Gap + (_icon != null && textSize.Height < IconSize ? (IconSize - textSize.Height) / 2 : 0), textSize.Width + 2, textSize.Height);
        Controls.Add(_label);

        var defs = MessageBox.ButtonsFor(buttons);
        int contentHeight = Math.Max(textSize.Height, _icon != null ? IconSize : 0);
        int buttonsWidth = defs.Length * ButtonWidth + (defs.Length - 1) * ButtonGap;
        int width = Math.Max(textLeft + textSize.Width + Gap + 2, buttonsWidth + 2 * Gap);
        width = Math.Max(width, 200);
        int height = Gap + contentHeight + Gap + StripHeight;
        ClientSize = new Size(width, height);

        int x = width - Gap - buttonsWidth;
        int y = height - StripHeight + (StripHeight - ButtonHeight) / 2;
        int defaultIndex = Math.Min(defs.Length - 1, (int)defaultButton / 0x100);
        for (int i = 0; i < defs.Length; i++)
        {
            var (result, label) = defs[i];
            var button = new Button
            {
                Text = label,
                DialogResult = result,
                Bounds = new Rectangle(x, y, ButtonWidth, ButtonHeight),
                TabIndex = i,
            };
            Controls.Add(button);
            _buttons.Add(button);
            x += ButtonWidth + ButtonGap;
        }

        AcceptButton = _buttons[defaultIndex];
        _escapeResult = buttons switch
        {
            MessageBoxButtons.OK => DialogResult.OK,
            MessageBoxButtons.YesNo => DialogResult.None,
            MessageBoxButtons.AbortRetryIgnore => DialogResult.None,
            _ => DialogResult.Cancel,
        };
        if (_escapeResult != DialogResult.None)
        {
            foreach (var b in _buttons)
            {
                if (b.DialogResult == _escapeResult) CancelButton = b;
            }
        }
        ActiveControl = _buttons[defaultIndex];
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        base.OnPaintBackground(pevent);
        var g = pevent.Graphics;
        using (var strip = new SolidBrush(Color.FromArgb(0xF0, 0xF0, 0xF0)))
        {
            g.FillRectangle(strip, new Rectangle(0, ClientSize.Height - StripHeight, ClientSize.Width, StripHeight));
        }
        if (_icon != null)
        {
            using var bmp = _icon.ToBitmap();
            g.DrawImage(bmp, Gap, Gap);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing via the title bar counts as Cancel (or OK for an OK-only box), as in Win32.
        if (DialogResult == DialogResult.None)
        {
            if (_escapeResult == DialogResult.None && e.CloseReason == CloseReason.UserClosing)
            {
                // Win32 greys out the close box for Yes/No and Abort/Retry/Ignore; we refuse the close.
                e.Cancel = true;
                base.OnFormClosing(e);
                return;
            }
            DialogResult = _escapeResult;
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Escape && _escapeResult == DialogResult.None) return true;
        return base.ProcessDialogKey(keyData);
    }
}
