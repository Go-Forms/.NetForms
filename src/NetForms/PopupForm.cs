using System;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// A borderless, non-activating top-level window anchored to a control: the drop-down of a
/// ComboBox, a ToolTip. It closes when the owner form is clicked, deactivated, moved or resized.
/// </summary>
internal class PopupForm : Form
{
    private Form? _ownerForm;

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        VisibleOwn = false;
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>Raised when the popup closed for any reason (click outside, escape, owner moved).</summary>
    public event EventHandler? Dismissed;

    /// <summary>Show the popup at <paramref name="screenLocation"/> (client origin), tied to <paramref name="anchor"/>'s form.</summary>
    public void ShowAt(Control anchor, Point screenLocation, Size size)
    {
        var owner = anchor.FindForm();
        Detach();
        _ownerForm = owner;
        if (owner != null)
        {
            Owner = owner;
            owner.MouseDownAnywhere += OwnerMouseDown;
            owner.Deactivate += OwnerDeactivated;
            owner.WindowMovedOrResized += OwnerMoved;
            owner.FormClosed += OwnerClosed;
        }
        ClientSize = size;
        Location = screenLocation;
        Show();
    }

    private void OwnerMouseDown(object? sender, MouseEventArgs e)
    {
        // Any click on the owner outside the anchor dismisses the popup; ComboBox decides what to do with clicks on its own button.
        if (!IsClickInsideAnchor(e.Location)) Dismiss();
    }

    protected virtual bool IsClickInsideAnchor(Point ownerClientPoint) => false;

    private void OwnerDeactivated(object? sender, EventArgs e)
    {
        // Showing a non-activating window still makes the owner report a moment of deactivation on
        // Windows, and dismissing right there would close the menu the click just opened. Re-check
        // once the message has been processed: if the application really lost the foreground, the
        // owner is still inactive and the popup goes away.
        var owner = _ownerForm;
        Application.Post(() =>
        {
            if (!VisibleOwn || owner == null) return;
            if (owner.IsWindowActive || IsWindowActive) return;
            Dismiss();
        });
    }

    private void OwnerMoved(object? sender, EventArgs e) => Dismiss();

    private void OwnerClosed(object? sender, FormClosedEventArgs e) => Dismiss();

    private void Detach()
    {
        if (_ownerForm == null) return;
        _ownerForm.MouseDownAnywhere -= OwnerMouseDown;
        _ownerForm.Deactivate -= OwnerDeactivated;
        _ownerForm.WindowMovedOrResized -= OwnerMoved;
        _ownerForm.FormClosed -= OwnerClosed;
        _ownerForm = null;
    }

    public void Dismiss()
    {
        if (!VisibleOwn) return;
        Detach();
        Hide();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Detach();
        base.Dispose(disposing);
    }
}
