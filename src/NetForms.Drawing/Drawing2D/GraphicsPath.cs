using System;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing.Drawing2D;

public enum PathPointType : byte
{
    Start = 0,
    Line = 1,
    Bezier = 3,
    Bezier3 = 3,
    PathTypeMask = 0x07,
    DashMode = 0x10,
    PathMarker = 0x20,
    CloseSubpath = 0x80,
}

/// <summary>
/// A sequence of figures (lines, curves, shapes) over <see cref="SKPath"/>, with GDI+'s
/// figure semantics: AddLine/AddArc/AddBezier/AddCurve continue the open figure, the
/// shape adders (rectangle, ellipse, polygon) each add a closed figure of their own.
/// </summary>
public sealed class GraphicsPath : ICloneable, IDisposable
{
    private SKPath _path;
    private bool _figureOpen;

    public GraphicsPath() : this(FillMode.Alternate) { }

    public GraphicsPath(FillMode fillMode)
    {
        _path = new SKPath { FillType = ToSK(fillMode) };
    }

    public GraphicsPath(PointF[] pts, byte[] types) : this(pts, types, FillMode.Alternate) { }

    public GraphicsPath(Point[] pts, byte[] types) : this(ToPointF(pts), types, FillMode.Alternate) { }

    public GraphicsPath(PointF[] pts, byte[] types, FillMode fillMode) : this(fillMode)
    {
        ArgumentNullException.ThrowIfNull(pts);
        ArgumentNullException.ThrowIfNull(types);
        if (pts.Length != types.Length) throw new ArgumentException("Point and type arrays must have the same length.");
        int i = 0;
        while (i < pts.Length)
        {
            var t = (PathPointType)(types[i] & (byte)PathPointType.PathTypeMask);
            bool close = (types[i] & (byte)PathPointType.CloseSubpath) != 0;
            switch (t)
            {
                case PathPointType.Start:
                    _path.MoveTo(pts[i].X, pts[i].Y);
                    i++;
                    break;
                case PathPointType.Line:
                    _path.LineTo(pts[i].X, pts[i].Y);
                    i++;
                    break;
                case PathPointType.Bezier:
                    if (i + 2 < pts.Length)
                    {
                        _path.CubicTo(pts[i].X, pts[i].Y, pts[i + 1].X, pts[i + 1].Y, pts[i + 2].X, pts[i + 2].Y);
                        close = (types[i + 2] & (byte)PathPointType.CloseSubpath) != 0;
                    }
                    i += 3;
                    break;
                default:
                    i++;
                    break;
            }
            if (close) _path.Close();
        }
        _figureOpen = pts.Length > 0 && (types[^1] & (byte)PathPointType.CloseSubpath) == 0;
    }

    private GraphicsPath(SKPath path, bool figureOpen)
    {
        _path = path;
        _figureOpen = figureOpen;
    }

    internal SKPath Skia => _path;

    private static SKPathFillType ToSK(FillMode m) => m == FillMode.Winding ? SKPathFillType.Winding : SKPathFillType.EvenOdd;

    public FillMode FillMode
    {
        get => _path.FillType == SKPathFillType.Winding ? FillMode.Winding : FillMode.Alternate;
        set => _path.FillType = ToSK(value);
    }

    public int PointCount => GetData().points.Count;

    public PointF[] PathPoints => GetData().points.ToArray();

    public byte[] PathTypes => GetData().types.ToArray();

    public PathData PathData
    {
        get
        {
            var (points, types) = GetData();
            return new PathData { Points = points.ToArray(), Types = types.ToArray() };
        }
    }

    // --- figures -------------------------------------------------------------------

    /// <summary>Move to <paramref name="p"/>, or continue the current figure with a line if one is open.</summary>
    private void Begin(PointF p)
    {
        if (_figureOpen && !_path.IsEmpty)
        {
            var last = _path.LastPoint;
            if (last.X != p.X || last.Y != p.Y) _path.LineTo(p.X, p.Y);
        }
        else
        {
            _path.MoveTo(p.X, p.Y);
            _figureOpen = true;
        }
    }

