using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

namespace System.Windows.Forms;

/// <summary>
/// RichEdit's CHARFORMAT, as much of it as RichTextBox exposes: the font (face, size in points - kept to
/// RichEdit's twips - and style), the text and back colours (<see cref="Color.Empty"/> back colour: none,
/// CFE_AUTOBACKCOLOR), the baseline offset in twips (SelectionCharOffset) and protection. Colours are kept
/// as plain RGB, as RichEdit keeps a COLORREF: SelectionColor hands back <c>Color.FromArgb</c>, never a named colour.
/// </summary>
internal sealed record RichCharFormat(string FontName, float Size, FontStyle Style, Color ForeColor, Color BackColor, int OffsetTwips, bool Protected)
{
    public static RichCharFormat From(Font font, Color foreColor) =>
        new(font.Name, SnapSize(font.SizeInPoints), font.Style & (FontStyle.Bold | FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout), Rgb(foreColor), Color.Empty, 0, false);

    public RichCharFormat WithFont(Font font) => this with { FontName = font.Name, Size = SnapSize(font.SizeInPoints), Style = font.Style & (FontStyle.Bold | FontStyle.Italic | FontStyle.Underline | FontStyle.Strikeout) };

    /// <summary>RichEdit keeps the size in twips (yHeight = points × 20, truncated).</summary>
    public static float SnapSize(float points) => Math.Max(1, (int)(points * 20)) / 20f;

    /// <summary>A colour as a COLORREF carries it: opaque RGB, no name.</summary>
    public static Color Rgb(Color c) => c.IsEmpty ? c : Color.FromArgb(255, c.R, c.G, c.B);
}

/// <summary>
/// RichEdit's PARAFORMAT as RichTextBox exposes it. Indents are in twips: <see cref="IndentTwips"/> is the first
/// line's (dxStartIndent), <see cref="OffsetTwips"/> moves the other lines relative to it (dxOffset, the hanging
/// indent), <see cref="RightIndentTwips"/> is from the right edge. Tab stops in twips from the left edge.
/// </summary>
internal sealed record RichParaFormat(HorizontalAlignment Alignment, bool Justify, int IndentTwips, int RightIndentTwips, int OffsetTwips, bool Bullet, int[] TabsTwips)
{
    public static readonly RichParaFormat Default = new(HorizontalAlignment.Left, false, 0, 0, 0, false, Array.Empty<int>());

    public bool Equals(RichParaFormat? other) =>
        other is not null && Alignment == other.Alignment && Justify == other.Justify && IndentTwips == other.IndentTwips
        && RightIndentTwips == other.RightIndentTwips && OffsetTwips == other.OffsetTwips && Bullet == other.Bullet
        && TabsTwips.AsSpan().SequenceEqual(other.TabsTwips);

    public override int GetHashCode() => HashCode.Combine(Alignment, Justify, IndentTwips, RightIndentTwips, OffsetTwips, Bullet, TabsTwips.Length);
}

/// <summary>
/// Formatted text: one <see cref="RichCharFormat"/> per character of <see cref="Text"/> (paragraph breaks are
/// "\n"), one <see cref="RichParaFormat"/> per paragraph (the breaks plus the last, unterminated one), and the
/// character format of the end of the text - RichEdit's final paragraph mark, what typing into an empty box uses.
/// </summary>
internal sealed class RichFragment
{
    public string Text = string.Empty;
    public List<RichCharFormat> Chars = new();
    public List<RichParaFormat> Paras = new();
    public RichCharFormat EndFormat = null!;
}

/// <summary>Twips (RTF, RichEdit) and pixels at 96 DPI, converted as WinForms' Pixel2Twip/Twip2Pixel do.</summary>
internal static class Twips
{
    public static int ToPixels(int twips) => (int)(twips / 20.0 / 72.0 * 96);

    public static int FromPixels(int pixels) => (int)(pixels / 96.0 * 72.0 * 20.0);
}

