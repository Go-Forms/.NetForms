using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

// The task dialog's parts, after dotnet/winforms (MIT) System/Windows/Forms/Dialogs/TaskDialog: what can be
// changed while the dialog is shown (text, icons, progress, enabled/checked states) and what throws.

/// <summary>A part of a <see cref="TaskDialogPage"/>. It is "bound" while its page is shown.</summary>
public abstract class TaskDialogControl
{
    private protected TaskDialogControl()
    {
    }

    public TaskDialogPage? BoundPage { get; private set; }

    public object? Tag { get; set; }

    /// <summary>Whether the part shows at all (a footnote without text does not).</summary>
    internal virtual bool IsCreatable => true;

    /// <summary>Bound and shown: only then are updates applied to the dialog.</summary>
    internal bool IsCreated { get; private set; }

    internal void Bind(TaskDialogPage page)
    {
        BoundPage = page;
        IsCreated = IsCreatable;
    }

    internal void Unbind()
    {
        BoundPage = null;
        IsCreated = false;
    }

    private protected void DenyIfBound() => BoundPage?.DenyIfBound();

    private protected void DenyIfBoundAndNotCreated()
    {
        if (BoundPage != null && !IsCreated) throw new InvalidOperationException("The control has not been created.");
    }

    /// <summary>The shown dialog redraws the part.</summary>
    private protected void Update()
    {
        if (IsCreated) BoundPage?.BoundDialog?.OnPageChanged();
    }
}

public sealed class TaskDialogRadioButton : TaskDialogControl
{
    private string? _text;
    private bool _enabled = true;
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public TaskDialogRadioButton()
    {
    }

    public TaskDialogRadioButton(string? text) => _text = text;

    public string? Text
    {
        get => _text;
        set
        {
            DenyIfBound();
            _text = value;
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Update();
        }
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            DenyIfBoundAndNotCreated();
            if (BoundPage?.BoundDialog == null)
            {
                _checked = value;
                return;
            }
            if (!value) throw new InvalidOperationException("Cannot uncheck a radio button while it is bound to a task dialog.");
            if (!_checked) BoundPage.CheckRadioButton(this);
        }
    }

    internal Collection<TaskDialogRadioButton>? Collection { get; set; }

    /// <summary>Sets the state from the dialog (a click or a sibling being checked) and tells the handlers.</summary>
    internal void SetCheckedFromDialog(bool value)
    {
        if (_checked == value) return;
        _checked = value;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => _text ?? base.ToString() ?? string.Empty;
}

public class TaskDialogRadioButtonCollection : Collection<TaskDialogRadioButton>
{
    public TaskDialogRadioButtonCollection()
    {
    }

    internal TaskDialogPage? BoundPage { get; set; }

    public TaskDialogRadioButton Add(string? text)
    {
        var button = new TaskDialogRadioButton { Text = text };
        Add(button);
        return button;
    }

    protected override void SetItem(int index, TaskDialogRadioButton item)
    {
        DenyIfBound();
        Check(item);
        this[index].Collection = null;
        base.SetItem(index, item);
        item.Collection = this;
    }

    protected override void InsertItem(int index, TaskDialogRadioButton item)
    {
        DenyIfBound();
        Check(item);
        base.InsertItem(index, item);
        item.Collection = this;
    }

    protected override void RemoveItem(int index)
    {
        DenyIfBound();
        this[index].Collection = null;
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        DenyIfBound();
        foreach (var item in this) item.Collection = null;
        base.ClearItems();
    }

    private void Check(TaskDialogRadioButton item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Collection != null) throw new InvalidOperationException("This control is already part of a different collection.");
    }

    private void DenyIfBound() => BoundPage?.DenyIfBound();
}

public sealed class TaskDialogVerificationCheckBox : TaskDialogControl
{
    private string? _text;
    private bool _checked;

    public event EventHandler? CheckedChanged;

    public TaskDialogVerificationCheckBox()
    {
    }

    public TaskDialogVerificationCheckBox(string? text, bool isChecked = false)
    {
        _text = text;
        _checked = isChecked;
    }

    public static implicit operator TaskDialogVerificationCheckBox(string verificationText) => new(verificationText);

