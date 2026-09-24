using System;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using SkiaSharp;

namespace System.Drawing;

/// <summary>
/// System.Drawing.Graphics over an <see cref="SKCanvas"/>. Coordinates follow GDI+:
/// fills cover exactly the pixels of the rectangle, strokes are centred on pixel centres
/// (so a 1px <see cref="DrawRectangle(Pen, Rectangle)"/> is width+1 pixels wide, like GDI+),
/// and lines include both end points.
///
/// The canvas state at construction is the "base": <see cref="ResetTransform"/> and
/// <see cref="ResetClip"/> return to it, never further, so a control painting through a
/// Graphics handed to it can never escape its own bounds.
/// </summary>
public sealed partial class Graphics : IDeviceContext, IDisposable
{
    private readonly bool _ownsCanvas;
    private readonly SKPaint _paint = new();
    private readonly int _baseSave;
    private readonly SKMatrix _baseMatrix;
    private SmoothingMode _smoothing = SmoothingMode.Default;

    private Graphics(SKCanvas canvas, bool ownsCanvas)
    {
        Canvas = canvas;
        _ownsCanvas = ownsCanvas;
        _baseMatrix = canvas.TotalMatrix;
        _baseSave = canvas.Save();
    }

    /// <summary>Wrap an existing canvas; the caller keeps ownership. The current clip and transform become the base state.</summary>
    internal static Graphics FromCanvas(SKCanvas canvas) => new Graphics(canvas, ownsCanvas: false);

    public static Graphics FromImage(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bmp = image.SkBitmap ?? throw new ObjectDisposedException(nameof(image));
        return new Graphics(new SKCanvas(bmp), ownsCanvas: true);
    }

    internal SKCanvas Canvas { get; }

    public float DpiX => 96f;
    public float DpiY => 96f;
    public GraphicsUnit PageUnit { get; set; } = GraphicsUnit.Display;
    public float PageScale { get; set; } = 1f;

    public SmoothingMode SmoothingMode
    {
        get => _smoothing;
        set => _smoothing = value;
    }

    public TextRenderingHint TextRenderingHint { get; set; } = TextRenderingHint.SystemDefault;
    public InterpolationMode InterpolationMode { get; set; } = InterpolationMode.Default;
    public PixelOffsetMode PixelOffsetMode { get; set; } = PixelOffsetMode.Default;
    public CompositingQuality CompositingQuality { get; set; } = CompositingQuality.Default;
    public CompositingMode CompositingMode { get; set; } = CompositingMode.SourceOver;
    public int TextContrast { get; set; } = 4;

    private bool AntiAlias => _smoothing is SmoothingMode.AntiAlias or SmoothingMode.HighQuality;

    // --- clip ----------------------------------------------------------------------

    public RectangleF ClipBounds
    {
        get
        {
            var r = Canvas.LocalClipBounds;
            return new RectangleF(r.Left, r.Top, r.Width, r.Height);
        }
    }

    public RectangleF VisibleClipBounds => ClipBounds;

    public bool IsClipEmpty => Canvas.IsClipEmpty;

    public bool IsVisibleClipEmpty => Canvas.IsClipEmpty;

    public Region Clip
    {
        get => new Region(Rectangle.Round(ClipBounds));
        set => SetClip(value, CombineMode.Replace);
    }

    public bool IsVisible(Point point) => ClipBounds.Contains(point);
    public bool IsVisible(PointF point) => ClipBounds.Contains(point);
    public bool IsVisible(int x, int y) => ClipBounds.Contains(x, y);
    public bool IsVisible(float x, float y) => ClipBounds.Contains(x, y);
    public bool IsVisible(Rectangle rect) => ClipBounds.IntersectsWith(rect);
    public bool IsVisible(RectangleF rect) => ClipBounds.IntersectsWith(rect);

    /// <summary>Clip to a region given in world coordinates (SKCanvas.ClipRegion would take device pixels).</summary>
    private void ClipRegionLocal(Region region)
    {
        using var path = region.Skia.GetBoundaryPath();
        Canvas.ClipPath(path, SKClipOperation.Intersect, antialias: false);
    }

    /// <summary>Rewind to the base state (dropping user clips) but keep the current transform.</summary>
    private void RewindClip()
    {
        var matrix = Canvas.TotalMatrix;
        Canvas.RestoreToCount(_baseSave);
        Canvas.Save();
        Canvas.SetMatrix(matrix);
    }

    public void SetClip(Rectangle rect) => SetClip((RectangleF)rect, CombineMode.Replace);

