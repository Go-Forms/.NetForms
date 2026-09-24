using System;
using System.Collections.Generic;
using System.Linq;

namespace System.Windows.Forms;

/// <summary>
/// The contents of a task dialog. A page is "bound" while a <see cref="TaskDialog"/> shows it: then the
/// texts, icons, progress and enabled/checked states update the dialog live, and the structure (buttons,
/// radio buttons, which parts exist) cannot change.
/// </summary>
public class TaskDialogPage
{
    private TaskDialogButtonCollection _buttons = new();
    private TaskDialogRadioButtonCollection _radioButtons = new();
    private TaskDialogVerificationCheckBox? _verification = new();
    private TaskDialogExpander? _expander = new();
    private TaskDialogFootnote? _footnote = new();
    private TaskDialogProgressBar? _progressBar = new(TaskDialogProgressBarState.None);
    private TaskDialogIcon? _icon;
    private string? _caption;
    private string? _heading;
    private string? _text;
    private bool _allowCancel;
    private bool _allowMinimize;
    private bool _rightToLeftLayout;
    private bool _sizeToContent;
    private bool _enableLinks;

    public event EventHandler? Created;

    public event EventHandler? Destroyed;

    public event EventHandler? HelpRequest;

    public event EventHandler<TaskDialogLinkClickedEventArgs>? LinkClicked;

    public TaskDialogPage()
    {
    }

