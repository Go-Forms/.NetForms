using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetForms.ApiDiff;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Decision 146: <c>NetForms.ApiDiff --usage</c> ranks the API NetForms lacks by how many real projects use it.
/// What NetForms lacks is listed by reflection over the reference pack; what code uses is bound by Roslyn. The
/// two meet on a member key, so the keys must be equal for the same member, and the scan must bind what a
/// compiler binds (the declaring type, the overridden member, implicit usings of an SDK project).
/// </summary>
public class ApiUsageTests
{
    private static readonly string Pack = ApiUsage.DefaultReferencePack;

    private static string[] Assemblies()
    {
        var pack = Directory.GetFiles(Pack, "*.dll");
        var names = pack.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return pack.Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll").Where(f => !names.Contains(Path.GetFileName(f))))
            .Where(f =>
            {
                try { using var pe = new PEReader(File.OpenRead(f)); return pe.HasMetadata; }
                catch (BadImageFormatException) { return false; }
            }).ToArray();
    }

    [Theory]
    [InlineData("System.Windows.Forms.Control")]
    [InlineData("System.Windows.Forms.ListView")]
    [InlineData("System.Windows.Forms.DataGridView")]
    [InlineData("System.Windows.Forms.ImageList+ImageCollection")]
    [InlineData("System.Windows.Forms.TaskDialogFootnote")]
    [InlineData("System.Windows.Forms.Cursor")]
    [InlineData("System.Windows.Forms.BindingSource")]
    [InlineData("System.Drawing.Graphics")]
    [InlineData("System.Drawing.Bitmap")]
    [InlineData("System.Drawing.Drawing2D.GraphicsPath")]
    public void ReflectionAndRoslynGiveAMemberTheSameKey(string typeName)
    {
        var files = Assemblies();
        using var context = new MetadataLoadContext(new PathAssemblyResolver(files));
        var assembly = typeName.StartsWith("System.Drawing", StringComparison.Ordinal) ? "System.Drawing.Common.dll" : "System.Windows.Forms.dll";
        var real = context.LoadFromAssemblyPath(Path.Combine(Pack, assembly)).GetType(typeName, throwOnError: true)!;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var byReflection = real.GetMembers(flags).Where(m => m switch
        {
            MethodBase mb => (mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly) && (!mb.IsSpecialName || mb.Name.StartsWith("op_", StringComparison.Ordinal) || mb is ConstructorInfo),
            FieldInfo f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
            PropertyInfo p => (p.GetMethod ?? p.SetMethod) is { } a && (a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
            EventInfo e => e.AddMethod is { } a && (a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly),
            _ => false,
        }).Select(ApiUsage.Key).ToHashSet();

        var compilation = CSharpCompilation.Create("keys", references: files.Select(f => MetadataReference.CreateFromFile(f)));
        var symbol = compilation.GetTypeByMetadataName(typeName)!;
        var byRoslyn = symbol.GetMembers().Where(m =>
                m.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal
                && m is not INamedTypeSymbol
                && m is not IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove })
            .Select(ApiUsage.Key).ToHashSet();

        Assert.NotEmpty(byReflection);
        Assert.Equal(byReflection.OrderBy(k => k, StringComparer.Ordinal), byRoslyn.OrderBy(k => k, StringComparer.Ordinal));
    }

