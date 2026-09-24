using System.Diagnostics;
using NetForms.Design.Serialization;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Ф5.6: the <c>dotnet new</c> templates (templates/). Their designer files are exactly what the
/// designer writes - opening a new form and saving it changes nothing; their sources compile against
/// NetForms; and <c>dotnet new netforms</c> + <c>dotnet new netforms-form</c> give a project that builds.
/// </summary>
public sealed class TemplateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-templates-" + Guid.NewGuid().ToString("N"));

    public TemplateTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Templates => Path.Combine(RepoRoot, "templates");

    public static TheoryData<string, string> DesignerFiles => new()
    {
        { "netforms-app", "MainForm.Designer.cs" },
        { "netforms-form", "Form1.Designer.cs" },
        { "netforms-usercontrol", "UserControl1.Designer.cs" },
    };

    /// <summary>
    /// A new project references the NetForms package of this very release: the version in the app template is
    /// the one Directory.Build.props gives every package, as the converter writes it (docs/RELEASING.md).
    /// </summary>
    [Fact]
    public void TheAppTemplateReferencesTheReleasedPackageVersion()
    {
        var csproj = File.ReadAllText(Path.Combine(Templates, "netforms-app", "NetFormsApp1.csproj"));
        Assert.Contains($"<PackageReference Include=\"NetForms\" Version=\"{NetForms.Converter.ProjectConverter.PackageVersion}\" />", csproj);
        Assert.DoesNotContain("0.0.0", NetForms.Converter.ProjectConverter.PackageVersion);
    }

    [Theory]
    [MemberData(nameof(DesignerFiles))]
    public void ATemplateDesignerFileIsWhatTheDesignerWrites(string template, string file)
    {
        TestPlatform.Install();
        var path = Path.Combine(Templates, template, file);
        var source = File.ReadAllText(path);
        var model = new DesignerCodeReader().ReadFile(path);
        Assert.Equal(source, new DesignerCodeWriter().Write(model, source));
    }

    [Theory]
    [MemberData(nameof(DesignerFiles))]
    public void ATemplateCompilesAgainstNetForms(string template, string file)
    {
        var path = Path.Combine(Templates, template, file);
        var assembly = DesignerCodeWriterTests.Compile(Path.GetDirectoryName(path)!, path, File.ReadAllText(path));
        var root = Path.GetFileName(path).Replace(".Designer.cs", "");
        Assert.Contains(assembly.GetTypes(), t => t.Name == root && typeof(ContainerControl).IsAssignableFrom(t));
    }

    [Fact]
    public void ANewProjectWithANewFormBuilds()
    {
        var hive = Path.Combine(_dir, "hive");
        Run(_dir, "new", "install", Templates, "--debug:custom-hive", hive);
        Run(_dir, "new", "netforms", "-n", "Demo", "--FrameworkPath", RepoRoot, "--debug:custom-hive", hive);
        var project = Path.Combine(_dir, "Demo");
        // Like the SDK's own item templates, ours read the project (a C# one, restored) for its namespace.
        Run(project, "restore", "-nologo", "-v", "quiet");
        Run(project, "new", "netforms-form", "-n", "SettingsForm", "--debug:custom-hive", hive);
        Run(project, "new", "netforms-usercontrol", "-n", "Toolbar", "--debug:custom-hive", hive);

        // The item templates take the project's namespace (msbuild:RootNamespace), as the SDK's own do.
        Assert.StartsWith("namespace Demo", File.ReadAllText(Path.Combine(project, "SettingsForm.Designer.cs")));
        Assert.Contains("partial class Toolbar", File.ReadAllText(Path.Combine(project, "Toolbar.Designer.cs")));
        Assert.Contains("ProjectReference", File.ReadAllText(Path.Combine(project, "Demo.csproj")));

        Run(project, "build", "-c", "Release", "-nologo", "-v", "quiet");
    }

    private static void Run(string dir, params string[] args) => Dotnet.Succeed(dir, args);
}
