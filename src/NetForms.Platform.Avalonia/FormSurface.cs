using System;
using Size = System.Drawing.Size;
using Rectangle = System.Drawing.Rectangle;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using AvaloniaControl = Avalonia.Controls.Control;
using AvaloniaPoint = Avalonia.Point;
using AvaloniaRect = Avalonia.Rect;

namespace NetForms.Platform.Avalonia;

/// <summary>
/// The one Avalonia control per Form: it covers the whole client area, paints the NetForms
/// tree and forwards raw input to the host.
///
/// Painting is double-buffered. The surface keeps a raster back buffer of the client area;
/// <see cref="Invalidate"/> accumulates a dirty rectangle, and <see cref="Render"/> (UI
/// thread, where the control tree may be touched safely) asks the host to repaint only that
/// rectangle into the buffer. A snapshot of the buffer is then handed to a custom draw
/// operation that blits it on the render thread through Avalonia's Skia lease, i.e.
/// straight onto the GPU surface. OnPaint keeps WinForms' single-threaded contract, an
/// Invalidate of a 75×23 button costs a 75×23 repaint, and nothing ever flickers.
/// </summary>
internal sealed class FormSurface : AvaloniaControl
{
    private readonly IWindowHost _host;
    private SKBitmap? _backBuffer;
    private Rectangle _dirty = Rectangle.Empty;
    private bool _dirtyAll = true;
    private static readonly bool s_trace = Environment.GetEnvironmentVariable("NETFORMS_TRACE_INPUT") == "1";

    private static void Trace(string message)
    {
        if (s_trace) Console.Error.WriteLine("[input] " + message);
    }

    public FormSurface(IWindowHost host)
    {
        _host = host;
        Focusable = true;
        IsHitTestVisible = true;
        ClipToBounds = true;

        // Drags from other applications (files from the file manager, text): the host decides per control.
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    // --- drops from other applications -------------------------------------------------------------------

    private PlatformDragData? _dragData;

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        _dragData = ReadDragData(e.DataTransfer);
        e.DragEffects = FromEffects(_host.DragEnter(ToPoint(e.GetPosition(this)), _dragData, ToEffects(e.DragEffects), ToModifiers(e.KeyModifiers)));
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        _dragData ??= ReadDragData(e.DataTransfer);
        e.DragEffects = FromEffects(_host.DragOver(ToPoint(e.GetPosition(this)), _dragData, ToEffects(e.DragEffects), ToModifiers(e.KeyModifiers)));
        e.Handled = true;
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        _dragData = null;
        _host.DragLeave();
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var data = _dragData ?? ReadDragData(e.DataTransfer);
        _dragData = null;
        e.DragEffects = FromEffects(_host.Drop(ToPoint(e.GetPosition(this)), data, ToEffects(e.DragEffects), ToModifiers(e.KeyModifiers)));
        e.Handled = true;
    }