    public void SetClip(Rectangle rect, CombineMode combineMode) => SetClip((RectangleF)rect, combineMode);

    public void SetClip(RectangleF rect) => SetClip(rect, CombineMode.Replace);

    public void SetClip(RectangleF rect, CombineMode combineMode)
    {
        if (combineMode == CombineMode.Replace) RewindClip();
        var op = combineMode switch
        {
            CombineMode.Exclude => SKClipOperation.Difference,
            _ => SKClipOperation.Intersect,
        };
        if (combineMode is CombineMode.Union or CombineMode.Xor or CombineMode.Complement)
        {
            // Skia clips can only shrink; build the union/xor as a region against the current clip.
            using var region = new Region(Rectangle.Round(ClipBounds));
            switch (combineMode)
            {
                case CombineMode.Union: region.Union(rect); break;
                case CombineMode.Xor: region.Xor(rect); break;
                case CombineMode.Complement: region.Complement(rect); break;
            }
            RewindClip();
            ClipRegionLocal(region);
            return;
        }
        Canvas.ClipRect(SkiaConvert.ToSK(rect), op, antialias: false);
    }

    public void SetClip(GraphicsPath path) => SetClip(path, CombineMode.Replace);

    public void SetClip(GraphicsPath path, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (combineMode == CombineMode.Replace) RewindClip();
        switch (combineMode)
        {
            case CombineMode.Replace:
            case CombineMode.Intersect:
                Canvas.ClipPath(path.Skia, SKClipOperation.Intersect, AntiAlias);
                break;
            case CombineMode.Exclude:
                Canvas.ClipPath(path.Skia, SKClipOperation.Difference, AntiAlias);
                break;
            default:
                {
                    using var region = new Region(Rectangle.Round(ClipBounds));
                    if (combineMode == CombineMode.Union) region.Union(path);
                    else if (combineMode == CombineMode.Xor) region.Xor(path);
                    else region.Complement(path);
                    RewindClip();
                    ClipRegionLocal(region);
                    break;
                }
        }
    }

    public void SetClip(Region region) => SetClip(region, CombineMode.Replace);

    public void SetClip(Region region, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(region);
        if (combineMode == CombineMode.Replace)
        {
            RewindClip();
            if (region.IsInfinite(this)) return;
            ClipRegionLocal(region);
            return;
        }
        using var current = new Region(Rectangle.Round(ClipBounds));
        switch (combineMode)
        {
            case CombineMode.Intersect: current.Intersect(region); break;
            case CombineMode.Union: current.Union(region); break;
            case CombineMode.Xor: current.Xor(region); break;
            case CombineMode.Exclude: current.Exclude(region); break;
            case CombineMode.Complement: current.Complement(region); break;
        }
        RewindClip();
        ClipRegionLocal(current);
    }

    public void SetClip(Graphics g) => SetClip(g, CombineMode.Replace);

    public void SetClip(Graphics g, CombineMode combineMode)
    {
        ArgumentNullException.ThrowIfNull(g);
        SetClip(g.ClipBounds, combineMode);
    }

    public void IntersectClip(Rectangle rect) => Canvas.ClipRect(SkiaConvert.ToSK(rect), SKClipOperation.Intersect, antialias: false);

    public void IntersectClip(RectangleF rect) => Canvas.ClipRect(SkiaConvert.ToSK(rect), SKClipOperation.Intersect, antialias: false);

    public void IntersectClip(Region region) => SetClip(region, CombineMode.Intersect);

    public void ExcludeClip(Rectangle rect) => Canvas.ClipRect(SkiaConvert.ToSK(rect), SKClipOperation.Difference, antialias: false);

    public void ExcludeClip(Region region) => SetClip(region, CombineMode.Exclude);

    public void ResetClip() => RewindClip();

    public void TranslateClip(int dx, int dy) => TranslateClip((float)dx, dy);

    public void TranslateClip(float dx, float dy)
    {
        var r = ClipBounds;
        r.Offset(dx, dy);
        SetClip(r, CombineMode.Replace);
    }

    // --- state ---------------------------------------------------------------------

    public GraphicsState Save() => new GraphicsState(Canvas.Save(), _smoothing, TextRenderingHint, InterpolationMode, PixelOffsetMode);

    public void Restore(GraphicsState gstate)
    {
        ArgumentNullException.ThrowIfNull(gstate);
        Canvas.RestoreToCount(gstate.SaveCount);
        _smoothing = gstate.Smoothing;
        TextRenderingHint = gstate.TextHint;
        InterpolationMode = gstate.Interpolation;
        PixelOffsetMode = gstate.PixelOffset;
    }

