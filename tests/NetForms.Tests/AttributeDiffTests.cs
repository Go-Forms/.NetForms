using System.Text.Json;
using NetForms.Compat;
using Xunit;
using Xunit.Abstractions;

namespace NetForms.Tests;

/// <summary>
/// The gate of phase 5.0: our controls must describe themselves to a designer exactly as the real
/// ones do. A designer reads nothing but <see cref="System.ComponentModel"/> metadata - category,
/// description, default value, browsability, serialization - so any difference here is a difference
/// the user sees in the toolbox and the property grid.
///
/// Reference data comes from <c>dotnet run --project tests/NetForms.Compat -- --attrs out.json</c>
/// on Windows, pointed at by NETFORMS_WINFORMS_ATTRS. Without it the test still dumps our own
/// metadata, so a regression shows up as a diff of netforms-attrs.json.
/// </summary>
public class AttributeDiffTests
{
    private readonly ITestOutputHelper _output;

    public AttributeDiffTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Fresh instances whose ShouldSerializeValue legitimately differs (Ф5.2). Everything else must agree:
    /// a difference is a line the designer code writer would add or drop.
    /// </summary>
    private static readonly HashSet<string> KnownFreshSerializationDifferences = new(StringComparer.Ordinal)
    {
        // IntegralHeight trims a new WinForms list box to 94px, off its DefaultSize; ours keeps 96.
        // The writer always writes a control's Size anyway (the VS designer does).
        "System.Windows.Forms.ListBox.Size",
        "System.Windows.Forms.CheckedListBox.Size",
        // Designer-shadowed in VS (ControlDesigner) and in our writer; the raw TabPage answer is moot.
        "System.Windows.Forms.TabPage.Enabled",
        "System.Windows.Forms.TabPage.Visible",
        // A hosted item not yet on a strip: WinForms reports a zero margin that differs from its
        // DefaultMargin. Ours reports the default. On a strip (samples/Strips) both agree.
        "System.Windows.Forms.ToolStripProgressBar.Margin",
        "System.Windows.Forms.ToolStripTextBox.Margin",
    };