    /// <summary>
    /// The usage scan found <c>TextureBrush</c> in <c>System.Drawing.Drawing2D</c> and <c>TableLayoutControlCollection</c>
    /// nested in the panel: types that exist, only elsewhere - code naming them does not compile, and the coverage
    /// tables count them missing. Every public type of NetForms named like a WinForms type is where WinForms has it.
    /// </summary>
    [Fact]
    public void EveryTypeNamedLikeAWinFormsTypeIsWhereWinFormsDeclaresIt()
    {
        using var context = new MetadataLoadContext(new PathAssemblyResolver(Assemblies()));
        static string Name(Type t) => (t.FullName ?? t.Name).Replace('+', '.');
        var real = new[] { "System.Windows.Forms.dll", "System.Windows.Forms.Primitives.dll", "System.Drawing.Common.dll" }
            .SelectMany(f => context.LoadFromAssemblyPath(Path.Combine(Pack, f)).GetExportedTypes())
            .Where(t => t.Namespace != null && (t.Namespace.StartsWith("System.Windows.Forms", StringComparison.Ordinal) || t.Namespace.StartsWith("System.Drawing", StringComparison.Ordinal)))
            .ToList();
        var realNames = real.Select(Name).ToHashSet();
        var bySimpleName = real.ToLookup(t => t.Name);
        var misplaced = new[] { typeof(Control).Assembly, typeof(Graphics).Assembly }
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => !realNames.Contains(Name(t)) && bySimpleName.Contains(t.Name))
            .Select(t => $"{Name(t)} - WinForms: {string.Join(", ", bySimpleName[t.Name].Select(Name))}")
            .OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.True(misplaced.Count == 0, "Not where WinForms declares them:\n" + string.Join("\n", misplaced));
    }

    [Fact]
    public void TheScanRanksWhatCodeUsesByRepositoriesThenUses()
    {
        var root = Path.Combine(Path.GetTempPath(), "netforms-usage-" + Guid.NewGuid().ToString("N"));
        try
        {
            // An SDK-style project: the WinForms namespaces come from ImplicitUsings, not from the file.
            Write(root, "RepoA/App/App.csproj", """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0-windows</TargetFramework>
                    <UseWindowsForms>true</UseWindowsForms>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>
                </Project>
                """);
            Write(root, "RepoA/App/Form1.cs", """
                namespace App;

                public class Form1 : Form
                {
                    private readonly ListView list = new ListView();
                    private readonly DomainUpDown upDown = new DomainUpDown();

                    public Form1()
                    {
                        list.VirtualMode = true;
                        list.VirtualListSize = 10;
                        if (list.VirtualMode) Text = "virtual";
                        Controls.Add(upDown);
                        if (list.Controls.Count == 0) upDown.Visible = true;
                    }

                    protected override void OnPaint(PaintEventArgs e) => base.OnPaint(e);
                }
                """);
            // An old-format project: explicit usings; a protected member reached through inheritance.
            Write(root, "RepoB/Old/Old.csproj", """<Project ToolsVersion="15.0"><ItemGroup><Compile Include="Main.cs" /></ItemGroup></Project>""");
            Write(root, "RepoB/Old/Main.cs", """
                using System.Windows.Forms;

                namespace Old
                {
                    class Glyph : LinkLabel
                    {
                        public Glyph()
                        {
                            OverrideCursor = Cursors.Hand;
                            var d = new DomainUpDown();
                            d.Items.Add("x");
                        }
                    }

                    class Other : ListView
                    {
                        public Other() { VirtualMode = false; }
                    }
                }
                """);

            var asked = new List<(string Type, string? Member)>();
            var missing = new HashSet<(string, string?)>
            {
                ("System.Windows.Forms.DomainUpDown", null),
                ("System.Windows.Forms.ListView", "P:VirtualMode"),
                ("System.Windows.Forms.ListView", "P:VirtualListSize"),
                ("System.Windows.Forms.LinkLabel", "P:OverrideCursor"),
                // Control.ControlCollection's base: its Count is reached through the collection, which has it.
                ("System.Windows.Forms.Layout.ArrangedElementCollection", null),
            };
            var scan = ApiUsage.Scan(root, Pack, (type, member) =>
            {
                asked.Add((type, member));
                return missing.Contains((type, member));
            });

            Assert.Equal(new[] { "RepoA", "RepoB" }, scan.Repositories);
            Assert.Equal(2, scan.Projects);
            Assert.Equal(
                new[]
                {
                    // new DomainUpDown() and the field's type in A; new DomainUpDown() and d.Items in B.
                    "DomainUpDown 2 4 RepoA,RepoB",
                    "ListView.VirtualMode 2 3 RepoA,RepoB",
                    "LinkLabel.OverrideCursor 1 1 RepoB",
                    "ListView.VirtualListSize 1 1 RepoA",
                },
                scan.Rows.Select(r => $"{r.Display} {r.Repositories.Count} {r.Uses} {string.Join(",", r.Repositories)}"));
            // The override is checked against the member it overrides; a member NetForms has is not a row.
            Assert.Contains(asked, a => a.Member == "M:System.Void OnPaint(System.Windows.Forms.PaintEventArgs)");
            Assert.Contains(asked, a => a.Member == "P:Text");
            Assert.Contains(asked, a => a == ("System.Windows.Forms.Control.ControlCollection", "P:Count"));
            Assert.True(scan.References > scan.Rows.Sum(r => r.Uses));

            var markdown = ApiUsage.Markdown(scan, (type, _) => type.EndsWith("DomainUpDown", StringComparison.Ordinal) ? "missing" : "partial");
            Assert.Contains("| [System.Windows.Forms.DomainUpDown](https://learn.microsoft.com/dotnet/api/system.windows.forms.domainupdown) | missing | 2 | 4 | the type |", markdown);
            Assert.Contains("| [System.Windows.Forms.ListView](https://learn.microsoft.com/dotnet/api/system.windows.forms.listview) | partial | 2 | 4 | `VirtualMode`, `VirtualListSize` |", markdown);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void Write(string root, string path, string text)
    {
        var file = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
    }
}
