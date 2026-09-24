using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace System.Windows.Forms;

/// <summary>
/// The rich edit control, on the editing engine of <see cref="TextBoxBase"/>. The text is RichEdit's: a paragraph
/// break is one character, "\n", so indices, <see cref="TextBoxBase.TextLength"/> and <see cref="TextBoxBase.Lines"/>
/// are those of WinForms. Beside it the box keeps one character format per character (font, colours, offset,
/// protection) and one paragraph format per paragraph (alignment, indents, bullet, tab stops), which edits move
/// along: typed text takes the format before the caret (or one set on an empty selection), a paragraph's format
/// lives in its break, as RichEdit's does in the paragraph mark. RTF is read and written by <see cref="RtfReader"/>
/// and <see cref="RtfWriter"/>; lines have the height of their tallest run, runs share a baseline.
/// </summary>
[DefaultEvent("TextChanged")]
public class RichTextBox : TextBoxBase
{
    /// <summary>The width of the selection bar (ECO_SELECTIONBAR).</summary>
    private const int SelectionMarginWidth = 8;
    /// <summary>RichEdit's default tab stops: every half inch.</summary>
    private const int DefaultTabTwips = 720;
    private const int MaxTabStops = 32;

    private readonly List<RichCharFormat> _chars = new();
    private List<RichParaFormat> _paras = new() { RichParaFormat.Default };
    private RichCharFormat? _endFormat;
    private RichCharFormat? _insertFormat;
    private int _insertFormatAt = -1;
    private RichFragment? _pendingInsert;
    private RichFragment? _loading;
    private int[]? _paraStarts;
    private List<(int Start, int Length)>? _urls;
    private readonly Dictionary<RichCharFormat, Font> _fonts = new();

    private bool _detectUrls = true;
    private RichTextBoxScrollBars _scrollBars = RichTextBoxScrollBars.Both;
    private int _bulletIndent;
    private int _rightMargin;
    private float _zoom = 1f;
    private bool _autoWordSelection;
    private bool _enableAutoDragDrop;
    private bool _showSelectionMargin;
    private bool _richTextShortcutsEnabled = true;
    private RichTextBoxLanguageOptions _languageOption = RichTextBoxLanguageOptions.AutoFont | RichTextBoxLanguageOptions.DualFont;

    private int _lastSelStart;
    private int _lastSelEnd;
    private int _lastContentHeight = -1;
    private bool _barsDirty = true;
    private bool _configuringBars;
    private bool _overLink;
    private bool _overMargin;

    public RichTextBox()
    {
        base.AutoSize = false;
        MaxLength = int.MaxValue;
        Multiline = true;
        UpdateScrollBars();
        _lastContentHeight = ContentHeight;
    }

    protected override Size DefaultSize => new Size(100, 96);

    protected override Cursor DefaultCursor => _overLink ? Cursors.Hand : _overMargin ? Cursors.Default : Cursors.IBeam;

    internal override string LineBreak => "\n";

    internal override bool GroupsTyping => true;

    internal override int UndoLimit => 100;

    // --- properties ----------------------------------------------------------------

    [Browsable(false)]
    public new bool AllowDrop
    {
        get => base.AllowDrop;
        set => base.AllowDrop = value;
    }