    public TaskDialogButtonCollection Buttons
    {
        get => _buttons;
        set
        {
            DenyIfBound();
            _buttons = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public TaskDialogButton? DefaultButton { get; set; }

    public TaskDialogRadioButtonCollection RadioButtons
    {
        get => _radioButtons;
        set
        {
            DenyIfBound();
            _radioButtons = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public TaskDialogVerificationCheckBox? Verification
    {
        get => _verification;
        set
        {
            DenyIfBound();
            _verification = value;
        }
    }

    public TaskDialogExpander? Expander
    {
        get => _expander;
        set
        {
            DenyIfBound();
            _expander = value;
        }
    }

    public TaskDialogFootnote? Footnote
    {
        get => _footnote;
        set
        {
            DenyIfBound();
            _footnote = value;
        }
    }

    public TaskDialogProgressBar? ProgressBar
    {
        get => _progressBar;
        set
        {
            DenyIfBound();
            _progressBar = value;
        }
    }

    /// <summary>The window title; the application's name when null.</summary>
    public string? Caption
    {
        get => _caption;
        set
        {
            _caption = value;
            BoundDialog?.OnPageChanged();
        }
    }

    /// <summary>The main instruction, in large type above the text.</summary>
    public string? Heading
    {
        get => _heading;
        set
        {
            _heading = value;
            BoundDialog?.OnPageChanged();
        }
    }

    public string? Text
    {
        get => _text;
        set
        {
            _text = value;
            BoundDialog?.OnPageChanged();
        }
    }

    public TaskDialogIcon? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            BoundDialog?.OnPageChanged();
        }
    }

    /// <summary>Whether the dialog can be closed with Escape, Alt+F4 or the title bar's close box (the result is Cancel).</summary>
    public bool AllowCancel
    {
        get => _allowCancel;
        set
        {
            DenyIfBound();
            _allowCancel = value;
        }
    }

    public bool AllowMinimize
    {
        get => _allowMinimize;
        set
        {
            DenyIfBound();
            _allowMinimize = value;
        }
    }

    public bool RightToLeftLayout
    {
        get => _rightToLeftLayout;
        set
        {
            DenyIfBound();
            _rightToLeftLayout = value;
        }
    }

    /// <summary>The width follows the text instead of a fixed default.</summary>
    public bool SizeToContent
    {
        get => _sizeToContent;
        set
        {
            DenyIfBound();
            _sizeToContent = value;
        }
    }

    /// <summary>Whether &lt;a href="..."&gt;text&lt;/a&gt; in the text, footnote and expander is a link (<see cref="LinkClicked"/>).</summary>
    public bool EnableLinks
    {
        get => _enableLinks;
        set
        {
            DenyIfBound();
            _enableLinks = value;
        }
    }

    public TaskDialog? BoundDialog { get; private set; }

    /// <summary>Replaces this page in its dialog with <paramref name="page"/>.</summary>
    public void Navigate(TaskDialogPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (BoundDialog == null) throw new InvalidOperationException("Cannot navigate from a page not bound to a dialog.");
        BoundDialog.Navigate(page);
    }

    protected internal void OnCreated(EventArgs e) => Created?.Invoke(this, e);

    protected internal void OnDestroyed(EventArgs e) => Destroyed?.Invoke(this, e);

    protected internal void OnHelpRequest(EventArgs e) => HelpRequest?.Invoke(this, e);

    protected internal void OnLinkClicked(TaskDialogLinkClickedEventArgs e) => LinkClicked?.Invoke(this, e);

    internal void DenyIfBound()
    {
        if (BoundDialog != null) throw new InvalidOperationException("Cannot set this property or call this method while the page is bound to a task dialog.");
    }

    // --- binding ---------------------------------------------------------------------------

    /// <summary>The checks TaskDialogPage.Validate makes before a page is shown.</summary>
    internal void Validate()
    {
        if (BoundDialog != null) throw new InvalidOperationException("This TaskDialogPage instance is already bound to a TaskDialog instance.");
        if (_buttons.BoundPage != null || _radioButtons.BoundPage != null)
            throw new InvalidOperationException("One of the collections of this TaskDialogPage is already bound to a TaskDialog instance.");
        if (AllControls().Any(c => c.BoundPage != null))
            throw new InvalidOperationException("One of the controls of this TaskDialogPage is already bound to a TaskDialog instance.");
        if (_radioButtons.Count(r => r.Checked) > 1)
            throw new InvalidOperationException("Only a single radio button can be set as checked.");
        bool custom = _buttons.Any(b => b is not TaskDialogCommandLinkButton && !b.IsStandardButton);
        bool commandLinks = _buttons.Any(b => b is TaskDialogCommandLinkButton);
        if (custom && commandLinks)
            throw new InvalidOperationException("Cannot show both custom buttons and command links at the same time.");
        if (_buttons.Any(b => b.IsCreatable && !b.IsStandardButton && string.IsNullOrEmpty(b.Text)))
            throw new InvalidOperationException("The text of a custom button must not be null or an empty string.");
        if (_radioButtons.Any(r => string.IsNullOrEmpty(r.Text)))
            throw new InvalidOperationException("The text of a radio button must not be null or an empty string.");
        if (DefaultButton is { } def && !_buttons.Contains(def))
            throw new InvalidOperationException("The default button must exist in the buttons collection.");
    }

    internal void Bind(TaskDialog dialog)
    {
        BoundDialog = dialog;
        _buttons.BoundPage = this;
        _radioButtons.BoundPage = this;
        foreach (var control in AllControls()) control.Bind(this);
    }

    internal void Unbind()
    {
        foreach (var control in AllControls()) control.Unbind();
        _buttons.BoundPage = null;
        _radioButtons.BoundPage = null;
        BoundDialog = null;
    }

    private IEnumerable<TaskDialogControl> AllControls()
    {
        foreach (var b in _buttons) yield return b;
        foreach (var r in _radioButtons) yield return r;
        if (_verification != null) yield return _verification;
        if (_expander != null) yield return _expander;
        if (_footnote != null) yield return _footnote;
        if (_progressBar != null) yield return _progressBar;
    }

    /// <summary>Checks <paramref name="button"/> and unchecks the others, raising CheckedChanged for each change.</summary>
    internal void CheckRadioButton(TaskDialogRadioButton button)
    {
        foreach (var other in _radioButtons)
        {
            if (!ReferenceEquals(other, button)) other.SetCheckedFromDialog(false);
        }
        button.SetCheckedFromDialog(true);
        BoundDialog?.OnPageChanged();
    }
}
