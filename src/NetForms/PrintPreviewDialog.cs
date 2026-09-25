using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;

namespace System.Windows.Forms;

/// <summary>
/// A form with a <see cref="PrintPreviewControl"/> and WinForms' tool bar: print, zoom, one to six pages, close,
/// and the page counter. The members the dialog hides from the designer are redeclared as dotnet/winforms does.
/// </summary>
[DesignTimeVisible(true)]
[DefaultProperty(nameof(Document))]
[ToolboxItemFilter("System.Windows.Forms.Control.TopLevel")]
[ToolboxItem(true)]
[Description("Displays a dialog box that shows a preview of a document to be printed.")]
public class PrintPreviewDialog : Form
{
    private readonly PrintPreviewControl _previewControl;
    private readonly ToolStrip _toolStrip;
    private readonly ToolStripButton _printButton;
    private readonly ToolStripSplitButton _zoomButton;
    private readonly ToolStripMenuItem[] _zoomItems;
    private readonly ToolStripButton[] _pagesButtons;
    private readonly ToolStripButton _closeButton;
    private readonly NumericUpDown _pageCounter;
    private readonly ToolStripControlHost _pageCounterItem;
    private readonly ToolStripLabel _pageLabel;

    private static readonly (string Text, double Zoom)[] s_zooms =
    [
        ("Auto", 0), ("500%", 5.0), ("250%", 2.5), ("150%", 1.5), ("100%", 1.0), ("75%", 0.75), ("50%", 0.5), ("25%", 0.25), ("10%", 0.1),
    ];

    private static readonly (string Text, int Rows, int Columns)[] s_layouts =
    [
        ("One page", 1, 1), ("Two pages", 1, 2), ("Three pages", 1, 3), ("Four pages", 2, 2), ("Six pages", 2, 3),
    ];

    public PrintPreviewDialog()
    {
        _previewControl = new PrintPreviewControl { Dock = DockStyle.Fill, TabIndex = 0 };
        _previewControl.StartPageChanged += (_, _) => _pageCounter!.Value = Math.Min(_pageCounter.Maximum, _previewControl.StartPage + 1);

        _toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, TabIndex = 1, Name = "toolStrip1" };
        _printButton = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, Image = ToolIcon(0), ToolTipText = "Print", Name = "printToolStripButton" };
        _printButton.Click += OnPrintToolStripButtonClick;

        _zoomButton = new ToolStripSplitButton { DisplayStyle = ToolStripItemDisplayStyle.Image, Image = ToolIcon(1), ToolTipText = "Zoom", Name = "zoomToolStripSplitButton" };
        _zoomItems = new ToolStripMenuItem[s_zooms.Length];
        for (int i = 0; i < s_zooms.Length; i++)
        {
            var (text, zoom) = s_zooms[i];
            var item = new ToolStripMenuItem(text) { CheckOnClick = true, Checked = i == 0 };
            item.Click += (_, _) =>
            {
                CheckZoomMenu(item);
                if (zoom == 0) _previewControl.AutoZoom = true;
                else _previewControl.Zoom = zoom;
            };
            _zoomItems[i] = item;
            _zoomButton.DropDownItems.Add(item);
        }
        _zoomButton.DefaultItem = _zoomItems[0];
        _zoomButton.ButtonClick += (_, _) =>
        {
            // The button itself steps back to "Auto", as WinForms' does.
            CheckZoomMenu(_zoomItems[0]);
            _previewControl.AutoZoom = true;
        };

        _pagesButtons = new ToolStripButton[s_layouts.Length];
        for (int i = 0; i < s_layouts.Length; i++)
        {
            var (text, rows, columns) = s_layouts[i];
            var button = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Image, Image = ToolIcon(2 + i), ToolTipText = text, Checked = i == 0 };
            button.Click += (_, _) => ShowPages(button, rows, columns);
            _pagesButtons[i] = button;
        }

        _closeButton = new ToolStripButton { DisplayStyle = ToolStripItemDisplayStyle.Text, Text = "&Close", Name = "closeToolStripButton" };
        _closeButton.Click += (_, _) => Close();

        _pageCounter = new NumericUpDown { TextAlign = HorizontalAlignment.Right, DecimalPlaces = 0, Minimum = 0, Maximum = 1000, Value = 1, Width = 50, Name = "pageCounter" };
        _pageCounter.ValueChanged += (_, _) =>
        {
            int page = (int)_pageCounter.Value - 1;
            if (page >= 0) _previewControl.StartPage = page;
            else _pageCounter.Value = _previewControl.StartPage + 1;
        };
        _pageCounterItem = new ToolStripControlHost(_pageCounter) { Alignment = ToolStripItemAlignment.Right };
        _pageLabel = new ToolStripLabel("Page") { Alignment = ToolStripItemAlignment.Right, Name = "pageToolStripLabel" };

        _toolStrip.Items.Add(_printButton);
        _toolStrip.Items.Add(_zoomButton);
        _toolStrip.Items.Add(new ToolStripSeparator());
        foreach (var button in _pagesButtons) _toolStrip.Items.Add(button);
        _toolStrip.Items.Add(new ToolStripSeparator());
        _toolStrip.Items.Add(_closeButton);
        _toolStrip.Items.Add(_pageCounterItem);
        _toolStrip.Items.Add(_pageLabel);

        Controls.Add(_previewControl);
        Controls.Add(_toolStrip);

        Text = "Print preview";
        ClientSize = new Size(400, 300);
        base.MinimizeBox = false;
        base.ShowInTaskbar = false;
        base.SizeGripStyle = SizeGripStyle.Hide;
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Specifies whether the print preview is anti-aliased.")]
    public bool UseAntiAlias
    {
        get => PrintPreviewControl.UseAntiAlias;
        set => PrintPreviewControl.UseAntiAlias = value;
    }

    [Category("Behavior")]
    [DefaultValue(null)]
    [Description("Specifies the document to preview.")]
    public PrintDocument? Document
    {
        get => _previewControl.Document;
        set => _previewControl.Document = value;
    }

    [Category("Behavior")]
    [Description("The PrintPreviewControl contained in this PrintPreviewDialog.")]
    [Browsable(false)]
    public PrintPreviewControl PrintPreviewControl => _previewControl;

    protected override Size DefaultMinimumSize => new(375, 250);

    /// <summary>The tool bar (for the tests).</summary>
    internal ToolStrip ToolStrip => _toolStrip;

    internal NumericUpDown PageCounter => _pageCounter;