    [Fact]
    public void DesignTimeMetadataMatchesRealWinForms()
    {
        TestPlatform.Install();
        var ours = AttributeDump.Run();

        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "netforms-attrs.json"),
            JsonSerializer.Serialize(ours, new JsonSerializerOptions { WriteIndented = true }));

        var referencePath = Environment.GetEnvironmentVariable("NETFORMS_WINFORMS_ATTRS");
        if (string.IsNullOrEmpty(referencePath) || !File.Exists(referencePath))
        {
            _output.WriteLine("NETFORMS_WINFORMS_ATTRS not set: real-WinForms comparison skipped (runs on Windows CI).");
            Assert.NotEmpty(ours);
            return;
        }

        var theirs = JsonSerializer.Deserialize<SortedDictionary<string, AttributeDump.TypeDump>>(
            File.ReadAllText(referencePath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var failures = new List<string>();
        // What a fresh instance reads back is a behavioural difference, not a markup one. It is
        // collected here because the sweep is already walking every member, and reported
        // separately: see docs/PLAN.md, the open questions of phase 5.
        var actualDiffs = new List<string>();
        // Ф5.2: a fresh instance must not ask the code writer for more (or fewer) lines than WinForms.
        var serializeDiffs = new List<string>();
        int comparedTypes = 0, comparedMembers = 0, missingTypes = 0, missingMembers = 0;

        foreach (var (typeName, theirType) in theirs)
        {
            if (!ours.TryGetValue(typeName, out var ourType)) { missingTypes++; continue; }
            comparedTypes++;
            if (theirType.DefaultEvent != ourType.DefaultEvent)
                failures.Add($"{typeName}: [DefaultEvent] WinForms={theirType.DefaultEvent ?? "(none)"} NetForms={ourType.DefaultEvent ?? "(none)"}");
            if (theirType.DefaultProperty != ourType.DefaultProperty)
                failures.Add($"{typeName}: [DefaultProperty] WinForms={theirType.DefaultProperty ?? "(none)"} NetForms={ourType.DefaultProperty ?? "(none)"}");

            foreach (var (memberName, theirs2) in theirType.Members)
            {
                if (!ourType.Members.TryGetValue(memberName, out var ours2)) { missingMembers++; continue; }
                comparedMembers++;

                var where = $"{typeName}.{memberName}";
                if (theirs2.Category != ours2.Category)
                    failures.Add($"{where}: Category WinForms={theirs2.Category} NetForms={ours2.Category}");
                if (theirs2.Browsable != ours2.Browsable)
                    failures.Add($"{where}: Browsable WinForms={theirs2.Browsable} NetForms={ours2.Browsable}");
                if (theirs2.Serialization != ours2.Serialization)
                    failures.Add($"{where}: Serialization WinForms={theirs2.Serialization} NetForms={ours2.Serialization}");
                if (theirs2.Localizable != ours2.Localizable)
                    failures.Add($"{where}: Localizable WinForms={theirs2.Localizable} NetForms={ours2.Localizable}");
                if (theirs2.HasDefaultValue != ours2.HasDefaultValue || theirs2.DefaultValue != ours2.DefaultValue)
                    failures.Add($"{where}: DefaultValue WinForms={Show(theirs2)} NetForms={Show(ours2)}");
                if (theirs2.ShouldSerialize != null && ours2.ShouldSerialize != null && theirs2.ShouldSerialize != ours2.ShouldSerialize
                    && !KnownFreshSerializationDifferences.Contains(where))
                    serializeDiffs.Add($"{where}: fresh instance ShouldSerializeValue WinForms={theirs2.ShouldSerialize} NetForms={ours2.ShouldSerialize}");
                if (theirs2.HasActual && ours2.HasActual && theirs2.Actual != ours2.Actual)
                    actualDiffs.Add($"{where}: fresh instance WinForms={theirs2.Actual ?? "null"} NetForms={ours2.Actual ?? "null"}");
            }
        }

        var summary = $"compared {comparedMembers} members over {comparedTypes} types; " +
                      $"{missingTypes} types and {missingMembers} members exist in WinForms but not here " +
                      "(API coverage, not a 5.0 gate)";
        _output.WriteLine(summary);

        // The full list goes to a file: 6000 console lines are unreadable, and the file is the
        // working list while the markup is being brought in line.
        File.WriteAllLines(Path.Combine(outDir, "attrs-diff.txt"), failures.Prepend(summary));
        File.WriteAllLines(Path.Combine(outDir, "defaults-diff.txt"), actualDiffs);
        File.WriteAllLines(Path.Combine(outDir, "shouldserialize-diff.txt"), serializeDiffs);
        _output.WriteLine($"{serializeDiffs.Count} properties of a fresh instance serialize differently (see shouldserialize-diff.txt)");
        _output.WriteLine($"{actualDiffs.Count} properties start life with a different value than in WinForms (see defaults-diff.txt)");
        foreach (var f in failures.Take(40)) _output.WriteLine(f);
        failures.AddRange(serializeDiffs);
        Assert.True(failures.Count == 0,
            $"Design-time metadata differs from WinForms in {failures.Count} places:\n" +
            string.Join("\n", failures.Take(60)));

        static string Show(AttributeDump.MemberDump m) => m.HasDefaultValue ? m.DefaultValue ?? "null" : "(none)";
    }

    /// <summary>
    /// The category count of the controls named in the phase-5.0 gate. Kept as a standalone check
    /// so the number is visible even when the full reference dump is not available: before 5.0 our
    /// Button had exactly one category ("Misc"), the real one has six.
    /// </summary>
    [Theory]
    [InlineData(typeof(Button))]
    [InlineData(typeof(TextBox))]
    [InlineData(typeof(Label))]
    [InlineData(typeof(ListView))]
    [InlineData(typeof(DataGridView))]
    public void ControlsExposeSeveralPropertyCategories(Type type)
    {
        TestPlatform.Install();
        var categories = System.ComponentModel.TypeDescriptor.GetProperties(type)
            .Cast<System.ComponentModel.PropertyDescriptor>()
            .Where(p => p.IsBrowsable)
            .Select(p => p.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToArray();

        _output.WriteLine($"{type.Name}: {string.Join(", ", categories)}");
        Assert.True(categories.Length >= 5, $"{type.Name} exposes only {categories.Length} categories: {string.Join(", ", categories)}");
    }
}
