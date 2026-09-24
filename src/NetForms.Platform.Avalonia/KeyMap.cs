using Avalonia.Input;

namespace NetForms.Platform.Avalonia;

/// <summary>Avalonia's WPF-style <see cref="Key"/> → Win32 virtual-key code (the values of System.Windows.Forms.Keys).</summary>
internal static class KeyMap
{
    public static int ToVirtualKey(Key key)
    {
        if (key >= Key.A && key <= Key.Z) return 0x41 + (key - Key.A);
        if (key >= Key.D0 && key <= Key.D9) return 0x30 + (key - Key.D0);
        if (key >= Key.NumPad0 && key <= Key.NumPad9) return 0x60 + (key - Key.NumPad0);
        if (key >= Key.F1 && key <= Key.F24) return 0x70 + (key - Key.F1);

        return key switch
        {
            Key.Cancel => 0x03,
            Key.Back => 0x08,
            Key.Tab => 0x09,
            Key.LineFeed => 0x0A,
            Key.Clear => 0x0C,
            Key.Return => 0x0D,
            Key.Pause => 0x13,
            Key.CapsLock => 0x14,
            Key.Escape => 0x1B,
            Key.Space => 0x20,
            Key.PageUp => 0x21,
            Key.PageDown => 0x22,
            Key.End => 0x23,
            Key.Home => 0x24,
            Key.Left => 0x25,
            Key.Up => 0x26,
            Key.Right => 0x27,
            Key.Down => 0x28,
            Key.Select => 0x29,
            Key.Print => 0x2A,
            Key.Execute => 0x2B,
            Key.Snapshot => 0x2C,
            Key.Insert => 0x2D,
            Key.Delete => 0x2E,
            Key.Help => 0x2F,
            Key.LWin => 0x5B,
            Key.RWin => 0x5C,
            Key.Apps => 0x5D,
            Key.Sleep => 0x5F,
            Key.Multiply => 0x6A,
            Key.Add => 0x6B,
            Key.Separator => 0x6C,
            Key.Subtract => 0x6D,
            Key.Decimal => 0x6E,
            Key.Divide => 0x6F,
            Key.NumLock => 0x90,
            Key.Scroll => 0x91,
            // WinForms reports the generic ShiftKey/ControlKey/Menu codes for KeyDown.
            Key.LeftShift or Key.RightShift => 0x10,
            Key.LeftCtrl or Key.RightCtrl => 0x11,
            Key.LeftAlt or Key.RightAlt => 0x12,
            Key.BrowserBack => 0xA6,
            Key.BrowserForward => 0xA7,
            Key.BrowserRefresh => 0xA8,
            Key.BrowserStop => 0xA9,
            Key.BrowserSearch => 0xAA,
            Key.BrowserFavorites => 0xAB,
            Key.BrowserHome => 0xAC,
            Key.VolumeMute => 0xAD,
            Key.VolumeDown => 0xAE,
            Key.VolumeUp => 0xAF,
            Key.MediaNextTrack => 0xB0,
            Key.MediaPreviousTrack => 0xB1,
            Key.MediaStop => 0xB2,
            Key.MediaPlayPause => 0xB3,
            Key.LaunchMail => 0xB4,
            Key.SelectMedia => 0xB5,
            Key.LaunchApplication1 => 0xB6,
            Key.LaunchApplication2 => 0xB7,
            Key.OemSemicolon => 0xBA,
            Key.OemPlus => 0xBB,
            Key.OemComma => 0xBC,
            Key.OemMinus => 0xBD,
            Key.OemPeriod => 0xBE,
            Key.OemQuestion => 0xBF,
            Key.OemTilde => 0xC0,
            Key.OemOpenBrackets => 0xDB,
            Key.OemPipe => 0xDC,
            Key.OemCloseBrackets => 0xDD,
            Key.OemQuotes => 0xDE,
            Key.Oem8 => 0xDF,
            Key.OemBackslash => 0xE2,
            Key.ImeProcessed => 0xE5,
            Key.Attn => 0xF6,
            Key.CrSel => 0xF7,
            Key.ExSel => 0xF8,
            Key.EraseEof => 0xF9,
            Key.Play => 0xFA,
            Key.Zoom => 0xFB,
            Key.NoName => 0xFC,
            Key.Pa1 => 0xFD,
            Key.OemClear => 0xFE,
            _ => 0,
        };
    }
}
