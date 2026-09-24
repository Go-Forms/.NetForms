using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetForms.Converter;

// dotnet NetForms.Convert.dll <project.csproj | solution | folder> [--apply] [--target cross|windows] [--framework-path <NetForms checkout>] [--json]
//
// Without --apply it only reports. With --json the report is one JSON object on stdout (what the
// VS Code command reads); otherwise it is printed for a person.
if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("Converts a Windows Forms project to NetForms (Windows and Linux).");
    Console.WriteLine("usage: NetForms.Convert <project.csproj | .sln | folder> [--apply] [--target cross|windows] [--framework-path <dir>] [--json]");
    return args.Length == 0 ? 1 : 0;
}

bool apply = args.Contains("--apply");
bool json = args.Contains("--json");
var target = ArgValue("--target") is "windows" ? ConvertTarget.Windows : ConvertTarget.Cross;
var frameworkPath = ArgValue("--framework-path");

// A project, a solution, or a folder: a folder with one project is that project; otherwise every project
// of its solution, or under it. Each is converted on its own - libraries too (an SDK project cannot
// reference a .NET Framework one).
List<string> projects;
try
{
    projects = SolutionProjects.Find(args[0]);
}
catch (IOException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

var converter = new ProjectConverter();
var reports = new List<ConvertReport>();
foreach (var project in projects)
{
    try
    {
        reports.Add(apply ? converter.Apply(project, target, frameworkPath) : converter.Analyze(project, target, frameworkPath));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"{project}: {(Environment.GetEnvironmentVariable("NETFORMS_DEBUG") != null ? ex : ex.Message)}");
        return 3;
    }
}

if (json)
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
    // One project: one object (what the VS Code command reads); several: an array of them.
    Console.WriteLine(reports.Count == 1 ? JsonSerializer.Serialize(reports[0], options) : JsonSerializer.Serialize(reports, options));
    return 0;
}

foreach (var report in reports)
{
    if (reports.Count > 1) Console.WriteLine();
    Console.WriteLine($"{Path.GetFileName(report.Project)}: target {string.Join(", ", report.TargetFrameworks)}, WinForms: {(report.UsesWindowsForms ? "yes" : "no")}");
    if (report.Actions.Count > 0)
    {
        Console.WriteLine(apply && report.Applied ? "Changed:" : "Would change:");
        foreach (var a in report.Actions) Console.WriteLine("  - " + a);
    }
    Console.WriteLine(report.Compiled ? "The sources compile against NetForms." : $"{report.Issues.Count(i => i.Severity == "error")} error(s) against NetForms:");
    foreach (var i in report.Issues.Take(200))
        Console.WriteLine($"  {i.Severity,-7} {i.Category}: {i.Message}{(i.File != null ? $" ({Path.GetFileName(i.File)}:{i.Line})" : "")}");
    foreach (var f in report.Forms)
        Console.WriteLine($"  form {(f.Opens ? "ok  " : "FAIL")} {Path.GetFileName(f.File)}{(f.Message != null ? " - " + f.Message : "")}");
    if (report.Applied) Console.WriteLine($"Converted; the original project file is {report.Backup}.");
}
return 0;

string? ArgValue(string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}