    public GraphicsContainer BeginContainer()
    {
        var state = Save();
        return new GraphicsContainer(state);
    }

    public GraphicsContainer BeginContainer(Rectangle dstrect, Rectangle srcrect, GraphicsUnit unit) => BeginContainer((RectangleF)dstrect, srcrect, unit);

    public GraphicsContainer BeginContainer(RectangleF dstrect, RectangleF srcrect, GraphicsUnit unit)
    {
        var container = BeginContainer();
        if (srcrect.Width != 0 && srcrect.Height != 0)
        {
            TranslateTransform(dstrect.X, dstrect.Y);
            ScaleTransform(dstrect.Width / srcrect.Width, dstrect.Height / srcrect.Height);
            TranslateTransform(-srcrect.X, -srcrect.Y);
        }
        return container;
    }

    public void EndContainer(GraphicsContainer container)
    {
        ArgumentNullException.ThrowIfNull(container);
        Restore(container.State);
    }

    public void Flush() => Canvas.Flush();

    public void Flush(FlushIntention intention) => Canvas.Flush();

    public IntPtr GetHdc() => IntPtr.Zero;

    public void ReleaseHdc() { }

    public void ReleaseHdc(IntPtr hdc) { }

    // --- transforms ----------------------------------------------------------------

    /// <summary>The world transform relative to the base state.</summary>
    public Matrix Transform
    {
        get
        {
            // total = base × world  (Skia: Concat(base, world)) → world = base⁻¹ × total
            if (!_baseMatrix.TryInvert(out var inv)) return new Matrix();
            return new Matrix(SKMatrix.Concat(inv, Canvas.TotalMatrix));
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Canvas.SetMatrix(SKMatrix.Concat(_baseMatrix, value.Skia));
        }
    }

    public void MultiplyTransform(Matrix matrix) => MultiplyTransform(matrix, MatrixOrder.Prepend);

