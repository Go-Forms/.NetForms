using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>Members of WinForms' ToolStripItem that code sets and reads but that change nothing NetForms draws.</summary>
public abstract partial class ToolStripItem
{
    private bool _isDisposed;

    /// <summary>True once the item is disposed (BindingNavigator forgets disposed items through it).</summary>
    [Browsable(false)]
    public bool IsDisposed => _isDisposed;

    protected override void Dispose(bool disposing)
    {
        _isDisposed = true;
        base.Dispose(disposing);
    }

    /// <summary>Whether the image is mirrored when the strip is right to left. NetForms does not mirror images yet.</summary>
    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue(false)]
    [Description("Specifies whether the image should be mirrored when the ToolStripItem is right to left.")]
    public bool RightToLeftAutoMirrorImage { get; set; }

    [Category("Accessibility")]
    [DefaultValue(null)]
    [Localizable(true)]
    [Description("The name that will be reported to accessibility clients.")]
    public string? AccessibleName { get; set; }

    [Category("Accessibility")]
    [DefaultValue(null)]
    [Localizable(true)]
    [Description("The description that will be reported to accessibility clients.")]
    public string? AccessibleDescription { get; set; }

    [Category("Accessibility")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The default action description of the control for use by accessibility client applications.")]
    public string? AccessibleDefaultActionDescription { get; set; }

    [Category("Accessibility")]
    [DefaultValue(AccessibleRole.Default)]
    [Description("The role that will be reported to accessibility clients.")]
    public AccessibleRole AccessibleRole { get; set; } = AccessibleRole.Default;
}