    public void StartFigure() => _figureOpen = false;

    public void CloseFigure()
    {
        if (_figureOpen)
        {
            _path.Close();
            _figureOpen = false;
        }
    }

    public void CloseAllFigures()
    {
        // Close every open contour by rebuilding the path.
        var rebuilt = new SKPath { FillType = _path.FillType };
        using var it = _path.CreateRawIterator();
        var pts = new SKPoint[4];
        bool open = false;
        SKPathVerb verb;
        while ((verb = it.Next(pts)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    if (open) rebuilt.Close();
                    rebuilt.MoveTo(pts[0]);
                    open = true;
                    break;
                case SKPathVerb.Line: rebuilt.LineTo(pts[1]); break;
                case SKPathVerb.Quad: rebuilt.QuadTo(pts[1], pts[2]); break;
                case SKPathVerb.Conic: rebuilt.ConicTo(pts[1], pts[2], it.ConicWeight()); break;
                case SKPathVerb.Cubic: rebuilt.CubicTo(pts[1], pts[2], pts[3]); break;
                case SKPathVerb.Close: rebuilt.Close(); open = false; break;
            }
        }
        if (open) rebuilt.Close();
        _path.Dispose();
        _path = rebuilt;
        _figureOpen = false;
    }

    public void Reset()
    {
        _path.Reset();
        _figureOpen = false;
    }

    public PointF GetLastPoint()
    {
        var p = _path.LastPoint;
        return new PointF(p.X, p.Y);
    }

    // --- lines ---------------------------------------------------------------------

    public void AddLine(Point pt1, Point pt2) => AddLine((PointF)pt1, (PointF)pt2);

    public void AddLine(int x1, int y1, int x2, int y2) => AddLine(new PointF(x1, y1), new PointF(x2, y2));

    public void AddLine(float x1, float y1, float x2, float y2) => AddLine(new PointF(x1, y1), new PointF(x2, y2));

    public void AddLine(PointF pt1, PointF pt2)
    {
        Begin(pt1);
        _path.LineTo(pt2.X, pt2.Y);
    }

    public void AddLines(Point[] points) => AddLines(ToPointF(points));