    public string? Text
    {
        get => _text;
        set
        {
            DenyIfBound();
            _text = value;
        }
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            DenyIfBoundAndNotCreated();
            SetChecked(value);
        }
    }

    internal override bool IsCreatable => base.IsCreatable && !string.IsNullOrEmpty(_text);

    internal void SetChecked(bool value)
    {
        if (_checked == value) return;
        _checked = value;
        Update();
        CheckedChanged?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => _text ?? base.ToString() ?? string.Empty;
}

public enum TaskDialogExpanderPosition
{
    AfterText = 0,
    AfterFootnote = 1,
}

public sealed class TaskDialogExpander : TaskDialogControl
{
    private string? _text;
    private string? _expandedButtonText;
    private string? _collapsedButtonText;
    private bool _expanded;
    private TaskDialogExpanderPosition _position;

    public event EventHandler? ExpandedChanged;

    public TaskDialogExpander()
    {
    }

    public TaskDialogExpander(string? text) => _text = text;

    public string? Text
    {
        get => _text;
        set
        {
            DenyIfBoundAndNotCreated();
            _text = value;
            Update();
        }
    }

    public string? ExpandedButtonText
    {
        get => _expandedButtonText;
        set
        {
            DenyIfBound();
            _expandedButtonText = value;
        }
    }

    public string? CollapsedButtonText
    {
        get => _collapsedButtonText;
        set
        {
            DenyIfBound();
            _collapsedButtonText = value;
        }
    }

    public bool Expanded
    {
        get => _expanded;
        set
        {
            DenyIfBound();
            _expanded = value;
        }
    }

    public TaskDialogExpanderPosition Position
    {
        get => _position;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(TaskDialogExpanderPosition));
            DenyIfBound();
            _position = value;
        }
    }

    internal override bool IsCreatable => base.IsCreatable && !string.IsNullOrEmpty(_text);

    /// <summary>The user toggled the expando button.</summary>
    internal void Toggle()
    {
        _expanded = !_expanded;
        ExpandedChanged?.Invoke(this, EventArgs.Empty);
    }

    public override string ToString() => _text ?? base.ToString() ?? string.Empty;
}

public sealed class TaskDialogFootnote : TaskDialogControl
{
    private string? _text;
    private TaskDialogIcon? _icon;

    public TaskDialogFootnote()
    {
    }

    public TaskDialogFootnote(string? text) => _text = text;

    public static implicit operator TaskDialogFootnote(string footnoteText) => new(footnoteText);

    public string? Text
    {
        get => _text;
        set
        {
            DenyIfBoundAndNotCreated();
            _text = value;
            Update();
        }
    }

    public TaskDialogIcon? Icon
    {
        get => _icon;
        set
        {
            DenyIfBoundAndNotCreated();
            _icon = value;
            Update();
        }
    }

    internal override bool IsCreatable => base.IsCreatable && !string.IsNullOrEmpty(_text);

    public override string ToString() => _text ?? base.ToString() ?? string.Empty;
}

public enum TaskDialogProgressBarState
{
    Normal = 0,
    Paused = 1,
    Error = 2,
    Marquee = 3,
    MarqueePaused = 4,
    None = 5,
}

public sealed class TaskDialogProgressBar : TaskDialogControl
{
    private TaskDialogProgressBarState _state;
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private int _marqueeSpeed;

    public TaskDialogProgressBar()
    {
    }

    public TaskDialogProgressBar(TaskDialogProgressBarState state) => _state = state;

    public TaskDialogProgressBarState State
    {
        get => _state;
        set
        {
            DenyIfBoundAndNotCreated();
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(TaskDialogProgressBarState));
            if (BoundPage?.BoundDialog != null && value == TaskDialogProgressBarState.None)
                throw new InvalidOperationException("Cannot remove the progress bar while the task dialog is shown.");
            _state = value;
            Update();
        }
    }

    public int Minimum
    {
        get => _minimum;
        set
        {
            CheckRange(value);
            DenyIfBoundAndNotCreated();
            _minimum = value;
            Update();
        }
    }

    public int Maximum
    {
        get => _maximum;
        set
        {
            CheckRange(value);
            DenyIfBoundAndNotCreated();
            _maximum = value;
            Update();
        }
    }

    public int Value
    {
        get => _value;
        set
        {
            CheckRange(value);
            DenyIfBoundAndNotCreated();
            _value = value;
            Update();
        }
    }

    public int MarqueeSpeed
    {
        get => _marqueeSpeed;
        set
        {
            DenyIfBoundAndNotCreated();
            _marqueeSpeed = value;
            Update();
        }
    }

    /// <summary>The native progress bar takes 16-bit positions.</summary>
    private static void CheckRange(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, ushort.MaxValue);
    }

    internal override bool IsCreatable => base.IsCreatable && _state != TaskDialogProgressBarState.None;
}

