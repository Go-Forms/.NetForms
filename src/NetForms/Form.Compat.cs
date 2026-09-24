using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>
/// Form's WinForms surface that real projects use beyond the core (Ф6.К, decision 114): a form embedded
/// in a panel (<see cref="TopLevel"/> = false), the .NET 1.x events and scaling properties old designer
/// files still carry, owned forms, and the window-style properties NetForms keeps without a native
/// frame to apply them to.
/// </summary>
public partial class Form
{
    private bool _topLevel = true;

    /// <summary>
    /// False makes the form an ordinary child control - the way a form is put inside a panel
    /// (<c>form.TopLevel = false; panel.Controls.Add(form); form.Show();</c>). It then has no window of its
    /// own and no frame (an embedded WinForms form keeps its caption bar; set FormBorderStyle.None there).
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool TopLevel
    {
        get => GetTopLevel();
        set
        {
            if (!value && IsMdiContainer) throw new ArgumentException("An MDI parent form cannot be a child control.", nameof(value));
            if (value && IsMdiChild) throw new ArgumentException("An MDI child form cannot be a top-level form.", nameof(value));
            if (_topLevel == value) return;
            if (!value && IsWindowCreated) throw new InvalidOperationException("A shown top-level form cannot become a child control.");
            _topLevel = value;
        }
    }

    /// <summary>A child form (TopLevel = false, or MDI): shown and closed inside its parent, with no window.</summary>
    internal bool IsEmbedded => IsMdiChild || !_topLevel;

    // --- .NET 1.x scaling -------------------------------------------------------------------------------

    private Size _autoScaleBaseSize;