    public void AddLines(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length == 0) return;
        Begin(points[0]);
        for (int i = 1; i < points.Length; i++) _path.LineTo(points[i].X, points[i].Y);
    }

    // --- curves --------------------------------------------------------------------

    public void AddArc(Rectangle rect, float startAngle, float sweepAngle) => AddArc((RectangleF)rect, startAngle, sweepAngle);

    public void AddArc(int x, int y, int width, int height, float startAngle, float sweepAngle) => AddArc(new RectangleF(x, y, width, height), startAngle, sweepAngle);

    public void AddArc(float x, float y, float width, float height, float startAngle, float sweepAngle) => AddArc(new RectangleF(x, y, width, height), startAngle, sweepAngle);

    public void AddArc(RectangleF rect, float startAngle, float sweepAngle)
    {
        var oval = SkiaConvert.ToSK(rect);
        // ArcTo with forceMoveTo=false connects from the current point, like GDI+.
        if (!_figureOpen || _path.IsEmpty)
        {
            _path.ArcTo(oval, startAngle, sweepAngle, true);
            _figureOpen = true;
        }
        else
        {
            _path.ArcTo(oval, startAngle, sweepAngle, false);
        }
    }

    public void AddBezier(Point pt1, Point pt2, Point pt3, Point pt4) => AddBezier((PointF)pt1, pt2, pt3, pt4);

    public void AddBezier(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4) =>
        AddBezier(new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));

    public void AddBezier(float x1, float y1, float x2, float y2, float x3, float y3, float x4, float y4) =>
        AddBezier(new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3), new PointF(x4, y4));

    public void AddBezier(PointF pt1, PointF pt2, PointF pt3, PointF pt4)
    {
        Begin(pt1);
        _path.CubicTo(pt2.X, pt2.Y, pt3.X, pt3.Y, pt4.X, pt4.Y);
    }

    public void AddBeziers(Point[] points) => AddBeziers(ToPointF(points));

    public void AddBeziers(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 4) throw new ArgumentException("At least four points are required.", nameof(points));
        Begin(points[0]);
        for (int i = 1; i + 2 < points.Length; i += 3)
        {
            _path.CubicTo(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, points[i + 2].X, points[i + 2].Y);
        }
    }

    public void AddCurve(Point[] points) => AddCurve(ToPointF(points), 0.5f);

    public void AddCurve(PointF[] points) => AddCurve(points, 0.5f);

    public void AddCurve(Point[] points, float tension) => AddCurve(ToPointF(points), tension);

    public void AddCurve(PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 2) throw new ArgumentException("At least two points are required.", nameof(points));
        AddCurve(points, 0, points.Length - 1, tension);
    }

    public void AddCurve(Point[] points, int offset, int numberOfSegments, float tension) => AddCurve(ToPointF(points), offset, numberOfSegments, tension);

    public void AddCurve(PointF[] points, int offset, int numberOfSegments, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        var beziers = CardinalSpline.ToBeziers(points, offset, numberOfSegments, tension, closed: false);
        AddBeziers(beziers);
    }

    public void AddClosedCurve(Point[] points) => AddClosedCurve(ToPointF(points), 0.5f);

    public void AddClosedCurve(PointF[] points) => AddClosedCurve(points, 0.5f);

    public void AddClosedCurve(Point[] points, float tension) => AddClosedCurve(ToPointF(points), tension);

    public void AddClosedCurve(PointF[] points, float tension)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 3) throw new ArgumentException("At least three points are required.", nameof(points));
        var beziers = CardinalSpline.ToBeziers(points, 0, points.Length, tension, closed: true);
        StartFigure();
        AddBeziers(beziers);
        CloseFigure();
    }

    // --- shapes (each a closed figure of its own) ----------------------------------

    public void AddRectangle(Rectangle rect) => AddRectangle((RectangleF)rect);

    public void AddRectangle(RectangleF rect)
    {
        _path.AddRect(SkiaConvert.ToSK(rect));
        _figureOpen = false;
    }

    public void AddRectangles(Rectangle[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var r in rects) AddRectangle(r);
    }

    public void AddRectangles(RectangleF[] rects)
    {
        ArgumentNullException.ThrowIfNull(rects);
        foreach (var r in rects) AddRectangle(r);
    }

    public void AddEllipse(Rectangle rect) => AddEllipse((RectangleF)rect);

    public void AddEllipse(int x, int y, int width, int height) => AddEllipse(new RectangleF(x, y, width, height));

    public void AddEllipse(float x, float y, float width, float height) => AddEllipse(new RectangleF(x, y, width, height));

    public void AddEllipse(RectangleF rect)
    {
        _path.AddOval(SkiaConvert.ToSK(rect));
        _figureOpen = false;
    }

    public void AddPie(Rectangle rect, float startAngle, float sweepAngle) => AddPie((float)rect.X, rect.Y, rect.Width, rect.Height, startAngle, sweepAngle);

    public void AddPie(int x, int y, int width, int height, float startAngle, float sweepAngle) => AddPie((float)x, y, width, height, startAngle, sweepAngle);

    public void AddPie(float x, float y, float width, float height, float startAngle, float sweepAngle)
    {
        StartFigure();
        var oval = new SKRect(x, y, x + width, y + height);
        _path.MoveTo(oval.MidX, oval.MidY);
        _path.ArcTo(oval, startAngle, sweepAngle, false);
        _path.Close();
        _figureOpen = false;
    }

    public void AddPolygon(Point[] points) => AddPolygon(ToPointF(points));

    public void AddPolygon(PointF[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Length < 3) throw new ArgumentException("At least three points are required.", nameof(points));
        var pts = new SKPoint[points.Length];
        for (int i = 0; i < points.Length; i++) pts[i] = new SKPoint(points[i].X, points[i].Y);
        _path.AddPoly(pts, close: true);
        _figureOpen = false;
    }

    public void AddPath(GraphicsPath addingPath, bool connect)
    {
        ArgumentNullException.ThrowIfNull(addingPath);
        if (connect && _figureOpen && !addingPath._path.IsEmpty)
        {
            var first = addingPath._path[0];
            Begin(new PointF(first.X, first.Y));
            _path.AddPath(addingPath._path, SKPathAddMode.Extend);
        }
        else
        {
            _path.AddPath(addingPath._path, SKPathAddMode.Append);
        }
        _figureOpen = addingPath._figureOpen;
    }

    // --- text ----------------------------------------------------------------------

    public void AddString(string s, FontFamily family, int style, float emSize, Point origin, StringFormat? format) =>
        AddString(s, family, style, emSize, new RectangleF(origin.X, origin.Y, 0, 0), format);

    public void AddString(string s, FontFamily family, int style, float emSize, PointF origin, StringFormat? format) =>
        AddString(s, family, style, emSize, new RectangleF(origin.X, origin.Y, 0, 0), format);

    public void AddString(string s, FontFamily family, int style, float emSize, Rectangle layoutRect, StringFormat? format) =>
        AddString(s, family, style, emSize, (RectangleF)layoutRect, format);

    public void AddString(string s, FontFamily family, int style, float emSize, RectangleF layoutRect, StringFormat? format)
    {
        ArgumentNullException.ThrowIfNull(family);
        if (string.IsNullOrEmpty(s)) return;
        // emSize is in world units (pixels here).
        using var font = new Font(family, emSize, (FontStyle)style, GraphicsUnit.Pixel);
        var skFont = font.SKFont;
        float lineHeight = TextLayout.LineHeight(font);
        var lines = TextLayout.Break(s, font, layoutRect.Width > 0 ? layoutRect.Width : 0, layoutRect.Width > 0);
        var fmt = format ?? StringFormat.GenericDefault;
        float totalHeight = lines.Count * lineHeight;
        float y = layoutRect.Height > 0
            ? fmt.LineAlignment switch
            {
                StringAlignment.Center => layoutRect.Top + (layoutRect.Height - totalHeight) / 2f,
                StringAlignment.Far => layoutRect.Bottom - totalHeight,
                _ => layoutRect.Top,
            }
            : layoutRect.Top;
        foreach (var line in lines)
        {
            float x = layoutRect.Width > 0
                ? fmt.Alignment switch
                {
                    StringAlignment.Center => layoutRect.Left + (layoutRect.Width - line.Width) / 2f,
                    StringAlignment.Far => layoutRect.Right - line.Width,
                    _ => layoutRect.Left,
                }
                : layoutRect.Left;
            using var textPath = skFont.GetTextPath(line.Text, new SKPoint(x, y - skFont.Metrics.Ascent));
            _path.AddPath(textPath, SKPathAddMode.Append);
            y += lineHeight;
        }
        _figureOpen = false;
    }

    // --- queries and transforms ----------------------------------------------------

    public RectangleF GetBounds() => GetBounds(null, null);

    public RectangleF GetBounds(Matrix? matrix) => GetBounds(matrix, null);

    public RectangleF GetBounds(Matrix? matrix, Pen? pen)
    {
        SKRect r;
        if (matrix != null)
        {
            using var copy = new SKPath(_path);
            copy.Transform(matrix.Skia);
            r = copy.TightBounds;
        }
        else
        {
            r = _path.TightBounds;
        }
        if (pen != null)
        {
            float w = pen.Width / 2f;
            r.Inflate(w, w);
        }
        return new RectangleF(r.Left, r.Top, r.Width, r.Height);
    }

    public bool IsVisible(Point point) => IsVisible((PointF)point);

    public bool IsVisible(int x, int y) => IsVisible(new PointF(x, y));

    public bool IsVisible(float x, float y) => IsVisible(new PointF(x, y));

    public bool IsVisible(PointF point) => _path.Contains(point.X, point.Y);

    public bool IsVisible(Point point, Graphics? graphics) => IsVisible(point);

    public bool IsVisible(PointF point, Graphics? graphics) => IsVisible(point);

    public bool IsOutlineVisible(Point point, Pen pen) => IsOutlineVisible((PointF)point, pen);

    public bool IsOutlineVisible(PointF point, Pen pen)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var paint = new SKPaint();
        pen.ApplyTo(paint);
        using var stroke = paint.GetFillPath(_path);
        return stroke != null && stroke.Contains(point.X, point.Y);
    }

    public void Transform(Matrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        _path.Transform(matrix.Skia);
    }

    public void Flatten() => Flatten(null, 0.25f);

    public void Flatten(Matrix? matrix) => Flatten(matrix, 0.25f);

    public void Flatten(Matrix? matrix, float flatness)
    {
        if (matrix != null) Transform(matrix);
        // Skia keeps curves exact; approximate GDI+ flattening by re-walking curves as line segments.
        var flat = new SKPath { FillType = _path.FillType };
        using var it = _path.CreateRawIterator();
        var pts = new SKPoint[4];
        SKPathVerb verb;
        SKPoint last = default;
        while ((verb = it.Next(pts)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move: flat.MoveTo(pts[0]); last = pts[0]; break;
                case SKPathVerb.Line: flat.LineTo(pts[1]); last = pts[1]; break;
                case SKPathVerb.Quad:
                    FlattenCubic(flat, last, Lerp(last, pts[1], 2f / 3f), Lerp(pts[2], pts[1], 2f / 3f), pts[2], flatness);
                    last = pts[2];
                    break;
                case SKPathVerb.Conic:
                    {
                        var quads = SKPath.ConvertConicToQuads(pts[0], pts[1], pts[2], it.ConicWeight(), 3);
                        for (int i = 0; i + 2 < quads.Length; i += 2)
                            FlattenCubic(flat, quads[i], Lerp(quads[i], quads[i + 1], 2f / 3f), Lerp(quads[i + 2], quads[i + 1], 2f / 3f), quads[i + 2], flatness);
                        last = pts[2];
                        break;
                    }
                case SKPathVerb.Cubic: FlattenCubic(flat, last, pts[1], pts[2], pts[3], flatness); last = pts[3]; break;
                case SKPathVerb.Close: flat.Close(); break;
            }
        }
        _path.Dispose();
        _path = flat;
    }

    private static SKPoint Lerp(SKPoint a, SKPoint b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    private static void FlattenCubic(SKPath into, SKPoint p0, SKPoint p1, SKPoint p2, SKPoint p3, float flatness)
    {
        float len = SKPoint.Distance(p0, p1) + SKPoint.Distance(p1, p2) + SKPoint.Distance(p2, p3);
        int n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(len / Math.Max(0.01f, flatness))));
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n, u = 1 - t;
            float x = u * u * u * p0.X + 3 * u * u * t * p1.X + 3 * u * t * t * p2.X + t * t * t * p3.X;
            float y = u * u * u * p0.Y + 3 * u * u * t * p1.Y + 3 * u * t * t * p2.Y + t * t * t * p3.Y;
            into.LineTo(x, y);
        }
    }

    public void Widen(Pen pen) => Widen(pen, null, 0.25f);

    public void Widen(Pen pen, Matrix? matrix) => Widen(pen, matrix, 0.25f);

    public void Widen(Pen pen, Matrix? matrix, float flatness)
    {
        ArgumentNullException.ThrowIfNull(pen);
        using var paint = new SKPaint();
        pen.ApplyTo(paint);
        var widened = paint.GetFillPath(_path) ?? new SKPath();
        if (matrix != null) widened.Transform(matrix.Skia);
        _path.Dispose();
        _path = widened;
        _figureOpen = false;
    }

    public void Reverse()
    {
        var reversed = new SKPath { FillType = _path.FillType };
        reversed.AddPathReverse(_path);
        _path.Dispose();
        _path = reversed;
    }

    public object Clone() => new GraphicsPath(new SKPath(_path), _figureOpen);

    public void Dispose()
    {
        _path.Dispose();
        GC.SuppressFinalize(this);
    }

    private (List<PointF> points, List<byte> types) GetData()
    {
        var points = new List<PointF>();
        var types = new List<byte>();
        using var it = _path.CreateRawIterator();
        var pts = new SKPoint[4];
        SKPathVerb verb;
        SKPoint last = default;
        while ((verb = it.Next(pts)) != SKPathVerb.Done)
        {
            switch (verb)
            {
                case SKPathVerb.Move:
                    points.Add(new PointF(pts[0].X, pts[0].Y));
                    types.Add((byte)PathPointType.Start);
                    last = pts[0];
                    break;
                case SKPathVerb.Line:
                    points.Add(new PointF(pts[1].X, pts[1].Y));
                    types.Add((byte)PathPointType.Line);
                    last = pts[1];
                    break;
                case SKPathVerb.Quad:
                    AddCubic(points, types, Lerp(last, pts[1], 2f / 3f), Lerp(pts[2], pts[1], 2f / 3f), pts[2]);
                    last = pts[2];
                    break;
                case SKPathVerb.Conic:
                    {
                        var quads = SKPath.ConvertConicToQuads(pts[0], pts[1], pts[2], it.ConicWeight(), 1);
                        for (int i = 0; i + 2 < quads.Length; i += 2)
                            AddCubic(points, types, Lerp(quads[i], quads[i + 1], 2f / 3f), Lerp(quads[i + 2], quads[i + 1], 2f / 3f), quads[i + 2]);
                        last = pts[2];
                        break;
                    }
                case SKPathVerb.Cubic:
                    AddCubic(points, types, pts[1], pts[2], pts[3]);
                    last = pts[3];
                    break;
                case SKPathVerb.Close:
                    if (types.Count > 0) types[^1] |= (byte)PathPointType.CloseSubpath;
                    break;
            }
        }
        return (points, types);
    }

    private static void AddCubic(List<PointF> points, List<byte> types, SKPoint c1, SKPoint c2, SKPoint end)
    {
        points.Add(new PointF(c1.X, c1.Y));
        points.Add(new PointF(c2.X, c2.Y));
        points.Add(new PointF(end.X, end.Y));
        types.Add((byte)PathPointType.Bezier);
        types.Add((byte)PathPointType.Bezier);
        types.Add((byte)PathPointType.Bezier);
    }

    internal static PointF[] ToPointF(Point[] pts)
    {
        ArgumentNullException.ThrowIfNull(pts);
        var r = new PointF[pts.Length];
        for (int i = 0; i < pts.Length; i++) r[i] = pts[i];
        return r;
    }
}

