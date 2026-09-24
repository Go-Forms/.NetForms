using System.Drawing.Drawing2D;
using Xunit;

namespace NetForms.Tests;

public class DrawingTests
{
    private static Bitmap Render(int w, int h, Action<Graphics> paint)
    {
        var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            paint(g);
        }
        return bmp;
    }

    [Fact]
    public void FillRectangleCoversExactlyItsPixels()
    {
        using var bmp = Render(20, 20, g => g.FillRectangle(Brushes.Black, 5, 5, 10, 10));
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(4, 4).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(14, 14).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(15, 15).ToArgb());
    }

    [Fact]
    public void DrawRectangleWithOnePixelPenIsWidthPlusOneWide()
    {
        // GDI+: DrawRectangle(x, y, w, h) touches columns x..x+w inclusive.
        using var bmp = Render(20, 20, g => g.DrawRectangle(Pens.Black, 5, 5, 10, 10));
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(15, 15).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(15, 10).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(16, 10).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(10, 10).ToArgb());
    }

    [Fact]
    public void DrawLineIncludesBothEndPoints()
    {
        using var bmp = Render(20, 20, g => g.DrawLine(Pens.Black, 2, 10, 12, 10));
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(2, 10).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(12, 10).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(13, 10).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(1, 10).ToArgb());
    }

    [Fact]
    public void SetClipReplacesAndResetClipReturnsToBase()
    {
        using var bmp = Render(30, 30, g =>
        {
            g.SetClip(new Rectangle(0, 0, 10, 10));
            g.SetClip(new Rectangle(20, 20, 10, 10)); // replaces, not intersects
            g.FillRectangle(Brushes.Black, 0, 0, 30, 30);
            g.ResetClip();
            g.FillRectangle(Brushes.Red, 0, 0, 5, 5);
        });
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(25, 25).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), bmp.GetPixel(2, 2).ToArgb());
    }

    [Fact]
    public void ChildGraphicsCannotEscapeItsBounds()
    {
        // The Graphics a control paints through must not let ResetClip reach the parent.
        var parent = new Control { Size = new Size(40, 40), BackColor = Color.White };
        var child = new Control { Bounds = new Rectangle(10, 10, 10, 10), BackColor = Color.White };
        child.Paint += (_, e) =>
        {
            e.Graphics.ResetClip();
            e.Graphics.ResetTransform();
            e.Graphics.FillRectangle(Brushes.Black, -100, -100, 300, 300);
        };
        parent.Controls.Add(child);
        using var bmp = new Bitmap(40, 40);
        parent.DrawToBitmap(bmp, new Rectangle(0, 0, 40, 40));
        Assert.Equal(Color.Black.ToArgb(), bmp.GetPixel(15, 15).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(5, 5).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bmp.GetPixel(25, 25).ToArgb());
    }

    [Fact]
    public void TransformRoundTrips()
    {
        using var bmp = new Bitmap(10, 10);
        using var g = Graphics.FromImage(bmp);
        g.TranslateTransform(5, 7);
        g.ScaleTransform(2, 3);
        var m = g.Transform;
        Assert.Equal(new[] { 2f, 0f, 0f, 3f, 5f, 7f }, m.Elements);

        g.ResetTransform();
        Assert.True(g.Transform.IsIdentity);

        g.Transform = m;
        var pts = new[] { new PointF(1, 1) };
        g.Transform.TransformPoints(pts);
        Assert.Equal(new PointF(7, 10), pts[0]);
    }

    [Fact]
    public void MatrixPrependAndAppendOrder()
    {
        var m = new Matrix();
        m.Translate(10, 0);
        m.Scale(2, 2); // prepend: scale happens first, then translate
        var p = new[] { new PointF(1, 1) };
        m.TransformPoints(p);
        Assert.Equal(new PointF(12, 2), p[0]);

        var a = new Matrix();
        a.Translate(10, 0);
        a.Scale(2, 2, MatrixOrder.Append); // append: translate first, then scale
        var q = new[] { new PointF(1, 1) };
        a.TransformPoints(q);
        Assert.Equal(new PointF(22, 2), q[0]);
    }

    [Fact]
    public void GraphicsPathReportsPointsAndTypes()
    {
        using var path = new GraphicsPath();
        path.AddLine(0, 0, 10, 0);
        path.AddLine(10, 0, 10, 10);
        path.CloseFigure();
        path.AddRectangle(new Rectangle(20, 20, 5, 5));

        var types = path.PathTypes;
        var points = path.PathPoints;
        Assert.Equal((byte)PathPointType.Start, types[0]);
        Assert.Equal((byte)PathPointType.Line, types[1]);
        Assert.Equal((byte)(PathPointType.Line | PathPointType.CloseSubpath), types[2]);
        Assert.Equal(new PointF(10, 10), points[2]);
        Assert.Equal((byte)PathPointType.Start, types[3]);
        Assert.Equal(7, path.PointCount);
        Assert.Equal(new RectangleF(0, 0, 25, 25), path.GetBounds());
        Assert.True(path.IsVisible(22, 22));
        Assert.False(path.IsVisible(15, 15));
    }

    [Fact]
    public void RegionSetOperations()
    {
        using var r = new Region(new Rectangle(0, 0, 10, 10));
        r.Union(new Rectangle(20, 0, 10, 10));
        Assert.True(r.IsVisible(5, 5));
        Assert.True(r.IsVisible(25, 5));
        Assert.False(r.IsVisible(15, 5));

        r.Exclude(new Rectangle(0, 0, 5, 10));
        Assert.False(r.IsVisible(2, 5));
        Assert.True(r.IsVisible(7, 5));

        using var inf = new Region();
        Assert.True(inf.IsInfinite(null!));
        inf.Intersect(new Rectangle(1, 1, 2, 2));
        Assert.False(inf.IsInfinite(null!));
        Assert.Equal(new RectangleF(1, 1, 2, 2), inf.GetBounds(null!));
    }

    [Fact]
    public void LinearGradientInterpolatesBetweenItsColours()
    {
        using var bmp = Render(100, 10, g =>
        {
            using var brush = new LinearGradientBrush(new Rectangle(0, 0, 100, 10), Color.Black, Color.White, LinearGradientMode.Horizontal);
            g.FillRectangle(brush, 0, 0, 100, 10);
        });
        Assert.True(bmp.GetPixel(2, 5).R < 20);
        Assert.True(bmp.GetPixel(97, 5).R > 235);
        int mid = bmp.GetPixel(50, 5).R;
        Assert.InRange(mid, 110, 145);
    }

    [Fact]
    public void PrivateFontCollectionWinsOverInstalledFonts()
    {
        var font = Golden.Font;
        Assert.Equal("DejaVu Sans", font.Name);
        Assert.Equal("DejaVu Sans", font.Typeface.FamilyName);
        var size = TextRenderer.MeasureText("Hello", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding);
        Assert.InRange(size.Width, 25, 40);
        Assert.Equal(font.Height, size.Height);
    }

    [Fact]
    public void GoldenPrimitives()
    {
        using var bmp = Render(160, 120, g =>
        {
            g.FillRectangle(Brushes.LightSteelBlue, 5, 5, 40, 30);
            g.DrawRectangle(Pens.Navy, 5, 5, 40, 30);
            g.DrawEllipse(Pens.DarkRed, 55, 5, 40, 30);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.Orange, 105, 5, 40, 30);
            g.DrawLine(new Pen(Color.Green, 3), 5, 50, 150, 70);
            using var path = new GraphicsPath();
            path.AddArc(5, 80, 30, 30, 180, 90);
            path.AddLine(20, 80, 60, 80);
            path.AddBezier(60, 80, 90, 80, 90, 110, 60, 110);
            path.CloseFigure();
            g.FillPath(new HatchBrush(HatchStyle.DiagonalCross, Color.Purple, Color.White), path);
            g.DrawPath(Pens.Purple, path);
            g.DrawCurve(Pens.Black, new[] { new Point(100, 110), new Point(115, 85), new Point(130, 110), new Point(150, 90) });
            g.DrawString("NetForms", Golden.Font, Brushes.Black, 100, 40);
        });
        Golden.Assert(bmp, "primitives");
    }

    /// <summary>
    /// Decision 112: NetForms' file APIs read a path written for Windows - backslashes, case ignored - on
    /// every system, as Sapper's <c>Image.FromFile(Directory.GetCurrentDirectory() + @"\Picture\closed.png")</c>.
    /// </summary>
    [Fact]
    public void AWindowsPathOpensTheFileOnEverySystem()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netforms-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Picture"));
        try
        {
            using (var bitmap = new Bitmap(3, 2)) bitmap.Save(Path.Combine(dir, "Picture", "closed.png"));
            using (var image = Image.FromFile(dir + @"\Picture\Closed.PNG")) Assert.Equal(new Size(3, 2), image.Size);
            using (var bitmap = new Bitmap(dir + @"\picture\closed.png")) Assert.Equal(3, bitmap.Width);
            using (var bitmap = new Bitmap(1, 1)) bitmap.Save(dir + @"\PICTURE\saved.png");
            Assert.True(File.Exists(Path.Combine(dir, "Picture", "saved.png")));
            // A missing file is FileNotFoundException, as in GDI+.
            Assert.Throws<FileNotFoundException>(() => Image.FromFile(dir + @"\Picture\missing.png"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
