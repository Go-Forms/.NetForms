using System.Drawing.Text;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Golden-image support. Text is rendered with the bundled DejaVu Sans so the pixels are
/// the same on every OS; references live in tests/NetForms.Tests/Golden/*.png and are
/// recorded on first run. Mismatches write an "-actual.png" and a "-diff.png" next to the
/// test output for inspection.
/// </summary>
internal static class Golden
{
    private static readonly Lazy<Font> s_font = new(() =>
    {
        var collection = new PrivateFontCollection();
        collection.AddFontFile(Path.Combine(AppContext.BaseDirectory, "Fonts", "DejaVuSans.ttf"));
        collection.AddFontFile(Path.Combine(AppContext.BaseDirectory, "Fonts", "DejaVuSans-Bold.ttf"));
        return new Font(collection.Families[0], 9f);
    });

    /// <summary>DejaVu Sans 9pt from the bundled file - deterministic across machines.</summary>
    public static Font Font => s_font.Value;

    private static string ReferenceDir
    {
        get
        {
            // Prefer the source tree so recorded references land in the repo.
            var dir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Golden"));
            if (!Directory.Exists(dir)) dir = Path.Combine(AppContext.BaseDirectory, "Golden");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string OutputDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "render-out");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Compare <paramref name="actual"/> with the reference named <paramref name="name"/>; record it if there is none.</summary>
    public static void Assert(Bitmap actual, string name, double maxMismatchFraction = 0.002, int channelTolerance = 16)
    {
        var referencePath = Path.Combine(ReferenceDir, name + ".png");
        actual.Save(Path.Combine(OutputDir, name + "-actual.png"));

        if (!File.Exists(referencePath))
        {
            actual.Save(referencePath);
            return;
        }

        using var expected = new Bitmap(referencePath);
        Xunit.Assert.True(expected.Width == actual.Width && expected.Height == actual.Height,
            $"{name}: size {actual.Width}x{actual.Height}, reference {expected.Width}x{expected.Height}");

        int mismatches = 0;
        using var diff = new Bitmap(actual.Width, actual.Height);
        for (int y = 0; y < actual.Height; y++)
        {
            for (int x = 0; x < actual.Width; x++)
            {
                var a = actual.GetPixel(x, y);
                var e = expected.GetPixel(x, y);
                bool same = Math.Abs(a.R - e.R) <= channelTolerance && Math.Abs(a.G - e.G) <= channelTolerance
                    && Math.Abs(a.B - e.B) <= channelTolerance && Math.Abs(a.A - e.A) <= channelTolerance;
                if (!same) mismatches++;
                diff.SetPixel(x, y, same ? Color.FromArgb(40, e) : Color.Red);
            }
        }

        double fraction = mismatches / (double)(actual.Width * actual.Height);
        if (fraction > maxMismatchFraction)
        {
            diff.Save(Path.Combine(OutputDir, name + "-diff.png"));
        }
        Xunit.Assert.True(fraction <= maxMismatchFraction,
            $"{name}: {mismatches} pixels differ ({fraction:P2}); see render-out/{name}-actual.png and -diff.png");
    }
}