/// <summary>The main or footnote icon: a standard one (with or without a coloured header bar) or any image.</summary>
public class TaskDialogIcon : IDisposable
{
    internal enum Kind { None, Information, Warning, Error, Shield, ShieldBlueBar, ShieldGrayBar, ShieldWarningYellowBar, ShieldErrorRedBar, ShieldSuccessGreenBar, Custom }

    public static readonly TaskDialogIcon None = new(Kind.None);
    public static readonly TaskDialogIcon Information = new(Kind.Information);
    public static readonly TaskDialogIcon Warning = new(Kind.Warning);
    public static readonly TaskDialogIcon Error = new(Kind.Error);
    public static readonly TaskDialogIcon Shield = new(Kind.Shield);
    public static readonly TaskDialogIcon ShieldBlueBar = new(Kind.ShieldBlueBar);
    public static readonly TaskDialogIcon ShieldGrayBar = new(Kind.ShieldGrayBar);
    public static readonly TaskDialogIcon ShieldWarningYellowBar = new(Kind.ShieldWarningYellowBar);
    public static readonly TaskDialogIcon ShieldErrorRedBar = new(Kind.ShieldErrorRedBar);
    public static readonly TaskDialogIcon ShieldSuccessGreenBar = new(Kind.ShieldSuccessGreenBar);

    private readonly Image? _image;
    private readonly bool _ownsImage;

    private TaskDialogIcon(Kind kind) => IconKind = kind;

    public TaskDialogIcon(Bitmap image)
        : this(Kind.Custom)
    {
        ArgumentNullException.ThrowIfNull(image);
        _image = image;
    }

    public TaskDialogIcon(Icon icon)
        : this(Kind.Custom)
    {
        ArgumentNullException.ThrowIfNull(icon);
        _image = icon.ToBitmap();
        _ownsImage = true;
        IconHandle = icon.Handle;
    }

    /// <summary>An HICON. NetForms has no native icons, so nothing is drawn for it.</summary>
    public TaskDialogIcon(IntPtr iconHandle)
        : this(Kind.Custom) => IconHandle = iconHandle;

    public IntPtr IconHandle { get; }

    internal Kind IconKind { get; }

    /// <summary>The picture to draw at the given size, or null for none.</summary>
    internal Image? GetImage()
    {
        return IconKind switch
        {
            Kind.Custom => _image,
            Kind.Information => SystemIcons.Information.Bitmap,
            Kind.Warning => SystemIcons.Warning.Bitmap,
            Kind.Error => SystemIcons.Error.Bitmap,
            Kind.None => null,
            _ => SystemIcons.Shield.Bitmap,
        };
    }

    /// <summary>The colour of the header bar behind the heading, for the Shield*Bar icons.</summary>
    internal Color? BarColor => IconKind switch
    {
        Kind.ShieldBlueBar => Color.FromArgb(0x1E, 0x56, 0xA8),
        Kind.ShieldGrayBar => Color.FromArgb(0x6B, 0x6B, 0x6B),
        Kind.ShieldWarningYellowBar => Color.FromArgb(0xF4, 0xCE, 0x3A),
        Kind.ShieldErrorRedBar => Color.FromArgb(0xB8, 0x1B, 0x1B),
        Kind.ShieldSuccessGreenBar => Color.FromArgb(0x2B, 0x8A, 0x3E),
        _ => null,
    };

    /// <summary>Heading text on the bar: dark on yellow, white on the others.</summary>
    internal Color BarTextColor => IconKind == Kind.ShieldWarningYellowBar ? Color.Black : Color.White;

    public void Dispose()
    {
        if (_ownsImage) _image?.Dispose();
        GC.SuppressFinalize(this);
    }
}

public enum TaskDialogStartupLocation
{
    CenterScreen = 0,
    CenterOwner = 1,
}

public class TaskDialogLinkClickedEventArgs : EventArgs
{
    public TaskDialogLinkClickedEventArgs(string linkHref) => LinkHref = linkHref;

    public string LinkHref { get; }
}
