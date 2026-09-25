using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;

namespace System.Windows.Forms;

/// <summary>
/// Shows the pages of a <see cref="PrintDocument"/> as they will print. The document is "printed" through a
/// <see cref="PreviewPrintController"/>; the pages are recorded drawings, replayed sharp at any zoom. Layout
/// (zoom, rows, columns, the 10/100-inch border, centring) follows dotnet/winforms' PrintPreviewControl.
/// </summary>
[DefaultProperty(nameof(Document))]
[Description("Displays a preview of a document to be printed.")]
public class PrintPreviewControl : Control
{
    private const double DefaultZoom = .3;
    // Spacing around each page, in hundredths of an inch.
    private const int Border = 10;
    private const int Dpi = 96;

    private PrintDocument? _document;
    private PreviewPageInfo[]? _pageInfo; // null: needs a new preview print
    private int _startPage;
    private int _rows = 1;
    private int _columns = 1;
    private bool _autoZoom = true;
    private double _zoom = DefaultZoom;
    private Size _virtualSize = new(1, 1);
    private bool _layoutOk;
    private bool _exceptionPrinting;
    private bool _pageInfoCalcPending;
    private bool _isForeColorSet;
    private bool _hVisible;
    private bool _vVisible;
    private Point _position;
    private readonly ScrollBarCore _hscroll;
    private readonly ScrollBarCore _vscroll;

    public PrintPreviewControl()
    {
        ResetBackColor();
        ResetForeColor();
        Size = new Size(100, 100);
        SetStyle(ControlStyles.ResizeRedraw, false);
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        TabStop = false;
        _hscroll = new ScrollBarCore(this, vertical: false, (v, _) => ScrollTo(v, _position.Y));
        _vscroll = new ScrollBarCore(this, vertical: true, (v, _) => ScrollTo(_position.X, v));
    }

    [Category("Behavior")]
    [DefaultValue(false)]
    [Description("Specifies whether the print preview is anti-aliased.")]
    public bool UseAntiAlias { get; set; }

    [Category("Behavior")]
    [DefaultValue(true)]
    [Description("If true, zoom automatically adjusts when the control is resized.")]
    public bool AutoZoom
    {
        get => _autoZoom;
        set
        {
            if (_autoZoom == value) return;
            _autoZoom = value;
            InvalidateLayout();
        }
    }