    /// <summary>The font-based scaling base of .NET 1.x designer files; kept (NetForms lays out at 96 DPI).</summary>
    [Localizable(true)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public virtual Size AutoScaleBaseSize
    {
        get => _autoScaleBaseSize.IsEmpty ? Size.Round(GetAutoScaleSize(Font)) : _autoScaleBaseSize;
        set => _autoScaleBaseSize = value;
    }

    [Category("Layout")]
    [Description("If set to true, the form will automatically scale with the screen font.")]
    [Obsolete("This property has been deprecated. Use the AutoScaleMode property instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool AutoScale
    {
        get => AutoScaleMode == AutoScaleMode.Font;
        set => AutoScaleMode = value ? AutoScaleMode.Font : AutoScaleMode.None;
    }

    /// <summary>The average character size of <paramref name="font"/>, as WinForms measures it for AutoScaleBaseSize.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method has been deprecated. Use the AutoScaleDimensions property instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
    public static SizeF GetAutoScaleSize(Font font)
    {
        float width = 9.0f;
        try
        {
            using var bitmap = new Bitmap(1, 1);
            using var graphics = Graphics.FromImage(bitmap);
            const string magicString = "The quick brown fox jumped over the lazy dog.";
            const double magicNumber = 44.549996948242189; // WinForms' constant: about magicString.Length
            width = (float)(graphics.MeasureString(magicString, font).Width / magicNumber);
        }
        catch (ArgumentException)
        {
        }
        return new SizeF(width, font.Height);
    }

    // --- .NET 1.x closing events --------------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs when the form is closing.")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Form.OnClosing, Form.OnClosed and the corresponding events are obsolete. Use Form.OnFormClosing, Form.OnFormClosed, Form.FormClosing and Form.FormClosed instead.", DiagnosticId = "WFDEV004", UrlFormat = "https://aka.ms/winforms-warnings/{0}")]
    public event CancelEventHandler? Closing;

    [Category("Behavior")]
    [Description("Occurs when the form is closed.")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Form.OnClosing, Form.OnClosed and the corresponding events are obsolete. Use Form.OnFormClosing, Form.OnFormClosed, Form.FormClosing and Form.FormClosed instead.", DiagnosticId = "WFDEV004", UrlFormat = "https://aka.ms/winforms-warnings/{0}")]
    public event EventHandler? Closed;

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Form.OnClosing, Form.OnClosed and the corresponding events are obsolete. Use Form.OnFormClosing, Form.OnFormClosed, Form.FormClosing and Form.FormClosed instead.", DiagnosticId = "WFDEV004", UrlFormat = "https://aka.ms/winforms-warnings/{0}")]
    protected virtual void OnClosing(CancelEventArgs e) => Closing?.Invoke(this, e);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Form.OnClosing, Form.OnClosed and the corresponding events are obsolete. Use Form.OnFormClosing, Form.OnFormClosed, Form.FormClosing and Form.FormClosed instead.", DiagnosticId = "WFDEV004", UrlFormat = "https://aka.ms/winforms-warnings/{0}")]
    protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);

    /// <summary>As WinForms: OnClosing, then OnFormClosing, with the same arguments.</summary>
    private void RaiseFormClosing(FormClosingEventArgs e)
    {
#pragma warning disable WFDEV004
        OnClosing(e);
#pragma warning restore WFDEV004
        OnFormClosing(e);
    }

    private void RaiseFormClosed(FormClosedEventArgs e)
    {
#pragma warning disable WFDEV004
        OnClosed(e);
#pragma warning restore WFDEV004
        OnFormClosed(e);
    }

    // --- owned forms --------------------------------------------------------------------------------------

    private readonly List<Form> _ownedForms = new();

    [Category("Window Style")]
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Array of forms owned by this form.")]
    public Form[] OwnedForms => _ownedForms.ToArray();

    public void AddOwnedForm(Form? ownedForm)
    {
        if (ownedForm == null) return;
        if (ownedForm.Owner != this)
        {
            ownedForm.Owner = this; // adds it here again, with the owner set
            return;
        }
        if (!_ownedForms.Contains(ownedForm)) _ownedForms.Add(ownedForm);
    }

    public void RemoveOwnedForm(Form? ownedForm)
    {
        if (ownedForm == null) return;
        if (ownedForm.Owner == this)
        {
            ownedForm.Owner = null; // removes it here again
            return;
        }
        _ownedForms.Remove(ownedForm);
    }

    // --- window style kept without a native frame to apply it to --------------------------------------------

    private SizeGripStyle _sizeGripStyle = SizeGripStyle.Auto;

    [Category("Window Style")]
    [DefaultValue(SizeGripStyle.Auto)]
    [Description("Determines when the SizeGrip will be displayed for the form.")]
    public SizeGripStyle SizeGripStyle
    {
        get => _sizeGripStyle;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(SizeGripStyle));
            _sizeGripStyle = value;
        }
    }

    [Category("Window Style")]
    [DefaultValue(false)]
    [Description("Determines whether a form has a Help button on the caption bar.")]
    public bool HelpButton { get; set; }

    [Browsable(true)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [Category("Behavior")]
    [Description("Event raised when the help button is clicked.")]
    public event CancelEventHandler? HelpButtonClicked;

    protected virtual void OnHelpButtonClicked(CancelEventArgs e) => HelpButtonClicked?.Invoke(this, e);

    [Category("Window Style")]
    [Description("A color which will appear transparent when painted on the form.")]
    public Color TransparencyKey { get; set; } = Color.Empty;

    internal bool ShouldSerializeTransparencyKey() => !TransparencyKey.IsEmpty;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Indicates whether the opacity of the control can be adjusted. ")]
    public bool AllowTransparency { get; set; }

    private Rectangle _maximizedBounds;

    protected Rectangle MaximizedBounds
    {
        get => _maximizedBounds;
        set
        {
            if (_maximizedBounds == value) return;
            _maximizedBounds = value;
            OnMaximizedBoundsChanged(EventArgs.Empty);
        }
    }

    [Category("Property Changed")]
    [Description("Event raised when the value of the MaximizedBounds property is changed on Form.")]
    public event EventHandler? MaximizedBoundsChanged;

    protected virtual void OnMaximizedBoundsChanged(EventArgs e) => MaximizedBoundsChanged?.Invoke(this, e);

    [Category("Property Changed")]
    [Description("Event raised when the value of the MaximumSize property is changed on Form.")]
    public event EventHandler? MaximumSizeChanged;

    protected virtual void OnMaximumSizeChanged(EventArgs e) => MaximumSizeChanged?.Invoke(this, e);

    [Category("Property Changed")]
    [Description("Event raised when the value of the MinimumSize property is changed on Form.")]
    public event EventHandler? MinimumSizeChanged;

    protected virtual void OnMinimumSizeChanged(EventArgs e) => MinimumSizeChanged?.Invoke(this, e);

    [Category("Behavior")]
    [Description("Occurs when any menu is displayed and menu modal message loop is entered.")]
    [Browsable(false)]
    public event EventHandler? MenuStart;

    [Category("Behavior")]
    [Description("Occurs when menu selection is completed and menu modal message loop has exited.")]
    [Browsable(false)]
    public event EventHandler? MenuComplete;

    protected virtual void OnMenuStart(EventArgs e) => MenuStart?.Invoke(this, e);

    protected virtual void OnMenuComplete(EventArgs e) => MenuComplete?.Invoke(this, e);

    private bool _rightToLeftLayout;

    [Category("Appearance")]
    [Localizable(true)]
    [DefaultValue(false)]
    [Description("Indicates whether the control layout is right-to-left when the RightToLeft property is set to Yes.")]
    public virtual bool RightToLeftLayout
    {
        get => _rightToLeftLayout;
        set
        {
            if (_rightToLeftLayout == value) return;
            _rightToLeftLayout = value;
            OnRightToLeftLayoutChanged(EventArgs.Empty);
        }
    }

    [Category("Property Changed")]
    [Description("Occurs when the value of the RightToLeftLayout property changes.")]
    public event EventHandler? RightToLeftLayoutChanged;

    protected virtual void OnRightToLeftLayoutChanged(EventArgs e) => RightToLeftLayoutChanged?.Invoke(this, e);

    // --- bounds on the desktop -----------------------------------------------------------------------------

    private Rectangle _restoreBounds = new(-1, -1, -1, -1);

    /// <summary>The bounds the form returns to from Maximized or Minimized (its current bounds while Normal).</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Browsable(false)]
    public Rectangle RestoreBounds => WindowState == FormWindowState.Normal || _restoreBounds.Width < 0 ? DesktopBounds : _restoreBounds;

    internal void RememberRestoreBounds() => _restoreBounds = DesktopBounds;

    public void SetDesktopLocation(int x, int y) => DesktopLocation = new Point(x, y);

    public void SetDesktopBounds(int x, int y, int width, int height) => SetBounds(x, y, width, height, BoundsSpecified.All);

    public void Show(IWin32Window? owner)
    {
        if (owner == this) throw new InvalidOperationException("A form cannot be its own owner.");
        if (owner is Form form) Owner = form;
        Show();
    }
}

public enum SizeGripStyle
{
    Auto = 0,
    Show = 1,
    Hide = 2,
}
