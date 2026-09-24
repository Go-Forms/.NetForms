using System;
using System.ComponentModel;
using System.IO;
using NetForms.Platform;

namespace System.Windows.Forms;

/// <summary>The base of the modal dialogs, as WinForms shapes it: <see cref="ShowDialog()"/> and <see cref="Reset"/>.</summary>
public abstract class CommonDialog : Component
{
    [Category("Data")]
    [Description("User-defined data associated with the object.")]
    [DefaultValue(null)]
    public object? Tag { get; set; }

    [Description("Occurs when the user clicks the Help button.")]
    public event EventHandler? HelpRequest;

    protected virtual void OnHelpRequest(EventArgs e) => HelpRequest?.Invoke(this, e);

    public abstract void Reset();

    /// <summary>Shows the dialog; the handle is a WinForms leftover and is not used here.</summary>
    protected abstract bool RunDialog(IntPtr hwndOwner);

    public DialogResult ShowDialog() => ShowDialog(null);

    public DialogResult ShowDialog(IWin32Window? owner)
    {
        OwnerWindow = owner as Form ?? (owner as Control)?.FindForm() ?? Form.ActiveForm;
        return RunDialog(IntPtr.Zero) ? DialogResult.OK : DialogResult.Cancel;
    }

    /// <summary>The form the dialog is modal against; set by <see cref="ShowDialog(IWin32Window)"/>.</summary>
    private protected Form? OwnerWindow { get; private set; }
}

/// <summary>
/// The common base of the file pickers. The dialog itself is the system's: WinForms called
/// GetOpenFileName, we hand the request to the platform layer, which uses Avalonia's storage
/// provider (the XDG portal on Linux, the common item dialog on Windows).
/// </summary>
[DefaultEvent("FileOk")]
[DefaultProperty("FileName")]
public abstract class FileDialog : CommonDialog
{
    private string _fileName = string.Empty;
    private string[] _fileNames = Array.Empty<string>();

    protected FileDialog() => Reset();

    [Description("Occurs when the user clicks the Open or Save button in the dialog box.")]
    public event CancelEventHandler? FileOk;

    protected void OnFileOk(CancelEventArgs e) => FileOk?.Invoke(this, e);

