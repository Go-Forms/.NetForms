using System.Diagnostics;
using NetForms.Converter;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Ф5.7: converting a WinForms project. The gate in small: a project exactly as Visual Studio creates
/// it (net8.0-windows, UseWindowsForms) is converted and then builds against NetForms with plain
/// <c>dotnet build</c>; and what cannot carry over is reported with its place, before anything changes.
/// </summary>
public sealed class ConvertTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-convert-" + Guid.NewGuid().ToString("N"));

    public ConvertTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private const string VisualStudioProject = """
        <Project Sdk="Microsoft.NET.Sdk">

          <PropertyGroup>
            <OutputType>WinExe</OutputType>
            <TargetFramework>net8.0-windows</TargetFramework>
            <Nullable>enable</Nullable>
            <UseWindowsForms>true</UseWindowsForms>
            <ImplicitUsings>enable</ImplicitUsings>
          </PropertyGroup>

        </Project>
        """;

    private string MakeProject(string name, string project, params (string File, string Text)[] files)
    {
        var dir = Path.Combine(_dir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name + ".csproj"), project);
        foreach (var (file, text) in files) File.WriteAllText(Path.Combine(dir, file), text);
        return Path.Combine(dir, name + ".csproj");
    }

    private string MakeHelloProject(string name)
    {
        var project = MakeProject(name, VisualStudioProject);
        foreach (var f in new[] { "Program.cs", "MainForm.cs", "MainForm.Designer.cs" })
            File.Copy(Sample("HelloForms/" + f), Path.Combine(Path.GetDirectoryName(project)!, f));
        return project;
    }

    [Fact]
    public void AVisualStudioProjectIsAnalysedWithoutBeingChanged()
    {
        var project = MakeHelloProject("App");
        var before = File.ReadAllText(project);
        var report = new ProjectConverter().Analyze(project);
        Assert.True(report.SdkStyle);
        Assert.True(report.UsesWindowsForms);
        Assert.False(report.AlreadyNetForms);
        Assert.Equal(new[] { "net8.0-windows" }, report.TargetFrameworks);
        Assert.True(report.Compiled, string.Join("\n", report.Issues.Select(i => i.Message)));
        Assert.Contains(report.Actions, a => a.Contains("net10.0"));
        Assert.Contains(report.Actions, a => a.Contains("PackageReference Include=\"NetForms\""));
        Assert.True(Assert.Single(report.Forms).Opens);
        Assert.Equal(before, File.ReadAllText(project));
    }

    [Fact]
    public void TheConvertedProjectBuildsAgainstNetForms()
    {
        var project = MakeHelloProject("Hello");
        var report = new ProjectConverter().Apply(project, ConvertTarget.Cross, RepoRoot);
        Assert.True(report.Applied);
        Assert.True(File.Exists(project + ".winforms.bak"));
        var converted = File.ReadAllText(project);
        Assert.DoesNotMatch(@"(?m)^\s*<UseWindowsForms>", converted);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", converted);
        Assert.Contains("src/NetForms/NetForms.csproj", converted);
        Assert.Contains("<Using Include=\"System.Windows.Forms\" />", converted);

        // A second run changes nothing: the project already uses NetForms.
        Assert.True(new ProjectConverter().Analyze(project).AlreadyNetForms);

        Dotnet.Succeed(_dir, "build", project, "-c", "Release", "-nologo", "-v", "quiet");
    }

    [Fact]
    public void WindowsOnlyCodeAndMissingApiAreReportedWithTheirPlace()
    {
        var project = MakeProject("Legacy", VisualStudioProject,
            ("Native.cs", """
                using System.Runtime.InteropServices;

                namespace Legacy;

                internal static class Native
                {
                    [DllImport("user32.dll")]
                    public static extern bool MessageBeep(uint type);
                }

                public class Hooked : Form
                {
                    protected override void WndProc(ref Message m) => base.WndProc(ref m);
                }

                public class Editor : Form
                {
                    private readonly ToolStripContainer _container = new ToolStripContainer();
                }
                """));
        var report = new ProjectConverter().Analyze(project);
        Assert.False(report.Compiled);
        Assert.Contains(report.Issues, i => i.Category == "Windows API" && i.Line == 7);
        Assert.Contains(report.Issues, i => i.Category == "Win32 messages" && i.Message.Contains("WndProc"));
        Assert.Contains(report.Issues, i => i.Category.StartsWith("Missing in NetForms") && i.Message.Contains("ToolStripContainer"));
        Assert.All(report.Issues.Where(i => i.Severity == "error"), i => Assert.NotNull(i.File));
    }

    /// <summary>Copies a directory tree (a fixture) into the test's folder.</summary>
    private string CopyFixture(string name)
    {
        var from = Path.Combine(RepoRoot, "tests", "Fixtures", name);
        var to = Path.Combine(_dir, name);
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target);
        }
        return to;
    }

    /// <summary>
    /// Decision 111: a .NET Framework 4.8.1 project exactly as Visual Studio's template makes it (AssemblyInfo,
    /// Properties\Resources and Settings, a library project beside it) is moved to the SDK style and builds
    /// against NetForms - with the traps real projects set: a file left on disk but not in the project, a
    /// picture the program reads from beside the executable, committed in bin\Debug.
    /// </summary>
    [Fact]
    public void ANetFrameworkProjectIsConvertedAndBuilds()
    {
        var dir = CopyFixture("NetFrameworkApp");
        Directory.CreateDirectory(Path.Combine(dir, "bin", "Debug", "Picture"));
        File.Copy(Path.Combine(dir, "Picture", "closed.png"), Path.Combine(dir, "bin", "Debug", "Picture", "closed.png"));
        var project = Path.Combine(dir, "FrameworkApp.csproj");

        var report = new ProjectConverter().Analyze(project, ConvertTarget.Cross, RepoRoot);
        Assert.False(report.SdkStyle);
        Assert.True(report.UsesWindowsForms);
        Assert.Equal(new[] { "v4.8.1" }, report.TargetFrameworks);
        // Settings (ApplicationSettingsBase), the library's types, and not Unlisted.cs, which does not compile.
        Assert.True(report.Compiled, string.Join("\n", report.Issues.Select(i => $"{i.Severity} {i.Message} {i.File}:{i.Line}")));
        Assert.True(Assert.Single(report.Forms).Opens);
        Assert.Contains(report.Issues, i => i.Category == "Windows path" && i.Severity == "info" && i.Line == 17);    // Image.FromFile: NetForms reads it
        Assert.Contains(report.Issues, i => i.Category == "Windows path" && i.Severity == "warning" && i.Line == 26); // File.ReadAllText: it does not

        var reports = SolutionProjects.Find(Path.Combine(dir, "FrameworkApp.sln")).Select(p => new ProjectConverter().Apply(p, ConvertTarget.Cross, RepoRoot)).ToList();
        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.True(r.Applied));
        Assert.True(File.Exists(project + ".winforms.bak"));

        var converted = File.ReadAllText(project);
        Assert.StartsWith("<Project Sdk=\"Microsoft.NET.Sdk\">", converted);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", converted);
        Assert.Contains("<GenerateAssemblyInfo>false</GenerateAssemblyInfo>", converted);
        Assert.DoesNotContain("Deterministic", converted); // the "1.0.*" in AssemblyInfo is in a comment
        Assert.Contains("<Compile Remove=\"Unlisted.cs\" />", converted);
        Assert.Contains("<Compile Remove=\"Legacy\\**\" />", converted); // the library's folder is its own
        Assert.Contains("<ProjectReference Include=\"Legacy\\Legacy.csproj\" />", converted);
        Assert.Matches(@"<Content Include=""Picture\\closed.png"">\s*<CopyToOutputDirectory>PreserveNewest", converted);
        Assert.DoesNotContain("bin\\Debug", converted);

        var library = File.ReadAllText(Path.Combine(dir, "Legacy", "Legacy.csproj"));
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", library);
        Assert.DoesNotContain("NetForms", library);

        Assert.True(new ProjectConverter().Analyze(project).AlreadyNetForms);
        Dotnet.Succeed(dir, "build", project, "-nologo", "-v", "quiet");
        Assert.True(File.Exists(Path.Combine(dir, "bin", "Debug", "net10.0", "Picture", "closed.png")));
        Assert.True(File.Exists(Path.Combine(dir, "bin", "Debug", "net10.0", "Legacy.dll")));
    }

    /// <summary>
    /// Decision 113: what cannot change the program's meaning is mended by the conversion - a using of a
    /// namespace .NET lacks (it imports nothing), a .resx file reference whose case only Windows forgives.
    /// </summary>
    [Fact]
    public void UnresolvableUsingsAndFileReferenceCaseAreMended()
    {
        var project = MakeHelloProject("Mend");
        var dir = Path.GetDirectoryName(project)!;
        File.WriteAllText(Path.Combine(dir, "Extra.cs"), "using System.Windows.Controls;\nusing System.Runtime.Remoting.Messaging;\nusing System.Text;\nusing Newtonsoft.Json;\n\nnamespace Mend;\n\ninternal static class Extra\n{\n    public static StringBuilder Text() => new StringBuilder();\n}\n");
        Directory.CreateDirectory(Path.Combine(dir, "Resources"));
        File.Copy(Path.Combine(RepoRoot, "tests", "Fixtures", "NetFrameworkApp", "Picture", "closed.png"), Path.Combine(dir, "Resources", "Pawn.png"));
        File.WriteAllText(Path.Combine(dir, "Images.resx"), """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
              <resheader name="version"><value>2.0</value></resheader>
              <resheader name="reader"><value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
              <resheader name="writer"><value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></resheader>
              <assembly alias="System.Windows.Forms" name="System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" />
              <data name="pawn" type="System.Resources.ResXFileRef, System.Windows.Forms">
                <value>resources\pawn.png;System.Drawing.Bitmap, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>
              </data>
            </root>
            """);

        var report = new ProjectConverter().Analyze(project, ConvertTarget.Cross, RepoRoot);
        // Newtonsoft.Json does not resolve either (no package), but it is a package's namespace: it stays.
        Assert.Equal(2, report.Fixes.Count(f => f.File.EndsWith("Extra.cs")));
        Assert.DoesNotContain(report.Fixes, f => f.Old.Contains("Newtonsoft"));
        bool windows = OperatingSystem.IsWindows(); // there the reference is found as written
        Assert.Equal(windows ? 0 : 1, report.Fixes.Count(f => f.File.EndsWith("Images.resx")));

        new ProjectConverter().Apply(project, ConvertTarget.Cross, RepoRoot);
        var extra = File.ReadAllText(Path.Combine(dir, "Extra.cs"));
        Assert.StartsWith("using System.Text;\nusing Newtonsoft.Json;", extra.ReplaceLineEndings("\n"));
        Assert.True(File.Exists(Path.Combine(dir, "Extra.cs.winforms.bak")));
        if (!windows) Assert.Contains(@"Resources\Pawn.png;", File.ReadAllText(Path.Combine(dir, "Images.resx")));

        // Without the package's using (the test has no package), the mended project builds.
        File.WriteAllText(Path.Combine(dir, "Extra.cs"), extra.Replace("using Newtonsoft.Json;", ""));
        Dotnet.Succeed(dir, "build", project, "-nologo", "-v", "quiet");
    }

    [Fact]
    public void ResxImagesCarryOverAndBinaryFormatterEntriesAreNamed()
    {
        var project = MakeHelloProject("Res");
        var dir = Path.GetDirectoryName(project)!;
        File.Copy(Sample("Gallery/ResourcesForm.resx"), Path.Combine(dir, "MainForm.resx"));
        Assert.DoesNotContain(new ProjectConverter().Analyze(project).Issues, i => i.Category == "Resources");

        File.WriteAllText(Path.Combine(dir, "Other.resx"), """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="imageList1.ImageStream" mimetype="application/x-microsoft.net.object.binary.base64"><value>AAEAAAD/////</value></data>
            </root>
            """);
        var issue = Assert.Single(new ProjectConverter().Analyze(project).Issues, i => i.Category == "Resources");
        Assert.Contains("imageList1.ImageStream", issue.Message);

        // A project reference carries no props: the converted project says what UseWindowsForms said.
        new ProjectConverter().Apply(project, ConvertTarget.Cross, RepoRoot);
        Assert.Contains("<GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources>", File.ReadAllText(project));
    }

    [Fact]
    public void TheWindowsOnlyTargetKeepsTheWindowsTfm()
    {
        var project = MakeHelloProject("WinOnly");
        new ProjectConverter().Apply(project, ConvertTarget.Windows, RepoRoot);
        Assert.Contains("<TargetFramework>net10.0-windows</TargetFramework>", File.ReadAllText(project));
    }
}