/// <summary>
/// Reads RTF into a <see cref="RichFragment"/>: fonts (with their charsets for \'hh), colours, character
/// formatting (\b \i \ul \strike \f \fs \cf \highlight/\cb \up/\dn \protect, hidden text dropped), paragraphs
/// (\pard \par \ql/\qc/\qr/\qj \li \ri \fi \tx, bullets from \pn\pnlvlblt or a Word list), \uN with \ucN fallback,
/// the special characters, fields by their result. Pictures, objects, style sheets, headers and the other
/// destinations are skipped. \line is RichEdit's soft line break, U+000B. What the RTF does not set is RTF's default
/// (12 points, regular, the automatic colour), the font the default font or, without a font table, the box's.
/// </summary>
internal sealed class RtfReader
{
    private sealed class State
    {
        public int Font = -1;
        public float Size;
        public FontStyle Style;
        public int ForeColor;
        public int BackColor;
        public int OffsetTwips;
        public bool Protected;
        public bool Hidden;
        public int Uc = 1;
        public Destination Dest = Destination.Text;
        public RichParaFormat Para = RichParaFormat.Default;
        public List<int> Tabs = new();
        public bool IgnorableGroup;

        public State Clone()
        {
            var s = (State)MemberwiseClone();
            s.Tabs = new List<int>(Tabs);
            s.IgnorableGroup = false;
            return s;
        }
    }

    private enum Destination { Text, Skip, FontTable, FontEntry, ColorTable, FieldInstruction, PnDefinition }

    /// <summary>RTF's default font size: \fs24.</summary>
    private const float DefaultSize = 12;

    private readonly string _rtf;
    private readonly RichCharFormat _defaultChar;
    private readonly bool _bytesAsLatin1;
    private int _pos;
    private readonly Dictionary<int, (string Name, int CodePage)> _fonts = new();
    private readonly List<Color> _colors = new();
    private bool _colorPending;
    private int _r, _g, _b;
    private int _defaultFont = -1;
    private int _documentCodePage = 1252;
    private readonly StringBuilder _fontName = new();
    private int _fontEntryIndex = -1;
    private int _fontEntryCharset = -1;
    private readonly List<byte> _bytes = new();
    private int _skipFallback;

    private readonly StringBuilder _text = new();
    private readonly List<RichCharFormat> _chars = new();
    private readonly List<RichParaFormat> _paras = new();
    private State _state = new();
    private readonly Stack<State> _stack = new();
    private RichCharFormat? _lastFormat;
    private State? _lastFormatState;

    private RtfReader(string rtf, RichCharFormat defaultChar, bool bytesAsLatin1)
    {
        _rtf = rtf;
        _defaultChar = defaultChar;
        _bytesAsLatin1 = bytesAsLatin1;
        _state.Size = DefaultSize;
    }

    /// <summary>
    /// Parses <paramref name="rtf"/>. <paramref name="bytesAsLatin1"/>: the string holds the file's bytes one per
    /// character (a stream read as Latin-1), so characters above 0x7F are bytes of the document's code page.
    /// </summary>
    public static RichFragment Read(string rtf, RichCharFormat defaultChar, bool bytesAsLatin1 = false)
    {
        var reader = new RtfReader(rtf, defaultChar, bytesAsLatin1);
        reader.Parse();
        var f = new RichFragment
        {
            Text = reader._text.ToString(),
            Chars = reader._chars,
            Paras = reader._paras,
            EndFormat = reader.CurrentFormat(),
        };
        f.Paras.Add(reader._state.Para with { TabsTwips = reader._state.Tabs.ToArray() });
        return f;
    }

    /// <summary>Does <paramref name="text"/> start as RTF does ("{\rtf")?</summary>
    public static bool LooksLikeRtf(string text) => text.StartsWith("{\\rtf", StringComparison.Ordinal);

    private void Parse()
    {
        while (_pos < _rtf.Length)
        {
            char c = _rtf[_pos++];
            switch (c)
            {
                case '{':
                    FlushBytes();
                    _skipFallback = 0;
                    _stack.Push(_state);
                    _state = _state.Clone();
                    break;
                case '}':
                    FlushBytes();
                    _skipFallback = 0;
                    EndGroup();
                    if (_stack.Count == 0) return; // the document's closing brace
                    _state = _stack.Pop();
                    break;
                case '\\':
                    ControlSequence();
                    break;
                case '\r':
                case '\n':
                    break;
                default:
                    if (_bytesAsLatin1 && c > 0x7F && c <= 0xFF) AddByte((byte)c);
                    else AddChar(c);
                    break;
            }
        }
        FlushBytes();
    }