    public void MultiplyTransform(Matrix matrix, MatrixOrder order)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var current = Transform;
        current.Multiply(matrix, order);
        Transform = current;
    }

    public void TranslateTransform(float dx, float dy) => TranslateTransform(dx, dy, MatrixOrder.Prepend);

    public void TranslateTransform(float dx, float dy, MatrixOrder order)
    {
        if (order == MatrixOrder.Prepend) Canvas.Translate(dx, dy);
        else MultiplyTransform(new Matrix(1, 0, 0, 1, dx, dy), MatrixOrder.Append);
    }

    public void ScaleTransform(float sx, float sy) => ScaleTransform(sx, sy, MatrixOrder.Prepend);

    public void ScaleTransform(float sx, float sy, MatrixOrder order)
    {
        if (order == MatrixOrder.Prepend) Canvas.Scale(sx, sy);
        else MultiplyTransform(new Matrix(sx, 0, 0, sy, 0, 0), MatrixOrder.Append);
    }

    public void RotateTransform(float angle) => RotateTransform(angle, MatrixOrder.Prepend);

    public void RotateTransform(float angle, MatrixOrder order)
    {
        if (order == MatrixOrder.Prepend) Canvas.RotateDegrees(angle);
        else
        {
            var m = new Matrix();
            m.Rotate(angle);
            MultiplyTransform(m, MatrixOrder.Append);
        }
    }

    public void ResetTransform() => Canvas.SetMatrix(_baseMatrix);

    public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, PointF[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        if (destSpace == srcSpace) return;
        var m = Transform;
        if (srcSpace == CoordinateSpace.World && destSpace != CoordinateSpace.World) m.TransformPoints(pts);
        else if (srcSpace != CoordinateSpace.World && destSpace == CoordinateSpace.World)
        {
            m.Invert();
            m.TransformPoints(pts);
        }
    }

    public void TransformPoints(CoordinateSpace destSpace, CoordinateSpace srcSpace, Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        var f = GraphicsPath.ToPointF(pts);
        TransformPoints(destSpace, srcSpace, f);
        for (int i = 0; i < pts.Length; i++) pts[i] = Point.Round(f[i]);
    }

    // --- clearing and filling ------------------------------------------------------

    public void Clear(Color color) => Canvas.Clear(SkiaConvert.ToSK(color));

    private void PrepareBrush(Brush brush)
    {
        ArgumentNullException.ThrowIfNull(brush);
        _paint.Reset();
        brush.ApplyTo(_paint);
        _paint.IsAntialias = AntiAlias;
        _paint.BlendMode = CompositingMode == CompositingMode.SourceCopy ? SKBlendMode.Src : SKBlendMode.SrcOver;
    }

    public void FillRectangle(Brush brush, Rectangle rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillRectangle(Brush brush, RectangleF rect) => FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillRectangle(Brush brush, int x, int y, int width, int height) => FillRectangle(brush, (float)x, y, width, height);

    public void FillRectangle(Brush brush, float x, float y, float width, float height)
    {
        if (width <= 0 || height <= 0) { ArgumentNullException.ThrowIfNull(brush); return; }
        PrepareBrush(brush);
        Canvas.DrawRect(x, y, width, height, _paint);
    }

    public void FillRectangles(Brush brush, Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var r in rects) FillRectangle(brush, r);
    }

    public void FillRectangles(Brush brush, RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var r in rects) FillRectangle(brush, r);
    }

    public void FillEllipse(Brush brush, Rectangle rect) => FillEllipse(brush, (float)rect.X, rect.Y, rect.Width, rect.Height);

    public void FillEllipse(Brush brush, RectangleF rect) => FillEllipse(brush, rect.X, rect.Y, rect.Width, rect.Height);

    public void FillEllipse(Brush brush, int x, int y, int width, int height) => FillEllipse(brush, (float)x, y, width, height);

    public void FillEllipse(Brush brush, float x, float y, float width, float height)
    {
        PrepareBrush(brush);
        Canvas.DrawOval(new SKRect(x, y, x + width, y + height), _paint);
    }

    public void FillPie(Brush brush, Rectangle rect, float startAngle, float sweepAngle) => FillPie(brush, (float)rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void FillPie(Brush brush, int x, int y, int width, int height, int startAngle, int sweepAngle) => FillPie(brush, (float)x, y, width, height, startAngle, sweepAngle);

    public void FillPie(Brush brush, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        PrepareBrush(brush);
        Canvas.DrawArc(new SKRect(x, y, x + width, y + height), startAngle, sweepAngle, useCenter: true, _paint);
    }

    public void FillPolygon(Brush brush, Point[] points) => FillPolygon(brush, GraphicsPath.ToPointF(points), FillMode.Alternate);

    public void FillPolygon(Brush brush, Point[] points, FillMode fillMode) => FillPolygon(brush, GraphicsPath.ToPointF(points), fillMode);

    public void FillPolygon(Brush brush, PointF[] points) => FillPolygon(brush, points, FillMode.Alternate);

    public void FillPolygon(Brush brush, PointF[] points, FillMode fillMode)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2) { ArgumentNullException.ThrowIfNull(brush); return; }
        using var path = new SKPath { FillType = fillMode == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd };
        path.MoveTo(points[0].X, points[0].Y);
        for (int i = 1; i < points.Length; i++) path.LineTo(points[i].X, points[i].Y);
        path.Close();
        PrepareBrush(brush);
        Canvas.DrawPath(path, _paint);
    }

    public void FillPath(Brush brush, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        PrepareBrush(brush);
        Canvas.DrawPath(path.Skia, _paint);
    }

    public void FillRegion(Brush brush, Region region)
    {
        ArgumentNullException.ThrowIfNull(region);
        PrepareBrush(brush);
        using var path = region.Skia.GetBoundaryPath();
        Canvas.DrawPath(path, _paint);
    }

    public void FillClosedCurve(Brush brush, Point[] points) => FillClosedCurve(brush, GraphicsPath.ToPointF(points), FillMode.Alternate, 0.5f);

    public void FillClosedCurve(Brush brush, PointF[] points) => FillClosedCurve(brush, points, FillMode.Alternate, 0.5f);

    public void FillClosedCurve(Brush brush, Point[] points, FillMode fillmode) => FillClosedCurve(brush, GraphicsPath.ToPointF(points), fillmode, 0.5f);

    public void FillClosedCurve(Brush brush, PointF[] points, FillMode fillmode) => FillClosedCurve(brush, points, fillmode, 0.5f);

    public void FillClosedCurve(Brush brush, Point[] points, FillMode fillmode, float tension) => FillClosedCurve(brush, GraphicsPath.ToPointF(points), fillmode, tension);

    public void FillClosedCurve(Brush brush, PointF[] points, FillMode fillmode, float tension)
    {
        using var path = new GraphicsPath(fillmode);
        path.AddClosedCurve(points, tension);
        FillPath(brush, path);
    }

    // --- strokes -------------------------------------------------------------------

    private void PreparePen(Pen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        _paint.Reset();
        pen.ApplyTo(_paint);
        _paint.IsAntialias = AntiAlias;
    }

    /// <summary>Half-pixel offset that puts an odd-width stroke on pixel centres.</summary>
    private float PixelOffset(Pen pen)
    {
        if (PixelOffsetMode is PixelOffsetMode.Half or PixelOffsetMode.HighQuality) return 0f;
        return ((int)Math.Round(pen.Width) & 1) == 1 ? 0.5f : 0f;
    }

    public void DrawRectangle(Pen pen, Rectangle rect) => DrawRectangle(pen, (float)rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawRectangle(Pen pen, RectangleF rect) => DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawRectangle(Pen pen, int x, int y, int width, int height) => DrawRectangle(pen, (float)x, y, width, height);

    public void DrawRectangle(Pen pen, float x, float y, float width, float height)
    {
        PreparePen(pen);
        float o = PixelOffset(pen);
        Canvas.DrawRect(x + o, y + o, width, height, _paint);
    }

    public void DrawRectangles(Pen pen, Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var r in rects) DrawRectangle(pen, r);
    }

    public void DrawRectangles(Pen pen, RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var r in rects) DrawRectangle(pen, r);
    }

    public void DrawLine(Pen pen, Point pt1, Point pt2) => DrawLine(pen, (float)pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void DrawLine(Pen pen, PointF pt1, PointF pt2) => DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);

    public void DrawLine(Pen pen, int x1, int y1, int x2, int y2) => DrawLine(pen, (float)x1, y1, x2, y2);

    public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
    {
        PreparePen(pen);
        float o = PixelOffset(pen);
        // GDI+ draws both end pixels; a square cap on a pixel-centred line does the same.
        if (pen.StartCap == LineCap.Flat && pen.EndCap == LineCap.Flat && !AntiAlias)
            _paint.StrokeCap = SKStrokeCap.Square;
        Canvas.DrawLine(x1 + o, y1 + o, x2 + o, y2 + o, _paint);
    }

    public void DrawLines(Pen pen, Point[] points) => DrawLines(pen, GraphicsPath.ToPointF(points));

    public void DrawLines(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2) { ArgumentNullException.ThrowIfNull(pen); return; }
        PreparePen(pen);
        float o = PixelOffset(pen);
        using var path = new SKPath();
        path.MoveTo(points[0].X + o, points[0].Y + o);
        for (int i = 1; i < points.Length; i++) path.LineTo(points[i].X + o, points[i].Y + o);
        Canvas.DrawPath(path, _paint);
    }

    public void DrawEllipse(Pen pen, Rectangle rect) => DrawEllipse(pen, (float)rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawEllipse(Pen pen, RectangleF rect) => DrawEllipse(pen, rect.X, rect.Y, rect.Width, rect.Height);

    public void DrawEllipse(Pen pen, int x, int y, int width, int height) => DrawEllipse(pen, (float)x, y, width, height);

    public void DrawEllipse(Pen pen, float x, float y, float width, float height)
    {
        PreparePen(pen);
        float o = PixelOffset(pen);
        Canvas.DrawOval(new SKRect(x + o, y + o, x + o + width, y + o + height), _paint);
    }

    public void DrawArc(Pen pen, Rectangle rect, float startAngle, float sweepAngle) => DrawArc(pen, (float)rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawArc(Pen pen, RectangleF rect, float startAngle, float sweepAngle) => DrawArc(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawArc(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) => DrawArc(pen, (float)x, y, width, height, startAngle, sweepAngle);

    public void DrawArc(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        PreparePen(pen);
        float o = PixelOffset(pen);
        Canvas.DrawArc(new SKRect(x + o, y + o, x + o + width, y + o + height), startAngle, sweepAngle, useCenter: false, _paint);
    }

    public void DrawPie(Pen pen, Rectangle rect, float startAngle, float sweepAngle) => DrawPie(pen, (float)rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawPie(Pen pen, RectangleF rect, float startAngle, float sweepAngle) => DrawPie(pen, rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void DrawPie(Pen pen, int x, int y, int width, int height, int startAngle, int sweepAngle) => DrawPie(pen, (float)x, y, width, height, startAngle, sweepAngle);

    public void DrawPie(Pen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        PreparePen(pen);
        float o = PixelOffset(pen);
        Canvas.DrawArc(new SKRect(x + o, y + o, x + o + width, y + o + height), startAngle, sweepAngle, useCenter: true, _paint);
    }

    public void DrawPolygon(Pen pen, Point[] points) => DrawPolygon(pen, GraphicsPath.ToPointF(points));

    public void DrawPolygon(Pen pen, PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2) { ArgumentNullException.ThrowIfNull(pen); return; }
        PreparePen(pen);
        float o = PixelOffset(pen);
        using var path = new SKPath();
        path.MoveTo(points[0].X + o, points[0].Y + o);
        for (int i = 1; i < points.Length; i++) path.LineTo(points[i].X + o, points[i].Y + o);
        path.Close();
        Canvas.DrawPath(path, _paint);
    }

    public void DrawBezier(Pen pen, Point pt1, Point pt2, Point pt3, Point pt4) => DrawBezier(pen, (PointF)pt1, pt2, pt3, pt4);

    public void DrawBezier(Pen pen, float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) =>
        DrawBezier(pen, new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));

    public void DrawBezier(Pen pen, PointF pt1, PointF pt2, PointF pt3, PointF pt4)
    {
        PreparePen(pen);
        float o = PixelOffset(pen);
        using var path = new SKPath();
        path.MoveTo(pt1.X + o, pt1.Y + o);
        path.CubicTo(pt2.X + o, pt2.Y + o, pt3.X + o, pt3.Y + o, pt4.X + o, pt4.Y + o);
        Canvas.DrawPath(path, _paint);
    }

    public void DrawBeziers(Pen pen, Point[] points) => DrawBeziers(pen, GraphicsPath.ToPointF(points));

    public void DrawBeziers(Pen pen, PointF[] points)
    {
        using var path = new GraphicsPath();
        path.AddBeziers(points);
        DrawPath(pen, path);
    }

    public void DrawCurve(Pen pen, Point[] points) => DrawCurve(pen, GraphicsPath.ToPointF(points), 0.5f);

    public void DrawCurve(Pen pen, PointF[] points) => DrawCurve(pen, points, 0.5f);

    public void DrawCurve(Pen pen, Point[] points, float tension) => DrawCurve(pen, GraphicsPath.ToPointF(points), tension);

    public void DrawCurve(Pen pen, PointF[] points, float tension)
    {
        using var path = new GraphicsPath();
        path.AddCurve(points, tension);
        DrawPath(pen, path);
    }

    public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments) => DrawCurve(pen, points, offset, numberOfSegments, 0.5f);

    public void DrawCurve(Pen pen, Point[] points, int offset, int numberOfSegments, float tension) => DrawCurve(pen, GraphicsPath.ToPointF(points), offset, numberOfSegments, tension);

    public void DrawCurve(Pen pen, PointF[] points, int offset, int numberOfSegments, float tension)
    {
        using var path = new GraphicsPath();
        path.AddCurve(points, offset, numberOfSegments, tension);
        DrawPath(pen, path);
    }

    public void DrawClosedCurve(Pen pen, Point[] points) => DrawClosedCurve(pen, GraphicsPath.ToPointF(points), 0.5f, FillMode.Alternate);

    public void DrawClosedCurve(Pen pen, PointF[] points) => DrawClosedCurve(pen, points, 0.5f, FillMode.Alternate);

    public void DrawClosedCurve(Pen pen, Point[] points, float tension, FillMode fillmode) => DrawClosedCurve(pen, GraphicsPath.ToPointF(points), tension, fillmode);

    public void DrawClosedCurve(Pen pen, PointF[] points, float tension, FillMode fillmode)
    {
        using var path = new GraphicsPath(fillmode);
        path.AddClosedCurve(points, tension);
        DrawPath(pen, path);
    }

    public void DrawPath(Pen pen, GraphicsPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        PreparePen(pen);
        float o = PixelOffset(pen);
        if (o != 0)
        {
            Canvas.Save();
            Canvas.Translate(o, o);
            Canvas.DrawPath(path.Skia, _paint);
            Canvas.Restore();
        }
        else
        {
            Canvas.DrawPath(path.Skia, _paint);
        }
    }

    // --- images --------------------------------------------------------------------

    public void DrawImage(Image image, Point point) => DrawImage(image, point.X, point.Y);

    public void DrawImage(Image image, PointF point) => DrawImage(image, point.X, point.Y);

    public void DrawImage(Image image, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImage(image, new Rectangle(x, y, image.Width, image.Height));
    }

    public void DrawImage(Image image, float x, float y)
    {
        ArgumentNullException.ThrowIfNull(image);
        DrawImage(image, new RectangleF(x, y, image.Width, image.Height));
    }

    public void DrawImage(Image image, Rectangle rect) => DrawImage(image, (RectangleF)rect);

    public void DrawImage(Image image, int x, int y, int width, int height) => DrawImage(image, new RectangleF(x, y, width, height));

    public void DrawImage(Image image, float x, float y, float width, float height) => DrawImage(image, new RectangleF(x, y, width, height));

    public void DrawImage(Image image, RectangleF rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bmp = image.SkBitmap ?? throw new ObjectDisposedException(nameof(image));
        DrawImage(image, rect, new RectangleF(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
    }

    public void DrawImage(Image image, Rectangle destRect, Rectangle srcRect, GraphicsUnit srcUnit) => DrawImage(image, (RectangleF)destRect, (RectangleF)srcRect, srcUnit);

    public void DrawImage(Image image, Rectangle destRect, int srcX, int srcY, int srcWidth, int srcHeight, GraphicsUnit srcUnit) =>
        DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit);

    public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit) =>
        DrawImage(image, (RectangleF)destRect, new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit);

    public void DrawImage(Image image, int x, int y, Rectangle srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, new RectangleF(x, y, srcRect.Width, srcRect.Height), srcRect, srcUnit);

    public void DrawImage(Image image, float x, float y, RectangleF srcRect, GraphicsUnit srcUnit) =>
        DrawImage(image, new RectangleF(x, y, srcRect.Width, srcRect.Height), srcRect, srcUnit);

    public void DrawImage(Image image, RectangleF destRect, RectangleF srcRect, GraphicsUnit srcUnit)
    {
        ArgumentNullException.ThrowIfNull(image);
        var bmp = image.SkBitmap ?? throw new ObjectDisposedException(nameof(image));
        using var img = SKImage.FromBitmap(bmp);
        var sampling = InterpolationMode is InterpolationMode.NearestNeighbor
            ? new SKSamplingOptions(SKFilterMode.Nearest)
            : new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        _paint.Reset();
        Canvas.DrawImage(img, SkiaConvert.ToSK(srcRect), SkiaConvert.ToSK(destRect), sampling, _paint);
    }

    public void DrawImage(Image image, Point[] destPoints)
    {
        ArgumentNullException.ThrowIfNull(destPoints);
        DrawImage(image, GraphicsPath.ToPointF(destPoints));
    }

    public void DrawImage(Image image, PointF[] destPoints)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(destPoints);
        if (destPoints.Length != 3) throw new ArgumentException("Three points are required.", nameof(destPoints));
        using var m = new Matrix(new RectangleF(0, 0, image.Width, image.Height), destPoints);
        Canvas.Save();
        Canvas.Concat(m.Skia);
        DrawImage(image, 0, 0);
        Canvas.Restore();
    }

    public void DrawImageUnscaled(Image image, int x, int y) => DrawImage(image, x, y);

    public void DrawImageUnscaled(Image image, Point point) => DrawImage(image, point);

    public void DrawImageUnscaled(Image image, Rectangle rect) => DrawImage(image, rect.X, rect.Y);

    public void DrawImageUnscaled(Image image, int x, int y, int width, int height) => DrawImage(image, x, y);

    public void DrawImageUnscaledAndClipped(Image image, Rectangle rect)
    {
        ArgumentNullException.ThrowIfNull(image);
        var state = Save();
        IntersectClip(rect);
        DrawImage(image, rect.X, rect.Y);
        Restore(state);
    }

    // --- text ----------------------------------------------------------------------

    public void DrawString(string? s, Font font, Brush brush, float x, float y) => DrawString(s, font, brush, new RectangleF(x, y, 0, 0), null);

    public void DrawString(string? s, Font font, Brush brush, PointF point) => DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), null);

    public void DrawString(string? s, Font font, Brush brush, float x, float y, StringFormat? format) => DrawString(s, font, brush, new RectangleF(x, y, 0, 0), format);

    public void DrawString(string? s, Font font, Brush brush, PointF point, StringFormat? format) => DrawString(s, font, brush, new RectangleF(point.X, point.Y, 0, 0), format);

    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle) => DrawString(s, font, brush, layoutRectangle, null);

    public void DrawString(string? s, Font font, Brush brush, RectangleF layoutRectangle, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(font);
        ArgumentNullException.ThrowIfNull(brush);
        if (string.IsNullOrEmpty(s)) return;

        var fmt = format ?? StringFormat.GenericDefault;
        bool unbounded = layoutRectangle.Width <= 0 && layoutRectangle.Height <= 0;
        bool wrap = !unbounded && layoutRectangle.Width > 0 && !fmt.FormatFlags.HasFlag(StringFormatFlags.NoWrap);
        bool clip = !unbounded && !fmt.FormatFlags.HasFlag(StringFormatFlags.NoClip);
        var overflow = fmt.Trimming is StringTrimming.EllipsisCharacter or StringTrimming.EllipsisWord or StringTrimming.EllipsisPath && !unbounded
            ? TextLayout.Overflow.Ellipsis
            : TextLayout.Overflow.None;

        var bounds = layoutRectangle;
        if (unbounded)
        {
            var size = TextLayout.Measure(s, font, 0, false);
            bounds = new RectangleF(layoutRectangle.X, layoutRectangle.Y, size.Width, size.Height);
        }
        // GDI+ starts glyphs about 1/6 em in from the layout rectangle.
        float pad = font.SizeInPixels / 6f;
        bounds = new RectangleF(bounds.X + pad, bounds.Y, Math.Max(0, bounds.Width - 2 * pad), bounds.Height);
        if (unbounded) bounds.Width += 2 * pad;

        if (brush is SolidBrush sb)
        {
            TextLayout.Draw(Canvas, s, font, SkiaConvert.ToSK(sb.Color), bounds, fmt.Alignment, fmt.LineAlignment, wrap, clip, overflow);
        }
        else
        {
            // Non-solid brushes: fill the glyph outlines with the brush.
            using var path = new GraphicsPath();
            path.AddString(s, font.FontFamily, (int)font.Style, font.SizeInPixels, bounds, fmt);
            PrepareBrush(brush);
            _paint.IsAntialias = true;
            Canvas.DrawPath(path.Skia, _paint);
        }
    }

    public SizeF MeasureString(string? text, Font font) => MeasureString(text, font, new SizeF(0, 0), null);

    public SizeF MeasureString(string? text, Font font, int width) => MeasureString(text, font, new SizeF(width, 0), null);

    public SizeF MeasureString(string? text, Font font, SizeF layoutArea) => MeasureString(text, font, layoutArea, null);

    public SizeF MeasureString(string? text, Font font, int width, StringFormat? format) => MeasureString(text, font, new SizeF(width, 0), format);

    public SizeF MeasureString(string? text, Font font, PointF origin, StringFormat? format) => MeasureString(text, font, new SizeF(0, 0), format);

    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (string.IsNullOrEmpty(text)) return SizeF.Empty;
        var fmt = format ?? StringFormat.GenericDefault;
        bool wrap = layoutArea.Width > 0 && !fmt.FormatFlags.HasFlag(StringFormatFlags.NoWrap);
        float pad = font.SizeInPixels / 6f;
        var size = TextLayout.Measure(text, font, wrap ? Math.Max(0, layoutArea.Width - 2 * pad) : 0, wrap);
        // GDI+ pads the measured box by roughly 1/6 em on each side; keep that quirk so
        // code that sizes boxes from MeasureString gets the room it expects.
        return new SizeF(size.Width + 2 * pad, size.Height);
    }

    public SizeF MeasureString(string? text, Font font, SizeF layoutArea, StringFormat? format, out int charactersFitted, out int linesFilled)
    {
        var s = MeasureString(text, font, layoutArea, format);
        charactersFitted = text?.Length ?? 0;
        linesFilled = string.IsNullOrEmpty(text) ? 0 : TextLayout.Break(text, font, layoutArea.Width, layoutArea.Width > 0).Count;
        return s;
    }

    // --- pixels --------------------------------------------------------------------

    public Color GetNearestColor(Color color) => color;

    public void CopyFromScreen(Point upperLeftSource, Point upperLeftDestination, Size blockRegionSize) => throw new NotSupportedException("Screen capture is not available.");

    public void CopyFromScreen(int sourceX, int sourceY, int destinationX, int destinationY, Size blockRegionSize) => throw new NotSupportedException("Screen capture is not available.");

    // --- lifetime ------------------------------------------------------------------

    public void Dispose()
    {
        _paint.Dispose();
        if (_ownsCanvas)
        {
            Canvas.Dispose();
        }
        else
        {
            Canvas.RestoreToCount(_baseSave);
        }
        GC.SuppressFinalize(this);
    }
}


public enum FlushIntention
{
    Flush = 0,
    Sync = 1,
}