    [Category("Behavior")]
    [DefaultValue(DefaultZoom)]
    [Description("Specifies the magnification of the page, with 1.0 as full size.")]
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (value <= 0) throw new ArgumentException("Zoom must be 0 or greater. Negative values are not permitted.");
            _autoZoom = false;
            _zoom = value;
            InvalidateLayout();
        }
    }

    [Category("Behavior")]
    [DefaultValue(null)]
    [Description("Specifies the document to preview.")]
    public PrintDocument? Document
    {
        get => _document;
        set
        {
            _document = value;
            InvalidatePreview();
        }
    }

    [Category("Behavior")]
    [DefaultValue(1)]
    [Description("Specifies the number of pages displayed vertically on the screen.")]
    public int Rows
    {
        get => _rows;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _rows = value;
            InvalidateLayout();
        }
    }

    [Category("Layout")]
    [DefaultValue(1)]
    [Description("Specifies the number of pages displayed horizontally on the screen.")]
    public int Columns
    {
        get => _columns;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            _columns = value;
            InvalidateLayout();
        }
    }

    [Category("Behavior")]
    [DefaultValue(0)]
    [Description("Specifies the page number displayed in the upper left corner.")]
    public int StartPage
    {
        get
        {
            int value = _startPage;
            if (_pageInfo != null) value = Math.Min(value, _pageInfo.Length - (_rows * _columns));
            return Math.Max(value, 0);
        }
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            int oldValue = StartPage;
            _startPage = value;
            if (oldValue != _startPage)
            {
                InvalidateLayout();
                OnStartPageChanged(EventArgs.Empty);
            }
        }
    }

    [Category("Property Changed")]
    [Description("Occurs when the start page is changed.")]
    public event EventHandler? StartPageChanged;

    protected virtual void OnStartPageChanged(EventArgs e) => StartPageChanged?.Invoke(this, e);

    [Category("Appearance")]
    [Localizable(true)]
    [AmbientValue(RightToLeft.Inherit)]
    [Description("Indicates whether the component should draw right-to-left for RTL languages.")]
    public override RightToLeft RightToLeft
    {
        get => base.RightToLeft;
        set
        {
            base.RightToLeft = value;
            InvalidatePreview();
        }
    }

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Bindable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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

    [DefaultValue(false)]
    public new bool TabStop
    {
        get => base.TabStop;
        set => base.TabStop = value;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ResetBackColor() => BackColor = SystemColors.AppWorkspace;

    internal override bool ShouldSerializeBackColor() => !BackColor.Equals(SystemColors.AppWorkspace);

    public override Color ForeColor
    {
        get => base.ForeColor;
        set
        {
            _isForeColorSet = true;
            base.ForeColor = value;
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override void ResetForeColor()
    {
        ForeColor = SystemColors.ControlText;
        _isForeColorSet = false;
    }

    internal override bool ShouldSerializeForeColor() => _isForeColorSet;

    protected override AccessibleObject CreateAccessibilityInstance() => new ControlAccessibleObject(this);

    /// <summary>The pages of the last preview print; null until one has run.</summary>
    internal PreviewPageInfo[]? PageInfo => _pageInfo;

    /// <summary>Where each shown page is drawn, in client coordinates (after the layout of the last paint).</summary>
    internal Rectangle[] PageRectangles { get; private set; } = [];

    /// <summary>Throw away the pages and print the document to the preview again.</summary>
    public void InvalidatePreview()
    {
        _pageInfo = null;
        _exceptionPrinting = false;
        _virtualSize = Size.Empty;
        InvalidateLayout();
    }

    private void InvalidateLayout()
    {
        _layoutOk = false;
        Invalidate();
    }

    protected override void OnResize(EventArgs eventargs)
    {
        InvalidateLayout();
        base.OnResize(eventargs);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Focused) Focus();
        if (e.Button == MouseButtons.Left)
        {
            if (_vVisible && _vscroll.Bounds.Contains(e.Location)) _vscroll.MouseDown(e.Location);
            else if (_hVisible && _hscroll.Bounds.Contains(e.Location)) _hscroll.MouseDown(e.Location);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool left = (e.Button & MouseButtons.Left) != 0;
        if (_vVisible) _vscroll.MouseMove(e.Location, left);
        if (_hVisible) _hscroll.MouseMove(e.Location, left);
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_vVisible) _vscroll.MouseUp(e.Location);
        if (_hVisible) _hscroll.MouseUp(e.Location);
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_vVisible) ScrollTo(_position.X, _position.Y - e.Delta / 120 * 3 * 16);
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) switch
    {
        Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End => true,
        _ => base.IsInputKey(keyData),
    };

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // As WinForms' WndProc (WM_KEYDOWN): arrows scroll, Page Up/Down move a page, Home/End go to the ends.
        int pages = _pageInfo?.Length ?? 0;
        switch (e.KeyCode)
        {
            case Keys.PageUp:
                if (e.Control) ScrollTo(_position.X, 0);
                else if (StartPage > 0) StartPage--;
                break;
            case Keys.PageDown:
                if (e.Control) ScrollTo(_position.X, _virtualSize.Height);
                else if (StartPage < pages) StartPage++;
                break;
            case Keys.Home:
                if (e.Control) StartPage = 0;
                break;
            case Keys.End:
                if (e.Control) StartPage = pages;
                break;
            case Keys.Up:
                ScrollTo(_position.X, _position.Y - 5);
                break;
            case Keys.Down:
                ScrollTo(_position.X, _position.Y + 5);
                break;
            case Keys.Left:
                ScrollTo(_position.X - 5, _position.Y);
                break;
            case Keys.Right:
                ScrollTo(_position.X + 5, _position.Y);
                break;
            default:
                base.OnKeyDown(e);
                return;
        }
        e.Handled = true;
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using var brush = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        using var backBrush = new SolidBrush(BackColor);
        if (_pageInfo == null && !_exceptionPrinting) CalculatePageInfo();

        if (_pageInfo == null || _pageInfo.Length == 0)
        {
            PageRectangles = [];
            var rect = InsideRectangle;
            pevent.Graphics.FillRectangle(backBrush, rect);
            DrawMessage(pevent.Graphics, rect, _exceptionPrinting);
        }
        else
        {
            if (!_layoutOk) ComputeLayout();
            DrawPages(pevent.Graphics, InsideRectangle, _pageInfo, backBrush);
        }

        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(pevent.Graphics, new Rectangle(0, 0, Width - 1, Height - 1));
        base.OnPaint(pevent);
    }

    internal override void OnPaintOverlay(Graphics g)
    {
        if (_hVisible) _hscroll.Paint(g);
        if (_vVisible) _vscroll.Paint(g);
        if (_hVisible && _vVisible)
            g.FillRectangle(SystemBrushes.Control, _vscroll.Bounds.X, _hscroll.Bounds.Y, ScrollBarCore.Thickness, ScrollBarCore.Thickness);
        base.OnPaintOverlay(g);
    }

    internal override bool IsOverlayPoint(Point p) =>
        (_vVisible && _vscroll.Bounds.Contains(p)) || (_hVisible && _hscroll.Bounds.Contains(p));

    private Rectangle InnerClientRectangle
    {
        get
        {
            var rect = ClientRectangle;
            rect.Inflate(-SystemInformation.HorizontalFocusThickness, -SystemInformation.VerticalFocusThickness);
            rect.Width = Math.Max(0, rect.Width - 1);
            rect.Height = Math.Max(0, rect.Height - 1);
            return rect;
        }
    }

    private Rectangle InsideRectangle
    {
        get
        {
            var rect = InnerClientRectangle;
            if (_hVisible) rect.Height -= ScrollBarCore.Thickness;
            if (_vVisible)
            {
                rect.Width -= ScrollBarCore.Thickness;
                if (RightToLeft == RightToLeft.Yes) rect.X += ScrollBarCore.Thickness;
            }
            return rect;
        }
    }

    /// <summary>Run the preview print now (WinForms does it on the first paint after the pages were invalidated).</summary>
    private void CalculatePageInfo()
    {
        if (_pageInfoCalcPending || _pageInfo != null) return;
        _pageInfoCalcPending = true;
        try
        {
            ComputePreview();
        }
        catch
        {
            _exceptionPrinting = true;
            throw;
        }
        finally
        {
            _pageInfoCalcPending = false;
        }
    }

    private void ComputePreview()
    {
        int oldStart = StartPage;
        if (_document == null)
        {
            _pageInfo = [];
        }
        else
        {
            var oldController = _document.PrintController;
            var previewController = new PreviewPrintController { UseAntiAlias = UseAntiAlias };
            _document.PrintController = new PrintControllerWithStatusDialog(previewController, "Generating Previews");
            try
            {
                _document.Print();
            }
            finally
            {
                _document.PrintController = oldController;
            }
            _pageInfo = previewController.GetPreviewPageInfo();
        }
        _layoutOk = false;
        if (oldStart != StartPage) OnStartPageChanged(EventArgs.Empty);
    }

    /// <summary>The zoom, the page size and the virtual size, from physical sizes (hundredths of an inch).</summary>
    private void ComputeLayout()
    {
        _layoutOk = true;
        if (_pageInfo == null || _pageInfo.Length == 0) return;
        var pageSize = _pageInfo[StartPage].PhysicalSize;
        var controlPhysicalSize = PixelsToPhysical(Size);
        if (_autoZoom)
        {
            double zoomX = ((double)controlPhysicalSize.Width - Border * (_columns + 1)) / (_columns * pageSize.Width);
            double zoomY = ((double)controlPhysicalSize.Height - Border * (_rows + 1)) / (_rows * pageSize.Height);
            _zoom = Math.Max(0.01, Math.Min(zoomX, zoomY));
        }
        var imageSize = new Size((int)(_zoom * pageSize.Width), (int)(_zoom * pageSize.Height));
        int virtualX = imageSize.Width * _columns + Border * (_columns + 1);
        int virtualY = imageSize.Height * _rows + Border * (_rows + 1);
        _virtualSize = PhysicalToPixels(new Size(virtualX, virtualY));
        LayoutScrollBars();
    }

    private void LayoutScrollBars()
    {
        var available = InnerClientRectangle;
        bool h = _virtualSize.Width > available.Width && available.Width > ScrollBarCore.Thickness;
        bool v = _virtualSize.Height > available.Height && available.Height > ScrollBarCore.Thickness;
        if (!h && v) h = _virtualSize.Width > available.Width - ScrollBarCore.Thickness;
        if (!v && h) v = _virtualSize.Height > available.Height - ScrollBarCore.Thickness;
        _hVisible = h;
        _vVisible = v;

        int viewW = available.Width - (v ? ScrollBarCore.Thickness : 0);
        int viewH = available.Height - (h ? ScrollBarCore.Thickness : 0);
        _hscroll.Minimum = 0;
        _hscroll.Maximum = Math.Max(0, _virtualSize.Width - 1);
        _hscroll.LargeChange = Math.Max(1, viewW);
        _hscroll.SmallChange = 5;
        _hscroll.Bounds = new Rectangle(available.X, available.Bottom - ScrollBarCore.Thickness, Math.Max(0, viewW), ScrollBarCore.Thickness);
        _vscroll.Minimum = 0;
        _vscroll.Maximum = Math.Max(0, _virtualSize.Height - 1);
        _vscroll.LargeChange = Math.Max(1, viewH);
        _vscroll.SmallChange = 5;
        int vx = RightToLeft == RightToLeft.Yes ? available.X : available.Right - ScrollBarCore.Thickness;
        _vscroll.Bounds = new Rectangle(vx, available.Y, ScrollBarCore.Thickness, Math.Max(0, viewH));

        _position = new Point(
            h ? Math.Clamp(_position.X, 0, Math.Max(0, _virtualSize.Width - viewW)) : 0,
            v ? Math.Clamp(_position.Y, 0, Math.Max(0, _virtualSize.Height - viewH)) : 0);
        _hscroll.Value = _position.X;
        _vscroll.Value = _position.Y;
    }

    private void ScrollTo(int x, int y)
    {
        var inside = InsideRectangle;
        var next = new Point(
            _hVisible ? Math.Clamp(x, 0, Math.Max(0, _virtualSize.Width - inside.Width)) : 0,
            _vVisible ? Math.Clamp(y, 0, Math.Max(0, _virtualSize.Height - inside.Height)) : 0);
        if (next == _position) return;
        _position = next;
        _hscroll.Value = next.X;
        _vscroll.Value = next.Y;
        Invalidate();
    }

    private void DrawMessage(Graphics g, Rectangle rect, bool isExceptionPrinting)
    {
        using var brush = new SolidBrush(_isForeColorSet ? ForeColor : SystemColors.ControlText);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        string message = isExceptionPrinting ? "Document cannot be displayed." : "Document does not contain any pages.";
        g.DrawString(message, Font, brush, rect, format);
    }

    private void DrawPages(Graphics g, Rectangle rect, PreviewPageInfo[] pages, Brush backBrush)
    {
        var state = g.Save();
        g.IntersectClip(rect);

        var controlPhysicalSize = PixelsToPhysical(rect.Size);
        // Centre the pages when they are smaller than the view.
        var offset = new Point(
            Math.Max(0, (rect.Width - _virtualSize.Width) / 2) - _position.X + rect.X,
            Math.Max(0, (rect.Height - _virtualSize.Height) / 2) - _position.Y + rect.Y);
        int borderPixelsX = PhysicalToPixels(Border);
        int borderPixelsY = PhysicalToPixels(Border);
        var areas = new Rectangle[_rows * _columns];
        int maxImageHeight = 0;
        for (int row = 0; row < _rows; row++)
        {
            int lastX = 0;
            int lastY = maxImageHeight * row;
            for (int column = 0; column < _columns; column++)
            {
                int imageIndex = StartPage + column + row * _columns;
                if (imageIndex >= pages.Length) continue;
                var pageSize = pages[imageIndex].PhysicalSize;
                if (_autoZoom)
                {
                    double zoomX = ((double)controlPhysicalSize.Width - Border * (_columns + 1)) / (_columns * pageSize.Width);
                    double zoomY = ((double)controlPhysicalSize.Height - Border * (_rows + 1)) / (_rows * pageSize.Height);
                    _zoom = Math.Max(0.01, Math.Min(zoomX, zoomY));
                }
                var imagePixels = PhysicalToPixels(new Size((int)(_zoom * pageSize.Width), (int)(_zoom * pageSize.Height)));
                int x = offset.X + borderPixelsX * (column + 1) + lastX;
                int y = offset.Y + borderPixelsY * (row + 1) + lastY;
                lastX += imagePixels.Width;
                maxImageHeight = Math.Max(maxImageHeight, imagePixels.Height);
                areas[imageIndex - StartPage] = new Rectangle(x, y, imagePixels.Width, imagePixels.Height);
            }
        }
        g.FillRectangle(backBrush, rect);
        PageRectangles = areas;

        for (int i = 0; i < areas.Length; i++)
        {
            if (i + StartPage >= pages.Length) continue;
            var box = areas[i];
            // White paper; an explicitly set ForeColor is the paper colour, as in WinForms.
            using (var paper = new SolidBrush(_isForeColorSet ? ForeColor : Color.White)) g.FillRectangle(paper, box);
            g.DrawRectangle(Pens.Black, box);
            box.Inflate(-1, -1);
            DrawPage(g, pages[i + StartPage], box);
            box.Width--;
            box.Height--;
            g.DrawRectangle(Pens.Black, box);
        }
        g.Restore(state);
    }

    private static void DrawPage(Graphics g, PreviewPageInfo page, Rectangle box)
    {
        var image = page.Image;
        if (image == null || box.Width <= 0 || box.Height <= 0) return;
        if (image.Picture is { } picture && page.PhysicalSize.Width > 0 && page.PhysicalSize.Height > 0)
        {
            // Replay the recorded page at this zoom: text and lines stay sharp.
            var canvas = g.Canvas;
            canvas.Save();
            canvas.ClipRect(new SkiaSharp.SKRect(box.X, box.Y, box.Right, box.Bottom));
            canvas.Translate(box.X, box.Y);
            canvas.Scale((float)box.Width / page.PhysicalSize.Width, (float)box.Height / page.PhysicalSize.Height);
            canvas.DrawPicture(picture);
            canvas.Restore();
            return;
        }
        g.DrawImage(image, box);
    }

    private static int PixelsToPhysical(int pixels) => (int)(pixels * 100.0 / Dpi);

    private static Size PixelsToPhysical(Size pixels) => new(PixelsToPhysical(pixels.Width), PixelsToPhysical(pixels.Height));

    private static int PhysicalToPixels(int physical) => (int)(physical * Dpi / 100.0);

    private static Size PhysicalToPixels(Size physical) => new(PhysicalToPixels(physical.Width), PhysicalToPixels(physical.Height));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hscroll.Dispose();
            _vscroll.Dispose();
        }
        base.Dispose(disposing);
    }
}
