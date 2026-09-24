using System.Text.Json;
using NetForms.Compat;
using Xunit;
using Xunit.Abstractions;

namespace NetForms.Tests;

/// <summary>
/// The measurable "how much of a copy is this" test. NetForms runs the shared scenarios;
/// when NETFORMS_WINFORMS_SCENARIOS points at the JSON produced by tests/NetForms.Compat
/// (real WinForms, Windows CI), every "exact/" key must match and every "text/" key must be
/// within a few pixels. Without the file the scenarios still run and their output is saved,
/// so a regression in our own numbers shows up as a diff of netforms-scenarios.json.
/// </summary>
public class CompatDiffTests
{
    private readonly ITestOutputHelper _output;

    public CompatDiffTests(ITestOutputHelper output) => _output = output;

    private const int TextTolerance = 3;

    /// <summary>
    /// Differences kept on purpose, each with the decision (docs/PLAN.md) that explains it. Anything else
    /// that differs fails the test.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDifferences = new(StringComparer.Ordinal)
    {
        // A WinForms control without a window keeps the negative width SetBounds gave it; once the
        // window exists Win32 clamps it to 0 - which is what NetForms always does (decision 110).
        ["exact/dock/huge-left/body"] = "negative size before the handle exists",
    };

    [Fact]
    public void ScenariosMatchRealWinForms()
    {
        TestPlatform.Install();
        var ours = CompatScenarios.Run();

        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "netforms-scenarios.json"), JsonSerializer.Serialize(ours, new JsonSerializerOptions { WriteIndented = true }));

        var referencePath = Environment.GetEnvironmentVariable("NETFORMS_WINFORMS_SCENARIOS");
        if (string.IsNullOrEmpty(referencePath) || !File.Exists(referencePath))
        {
            _output.WriteLine("NETFORMS_WINFORMS_SCENARIOS not set: real-WinForms comparison skipped (runs on Windows CI).");
            Assert.NotEmpty(ours);
            return;
        }

        var theirs = JsonSerializer.Deserialize<SortedDictionary<string, string>>(File.ReadAllText(referencePath))!;
        var failures = new List<string>();
        var textDiffs = new List<string>();
        foreach (var (key, expected) in theirs)
        {
            if (!ours.TryGetValue(key, out var actual))
            {
                failures.Add($"{key}: missing in NetForms");
                continue;
            }
            if (key.StartsWith("info/", StringComparison.Ordinal))
            {
                if (expected != actual) _output.WriteLine($"info: {key}: WinForms={expected} NetForms={actual}");
                continue;
            }
            if (key.StartsWith("text/", StringComparison.Ordinal))
            {
                if (!WithinTolerance(expected, actual, TextTolerance))
                {
                    textDiffs.Add($"{key}: WinForms={expected} NetForms={actual}");
                }
                continue;
            }
            if (expected == actual) continue;
            if (KnownDifferences.TryGetValue(key, out var why))
            {
                _output.WriteLine($"known difference: {key}: WinForms={expected} NetForms={actual} ({why})");
                continue;
            }
            failures.Add($"{key}: WinForms={expected} NetForms={actual}");
        }

        foreach (var d in textDiffs) _output.WriteLine("text metric outside tolerance: " + d);
        Assert.True(failures.Count == 0, "Behaviour differs from WinForms:\n" + string.Join("\n", failures));
        Assert.True(textDiffs.Count == 0, "Text metrics differ from WinForms by more than " + TextTolerance + "px:\n" + string.Join("\n", textDiffs));
    }

    private static bool WithinTolerance(string expected, string actual, int tolerance)
    {
        var e = expected.Split(',');
        var a = actual.Split(',');
        if (e.Length != a.Length) return false;
        for (int i = 0; i < e.Length; i++)
        {
            if (!int.TryParse(e[i], out var ev) || !int.TryParse(a[i], out var av)) return e[i] == a[i];
            if (Math.Abs(ev - av) > tolerance) return false;
        }
        return true;
    }
}
