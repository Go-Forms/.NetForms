using System.Text.Json;
using NetForms.Compat;

// Modes:
//   (default)        run the shared scenarios and write their values  -> feeds CompatDiffTests
//   --attrs [file]   dump design-time metadata of every component     -> feeds AttributeDiffTests
//   --serialization [file]  what ShouldSerializeValue says for the samples -> feeds SerializationDiffTests
//   --types [file]   the public type names of WinForms + System.Drawing -> tools/NetForms.Convert/winforms-types.txt
var options = new JsonSerializerOptions { WriteIndented = true };

// Every mode runs the way a real application runs: a project starts with
// ApplicationConfiguration.Initialize() - visual styles, GDI text (TextRenderer), Segoe UI 9pt.
// Without it WinForms keeps the .NET Framework defaults, GDI+ text among them, and the text metrics
// the scenarios record would be GDI+ ones, which no application sees (the Label/CheckBox heights of
// decision 42 were fitted to exactly that; docs/PLAN.md decision 110).
if (args.Length == 0 || args[0] != "--types")
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.SetDefaultFont(new Font("Segoe UI", 9F));
}

if (args.Length > 0 && args[0] == "--attrs")
{
    var rest = args.Skip(1).ToArray();
    var includeText = rest.Contains("--text");
    var attrsOutput = rest.FirstOrDefault(a => !a.StartsWith("--")) ?? "winforms-attrs.json";
    var attrs = AttributeDump.Run(includeText);
    File.WriteAllText(attrsOutput, JsonSerializer.Serialize(attrs, options));
    Console.WriteLine($"{attrs.Count} component types written to {attrsOutput}");
    return;
}

if (args.Length > 0 && args[0] == "--serialization")
{
    var serOutput = args.Length > 1 ? args[1] : "winforms-serialization.json";
    var ser = SerializationDump.Run();
    File.WriteAllText(serOutput, JsonSerializer.Serialize(ser, options));
    Console.WriteLine($"{ser.Count} serialized properties written to {serOutput}");
    return;
}

if (args.Length > 0 && args[0] == "--types")
{
    // The public types of the real System.Windows.Forms and System.Drawing(.Common): the converter
    // (tools/NetForms.Convert) tells "not in NetForms yet" from "not in WinForms either" with it.
    var typesOutput = args.Length > 1 ? args[1] : "winforms-types.txt";
    var names = new[] { typeof(Control).Assembly, typeof(System.Drawing.Graphics).Assembly }
        .SelectMany(a => a.GetExportedTypes())
        .Select(t => (t.FullName ?? t.Name).Replace('+', '.'))
        .Where(n => !n.Contains('`'))
        .Distinct()
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToList();
    File.WriteAllLines(typesOutput, names);
    Console.WriteLine($"{names.Count} type names written to {typesOutput}");
    return;
}

var output = args.Length > 0 ? args[0] : "winforms-scenarios.json";
var results = CompatScenarios.Run();
File.WriteAllText(output, JsonSerializer.Serialize(results, options));
Console.WriteLine($"{results.Count} scenario values written to {output}");
