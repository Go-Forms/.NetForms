using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

public enum MaskFormat
{
    IncludePrompt = 0x0001,
    IncludeLiterals = 0x0002,
    IncludePromptAndLiterals = IncludePrompt | IncludeLiterals,
    ExcludePromptAndLiterals = 0x000,
}

public enum InsertKeyMode
{
    Default,
    Insert,
    Overwrite,
}

public class MaskInputRejectedEventArgs : EventArgs
{
    public MaskInputRejectedEventArgs(int position, MaskedTextResultHint rejectionHint)
    {
        Position = position;
        RejectionHint = rejectionHint;
    }

    public int Position { get; }
    public MaskedTextResultHint RejectionHint { get; }
}

public delegate void MaskInputRejectedEventHandler(object? sender, MaskInputRejectedEventArgs e);

public class TypeValidationEventArgs : EventArgs
{
    public TypeValidationEventArgs(Type? validatingType, bool isValidInput, object? returnValue, string? message)
    {
        ValidatingType = validatingType;
        IsValidInput = isValidInput;
        ReturnValue = returnValue;
        Message = message;
    }

    public Type? ValidatingType { get; }
    public bool IsValidInput { get; }
    public object? ReturnValue { get; }
    public string? Message { get; }
    public bool Cancel { get; set; }
}

public delegate void TypeValidationEventHandler(object? sender, TypeValidationEventArgs e);

