using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace System.Windows.Forms;

/// <summary>
/// The rest of Control's WinForms surface that real projects reach for (Ф6.К, decision 114): the
/// background image, PreviewKeyDown, the drag-and-drop and help events, scaling, the typed Invoke
/// overloads - and the Win32 hooks (<see cref="WndProc"/>, <see cref="CreateParams"/>, handles), which
/// exist so that code overriding them compiles: a NetForms control has no window procedure, so they are
/// not called by the platform.
/// </summary>
public partial class Control
{
    // --- background image -----------------------------------------------------------------------------

    private Image? _backgroundImage;
    private ImageLayout _backgroundImageLayout = ImageLayout.Tile;

    [Category("Appearance")]
    [DefaultValue(null)]
    [Localizable(true)]
    [Description("The background image used for the control.")]
    public virtual Image? BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (_backgroundImage == value) return;
            _backgroundImage = value;
            OnBackgroundImageChanged(EventArgs.Empty);
        }
    }

    [Category("Appearance")]
    [DefaultValue(ImageLayout.Tile)]
    [Localizable(true)]
    [Description("The background image layout used for the component.")]
    public virtual ImageLayout BackgroundImageLayout
    {
        get => _backgroundImageLayout;
        set
        {
            if (!Enum.IsDefined(value)) throw new InvalidEnumArgumentException(nameof(value), (int)value, typeof(ImageLayout));
            if (_backgroundImageLayout == value) return;
            _backgroundImageLayout = value;
            OnBackgroundImageLayoutChanged(EventArgs.Empty);
        }
    }

    [Category("Property Changed")]
    [Description("Event raised when the value of the BackgroundImage property is changed on Control.")]
    public event EventHandler? BackgroundImageChanged;

    [Category("Property Changed")]
    [Description("Event raised when the value of the BackgroundImageLayout property is changed on Control.")]
    public event EventHandler? BackgroundImageLayoutChanged;

    protected virtual void OnBackgroundImageChanged(EventArgs e)
    {
        Invalidate();
        BackgroundImageChanged?.Invoke(this, e);
        if (_controls != null)
            foreach (var c in _controls) c.OnParentBackgroundImageChanged(e);
    }

    protected virtual void OnBackgroundImageLayoutChanged(EventArgs e)
    {
        Invalidate();
        BackgroundImageLayoutChanged?.Invoke(this, e);
    }

    protected virtual void OnParentBackgroundImageChanged(EventArgs e) => OnBackgroundImageChanged(e);

    /// <summary>
    /// ControlPaint.DrawBackgroundImage of WinForms: Tile repeats the image from the display origin (it
    /// scrolls with an AutoScroll form); None, Center, Stretch and Zoom fill the back color first - the
    /// image may be transparent or not cover the control.
    /// </summary>
    internal void PaintBackgroundImage(Graphics g, Image image, Color backColor, Rectangle clip)
    {
        var bounds = ClientRectangle;
        if (_backgroundImageLayout != ImageLayout.Tile || image.Width <= 0 || image.Height <= 0)
        {
            if (backColor.A != 0)
            {
                using var brush = new SolidBrush(backColor);
                g.FillRectangle(brush, clip);
            }
            if (image.Width <= 0 || image.Height <= 0) return;
            var target = CalculateBackgroundImageRectangle(bounds, image.Size, _backgroundImageLayout);
            if (RightToLeft == RightToLeft.Yes && _backgroundImageLayout == ImageLayout.None) target.X += clip.Width - target.Width;
            g.DrawImage(image, target);
            return;
        }

        if (backColor.A != 0)
        {
            using var brush = new SolidBrush(backColor);
            g.FillRectangle(brush, clip);
        }
        var origin = this is ScrollableControl scrollable ? scrollable.AutoScrollPosition : Point.Empty;
        int startX = origin.X + (int)Math.Floor((clip.Left - origin.X) / (double)image.Width) * image.Width;
        int startY = origin.Y + (int)Math.Floor((clip.Top - origin.Y) / (double)image.Height) * image.Height;
        var state = g.Save();
        g.SetClip(clip);
        for (int y = startY; y < clip.Bottom; y += image.Height)
            for (int x = startX; x < clip.Right; x += image.Width)
                g.DrawImage(image, x, y, image.Width, image.Height);
        g.Restore(state);
    }

    /// <summary>Where a background image of <paramref name="imageSize"/> goes in <paramref name="bounds"/> (ControlPaint of WinForms).</summary>
    internal static Rectangle CalculateBackgroundImageRectangle(Rectangle bounds, Size imageSize, ImageLayout imageLayout)
    {
        var result = bounds;
        switch (imageLayout)
        {
            case ImageLayout.Stretch:
                result.Size = bounds.Size;
                break;
            case ImageLayout.None:
                result.Size = imageSize;
                break;
            case ImageLayout.Center:
                result.Size = imageSize;
                if (bounds.Width > result.Width) result.X = (bounds.Width - result.Width) / 2;
                if (bounds.Height > result.Height) result.Y = (bounds.Height - result.Height) / 2;
                break;
            case ImageLayout.Zoom:
                float xRatio = bounds.Width / (float)imageSize.Width;
                float yRatio = bounds.Height / (float)imageSize.Height;
                if (xRatio < yRatio)
                {
                    result.Width = bounds.Width;
                    result.Height = (int)((imageSize.Height * xRatio) + .5);
                    if (bounds.Y >= 0) result.Y = (bounds.Height - result.Height) / 2;
                }
                else
                {
                    result.Height = bounds.Height;
                    result.Width = (int)((imageSize.Width * yRatio) + .5);
                    if (bounds.X >= 0) result.X = (bounds.Width - result.Width) / 2;
                }
                break;
        }
        return result;
    }

    // --- keyboard ---------------------------------------------------------------------------------------

    [Category("Key")]
    [Description("Occurs before the KeyDown event when a key is pressed while focus is on this control.")]
    public event PreviewKeyDownEventHandler? PreviewKeyDown;

    protected virtual void OnPreviewKeyDown(PreviewKeyDownEventArgs e) => PreviewKeyDown?.Invoke(this, e);

    /// <summary>First of a key press, before menus, dialog keys and KeyDown; true when the control takes the key as input.</summary>
    internal bool RaisePreviewKeyDown(Keys keyData)
    {
        var e = new PreviewKeyDownEventArgs(keyData);
        OnPreviewKeyDown(e);
        return e.IsInputKey;
    }

    protected virtual bool IsInputChar(char charCode) => false;

    protected virtual bool ProcessDialogChar(char charCode) => _parent?.ProcessDialogChar(charCode) ?? false;

    protected internal virtual bool ProcessMnemonic(char charCode) => false;

    protected virtual bool ProcessKeyPreview(ref Message m) => _parent?.ProcessKeyPreview(ref m) ?? false;

    protected internal virtual bool ProcessKeyMessage(ref Message m) =>
        (_parent != null && _parent.ProcessKeyPreview(ref m)) || ProcessKeyEventArgs(ref m);

    /// <summary>Win32 message pre-processing; NetForms routes keys itself (Form), so this is not called.</summary>
    public virtual bool PreProcessMessage(ref Message msg) => false;

    public PreProcessControlState PreProcessControlMessage(ref Message msg) => PreProcessControlState.MessageNotNeeded;

    /// <summary>Whether <paramref name="charCode"/> is the mnemonic of <paramref name="text"/> ("&amp;File" and 'f').</summary>
    public static bool IsMnemonic(char charCode, string? text)
    {
        if (charCode == '&' || string.IsNullOrEmpty(text)) return false;
        int pos = -1;
        char upper = char.ToUpper(charCode, System.Globalization.CultureInfo.CurrentCulture);
        for (; ; )
        {
            if (pos + 1 >= text.Length) break;
            pos = text.IndexOf('&', pos + 1) + 1;
            if (pos <= 0 || pos >= text.Length) break;
            char c = char.ToUpper(text[pos], System.Globalization.CultureInfo.CurrentCulture);
            if (c == upper || char.ToLower(c, System.Globalization.CultureInfo.CurrentCulture) == char.ToLower(upper, System.Globalization.CultureInfo.CurrentCulture)) return true;
            if (c == '&') continue; // "&&" is a literal ampersand
        }
        return false;
    }

    /// <summary>The lock keys' state. The platform layer does not report it yet, so this is false (decision 114).</summary>
    public static bool IsKeyLocked(Keys keyVal)
    {
        if (keyVal is not (Keys.Insert or Keys.NumLock or Keys.CapsLock or Keys.Scroll))
            throw new NotSupportedException("Specified key is not supported.");
        return false;
    }

    // --- the native window, which NetForms controls do not have ------------------------------------------

    protected virtual CreateParams CreateParams => new()
    {
        Caption = Text,
        X = _x,
        Y = _y,
        Width = _width,
        Height = _height,
    };

    protected virtual void CreateHandle() { }

    protected virtual void DestroyHandle() { }

    protected void RecreateHandle() { }

    protected void UpdateStyles() => OnStyleChanged(EventArgs.Empty);

    /// <summary>The window procedure of a Win32 control. NetForms controls receive no window messages; overrides are not called.</summary>
    protected virtual void WndProc(ref Message m) => DefWndProc(ref m);

    protected virtual void DefWndProc(ref Message m) { }

    protected virtual void OnNotifyMessage(Message m) { }

    protected static bool ReflectMessage(IntPtr hWnd, ref Message m) => false;

    public static Control? FromHandle(IntPtr handle) => null;

    public static Control? FromChildHandle(IntPtr handle) => null;

    // --- layout and painting helpers ---------------------------------------------------------------------

    [Description("Indicates whether the control should redraw itself when resized.")]
    protected bool ResizeRedraw
    {
        get => GetStyle(ControlStyles.ResizeRedraw);
        set => SetStyle(ControlStyles.ResizeRedraw, value);
    }

    private int _fontHeight = -1;

    protected int FontHeight
    {
        get => _fontHeight >= 0 ? _fontHeight : Font.Height;
        set => _fontHeight = value;
    }

    /// <summary>Called once the control has been added to a parent (ControlCollection.Add).</summary>
    protected virtual void InitLayout() { }

    internal void RaiseInitLayout() => InitLayout();

    protected internal void UpdateBounds() { }

    protected void UpdateBounds(int x, int y, int width, int height) => SetBounds(x, y, width, height);

    protected void UpdateBounds(int x, int y, int width, int height, int clientWidth, int clientHeight) => SetBounds(x, y, width, height);

    protected void UpdateZOrder() => _parent?.Invalidate(Bounds);

    /// <summary>The direct child under <paramref name="pt"/> (client coordinates), the front-most first.</summary>
    public Control? GetChildAtPoint(Point pt, GetChildAtPointSkip skipValue)
    {
        if (((int)skipValue & ~0x7) != 0) throw new InvalidEnumArgumentException(nameof(skipValue), (int)skipValue, typeof(GetChildAtPointSkip));
        if (_controls == null) return null;
        foreach (var c in _controls)
        {
            if ((skipValue & GetChildAtPointSkip.Invisible) != 0 && !c.Visible) continue;
            if ((skipValue & GetChildAtPointSkip.Disabled) != 0 && !c.Enabled) continue;
            if (c.Bounds.Contains(pt)) return c;
        }
        return null;
    }

    public Control? GetChildAtPoint(Point pt) => GetChildAtPoint(pt, GetChildAtPointSkip.None);

    protected void InvokeOnClick(Control? toInvoke, EventArgs e) => toInvoke?.OnClick(e);

    protected void InvokeGotFocus(Control? toInvoke, EventArgs e) => toInvoke?.OnGotFocus(e);

    protected void InvokeLostFocus(Control? toInvoke, EventArgs e) => toInvoke?.OnLostFocus(e);

    protected void InvokePaint(Control c, PaintEventArgs e) => c.OnPaint(e);

    protected void InvokePaintBackground(Control c, PaintEventArgs e) => c.OnPaintBackground(e);

    public void Invalidate(Region? region) => Invalidate(region, false);

    public void Invalidate(Region? region, bool invalidateChildren)
    {
        if (region == null)
        {
            Invalidate(invalidateChildren);
            return;
        }
        using var bitmap = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bitmap);
        Invalidate(Rectangle.Ceiling(region.GetBounds(g)), invalidateChildren);
    }

    private Region? _region;

    /// <summary>The shape of the control. Kept and reported; painting and hit-testing still use the rectangle.</summary>
    [Category("Layout")]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("The region, or shape, of this control.")]
    public Region? Region
    {
        get => _region;
        set
        {
            if (_region == value) return;
            _region = value;
            OnRegionChanged(EventArgs.Empty);
        }
    }

    [Category("Property Changed")]
    [Description("Event raised when the value of Region property is changed on Control.")]
    public event EventHandler? RegionChanged;

    protected virtual void OnRegionChanged(EventArgs e)
    {
        Invalidate();
        RegionChanged?.Invoke(this, e);
    }

    // --- scaling and DPI (NetForms works in device-independent pixels: 96 DPI) -------------------------

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Always)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int DeviceDpi => 96;

    public int LogicalToDeviceUnits(int value) => value;

    public Size LogicalToDeviceUnits(Size value) => value;

    public void ScaleBitmapLogicalToDevice(ref Bitmap logicalBitmap) { }

    protected virtual void RescaleConstantsForDpi(int deviceDpiOld, int deviceDpiNew) { }

    [Category("Layout")]
    [Description("Occurs when the top level parent of this control is moved to a monitor with a different resolution and scaling level, or when parent's monitor scaling level is changed in Windows settings. Child windows receive this message after parent window receives DpiChanged event.")]
    public event EventHandler? DpiChangedAfterParent;

    [Category("Layout")]
    [Description("Occurs when the top level parent of this control is moved to a monitor with a different resolution and scaling level, or when parent's monitor scaling level is changed in Windows settings. Child windows receive this message before parent window receives DpiChanged event.")]
    public event EventHandler? DpiChangedBeforeParent;

    protected virtual void OnDpiChangedAfterParent(EventArgs e) => DpiChangedAfterParent?.Invoke(this, e);

    protected virtual void OnDpiChangedBeforeParent(EventArgs e) => DpiChangedBeforeParent?.Invoke(this, e);

    protected virtual bool ScaleChildren => true;

    /// <summary>Scales the control and its children (location of a top-level control excepted), as WinForms.</summary>
    public void Scale(SizeF factor)
    {
        SuspendLayout();
        try
        {
            ScaleControl(factor, BoundsSpecified.All);
            if (ScaleChildren && _controls != null)
                foreach (var c in _controls.ToArray()) c.Scale(factor);
        }
        finally
        {
            ResumeLayout();
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method has been deprecated. Use the Scale(SizeF ratio) method instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public void Scale(float ratio) => ScaleCore(ratio, ratio);

    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method has been deprecated. Use the Scale(SizeF ratio) method instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public void Scale(float dx, float dy)
    {
        SuspendLayout();
        try { ScaleCore(dx, dy); }
        finally { ResumeLayout(); }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected virtual void ScaleCore(float dx, float dy) => Scale(new SizeF(dx, dy));

    protected virtual void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        var scaled = GetScaledBounds(Bounds, factor, specified);
        SetBounds(scaled.X, scaled.Y, scaled.Width, scaled.Height, BoundsSpecified.All);
    }

    /// <summary>Control.GetScaledBounds of WinForms: a top-level control keeps its location; fixed sizes keep theirs.</summary>
    protected virtual Rectangle GetScaledBounds(Rectangle bounds, SizeF factor, BoundsSpecified specified)
    {
        float dx = factor.Width, dy = factor.Height;
        int sx = bounds.X, sy = bounds.Y;
        bool scaleLoc = _parent != null && this is not Form { TopLevel: true };
        if (scaleLoc)
        {
            if ((specified & BoundsSpecified.X) != 0) sx = (int)Math.Round(bounds.X * dx);
            if ((specified & BoundsSpecified.Y) != 0) sy = (int)Math.Round(bounds.Y * dy);
        }
        int sw = bounds.Width, sh = bounds.Height;
        if (!GetStyle(ControlStyles.FixedWidth) && (specified & BoundsSpecified.Width) != 0)
            sw = (int)Math.Round((bounds.X + bounds.Width) * dx) - (scaleLoc ? sx : (int)Math.Round(bounds.X * dx));
        if (!GetStyle(ControlStyles.FixedHeight) && (specified & BoundsSpecified.Height) != 0)
            sh = (int)Math.Round((bounds.Y + bounds.Height) * dy) - (scaleLoc ? sy : (int)Math.Round(bounds.Y * dy));
        return new Rectangle(sx, sy, sw, sh);
    }

    // --- threading --------------------------------------------------------------------------------------

    /// <summary>Kept for code that sets it; NetForms marshals nothing implicitly and checks nothing.</summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public static bool CheckForIllegalCrossThreadCalls { get; set; } = System.Diagnostics.Debugger.IsAttached;

    public T Invoke<T>(Func<T> method) => (T)Invoke((Delegate)method)!;

    public IAsyncResult BeginInvoke(Action method) => BeginInvoke((Delegate)method, null);

    public Task InvokeAsync(Action callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return InvokeAsync<object?>(() => { callback(); return null; }, cancellationToken);
    }

    public Task<T> InvokeAsync<T>(Func<T> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.IsCancellationRequested) { tcs.SetCanceled(cancellationToken); return tcs.Task; }
        Application.Post(() =>
        {
            if (cancellationToken.IsCancellationRequested) { tcs.TrySetCanceled(cancellationToken); return; }
            try { tcs.TrySetResult(callback()); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        return tcs.Task;
    }

    public Task InvokeAsync(Func<CancellationToken, ValueTask> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return InvokeAsync(() => callback(cancellationToken).AsTask(), cancellationToken).Unwrap();
    }

    public Task<T> InvokeAsync<T>(Func<CancellationToken, ValueTask<T>> callback, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return InvokeAsync(() => callback(cancellationToken).AsTask(), cancellationToken).Unwrap();
    }

    // --- product information (from the assembly that declares the control's type) -----------------------

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Retrieves the company name for this control.")]
    public string CompanyName => GetType().Assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company ?? "";

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Retrieves the name of the product associated with this component.")]
    public string ProductName => GetType().Assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? GetType().Assembly.GetName().Name ?? "";

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    [Description("Retrieves the version of the product associated with this component.")]
    public string ProductVersion =>
        GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? GetType().Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version
        ?? GetType().Assembly.GetName().Version?.ToString() ?? "";

    // --- drag and drop (decision 155: NetForms runs the OLE protocol itself, DragDropManager) -------------

    /// <summary>
    /// Start a drag with <paramref name="data"/> (an <see cref="IDataObject"/>, or any object, wrapped in a
    /// <see cref="DataObject"/>) and return when it is dropped or cancelled: the effect the target chose, or None.
    /// </summary>
    public DragDropEffects DoDragDrop(object data, DragDropEffects allowedEffects) =>
        DragDropManager.DoDragDrop(this, data, allowedEffects);

    /// <summary>As <see cref="DoDragDrop(object, DragDropEffects)"/>; the drag image is not drawn (decision 155).</summary>
    public DragDropEffects DoDragDrop(object data, DragDropEffects allowedEffects, Bitmap? dragImage, Point cursorOffset, bool useDefaultDragImage) =>
        DragDropManager.DoDragDrop(this, data, allowedEffects);

    /// <summary>Drag <paramref name="data"/> serialized as JSON under its type's name (.NET 9+); TryGetData&lt;T&gt; reads it back.</summary>
    public DragDropEffects DoDragDropAsJson<T>(T data, DragDropEffects allowedEffects)
    {
        var dataObject = new DataObject();
        dataObject.SetDataAsJson(data);
        return DoDragDrop(dataObject, allowedEffects);
    }

    public DragDropEffects DoDragDropAsJson<T>(T data, DragDropEffects allowedEffects, Bitmap? dragImage, Point cursorOffset, bool useDefaultDragImage)
    {
        var dataObject = new DataObject();
        dataObject.SetDataAsJson(data);
        return DoDragDrop(dataObject, allowedEffects, dragImage, cursorOffset, useDefaultDragImage);
    }

    // The event keys RaiseDragEvent takes (WinForms' EventDragDrop, EventDragEnter, EventDragOver).
    internal static readonly object s_dragDropEvent = new();
    internal static readonly object s_dragEnterEvent = new();
    internal static readonly object s_dragOverEvent = new();

    /// <summary>Raise the drag event named by <paramref name="key"/> without the On* method (a control forwarding a child's drag).</summary>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    protected void RaiseDragEvent(object key, DragEventArgs e)
    {
        if (ReferenceEquals(key, s_dragDropEvent)) DragDrop?.Invoke(this, e);
        else if (ReferenceEquals(key, s_dragEnterEvent)) DragEnter?.Invoke(this, e);
        else if (ReferenceEquals(key, s_dragOverEvent)) DragOver?.Invoke(this, e);
    }

    internal void RaiseDragEnter(DragEventArgs e) => OnDragEnter(e);

    internal void RaiseDragOver(DragEventArgs e) => OnDragOver(e);

    internal void RaiseDragLeave(EventArgs e) => OnDragLeave(e);

    internal void RaiseDragDrop(DragEventArgs e) => OnDragDrop(e);

    internal void RaiseGiveFeedback(GiveFeedbackEventArgs e) => OnGiveFeedback(e);

    internal void RaiseQueryContinueDrag(QueryContinueDragEventArgs e) => OnQueryContinueDrag(e);

    [Category("Drag Drop")]
    [Description("Occurs when a drag-and-drop operation is completed.")]
    public event DragEventHandler? DragDrop;

    [Category("Drag Drop")]
    [Description("Occurs when the mouse drags an item into the client area for this Control.")]
    public event DragEventHandler? DragEnter;

    [Category("Drag Drop")]
    [Description("Occurs when an object is dragged over the control's bounds.")]
    public event DragEventHandler? DragOver;

    [Category("Drag Drop")]
    [Description("Occurs when an object is dragged out of the control's bounds.")]
    public event EventHandler? DragLeave;

    [Category("Drag Drop")]
    [Description("Occurs when the mouse drags an item. The system requests that the Control provide feedback to that effect.")]
    public event GiveFeedbackEventHandler? GiveFeedback;

    [Category("Drag Drop")]
    [Description("Occurs when the mouse drags an item. The system requests whether the drag-and-drop operation should be allowed to continue.")]
    public event QueryContinueDragEventHandler? QueryContinueDrag;

    protected virtual void OnDragDrop(DragEventArgs drgevent) => DragDrop?.Invoke(this, drgevent);

    protected virtual void OnDragEnter(DragEventArgs drgevent) => DragEnter?.Invoke(this, drgevent);

    protected virtual void OnDragOver(DragEventArgs drgevent) => DragOver?.Invoke(this, drgevent);

    protected virtual void OnDragLeave(EventArgs e) => DragLeave?.Invoke(this, e);

    protected virtual void OnGiveFeedback(GiveFeedbackEventArgs gfbevent) => GiveFeedback?.Invoke(this, gfbevent);

    protected virtual void OnQueryContinueDrag(QueryContinueDragEventArgs qcdevent) => QueryContinueDrag?.Invoke(this, qcdevent);

    // --- the remaining notifications ---------------------------------------------------------------------

    [Category("Behavior")]
    [Description("Occurs when the user invokes Help for the control.")]
    public event HelpEventHandler? HelpRequested;

    protected virtual void OnHelpRequested(HelpEventArgs hevent)
    {
        HelpRequested?.Invoke(this, hevent);
        // Unhandled help goes to the parent, as WM_HELP does.
        if (!hevent.Handled) _parent?.OnHelpRequested(hevent);
    }

    internal void RaiseHelpRequested(HelpEventArgs e) => OnHelpRequested(e);

    [Category("Behavior")]
    [Description("Occurs when focus rectangles and keyboard cue underlines are being shown or hidden.")]
    public event UICuesEventHandler? ChangeUICues;

    protected virtual void OnChangeUICues(UICuesEventArgs e) => ChangeUICues?.Invoke(this, e);

    [Category("Behavior")]
    [Description("Event raised when the window style of a Control is changed.")]
    public event EventHandler? StyleChanged;

    protected virtual void OnStyleChanged(EventArgs e) => StyleChanged?.Invoke(this, e);

    [Category("Behavior")]
    [Description("Event raised when the system colors change.")]
    public event EventHandler? SystemColorsChanged;

    protected virtual void OnSystemColorsChanged(EventArgs e)
    {
        if (_controls != null)
            foreach (var c in _controls) c.OnSystemColorsChanged(EventArgs.Empty);
        Invalidate();
        SystemColorsChanged?.Invoke(this, e);
    }

    [Category("Property Changed")]
    [Description("Occurs when the value of the RightToLeft property changes.")]
    public event EventHandler? RightToLeftChanged;

    protected virtual void OnRightToLeftChanged(EventArgs e)
    {
        RightToLeftChanged?.Invoke(this, e);
        if (_controls != null)
            foreach (var c in _controls) c.OnParentRightToLeftChanged(e);
    }

    protected virtual void OnParentRightToLeftChanged(EventArgs e) => OnRightToLeftChanged(e);

    [Category("Behavior")]
    [Description("Occurs when the control's input method editor (IME) mode changes.")]
    public event EventHandler? ImeModeChanged;

    protected virtual void OnImeModeChanged(EventArgs e) => ImeModeChanged?.Invoke(this, e);

    [Category("Property Changed")]
    [Description("Event raised when the value of the CausesValidation property is changed on Control.")]
    public event EventHandler? CausesValidationChanged;

    protected virtual void OnCausesValidationChanged(EventArgs e) => CausesValidationChanged?.Invoke(this, e);

    [Category("Property Changed")]
    [Description("Occurs when the value of the ContextMenuStrip property changes.")]
    public event EventHandler? ContextMenuStripChanged;

    protected virtual void OnContextMenuStripChanged(EventArgs e) => ContextMenuStripChanged?.Invoke(this, e);

    protected virtual void OnParentCursorChanged(EventArgs e) { }
}

public enum PreProcessControlState
{
    MessageProcessed = 0x00,
    MessageNeeded = 0x01,
    MessageNotNeeded = 0x02,
}
