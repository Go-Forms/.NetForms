using System.Drawing;
using System.Drawing.Printing;

namespace System.Windows.Forms;

/// <summary>
/// Wraps another controller and shows "Page N of document" with a Cancel button while it prints. WinForms runs
/// the dialog on a thread of its own; here it is a small modeless form on the UI thread, pumped after every
/// page (Application.DoEvents), so it paints and its Cancel button works while the document prints.
/// </summary>
public class PrintControllerWithStatusDialog : PrintController
{
    private readonly PrintController _underlyingController;
    private readonly string _dialogTitle;
    private PrintDocument? _document;
    private StatusForm? _dialog;
    private int _pageNumber;

    public PrintControllerWithStatusDialog(PrintController underlyingController)
        : this(underlyingController, "Printing")
    {
    }

    public PrintControllerWithStatusDialog(PrintController underlyingController, string dialogTitle)
    {
        _underlyingController = underlyingController;
        _dialogTitle = dialogTitle;
    }

    public override bool IsPreview => _underlyingController?.IsPreview ?? false;

    /// <summary>The status form while a print runs (for the tests).</summary>
    internal Form? StatusDialog => _dialog;

    /// <summary>Whether the status form is shown at all (WinForms: SystemInformation.UserInteractive).</summary>
    internal static bool ShowStatus { get; set; } = SystemInformation.UserInteractive;

    public override void OnStartPrint(PrintDocument document, PrintEventArgs e)
    {
        base.OnStartPrint(document, e);
        _document = document;
        _pageNumber = 1;

        if (ShowStatus)
        {
            _dialog = new StatusForm(_dialogTitle);
            _dialog.UpdateLabel(_pageNumber, document.DocumentName);
            _dialog.Show(Form.ActiveForm);
            Application.DoEvents();
        }

        try
        {
            _underlyingController.OnStartPrint(document, e);
        }
        catch
        {
            CloseDialog();
            throw;
        }
        finally
        {
            if (_dialog is { Canceled: true }) e.Cancel = true;
        }
    }

    public override Graphics? OnStartPage(PrintDocument document, PrintPageEventArgs e)
    {
        base.OnStartPage(document, e);
        if (_dialog != null)
        {
            _dialog.UpdateLabel(_pageNumber, document.DocumentName);
            Application.DoEvents();
        }

        var result = _underlyingController.OnStartPage(document, e);
        if (_dialog is { Canceled: true }) e.Cancel = true;
        return result;
    }

    public override void OnEndPage(PrintDocument document, PrintPageEventArgs e)
    {
        _underlyingController.OnEndPage(document, e);
        if (_dialog != null)
        {
            Application.DoEvents();
            if (_dialog.Canceled) e.Cancel = true;
        }
        _pageNumber++;
        base.OnEndPage(document, e);
    }

    public override void OnEndPrint(PrintDocument document, PrintEventArgs e)
    {
        _underlyingController.OnEndPrint(document, e);
        if (_dialog is { Canceled: true }) e.Cancel = true;
        CloseDialog();
        base.OnEndPrint(document, e);
    }

    private void CloseDialog()
    {
        var dialog = _dialog;
        _dialog = null;
        if (dialog == null) return;
        if (!dialog.IsDisposed) dialog.Close();
        dialog.Dispose();
    }

    private sealed class StatusForm : Form
    {
        private readonly Label _label;
        private readonly Button _cancel;

        public StatusForm(string title)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 96);
            _label = new Label
            {
                Location = new Point(12, 16),
                Size = new Size(276, 36),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _cancel = new Button { Text = "Cancel", Size = new Size(80, 26), Location = new Point(110, 60) };
            _cancel.Click += (_, _) =>
            {
                Canceled = true;
                _cancel.Enabled = false;
                _label.Text = "Canceling Print...";
            };
            Controls.Add(_label);
            Controls.Add(_cancel);
            CancelButton = _cancel;
        }

        public bool Canceled { get; private set; }

        public void UpdateLabel(int page, string documentName)
        {
            if (!Canceled) _label.Text = $"Page {page} of {documentName}";
        }
    }
}