/// <summary>
/// A text box that takes input through a mask ("00/00/0000", "(999) 000-0000", "&gt;L&lt;??????"). The mask
/// logic is the BCL's <see cref="System.ComponentModel.MaskedTextProvider"/>, the one WinForms uses: what it
/// accepts, rejects and where the caret goes are the same. The box shows the display string (prompts in
/// the empty positions); <see cref="Text"/> is what <see cref="TextMaskFormat"/> asks for. Without a mask it
/// is a plain text box.
/// </summary>
[DefaultEvent("MaskInputRejected")]
[DefaultBindingProperty("Text")]
[DefaultProperty("Mask")]
public class MaskedTextBox : TextBoxBase
{
    // Members WinForms hides from the designer on this control (Browsable(false)); checked against the real
    // WinForms by AttributeDiffTests.

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool AcceptsTab
    {
        get => base.AcceptsTab;
        set => base.AcceptsTab = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new string[] Lines
    {
        get => base.Lines;
        set => base.Lines = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool Multiline
    {
        get => base.Multiline;
        set => base.Multiline = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new bool WordWrap
    {
        get => base.WordWrap;
        set => base.WordWrap = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? AcceptsTabChanged
    {
        add => base.AcceptsTabChanged += value;
        remove => base.AcceptsTabChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? MultilineChanged
    {
        add => base.MultilineChanged += value;
        remove => base.MultilineChanged -= value;
    }

    private MaskedTextProvider _provider;
    private bool _hidePromptOnLeave;
    private bool _beepOnError;
    private bool _rejectInputOnFirstFailure;
    private bool _useSystemPasswordChar;
    private InsertKeyMode _insertKeyMode = InsertKeyMode.Default;
    private bool _insertToggled;
    private MaskFormat _textMaskFormat = MaskFormat.IncludeLiterals;
    private MaskFormat _cutCopyMaskFormat = MaskFormat.IncludeLiterals;
    private HorizontalAlignment _textAlign = HorizontalAlignment.Left;
    private IFormatProvider? _formatProvider;

    public MaskedTextBox() : this(new MaskedTextProvider("<>", CultureInfo.CurrentCulture)) { }

    public MaskedTextBox(string mask) : this(new MaskedTextProvider(mask, CultureInfo.CurrentCulture)) { }

    public MaskedTextBox(MaskedTextProvider maskedTextProvider)
    {
        ArgumentNullException.ThrowIfNull(maskedTextProvider);
        _provider = maskedTextProvider;
        Show(userEdit: false, caret: 0);
    }

    /// <summary>"&lt;&gt;" is WinForms' marker of "no mask".</summary>
    private bool HasMask => _provider.Mask != "<>";

    // --- the mask -------------------------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Sets the string governing the input allowed for this control.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue("")]
    [Localizable(true)]
    public string Mask
    {
        get => HasMask ? _provider.Mask : string.Empty;
        set
        {
            value = string.IsNullOrEmpty(value) ? "<>" : value;
            if (value == _provider.Mask) return;
            string current = HasMask ? _provider.ToString(false, true) : base.Text;
            var next = new MaskedTextProvider(value, _provider.Culture, _provider.AllowPromptAsInput, _provider.PromptChar, _provider.PasswordChar, _provider.AsciiOnly)
            {
                ResetOnPrompt = _provider.ResetOnPrompt,
                ResetOnSpace = _provider.ResetOnSpace,
                SkipLiterals = _provider.SkipLiterals,
                IncludePrompt = _provider.IncludePrompt,
                IncludeLiterals = _provider.IncludeLiterals,
            };
            _provider = next;
            if (HasMask) SetProviderText(current); // the old text, through the new mask
            Show(userEdit: false, caret: 0);
            OnMaskChanged(EventArgs.Empty);
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public MaskedTextProvider? MaskedTextProvider => HasMask ? (MaskedTextProvider)_provider.Clone() : null;

    [Browsable(false)]
    public bool MaskCompleted => _provider.MaskCompleted;

    [Browsable(false)]
    public bool MaskFull => _provider.MaskFull;

    [Category("Appearance")]
    [Description("Indicates the character used as the placeholder.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [Localizable(true)]
    [DefaultValue('_')]
    public char PromptChar
    {
        get => _provider.PromptChar;
        set
        {
            if (value == _provider.PromptChar) return;
            _provider.PromptChar = value;
            Show(userEdit: false, caret: CaretIndex);
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the prompt character is valid as input.")]
    [DefaultValue(true)]
    public bool AllowPromptAsInput
    {
        get => _provider.AllowPromptAsInput;
        set
        {
            if (value == _provider.AllowPromptAsInput) return;
            RecreateProvider(allowPromptAsInput: value);
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether only ASCII characters are accepted as valid input.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue(false)]
    public bool AsciiOnly
    {
        get => _provider.AsciiOnly;
        set
        {
            if (value == _provider.AsciiOnly) return;
            RecreateProvider(asciiOnly: value);
        }
    }

    [Category("Behavior")]
    [Description("The culture that determines the value of the localizable mask language separators and placeholders.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    public CultureInfo Culture
    {
        get => _provider.Culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Equals(_provider.Culture)) return;
            RecreateProvider(culture: value);
        }
    }

    internal bool ShouldSerializeCulture() => !CultureInfo.CurrentCulture.Equals(Culture);

    private void RecreateProvider(CultureInfo? culture = null, bool? allowPromptAsInput = null, bool? asciiOnly = null)
    {
        string current = _provider.ToString(false, true);
        var next = new MaskedTextProvider(_provider.Mask, culture ?? _provider.Culture, allowPromptAsInput ?? _provider.AllowPromptAsInput,
            _provider.PromptChar, _provider.PasswordChar, asciiOnly ?? _provider.AsciiOnly)
        {
            ResetOnPrompt = _provider.ResetOnPrompt,
            ResetOnSpace = _provider.ResetOnSpace,
            SkipLiterals = _provider.SkipLiterals,
        };
        _provider = next;
        SetProviderText(current);
        Show(userEdit: false, caret: 0);
    }

    [Category("Behavior")]
    [Description("Indicates whether prompt characters are displayed when the control does not have focus.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue(false)]
    public bool HidePromptOnLeave
    {
        get => _hidePromptOnLeave;
        set
        {
            if (_hidePromptOnLeave == value) return;
            _hidePromptOnLeave = value;
            Show(userEdit: false, caret: CaretIndex);
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the control will beep when an invalid character is typed.")]
    [DefaultValue(false)]
    public bool BeepOnError
    {
        get => _beepOnError;
        set => _beepOnError = value;
    }

    [Category("Behavior")]
    [Description("If true, the input text is rejected whenever a character fails to comply with the mask; otherwise, characters in the text are processed one by one as individual inputs.")]
    [DefaultValue(false)]
    public bool RejectInputOnFirstFailure
    {
        get => _rejectInputOnFirstFailure;
        set => _rejectInputOnFirstFailure = value;
    }

    [Category("Behavior")]
    [Description("Specifies whether to reset and skip the current position if editable, when the input character has the same value as the prompt.")]
    [DefaultValue(true)]
    public bool ResetOnPrompt
    {
        get => _provider.ResetOnPrompt;
        set => _provider.ResetOnPrompt = value;
    }

    [Category("Behavior")]
    [Description("Specifies whether to reset and skip the current position if editable, when the input is the space character.")]
    [DefaultValue(true)]
    public bool ResetOnSpace
    {
        get => _provider.ResetOnSpace;
        set => _provider.ResetOnSpace = value;
    }

    [Category("Behavior")]
    [Description("Specifies whether to skip the current position if non-editable and the input character has the same value as the literal at that position.")]
    [DefaultValue(true)]
    public bool SkipLiterals
    {
        get => _provider.SkipLiterals;
        set => _provider.SkipLiterals = value;
    }

    [Category("Behavior")]
    [Description("Indicates whether the string returned from the Text property includes literals and/or prompt characters.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue(MaskFormat.IncludeLiterals)]
    public MaskFormat TextMaskFormat
    {
        get => _textMaskFormat;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MaskFormat));
            _textMaskFormat = value;
        }
    }

    [Category("Behavior")]
    [Description("Indicates whether the text to be copied to the clipboard includes literals and/or prompt characters.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue(MaskFormat.IncludeLiterals)]
    public MaskFormat CutCopyMaskFormat
    {
        get => _cutCopyMaskFormat;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(MaskFormat));
            _cutCopyMaskFormat = value;
        }
    }

    [Category("Behavior")]
    [Description("Indicates the character to display for password input.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue('\0')]
    public char PasswordChar
    {
        get => _provider.PasswordChar;
        set
        {
            if (value == _provider.PasswordChar) return;
            _provider.PasswordChar = value;
            Show(userEdit: false, caret: CaretIndex);
        }
    }

    [Category("Behavior")]
    [Description("Indicates if the text in the edit control should appear as the default password character.")]
    [RefreshProperties(RefreshProperties.Repaint)]
    [DefaultValue(false)]
    public bool UseSystemPasswordChar
    {
        get => _useSystemPasswordChar;
        set
        {
            if (_useSystemPasswordChar == value) return;
            _useSystemPasswordChar = value;
            Show(userEdit: false, caret: CaretIndex);
        }
    }

    [Category("Behavior")]
    [Description("Indicates the masked text box input character typing mode.")]
    [DefaultValue(InsertKeyMode.Default)]
    public InsertKeyMode InsertKeyMode
    {
        get => _insertKeyMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(InsertKeyMode));
            if (_insertKeyMode == value) return;
            bool was = IsOverwriteMode;
            _insertKeyMode = value;
            if (was != IsOverwriteMode) OnIsOverwriteModeChanged(EventArgs.Empty);
        }
    }

    /// <summary>Overwrite, rather than insert, the character at the caret (the Insert key toggles it in Default mode).</summary>
    [Browsable(false)]
    public bool IsOverwriteMode => _insertKeyMode switch
    {
        InsertKeyMode.Overwrite => true,
        InsertKeyMode.Insert => false,
        _ => _insertToggled,
    };

    [Localizable(true)]
    [Category("Appearance")]
    [DefaultValue(HorizontalAlignment.Left)]
    [Description("Indicates how the text should be aligned for edit controls.")]
    public HorizontalAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value) return;
            _textAlign = value;
            Invalidate();
            OnTextAlignChanged(EventArgs.Empty);
        }
    }

    [Browsable(false)]
    [DefaultValue(null)]
    public Type? ValidatingType { get; set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IFormatProvider? FormatProvider
    {
        get => _formatProvider;
        set => _formatProvider = value;
    }

    // --- text -------------------------------------------------------------------------------------------

    /// <summary>The text as <see cref="TextMaskFormat"/> says: with or without the literals and the prompts.</summary>
    [RefreshProperties(RefreshProperties.Repaint)]
    [Bindable(true)]
    [Localizable(true)]
    [DefaultValue("")]
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => HasMask ? _provider.ToString(true, (_textMaskFormat & MaskFormat.IncludePrompt) != 0, (_textMaskFormat & MaskFormat.IncludeLiterals) != 0, 0, _provider.Length) : base.Text;
        set
        {
            if (!HasMask)
            {
                base.Text = value;
                return;
            }
            SetProviderText(value ?? string.Empty);
            Show(userEdit: false, caret: 0);
        }
    }

    public override int TextLength => Text.Length;

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override int MaxLength
    {
        get => base.MaxLength;
        set { }
    }

    private void SetProviderText(string text)
    {
        _provider.Clear();
        if (_rejectInputOnFirstFailure)
        {
            if (!_provider.Set(text, out int position, out var hint)) OnMaskInputRejected(new MaskInputRejectedEventArgs(position, hint));
            return;
        }
        // One character at a time, the invalid ones rejected (and reported) - as WinForms does.
        int pos = 0;
        foreach (char c in text)
        {
            if (pos >= _provider.Length) break;
            if (_provider.Replace(c, pos, out int testPosition, out var hint)) pos = testPosition + 1;
            else OnMaskInputRejected(new MaskInputRejectedEventArgs(pos, hint));
        }
    }

    /// <summary>The display string: prompts where nothing is typed, unless the box is unfocused and HidePromptOnLeave.</summary>
    private string Display()
    {
        if (!HasMask) return base.Text;
        bool password = _useSystemPasswordChar || _provider.PasswordChar != '\0';
        if (_useSystemPasswordChar && _provider.PasswordChar == '\0')
        {
            var chars = _provider.ToDisplayString().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
                if (_provider.IsEditPosition(i) && _provider.IsAvailablePosition(i) == false) chars[i] = '●';
            return new string(chars);
        }
        bool includePrompt = Focused || !_hidePromptOnLeave;
        return _provider.ToString(!password, includePrompt, true, 0, _provider.Length);
    }

    private void Show(bool userEdit, int caret)
    {
        if (!HasMask) return;
        SetEditedText(Display(), caret, userEdit);
    }

    /// <summary>The user's edit, through the mask (decision 117): typing, pasting, Backspace and Delete.</summary>
    internal override bool EditCore(int start, int length, string replacement)
    {
        if (!HasMask) return false;
        int testPosition;
        MaskedTextResultHint hint;
        bool ok;
        if (replacement.Length == 0)
        {
            if (length == 0) return true;
            ok = _provider.RemoveAt(start, Math.Min(_provider.Length - 1, start + length - 1), out testPosition, out hint);
            testPosition = start;
        }
        else if (length > 0)
        {
            ok = _provider.Replace(replacement, start, Math.Min(_provider.Length - 1, start + length - 1), out testPosition, out hint);
            testPosition++;
        }
        else if (IsOverwriteMode || replacement.Length > 1)
        {
            ok = _provider.Replace(replacement, start, out testPosition, out hint);
            testPosition++;
        }
        else
        {
            ok = _provider.InsertAt(replacement, start, out testPosition, out hint);
            testPosition++;
        }
        if (!ok)
        {
            OnMaskInputRejected(new MaskInputRejectedEventArgs(testPosition, hint));
            return true;
        }
        Show(userEdit: true, caret: testPosition);
        return true;
    }

    // --- keys and focus --------------------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Insert && e.Modifiers == Keys.None && _insertKeyMode == InsertKeyMode.Default)
        {
            _insertToggled = !_insertToggled;
            OnIsOverwriteModeChanged(EventArgs.Empty);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (_hidePromptOnLeave) Show(userEdit: false, caret: CaretIndex);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        if (_hidePromptOnLeave) Show(userEdit: false, caret: CaretIndex);
    }

    protected override void OnValidating(CancelEventArgs e)
    {
        PerformTypeValidation(e);
        base.OnValidating(e);
    }

    /// <summary>The text parsed as <see cref="ValidatingType"/> (its static Parse), or null when there is no type or it does not parse.</summary>
    public object? ValidateText() => PerformTypeValidation(null);

    private object? PerformTypeValidation(CancelEventArgs? e)
    {
        if (ValidatingType == null) return null;
        object? value = null;
        string? message = null;
        bool valid;
        try
        {
            var text = _provider.ToString(false, true);
            var parse = ValidatingType.GetMethod("Parse", new[] { typeof(string), typeof(IFormatProvider) });
            value = parse != null
                ? parse.Invoke(null, new object?[] { text, _formatProvider ?? Culture })
                : ValidatingType.GetMethod("Parse", new[] { typeof(string) })?.Invoke(null, new object?[] { text });
            valid = value != null;
        }
        catch (System.Reflection.TargetInvocationException ex)
        {
            valid = false;
            message = ex.InnerException?.Message ?? ex.Message;
        }
        var args = new TypeValidationEventArgs(ValidatingType, valid, value, message);
        OnTypeValidationCompleted(args);
        if (e != null) e.Cancel = args.Cancel;
        return valid ? value : null;
    }

    // --- events -----------------------------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs when the input character or text does not comply with the mask specification.")]
    public event MaskInputRejectedEventHandler? MaskInputRejected;

    [Category("Focus")]
    [Description("Occurs when the ValidatingType object has completed parsing the input text.")]
    public event TypeValidationEventHandler? TypeValidationCompleted;

    [Category("Property Changed")]
    [Description("Occurs when the value of Mask property changes.")]
    public event EventHandler? MaskChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of IsOverwriteMode property changes.")]
    public event EventHandler? IsOverwriteModeChanged;

    [Category("Property Changed")]
    [Description("Occurs when the value of the TextAlign property changes")]
    public event EventHandler? TextAlignChanged;

    private void OnMaskInputRejected(MaskInputRejectedEventArgs e)
    {
        if (_beepOnError) Console.Beep();
        MaskInputRejected?.Invoke(this, e);
    }

    protected virtual void OnMaskChanged(EventArgs e) => MaskChanged?.Invoke(this, e);

    protected virtual void OnIsOverwriteModeChanged(EventArgs e) => IsOverwriteModeChanged?.Invoke(this, e);

    protected virtual void OnTextAlignChanged(EventArgs e) => TextAlignChanged?.Invoke(this, e);

    private void OnTypeValidationCompleted(TypeValidationEventArgs e) => TypeValidationCompleted?.Invoke(this, e);

    public override string ToString() => HasMask ? base.ToString().Split(", Text:")[0] + ", Text: " + Text : base.ToString();
}
