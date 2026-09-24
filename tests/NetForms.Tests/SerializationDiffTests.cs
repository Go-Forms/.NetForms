using System.Text.Json;
using NetForms.Compat;
using Xunit;
using Xunit.Abstractions;

namespace NetForms.Tests;

/// <summary>
/// Ф5.2 oracle: for every component of the sample forms, the properties whose descriptor says
/// <c>ShouldSerializeValue == true</c> are the ones WinForms says - the code writer emits exactly
/// those, so a difference here is a line of designer code added or lost.
///
/// Reference data: <c>dotnet run --project tests/NetForms.Compat -- --serialization out.json</c>
/// (Windows), pointed at by NETFORMS_WINFORMS_SERIALIZATION. Without it the test only writes our
/// own dump to render-out/netforms-serialization.json.
/// </summary>
public class SerializationDiffTests
{
    private readonly ITestOutputHelper _output;

    public SerializationDiffTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Keys whose value legitimately differs: the value depends on text metrics and the default font
    /// (the reference process runs without <c>ApplicationConfiguration.Initialize()</c>, so its
    /// default font is Microsoft Sans Serif 8.25pt, ours Segoe UI 9pt - decision 9), or on a
    /// documented layout difference. Their presence is still compared.
    /// </summary>
    private static bool ValueDependsOnMetrics(string key) =>
        key.EndsWith(".Size", StringComparison.Ordinal)
        || key.EndsWith(".ClientSize", StringComparison.Ordinal)
        || key.EndsWith(".Location", StringComparison.Ordinal)
        || key.EndsWith(".ItemHeight", StringComparison.Ordinal)
        || key.EndsWith(".Font", StringComparison.Ordinal);

    /// <summary>
    /// Presence differences that are understood, each with its reason. Anything else is a failure.
    /// </summary>
    private static string? KnownDifference(string key)
    {
        // GenLabs was designed at 6x13 (MS Sans Serif); WinForms rescales it to Segoe UI's 7x15 on
        // load, so every Margin/Padding becomes non-default there. We do not scale (decision 8).
        if (key.StartsWith("Form1/", StringComparison.Ordinal)
            && (key.EndsWith(".Margin", StringComparison.Ordinal) || key.EndsWith(".Padding", StringComparison.Ordinal) || key.EndsWith(".Size", StringComparison.Ordinal)))
            return "AutoScale of a 6x13 form (decision 8)";
        // An unshown, maximized form reads (-1,-1) in WinForms; ours stays at the origin.
        if (key == "Form1/(root).Location") return "location of an unshown maximized form";
        // The raw TabPage descriptors say "serialize Enabled/Visible"; the VS designer shadows both
        // properties (ControlDesigner), and so does our writer.
        if (key.Contains("/tabPage", StringComparison.Ordinal)
            && (key.EndsWith(".Enabled", StringComparison.Ordinal) || key.EndsWith(".Visible", StringComparison.Ordinal)))
            return "designer-shadowed on TabPage";
        // The edit/buttons inside ComboBox and NumericUpDown are child controls here, native parts
        // there (decision 26). They are not components, so the writer never emits them.
        if ((key.Contains("/comboBox", StringComparison.Ordinal) || key.Contains("/numericUpDown", StringComparison.Ordinal))
            && key.EndsWith(".Controls", StringComparison.Ordinal))
            return "inner edit controls (decision 26)";
        // WinForms' DataGridView keeps its scroll bars as child controls; they are not components,
        // the VS designer never writes them, nor does ours (ours are drawn, not children).
        if (key.Contains("/dataGridView", StringComparison.Ordinal) && key.EndsWith(".Controls", StringComparison.Ordinal))
            return "DataGridView scroll bars";
        return null;
    }

    [Fact]
    public void SampleComponentsSerializeTheSamePropertiesAsWinForms()
    {
        TestPlatform.Install();
        var ours = SerializationDump.Run();

        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "netforms-serialization.json"),
            JsonSerializer.Serialize(ours, new JsonSerializerOptions { WriteIndented = true }));

        var referencePath = Environment.GetEnvironmentVariable("NETFORMS_WINFORMS_SERIALIZATION");
        if (string.IsNullOrEmpty(referencePath) || !File.Exists(referencePath))
        {
            _output.WriteLine("NETFORMS_WINFORMS_SERIALIZATION not set: real-WinForms comparison skipped (runs on Windows CI).");
            Assert.NotEmpty(ours);
            return;
        }

        var theirs = JsonSerializer.Deserialize<SortedDictionary<string, string?>>(File.ReadAllText(referencePath))!;
        var failures = new List<string>();
        var valueDiffs = new List<string>();
        foreach (var key in theirs.Keys.Union(ours.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            if (KnownDifference(key) != null) continue;
            bool inTheirs = theirs.TryGetValue(key, out var t), inOurs = ours.TryGetValue(key, out var o);
            if (inTheirs && !inOurs) failures.Add($"{key}: WinForms writes it ({t}), NetForms does not");
            else if (!inTheirs && inOurs) failures.Add($"{key}: NetForms writes it ({o}), WinForms does not");
            else if (t != o)
            {
                if (ValueDependsOnMetrics(key)) valueDiffs.Add($"{key}: WinForms={t} NetForms={o}");
                else failures.Add($"{key}: WinForms={t} NetForms={o}");
            }
        }

        File.WriteAllLines(Path.Combine(outDir, "serialization-diff.txt"), failures.Concat(valueDiffs.Select(v => "(metrics) " + v)));
        _output.WriteLine($"{theirs.Count} reference keys, {ours.Count} ours; {failures.Count} differences, {valueDiffs.Count} metric-only value differences");
        foreach (var f in failures.Take(60)) _output.WriteLine(f);
        Assert.True(failures.Count == 0,
            $"Serialized properties differ from WinForms in {failures.Count} places:\n" + string.Join("\n", failures.Take(80)));
    }
}
