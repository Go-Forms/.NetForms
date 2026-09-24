using System;
using System.Collections.ObjectModel;

namespace System.Windows.Forms;

/// <summary>The IDs of the task dialog's common buttons (IDOK, IDCANCEL, ...).</summary>
internal enum TaskDialogResult
{
    OK = 1,
    Cancel = 2,
    Abort = 3,
    Retry = 4,
    Ignore = 5,
    Yes = 6,
    No = 7,
    Close = 8,
    Help = 9,
    TryAgain = 10,
    Continue = 11,
}

/// <summary>
/// A button of a task dialog: a standard one (<see cref="OK"/>, <see cref="Yes"/>, ... - a new instance on
/// every access, all equal to each other) or a custom one with its own text.
/// </summary>
public class TaskDialogButton : TaskDialogControl
{
    private readonly TaskDialogResult? _standardButtonResult;
    private bool _enabled = true;
    private bool _showShieldIcon;
    private bool _visible = true;
    private string? _text;

    public event EventHandler? Click;

    public TaskDialogButton()
    {
    }

    public TaskDialogButton(string? text, bool enabled = true, bool allowCloseDialog = true)
        : this()
    {
        _text = text;
        Enabled = enabled;
        AllowCloseDialog = allowCloseDialog;
    }

    internal TaskDialogButton(TaskDialogResult standardButtonResult)
    {
        _standardButtonResult = standardButtonResult;
        _text = standardButtonResult.ToString();
    }

    public static TaskDialogButton OK => new(TaskDialogResult.OK);
    public static TaskDialogButton Cancel => new(TaskDialogResult.Cancel);
    public static TaskDialogButton Abort => new(TaskDialogResult.Abort);
    public static TaskDialogButton Retry => new(TaskDialogResult.Retry);
    public static TaskDialogButton Ignore => new(TaskDialogResult.Ignore);
    public static TaskDialogButton Yes => new(TaskDialogResult.Yes);
    public static TaskDialogButton No => new(TaskDialogResult.No);
    public static TaskDialogButton Close => new(TaskDialogResult.Close);
    public static TaskDialogButton Help => new(TaskDialogResult.Help);
    public static TaskDialogButton TryAgain => new(TaskDialogResult.TryAgain);
    public static TaskDialogButton Continue => new(TaskDialogResult.Continue);

    public bool AllowCloseDialog { get; set; } = true;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            Update();
        }
    }

    public bool ShowShieldIcon
    {
        get => _showShieldIcon;
        set
        {
            _showShieldIcon = value;
            Update();
        }
    }

    public bool Visible
    {
        get => _visible;
        set
        {
            DenyIfBound();
            _visible = value;
        }
    }

    public string? Text
    {
        get => _text;
        set
        {
            if (IsStandardButton) throw new InvalidOperationException("Cannot set the text for a standard button.");
            DenyIfBound();
            _text = value;
        }
    }

    internal override bool IsCreatable => base.IsCreatable && _visible;

    internal bool IsStandardButton => _standardButtonResult is not null;

    internal TaskDialogResult StandardButtonResult => _standardButtonResult ?? throw new InvalidOperationException();

    internal TaskDialogButtonCollection? Collection { get; set; }

    /// <summary>The caption shown: the system's (localized) text for a standard button.</summary>
    internal virtual string DisplayText => _standardButtonResult switch
    {
        TaskDialogResult.OK => SystemStrings.Get("OK"),
        TaskDialogResult.Cancel => SystemStrings.Get("Cancel"),
        TaskDialogResult.Abort => SystemStrings.Get("&Abort"),
        TaskDialogResult.Retry => SystemStrings.Get("&Retry"),
        TaskDialogResult.Ignore => SystemStrings.Get("&Ignore"),
        TaskDialogResult.Yes => SystemStrings.Get("&Yes"),
        TaskDialogResult.No => SystemStrings.Get("&No"),
        TaskDialogResult.Close => SystemStrings.Get("&Close"),
        TaskDialogResult.Help => SystemStrings.Get("Help"),
        TaskDialogResult.TryAgain => SystemStrings.Get("&Try Again"),
        TaskDialogResult.Continue => SystemStrings.Get("&Continue"),
        _ => _text ?? string.Empty,
    };

    public static bool operator ==(TaskDialogButton? b1, TaskDialogButton? b2) => Equals(b1, b2);

    public static bool operator !=(TaskDialogButton? b1, TaskDialogButton? b2) => !(b1 == b2);

    /// <summary>Clicks the button in the shown dialog, as the user would.</summary>
    public void PerformClick()
    {
        var dialog = BoundPage?.BoundDialog ?? throw new InvalidOperationException("This control is not currently bound to a task dialog.");
        dialog.ClickButton(this);
    }

    public override bool Equals(object? obj)
    {
        if (IsStandardButton && obj is TaskDialogButton other && other.IsStandardButton)
            return _standardButtonResult!.Value == other._standardButtonResult!.Value;
        return base.Equals(obj);
    }

    public override int GetHashCode() => IsStandardButton ? (int)_standardButtonResult!.Value : base.GetHashCode();

    public override string ToString() => _text ?? base.ToString() ?? string.Empty;

    /// <summary>Raises Click; true if the dialog may close with this button as the result.</summary>
    internal bool HandleButtonClicked()
    {
        Click?.Invoke(this, EventArgs.Empty);
        return AllowCloseDialog;
    }
}

/// <summary>A large button with a description line, shown in the content area.</summary>
public sealed class TaskDialogCommandLinkButton : TaskDialogButton
{
    private string? _descriptionText;

    public TaskDialogCommandLinkButton()
    {
    }

    public TaskDialogCommandLinkButton(string? text, string? descriptionText = null, bool enabled = true, bool allowCloseDialog = true)
        : base(text, enabled, allowCloseDialog)
    {
        _descriptionText = descriptionText;
    }

    public string? DescriptionText
    {
        get => _descriptionText;
        set
        {
            DenyIfBound();
            _descriptionText = value;
        }
    }
}

public class TaskDialogButtonCollection : Collection<TaskDialogButton>
{
    public TaskDialogButtonCollection()
    {
    }

    internal TaskDialogPage? BoundPage { get; set; }

    public TaskDialogButton Add(string? text, bool enabled = true, bool allowCloseDialog = true)
    {
        var button = new TaskDialogButton(text, enabled, allowCloseDialog);
        Add(button);
        return button;
    }

    protected override void SetItem(int index, TaskDialogButton item)
    {
        DenyIfBound();
        Check(item, index);
        this[index].Collection = null;
        base.SetItem(index, item);
        item.Collection = this;
    }

    protected override void InsertItem(int index, TaskDialogButton item)
    {
        DenyIfBound();
        Check(item, -1);
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

    private void Check(TaskDialogButton item, int replacing)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Collection != null) throw new InvalidOperationException("This control is already part of a different collection.");
        // A standard button equals every other instance of it, so it can be in the collection once.
        int existing = IndexOf(item);
        if (existing >= 0 && existing != replacing) throw new InvalidOperationException("This control has already been added to the collection.");
    }

    private void DenyIfBound() => BoundPage?.DenyIfBound();
}