    private static PlatformDragData ReadDragData(IDataTransfer transfer)
    {
        var files = new System.Collections.Generic.List<string>();
        try
        {
            foreach (var item in transfer.TryGetFiles() ?? [])
            {
                var path = item.TryGetLocalPath();
                if (!string.IsNullOrEmpty(path)) files.Add(path);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            // A format the platform cannot hand out synchronously: go on with the text.
        }
        string? text = null;
        try
        {
            text = transfer.TryGetText();
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
        }
        return new PlatformDragData { Files = files, Text = text };
    }

    private static PlatformDragEffects ToEffects(DragDropEffects effects)
    {
        var result = PlatformDragEffects.None;
        if ((effects & DragDropEffects.Copy) != 0) result |= PlatformDragEffects.Copy;
        if ((effects & DragDropEffects.Move) != 0) result |= PlatformDragEffects.Move;
        if ((effects & DragDropEffects.Link) != 0) result |= PlatformDragEffects.Link;
        return result;
    }

    private static DragDropEffects FromEffects(PlatformDragEffects effects)
    {
        var result = DragDropEffects.None;
        if ((effects & PlatformDragEffects.Copy) != 0) result |= DragDropEffects.Copy;
        if ((effects & PlatformDragEffects.Move) != 0) result |= DragDropEffects.Move;
        if ((effects & PlatformDragEffects.Link) != 0) result |= DragDropEffects.Link;
        return result;
    }

    /// <summary>Mark part of the client area (all of it for <c>null</c>) as needing a repaint.</summary>
    public void Invalidate(Rectangle? area)
    {
        if (area == null)
        {
            _dirtyAll = true;
        }
        else if (!_dirtyAll)
        {
            var r = area.Value;
            if (r.Width <= 0 || r.Height <= 0) return;
            _dirty = _dirty.IsEmpty ? r : Rectangle.Union(_dirty, r);
        }

        // WinForms lets OnPaint call Invalidate; Avalonia throws if a visual is invalidated
        // while it is being rendered, so a repaint asked for from inside Render is deferred
        // to the next frame. The dirty rectangle above is already recorded either way.
        if (_rendering)
        {
            if (!_repaintQueued)
            {
                _repaintQueued = true;
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _repaintQueued = false;
                    InvalidateVisual();
                }, global::Avalonia.Threading.DispatcherPriority.Render);
            }
            return;
        }
        InvalidateVisual();
    }

    private bool _rendering;
    private bool _repaintQueued;

    public override void Render(DrawingContext context)
    {
        _rendering = true;
        try
        {
            RenderCore(context);
        }
        finally
        {
            _rendering = false;
        }
    }

    private void RenderCore(DrawingContext context)
    {
        var bounds = Bounds;
        int w = (int)Math.Ceiling(bounds.Width);
        int h = (int)Math.Ceiling(bounds.Height);
        if (w <= 0 || h <= 0) return;

        // The buffer holds device pixels; the tree paints in DIPs through a scale transform,
        // so text and lines stay crisp on HiDPI screens.
        double scale = (VisualRoot as global::Avalonia.Controls.TopLevel)?.RenderScaling ?? 1.0;
        int pw = (int)Math.Ceiling(w * scale);
        int ph = (int)Math.Ceiling(h * scale);
        if (_backBuffer == null || _backBuffer.Width != pw || _backBuffer.Height != ph)
        {
            _backBuffer?.Dispose();
            _backBuffer = new SKBitmap(new SKImageInfo(pw, ph, SKColorType.Bgra8888, SKAlphaType.Premul));
            _dirtyAll = true;
        }

        var clip = _dirtyAll ? new Rectangle(0, 0, w, h) : Rectangle.Intersect(_dirty, new Rectangle(0, 0, w, h));
        _dirty = Rectangle.Empty;
        _dirtyAll = false;

        if (clip.Width > 0 && clip.Height > 0)
        {
            using var canvas = new SKCanvas(_backBuffer);
            canvas.Scale((float)scale);
            canvas.ClipRect(new SKRect(clip.Left, clip.Top, clip.Right, clip.Bottom), SKClipOperation.Intersect, false);
            _host.Paint(canvas, new Size(w, h), clip);
            canvas.Flush();
        }

        // The render thread gets its own copy; the back buffer stays ours to paint into.
        var snapshot = SKImage.FromBitmap(_backBuffer);
        context.Custom(new BlitDrawOperation(snapshot, new AvaloniaRect(0, 0, w, h)));
    }

    // --- pointer ---------------------------------------------------------------------

    private static System.Drawing.Point ToPoint(AvaloniaPoint p) => new((int)Math.Floor(p.X), (int)Math.Floor(p.Y));

    private static InputModifiers ToModifiers(KeyModifiers m)
    {
        var r = InputModifiers.None;
        if ((m & KeyModifiers.Shift) != 0) r |= InputModifiers.Shift;
        if ((m & KeyModifiers.Control) != 0) r |= InputModifiers.Control;
        if ((m & KeyModifiers.Alt) != 0) r |= InputModifiers.Alt;
        return r;
    }

    private static MouseButton PressedButtons(PointerPointProperties p)
    {
        var r = MouseButton.None;
        if (p.IsLeftButtonPressed) r |= MouseButton.Left;
        if (p.IsRightButtonPressed) r |= MouseButton.Right;
        if (p.IsMiddleButtonPressed) r |= MouseButton.Middle;
        if (p.IsXButton1Pressed) r |= MouseButton.XButton1;
        if (p.IsXButton2Pressed) r |= MouseButton.XButton2;
        return r;
    }

    private static MouseButton ToButton(PointerUpdateKind kind) => kind switch
    {
        PointerUpdateKind.LeftButtonPressed or PointerUpdateKind.LeftButtonReleased => MouseButton.Left,
        PointerUpdateKind.RightButtonPressed or PointerUpdateKind.RightButtonReleased => MouseButton.Right,
        PointerUpdateKind.MiddleButtonPressed or PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
        PointerUpdateKind.XButton1Pressed or PointerUpdateKind.XButton1Released => MouseButton.XButton1,
        PointerUpdateKind.XButton2Pressed or PointerUpdateKind.XButton2Released => MouseButton.XButton2,
        _ => MouseButton.None,
    };

    private static MouseButton ToButton(global::Avalonia.Input.MouseButton b) => b switch
    {
        global::Avalonia.Input.MouseButton.Left => MouseButton.Left,
        global::Avalonia.Input.MouseButton.Right => MouseButton.Right,
        global::Avalonia.Input.MouseButton.Middle => MouseButton.Middle,
        global::Avalonia.Input.MouseButton.XButton1 => MouseButton.XButton1,
        global::Avalonia.Input.MouseButton.XButton2 => MouseButton.XButton2,
        _ => MouseButton.None,
    };

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var button = ToButton(point.Properties.PointerUpdateKind);
        Trace($"pressed {point.Properties.PointerUpdateKind} at {point.Position} clicks={e.ClickCount}");
        if (button == MouseButton.None) return;
        Focus();
        _host.MouseDown(button, ToPoint(point.Position), e.ClickCount, ToModifiers(e.KeyModifiers));
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var button = ToButton(e.InitialPressMouseButton);
        Trace($"released {e.InitialPressMouseButton} at {point.Position}");
        if (button == MouseButton.None) return;
        _host.MouseUp(button, ToPoint(point.Position), ToModifiers(e.KeyModifiers));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        Trace($"move {point.Position}");
        _host.MouseMove(ToPoint(point.Position), PressedButtons(point.Properties), ToModifiers(e.KeyModifiers));
        e.Handled = true;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _host.MouseLeave();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        // WinForms reports 120 per notch.
        _host.MouseWheel(ToPoint(point.Position), (int)Math.Round(e.Delta.Y * 120), ToModifiers(e.KeyModifiers));
        e.Handled = true;
    }

    // --- keyboard --------------------------------------------------------------------

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int vk = KeyMap.ToVirtualKey(e.Key);
        Trace($"keydown {e.Key} vk={vk}");
        if (vk == 0) return;
        if (_host.KeyDown(vk, ToModifiers(e.KeyModifiers))) e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        int vk = KeyMap.ToVirtualKey(e.Key);
        if (vk == 0) return;
        if (_host.KeyUp(vk, ToModifiers(e.KeyModifiers))) e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            _host.TextInput(e.Text);
            e.Handled = true;
        }
    }

    /// <summary>Blits a back-buffer snapshot on the render thread via the Skia lease.</summary>
    private sealed class BlitDrawOperation : ICustomDrawOperation
    {
        private readonly SKImage _image;

        public BlitDrawOperation(SKImage image, AvaloniaRect bounds)
        {
            _image = image;
            Bounds = bounds;
        }

        public AvaloniaRect Bounds { get; }

        public bool HitTest(AvaloniaPoint p) => Bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.DrawImage(_image, new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height), new SKSamplingOptions(SKFilterMode.Nearest));
        }

        public void Dispose() => _image.Dispose();
    }
}