public sealed class PathData
{
    public PointF[]? Points { get; set; }
    public byte[]? Types { get; set; }
}

/// <summary>GDI+ cardinal splines (DrawCurve/AddCurve) as cubic Béziers.</summary>
internal static class CardinalSpline
{
    public static PointF[] ToBeziers(PointF[] points, int offset, int segments, float tension, bool closed)
    {
        if (offset < 0 || offset >= points.Length) throw new ArgumentOutOfRangeException(nameof(offset));
        int count = closed ? points.Length : segments + 1;
        if (!closed && offset + count > points.Length) throw new ArgumentOutOfRangeException(nameof(segments));
        if (count < 2) return Array.Empty<PointF>();

        // tension 0 = straight lines, 0.5 = GDI+ default, 1 = very loose.
        float k = tension / 3f;
        var result = new List<PointF> { points[offset] };
        int n = closed ? count : count - 1;
        for (int i = 0; i < n; i++)
        {
            var p0 = Pt(points, offset, count, i - 1, closed);
            var p1 = Pt(points, offset, count, i, closed);
            var p2 = Pt(points, offset, count, i + 1, closed);
            var p3 = Pt(points, offset, count, i + 2, closed);
            result.Add(new PointF(p1.X + k * (p2.X - p0.X), p1.Y + k * (p2.Y - p0.Y)));
            result.Add(new PointF(p2.X - k * (p3.X - p1.X), p2.Y - k * (p3.Y - p1.Y)));
            result.Add(p2);
        }
        return result.ToArray();
    }

    private static PointF Pt(PointF[] points, int offset, int count, int i, bool closed)
    {
        if (closed)
        {
            i = ((i % count) + count) % count;
        }
        else
        {
            i = Math.Clamp(i, 0, count - 1);
        }
        return points[offset + i];
    }
}
