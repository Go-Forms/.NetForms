using System;

namespace System.Drawing;

public sealed class StringFormat : IDisposable, ICloneable
{
    public StringFormat() { }

    public StringFormat(StringFormatFlags options) => FormatFlags = options;

    public StringFormat(StringFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        FormatFlags = format.FormatFlags;
        Alignment = format.Alignment;
        LineAlignment = format.LineAlignment;
        Trimming = format.Trimming;
        HotkeyPrefix = format.HotkeyPrefix;
    }

    public StringFormatFlags FormatFlags { get; set; }
    public StringAlignment Alignment { get; set; } = StringAlignment.Near;
    public StringAlignment LineAlignment { get; set; } = StringAlignment.Near;
    public StringTrimming Trimming { get; set; } = StringTrimming.Character;
    public Text.HotkeyPrefix HotkeyPrefix { get; set; } = Text.HotkeyPrefix.None;

    public static StringFormat GenericDefault => new StringFormat();

    public static StringFormat GenericTypographic => new StringFormat(StringFormatFlags.LineLimit | StringFormatFlags.NoClip)
    {
        Trimming = StringTrimming.None,
    };

    public object Clone() => new StringFormat(this);

    public void Dispose() { }

    public override string ToString() => $"[StringFormat, FormatFlags={FormatFlags}]";
}