    private void EndGroup()
    {
        switch (_state.Dest)
        {
            case Destination.FontEntry:
                CommitFontEntry();
                break;
        }
    }

    private void ControlSequence()
    {
        if (_pos >= _rtf.Length) return;
        char c = _rtf[_pos];
        if (!char.IsAsciiLetter(c))
        {
            _pos++;
            switch (c)
            {
                case '\'':
                    if (_pos + 2 <= _rtf.Length && byte.TryParse(_rtf.AsSpan(_pos, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                    {
                        _pos += 2;
                        AddByte(b);
                    }
                    return;
                case '*':
                    _state.IgnorableGroup = true;
                    return;
                case '~': AddChar('\u00A0'); return;
                case '_': AddChar('\u2011'); return;
                case '-': return; // optional hyphen
                case '\r':
                case '\n':
                    Word("par", false, 0);
                    return;
                default:
                    AddChar(c); // \\ \{ \} and anything else: the character itself
                    return;
            }
        }

        // The bytes of 'hh before it are decoded with the font they were written in.
        FlushBytes();
        int start = _pos;
        while (_pos < _rtf.Length && char.IsAsciiLetter(_rtf[_pos])) _pos++;
        var word = _rtf.Substring(start, _pos - start);
        bool hasParam = false;
        int param = 0;
        if (_pos < _rtf.Length && (_rtf[_pos] == '-' || char.IsAsciiDigit(_rtf[_pos])))
        {
            int ps = _pos;
            if (_rtf[_pos] == '-') _pos++;
            while (_pos < _rtf.Length && char.IsAsciiDigit(_rtf[_pos])) _pos++;
            hasParam = int.TryParse(_rtf.AsSpan(ps, _pos - ps), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out param);
        }
        if (_pos < _rtf.Length && _rtf[_pos] == ' ') _pos++;
        Word(word, hasParam, param);
    }

    private static readonly HashSet<string> s_skippedDestinations = new(StringComparer.Ordinal)
    {
        "stylesheet", "info", "pict", "object", "header", "headerl", "headerr", "headerf", "footer", "footerl", "footerr", "footerf",
        "listtable", "listoverridetable", "rsidtbl", "generator", "themedata", "colorschememapping", "latentstyles", "datastore",
        "xmlnstbl", "mmathPr", "pgdsctbl", "pntext", "listtext", "bkmkstart", "bkmkend", "shp", "shpinst", "nonshppict",
        "userprops", "docvar", "revtbl", "filetbl", "fchars", "lchars", "protusertbl", "wgrffmtfilter", "ftnsep", "ftnsepc",
        "aftnsep", "aftnsepc", "footnote", "annotation", "atnid", "atnauthor", "template", "operator", "title", "author",
        "company", "falt", "panose", "fname", "result", "objdata", "blipuid", "sp", "sn", "sv", "xe", "tc", "txe",
        "pnseclvl", "pntxta", "pntxtb", "listpicture", "passwordhash", "defchp", "defpap", "background", "ud", "upr",
    };

    private void Word(string word, bool hasParam, int param)
    {
        if (_skipFallback > 0 && _state.Dest == Destination.Text)
        {
            // A control word counts as one fallback character of a preceding \u.
            _skipFallback--;
            return;
        }

        // Destinations first: whatever follows in this group is theirs.
        switch (word)
        {
            case "fonttbl": _state.Dest = Destination.FontTable; return;
            case "colortbl": _state.Dest = Destination.ColorTable; _colorPending = false; return;
            case "fldinst": _state.Dest = Destination.FieldInstruction; return;
            case "fldrslt": _state.Dest = Destination.Text; return;
            case "pn":
                _state.Dest = Destination.PnDefinition;
                return;
            case "pnlvlblt":
                if (_state.Dest == Destination.PnDefinition) SetParaBullet(true);
                return;
        }
        if (_state.Dest == Destination.PnDefinition) return;
        if (s_skippedDestinations.Contains(word))
        {
            _state.Dest = Destination.Skip;
            return;
        }
        if (_state.Dest == Destination.Skip || _state.Dest == Destination.FieldInstruction) return;

        if (_state.Dest is Destination.FontTable or Destination.FontEntry)
        {
            switch (word)
            {
                case "f":
                    CommitFontEntry();
                    _state.Dest = Destination.FontEntry;
                    _fontEntryIndex = param;
                    _fontEntryCharset = -1;
                    _fontName.Clear();
                    return;
                case "fcharset":
                    _fontEntryCharset = param;
                    return;
            }
            return;
        }

        if (_state.Dest == Destination.ColorTable)
        {
            switch (word)
            {
                case "red": _r = param; _colorPending = true; break;
                case "green": _g = param; _colorPending = true; break;
                case "blue": _b = param; _colorPending = true; break;
            }
            return;
        }

        switch (word)
        {
            // header
            case "ansicpg": if (hasParam && param > 0) _documentCodePage = param; return;
            case "mac": _documentCodePage = 10000; return;
            case "pc": _documentCodePage = 437; return;
            case "pca": _documentCodePage = 850; return;
            case "deff": _defaultFont = param; return;
            case "uc": _state.Uc = Math.Max(0, param); return;
            case "u":
                FlushBytes();
                AddChar((char)(param < 0 ? param + 65536 : param));
                _skipFallback = _state.Uc;
                return;

            // characters
            case "line":
                AddChar('\v');
                return;
            case "par":
            case "sect":
            case "page":
            case "row":
                FlushBytes();
                AddParagraphBreak();
                return;
            case "tab":
            case "cell":
                AddChar('\t');
                return;
            case "bullet": AddChar('\u2022'); return;
            case "endash": AddChar('\u2013'); return;
            case "emdash": AddChar('\u2014'); return;
            case "lquote": AddChar('\u2018'); return;
            case "rquote": AddChar('\u2019'); return;
            case "ldblquote": AddChar('\u201C'); return;
            case "rdblquote": AddChar('\u201D'); return;
            case "enspace":
            case "emspace":
            case "qmspace":
                AddChar(' ');
                return;
            case "zwj": AddChar('\u200D'); return;
            case "zwnj": AddChar('\u200C'); return;

            // character formatting
            case "plain":
                _state.Font = -1;
                _state.Size = DefaultSize;
                _state.Style = FontStyle.Regular;
                _state.ForeColor = 0;
                _state.BackColor = 0;
                _state.OffsetTwips = 0;
                _state.Protected = false;
                _state.Hidden = false;
                return;
            case "f": _state.Font = param; return;
            case "fs": if (hasParam && param > 0) _state.Size = param / 2f; return;
            case "b": SetStyle(FontStyle.Bold, !hasParam || param != 0); return;
            case "i": SetStyle(FontStyle.Italic, !hasParam || param != 0); return;
            case "ul":
            case "uld":
            case "uldash":
            case "uldashd":
            case "uldashdd":
            case "uldb":
            case "ulhwave":
            case "ulth":
            case "ulw":
            case "ulwave":
                SetStyle(FontStyle.Underline, !hasParam || param != 0);
                return;
            case "ulnone": SetStyle(FontStyle.Underline, false); return;
            case "strike":
            case "striked":
                SetStyle(FontStyle.Strikeout, !hasParam || param != 0);
                return;
            case "cf": _state.ForeColor = param; return;
            case "cb":
            case "highlight":
            case "chcbpat":
                _state.BackColor = param;
                return;
            case "up": _state.OffsetTwips = (hasParam ? param : 6) * 10; return;
            case "dn": _state.OffsetTwips = -(hasParam ? param : 6) * 10; return;
            case "protect": _state.Protected = !hasParam || param != 0; return;
            case "v": _state.Hidden = !hasParam || param != 0; return;

            // paragraph formatting
            case "pard":
                _state.Para = RichParaFormat.Default;
                _state.Tabs.Clear();
                return;
            case "ql": _state.Para = _state.Para with { Alignment = HorizontalAlignment.Left, Justify = false }; return;
            case "qc": _state.Para = _state.Para with { Alignment = HorizontalAlignment.Center, Justify = false }; return;
            case "qr": _state.Para = _state.Para with { Alignment = HorizontalAlignment.Right, Justify = false }; return;
            case "qj":
            case "qd":
                _state.Para = _state.Para with { Alignment = HorizontalAlignment.Left, Justify = true };
                return;
            case "li":
                {
                    // \li is where the other lines start, \fi the first line relative to it.
                    int first = FirstLineIndent();
                    int li = param;
                    _state.Para = _state.Para with { IndentTwips = li + first, OffsetTwips = -first };
                    return;
                }
            case "fi":
                {
                    int li = _state.Para.IndentTwips + _state.Para.OffsetTwips;
                    _state.Para = _state.Para with { IndentTwips = li + param, OffsetTwips = -param };
                    return;
                }
            case "ri": _state.Para = _state.Para with { RightIndentTwips = param }; return;
            case "tx":
            case "tb":
                _state.Tabs.Add(param);
                return;
            case "ls":
                SetParaBullet(true);
                return;
        }
        // Anything else is ignored; an unknown "\*" destination is skipped with its group.
        if (_state.IgnorableGroup) _state.Dest = Destination.Skip;
    }

    private int FirstLineIndent() => -_state.Para.OffsetTwips;

    /// <summary>
    /// Sets the paragraph's bullet. Inside a "\*\pn" group the paragraph is the enclosing group's, so the
    /// setting goes out through the groups of the definition to the first one that is text.
    /// </summary>
    private void SetParaBullet(bool bullet)
    {
        _state.Para = _state.Para with { Bullet = bullet };
        if (_state.Dest != Destination.PnDefinition) return;
        foreach (var s in _stack)
        {
            s.Para = s.Para with { Bullet = bullet };
            if (s.Dest != Destination.PnDefinition) break;
        }
    }

    private void SetStyle(FontStyle style, bool on) => _state.Style = on ? _state.Style | style : _state.Style & ~style;

    private void CommitFontEntry()
    {
        if (_state.Dest != Destination.FontEntry || _fontEntryIndex < 0) return;
        var name = _fontName.ToString().Trim().TrimEnd(';').Trim();
        if (!_fonts.ContainsKey(_fontEntryIndex)) _fonts[_fontEntryIndex] = (name, CodePageOfCharset(_fontEntryCharset));
        _fontEntryIndex = -1;
        _fontName.Clear();
    }

    private static int CodePageOfCharset(int charset) => charset switch
    {
        0 => 1252,
        1 => 0,
        2 => 42, // symbol: bytes are the code points
        77 => 10000,
        128 => 932,
        129 => 949,
        130 => 1361,
        134 => 936,
        136 => 950,
        161 => 1253,
        162 => 1254,
        163 => 1258,
        177 => 1255,
        178 => 1256,
        186 => 1257,
        204 => 1251,
        222 => 874,
        238 => 1250,
        254 => 437,
        255 => 850,
        _ => 0,
    };

    private void AddByte(byte b)
    {
        if (_skipFallback > 0 && _state.Dest == Destination.Text)
        {
            _skipFallback--;
            return;
        }
        _bytes.Add(b);
    }

    private void FlushBytes()
    {
        if (_bytes.Count == 0) return;
        var bytes = _bytes.ToArray();
        _bytes.Clear();
        int cp = CurrentCodePage();
        string s;
        if (cp == 42) s = new string(bytes.Select(b => (char)(b < 0x80 ? b : 0xF000 + b)).ToArray()).Replace('\uF0B7', '\u2022');
        else
        {
            Encoding enc;
            try
            {
                enc = CodePagesEncodingProvider.Instance.GetEncoding(cp) ?? Encoding.GetEncoding(cp);
            }
            catch (Exception)
            {
                enc = Encoding.Latin1;
            }
            s = enc.GetString(bytes);
        }
        foreach (var ch in s) AddCharCore(ch);
    }

    private int CurrentCodePage()
    {
        int font = _state.Font >= 0 ? _state.Font : _defaultFont;
        if (font >= 0 && _fonts.TryGetValue(font, out var f) && f.CodePage != 0) return f.CodePage;
        return _documentCodePage;
    }

    private void AddChar(char c)
    {
        if (_skipFallback > 0 && _state.Dest == Destination.Text)
        {
            _skipFallback--;
            return;
        }
        FlushBytes();
        AddCharCore(c);
    }

    private void AddCharCore(char c)
    {
        switch (_state.Dest)
        {
            case Destination.FontEntry:
                _fontName.Append(c);
                if (c == ';') CommitFontEntry();
                return;
            case Destination.FontTable:
                return;
            case Destination.ColorTable:
                if (c == ';')
                {
                    _colors.Add(_colorPending ? Color.FromArgb(255, Clamp(_r), Clamp(_g), Clamp(_b)) : Color.Empty);
                    _colorPending = false;
                    _r = _g = _b = 0;
                }
                return;
            case Destination.Text:
                if (_state.Hidden || c is '\r' or '\0') return;
                if (c == '\n') { AddParagraphBreak(); return; }
                _text.Append(c);
                _chars.Add(CurrentFormat());
                return;
            default:
                return;
        }
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 255);

    private void AddParagraphBreak()
    {
        if (_state.Dest != Destination.Text) return;
        _text.Append('\n');
        _chars.Add(CurrentFormat());
        _paras.Add(_state.Para with { TabsTwips = _state.Tabs.ToArray() });
    }

    private RichCharFormat CurrentFormat()
    {
        var s = _state;
        if (_lastFormat != null && _lastFormatState is { } l && l.Font == s.Font && l.Size == s.Size && l.Style == s.Style
            && l.ForeColor == s.ForeColor && l.BackColor == s.BackColor && l.OffsetTwips == s.OffsetTwips && l.Protected == s.Protected)
        {
            return _lastFormat;
        }
        int fontIndex = s.Font >= 0 ? s.Font : _defaultFont;
        string name = fontIndex >= 0 && _fonts.TryGetValue(fontIndex, out var f) && f.Name.Length > 0 ? f.Name : _defaultChar.FontName;
        var fore = s.ForeColor > 0 && s.ForeColor < _colors.Count && !_colors[s.ForeColor].IsEmpty ? _colors[s.ForeColor] : _defaultChar.ForeColor;
        var back = s.BackColor > 0 && s.BackColor < _colors.Count ? _colors[s.BackColor] : Color.Empty;
        _lastFormat = new RichCharFormat(name, RichCharFormat.SnapSize(s.Size), s.Style, fore, back, s.OffsetTwips, s.Protected);
        _lastFormatState = s.Clone();
        return _lastFormat;
    }
}

/// <summary>
/// Writes a range of formatted text as RTF in the shape RichEdit streams it out (checked against the real control,
/// tests/Shared/CompatScenarios.cs): the font table in order of use, a colour table when a colour other than the
/// automatic one is used, "\pard" and the paragraph's properties (\fi \li \ri \q* \tx, a bullet's \pntext and \pn
/// groups first) only where the paragraph format changes - after an empty line - a bullet's \pntext again on every
/// bulleted paragraph, character properties as they change (\cf \highlight \ul \b \i \strike \protect \up/\dn \f \fs,
/// \fs whenever the size changes, even if it rounds to the same half point), a space after a control word only where
/// text follows, "\par" per paragraph break. The whole text ends with the "\par" of its final paragraph mark, a
/// selection where it ends. Characters outside ASCII are written as \uN? (RichEdit writes those of the font's code
/// page as \'hh - either reads back the same).
/// </summary>
internal static class RtfWriter
{
    public static string Write(string text, IReadOnlyList<RichCharFormat> chars, IReadOnlyList<RichParaFormat> paras, int start, int end,
        bool wholeDocument, RichCharFormat endFormat, Color autoColor)
    {
        var fonts = new List<string>();
        var colors = new List<Color>();
        int FontIndex(string name)
        {
            int i = fonts.FindIndex(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase));
            if (i < 0) { fonts.Add(name); i = fonts.Count - 1; }
            return i;
        }
        int ColorIndex(Color c)
        {
            if (c.IsEmpty) return 0;
            int argb = c.ToArgb();
            int i = colors.FindIndex(x => x.ToArgb() == argb);
            if (i < 0) { colors.Add(c); i = colors.Count - 1; }
            return i + 1;
        }
        int autoArgb = RichCharFormat.Rgb(autoColor).ToArgb();
        int ForeIndex(Color c) => ColorIndex(c.IsEmpty || c.ToArgb() == autoArgb ? Color.Empty : c);

        // Tables first: every format of the range, in order of use.
        for (int i = start; i < end; i++)
        {
            FontIndex(chars[i].FontName);
            ForeIndex(chars[i].ForeColor);
            ColorIndex(chars[i].BackColor);
        }
        if (wholeDocument || start == end)
        {
            FontIndex(endFormat.FontName);
            ForeIndex(endFormat.ForeColor);
            ColorIndex(endFormat.BackColor);
        }
        int firstPara = ParagraphOf(text, start);
        int lastPara = ParagraphOf(text, end);
        bool anyBullet = false;
        for (int p = firstPara; p <= lastPara && p < paras.Count; p++) anyBullet |= paras[p].Bullet;
        int symbolFont = anyBullet ? FontIndex("Symbol") : -1;

        var sb = new StringBuilder();
        sb.Append(@"{\rtf1\ansi\ansicpg1252\deff0\nouicompat\deflang1033{\fonttbl");
        for (int i = 0; i < fonts.Count; i++)
        {
            sb.Append(@"{\f").Append(i).Append(@"\fnil");
            sb.Append(i == symbolFont ? @"\fcharset2 " : @"\fcharset0 ");
            foreach (var ch in fonts[i]) AppendEscaped(sb, ch);
            sb.Append(";}");
        }
        sb.Append("}\r\n");
        if (colors.Count > 0)
        {
            sb.Append(@"{\colortbl ;");
            foreach (var c in colors) sb.Append(@"\red").Append(c.R).Append(@"\green").Append(c.G).Append(@"\blue").Append(c.B).Append(';');
            sb.Append("}\r\n");
        }
        sb.Append(@"{\*\generator NetForms}").Append(wholeDocument ? @"\viewkind4\uc1 " : @"\uc1 ").Append("\r\n");

        RichParaFormat? currentPara = null;
        RichCharFormat? current = null;
        bool pending = false; // a control word waits for its delimiter
        int para = firstPara;
        bool atParagraphStart = true;
        for (int i = start; ; i++)
        {
            bool atEnd = i >= end;
            if (atEnd && !wholeDocument) break;
            if (atParagraphStart)
            {
                var pf = para < paras.Count ? paras[para] : RichParaFormat.Default;
                if (currentPara == null || !currentPara.Equals(pf))
                {
                    if (currentPara != null) sb.Append("\r\n");
                    sb.Append(@"\pard");
                    if (pf.Bullet)
                    {
                        AppendBulletText(sb, symbolFont);
                        sb.Append(@"{\*\pn\pnlvlblt\pnf").Append(symbolFont).Append(@"\pnindent0{\pntxtb\'B7}}");
                    }
                    pending = true;
                    AppendPara(sb, pf);
                    currentPara = pf;
                }
                else if (pf.Bullet)
                {
                    AppendBulletText(sb, symbolFont);
                    pending = false;
                }
                atParagraphStart = false;
            }
            var f = atEnd ? endFormat : chars[i];
            if (current == null || !current.Equals(f))
            {
                int before = sb.Length;
                AppendCharDiff(sb, current, f, FontIndex, ForeIndex, ColorIndex);
                pending |= sb.Length > before;
                current = f;
            }
            if (atEnd)
            {
                sb.Append(@"\par").Append("\r\n");
                break;
            }
            char ch = text[i];
            if (ch == '\n')
            {
                sb.Append(@"\par").Append("\r\n");
                pending = false;
                para++;
                atParagraphStart = true;
                continue;
            }
            AppendChar(sb, ch, ref pending);
        }
        sb.Append("}\r\n");
        return sb.ToString();
    }

    private static int ParagraphOf(string text, int index)
    {
        int n = 0;
        for (int i = 0; i < index && i < text.Length; i++) if (text[i] == '\n') n++;
        return n;
    }

    private static void AppendBulletText(StringBuilder sb, int symbolFont) =>
        sb.Append(@"{\pntext\f").Append(symbolFont).Append(@"\'B7\tab}");

    private static void AppendPara(StringBuilder sb, RichParaFormat pf)
    {
        int first = -pf.OffsetTwips;
        int li = pf.IndentTwips + pf.OffsetTwips;
        if (first != 0) sb.Append(@"\fi").Append(first);
        if (li != 0) sb.Append(@"\li").Append(li);
        if (pf.RightIndentTwips != 0) sb.Append(@"\ri").Append(pf.RightIndentTwips);
        if (pf.Justify) sb.Append(@"\qj");
        else if (pf.Alignment == HorizontalAlignment.Center) sb.Append(@"\qc");
        else if (pf.Alignment == HorizontalAlignment.Right) sb.Append(@"\qr");
        foreach (var t in pf.TabsTwips) sb.Append(@"\tx").Append(t);
    }

    private static void AppendCharDiff(StringBuilder sb, RichCharFormat? from, RichCharFormat to, Func<string, int> font, Func<Color, int> foreIndex, Func<Color, int> backIndex)
    {
        int fore = foreIndex(to.ForeColor);
        if (from == null ? fore != 0 : foreIndex(from.ForeColor) != fore) sb.Append(@"\cf").Append(fore);
        int back = backIndex(to.BackColor);
        if (from == null ? back != 0 : backIndex(from.BackColor) != back) sb.Append(@"\highlight").Append(back);
        void Flag(FontStyle style, string on, string off)
        {
            bool now = (to.Style & style) != 0;
            bool was = from != null && (from.Style & style) != 0;
            if (now != was) sb.Append(now ? on : off);
        }
        Flag(FontStyle.Underline, @"\ul", @"\ulnone");
        Flag(FontStyle.Bold, @"\b", @"\b0");
        Flag(FontStyle.Italic, @"\i", @"\i0");
        Flag(FontStyle.Strikeout, @"\strike", @"\strike0");
        if (from == null ? to.Protected : from.Protected != to.Protected) sb.Append(to.Protected ? @"\protect" : @"\protect0");
        if (from == null ? to.OffsetTwips != 0 : from.OffsetTwips != to.OffsetTwips)
        {
            int halfPoints = to.OffsetTwips / 10;
            sb.Append(halfPoints >= 0 ? @"\up" : @"\dn").Append(Math.Abs(halfPoints));
        }
        if (from == null || !string.Equals(from.FontName, to.FontName, StringComparison.OrdinalIgnoreCase)) sb.Append(@"\f").Append(font(to.FontName));
        if (from == null || from.Size != to.Size) sb.Append(@"\fs").Append((int)Math.Round(to.Size * 2, MidpointRounding.AwayFromZero));
    }

    private static void AppendChar(StringBuilder sb, char ch, ref bool pending)
    {
        switch (ch)
        {
            case '\t':
                sb.Append(@"\tab");
                pending = true;
                return;
            case '\v':
                sb.Append(@"\line");
                pending = true;
                return;
            case (char)0xA0:
                sb.Append(@"\~");
                pending = false;
                return;
        }
        if (ch >= 0x20 && ch < 0x7F && ch is not ('\\' or '{' or '}'))
        {
            if (pending) sb.Append(' ');
            pending = false;
            sb.Append(ch);
            return;
        }
        AppendEscaped(sb, ch);
        pending = false;
    }

    private static void AppendEscaped(StringBuilder sb, char ch)
    {
        if (ch is '\\' or '{' or '}') sb.Append('\\').Append(ch);
        else if (ch >= 0x20 && ch < 0x7F) sb.Append(ch);
        else sb.Append('\\').Append('u').Append((short)ch).Append('?');
    }
}
