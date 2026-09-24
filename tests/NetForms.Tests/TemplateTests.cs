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

    /// <summary>
    /// The way a user gets NetForms: packages from a feed, templates from their package. The five runtime
    /// packages and the templates are packed from a copy of the sources (so the build does not share
    /// bin/obj with tests running in parallel) under a version of their own, the templates are installed
    /// from the .nupkg, and the new project restores NetForms from that feed - no reference to this checkout.
    /// </summary>
    [Fact]
    public void ANewProjectFromTheTemplatePackageBuildsFromTheNetFormsPackages()
    {
        var version = "0.0.0-templatetest." + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "." + Environment.ProcessId;
        var sources = Path.Combine(_dir, "src-copy");
        foreach (var part in new[] { "src", "templates", "eng" }) CopyTree(Path.Combine(RepoRoot, part), Path.Combine(sources, part));
        foreach (var file in new[] { "Directory.Build.props", "Directory.Packages.props" }) File.Copy(Path.Combine(RepoRoot, file), Path.Combine(sources, file));
        // The app template names the released version; this run's packages carry their own.
        var appProject = Path.Combine(sources, "templates", "netforms-app", "NetFormsApp1.csproj");
        File.WriteAllText(appProject, File.ReadAllText(appProject).Replace($"Version=\"{NetForms.Converter.ProjectConverter.PackageVersion}\"", $"Version=\"{version}\""));

        var feed = Path.Combine(_dir, "feed");
        foreach (var project in new[] { "NetForms", "NetForms.Drawing", "NetForms.Drawing.Common", "NetForms.Platform", "NetForms.Platform.Avalonia" })
            Run(sources, "pack", Path.Combine("src", project, project + ".csproj"), "-c", "Release", "-o", feed, "-p:Version=" + version, "-nologo", "-v", "quiet");
        Run(sources, "pack", Path.Combine("templates", "NetForms.Templates.csproj"), "-o", feed, "-p:Version=" + version, "-nologo", "-v", "quiet");
        File.WriteAllText(Path.Combine(_dir, "nuget.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <add key="netforms-test" value="{feed}" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """);

        var work = Path.Combine(_dir, "work");
        Directory.CreateDirectory(work);
        var hive = Path.Combine(_dir, "hive");
        try
        {
            Run(work, "new", "install", Path.Combine(feed, $"NetForms.Templates.{version}.nupkg"), "--debug:custom-hive", hive);
            Run(work, "new", "netforms", "-n", "Demo", "--debug:custom-hive", hive);
            var project = Path.Combine(work, "Demo");
            var csproj = File.ReadAllText(Path.Combine(project, "Demo.csproj"));
            Assert.Contains($"<PackageReference Include=\"NetForms\" Version=\"{version}\" />", csproj);
            Assert.DoesNotContain("ProjectReference", csproj);

            // Like the SDK's own item templates, ours read the project (a C# one, restored) for its namespace.
            Run(project, "restore", "-nologo", "-v", "quiet");
            Run(project, "new", "netforms-form", "-n", "SettingsForm", "--debug:custom-hive", hive);
            Run(project, "new", "netforms-usercontrol", "-n", "Toolbar", "--debug:custom-hive", hive);
            // ...or take it as a parameter, as the VS Code extension passes it (with the folder, as VS does).
            Directory.CreateDirectory(Path.Combine(project, "Views"));
            Run(Path.Combine(project, "Views"), "new", "netforms-form", "-n", "AboutForm", "--Namespace", "Demo.Views", "--debug:custom-hive", hive);

            Assert.StartsWith("namespace Demo", File.ReadAllText(Path.Combine(project, "SettingsForm.Designer.cs")));
            Assert.Contains("partial class Toolbar", File.ReadAllText(Path.Combine(project, "Toolbar.Designer.cs")));
            Assert.StartsWith("namespace Demo.Views", File.ReadAllText(Path.Combine(project, "Views", "AboutForm.Designer.cs")));

            Run(project, "build", "-c", "Release", "-nologo", "-v", "quiet");
            // The facades come with the package, next to NetForms.dll.
            var output = Path.Combine(project, "bin", "Release", "net10.0");
            Assert.True(File.Exists(Path.Combine(output, "NetForms.dll")));
            Assert.True(File.Exists(Path.Combine(output, "System.Windows.Forms.dll")));
        }
        finally
        {
            // This run's packages in the global packages folder, so they do not pile up.
            var (_, locals) = Dotnet.Run(_dir, "nuget", "locals", "global-packages", "--list");
            var packages = locals.Split(':', 2).ElementAtOrDefault(1)?.Trim();
            if (!string.IsNullOrEmpty(packages) && Directory.Exists(packages))
                foreach (var id in Directory.GetDirectories(packages, "netforms*"))
                    try { Directory.Delete(Path.Combine(id, version), recursive: true); } catch (DirectoryNotFoundException) { } catch (IOException) { }
        }
    }

    private static void CopyTree(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var dir in Directory.GetDirectories(from))
        {
            var name = Path.GetFileName(dir);
            if (name is "bin" or "obj") continue;
            CopyTree(dir, Path.Combine(to, name));
        }
        foreach (var file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
    }

    private static void Run(string dir, params string[] args) => Dotnet.Succeed(dir, args);
}