    [Category("Data")]
    [Description("The file first shown in the dialog box, or the last one selected by the user.")]
    [DefaultValue("")]
    public virtual string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value ?? string.Empty;
            _fileNames = _fileName.Length > 0 ? new[] { _fileName } : Array.Empty<string>();
        }
    }

    [Description("Retrieves the file names of all selected files in the dialog box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] FileNames => (string[])_fileNames.Clone();

    /// <summary>WinForms' filter string: "Text files (*.txt)|*.txt|All files (*.*)|*.*".</summary>
    [Category("Behavior")]
    [Description("The file filters to display in the dialog box, for example, \"C# files|*.cs|All files|*.*\".")]
    [DefaultValue("")]
    [Localizable(true)]
    public string Filter
    {
        get => _filter;
        set
        {
            value ??= string.Empty;
            if (value.Length > 0 && value.Split('|').Length % 2 != 0)
                throw new ArgumentException("The filter string is not valid.", nameof(value));
            _filter = value;
        }
    }

    private string _filter = string.Empty;

    [Category("Behavior")]
    [Description("The index of the file filter selected in the dialog box. The first item has an index of 1.")]
    [DefaultValue(1)]
    public int FilterIndex { get; set; } = 1;

    [Category("Data")]
    [Description("The initial directory for the dialog box.")]
    [DefaultValue("")]
    public string InitialDirectory { get; set; } = string.Empty;

    [Category("Appearance")]
    [Description("The string to display in the title bar of the dialog box.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string Title { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("The default file name extension. If the user types a file name, this extension is added at the end of the file name if one is not specified.")]
    [DefaultValue("")]
    public string DefaultExt { get; set; } = string.Empty;

    [Category("Behavior")]
    [Description("Controls whether extensions are automatically added to file names.")]
    [DefaultValue(true)]
    public bool AddExtension { get; set; } = true;

    [Category("Behavior")]
    [Description("Checks that the specified file exists before returning from the dialog.")]
    [DefaultValue(false)]
    public bool CheckFileExists { get; set; }

    [Category("Behavior")]
    [Description("Checks that the specified path exists before returning from the dialog.")]
    [DefaultValue(true)]
    public bool CheckPathExists { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether shortcuts are dereferenced before returning from the dialog.")]
    [DefaultValue(true)]
    public bool DereferenceLinks { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether the dialog box restores the current directory before closing.")]
    [DefaultValue(false)]
    public bool RestoreDirectory { get; set; }

    [Category("Behavior")]
    [Description("Enables the Help button.")]
    [DefaultValue(false)]
    public bool ShowHelp { get; set; }

    [Category("Behavior")]
    [Description("Controls whether multi-dotted extensions are supported.")]
    [DefaultValue(false)]
    public bool SupportMultiDottedExtensions { get; set; }

    [Category("Behavior")]
    [Description("Controls whether the dialog box ensures that file names do not contain invalid characters or sequences.")]
    [DefaultValue(true)]
    public bool ValidateNames { get; set; } = true;

    [DefaultValue(true)]
    public bool AutoUpgradeEnabled { get; set; } = true;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? ClientGuid { get; set; }

    public override void Reset()
    {
        _fileName = string.Empty;
        _fileNames = Array.Empty<string>();
        _filter = string.Empty;
        FilterIndex = 1;
        InitialDirectory = string.Empty;
        Title = string.Empty;
        DefaultExt = string.Empty;
        AddExtension = true;
        CheckFileExists = false;
        CheckPathExists = true;
        RestoreDirectory = false;
        ValidateNames = true;
    }

    private protected FileDialogOptions BuildOptions() => new()
    {
        Title = Title,
        Filter = _filter,
        FilterIndex = FilterIndex,
        InitialDirectory = InitialDirectory,
        FileName = _fileName.Length > 0 ? Path.GetFileName(_fileName) : string.Empty,
        DefaultExt = DefaultExt,
        AddExtension = AddExtension,
    };

    private protected bool Accept(string[]? paths)
    {
        if (paths == null || paths.Length == 0) return false;
        _fileNames = paths;
        _fileName = paths[0];

        var e = new CancelEventArgs();
        OnFileOk(e);
        if (e.Cancel)
        {
            _fileNames = Array.Empty<string>();
            _fileName = string.Empty;
            return false;
        }
        return true;
    }

    private protected IPlatformWindow? OwnerPlatformWindow => OwnerWindow?.PlatformWindow;

    public override string ToString() => base.ToString() + ": Title: " + Title + ", FileName: " + _fileName;
}

public class OpenFileDialog : FileDialog
{
    [Category("Behavior")]
    [Description("Controls whether multiple files can be selected in the dialog.")]
    [DefaultValue(false)]
    public bool Multiselect { get; set; }

    [Category("Behavior")]
    [Description("The state of the read-only check box in the dialog.")]
    [DefaultValue(false)]
    public bool ReadOnlyChecked { get; set; }

    [Category("Behavior")]
    [Description("Controls whether to show the read-only check box in the dialog.")]
    [DefaultValue(false)]
    public bool ShowReadOnly { get; set; }

    [Category("Behavior")]
    [Description("Controls whether the dialog box shows a preview for selected files.")]
    [DefaultValue(false)]
    public bool ShowPreview { get; set; }

    public override void Reset()
    {
        base.Reset();
        Multiselect = false;
        ReadOnlyChecked = false;
        ShowReadOnly = false;
        CheckFileExists = true;
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        var options = BuildOptions();
        options.Multiselect = Multiselect;
        return Accept(Application.Platform.ShowOpenFileDialog(OwnerPlatformWindow, options));
    }

    public Stream OpenFile()
    {
        if (FileName.Length == 0) throw new ArgumentNullException(nameof(FileName));
        return new FileStream(FileName, FileMode.Open, FileAccess.Read);
    }

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Category("Behavior")]
    [DefaultValue(true)]
    [Localizable(false)]
    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new bool CheckFileExists { get => base.CheckFileExists; set => base.CheckFileExists = value; }
}

public class SaveFileDialog : FileDialog
{
    [Category("Behavior")]
    [Description("Controls whether to prompt the user when an existing file is about to be overwritten. It is only applicable if 'ValidateNames' is set to true.")]
    [DefaultValue(true)]
    public bool OverwritePrompt { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether to prompt the user when a new file is about to be created. It is only applicable if 'ValidateNames' is set to true.")]
    [DefaultValue(false)]
    public bool CreatePrompt { get; set; }

    [Category("Behavior")]
    [Description("Controls whether the dialog box is always opened in the expanded mode.")]
    [DefaultValue(true)]
    public bool ExpandedMode { get; set; } = true;

    public override void Reset()
    {
        base.Reset();
        OverwritePrompt = true;
        CreatePrompt = false;
        CheckFileExists = false;
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        var path = Application.Platform.ShowSaveFileDialog(OwnerPlatformWindow, BuildOptions());
        return Accept(path == null ? null : new[] { path });
    }

    public Stream OpenFile()
    {
        if (FileName.Length == 0) throw new ArgumentNullException(nameof(FileName));
        return new FileStream(FileName, FileMode.Create, FileAccess.ReadWrite);
    }
}

[DefaultEvent("HelpRequest")]
[DefaultProperty("SelectedPath")]
public class FolderBrowserDialog : CommonDialog
{
    public FolderBrowserDialog() => Reset();

    [Category("Folder Browsing")]
    [Description("The path of the folder first selected in the dialog or the last one selected by the user.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string SelectedPath { get; set; } = string.Empty;

    [Category("Folder Browsing")]
    [Description("The string that is displayed above the tree view control in the dialog box. This string can be used to specify instructions to the user.")]
    [DefaultValue("")]
    [Localizable(true)]
    public string Description { get; set; } = string.Empty;

    [Category("Folder Browsing")]
    [Description("The initial directory for the dialog box.")]
    [DefaultValue("")]
    public string InitialDirectory { get; set; } = string.Empty;

    [Category("Folder Browsing")]
    [Description("Include the New Folder button in the dialog box.")]
    [DefaultValue(true)]
    public bool ShowNewFolderButton { get; set; } = true;

    [Category("Folder Browsing")]
    [Description("A value that indicates whether to use the value of the Description property as the dialog title for Vista style dialogs. This property has no effect on old style dialogs.")]
    [DefaultValue(false)]
    [Localizable(true)]
    public bool UseDescriptionForTitle { get; set; }

    [DefaultValue(true)]
    public bool AutoUpgradeEnabled { get; set; } = true;

    [Category("Behavior")]
    [Description("Controls whether multiple folders can be selected in the dialog.")]
    [DefaultValue(false)]
    public bool Multiselect { get; set; }

    [Description("Retrieves the paths of all selected folders in the dialog box.")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string[] SelectedPaths => SelectedPath.Length > 0 ? new[] { SelectedPath } : Array.Empty<string>();

    [Category("Folder Browsing")]
    [Description("The location of the root folder from which to start browsing. Only the specified folder and any subfolders that are beneath it will appear in the dialog box.")]
    [DefaultValue(typeof(Environment.SpecialFolder), "Desktop")]
    public Environment.SpecialFolder RootFolder { get; set; } = Environment.SpecialFolder.Desktop;

    public override void Reset()
    {
        SelectedPath = string.Empty;
        Description = string.Empty;
        InitialDirectory = string.Empty;
        ShowNewFolderButton = true;
        RootFolder = Environment.SpecialFolder.Desktop;
    }

    protected override bool RunDialog(IntPtr hwndOwner)
    {
        var start = InitialDirectory.Length > 0 ? InitialDirectory
            : SelectedPath.Length > 0 ? SelectedPath
            : Environment.GetFolderPath(RootFolder);

        var path = Application.Platform.ShowFolderDialog(OwnerWindow?.PlatformWindow, new FolderDialogOptions
        {
            Title = UseDescriptionForTitle ? Description : Description,
            InitialDirectory = start,
        });
        if (path == null) return false;
        SelectedPath = path;
        return true;
    }

    public override string ToString() => base.ToString() + ": SelectedPath: " + SelectedPath;

    // Members WinForms hides or re-defaults on this control; TypeDescriptor reads them
    // off the derived type, so they have to be re-declared here to be advertised differently.

    [Localizable(false)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public new event EventHandler? HelpRequest
    {
        add => base.HelpRequest += value;
        remove => base.HelpRequest -= value;
    }
}