    [DefaultValue(false)]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    [Category("Behavior")]
    [Description("Turns on/off automatic word selection.")]
    [DefaultValue(false)]
    public bool AutoWordSelection
    {
        get => _autoWordSelection;
        set => _autoWordSelection = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    [Category("Behavior")]
    [Description("Defines the indent for the bullets in the control.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int BulletIndent
    {
        get => _bulletIndent;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _bulletIndent = value;
            // Re-applied to the selection's bullets, as WinForms does.
            if (SelectionBullet) SelectionBullet = true;
        }
    }

    [Description("Indicates if the rich edit control can redo the previous action.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool CanRedo => base.CanRedo;

    [Category("Behavior")]
    [Description("Indicates whether URLs are automatically formatted as links.")]
    [DefaultValue(true)]
    public bool DetectUrls
    {
        get => _detectUrls;
        set
        {
            if (_detectUrls == value) return;
            _detectUrls = value;
            _urls = null;
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Enable drag/drop of text, pictures, and other data")]
    [DefaultValue(false)]
    public bool EnableAutoDragDrop
    {
        get => _enableAutoDragDrop;
        set => _enableAutoDragDrop = value;
    }

    [AllowNull]
    public override Font Font
    {
        get => base.Font;
        set
        {
            // The whole text takes the font (SCF_ALL), even when the box's own font does not change.
            if (value != null && TextLength > 0) ApplyToAll(f => f.WithFont(value));
            base.Font = value!;
        }
    }

    public override Color ForeColor
    {
        get => base.ForeColor;
        set => base.ForeColor = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RichTextBoxLanguageOptions LanguageOption
    {
        get => _languageOption;
        set => _languageOption = value;
    }

    [Category("Behavior")]
    [Description("Specifies the maximum number of characters that can be entered into the edit control.")]
    [DefaultValue(int.MaxValue)]
    [Localizable(true)]
    public override int MaxLength
    {
        get => base.MaxLength;
        set => base.MaxLength = value;
    }

    [Category("Behavior")]
    [Description("Controls whether the text of the edit control can span more than one line.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public override bool Multiline
    {
        get => base.Multiline;
        set
        {
            base.Multiline = value;
            UpdateScrollBars();
        }
    }

    [Category("Behavior")]
    [Description("The name of the action that will be performed if the user redoes a previous action.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string RedoActionName => CanRedo ? ActionName(PeekUndoAction(redo: true)) : string.Empty;

    [DefaultValue(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool RichTextShortcutsEnabled
    {
        get => _richTextShortcutsEnabled;
        set => _richTextShortcutsEnabled = value;
    }

    [Category("Behavior")]
    [Description("Defines the right margin dimensions.")]
    [DefaultValue(0)]
    [Localizable(true)]
    public int RightMargin
    {
        get => _rightMargin;
        set
        {
            if (_rightMargin == value) return;
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _rightMargin = value;
            LayoutChanged();
        }
    }

    [Description("Defines the rich text-formatted contents of the control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [RefreshProperties(RefreshProperties.All)]
    [AllowNull]
    public string Rtf
    {
        get => RtfWriter.Write(Text, _chars, _paras, 0, TextLength, wholeDocument: true, EndFormat, AutoColor);
        set
        {
            value ??= string.Empty;
            if (value.Equals(Rtf, StringComparison.Ordinal)) return;
            if (value.Length == 0)
            {
                // WM_SETTEXT "": the text goes, and with it its formatting.
                LoadFragment(PlainFragment(string.Empty));
                return;
            }
            if (!RtfReader.LooksLikeRtf(value)) throw new ArgumentException(SystemStrings.Get("File format is not valid."));
            LoadFragment(ReadDocument(value, bytesAsLatin1: false));
            Modified = true;
        }
    }

    [Category("Appearance")]
    [Description("Defines the behavior of the scroll bars of the control.")]
    [DefaultValue(RichTextBoxScrollBars.Both)]
    [Localizable(true)]
    public RichTextBoxScrollBars ScrollBars
    {
        get => _scrollBars;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(RichTextBoxScrollBars));
            if (_scrollBars == value) return;
            _scrollBars = value;
            LayoutChanged();
        }
    }

    [Description("Defines the rich text formatted contents of the currently selected text.")]
    [DefaultValue("")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public string SelectedRtf
    {
        // An empty selection streams out nothing.
        get => SelectionLength == 0 ? string.Empty : RtfWriter.Write(Text, _chars, _paras, SelectionStart, SelectionStart + SelectionLength, wholeDocument: false, EndFormat, AutoColor);
        set
        {
            value ??= string.Empty;
            if (TouchesProtected(SelectionStart, SelectionLength))
            {
                OnProtected(EventArgs.Empty);
                return;
            }
            if (value.Length == 0)
            {
                base.SelectedText = string.Empty;
                return;
            }
            if (!RtfReader.LooksLikeRtf(value)) throw new ArgumentException(SystemStrings.Get("File format is not valid."));
            InsertFragment(RtfReader.Read(value, RtfDefault), UndoAction.Unknown, fromUser: false);
        }
    }

    [Description("Displays the currently selected text in the control.")]
    [DefaultValue("")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public override string SelectedText
    {
        get => base.SelectedText;
        set
        {
            if (TouchesProtected(SelectionStart, SelectionLength))
            {
                OnProtected(EventArgs.Empty);
                return;
            }
            base.SelectedText = value ?? string.Empty;
        }
    }

    [Description("Defines the alignment of the currently selected text.")]
    [DefaultValue(HorizontalAlignment.Left)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public HorizontalAlignment SelectionAlignment
    {
        // Mixed paragraphs, and justified ones (PFA_JUSTIFY has no HorizontalAlignment), read as Left.
        get => UniformPara(p => p.Justify ? HorizontalAlignment.Left : p.Alignment, out var a) ? a : HorizontalAlignment.Left;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(HorizontalAlignment));
            ApplyParaFormat(p => p with { Alignment = value, Justify = false });
        }
    }

    [Description("Sets the text color for the currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectionBackColor
    {
        get => UniformChar(f => f.BackColor, out var c) ? (c.IsEmpty ? BackColor : FromColorRef(c)) : Color.Empty;
        set
        {
            var back = RichCharFormat.Rgb(value);
            ApplyCharFormat(f => f with { BackColor = back });
        }
    }

    [Description("Turns on/off the bullets in front of the currently selected text.")]
    [DefaultValue(false)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SelectionBullet
    {
        get => UniformPara(p => p.Bullet, out var b) && b;
        set
        {
            int offset = value ? Twips.FromPixels(_bulletIndent) : 0;
            ApplyParaFormat(p => p with { Bullet = value, OffsetTwips = offset });
        }
    }

    [Description("Defines the superscript/subscript mode for the characters.")]
    [DefaultValue(0)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionCharOffset
    {
        // CHARFORMAT's yOffset of the selection's first character (EM_GETCHARFORMAT fills it even when mixed).
        get => Twips.ToPixels(SelectionFormats().First().OffsetTwips);
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 2000);
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -2000);
            int twips = Twips.FromPixels(value);
            ApplyCharFormat(f => f with { OffsetTwips = twips });
        }
    }

    [Description("Sets the text color for the currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color SelectionColor
    {
        get => UniformChar(f => f.ForeColor, out var c) ? FromColorRef(c) : Color.Empty;
        set
        {
            var fore = RichCharFormat.Rgb(value);
            ApplyCharFormat(f => f with { ForeColor = fore });
        }
    }

    [Description("Defines the font of the currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [DisallowNull]
    public Font? SelectionFont
    {
        get
        {
            // As GetCharFormatFont: no font when the faces differ, 13 points when the sizes do, a style only when all share it.
            if (!UniformChar(f => f.FontName.ToUpperInvariant(), out _)) return null;
            var formats = SelectionFormats().ToList();
            float size = UniformChar(f => f.Size, out var s) ? s : 13;
            var style = FontStyle.Regular;
            foreach (var bit in new[] { FontStyle.Bold, FontStyle.Italic, FontStyle.Strikeout, FontStyle.Underline })
            {
                if (formats.All(f => (f.Style & bit) != 0)) style |= bit;
            }
            try
            {
                return new Font(formats[0].FontName, size, style, GraphicsUnit.Point);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            ApplyCharFormat(f => f.WithFont(value));
        }
    }

    [Description("Defines the hanging indent of the currently selected text.")]
    [DefaultValue(0)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionHangingIndent
    {
        get => UniformPara(p => p.OffsetTwips, out var t) ? Twips.ToPixels(t) : 0;
        set
        {
            int twips = Twips.FromPixels(value);
            ApplyParaFormat(p => p with { OffsetTwips = twips });
        }
    }

    [Description("Defines the indent of the currently selected text.")]
    [DefaultValue(0)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionIndent
    {
        get => UniformPara(p => p.IndentTwips, out var t) ? Twips.ToPixels(t) : 0;
        set
        {
            int twips = Twips.FromPixels(value);
            ApplyParaFormat(p => p with { IndentTwips = twips });
        }
    }

    [Category("Appearance")]
    [Description("The number of characters selected in the text box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override int SelectionLength
    {
        get => base.SelectionLength;
        set => base.SelectionLength = value;
    }

    [Description("Turns on/off protection around the contents of the currently selected text.")]
    [DefaultValue(false)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool SelectionProtected
    {
        get => SelectionFormats().All(f => f.Protected);
        set => ApplyCharFormat(f => f with { Protected = value }, changesProtection: true);
    }

    [Description("Defines the right-indent of the currently selected text.")]
    [DefaultValue(0)]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionRightIndent
    {
        get => UniformPara(p => p.RightIndentTwips, out var t) ? Twips.ToPixels(t) : 0;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            int twips = Twips.FromPixels(value);
            ApplyParaFormat(p => p with { RightIndentTwips = twips });
        }
    }

    [Description("Defines the locations of tab stops in the currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [AllowNull]
    public int[] SelectionTabs
    {
        get => UniformPara(p => string.Join(",", p.TabsTwips), out _) ? SelectedParagraphFormats().First().TabsTwips.Select(Twips.ToPixels).ToArray() : Array.Empty<int>();
        set
        {
            if (value != null && value.Length > MaxTabStops) throw new ArgumentOutOfRangeException(nameof(value), SystemStrings.Get("SelTabCount out of range."));
            var tabs = (value ?? Array.Empty<int>()).Select(Twips.FromPixels).ToArray();
            ApplyParaFormat(p => p with { TabsTwips = tabs });
        }
    }

    [Category("Behavior")]
    [Description("The type of selection.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public RichTextBoxSelectionTypes SelectionType => SelectionLength switch
    {
        0 => RichTextBoxSelectionTypes.Empty,
        1 => RichTextBoxSelectionTypes.Text,
        _ => RichTextBoxSelectionTypes.Text | RichTextBoxSelectionTypes.MultiChar,
    };

    [Category("Behavior")]
    [Description("Turns on/off the selection margin.")]
    [DefaultValue(false)]
    public bool ShowSelectionMargin
    {
        get => _showSelectionMargin;
        set
        {
            if (_showSelectionMargin == value) return;
            _showSelectionMargin = value;
            LayoutChanged();
        }
    }

    [Localizable(true)]
    [RefreshProperties(RefreshProperties.All)]
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            // WM_SETTEXT/StreamIn: even the same text comes back without its formatting.
            var text = NormalizeNewlines(value ?? string.Empty);
            if (text == base.Text && IsPlain()) return;
            ReplaceAllText(text);
        }
    }

    [Browsable(false)]
    public override int TextLength => base.TextLength;

    [Category("Behavior")]
    [Description("The name of the action that will be performed if the user undoes a previous edit.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string UndoActionName => CanUndo ? ActionName(PeekUndoAction(redo: false)) : string.Empty;

    [Category("Behavior")]
    [Description("Defines the current scaling factor of the RichTextBox display; 1.0 is normal viewing.")]
    [DefaultValue(1.0f)]
    [Localizable(true)]
    public float ZoomFactor
    {
        get => _zoom;
        set
        {
            if (!float.IsNaN(value))
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, 0.015625f);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, 64.0f);
            }
            if (value == _zoom) return;
            // EM_SETZOOM takes a fraction: thousandths, rounded up (as WinForms' SendZoomFactor).
            float zoom = 1f;
            if (value != 1f && !float.IsNaN(value))
            {
                float multiplier = 1000 * value;
                int numerator = (int)Math.Ceiling(multiplier);
                if (numerator >= 64000) numerator = (int)Math.Floor(multiplier);
                zoom = numerator / 1000f;
            }
            if (zoom == _zoom) return;
            _zoom = zoom;
            ClearFonts();
            LayoutChanged();
        }
    }

    // --- events --------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs when the control's contents are either smaller or larger than the control's window size.")]
    public event ContentsResizedEventHandler? ContentsResized;

    [Browsable(false)]
    public new event DragEventHandler? DragDrop
    {
        add => base.DragDrop += value;
        remove => base.DragDrop -= value;
    }

    [Browsable(false)]
    public new event DragEventHandler? DragEnter
    {
        add => base.DragEnter += value;
        remove => base.DragEnter -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? DragLeave
    {
        add => base.DragLeave += value;
        remove => base.DragLeave -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event DragEventHandler? DragOver
    {
        add => base.DragOver += value;
        remove => base.DragOver -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event GiveFeedbackEventHandler? GiveFeedback
    {
        add => base.GiveFeedback += value;
        remove => base.GiveFeedback -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event QueryContinueDragEventHandler? QueryContinueDrag
    {
        add => base.QueryContinueDrag += value;
        remove => base.QueryContinueDrag -= value;
    }

    [Category("Behavior")]
    [Description("Occurs when the control's horizontal scroll bar is clicked.")]
    public event EventHandler? HScroll;

    [Category("Behavior")]
    [Description("Occurs when the control's IME conversion status changes. (East Asian versions of OS only.)")]
    public event EventHandler? ImeChange;

    [Category("Behavior")]
    [Description("Occurs when a hyperlink in the text is clicked.")]
    public event LinkClickedEventHandler? LinkClicked;

    [Category("Behavior")]
    [Description("Occurs when the user takes an action that would change a protected range of text.")]
    public event EventHandler? Protected;

    [Category("Behavior")]
    [Description("Occurs when the current selection has changed.")]
    public event EventHandler? SelectionChanged;

    [Category("Behavior")]
    [Description("Occurs when the control's vertical scroll bar is clicked.")]
    public event EventHandler? VScroll;

    protected virtual void OnContentsResized(ContentsResizedEventArgs e) => ContentsResized?.Invoke(this, e);

    protected virtual void OnHScroll(EventArgs e) => HScroll?.Invoke(this, e);

    protected virtual void OnImeChange(EventArgs e) => ImeChange?.Invoke(this, e);

    protected virtual void OnLinkClicked(LinkClickedEventArgs e) => LinkClicked?.Invoke(this, e);

    protected virtual void OnProtected(EventArgs e) => Protected?.Invoke(this, e);

    protected virtual void OnSelectionChanged(EventArgs e) => SelectionChanged?.Invoke(this, e);

    protected virtual void OnVScroll(EventArgs e) => VScroll?.Invoke(this, e);

    // --- methods -------------------------------------------------------------------

    public bool CanPaste(DataFormats.Format clipFormat)
    {
        ArgumentNullException.ThrowIfNull(clipFormat);
        return clipFormat.Name switch
        {
            DataFormats.Text or DataFormats.UnicodeText or DataFormats.OemText => Clipboard.ContainsText(),
            _ => Clipboard.ContainsData(clipFormat.Name),
        };
    }

    public void Paste(DataFormats.Format clipFormat)
    {
        ArgumentNullException.ThrowIfNull(clipFormat);
        if (ReadOnly) return;
        switch (clipFormat.Name)
        {
            case DataFormats.Rtf:
                if (Clipboard.GetData(DataFormats.Rtf) is string rtf && RtfReader.LooksLikeRtf(rtf))
                {
                    InsertFragment(RtfReader.Read(rtf, RtfDefault), UndoAction.Paste, fromUser: true);
                }
                break;
            case DataFormats.Text:
            case DataFormats.UnicodeText:
            case DataFormats.OemText:
                var text = Clipboard.GetText();
                if (text.Length > 0) Paste(text);
                break;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public new void DrawToBitmap(Bitmap bitmap, Rectangle targetBounds) => base.DrawToBitmap(bitmap, targetBounds);

    public int Find(string str) => Find(str, 0, 0, RichTextBoxFinds.None);

    public int Find(string str, RichTextBoxFinds options) => Find(str, 0, 0, options);

    public int Find(string str, int start, RichTextBoxFinds options) => Find(str, start, -1, options);

    /// <summary>
    /// EM_FINDTEXT over [<paramref name="start"/>, <paramref name="end"/>) (-1: to the end; an empty range: the whole
    /// text), forwards or, with <see cref="RichTextBoxFinds.Reverse"/>, the last match; the match is selected and
    /// scrolled to unless <see cref="RichTextBoxFinds.NoHighlight"/>.
    /// </summary>
    public int Find(string str, int start, int end, RichTextBoxFinds options)
    {
        ArgumentNullException.ThrowIfNull(str);
        int textLength = TextLength;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, textLength);
        if (end < -1) throw new ArgumentOutOfRangeException(nameof(end), end, FindEndInvalid(end));
        if (end == -1) end = textLength;
        if (start > end) throw new ArgumentException(FindEndInvalid(end));
        if (start == end)
        {
            start = 0;
            end = textLength;
        }

        int position = FindText(str, start, end, options);
        if (position != -1 && (options & RichTextBoxFinds.NoHighlight) == 0)
        {
            Select(position, str.Length);
            ScrollToCaret();
        }
        return position;
    }

    private static string FindEndInvalid(int end) =>
        string.Format(SystemStrings.Get("Value '{0}' is not a valid value for 'end'.  'end' must be greater than or equal to 'start', or -1."), end);

    private int FindText(string str, int start, int end, RichTextBoxFinds options)
    {
        if (str.Length == 0 || end - start < str.Length) return -1;
        var text = Text;
        var comparison = (options & RichTextBoxFinds.MatchCase) != 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        bool wholeWord = (options & RichTextBoxFinds.WholeWord) != 0;
        bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        bool Accept(int at) => !wholeWord
            || ((at == 0 || !IsWord(text[at - 1]) || !IsWord(str[0])) && (at + str.Length >= text.Length || !IsWord(text[at + str.Length]) || !IsWord(str[^1])));

        if ((options & RichTextBoxFinds.Reverse) == 0)
        {
            for (int from = start; from <= end - str.Length;)
            {
                int at = text.IndexOf(str, from, end - from, comparison);
                if (at < 0) return -1;
                if (Accept(at)) return at;
                from = at + 1;
            }
            return -1;
        }
        for (int to = end; to - start >= str.Length;)
        {
            int at = text.LastIndexOf(str, to - 1, to - start, comparison);
            if (at < 0) return -1;
            if (Accept(at)) return at;
            to = at + str.Length - 1;
        }
        return -1;
    }

    public int Find(char[] characterSet) => Find(characterSet, 0, -1);

    public int Find(char[] characterSet, int start) => Find(characterSet, start, -1);

    /// <summary>The first character of <paramref name="characterSet"/> in [<paramref name="start"/>, <paramref name="end"/>); nothing is selected.</summary>
    public int Find(char[] characterSet, int start, int end)
    {
        int textLength = TextLength;
        ArgumentNullException.ThrowIfNull(characterSet);
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, textLength);
        if (end != -1) ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
        if (characterSet.Length == 0) return -1;
        if (start == end)
        {
            start = 0;
            end = textLength;
        }
        if (end == -1) end = textLength;
        int at = Text.AsSpan(start, Math.Min(end, textLength) - start).IndexOfAny(characterSet);
        return at < 0 ? -1 : start + at;
    }

    /// <summary>EM_CHARFROMPOS: past the last character it answers the last character.</summary>
    public override int GetCharIndexFromPosition(Point pt)
    {
        int index = base.GetCharIndexFromPosition(pt);
        if (index >= TextLength) index = Math.Max(TextLength - 1, 0);
        return index;
    }

    public override int GetLineFromCharIndex(int index) => base.GetLineFromCharIndex(index);

    public override Point GetPositionFromCharIndex(int index)
    {
        if (index < 0 || index > TextLength) return Point.Empty;
        return base.GetPositionFromCharIndex(index);
    }

    public void LoadFile(string path) => LoadFile(path, RichTextBoxStreamType.RichText);

    public void LoadFile(string path, RichTextBoxStreamType fileType)
    {
        if (!Enum.IsDefined(fileType)) throw new InvalidEnumArgumentException(nameof(fileType), (int)fileType, typeof(RichTextBoxStreamType));
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        LoadFile(file, fileType);
    }

    public void LoadFile(Stream data, RichTextBoxStreamType fileType)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!Enum.IsDefined(fileType)) throw new InvalidEnumArgumentException(nameof(fileType), (int)fileType, typeof(RichTextBoxStreamType));
        using var buffer = new MemoryStream();
        data.CopyTo(buffer);
        var bytes = buffer.ToArray();
        switch (fileType)
        {
            case RichTextBoxStreamType.RichText:
                {
                    if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "{\\rtf") throw new ArgumentException(SystemStrings.Get("File format is not valid."));
                    LoadFragment(ReadDocument(Encoding.Latin1.GetString(bytes), bytesAsLatin1: true));
                    break;
                }
            case RichTextBoxStreamType.PlainText:
                LoadPlainText(DecodeAnsi(bytes));
                break;
            case RichTextBoxStreamType.UnicodePlainText:
                {
                    int skip = bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE ? 2 : 0;
                    LoadPlainText(Encoding.Unicode.GetString(bytes, skip, bytes.Length - skip));
                    break;
                }
            default:
                throw new ArgumentException(SystemStrings.Get("File type is not valid."));
        }
        Modified = true;
    }

    public void SaveFile(string path) => SaveFile(path, RichTextBoxStreamType.RichText);

    public void SaveFile(string path, RichTextBoxStreamType fileType)
    {
        if (!Enum.IsDefined(fileType)) throw new InvalidEnumArgumentException(nameof(fileType), (int)fileType, typeof(RichTextBoxStreamType));
        using var file = File.Create(path);
        SaveFile(file, fileType);
    }

    public void SaveFile(Stream data, RichTextBoxStreamType fileType)
    {
        ArgumentNullException.ThrowIfNull(data);
        byte[] bytes = fileType switch
        {
            RichTextBoxStreamType.RichText or RichTextBoxStreamType.RichNoOleObjs => Encoding.ASCII.GetBytes(Rtf),
            RichTextBoxStreamType.PlainText or RichTextBoxStreamType.TextTextOleObjs => AnsiEncoding().GetBytes(Text.Replace("\n", "\r\n")),
            RichTextBoxStreamType.UnicodePlainText => Encoding.Unicode.GetBytes(Text.Replace("\n", "\r\n")),
            _ => throw new InvalidEnumArgumentException(nameof(fileType), (int)fileType, typeof(RichTextBoxStreamType)),
        };
        data.Write(bytes, 0, bytes.Length);
    }

    protected virtual object CreateRichEditOleCallback() => new object();

    protected override bool ProcessCmdKey(ref Message m, Keys keyData)
    {
        if (!_richTextShortcutsEnabled && keyData is (Keys.Control | Keys.L) or (Keys.Control | Keys.R) or (Keys.Control | Keys.E) or (Keys.Control | Keys.J))
        {
            return true;
        }
        return base.ProcessCmdKey(ref m, keyData);
    }

    protected override void WndProc(ref Message m) => base.WndProc(ref m);

    protected override void OnBackColorChanged(EventArgs e)
    {
        Invalidate();
        base.OnBackColorChanged(e);
    }

    protected override void OnGotFocus(EventArgs e) => base.OnGotFocus(e);

    protected override void OnHandleCreated(EventArgs e) => base.OnHandleCreated(e);

    protected override void OnHandleDestroyed(EventArgs e) => base.OnHandleDestroyed(e);

    protected override void OnRightToLeftChanged(EventArgs e) => base.OnRightToLeftChanged(e);

    protected override void OnFontChanged(EventArgs e)
    {
        // The whole text takes the box's font (SCF_ALL); an empty box types with the default format (WM_SETFONT).
        if (TextLength > 0)
        {
            var font = Font;
            ApplyToAll(f => f.WithFont(font));
        }
        else
        {
            _endFormat = DefaultFormat;
            _insertFormat = null;
            FormattingChanged();
        }
        base.OnFontChanged(e);
    }

    protected override void OnForeColorChanged(EventArgs e)
    {
        var color = RichCharFormat.Rgb(ForeColor);
        ApplyToAll(f => f with { ForeColor = color });
        base.OnForeColorChanged(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        UpdateScrollBars();
        CheckContentsResized();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        CheckContentsResized();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !e.Control || e.Shift || e.Alt || ReadOnly || !_richTextShortcutsEnabled) return;
        switch (e.KeyCode)
        {
            // RichEdit's own paragraph shortcuts.
            case Keys.L: ApplyParaFormat(p => p with { Alignment = HorizontalAlignment.Left, Justify = false }); break;
            case Keys.E: ApplyParaFormat(p => p with { Alignment = HorizontalAlignment.Center, Justify = false }); break;
            case Keys.R: ApplyParaFormat(p => p with { Alignment = HorizontalAlignment.Right, Justify = false }); break;
            case Keys.J: ApplyParaFormat(p => p with { Alignment = HorizontalAlignment.Left, Justify = true }); break;
            default: return;
        }
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var area = TextArea;
        bool overMargin = _showSelectionMargin && e.X < area.X && e.X >= BorderSize;
        bool overLink = !overMargin && _detectUrls && UrlAt(CharAtPoint(e.Location)) != null;
        if (overLink != _overLink || overMargin != _overMargin)
        {
            _overLink = overLink;
            _overMargin = overMargin;
            TopLevelForm?.UpdateCursor();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _overLink = _overMargin = false;
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (_barsDirty) UpdateScrollBars();
        base.OnPaint(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ClearFonts();
        base.Dispose(disposing);
    }

    // --- the formatting model ------------------------------------------------------

    /// <summary>
    /// The default character format: the box's font as WM_SETFONT gives it to RichEdit - a LOGFONT, whole pixels at 96 DPI,
    /// so Arial 10 is 9.75 points (the real control reads that back) - and its text colour. Text without formatting of
    /// its own (Text, plain files, an emptied box) takes it; the font set while there is text is applied exactly (SCF_ALL).
    /// </summary>
    private RichCharFormat DefaultFormat
    {
        get
        {
            var format = RichCharFormat.From(Font, ForeColor);
            float pixels = (float)Math.Round(Font.SizeInPoints * 96 / 72, MidpointRounding.AwayFromZero);
            return format with { Size = RichCharFormat.SnapSize(pixels * 72 / 96) };
        }
    }

    /// <summary>RichEdit's automatic text colour (CFE_AUTOCOLOR): the window text colour; RTF's \cf0.</summary>
    private static Color AutoColor => RichCharFormat.Rgb(SystemColors.WindowText);

    /// <summary>What RTF that sets nothing reads as: RTF's defaults, the box's font where there is no font table.</summary>
    private RichCharFormat RtfDefault => DefaultFormat with { ForeColor = AutoColor };

    /// <summary>A colour as RichEdit hands it back, through a COLORREF: ColorTranslator.FromOle names the known ones.</summary>
    private static Color FromColorRef(Color c) => c.IsEmpty ? c : ColorTranslator.FromOle(c.R | (c.G << 8) | (c.B << 16));

    private bool IsPlain()
    {
        var format = DefaultFormat;
        foreach (var f in _chars) if (!f.Equals(format)) return false;
        foreach (var p in _paras) if (!p.Equals(RichParaFormat.Default)) return false;
        return true;
    }

    private RichFragment PlainFragment(string text)
    {
        var format = DefaultFormat;
        return new RichFragment
        {
            Text = text,
            Chars = Enumerable.Repeat(format, text.Length).ToList(),
            Paras = Enumerable.Repeat(RichParaFormat.Default, CountBreaks(text, 0, text.Length) + 1).ToList(),
            EndFormat = format,
        };
    }

    /// <summary>The format of the text's end - RichEdit's final paragraph mark; an empty box types with it.</summary>
    private RichCharFormat EndFormat => _endFormat ??= DefaultFormat;

    /// <summary>
    /// The format text inserted at <paramref name="pos"/> takes: one set on the empty selection there, else the
    /// character before (within the paragraph), else the one after, else the end's.
    /// </summary>
    private RichCharFormat FormatForInsertAt(int pos)
    {
        if (_insertFormat != null && pos == _insertFormatAt) return _insertFormat;
        if (_chars.Count == 0) return EndFormat;
        var text = Text;
        pos = Math.Clamp(pos, 0, _chars.Count);
        if (pos > 0 && text[pos - 1] != '\n') return _chars[pos - 1];
        if (pos < _chars.Count) return _chars[pos];
        return _chars[pos - 1];
    }

    /// <summary>The character formats of the selection, or the insertion format at an empty one.</summary>
    private IEnumerable<RichCharFormat> SelectionFormats()
    {
        int s = SelectionStart, len = SelectionLength;
        if (len == 0)
        {
            yield return FormatForInsertAt(s);
            yield break;
        }
        for (int i = s; i < s + len && i < _chars.Count; i++) yield return _chars[i];
    }

    private bool UniformChar<T>(Func<RichCharFormat, T> get, out T value)
    {
        value = default!;
        bool first = true;
        RichCharFormat? last = null;
        foreach (var f in SelectionFormats())
        {
            if (ReferenceEquals(f, last)) continue;
            last = f;
            var v = get(f);
            if (first)
            {
                value = v;
                first = false;
            }
            else if (!EqualityComparer<T>.Default.Equals(v, value))
            {
                return false;
            }
        }
        return !first;
    }

    /// <summary>The paragraphs the selection touches: the one of its start through the one of its last character.</summary>
    private (int First, int Last) SelectedParagraphs()
    {
        int s = SelectionStart, len = SelectionLength;
        int first = ParaOf(s);
        int last = len > 0 ? ParaOf(s + len - 1) : first;
        return (first, last);
    }

    private IEnumerable<RichParaFormat> SelectedParagraphFormats()
    {
        var (first, last) = SelectedParagraphs();
        for (int p = first; p <= last; p++) yield return _paras[p];
    }

    private bool UniformPara<T>(Func<RichParaFormat, T> get, out T value)
    {
        value = default!;
        bool first = true;
        foreach (var p in SelectedParagraphFormats())
        {
            var v = get(p);
            if (first)
            {
                value = v;
                first = false;
            }
            else if (!EqualityComparer<T>.Default.Equals(v, value))
            {
                return false;
            }
        }
        return !first;
    }

    /// <summary>
    /// Changes the selection's character format (EM_SETCHARFORMAT, SCF_SELECTION): an empty selection keeps it for
    /// the next typing there; protected text refuses anything but its protection, and raises Protected.
    /// </summary>
    private void ApplyCharFormat(Func<RichCharFormat, RichCharFormat> change, bool changesProtection = false)
    {
        int s = SelectionStart, len = SelectionLength;
        if (len == 0)
        {
            _insertFormat = change(FormatForInsertAt(s));
            _insertFormatAt = s;
            if (_chars.Count == 0) _endFormat = _insertFormat;
            return;
        }
        if (!changesProtection && TouchesProtected(s, len))
        {
            OnProtected(EventArgs.Empty);
            return;
        }
        RecordUndo();
        var changed = new Dictionary<RichCharFormat, RichCharFormat>();
        for (int i = s; i < s + len; i++)
        {
            var f = _chars[i];
            if (!changed.TryGetValue(f, out var n)) changed[f] = n = change(f);
            _chars[i] = n;
        }
        // A selection up to the end takes the final paragraph mark along, as SelectAll does in RichEdit.
        if (s + len == _chars.Count) _endFormat = change(EndFormat);
        FormattingChanged();
        Modified = true;
    }

    /// <summary>Changes the paragraph format of the paragraphs the selection touches (EM_SETPARAFORMAT).</summary>
    private void ApplyParaFormat(Func<RichParaFormat, RichParaFormat> change)
    {
        if (TouchesProtected(SelectionStart, SelectionLength))
        {
            OnProtected(EventArgs.Empty);
            return;
        }
        var (first, last) = SelectedParagraphs();
        RecordUndo();
        for (int p = first; p <= last; p++) _paras[p] = change(_paras[p]);
        FormattingChanged();
        Modified = true;
    }

    /// <summary>A change of the whole text's formatting (the box's font or colour): not undoable, not a modification.</summary>
    private void ApplyToAll(Func<RichCharFormat, RichCharFormat> change)
    {
        var changed = new Dictionary<RichCharFormat, RichCharFormat>();
        for (int i = 0; i < _chars.Count; i++)
        {
            var f = _chars[i];
            if (!changed.TryGetValue(f, out var n)) changed[f] = n = change(f);
            _chars[i] = n;
        }
        _endFormat = change(EndFormat);
        if (_insertFormat != null) _insertFormat = change(_insertFormat);
        FormattingChanged();
    }

    private void FormattingChanged()
    {
        InvalidateLines();
        UpdateScrollBars();
        Invalidate();
        CheckContentsResized();
    }

    private void LayoutChanged()
    {
        InvalidateLines();
        UpdateScrollBars();
        Invalidate();
    }

    /// <summary>
    /// Would changing [<paramref name="start"/>, +<paramref name="length"/>) touch protected text? A range: if it has a
    /// protected character; an insertion point: if it is inside protected text (protected on both sides).
    /// </summary>
    private bool TouchesProtected(int start, int length)
    {
        if (length > 0)
        {
            for (int i = start; i < start + length && i < _chars.Count; i++) if (_chars[i].Protected) return true;
            return false;
        }
        return start > 0 && start < _chars.Count && _chars[start - 1].Protected && _chars[start].Protected;
    }

    internal override bool EditCore(int start, int length, string replacement)
    {
        if (!TouchesProtected(start, length)) return false;
        OnProtected(EventArgs.Empty);
        return true;
    }

    internal override void OnTextSplicing(int start, int length, string replacement)
    {
        var old = Text;
        int p = CountBreaks(old, 0, start);
        int removedBreaks = CountBreaks(old, start, start + length);
        var fragment = _pendingInsert is { } pending && pending.Text == replacement ? pending : null;
        var insert = fragment == null ? FormatForInsertAt(start) : null;

        _chars.RemoveRange(start, length);
        if (fragment != null) _chars.InsertRange(start, fragment.Chars);
        else _chars.InsertRange(start, Enumerable.Repeat(insert!, replacement.Length));

        // Paragraphs joined by removing their breaks keep the first one's format (as RichEdit does); new breaks split
        // a paragraph into ones of its format - or, from RTF, into its paragraphs, the original format going to the last.
        var head = _paras[p];
        _paras.RemoveRange(p + 1, removedBreaks);
        int addedBreaks = CountBreaks(replacement, 0, replacement.Length);
        if (addedBreaks > 0)
        {
            if (fragment != null) _paras.InsertRange(p, fragment.Paras.Take(addedBreaks));
            else _paras.InsertRange(p, Enumerable.Repeat(head, addedBreaks));
        }
        if (_chars.Count > 0) _endFormat = _chars[^1];
        TextStructureChanged();
    }

    internal override void OnTextReplaced()
    {
        var text = Text;
        if (_loading is { } fragment && fragment.Text == text)
        {
            _chars.Clear();
            _chars.AddRange(fragment.Chars);
            _paras = new List<RichParaFormat>(fragment.Paras);
            _endFormat = fragment.EndFormat;
        }
        else
        {
            // Plain text replacing everything drops the formatting, the paragraphs' too (WM_SETTEXT).
            var plain = PlainFragment(text);
            _chars.Clear();
            _chars.AddRange(plain.Chars);
            _paras = plain.Paras;
            _endFormat = plain.EndFormat;
        }
        _insertFormat = null;
        TextStructureChanged();
    }

    private void TextStructureChanged()
    {
        _paraStarts = null;
        _urls = null;
    }

    private static int CountBreaks(string s, int start, int end)
    {
        int n = 0;
        for (int i = start; i < end; i++) if (s[i] == '\n') n++;
        return n;
    }

    /// <summary>The start of every paragraph.</summary>
    private int[] ParaStarts
    {
        get
        {
            if (_paraStarts != null) return _paraStarts;
            var text = Text;
            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') starts.Add(i + 1);
            return _paraStarts = starts.ToArray();
        }
    }

    private int ParaOf(int index)
    {
        var starts = ParaStarts;
        int i = Array.BinarySearch(starts, index);
        return i >= 0 ? i : ~i - 1;
    }

    /// <summary>What undo keeps beside the text: the formats, run-length encoded.</summary>
    private sealed record Snapshot((int Count, RichCharFormat Format)[] Runs, RichParaFormat[] Paras, RichCharFormat End);

    internal override object? CaptureUndoExtra()
    {
        var runs = new List<(int, RichCharFormat)>();
        for (int i = 0; i < _chars.Count;)
        {
            int j = i + 1;
            while (j < _chars.Count && ReferenceEquals(_chars[j], _chars[i])) j++;
            runs.Add((j - i, _chars[i]));
            i = j;
        }
        return new Snapshot(runs.ToArray(), _paras.ToArray(), EndFormat);
    }

    internal override void RestoreUndoExtra(object? extra)
    {
        if (extra is not Snapshot s) return;
        _chars.Clear();
        foreach (var (count, format) in s.Runs) _chars.AddRange(Enumerable.Repeat(format, count));
        _paras = s.Paras.ToList();
        _endFormat = s.End;
        _insertFormat = null;
        TextStructureChanged();
    }

    private static string ActionName(UndoAction action) => SystemStrings.Get(action switch
    {
        UndoAction.Typing => "Typing",
        UndoAction.Delete => "Delete",
        UndoAction.DragDrop => "Drag and Drop",
        UndoAction.Cut => "Cut",
        UndoAction.Paste => "Paste",
        _ => "Unknown",
    });

    // --- RTF and files -------------------------------------------------------------

    /// <summary>Reads a whole document: its final "\par" is the text's final paragraph mark, not a line break.</summary>
    private RichFragment ReadDocument(string rtf, bool bytesAsLatin1)
    {
        var fragment = RtfReader.Read(rtf, RtfDefault, bytesAsLatin1);
        if (fragment.Text.EndsWith('\n'))
        {
            fragment.Text = fragment.Text[..^1];
            fragment.EndFormat = fragment.Chars[^1];
            fragment.Chars.RemoveAt(fragment.Chars.Count - 1);
            fragment.Paras.RemoveAt(fragment.Paras.Count - 1);
        }
        return fragment;
    }

    private void LoadFragment(RichFragment fragment)
    {
        if (fragment.Paras.Count == 0) fragment.Paras.Add(RichParaFormat.Default);
        _loading = fragment;
        try
        {
            ReplaceAllText(fragment.Text);
        }
        finally
        {
            _loading = null;
        }
        InvalidateLines();
        UpdateScrollBars();
        Invalidate();
    }

    private void InsertFragment(RichFragment fragment, UndoAction action, bool fromUser)
    {
        _pendingInsert = fragment;
        try
        {
            SetPendingUndoAction(action);
            ReplaceSelection(fragment.Text, recordUndo: true, fromUser);
        }
        finally
        {
            _pendingInsert = null;
        }
    }

    /// <summary>Plain text streamed in replaces everything, without formatting.</summary>
    private void LoadPlainText(string text) => LoadFragment(PlainFragment(NormalizeNewlines(text)));

    /// <summary>The system's ANSI code page, as RichEdit's SF_TEXT uses (UTF-8 where there is none).</summary>
    private static Encoding AnsiEncoding()
    {
        try
        {
            return CodePagesEncodingProvider.Instance.GetEncoding(0) ?? Encoding.UTF8;
        }
        catch (Exception)
        {
            return Encoding.UTF8;
        }
    }

    private static string DecodeAnsi(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        return AnsiEncoding().GetString(bytes);
    }

    internal override void CopyCore()
    {
        var data = new DataObject();
        data.SetData(DataFormats.Text, SelectedText.Replace("\n", "\r\n"));
        data.SetData(DataFormats.Rtf, SelectedRtf);
        Clipboard.SetDataObject(data);
    }

    internal override void PasteCore()
    {
        if (Clipboard.GetDataObject()?.GetData(DataFormats.Rtf) is string rtf && RtfReader.LooksLikeRtf(rtf))
        {
            InsertFragment(RtfReader.Read(rtf, RtfDefault), UndoAction.Paste, fromUser: true);
            return;
        }
        base.PasteCore();
    }

    // --- selection, scrolling, contents ------------------------------------------------

    internal override void OnSelectionMaybeChanged()
    {
        int s = SelectionStart, e = s + SelectionLength;
        if (s == _lastSelStart && e == _lastSelEnd) return;
        _lastSelStart = s;
        _lastSelEnd = e;
        if (_insertFormat != null && (s != e || s != _insertFormatAt)) _insertFormat = null;
        OnSelectionChanged(EventArgs.Empty);
    }

    internal override void OnScrolled(bool vertical)
    {
        if (vertical) OnVScroll(EventArgs.Empty);
        else OnHScroll(EventArgs.Empty);
    }

    /// <summary>
    /// ScrollToCaret as RichEdit's ITextRange.ScrollIntoView: as much text as possible - scrolled to the end, unless
    /// that hides the selection's start, which then goes to the top.
    /// </summary>
    internal override void ScrollToCaretCore()
    {
        var lines = GetLines();
        var tops = LineTops;
        int height = TextArea.Height;
        int bottomTop = lines.Count - 1;
        while (bottomTop > 0 && tops[lines.Count] - tops[bottomTop - 1] <= height) bottomTop--;
        int selectionLine = GetLineFromCharIndex(SelectionStart);
        SetTopLine(selectionLine >= bottomTop ? bottomTop : selectionLine);
        EnsureCaretVisible();
        Invalidate();
    }

    private int ContentHeight => LineTops[^1];

    private void CheckContentsResized()
    {
        int height = ContentHeight;
        if (height == _lastContentHeight) return;
        _lastContentHeight = height;
        var area = TextArea;
        OnContentsResized(new ContentsResizedEventArgs(new Rectangle(area.X, area.Y, area.Width, height + (BorderStyle == BorderStyle.Fixed3D ? 1 : 0))));
    }

    internal override void OnLayoutInvalidated() => _barsDirty = true;

    /// <summary>
    /// Scroll bars as RichEdit shows them: only while the text overflows (always, disabled when it does not, for the
    /// Forced* values); none on a single-line box; a horizontal one only without word wrap.
    /// </summary>
    internal override void UpdateScrollBars()
    {
        if (!_configuringBars)
        {
            _configuringBars = true;
            try
            {
                ConfigureScrollBars();
            }
            finally
            {
                _configuringBars = false;
            }
        }
        base.UpdateScrollBars();
        bool forced = ((int)_scrollBars & 0x10) != 0;
        if (forced && VerticalBar != null && ContentHeight <= TextArea.Height) VerticalBar.Enabled = false;
        if (forced && HorizontalBar != null && ContentWidth() <= TextArea.Width) HorizontalBar.Enabled = false;
        _barsDirty = false;
    }

    private void ConfigureScrollBars()
    {
        bool forced = ((int)_scrollBars & 0x10) != 0;
        bool allowVertical = Multiline && (_scrollBars & RichTextBoxScrollBars.Vertical) != 0;
        bool allowHorizontal = Multiline && (_scrollBars & RichTextBoxScrollBars.Horizontal) != 0 && WrapWidth < 0;
        for (int pass = 0; pass < 3; pass++)
        {
            bool vertical = allowVertical && (forced || ContentHeight > TextArea.Height);
            bool horizontal = allowHorizontal && (forced || ContentWidth() > TextArea.Width);
            bool changed = false;
            if (vertical && VerticalBar == null)
            {
                VerticalBar = new ScrollBarCore(this, vertical: true, (v, _) => SetTopLine(v));
                changed = true;
            }
            else if (!vertical && VerticalBar != null)
            {
                VerticalBar.Dispose();
                VerticalBar = null;
                SetTopLine(0);
                changed = true;
            }
            if (horizontal && HorizontalBar == null)
            {
                HorizontalBar = new ScrollBarCore(this, vertical: false, (v, _) => SetScrollX(v));
                changed = true;
            }
            else if (!horizontal && HorizontalBar != null)
            {
                HorizontalBar.Dispose();
                HorizontalBar = null;
                changed = true;
            }
            if (!changed) break;
            InvalidateLines();
        }
    }

    private int ContentWidth()
    {
        float widest = 0;
        foreach (var line in GetLines()) widest = Math.Max(widest, ContentWidthOf(line));
        return (int)Math.Ceiling(widest);
    }

    // --- layout --------------------------------------------------------------------

    internal override int TextInsetLeft => _showSelectionMargin ? SelectionMarginWidth : 0;

    internal override int WrapWidth => !Multiline ? -1 : _rightMargin > 0 ? (int)(_rightMargin * _zoom) : WordWrap ? TextArea.Width : -1;

    private Font FontOf(RichCharFormat f)
    {
        if (_fonts.TryGetValue(f, out var font)) return font;
        try
        {
            font = new Font(f.FontName, f.Size * _zoom, f.Style, GraphicsUnit.Point);
        }
        catch (ArgumentException)
        {
            font = new Font(Font.FontFamily, f.Size * _zoom, f.Style, GraphicsUnit.Point);
        }
        _fonts[f] = font;
        return font;
    }

    private void ClearFonts()
    {
        foreach (var font in _fonts.Values) font.Dispose();
        _fonts.Clear();
    }

    private float Zoomed(int twips) => Twips.ToPixels(twips) * _zoom;

    /// <summary>The format a paragraph's bullet is drawn with: its first character's.</summary>
    private RichCharFormat BulletFormat(int paragraph)
    {
        int start = ParaStarts[paragraph];
        return start < _chars.Count ? _chars[start] : (_chars.Count > 0 ? _chars[^1] : EndFormat);
    }

    /// <summary>
    /// Where the text of a line starts, from the text area's left: the first line at the start indent, the others
    /// moved by the hanging offset; a bulleted paragraph has its bullet at the start indent and all its text past it.
    /// </summary>
    private float LeftIndent(int paragraph, bool firstLine)
    {
        var pf = _paras[paragraph];
        if (pf.Bullet)
        {
            float bullet = TextLayout.MeasureWidth(FontOf(BulletFormat(paragraph)), "\u2022") + 6 * _zoom;
            return Math.Max(0, Zoomed(pf.IndentTwips) + Math.Max(Zoomed(pf.OffsetTwips), bullet));
        }
        return Math.Max(0, Zoomed(firstLine ? pf.IndentTwips : pf.IndentTwips + pf.OffsetTwips));
    }

    /// <summary>A line ends at a paragraph break or at RichEdit's soft break (U+000B), which starts no paragraph.</summary>
    internal override int NextLineBreak(string text, int from)
    {
        int i = text.AsSpan(from).IndexOfAny('\n', '\v');
        return i < 0 ? -1 : from + i;
    }

    internal override int LineWrapWidth(int lineStart, bool paragraphStart, int wrapWidth)
    {
        int p = ParaOf(lineStart);
        float available = wrapWidth - LeftIndent(p, lineStart == ParaStarts[p]) - Zoomed(_paras[p].RightIndentTwips);
        return Math.Max(1, (int)available);
    }

    internal override int LineOffsetX(LineInfo line)
    {
        int p = ParaOf(line.Start);
        var pf = _paras[p];
        float left = LeftIndent(p, line.Start == ParaStarts[p]);
        if (pf.Alignment == HorizontalAlignment.Left || pf.Justify) return (int)Math.Round(left);
        int wrap = WrapWidth > 0 ? WrapWidth : TextArea.Width;
        float available = wrap - left - Zoomed(pf.RightIndentTwips);
        var text = Text;
        int end = line.End;
        while (end > line.Start && text[end - 1] == ' ') end--;
        float free = Math.Max(0, available - MeasureRange(line.Start, end));
        return (int)Math.Round(left + (pf.Alignment == HorizontalAlignment.Center ? free / 2 : free));
    }

    internal override float ContentWidthOf(LineInfo line)
    {
        int p = ParaOf(line.Start);
        return LeftIndent(p, line.Start == ParaStarts[p]) + MeasureRange(line.Start, line.End);
    }

    /// <summary>
    /// The width of [<paramref name="start"/>, <paramref name="end"/>) of a line starting at <paramref name="start"/>:
    /// runs measured in their own fonts, tabs to the paragraph's stops (then every half inch) from the text area's left.
    /// </summary>
    internal override float MeasureRange(int start, int end)
    {
        if (end <= start) return 0;
        var text = Text;
        int p = ParaOf(start);
        float left = -1;
        float x = 0;
        int i = start;
        while (i < end)
        {
            if (text[i] == '\t')
            {
                if (left < 0) left = LeftIndent(p, start == ParaStarts[p]);
                x = NextTabStop(_paras[p], left + x) - left;
                i++;
                continue;
            }
            var f = _chars[i];
            int j = i + 1;
            while (j < end && text[j] != '\t' && ReferenceEquals(_chars[j], f)) j++;
            x += TextLayout.MeasureWidth(FontOf(f), text.Substring(i, j - i));
            i = j;
        }
        return x;
    }

    private float NextTabStop(RichParaFormat pf, float x)
    {
        foreach (var t in pf.TabsTwips)
        {
            float stop = Zoomed(t);
            if (stop > x + 0.5f) return stop;
        }
        float interval = Math.Max(1, Zoomed(DefaultTabTwips));
        return (float)(Math.Floor(x / interval + 1e-4) + 1) * interval;
    }

    /// <summary>The line's ascent (from its top to the shared baseline) and descent, over its runs and their offsets.</summary>
    private (float Top, float Bottom) LineMetrics(LineInfo line)
    {
        float top = 0, bottom = 0;
        void Add(RichCharFormat f)
        {
            var m = FontOf(f).SKFont.Metrics;
            float raise = Zoomed(f.OffsetTwips);
            top = Math.Max(top, -m.Ascent + raise);
            bottom = Math.Max(bottom, m.Descent + m.Leading - raise);
        }
        if (line.Length == 0)
        {
            Add(line.Start < _chars.Count ? _chars[line.Start] : (_chars.Count > 0 ? _chars[^1] : EndFormat));
        }
        else
        {
            RichCharFormat? last = null;
            for (int i = line.Start; i < line.End; i++)
            {
                if (ReferenceEquals(_chars[i], last)) continue;
                last = _chars[i];
                Add(last);
            }
        }
        return (top, bottom);
    }

    internal override int LineHeightOf(int lineIndex, LineInfo line)
    {
        var (top, bottom) = LineMetrics(line);
        return Math.Max(1, (int)Math.Ceiling(top + bottom));
    }

    // --- painting ------------------------------------------------------------------

    internal override void PaintLine(Graphics g, int lineIndex, LineInfo line, int x, int y, int height, LineSelection sel, Color textColor)
    {
        var text = Text;
        var (top, _) = LineMetrics(line);
        float baseline = y + top;
        int p = ParaOf(line.Start);
        var pf = _paras[p];
        bool enabled = Enabled;

        if (pf.Bullet && line.Start == ParaStarts[p])
        {
            var bf = BulletFormat(p);
            var font = FontOf(bf);
            int bx = x - LineOffsetX(line) + (int)Math.Round(Zoomed(pf.IndentTwips));
            int by = (int)Math.Round(baseline + font.SKFont.Metrics.Ascent - Zoomed(bf.OffsetTwips));
            TextRenderer.DrawText(g, "\u2022", font, new Rectangle(bx, by, int.MaxValue / 2, height), enabled ? bf.ForeColor : Theme.DisabledText, LineFlags);
        }

        // Backgrounds of the runs, then the selection over them, then the text.
        foreach (var (a, b) in Segments(line, sel))
        {
            var f = _chars[a];
            if (f.BackColor.IsEmpty) continue;
            float x0 = MeasureRange(line.Start, a), x1 = MeasureRange(line.Start, b);
            using var brush = new SolidBrush(f.BackColor);
            g.FillRectangle(brush, x + x0, y, x1 - x0, height);
        }

        int selStart = sel.Start, selEnd = sel.End;
        if (sel.Show && selEnd > line.Start && selStart <= line.End)
        {
            float x0 = MeasureRange(line.Start, Math.Max(selStart, line.Start));
            float x1 = MeasureRange(line.Start, Math.Min(selEnd, line.End));
            if (selEnd > line.End && line.EndsWithNewline) x1 += height / 3f;
            using var hl = new SolidBrush(sel.Focused ? Theme.Highlight : Theme.HighlightInactive);
            g.FillRectangle(hl, x + x0, y, Math.Max(1, x1 - x0), height);
        }

        foreach (var (a, b) in Segments(line, sel))
        {
            if (text[a] == '\t') continue;
            var f = _chars[a];
            bool selected = sel.Show && a >= selStart && a < selEnd;
            bool link = _detectUrls && UrlAt(a) != null;
            var font = FontOf(f);
            if (link && !font.Underline) font = FontOf(f with { Style = f.Style | FontStyle.Underline });
            var color = selected && sel.Focused ? Theme.HighlightText : !enabled ? Theme.DisabledText : link ? Theme.LinkText : f.ForeColor;
            float x0 = MeasureRange(line.Start, a);
            int ry = (int)Math.Round(baseline + font.SKFont.Metrics.Ascent - Zoomed(f.OffsetTwips));
            TextRenderer.DrawText(g, text.Substring(a, b - a), font, new Rectangle((int)Math.Round(x + x0), ry, int.MaxValue / 2, height), color, LineFlags);
        }
    }

    /// <summary>
    /// The line cut where anything drawn changes: the format, a tab (a segment of its own), the selection's ends, a link's ends.
    /// </summary>
    private IEnumerable<(int Start, int End)> Segments(LineInfo line, LineSelection sel)
    {
        var text = Text;
        int i = line.Start;
        while (i < line.End)
        {
            if (text[i] == '\t')
            {
                yield return (i, i + 1);
                i++;
                continue;
            }
            var f = _chars[i];
            var url = _detectUrls ? UrlAt(i) : null;
            int j = i + 1;
            while (j < line.End && text[j] != '\t' && ReferenceEquals(_chars[j], f)
                && !(sel.Show && (j == sel.Start || j == sel.End))
                && (_detectUrls ? UrlAt(j) == url : true))
            {
                j++;
            }
            yield return (i, j);
            i = j;
        }
    }

    // --- links and the selection margin -----------------------------------------------

    private static readonly Regex s_url = new(
        @"(?:\b(?:https?|ftp|file|mailto|news|nntp|telnet|gopher|wais|prospero|notes|onenote|tel|callto|sip|sips):|\bwww\.)[^\s<>""]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>The URLs of the text (DetectUrls), as RichEdit's auto-URL detection finds them.</summary>
    private List<(int Start, int Length)> Urls
    {
        get
        {
            if (_urls != null) return _urls;
            var urls = new List<(int, int)>();
            foreach (Match m in s_url.Matches(Text))
            {
                int length = m.Length;
                while (length > 0 && ".,;:!?'\")]}".Contains(m.Value[length - 1])) length--;
                if (length > m.Value.IndexOf(':') + 1 || m.Value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && length > 4)
                {
                    urls.Add((m.Index, length));
                }
            }
            return _urls = urls;
        }
    }

    private (int Start, int Length)? UrlAt(int index)
    {
        if (index < 0) return null;
        foreach (var u in Urls)
        {
            if (index >= u.Start && index < u.Start + u.Length) return u;
            if (u.Start > index) break;
        }
        return null;
    }

    /// <summary>The character drawn under <paramref name="pt"/>, or -1 (past the end of the line, between lines).</summary>
    private int CharAtPoint(Point pt)
    {
        var area = TextArea;
        var tops = LineTops;
        int lineIndex = LineAtY(pt.Y - area.Y);
        int y = pt.Y - area.Y + tops[Math.Min(TopLine, tops.Length - 1)];
        if (y < tops[lineIndex] || y >= tops[lineIndex + 1]) return -1;
        var line = GetLines()[lineIndex];
        float rx = pt.X - area.X + ScrollX - LineOffsetX(line);
        if (rx < 0) return -1;
        for (int n = 0; n < line.Length; n++)
        {
            if (rx < MeasureRange(line.Start, line.Start + n + 1)) return line.Start + n;
        }
        return -1;
    }

    internal override bool MouseDownCore(MouseEventArgs e)
    {
        var area = TextArea;
        if (_showSelectionMargin && e.X < area.X)
        {
            // The selection bar: a click selects the line, a double click the paragraph.
            var lines = GetLines();
            var line = lines[LineAtY(e.Y - area.Y)];
            if (e.Clicks >= 2)
            {
                int p = ParaOf(line.Start);
                int start = ParaStarts[p];
                int end = p + 1 < ParaStarts.Length ? ParaStarts[p + 1] : TextLength;
                SetSelectionFromMouse(start, end);
            }
            else
            {
                SetSelectionFromMouse(line.Start, line.End + (line.EndsWithNewline ? 1 : 0));
            }
            return true;
        }
        if (_detectUrls && UrlAt(CharAtPoint(e.Location)) is { } url)
        {
            // Mouse-down follows the link (EN_LINK on WM_LBUTTONDOWN, "as Outlook 2000").
            OnLinkClicked(new LinkClickedEventArgs(Text.Substring(url.Start, url.Length), url.Start, url.Length));
            return true;
        }
        return false;
    }
}