#pragma warning disable CS0672, WFDEV004 // WinForms overrides the obsolete OnClosing here too
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        _previewControl.InvalidatePreview();
    }
#pragma warning restore CS0672, WFDEV004

    protected override bool ProcessDialogKey(Keys keyData)
    {
        Keys keyCode = keyData & Keys.KeyCode;
        if ((keyData & (Keys.Alt | Keys.Control)) == Keys.None)
        {
            if (keyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down) return false;
        }
        else if ((keyData & Keys.Control) == Keys.Control)
        {
            int index = keyCode switch { Keys.D1 => 0, Keys.D2 => 1, Keys.D3 => 2, Keys.D4 => 3, Keys.D5 => 4, _ => -1 };
            if (index >= 0)
            {
                _pagesButtons[index].PerformClick();
                return true;
            }
        }
        return base.ProcessDialogKey(keyData);
    }

    protected override bool ProcessTabKey(bool forward)
    {
        if (ActiveControl == _previewControl)
        {
            _pageCounter.Focus();
            return true;
        }
        return false;
    }

    internal override bool ShouldSerializeText() => !Text.Equals("Print preview");

    // These are redeclared without a default in WinForms, so a fresh dialog asks the designer for them: VS writes
    // AutoScrollMargin, AutoScrollMinSize, Enabled and Icon for every PrintPreviewDialog (checked by AttributeDiffTests).
    private bool ShouldSerializeAutoScrollMargin() => true;

    private bool ShouldSerializeAutoScrollMinSize() => true;

    private bool ShouldSerializeEnabled() => true;

    private bool ShouldSerializeIcon() => true;

    private void CheckZoomMenu(ToolStripMenuItem? toCheck)
    {
        foreach (var item in _zoomItems) item.Checked = toCheck == item;
    }

    private void ShowPages(ToolStripButton button, int rows, int columns)
    {
        foreach (var b in _pagesButtons) b.Checked = b == button;
        _previewControl.Rows = rows;
        _previewControl.Columns = columns;
    }

    private void OnPrintToolStripButtonClick(object? sender, EventArgs e) => _previewControl.Document?.Print();

    /// <summary>The tool bar's pictures (WinForms takes them from a resource strip): print, zoom, 1/2/3/4/6 pages.</summary>
    private static Bitmap ToolIcon(int index)
    {
        var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var ink = new Pen(Color.FromArgb(0x40, 0x40, 0x40));
        switch (index)
        {
            case 0: // printer
                g.FillRectangle(Brushes.White, 4, 1, 8, 5);
                g.DrawRectangle(ink, 4, 1, 8, 5);
                g.FillRectangle(Brushes.Silver, 1, 6, 14, 6);
                g.DrawRectangle(ink, 1, 6, 14, 6);
                g.FillRectangle(Brushes.White, 4, 10, 8, 5);
                g.DrawRectangle(ink, 4, 10, 8, 5);
                break;
            case 1: // magnifier
                g.FillEllipse(Brushes.White, 1, 1, 9, 9);
                g.DrawEllipse(ink, 1, 1, 9, 9);
                using (var handle = new Pen(Color.FromArgb(0x40, 0x40, 0x40), 2.5f)) g.DrawLine(handle, 9, 9, 14, 14);
                break;
            default:
                {
                    var (_, rows, columns) = s_layouts[index - 2];
                    float w = 14f / columns, h = 14f / rows;
                    for (int r = 0; r < rows; r++)
                    {
                        for (int c = 0; c < columns; c++)
                        {
                            var page = new RectangleF(1 + c * w, 1 + r * h, w - 1, h - 1);
                            g.FillRectangle(Brushes.White, page);
                            g.DrawRectangle(ink, page.X, page.Y, page.Width, page.Height);
                        }
                    }
                    break;
                }
        }
        return bitmap;
    }

    // Members hidden from the designer on this form (dotnet/winforms PrintPreviewDialog.cs).

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new IButtonControl? AcceptButton
    {
        get => base.AcceptButton;
        set => base.AcceptButton = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool AutoScale
    {
#pragma warning disable CS0618 // Type or member is obsolete
        get => base.AutoScale;
        set => base.AutoScale = value;
#pragma warning restore CS0618
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool AutoScroll
    {
        get => base.AutoScroll;
        set => base.AutoScroll = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set => base.AutoSize = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? AutoSizeChanged
    {
        add => base.AutoSizeChanged += value;
        remove => base.AutoSizeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override AutoValidate AutoValidate
    {
        get => base.AutoValidate;
        set => base.AutoValidate = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? AutoValidateChanged
    {
        add => base.AutoValidateChanged += value;
        remove => base.AutoValidateChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Color BackColor
    {
        get => base.BackColor;
        set => base.BackColor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackColorChanged
    {
        add => base.BackColorChanged += value;
        remove => base.BackColorChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new IButtonControl? CancelButton
    {
        get => base.CancelButton;
        set => base.CancelButton = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool ControlBox
    {
        get => base.ControlBox;
        set => base.ControlBox = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override ContextMenuStrip? ContextMenuStrip
    {
        get => base.ContextMenuStrip;
        set => base.ContextMenuStrip = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ContextMenuStripChanged
    {
        add => base.ContextMenuStripChanged += value;
        remove => base.ContextMenuStripChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new FormBorderStyle FormBorderStyle
    {
        get => base.FormBorderStyle;
        set => base.FormBorderStyle = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool HelpButton
    {
        get => base.HelpButton;
        set => base.HelpButton = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Icon? Icon
    {
        get => base.Icon;
        set => base.Icon = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool IsMdiContainer
    {
        get => base.IsMdiContainer;
        set => base.IsMdiContainer = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool KeyPreview
    {
        get => base.KeyPreview;
        set => base.KeyPreview = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Size MaximumSize
    {
        get => base.MaximumSize;
        set => base.MaximumSize = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? MaximumSizeChanged
    {
        add => base.MaximumSizeChanged += value;
        remove => base.MaximumSizeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool MaximizeBox
    {
        get => base.MaximizeBox;
        set => base.MaximizeBox = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Padding Margin
    {
        get => base.Margin;
        set => base.Margin = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? MarginChanged
    {
        add => base.MarginChanged += value;
        remove => base.MarginChanged -= value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Size MinimumSize
    {
        get => base.MinimumSize;
        set => base.MinimumSize = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? MinimumSizeChanged
    {
        add => base.MinimumSizeChanged += value;
        remove => base.MinimumSizeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Padding Padding
    {
        get => base.Padding;
        set => base.Padding = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? PaddingChanged
    {
        add => base.PaddingChanged += value;
        remove => base.PaddingChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Size Size
    {
        get => base.Size;
        set => base.Size = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? SizeChanged
    {
        add => base.SizeChanged += value;
        remove => base.SizeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new FormStartPosition StartPosition
    {
        get => base.StartPosition;
        set => base.StartPosition = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool TopMost
    {
        get => base.TopMost;
        set => base.TopMost = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Color TransparencyKey
    {
        get => base.TransparencyKey;
        set => base.TransparencyKey = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool UseWaitCursor
    {
        get => base.UseWaitCursor;
        set => base.UseWaitCursor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new FormWindowState WindowState
    {
        get => base.WindowState;
        set => base.WindowState = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new AccessibleRole AccessibleRole
    {
        get => base.AccessibleRole;
        set => base.AccessibleRole = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new string? AccessibleDescription
    {
        get => base.AccessibleDescription;
        set => base.AccessibleDescription = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new string? AccessibleName
    {
        get => base.AccessibleName;
        set => base.AccessibleName = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool CausesValidation
    {
        get => base.CausesValidation;
        set => base.CausesValidation = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? CausesValidationChanged
    {
        add => base.CausesValidationChanged += value;
        remove => base.CausesValidationChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new ControlBindingsCollection DataBindings => base.DataBindings;

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool Enabled
    {
        get => base.Enabled;
        set => base.Enabled = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? EnabledChanged
    {
        add => base.EnabledChanged += value;
        remove => base.EnabledChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public new Point Location
    {
        get => base.Location;
        set => base.Location = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? LocationChanged
    {
        add => base.LocationChanged += value;
        remove => base.LocationChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new object? Tag
    {
        get => base.Tag;
        set => base.Tag = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool AllowDrop
    {
        get => base.AllowDrop;
        set => base.AllowDrop = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Cursor Cursor
    {
        get => base.Cursor;
        set => base.Cursor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? CursorChanged
    {
        add => base.CursorChanged += value;
        remove => base.CursorChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Image? BackgroundImage
    {
        get => base.BackgroundImage;
        set => base.BackgroundImage = value;
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
    public override ImageLayout BackgroundImageLayout
    {
        get => base.BackgroundImageLayout;
        set => base.BackgroundImageLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? BackgroundImageLayoutChanged
    {
        add => base.BackgroundImageLayoutChanged += value;
        remove => base.BackgroundImageLayoutChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new ImeMode ImeMode
    {
        get => base.ImeMode;
        set => base.ImeMode = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ImeModeChanged
    {
        add => base.ImeModeChanged += value;
        remove => base.ImeModeChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Size AutoScrollMargin
    {
        get => base.AutoScrollMargin;
        set => base.AutoScrollMargin = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new Size AutoScrollMinSize
    {
        get => base.AutoScrollMinSize;
        set => base.AutoScrollMinSize = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override AnchorStyles Anchor
    {
        get => base.Anchor;
        set => base.Anchor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool Visible
    {
        get => base.Visible;
        set => base.Visible = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? VisibleChanged
    {
        add => base.VisibleChanged += value;
        remove => base.VisibleChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Color ForeColor
    {
        get => base.ForeColor;
        set => base.ForeColor = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? ForeColorChanged
    {
        add => base.ForeColorChanged += value;
        remove => base.ForeColorChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override RightToLeft RightToLeft
    {
        get => base.RightToLeft;
        set => base.RightToLeft = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool RightToLeftLayout
    {
        get => base.RightToLeftLayout;
        set => base.RightToLeftLayout = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? RightToLeftChanged
    {
        add => base.RightToLeftChanged += value;
        remove => base.RightToLeftChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? RightToLeftLayoutChanged
    {
        add => base.RightToLeftLayoutChanged += value;
        remove => base.RightToLeftLayoutChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool TabStop
    {
        get => base.TabStop;
        set => base.TabStop = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TabStopChanged
    {
        add => base.TabStopChanged += value;
        remove => base.TabStopChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string Text
    {
        get => base.Text;
        set => base.Text = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? TextChanged
    {
        add => base.TextChanged += value;
        remove => base.TextChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override DockStyle Dock
    {
        get => base.Dock;
        set => base.Dock = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? DockChanged
    {
        add => base.DockChanged += value;
        remove => base.DockChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override Font Font
    {
        get => base.Font;
        set => base.Font = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new event EventHandler? FontChanged
    {
        add => base.FontChanged += value;
        remove => base.FontChanged -= value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new DockPaddingEdges DockPadding => base.DockPadding;

#pragma warning disable 0809
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This property has been deprecated. Use the AutoScaleDimensions property instead.  https://go.microsoft.com/fwlink/?linkid=14202")]
    public override Size AutoScaleBaseSize
    {
        get => base.AutoScaleBaseSize;
        set
        {
            // No-op
        }
    }

    [Browsable(false)]
    [DefaultValue(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool MinimizeBox
    {
        get => base.MinimizeBox;
        set => base.MinimizeBox = value;
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public new double Opacity
    {
        get => base.Opacity;
        set => base.Opacity = value;
    }

    [Browsable(false)]
    [DefaultValue(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new bool ShowInTaskbar
    {
        get => base.ShowInTaskbar;
        set => base.ShowInTaskbar = value;
    }

    [Browsable(false)]
    [DefaultValue(SizeGripStyle.Hide)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public new SizeGripStyle SizeGripStyle
    {
        get => base.SizeGripStyle;
        set => base.SizeGripStyle = value;
    }
}
