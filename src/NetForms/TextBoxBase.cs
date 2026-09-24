using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace System.Windows.Forms;

/// <summary>
/// The editing engine shared by TextBox, MaskedTextBox and RichTextBox: text storage,
/// selection, caret, line layout with word wrap, keyboard editing, clipboard, undo/redo and
/// scrolling. Everything is drawn by us; the platform only delivers key and text input.
/// </summary>
[DefaultEvent("TextChanged")]
public abstract class TextBoxBase : Control
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
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
    public new event EventHandler? BackgroundImageChanged
    {
        add => base.BackgroundImageChanged += value;
        remove => base.BackgroundImageChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    /// <summary>Inset of the text from inside the border; Win32's edit control uses one pixel.</summary>
    private const int TextPadding = 1;
    private const int CaretBlinkInterval = 530;

    private string _text = string.Empty;
    private int _selectionAnchor;
    private int _caret;
    private bool _multiline;
    private bool _readOnly;
    private bool _wordWrap = true;
    private bool _acceptsTab;
    private bool _hideSelection = true;
    private bool _modified;
    private int _maxLength = 32767;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private readonly List<UndoEntry> _undo = new();
    private readonly List<UndoEntry> _redo = new();
    private Timer? _caretTimer;
    private bool _caretVisible;
    private int _topLine;
    private int _scrollX;
    private bool _dragging;
    private int _lastClickIndex = -1;
    private List<LineInfo>? _lines;
    private int[]? _lineTops;
    private int _linesWidth = -1;
    internal ScrollBarCore? VerticalBar;
    internal ScrollBarCore? HorizontalBar;

    /// <summary>
    /// A snapshot for undo/redo. <paramref name="Extra"/> is what a derived box keeps beside the text
    /// (RichTextBox: the formatting); <paramref name="Action"/> names the edit (<see cref="UndoAction"/>).
    /// </summary>
    private readonly record struct UndoEntry(string Text, int SelectionStart, int SelectionLength, object? Extra, UndoAction Action);

    /// <summary>The edit an undo entry reverts - the ids of RichEdit's UNDONAMEID (EM_GETUNDONAME).</summary>
    internal enum UndoAction { Unknown = 0, Typing = 1, Delete = 2, DragDrop = 3, Cut = 4, Paste = 5 }

    private UndoAction _pendingAction;
    private int _typingEnd = -1;

    /// <summary>A visual line: a range of the text (trailing spaces included, newline excluded).</summary>
    internal readonly record struct LineInfo(int Start, int Length, bool EndsWithNewline)
    {
        public int End => Start + Length;
    }

    protected TextBoxBase()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        SetStyle(ControlStyles.ResizeRedraw, true);
        base.AutoSize = true;
    }

    protected override Size DefaultSize => new Size(100, PreferredHeight);

    protected override Cursor DefaultCursor => Cursors.IBeam;

    [Category("Appearance")]
    [Description("The background color of the component.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color BackColor
    {
        get => IsBackColorSet ? base.BackColor : (_readOnly ? SystemColors.Control : SystemColors.Window);
        set => base.BackColor = value;
    }

    [Category("Appearance")]
    [Description("The foreground color of this component, which is used to display text.")]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override Color ForeColor
    {
        get => base.ForeColor;
        set => base.ForeColor = value;
    }

    // --- events ---------------------------------------------------------------------

    [Category("Property Changed")]
    [Description("Occurs when the value of the AcceptsTab property changes.")]
    public event EventHandler? AcceptsTabChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the BorderStyle property changes.")]
    public event EventHandler? BorderStyleChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the HideSelection property changes.")]
    public event EventHandler? HideSelectionChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the Modified property changes.")]
    public event EventHandler? ModifiedChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the Multiline property changes.")]
    public event EventHandler? MultilineChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the ReadOnly property changes.")]
    public event EventHandler? ReadOnlyChanged;

    protected virtual void OnAcceptsTabChanged(EventArgs e) => AcceptsTabChanged?.Invoke(this, e);
    protected virtual void OnBorderStyleChanged(EventArgs e) => BorderStyleChanged?.Invoke(this, e);
    protected virtual void OnHideSelectionChanged(EventArgs e) => HideSelectionChanged?.Invoke(this, e);
    protected virtual void OnModifiedChanged(EventArgs e) => ModifiedChanged?.Invoke(this, e);
    protected virtual void OnMultilineChanged(EventArgs e) => MultilineChanged?.Invoke(this, e);
    protected virtual void OnReadOnlyChanged(EventArgs e) => ReadOnlyChanged?.Invoke(this, e);

    // --- text ----------------------------------------------------------------------

    [Category("Appearance")]
    [Description("The text associated with the control.")]
    [Localizable(true)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public override string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            // Kept even when single-line, as the Win32 edit control keeps what code puts in it
            // (Lines then has two entries); only typing cannot add a line break there.
            value = NormalizeNewlines(value);
            if (_text == value) return;
            ReplaceAllText(value);
        }
    }

    /// <summary>
    /// Replaces the whole text (already normalized) as setting <see cref="Text"/> does - caret to the start, undo
    /// cleared, not modified, TextChanged - even when it is the same text (RichTextBox reloading its formatting).
    /// </summary>
    internal void ReplaceAllText(string value)
    {
        _text = value;
        _caret = 0;
        _selectionAnchor = 0;
        SetTopLineCore(0);
        SetScrollXCore(0);
        InvalidateLines();
        OnTextReplaced();
        ClearUndo();
        _modified = false;
        OnTextChanged(EventArgs.Empty);
        OnSelectionMaybeChanged();
    }

    /// <summary>Line breaks are stored as "\r\n", two characters, so SelectionStart/Length index the text exactly as in WinForms.</summary>
    internal const string NewLine = "\r\n";

    /// <summary>
    /// The line break this box stores: "\r\n" for the edit control, "\n" for RichEdit, whose Text and
    /// character indices count a paragraph break as one character.
    /// </summary>
    internal virtual string LineBreak => NewLine;

    internal string NormalizeNewlines(string s)
    {
        if (s.IndexOf('\r') < 0 && s.IndexOf('\n') < 0) return s;
        var lf = s.Replace(NewLine, "\n").Replace('\r', '\n');
        return LineBreak == "\n" ? lf : lf.Replace("\n", LineBreak);
    }

    [Browsable(false)]
    public virtual int TextLength => _text.Length;

    [Category("Appearance")]
    [Description("The lines of text in a multiline edit, as an array of String values.")]
    [Localizable(true)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] Lines
    {
        get => _text.Length == 0 ? Array.Empty<string>() : _text.Split(LineBreak);
        set => Text = value == null ? string.Empty : string.Join(LineBreak, value);
    }

    [Category("Behavior")]
    [Description("Specifies the maximum number of characters that can be entered into the edit control.")]
    [DefaultValue(32767)]
    [Localizable(true)]
    public virtual int MaxLength
    {
        get => _maxLength;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _maxLength = value;
        }
    }

    [Category("Behavior")]
    [Description("Controls whether the text of the edit control can span more than one line.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public virtual bool Multiline
    {
        get => _multiline;
        set
        {
            if (_multiline == value) return;
            _multiline = value;
            if (!value && _text.Contains(LineBreak, StringComparison.Ordinal))
            {
                _text = _text.Replace(LineBreak, string.Empty);
                _caret = Math.Min(_caret, _text.Length);
                _selectionAnchor = Math.Min(_selectionAnchor, _text.Length);
                OnTextReplaced();
            }
            InvalidateLines();
            OnMultilineChanged(EventArgs.Empty);
            AdjustHeightToFont();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Indicates if lines are automatically word-wrapped for multiline edit controls.")]
    [DefaultValue(true)]
    [Localizable(true)]
    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            if (_wordWrap == value) return;
            _wordWrap = value;
            InvalidateLines();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Controls whether the text in the edit control can be changed or not.")]
    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value) return;
            _readOnly = value;
            OnReadOnlyChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Indicates if tab characters are accepted as input for multiline edit controls.")]
    [DefaultValue(false)]
    public bool AcceptsTab
    {
        get => _acceptsTab;
        set
        {
            if (_acceptsTab == value) return;
            _acceptsTab = value;
            OnAcceptsTabChanged(EventArgs.Empty);
        }
    }

    [Category("Behavior")]
    [Description("Indicates that the selection should be hidden when the edit control loses focus.")]
    [DefaultValue(true)]
    public bool HideSelection
    {
        get => _hideSelection;
        set
        {
            if (_hideSelection == value) return;
            _hideSelection = value;
            OnHideSelectionChanged(EventArgs.Empty);
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Indicates if the text in the edit control has been modified by the user.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Modified
    {
        get => _modified;
        set
        {
            if (_modified == value) return;
            _modified = value;
            OnModifiedChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [Description("Indicates whether the edit control should have a border.")]
    [DefaultValue(BorderStyle.Fixed3D)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value) return;
            _borderStyle = value;
            InvalidateLines();
            OnBorderStyleChanged(EventArgs.Empty);
            AdjustHeightToFont();
            Invalidate();
        }
    }

    [Category("Behavior")]
    [Description("Enables automatic resizing based on font size for single-line edit controls.")]
    [DefaultValue(true)]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set
        {
            base.AutoSize = value;
            AdjustHeightToFont();
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether shortcuts defined for the control are enabled.")]
    [DefaultValue(true)]
    public bool ShortcutsEnabled { get; set; } = true;

    [Category("Behavior")]
    [Description("Indicates if the edit control can undo the previous action.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool CanUndo => _undo.Count > 0;

    public virtual bool CanRedo => _redo.Count > 0;

    /// <summary>Single-line height for the current font: text plus 3px padding and the border.</summary>
    /// <summary>
    /// WinForms' formula (TextBoxBase.PreferredHeight): the font height plus a fixed 7 pixels for a
    /// bordered box, 3 without a border. Verified against real WinForms by the compat diff -
    /// deriving it from our own padding gave 26 where WinForms gives 23.
    /// </summary>
    [Category("Layout")]
    [Description("The preferred height of this control.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int PreferredHeight => Font.Height + (_borderStyle == BorderStyle.None ? 3 : 7);

    internal int BorderSize => _borderStyle == BorderStyle.None ? 0 : _borderStyle == BorderStyle.FixedSingle ? 1 : 2;

    // --- selection -----------------------------------------------------------------

    [Category("Appearance")]
    [Description("The beginning of the currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int SelectionStart
    {
        get => Math.Min(_caret, _selectionAnchor);
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Select(value, SelectionLength);
        }
    }

    [Category("Appearance")]
    [Description("The length of the currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual int SelectionLength
    {
        get => Math.Abs(_caret - _selectionAnchor);
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            Select(SelectionStart, value);
        }
    }

    [Category("Appearance")]
    [Description("The currently selected text.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual string SelectedText
    {
        get => _text.Substring(SelectionStart, SelectionLength);
        set => ReplaceSelection(value ?? string.Empty, recordUndo: true, fromUser: false);
    }

    public void Select(int start, int length)
    {
        start = Math.Clamp(start, 0, _text.Length);
        length = Math.Clamp(length, 0, _text.Length - start);
        _selectionAnchor = start;
        _caret = start + length;
        EnsureCaretVisible();
        Invalidate();
        OnSelectionMaybeChanged();
    }

    public void SelectAll() => Select(0, _text.Length);

    public void DeselectAll() => Select(_caret, 0);

    public void AppendText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        int end = _text.Length;
        _selectionAnchor = _caret = end;
        ReplaceSelection(text, recordUndo: false, fromUser: false);
        _selectionAnchor = _caret = _text.Length;
        EnsureCaretVisible();
        OnSelectionMaybeChanged();
    }

    public void Clear() => Text = string.Empty;

    // --- clipboard and undo --------------------------------------------------------

    public void Copy()
    {
        if (SelectionLength == 0) return;
        CopyCore();
    }

    /// <summary>Puts the (non-empty) selection on the clipboard; RichTextBox adds its RTF.</summary>
    internal virtual void CopyCore() => Clipboard.SetText(SelectedText);

    public void Cut()
    {
        if (SelectionLength == 0 || _readOnly) return;
        Copy();
        _pendingAction = UndoAction.Cut;
        ReplaceSelection(string.Empty, recordUndo: true, fromUser: true);
    }

    public void Paste()
    {
        if (_readOnly) return;
        PasteCore();
    }

    /// <summary>Pastes what the clipboard holds; RichTextBox prefers its RTF.</summary>
    internal virtual void PasteCore()
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;
        Paste(text);
    }

    public void Paste(string? text)
    {
        if (_readOnly || text == null) return;
        _pendingAction = UndoAction.Paste;
        ReplaceSelection(text, recordUndo: true, fromUser: true);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var e = Pop(_undo);
        Push(_redo, new UndoEntry(_text, SelectionStart, SelectionLength, CaptureUndoExtra(), e.Action));
        RestoreSnapshot(e);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var e = Pop(_redo);
        Push(_undo, new UndoEntry(_text, SelectionStart, SelectionLength, CaptureUndoExtra(), e.Action));
        RestoreSnapshot(e);
    }

    private void Push(List<UndoEntry> stack, UndoEntry e)
    {
        stack.Add(e);
        if (stack.Count > UndoLimit) stack.RemoveAt(0);
    }

    private static UndoEntry Pop(List<UndoEntry> stack)
    {
        var e = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return e;
    }

    /// <summary>How many steps undo keeps; RichEdit's default (EM_SETUNDOLIMIT) is 100.</summary>
    internal virtual int UndoLimit => int.MaxValue;

    private void RestoreSnapshot(UndoEntry e)
    {
        _text = e.Text;
        RestoreUndoExtra(e.Extra);
        _selectionAnchor = e.SelectionStart;
        _caret = e.SelectionStart + e.SelectionLength;
        _typingEnd = -1;
        InvalidateLines();
        Modified = true;
        OnTextChanged(EventArgs.Empty);
        EnsureCaretVisible();
        Invalidate();
        OnSelectionMaybeChanged();
    }

    public void ClearUndo()
    {
        _undo.Clear();
        _redo.Clear();
        _typingEnd = -1;
    }

    /// <summary>The action the next <see cref="Undo"/> (or <see cref="Redo"/>) reverts, <see cref="UndoAction.Unknown"/> without one.</summary>
    internal UndoAction PeekUndoAction(bool redo) => redo ? (_redo.Count > 0 ? _redo[^1].Action : UndoAction.Unknown) : (_undo.Count > 0 ? _undo[^1].Action : UndoAction.Unknown);

    /// <summary>Names the action of the next edit, for its undo step.</summary>
    internal void SetPendingUndoAction(UndoAction action) => _pendingAction = action;

    /// <summary>Records the current state as an undo step - for a change that is not a text edit (RichTextBox formatting).</summary>
    internal void RecordUndo()
    {
        Push(_undo, new UndoEntry(_text, SelectionStart, SelectionLength, CaptureUndoExtra(), UndoAction.Unknown));
        _redo.Clear();
        _typingEnd = -1;
    }

    /// <summary>What a derived box keeps beside the text for undo (RichTextBox: the formatting).</summary>
    internal virtual object? CaptureUndoExtra() => null;

    internal virtual void RestoreUndoExtra(object? extra) { }

    /// <summary>
    /// Whether consecutive typing is one undo step, as RichEdit does (until the caret moves elsewhere);
    /// the edit control's own undo keeps every keystroke apart here.
    /// </summary>
    internal virtual bool GroupsTyping => false;

    // --- editing core --------------------------------------------------------------

    /// <summary>Replace the selection with <paramref name="replacement"/>, honouring ReadOnly (for user edits), MaxLength and Multiline.</summary>
    internal void ReplaceSelection(string replacement, bool recordUndo, bool fromUser)
    {
        var action = _pendingAction;
        _pendingAction = UndoAction.Unknown;
        if (fromUser && _readOnly) return;
        if (fromUser && EditCore(SelectionStart, SelectionLength, replacement)) return;
        replacement = NormalizeNewlines(replacement);
        // The user's paste into a single-line box loses its line breaks; code's AppendText keeps them (as Win32).
        if (!_multiline && fromUser) replacement = replacement.Replace(LineBreak, string.Empty);
        replacement = FilterInput(replacement);

        int start = SelectionStart, length = SelectionLength;
        int room = _maxLength > 0 ? _maxLength - (_text.Length - length) : int.MaxValue;
        if (fromUser && room <= 0 && replacement.Length > 0) return;
        if (replacement.Length > room) replacement = replacement.Substring(0, Math.Max(0, room));

        if (recordUndo)
        {
            bool continuesTyping = GroupsTyping && action == UndoAction.Typing && length == 0 && start == _typingEnd
                && _undo.Count > 0 && _undo[^1].Action == UndoAction.Typing;
            if (!continuesTyping) Push(_undo, new UndoEntry(_text, start, length, CaptureUndoExtra(), action));
            _redo.Clear();
        }
        _typingEnd = action == UndoAction.Typing ? start + replacement.Length : -1;

        OnTextSplicing(start, length, replacement);
        _text = string.Concat(_text.AsSpan(0, start), replacement, _text.AsSpan(start + length));
        _caret = _selectionAnchor = start + replacement.Length;
        InvalidateLines();
        Modified = true;
        OnTextChanged(EventArgs.Empty);
        EnsureCaretVisible();
        Invalidate();
        OnSelectionMaybeChanged();
    }

    /// <summary>
    /// Called just before [<paramref name="start"/>, +<paramref name="length"/>) of the text is replaced by
    /// <paramref name="replacement"/> (the old text is still in place): RichTextBox moves its formatting along.
    /// </summary>
    internal virtual void OnTextSplicing(int start, int length, string replacement) { }

    /// <summary>Called after the whole text was replaced other than by an edit (Text, Multiline, a mask).</summary>
    internal virtual void OnTextReplaced() { }

    /// <summary>Called whenever the selection may have moved; RichTextBox raises SelectionChanged when it has.</summary>
    internal virtual void OnSelectionMaybeChanged() { }

    /// <summary>Hook for derived classes (CharacterCasing, masks).</summary>
    internal virtual string FilterInput(string text) => text;

    /// <summary>
    /// A box that owns its editing (MaskedTextBox) takes the user's edit - <paramref name="replacement"/> over
    /// [<paramref name="start"/>, +<paramref name="length"/>) of what is shown - and returns true.
    /// </summary>
    internal virtual bool EditCore(int start, int length, string replacement) => false;

    /// <summary>What the box shows, after an edit a derived class made: the caret, TextChanged, Modified.</summary>
    internal void SetEditedText(string shown, int caret, bool userEdit)
    {
        bool changed = _text != shown;
        _text = shown;
        _caret = _selectionAnchor = Math.Clamp(caret, 0, shown.Length);
        InvalidateLines();
        if (changed) OnTextReplaced();
        if (userEdit && changed) Modified = true;
        if (changed) OnTextChanged(EventArgs.Empty);
        EnsureCaretVisible();
        Invalidate();
        OnSelectionMaybeChanged();
    }

    internal int CaretIndex => _caret;

    /// <summary>The end of the selection that does not move with Shift+arrows.</summary>
    internal int SelectionAnchor => _selectionAnchor;

    private void DeleteRange(int start, int length)
    {
        if (length <= 0) return;
        _selectionAnchor = start;
        _caret = start + length;
        _pendingAction = UndoAction.Delete;
        ReplaceSelection(string.Empty, recordUndo: true, fromUser: true);
    }

    // --- layout --------------------------------------------------------------------

    internal Rectangle TextArea
    {
        get
        {
            int b = BorderSize;
            int left = b + TextPadding + TextInsetLeft;
            var r = new Rectangle(left, b + TextPadding, Math.Max(0, Width - left - b - TextPadding), Math.Max(0, Height - 2 * b - 2 * TextPadding));
            if (VerticalBar != null) r.Width = Math.Max(0, r.Width - ScrollBarCore.Thickness);
            if (HorizontalBar != null) r.Height = Math.Max(0, r.Height - ScrollBarCore.Thickness);
            return r;
        }
    }

    /// <summary>Extra room left of the text (RichTextBox.ShowSelectionMargin).</summary>
    internal virtual int TextInsetLeft => 0;

    internal int LineHeight => Font.Height;

    /// <summary>The height of one visual line; the edit control's lines all have the font's height.</summary>
    internal virtual int LineHeightOf(int lineIndex, LineInfo line) => LineHeight;

    /// <summary>How many lines fit the text area from the top line on (past the text, lines of the font's height).</summary>
    private int VisibleLines
    {
        get
        {
            var tops = LineTops;
            int count = tops.Length - 1, height = TextArea.Height, used = 0, n = 0;
            for (int i = _topLine; ; i++)
            {
                used += i < count ? tops[i + 1] - tops[i] : Math.Max(1, LineHeight);
                if (used > height) break;
                n++;
            }
            return Math.Max(1, n);
        }
    }

    /// <summary>Drops the line layout; the next query breaks the text again.</summary>
    internal void InvalidateLines()
    {
        _lines = null;
        _lineTops = null;
        OnLayoutInvalidated();
    }

    /// <summary>Called when the line layout is dropped (text, width, font, wrapping changed).</summary>
    internal virtual void OnLayoutInvalidated() { }

    internal List<LineInfo> GetLines()
    {
        int wrapWidth = WrapWidth;
        if (_lines != null && _linesWidth == wrapWidth) return _lines;
        _lines = BreakLines(wrapWidth);
        _lineTops = null;
        _linesWidth = wrapWidth;
        return _lines;
    }

    /// <summary>The width lines wrap at, or -1 for no wrapping.</summary>
    internal virtual int WrapWidth => _multiline && _wordWrap ? TextArea.Width : -1;

    /// <summary>
    /// The width available to the visual line starting at <paramref name="lineStart"/> (<paramref name="paragraphStart"/>:
    /// the first line of its paragraph) out of <paramref name="wrapWidth"/>; RichTextBox takes its indents off.
    /// </summary>
    internal virtual int LineWrapWidth(int lineStart, bool paragraphStart, int wrapWidth) => wrapWidth;

    /// <summary>The top of every visual line from the top of the text; the last entry is the text's height.</summary>
    internal int[] LineTops
    {
        get
        {
            var lines = GetLines();
            if (_lineTops != null) return _lineTops;
            var tops = new int[lines.Count + 1];
            for (int i = 0; i < lines.Count; i++) tops[i + 1] = tops[i] + LineHeightOf(i, lines[i]);
            return _lineTops = tops;
        }
    }

    /// <summary>
    /// Where the next forced line end at or after <paramref name="from"/> is, -1 for none: the line break; RichTextBox
    /// also ends a line at RichEdit's soft break (U+000B, RTF's \line), which is as long as its "\n".
    /// </summary>
    internal virtual int NextLineBreak(string text, int from) => text.IndexOf(LineBreak, from, StringComparison.Ordinal);

    internal int TopLine => _topLine;

    internal int ScrollX => _scrollX;

    private List<LineInfo> BreakLines(int wrapWidth)
    {
        var lines = new List<LineInfo>();
        var text = _text;
        var nlText = LineBreak;
        int pos = 0;
        while (true)
        {
            int nl = _multiline ? NextLineBreak(text, pos) : -1;
            int paraEnd = nl < 0 ? text.Length : nl;
            if (wrapWidth <= 0 || paraEnd == pos)
            {
                lines.Add(new LineInfo(pos, paraEnd - pos, nl >= 0));
            }
            else
            {
                int lineStart = pos;
                while (lineStart < paraEnd)
                {
                    int end = FitLine(text, lineStart, paraEnd, LineWrapWidth(lineStart, lineStart == pos, wrapWidth));
                    lines.Add(new LineInfo(lineStart, end - lineStart, nl >= 0 && end == paraEnd));
                    lineStart = end;
                }
            }
            if (nl < 0) break;
            pos = nl + nlText.Length;
            if (pos == text.Length)
            {
                lines.Add(new LineInfo(pos, 0, false));
                break;
            }
        }
        if (lines.Count == 0) lines.Add(new LineInfo(0, 0, false));
        return lines;
    }

    /// <summary>Greedy wrap: the longest prefix of [start, paraEnd) fitting the width, cut after a space when possible.</summary>
    private int FitLine(string text, int start, int paraEnd, int width)
    {
        if (MeasureRange(start, paraEnd) <= width) return paraEnd;
        // The longest prefix that fits, by bisection (widths grow with the prefix); at least one character.
        int lo = start + 1, hi = paraEnd - 1;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            if (MeasureRange(start, mid) <= width) lo = mid;
            else hi = mid - 1;
        }
        int space = lo - 1 >= start ? text.LastIndexOf(' ', lo - 1, lo - start) : -1;
        return space >= start ? space + 1 : lo;
    }

    internal virtual string DisplayText(ReadOnlySpan<char> text) => text.ToString();

    private float MeasureWidth(ReadOnlySpan<char> text) => text.Length == 0 ? 0 : TextLayout.MeasureWidth(Font, DisplayText(text));

    /// <summary>
    /// The width of [<paramref name="start"/>, <paramref name="end"/>) laid out from <paramref name="start"/>,
    /// which begins a visual line: the x of <paramref name="end"/> within its line.
    /// </summary>
    internal virtual float MeasureRange(int start, int end) => end <= start ? 0 : MeasureWidth(_text.AsSpan(start, end - start));

    /// <summary>The x of <paramref name="index"/> in visual line <paramref name="line"/>, from the text area's left, before scrolling.</summary>
    internal float XOf(LineInfo line, int index) => LineOffsetX(line) + MeasureRange(line.Start, Math.Clamp(index, line.Start, line.End));

    /// <summary>The width the content of a visual line takes, for the horizontal scroll range.</summary>
    internal virtual float ContentWidthOf(LineInfo line) => MeasureRange(line.Start, line.End);

    public virtual int GetLineFromCharIndex(int index)
    {
        var lines = GetLines();
        index = Math.Clamp(index, 0, _text.Length);
        int nlLength = LineBreak.Length;
        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            // An index inside the "\r\n" pair belongs to the line it terminates.
            int endInclusive = l.EndsWithNewline ? l.End + nlLength - 1 : l.End;
            if (index < endInclusive || (index == endInclusive && (i == lines.Count - 1 || l.EndsWithNewline))) return i;
            if (index == l.End && !l.EndsWithNewline && i == lines.Count - 1) return i;
        }
        return lines.Count - 1;
    }

    public int GetFirstCharIndexFromLine(int lineNumber)
    {
        var lines = GetLines();
        if (lineNumber < 0 || lineNumber >= lines.Count) return -1;
        return lines[lineNumber].Start;
    }

    public int GetFirstCharIndexOfCurrentLine() => GetFirstCharIndexFromLine(GetLineFromCharIndex(_caret));

    public virtual Point GetPositionFromCharIndex(int index)
    {
        var lines = GetLines();
        int line = GetLineFromCharIndex(index);
        var l = lines[line];
        var area = TextArea;
        var tops = LineTops;
        int x = area.X + (int)Math.Round(XOf(l, index)) - _scrollX;
        int y = area.Y + tops[line] - tops[Math.Min(_topLine, tops.Length - 1)];
        return new Point(x, y);
    }

    public virtual int GetCharIndexFromPosition(Point pt)
    {
        var lines = GetLines();
        var area = TextArea;
        int line = LineAtY(pt.Y - area.Y);
        var l = lines[line];
        float x = pt.X - area.X + _scrollX - LineOffsetX(l);
        if (x <= 0) return l.Start;
        // Nearest caret position: the boundary whose x is closest.
        float prev = 0;
        for (int n = 1; n <= l.Length; n++)
        {
            float w = MeasureRange(l.Start, l.Start + n);
            if (w >= x) return (x - prev) < (w - x) ? l.Start + n - 1 : l.Start + n;
            prev = w;
        }
        return l.End;
    }

    public virtual char GetCharFromPosition(Point pt)
    {
        int index = GetCharIndexFromPosition(pt);
        return index >= 0 && index < _text.Length ? _text[index] : '\0';
    }

    /// <summary>The visual line at <paramref name="y"/> pixels below the top of the text area (clamped to the text).</summary>
    internal int LineAtY(int y)
    {
        var tops = LineTops;
        int count = tops.Length - 1;
        int top = Math.Min(_topLine, count - 1);
        int line;
        if (y < 0) line = top + (int)Math.Floor(y / (double)Math.Max(1, LineHeight));
        else
        {
            int abs = tops[top] + y;
            line = top;
            while (line + 1 < count && tops[line + 1] <= abs) line++;
        }
        return Math.Clamp(line, 0, count - 1);
    }

    public void ScrollToCaret() => ScrollToCaretCore();

    /// <summary>The public ScrollToCaret; RichTextBox shows as much text as it can (ITextRange.ScrollIntoView).</summary>
    internal virtual void ScrollToCaretCore() => EnsureCaretVisible();

    /// <summary>Scrolls just enough to show the caret - after every edit and caret move.</summary>
    internal void EnsureCaretVisible()
    {
        var lines = GetLines();
        int line = GetLineFromCharIndex(_caret);
        var tops = LineTops;
        int top = _topLine;
        if (line < top) top = line;
        else
        {
            int height = TextArea.Height;
            while (top < line && tops[line + 1] - tops[top] > height) top++;
        }
        SetTopLineCore(Math.Clamp(top, 0, Math.Max(0, lines.Count - 1)));

        var l = lines[line];
        int caretX = (int)Math.Round(XOf(l, _caret));
        int width = TextArea.Width;
        if (width > 0)
        {
            int scrollX = _scrollX;
            if (caretX - scrollX > width - 2) scrollX = caretX - width + 2;
            if (caretX - scrollX < 0) scrollX = caretX;
            SetScrollXCore(Math.Max(0, scrollX));
        }
        UpdateScrollBars();
    }

    internal virtual void UpdateScrollBars()
    {
        if (VerticalBar != null)
        {
            var lines = GetLines();
            VerticalBar.Minimum = 0;
            VerticalBar.Maximum = Math.Max(0, lines.Count - 1);
            VerticalBar.LargeChange = VisibleLines;
            VerticalBar.SmallChange = 1;
            VerticalBar.Value = _topLine;
            VerticalBar.Enabled = Enabled;
            int b = BorderSize;
            VerticalBar.Bounds = new Rectangle(Width - b - ScrollBarCore.Thickness, b, ScrollBarCore.Thickness, Math.Max(0, Height - 2 * b - (HorizontalBar != null ? ScrollBarCore.Thickness : 0)));
        }
        if (HorizontalBar != null)
        {
            int widest = 0;
            foreach (var l in GetLines()) widest = Math.Max(widest, (int)Math.Ceiling(ContentWidthOf(l)));
            HorizontalBar.Minimum = 0;
            HorizontalBar.Maximum = Math.Max(0, widest + 8);
            HorizontalBar.LargeChange = Math.Max(1, TextArea.Width);
            HorizontalBar.SmallChange = 8;
            HorizontalBar.Value = _scrollX;
            HorizontalBar.Enabled = Enabled;
            int b = BorderSize;
            HorizontalBar.Bounds = new Rectangle(b, Height - b - ScrollBarCore.Thickness, Math.Max(0, Width - 2 * b - (VerticalBar != null ? ScrollBarCore.Thickness : 0)), ScrollBarCore.Thickness);
        }
    }

    internal void SetTopLine(int line)
    {
        var lines = GetLines();
        line = Math.Clamp(line, 0, Math.Max(0, lines.Count - 1));
        if (_topLine == line) return;
        SetTopLineCore(line);
        Invalidate();
    }

    internal void SetScrollX(int x)
    {
        x = Math.Max(0, x);
        if (_scrollX == x) return;
        SetScrollXCore(x);
        Invalidate();
    }

    private void SetTopLineCore(int line)
    {
        if (_topLine == line) return;
        _topLine = line;
        OnScrolled(vertical: true);
    }

    private void SetScrollXCore(int x)
    {
        if (_scrollX == x) return;
        _scrollX = x;
        OnScrolled(vertical: false);
    }

    /// <summary>Called when the text scrolled (RichTextBox raises VerticalBar/HorizontalBar).</summary>
    internal virtual void OnScrolled(bool vertical) { }

    private void AdjustHeightToFont()
    {
        if (AutoSize && !_multiline)
        {
            int h = PreferredHeight;
            if (Height != h) SetBounds(Left, Top, Width, h, BoundsSpecified.Height);
        }
    }

    /// <summary>Not layout auto-sizing: the fixed height is enforced here, in <see cref="SetBoundsCore"/>.</summary>
    internal override bool LayoutAutoSize => false;

    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        // Single-line text boxes keep the height their font dictates, as in WinForms.
        if (AutoSize && !_multiline) height = PreferredHeight;
        base.SetBoundsCore(x, y, width, height, specified);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        InvalidateLines();
        base.OnFontChanged(e);
        AdjustHeightToFont();
    }

    protected override void OnResize(EventArgs e)
    {
        InvalidateLines();
        base.OnResize(e);
        UpdateScrollBars();
    }

    // --- caret ---------------------------------------------------------------------

    private void StartCaret()
    {
        _caretTimer ??= new Timer { Interval = CaretBlinkInterval };
        _caretTimer.Tick -= CaretTick;
        _caretTimer.Tick += CaretTick;
        _caretVisible = true;
        _caretTimer.Start();
        Invalidate();
    }

    private void StopCaret()
    {
        _caretTimer?.Stop();
        _caretVisible = false;
        Invalidate();
    }

    private void CaretTick(object? sender, EventArgs e)
    {
        _caretVisible = !_caretVisible;
        Invalidate(CaretRectangle);
    }

    private void ResetCaretBlink()
    {
        if (_caretTimer != null && _caretTimer.Enabled)
        {
            _caretVisible = true;
            _caretTimer.Stop();
            _caretTimer.Start();
        }
    }

    private Rectangle CaretRectangle
    {
        get
        {
            var p = GetPositionFromCharIndex(_caret);
            int line = GetLineFromCharIndex(_caret);
            return new Rectangle(p.X, p.Y, 1, LineHeightOf(line, GetLines()[line]));
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        StartCaret();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        StopCaret();
        _dragging = false;
        base.OnLostFocus(e);
    }

    // --- mouse ---------------------------------------------------------------------

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (CanFocus) Focus();
            if (VerticalBar != null && VerticalBar.Bounds.Contains(e.Location)) { VerticalBar.MouseDown(e.Location); base.OnMouseDown(e); return; }
            if (HorizontalBar != null && HorizontalBar.Bounds.Contains(e.Location)) { HorizontalBar.MouseDown(e.Location); base.OnMouseDown(e); return; }
            if (MouseDownCore(e))
            {
                ResetCaretBlink();
                Invalidate();
                base.OnMouseDown(e);
                OnSelectionMaybeChanged();
                return;
            }

            int index = GetCharIndexFromPosition(e.Location);
            if (e.Clicks >= 2 && index == _lastClickIndex)
            {
                SelectWordAt(index);
            }
            else
            {
                if ((Control.ModifierKeys & Keys.Shift) != 0) _caret = index;
                else _caret = _selectionAnchor = index;
                _dragging = true;
            }
            _lastClickIndex = index;
            ResetCaretBlink();
            Invalidate();
        }
        base.OnMouseDown(e);
        OnSelectionMaybeChanged();
    }

    /// <summary>
    /// A left press in the text a derived box handles itself (RichTextBox: a link, the selection margin);
    /// true skips placing the caret.
    /// </summary>
    internal virtual bool MouseDownCore(MouseEventArgs e) => false;

    /// <summary>Selects [<paramref name="start"/>, <paramref name="end"/>) with the caret at <paramref name="end"/>, as a mouse gesture does.</summary>
    internal void SetSelectionFromMouse(int start, int end)
    {
        _selectionAnchor = Math.Clamp(start, 0, _text.Length);
        _caret = Math.Clamp(end, 0, _text.Length);
        _lastClickIndex = -1;
        EnsureCaretVisible();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        VerticalBar?.MouseMove(e.Location, (e.Button & MouseButtons.Left) != 0);
        HorizontalBar?.MouseMove(e.Location, (e.Button & MouseButtons.Left) != 0);
        if (_dragging && (e.Button & MouseButtons.Left) != 0)
        {
            int index = GetCharIndexFromPosition(e.Location);
            if (index != _caret)
            {
                _caret = index;
                EnsureCaretVisible();
                Invalidate();
                OnSelectionMaybeChanged();
            }
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            VerticalBar?.MouseUp(e.Location);
            HorizontalBar?.MouseUp(e.Location);
            _dragging = false;
        }
        base.OnMouseUp(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        VerticalBar?.MouseLeave();
        HorizontalBar?.MouseLeave();
        _mouseOver = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_multiline)
        {
            int notches = e.Delta / 120;
            if (notches == 0) notches = Math.Sign(e.Delta);
            SetTopLine(_topLine - notches * 3);
            UpdateScrollBars();
        }
        base.OnMouseWheel(e);
    }

    private void SelectWordAt(int index)
    {
        var (start, end) = WordBoundsAt(index);
        _selectionAnchor = start;
        _caret = end;
    }

    private (int start, int end) WordBoundsAt(int index)
    {
        if (_text.Length == 0) return (0, 0);
        index = Math.Clamp(index, 0, _text.Length);
        int start = index, end = index;
        bool IsWord(char c) => char.IsLetterOrDigit(c) || c == '_';
        if (index < _text.Length && IsWord(_text[index]))
        {
            while (start > 0 && IsWord(_text[start - 1])) start--;
            while (end < _text.Length && IsWord(_text[end])) end++;
        }
        else if (index > 0 && IsWord(_text[index - 1]))
        {
            while (start > 0 && IsWord(_text[start - 1])) start--;
        }
        else if (index < _text.Length)
        {
            end = index + 1;
        }
        return (start, end);
    }

    private int NextWordStart(int index)
    {
        int i = index;
        while (i < _text.Length && !char.IsWhiteSpace(_text[i])) i++;
        while (i < _text.Length && char.IsWhiteSpace(_text[i])) i++;
        return i;
    }

    private int PreviousWordStart(int index)
    {
        int i = index;
        while (i > 0 && char.IsWhiteSpace(_text[i - 1])) i--;
        while (i > 0 && !char.IsWhiteSpace(_text[i - 1])) i--;
        return i;
    }

    // --- keyboard ------------------------------------------------------------------

    protected override bool IsInputKey(Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        if (code is Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Back or Keys.Delete or Keys.Insert) return true;
        if (_multiline && code is Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown) return true;
        if (code == Keys.Return && _multiline && AcceptsReturnCore) return true;
        if (code == Keys.Tab && _acceptsTab && _multiline && (keyData & Keys.Control) == 0) return true;
        return base.IsInputKey(keyData);
    }

    /// <summary>TextBox.AcceptsReturn; RichTextBox always accepts Return.</summary>
    internal virtual bool AcceptsReturnCore => true;

    /// <summary>Step one "character": a "\r\n" pair counts as one.</summary>
    private int StepRight(int index) => index + 1 < _text.Length && _text[index] == '\r' && _text[index + 1] == '\n' ? index + 2 : index + 1;

    private int StepLeft(int index) => index >= 2 && _text[index - 1] == '\n' && _text[index - 2] == '\r' ? index - 2 : index - 1;

    private void MoveCaret(int index, bool extend)
    {
        index = Math.Clamp(index, 0, _text.Length);
        // Never leave the caret between '\r' and '\n'.
        if (index > 0 && index < _text.Length && _text[index] == '\n' && _text[index - 1] == '\r') index++;
        _caret = index;
        if (!extend) _selectionAnchor = index;
        ResetCaretBlink();
        EnsureCaretVisible();
        Invalidate();
        OnSelectionMaybeChanged();
    }

    private int LineMove(int delta)
    {
        var lines = GetLines();
        int line = GetLineFromCharIndex(_caret);
        int target = Math.Clamp(line + delta, 0, lines.Count - 1);
        if (target == line) return delta < 0 ? 0 : _text.Length;
        float x = XOf(lines[line], _caret);
        var to = lines[target];
        float offset = LineOffsetX(to);
        if (x <= offset) return to.Start;
        for (int n = 1; n <= to.Length; n++)
        {
            float w = offset + MeasureRange(to.Start, to.Start + n);
            if (w >= x)
            {
                float prev = offset + MeasureRange(to.Start, to.Start + n - 1);
                return (x - prev) < (w - x) ? to.Start + n - 1 : to.Start + n;
            }
        }
        return to.End;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        bool shift = e.Shift, ctrl = e.Control;
        switch (e.KeyCode)
        {
            case Keys.Left:
                MoveCaret(ctrl ? PreviousWordStart(_caret) : (!shift && SelectionLength > 0 ? SelectionStart : StepLeft(_caret)), shift);
                e.Handled = true;
                break;
            case Keys.Right:
                MoveCaret(ctrl ? NextWordStart(_caret) : (!shift && SelectionLength > 0 ? SelectionStart + SelectionLength : StepRight(_caret)), shift);
                e.Handled = true;
                break;
            case Keys.Up when _multiline:
                MoveCaret(LineMove(-1), shift);
                e.Handled = true;
                break;
            case Keys.Down when _multiline:
                MoveCaret(LineMove(1), shift);
                e.Handled = true;
                break;
            case Keys.PageUp when _multiline:
                MoveCaret(LineMove(-VisibleLines), shift);
                e.Handled = true;
                break;
            case Keys.PageDown when _multiline:
                MoveCaret(LineMove(VisibleLines), shift);
                e.Handled = true;
                break;
            case Keys.Home:
                MoveCaret(ctrl ? 0 : GetFirstCharIndexOfCurrentLine(), shift);
                e.Handled = true;
                break;
            case Keys.End:
                {
                    var l = GetLines()[GetLineFromCharIndex(_caret)];
                    MoveCaret(ctrl ? _text.Length : l.End, shift);
                    e.Handled = true;
                    break;
                }
            case Keys.Back:
                if (SelectionLength > 0) ReplaceSelection(string.Empty, true, true);
                else if (_caret > 0)
                {
                    int start = ctrl ? PreviousWordStart(_caret) : StepLeft(_caret);
                    DeleteRange(start, _caret - start);
                }
                e.Handled = true;
                break;
            case Keys.Delete:
                if (shift) { Cut(); }
                else if (SelectionLength > 0) ReplaceSelection(string.Empty, true, true);
                else if (_caret < _text.Length)
                {
                    int end = ctrl ? NextWordStart(_caret) : StepRight(_caret);
                    DeleteRange(_caret, end - _caret);
                }
                e.Handled = true;
                break;
            case Keys.Insert:
                if (ctrl) { Copy(); e.Handled = true; }
                else if (shift) { Paste(); e.Handled = true; }
                break;
            case Keys.A when ctrl && ShortcutsEnabled:
                SelectAll();
                e.Handled = true;
                break;
            case Keys.C when ctrl && ShortcutsEnabled:
                Copy();
                e.Handled = true;
                break;
            case Keys.X when ctrl && ShortcutsEnabled:
                Cut();
                e.Handled = true;
                break;
            case Keys.V when ctrl && ShortcutsEnabled:
                Paste();
                e.Handled = true;
                break;
            case Keys.Z when ctrl && ShortcutsEnabled:
                if (shift) Redo(); else Undo();
                e.Handled = true;
                break;
            case Keys.Y when ctrl && ShortcutsEnabled:
                Redo();
                e.Handled = true;
                break;
            case Keys.Return when _multiline && AcceptsReturnCore:
                _pendingAction = UndoAction.Typing;
                ReplaceSelection(LineBreak, true, true);
                e.Handled = true;
                break;
            case Keys.Tab when _multiline && _acceptsTab && !ctrl:
                _pendingAction = UndoAction.Typing;
                ReplaceSelection("\t", true, true);
                e.Handled = true;
                break;
        }
        // Only Return and Tab can also arrive as text input; suppress that echo, nothing else.
        if (e.Handled && e.KeyCode is Keys.Return or Keys.Tab) e.SuppressKeyPress = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled) return;
        char c = e.KeyChar;
        if (c < ' ' && c != '\t') return; // control characters arrive via KeyDown
        if (c == '\t' && !(_multiline && _acceptsTab)) return;
        _pendingAction = UndoAction.Typing;
        ReplaceSelection(c.ToString(), true, true);
        e.Handled = true;
    }

    protected override bool ProcessKeyEventArgs(ref Message m)
    {
        // Characters typed while a handled KeyDown asked to suppress them (Ctrl+shortcuts, Enter) must not be inserted.
        if (m.Msg == Message.WM_CHAR && _suppressNextChar)
        {
            _suppressNextChar = false;
            char ch = (char)m.WParam;
            if (ch is '\r' or '\n' or '\t') return true;
        }
        if (m.Msg == Message.WM_KEYDOWN)
        {
            var e = new KeyEventArgs((Keys)m.WParam | ModifierKeys);
            OnKeyDown(e);
            _suppressNextChar = e.SuppressKeyPress;
            return e.Handled;
        }
        return base.ProcessKeyEventArgs(ref m);
    }

    private bool _suppressNextChar;

    // --- painting ------------------------------------------------------------------

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(Enabled ? BackColor : SystemColors.Control);
        pevent.Graphics.FillRectangle(brush, pevent.ClipRectangle);
    }

    internal virtual TextFormatFlags LineFlags => TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.NoClipping;

    internal virtual int LineOffsetX(LineInfo line) => 0;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var area = TextArea;
        var lines = GetLines();
        var tops = LineTops;
        bool focused = Focused;
        var selection = new LineSelection(SelectionStart, SelectionStart + SelectionLength, SelectionLength > 0 && (focused || !_hideSelection), focused);
        var textColor = Enabled ? ForeColor : Theme.DisabledText;

        var state = g.Save();
        int b = BorderSize;
        g.IntersectClip(new Rectangle(b, b, Math.Max(0, Width - 2 * b - (VerticalBar != null ? ScrollBarCore.Thickness : 0)), Math.Max(0, Height - 2 * b - (HorizontalBar != null ? ScrollBarCore.Thickness : 0))));

        int top = Math.Min(_topLine, lines.Count - 1);
        for (int lineIndex = top; lineIndex < lines.Count; lineIndex++)
        {
            int y = area.Y + tops[lineIndex] - tops[top];
            if (y >= area.Bottom + LineHeight) break;
            var line = lines[lineIndex];
            int x = area.X - _scrollX + LineOffsetX(line);
            PaintLine(g, lineIndex, line, x, y, tops[lineIndex + 1] - tops[lineIndex], selection, textColor);
        }

        if (focused && _caretVisible)
        {
            var caret = CaretRectangle;
            using var pen = new Pen(ForeColor);
            g.DrawLine(pen, caret.X, caret.Y, caret.X, caret.Bottom - 1);
        }
        g.Restore(state);

        VerticalBar?.Paint(g);
        HorizontalBar?.Paint(g);
        if (VerticalBar != null && HorizontalBar != null)
        {
            using var corner = new SolidBrush(SystemColors.Control);
            g.FillRectangle(corner, new Rectangle(VerticalBar.Bounds.X, HorizontalBar.Bounds.Y, ScrollBarCore.Thickness, ScrollBarCore.Thickness));
        }

        PaintBorder(g);
        base.OnPaint(e);
    }

    /// <summary>The selection as painting sees it: [Start, End), whether it shows, and whether the box has focus.</summary>
    internal readonly record struct LineSelection(int Start, int End, bool Show, bool Focused);

    /// <summary>
    /// Paints visual line <paramref name="line"/> with its text origin at (<paramref name="x"/>, <paramref name="y"/>):
    /// the selection highlight, then the text, the selected part in the highlight colour.
    /// </summary>
    internal virtual void PaintLine(Graphics g, int lineIndex, LineInfo line, int x, int y, int height, LineSelection sel, Color textColor)
    {
        var display = DisplayText(_text.AsSpan(line.Start, line.Length));
        int selStart = sel.Start, selEnd = sel.End;

        if (sel.Show && selEnd > line.Start && selStart <= line.End)
        {
            int s = Math.Max(selStart, line.Start);
            int t = Math.Min(selEnd, line.End);
            float x0 = MeasureRange(line.Start, s);
            float x1 = MeasureRange(line.Start, t);
            if (selEnd > line.End && line.EndsWithNewline) x1 += height / 3f;
            using var hl = new SolidBrush(sel.Focused ? Theme.Highlight : Theme.HighlightInactive);
            g.FillRectangle(hl, x + x0, y, Math.Max(1, x1 - x0), height);
        }

        if (display.Length == 0) return;
        if (sel.Show && selEnd > line.Start && selStart < line.End)
        {
            // Draw the unselected and selected parts in their own colours.
            int s = Math.Max(selStart, line.Start) - line.Start;
            int t = Math.Min(selEnd, line.End) - line.Start;
            DrawSegment(g, display, 0, s, x, y, height, textColor);
            DrawSegment(g, display, s, t, x + MeasureRange(line.Start, line.Start + s), y, height, sel.Focused ? Theme.HighlightText : textColor);
            DrawSegment(g, display, t, display.Length, x + MeasureRange(line.Start, line.Start + t), y, height, textColor);
        }
        else
        {
            TextRenderer.DrawText(g, display, Font, new Rectangle(x, y, int.MaxValue / 2, height), textColor, LineFlags);
        }
    }

    private void DrawSegment(Graphics g, string display, int from, int to, float x, int y, int height, Color color)
    {
        if (to <= from) return;
        TextRenderer.DrawText(g, display.Substring(from, to - from), Font, new Rectangle((int)Math.Round(x), y, int.MaxValue / 2, height), color, LineFlags);
    }

    private void PaintBorder(Graphics g)
    {
        if (_borderStyle == BorderStyle.None) return;
        var rect = ClientRectangle;
        if (_borderStyle == BorderStyle.FixedSingle)
        {
            using var pen = new Pen(SystemColors.WindowFrame);
            g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            return;
        }
        // Fixed3D in the modern theme: a flat 1px frame that turns accent-coloured on focus, inside a 1px window-colour ring.
        var color = !Enabled ? Theme.ButtonBorderDisabled : Focused ? Theme.WindowBorderFocused : MouseIsOverControl ? Theme.CheckBorder : Theme.WindowBorder;
        using (var pen = new Pen(color)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
        using (var inner = new Pen(Enabled ? BackColor : SystemColors.Control)) g.DrawRectangle(inner, rect.X + 1, rect.Y + 1, rect.Width - 3, rect.Height - 3);
    }

    private bool _mouseOver;

    private bool MouseIsOverControl => _mouseOver;

    protected override void OnMouseEnter(EventArgs e)
    {
        _mouseOver = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        if (VerticalBar != null) VerticalBar.Enabled = Enabled;
        if (HorizontalBar != null) HorizontalBar.Enabled = Enabled;
        base.OnEnabledChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _caretTimer?.Dispose();
            VerticalBar?.Dispose();
            HorizontalBar?.Dispose();
        }
        base.Dispose(disposing);
    }

    public override string ToString()
    {
        var t = Text;
        if (t.Length > 40) t = t.Substring(0, 40) + "...";
        return base.ToString() + ", Text: " + t;
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Layout")]
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Padding Padding { get => base.Padding; set => base.Padding = value; }

    [Category("Layout")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }

    [Category("Appearance")]
    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event PaintEventHandler? Paint
    {
        add => base.Paint += value;
        remove => base.Paint -= value;
    }
}
