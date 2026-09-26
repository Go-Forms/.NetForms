using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetForms.Design;
using NetForms.Design.Serialization;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Decision 157 (docs/designer-control-libraries.md): the designer takes controls from the user's project
/// and the libraries it references. The fixture is tests/Fixtures/ControlLibrary, built for NetForms; the
/// tests load its build output as the host loads a project's - a shadow copy in a collectible context.
/// </summary>
public sealed class ControlLibraryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-libraries-" + Guid.NewGuid().ToString("N"));

    public ControlLibraryTests()
    {
        TestPlatform.Install();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>The fixture's build output, copied to a folder of the test (the "bin" of a project).</summary>
    private string FixtureBuild()
    {
        var config = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
        var output = Path.Combine(RepoRoot, "tests", "Fixtures", "ControlLibrary", "bin", config, "net10.0");
        Assert.True(File.Exists(Path.Combine(output, "ControlLibrary.dll")), "The fixture is not built: " + output);
        var bin = Path.Combine(_dir, "bin");
        Directory.CreateDirectory(bin);
        foreach (var f in Directory.GetFiles(output)) File.Copy(f, Path.Combine(bin, Path.GetFileName(f)));
        return Path.Combine(bin, "ControlLibrary.dll");
    }

    // --- the scan: metadata only ------------------------------------------------------------------

    [Fact]
    public void TheScanFindsWhatVisualStudioWouldPutInTheToolbox()
    {
        var report = Assert.Single(ControlLibraryScanner.Scan(new[] { FixtureBuild() }));
        Assert.Equal("ok", report.Verdict);
        Assert.True(report.NetForms);
        Assert.Equal(".NETCoreApp,Version=v10.0", report.TargetFramework);
        Assert.Equal(
            new[] { "ControlLibrary.AddressBox", "ControlLibrary.Extras.Badge", "ControlLibrary.FaultyConstructor", "ControlLibrary.FaultyPainter", "ControlLibrary.Gauge", "ControlLibrary.Ticker" },
            report.Items.Select(i => i.Type).OrderBy(t => t, StringComparer.Ordinal));
        Assert.True(report.Items.Single(i => i.Name == "Ticker").Tray);
        Assert.False(report.Items.Single(i => i.Name == "Gauge").Tray);
        Assert.Equal("ControlLibrary.Extras", report.Items.Single(i => i.Name == "Badge").Namespace);

        // [ToolboxBitmap(typeof(Gauge), "Gauge.bmp")]: a 16×16 PNG, transparent where the bitmap's corner colour is.
        var icon = report.Items.Single(i => i.Name == "Gauge").Icon;
        Assert.NotNull(icon);
        using var image = new Bitmap(new MemoryStream(Convert.FromBase64String(icon!)));
        Assert.Equal(new Size(16, 16), image.Size);
        Assert.Equal(0, image.GetPixel(0, 15).A);
        Assert.Null(report.Items.Single(i => i.Name == "Badge").Icon);
    }

    [Fact]
    public void TheScanRunsNoCodeOfTheLibrary()
    {
        var path = FixtureBuild();
        ControlLibraryScanner.Scan(new[] { path });
        Assert.DoesNotContain(System.Runtime.Loader.AssemblyLoadContext.All.SelectMany(c => c.Assemblies), a => a.Location == path);
        // And lets go of the file: the next build can overwrite it.
        File.Copy(path, path + ".copy");
        File.Delete(path);
        File.Move(path + ".copy", path);
    }

    [Fact]
    public void ALibraryBuiltForTheRealWinFormsIsAWarning()
    {
        var winForms = Compile("System.Windows.Forms", "namespace System.Windows.Forms { public class Control : System.ComponentModel.Component { } }");
        var library = Compile("OldGauges", "namespace OldGauges { public class RoundGauge : System.Windows.Forms.Control { } }", winForms);
        var report = Assert.Single(ControlLibraryScanner.Scan(new[] { library }));
        Assert.Equal("warning", report.Verdict);
        Assert.False(report.NetForms);
        Assert.Contains(report.Findings, f => f.Code == "winForms" && f.Message.Contains("decision 136"));
        // Its controls are still listed: the facade forwards System.Windows.Forms to NetForms.
        Assert.Equal("OldGauges.RoundGauge", Assert.Single(report.Items).Type);
    }

    [Fact]
    public void ALibraryForTheDotNetFrameworkIsRefused()
    {
        var library = Compile("Legacy", """
            [assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]
            namespace Legacy { public class Box : System.ComponentModel.Component { } }
            """);
        var report = Assert.Single(ControlLibraryScanner.Scan(new[] { library }));
        Assert.Equal("error", report.Verdict);
        Assert.Contains(report.Findings, f => f.Code == "noFramework");
        Assert.Empty(report.Items);

        var notAssembly = Path.Combine(_dir, "native.dll");
        File.WriteAllBytes(notAssembly, new byte[] { 1, 2, 3 });
        Assert.Contains(ControlLibraryScanner.Scan(new[] { notAssembly })[0].Findings, f => f.Code == "notManaged");
    }

    [Theory]
    [InlineData("net10.0", "net48", "net10.0", "netstandard2.0")]
    [InlineData("net8.0", "net48", "net8.0", "netstandard2.1", "netcoreapp3.1")]
    [InlineData("netstandard2.1", "net48", "netstandard2.0", "netstandard2.1")]
    [InlineData("net8.0", "net8.0-windows7.0", "net8.0")]
    [InlineData("net8.0-windows7.0", "net48", "net8.0-windows7.0")]
    [InlineData(null, "net48", "net472", "net11.0", "net8.0-android")]
    public void APackageGivesTheLibFolderANet10ProjectTakes(string? expected, params string[] folders) =>
        Assert.Equal(expected, ControlLibraryScanner.BestFramework(folders));

    [Fact]
    public void APackageIsCheckedBeforeItIsAdded()
    {
        var library = FixtureBuild();
        var nupkg = Path.Combine(_dir, "Gauges.1.0.0.nupkg");
        using (var zip = ZipFile.Open(nupkg, ZipArchiveMode.Create))
        {
            zip.CreateEntryFromFile(library, "lib/net8.0/ControlLibrary.dll");
            zip.CreateEntryFromFile(library, "lib/net48/ControlLibrary.dll");
            zip.CreateEntry("runtimes/win-x64/native/gauge.dll").Open().Dispose();
            using (var writer = new StreamWriter(zip.CreateEntry("Gauges.nuspec").Open()))
                writer.Write("<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><id>Gauges</id><version>1.0.0</version></metadata></package>");
        }
        var report = ControlLibraryScanner.ScanPackage(nupkg);
        Assert.Equal("net8.0", report.Framework);
        Assert.Equal(("Gauges", "1.0.0"), (report.Id, report.Version));
        Assert.Equal(new[] { "net48", "net8.0" }, report.Frameworks);
        Assert.Equal("warning", report.Verdict);
        Assert.Contains(report.Findings, f => f.Code == "windowsOnly");
        var assembly = Assert.Single(report.Assemblies);
        Assert.Equal("lib/net8.0/ControlLibrary.dll", assembly.Path);
        Assert.Contains(assembly.Items, i => i.Name == "Gauge");

        var empty = Path.Combine(_dir, "Empty.1.0.0.nupkg");
        using (var zip = ZipFile.Open(empty, ZipArchiveMode.Create))
            zip.CreateEntryFromFile(library, "lib/net48/ControlLibrary.dll");
        var refused = ControlLibraryScanner.ScanPackage(empty);
        Assert.Equal("error", refused.Verdict);
        Assert.Null(refused.Framework);
    }

    // --- loading: the form uses the library's controls ---------------------------------------------

    private const string LibraryForm = """
        namespace Demo
        {
            partial class TestForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    components = new System.ComponentModel.Container();
                    gauge1 = new ControlLibrary.Gauge();
                    faultyPainter1 = new ControlLibrary.FaultyPainter();
                    faultyConstructor1 = new ControlLibrary.FaultyConstructor();
                    ticker1 = new ControlLibrary.Ticker(components);
                    SuspendLayout();
                    //
                    // gauge1
                    //
                    gauge1.Location = new Point(10, 10);
                    gauge1.Name = "gauge1";
                    gauge1.Size = new Size(120, 24);
                    gauge1.TabIndex = 0;
                    gauge1.Value = 25;
                    gauge1.ValueChanged += gauge1_ValueChanged;
                    //
                    // faultyPainter1
                    //
                    faultyPainter1.Location = new Point(10, 50);
                    faultyPainter1.Name = "faultyPainter1";
                    faultyPainter1.Size = new Size(100, 40);
                    faultyPainter1.TabIndex = 1;
                    //
                    // faultyConstructor1
                    //
                    faultyConstructor1.Caption = "licensed";
                    faultyConstructor1.Location = new Point(10, 100);
                    faultyConstructor1.Name = "faultyConstructor1";
                    faultyConstructor1.Size = new Size(160, 40);
                    faultyConstructor1.TabIndex = 2;
                    //
                    // ticker1
                    //
                    ticker1.Interval = 250;
                    //
                    // TestForm
                    //
                    AutoScaleDimensions = new SizeF(7F, 15F);
                    AutoScaleMode = AutoScaleMode.Font;
                    ClientSize = new Size(300, 200);
                    Controls.Add(faultyConstructor1);
                    Controls.Add(faultyPainter1);
                    Controls.Add(gauge1);
                    Name = "TestForm";
                    Text = "TestForm";
                    ResumeLayout(false);
                }

                private ControlLibrary.Gauge gauge1;
                private ControlLibrary.FaultyPainter faultyPainter1;
                private ControlLibrary.FaultyConstructor faultyConstructor1;
                private ControlLibrary.Ticker ticker1;
            }
        }
        """;

    private const string LibraryFormCode = """
        namespace Demo
        {
            public partial class TestForm : Form
            {
                public TestForm() { InitializeComponent(); }

                private void gauge1_ValueChanged(object sender, EventArgs e) { }
            }
        }
        """;

    private string WriteForm(string designer = LibraryForm, string code = LibraryFormCode)
    {
        var path = Path.Combine(_dir, "TestForm.Designer.cs");
        File.WriteAllText(path, designer);
        File.WriteAllText(Path.Combine(_dir, "TestForm.cs"), code);
        return path;
    }

    [Fact]
    public void WithoutTheLibraryItsControlsArePlaceholdersThatKeepEveryStatement()
    {
        var path = WriteForm();
        using var surface = DesignSurface.Open(path);
        Assert.True(surface.Model.Find("gauge1")!.IsPlaceholder);
        surface.Apply(Array.Empty<DesignerOp>());
        // Nothing lost - the constructor's argument included.
        Assert.Contains("ticker1 = new ControlLibrary.Ticker(components);", surface.Source);
        Assert.Contains("gauge1.Value = 25;", surface.Source);
    }

    [Fact]
    public void TheLibrarysControlsAreLiveOnTheCanvas()
    {
        var path = WriteForm();
        using var libraries = DesignerLibraries.Load(new[] { FixtureBuild() });
        Assert.Empty(libraries.Errors);
        Assert.Equal("ControlLibrary", Assert.Single(libraries.Assemblies).GetName().Name);
        using var surface = DesignSurface.Open(path, libraries);

        var gauge = surface.Model.Find("gauge1")!;
        Assert.False(gauge.IsPlaceholder);
        Assert.Equal("ControlLibrary.Gauge", gauge.Instance.GetType().FullName);
        Assert.True(((Control)gauge.Instance).Site!.DesignMode);
        Assert.Equal(25, gauge.Instance.GetType().GetProperty("Value")!.GetValue(gauge.Instance));
        Assert.False(surface.Model.Find("ticker1")!.IsPlaceholder);

        // The property grid and the Events tab come from the library's attributes.
        var value = surface.GetProperties("gauge1").Single(r => r.Name == "Value");
        Assert.Equal("Behavior", value.Category);
        Assert.Equal("How full the gauge is, 0 to 100.", value.Description);
        Assert.True(value.Modified);
        Assert.Equal("gauge1_ValueChanged", surface.GetEvents("gauge1").Single(e => e.Name == "ValueChanged").Handler);

        surface.Apply(new[]
        {
            new DesignerOp { Op = "setProp", Id = "gauge1", Prop = "Value", Value = "60" },
            new DesignerOp { Op = "setProp", Id = "ticker1", Prop = "Interval", Value = "1000" },
        });
        Assert.Contains("gauge1.Value = 60;", surface.Source);
        Assert.DoesNotContain("ticker1.Interval", surface.Source); // back to its [DefaultValue]
        Assert.Contains("ticker1 = new ControlLibrary.Ticker(components);", surface.Source);

        // A value its setter refuses is an edit refused, not a broken form.
        var refused = Assert.Throws<DesignerEditException>(() => surface.Apply(new[] { new DesignerOp { Op = "setProp", Id = "gauge1", Prop = "Value", Value = "300" } }));
        Assert.Contains("Value", refused.Message);
        Assert.Contains("gauge1.Value = 60;", surface.Source);
    }

    [Fact]
    public void ALibraryControlThatFailsIsARedCrossNotAFailure()
    {
        var path = WriteForm();
        using var libraries = DesignerLibraries.Load(new[] { FixtureBuild() });
        using var surface = DesignSurface.Open(path, libraries);

        // The constructor threw: a placeholder that says why, and keeps what the file says.
        var broken = surface.Model.Find("faultyConstructor1")!;
        Assert.True(broken.IsPlaceholder);
        var placeholder = Assert.IsType<DesignerPlaceholder>(broken.Instance);
        Assert.Equal("InvalidOperationException: No license for design time.", placeholder.Error);

        // OnPaint throws: the control is drawn as a red cross, and the rest of the form is drawn.
        var view = surface.Render();
        using var png = new Bitmap(new MemoryStream(Convert.FromBase64String(view.Png)));
        var painter = view.Items.Single(i => i.Id == "faultyPainter1");
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), png.GetPixel(painter.X + 1, painter.Y + 1));
        var cross = view.Items.Single(i => i.Id == "faultyConstructor1");
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), png.GetPixel(cross.X + 1, cross.Y + 1));
        var gauge = view.Items.Single(i => i.Id == "gauge1");
        Assert.Equal(Color.Green.ToArgb(), png.GetPixel(gauge.X + 5, gauge.Y + 10).ToArgb());

        surface.Apply(new[] { new DesignerOp { Op = "setBounds", Id = "faultyConstructor1", X = 20 } });
        Assert.Contains("faultyConstructor1 = new ControlLibrary.FaultyConstructor();", surface.Source);
        Assert.Contains("faultyConstructor1.Caption = \"licensed\";", surface.Source);
        Assert.Contains("faultyConstructor1.Location = new Point(20, 100);", surface.Source);
    }

    [Fact]
    public void TheToolboxAddsTheLibrarysControls()
    {
        var path = WriteForm();
        using var libraries = DesignerLibraries.Load(new[] { FixtureBuild() });
        using var surface = DesignSurface.Open(path, libraries);
        surface.Apply(new[]
        {
            new DesignerOp { Op = "add", Type = "ControlLibrary.Extras.Badge", Parent = "", X = 200, Y = 10 },
            new DesignerOp { Op = "add", Type = "ControlLibrary.Ticker" },
        });
        Assert.Contains("badge1 = new ControlLibrary.Extras.Badge();", surface.Source);
        Assert.Contains("ticker2 = new ControlLibrary.Ticker(components);", surface.Source);
        Assert.Contains("private ControlLibrary.Extras.Badge badge1;", surface.Source);
        Assert.Contains(surface.Render().Tray, t => t.Id == "ticker2");

        // Copy and paste take the library's controls along.
        var text = surface.Copy(new[] { "gauge1" });
        surface.Apply(new[] { new DesignerOp { Op = "paste", Value = text, Parent = "" } });
        Assert.Contains("gauge2 = new ControlLibrary.Gauge();", surface.Source);
        Assert.Contains("gauge2.Value = 25;", surface.Source);
    }

    [Fact]
    public void AFormCanDeriveFromAFormOfTheProject()
    {
        var path = WriteForm(LibraryForm.Replace("gauge1.ValueChanged += gauge1_ValueChanged;", ""),
            LibraryFormCode.Replace(": Form", ": ControlLibrary.SettingsForm").Replace("private void gauge1_ValueChanged(object sender, EventArgs e) { }", ""));
        Assert.Throws<DesignerCodeException>(() => DesignSurface.Open(path).Dispose());
        using var libraries = DesignerLibraries.Load(new[] { FixtureBuild() });
        using var surface = DesignSurface.Open(path, libraries);
        Assert.Equal("ControlLibrary.SettingsForm", surface.Model.RootType.FullName);
    }

    [Fact]
    public void TheLibraryIsLoadedFromACopyAndTheHostKeepsItsOwnNetForms()
    {
        var dll = FixtureBuild();
        using var libraries = DesignerLibraries.Load(new[] { dll });
        var assembly = Assert.Single(libraries.Assemblies);
        Assert.StartsWith(DesignerLibraries.ShadowBase, assembly.Location);
        Assert.Equal(libraries.ShadowOf(dll), assembly.Location);
        // The project's copy of NetForms is not loaded: its Control is the designer's.
        var gauge = libraries.FindType("ControlLibrary.Gauge")!;
        Assert.True(typeof(Control).IsAssignableFrom(gauge));
        Assert.DoesNotContain(libraries.Assemblies, a => a.GetName().Name == "NetForms");
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(assembly.Location)!, "NetForms.dll")));
        // The build output is free: a rebuild overwrites it.
        File.WriteAllBytes(dll, File.ReadAllBytes(dll));
        File.Delete(dll);
    }

    [Fact]
    public void AnUnloadedLibraryIsCollected()
    {
        var weak = LoadOpenAndUnload(WriteForm(), FixtureBuild());
        for (int i = 0; i < 20 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.False(weak.IsAlive, "The collectible context is still referenced after Dispose.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference LoadOpenAndUnload(string path, string dll)
    {
        var libraries = DesignerLibraries.Load(new[] { dll });
        using (var surface = DesignSurface.Open(path, libraries))
        {
            surface.Apply(new[] { new DesignerOp { Op = "setProp", Id = "gauge1", Prop = "Value", Value = "30" } });
            surface.GetProperties("gauge1");
            surface.GetEvents("gauge1");
            surface.Render();
        }
        var weak = libraries.ContextReference();
        libraries.Dispose();
        return weak;
    }

    // --- the protocol ---------------------------------------------------------------------------------

    [Fact]
    public void TheProtocolLoadsTheProjectAndListsItsToolboxGroups()
    {
        var path = WriteForm();
        var dll = FixtureBuild();
        using var protocol = new DesignerProtocol();
        JsonNode Call(string method, object? p = null)
        {
            var line = protocol.Handle(System.Text.Json.JsonSerializer.Serialize(new { id = 1, method, @params = p }));
            var node = JsonNode.Parse(line)!;
            Assert.True(node["error"] == null, line);
            return node["result"]!;
        }

        var library = new { id = "project", name = "ControlLibrary Components", assemblies = new[] { dll }, excludeNamespaces = new[] { "ControlLibrary.Extras" } };
        var extras = new { id = "extras", name = "Extras", assemblies = new[] { dll }, namespaces = new[] { "ControlLibrary.Extras" } };

        // Not loaded yet (an untrusted workspace): listed from the metadata, but not usable.
        var groups = Call("toolbox", new { libraries = new object[] { library, extras } }).AsArray();
        var group = groups.Single(g => (string?)g!["name"] == "ControlLibrary Components")!;
        Assert.True((bool)group["library"]!);
        Assert.Contains(group["items"]!.AsArray(), i => (string?)i!["name"] == "Gauge" && i["unavailable"] != null);
        Assert.DoesNotContain(group["items"]!.AsArray(), i => (string?)i!["name"] == "Badge");
        Assert.Equal("Badge", (string?)Assert.Single(groups.Single(g => (string?)g!["name"] == "Extras")!["items"]!.AsArray())!["name"]);

        Call("open", new { path });
        Assert.DoesNotContain(Call("properties", new { id = "gauge1" }).AsArray(), r => (string?)r!["name"] == "Value");

        // Loaded: the open form is read again with the library's types.
        var loaded = Call("libraries", new { assemblies = new[] { dll } });
        Assert.Equal("ControlLibrary", (string?)loaded["assemblies"]![0]);
        Assert.NotNull(loaded["view"]);
        Assert.Contains(Call("properties", new { id = "gauge1" }).AsArray(), r => (string?)r!["name"] == "Value" && (string?)r["value"] == "25");
        group = Call("toolbox", new { libraries = new object[] { library } }).AsArray().Single(g => (string?)g!["name"] == "ControlLibrary Components")!;
        var gauge = group["items"]!.AsArray().Single(i => (string?)i!["name"] == "Gauge")!;
        Assert.Null(gauge["unavailable"]);
        Assert.NotNull(gauge["icon"]);

        var reports = Call("scan", new { paths = new[] { dll } }).AsArray();
        Assert.Equal("ok", (string?)reports[0]!["verdict"]);

        // Unloading: the form falls back to placeholders.
        Call("libraries", new { assemblies = Array.Empty<string>() });
        Assert.DoesNotContain(Call("properties", new { id = "gauge1" }).AsArray(), r => (string?)r!["name"] == "Value");
    }

    // --- a control whose painting throws, outside the designer ----------------------------------------

    private sealed class Thrower : Control
    {
        public int Paints;

        protected override void OnPaint(PaintEventArgs e)
        {
            Paints++;
            throw new InvalidOperationException("paint");
        }
    }

    [Fact]
    public void APaintingExceptionReachesTheApplicationOnceAndTheControlIsARedCrossFromThenOn()
    {
        using var thrower = new Thrower { Size = new Size(40, 40) };
        using var bitmap = new Bitmap(40, 40);
        Assert.Throws<InvalidOperationException>(() => thrower.DrawToBitmap(bitmap, new Rectangle(0, 0, 40, 40)));
        thrower.DrawToBitmap(bitmap, new Rectangle(0, 0, 40, 40));
        Assert.Equal(1, thrower.Paints);
        Assert.Equal(Color.FromArgb(255, 255, 0, 0), bitmap.GetPixel(20, 20));
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), bitmap.GetPixel(20, 8));
    }

    // --- helpers ------------------------------------------------------------------------------------

    /// <summary>A small assembly compiled here, for libraries the fixture cannot be (built for WinForms, for .NET Framework).</summary>
    private string Compile(string name, string source, params string[] references)
    {
        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "System.ComponentModel.Primitives.dll" or "netstandard.dll");
        var compilation = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(source) },
            tpa.Concat(references).Select(p => MetadataReference.CreateFromFile(p)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = Path.Combine(_dir, name + ".dll");
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        return path;
    }
}
