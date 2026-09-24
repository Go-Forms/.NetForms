using System.ComponentModel;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetForms.Design.Serialization;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Ф5.2: <see cref="DesignerCodeWriter"/> writes the model back into <c>*.Designer.cs</c>. The gate, for
/// every form of <c>samples/</c>: read → write → the written file compiles, and its compiled
/// <c>InitializeComponent()</c> builds the same tree as the original's; the reader reads the written
/// file into that same tree; and writing again changes nothing.
/// </summary>
public class DesignerCodeWriterTests
{
    // --- the gate --------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SampleForms), MemberType = typeof(DesignerCodeReaderTests))]
    public void WrittenFileCompilesToTheSameTree(string designerFile, Type formType)
    {
        TestPlatform.Install();
        var path = Sample(designerFile);
        var model = new DesignerCodeReader().ReadFile(path);
        var written = new DesignerCodeWriter().Write(model, File.ReadAllText(path));
        var introduced = model.Components.Where(c => c.OriginalName == null).Select(c => c.Name!).ToHashSet();
        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out", "written", Path.GetDirectoryName(designerFile)!);
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, Path.GetFileName(designerFile)), written);

        // 1. The written file compiles, and its InitializeComponent builds what the original's builds.
        var compiledType = Compile(Path.GetDirectoryName(path)!, path, written).GetType(formType.FullName!)!;
        var original = Settle(RealInitializeComponent(formType));
        var rewritten = Settle(RealInitializeComponent(compiledType));
        var originalNames = ComponentFields(formType, original);
        var rewrittenNames = ComponentFields(compiledType, rewritten);
        Assert.Equal(originalNames.Select(n => n.Name).Concat(introduced).OrderBy(n => n), rewrittenNames.Select(n => n.Name).OrderBy(n => n));

        // Inline objects of the original got field names; the trees are compared as if they had not.
        foreach (var (name, value) in rewrittenNames.Where(n => introduced.Contains(n.Name)))
            if (value is Control control) control.Name = "";
        var kept = rewrittenNames.Where(n => !introduced.Contains(n.Name)).ToList();
        AssertSameLines(Snapshot(original, model.RootType, originalNames), Snapshot(rewritten, model.RootType, kept),
            $"The written {designerFile} builds a different tree than the original:");

        // 2. The reader reads the written file into the tree its compiled code builds.
        var reread = new DesignerCodeReader().Read(written, Companions(path), path);
        var rewrittenAgain = Settle(RealInitializeComponent(compiledType));
        Settle((Control)reread.Root.Instance);
        AssertSameLines(Snapshot(rewrittenAgain, model.RootType, ComponentFields(compiledType, rewrittenAgain)),
            Snapshot(reread.Root.Instance, model.RootType, reread.Components.Where(c => c.Instance is IComponent).Select(c => (c.Name!, c.Instance)).ToList()),
            $"The reader reads the written {designerFile} differently than it compiles:");

        // 3. Writing is a fixed point: no diff on the second save (of a fresh read - the one above was laid out).
        var again = new DesignerCodeReader().Read(written, Companions(path), path);
        Assert.Equal(written, new DesignerCodeWriter().Write(again, written));
    }

    /// <summary>
    /// Runs every pending layout, parents first. The written file suspends and resumes layout where
    /// the hand-written samples did not (as VS does), so laziness - a tool strip's item layout that has
    /// not run yet - would otherwise show up as a difference no shown form ever has.
    /// </summary>
    private static Form Settle(Control control)
    {
        control.PerformLayout();
        foreach (Control child in control.Controls) Settle(child);
        if (control is ToolStrip strip)
            foreach (var item in strip.Items.OfType<ToolStripDropDownItem>())
                if (item.HasDropDownItems) Settle(item.DropDown);
        return control as Form ?? null!;
    }

    private static List<(string Name, object Value)> ComponentFields(Type formType, object instance) =>
        formType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(f => (f.Name, Value: f.GetValue(instance)))
            .Where(f => f.Value is IComponent && f.Value is not IContainer)
            .Select(f => (f.Name, f.Value!))
            .ToList();

    private static IEnumerable<string> Companions(string designerPath)
    {
        var companion = DesignerCodeReader.CompanionPath(designerPath);
        return companion != null && File.Exists(companion) ? new[] { File.ReadAllText(companion) } : Array.Empty<string>();
    }

    // --- compiling what was written --------------------------------------------------------------

    /// <summary>The implicit usings of a NetForms project (ImplicitUsings + System.Drawing + System.Windows.Forms).</summary>
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Drawing;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using System.Windows.Forms;
        """;

    private static int s_compilations;

    /// <summary>
    /// Compiles the sample project's sources with <paramref name="designerPath"/> replaced by
    /// <paramref name="designerText"/> (plus <paramref name="extra"/>), against the NetForms assemblies,
    /// and loads the result.
    /// </summary>
    internal static Assembly Compile(string projectDir, string? designerPath, string designerText, params string[] extra)
    {
        var sources = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(GlobalUsings) };
        if (designerPath != null)
        {
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(projectDir, file);
                if (relative.StartsWith("bin" + Path.DirectorySeparatorChar) || relative.StartsWith("obj" + Path.DirectorySeparatorChar)) continue;
                if (Path.GetFullPath(file) == Path.GetFullPath(designerPath)) continue;
                sources.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file));
            }
        }
        sources.Add(CSharpSyntaxTree.ParseText(designerText, path: designerPath ?? "Designer.cs"));
        sources.AddRange(extra.Select(e => CSharpSyntaxTree.ParseText(e)));

        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Concat(new[] { typeof(Control).Assembly.Location, typeof(Graphics).Assembly.Location })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .Select(p => MetadataReference.CreateFromFile(p));

        var compilation = CSharpCompilation.Create(
            "Written" + Interlocked.Increment(ref s_compilations) + "_" + Guid.NewGuid().ToString("N"),
            sources, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, manifestResources: designerPath == null ? null : Resources(designerPath, sources.Last()));
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(result.Success, "The written code does not compile:\n" + string.Join("\n", errors.Take(20)) + "\n\n" + designerText);
        return Assembly.Load(stream.ToArray());
    }

    /// <summary>
    /// The form's .resx as MSBuild embeds it for a NetForms project (GenerateResourceUsePreserializedResources):
    /// strings as strings, base64 byte arrays as type-converter resources, BinaryFormatter records as they are,
    /// under "Namespace.Class.resources".
    /// </summary>
    private static IEnumerable<ResourceDescription>? Resources(string designerPath, SyntaxTree designer)
    {
        var resx = ResxResources.ResxPathOf(designerPath);
        if (resx == null || !File.Exists(resx)) return null;
        var cls = designer.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().First();
        var ns = cls.Ancestors().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        var name = (ns == null ? "" : ns + ".") + cls.Identifier.ValueText + ".resources";

        var output = new MemoryStream();
        using (var writer = new System.Resources.Extensions.PreserializedResourceWriter(output))
        {
            foreach (var data in System.Xml.Linq.XDocument.Load(resx).Root!.Elements("data"))
            {
                var key = (string)data.Attribute("name")!;
                var type = (string?)data.Attribute("type");
                var value = data.Element("value")?.Value ?? "";
                var mime = (string?)data.Attribute("mimetype");
                // A BinaryFormatter record (ImageList.ImageStream): MSBuild copies its bytes without a type name.
#pragma warning disable SYSLIB0011
                if (mime == "application/x-microsoft.net.object.binary.base64") writer.AddBinaryFormattedResource(key, Convert.FromBase64String(value));
#pragma warning restore SYSLIB0011
                else if (type == null) writer.AddResource(key, value);
                else writer.AddTypeConverterResource(key, Convert.FromBase64String(value), type);
            }
            writer.Generate();
        }
        var bytes = output.ToArray();
        return new[] { new ResourceDescription(name, () => new MemoryStream(bytes), isPublic: true) };
    }

    // --- the format ------------------------------------------------------------------------------

    [Fact]
    public void AVisualStudioFileIsWrittenBackAsItWas()
    {
        // HelloForms is exactly what the VS designer writes. The only line that changes is the one
        // our text metrics decide: the size of the auto-sized label.
        TestPlatform.Install();
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var source = File.ReadAllText(path);
        var model = new DesignerCodeReader().ReadFile(path);
        var label = (Label)model.Find("label1")!.Instance;
        var expected = source.Replace("label1.Size = new Size(38, 15);", $"label1.Size = new Size({label.Width}, {label.Height});");
        Assert.Equal(expected, new DesignerCodeWriter().Write(model, source));
    }

    [Fact]
    public void StatementsFollowTheCanonicalOrder()
    {
        TestPlatform.Install();
        var written = WriteSample("Gallery/GalleryForm.Designer.cs", out _);
        var lines = BodyLines(written);

        // Creations, then BeginInit/SuspendLayout, component blocks in creation order, the root block, EndInit/ResumeLayout.
        int Index(string statement)
        {
            int i = lines.IndexOf(statement);
            Assert.True(i >= 0, $"'{statement}' is not written:\n{string.Join("\n", lines)}");
            return i;
        }
        Assert.Equal(0, Index("components = new System.ComponentModel.Container();"));
        Assert.True(Index("toolTip1 = new ToolTip(components);") < Index("tabControl1.SuspendLayout();"));
        Assert.True(Index("((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();") < Index("splitContainer1.Panel1.SuspendLayout();"));
        Assert.True(Index("splitContainer1.Panel2.SuspendLayout();") < Index("splitContainer1.SuspendLayout();"));
        Assert.True(Index("SuspendLayout();") < Index("// tabControl1"));
        Assert.True(Index("// statusLabel") < Index("// GalleryForm"));
        Assert.True(Index("splitContainer1.Panel2.ResumeLayout(false);") < Index("((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();"));
        Assert.True(Index("((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();") < Index("splitContainer1.ResumeLayout(false);"));
        Assert.Equal(lines.Count - 1, Index("ResumeLayout(false);"));

        // Inside a block: alphabetical, the way PropertyDescriptorCollection.Sort orders (Checked before CheckState).
        Assert.True(Index("checkBox2.Checked = true;") < Index("checkBox2.CheckState = CheckState.Checked;"));
        Assert.True(Index("checkBox2.CheckState = CheckState.Checked;") < Index("checkBox2.Location = new Point(12, 45);"));
        // The nested panels are written inside their owner's block, under their own header.
        Assert.True(Index("// splitContainer1.Panel1") < Index("splitContainer1.Panel1.Controls.Add(textBox3);"));
        Assert.True(Index("splitContainer1.Name = \"splitContainer1\";") < Index("// splitContainer1.Panel1"));
        // The root block starts with AutoScale, as the VS serializer writes it.
        Assert.Equal(Index("// GalleryForm") + 2, Index("AutoScaleDimensions = new SizeF(7F, 15F);"));
    }

    [Fact]
    public void InlineObjectsBecomeFields()
    {
        TestPlatform.Install();
        var written = WriteSample("Gallery/GalleryForm.Designer.cs", out var model);
        Assert.DoesNotContain("new Button {", written);
        Assert.DoesNotContain("GetControlFromPosition", written);

        // The first inline button of the flow panel is the first free "button" name.
        var first = model.Components.First(c => c.Instance is Button { Text: "One" });
        Assert.Equal("button2", first.Name);
        Assert.Equal("button2", ((Button)first.Instance).Name);
        Assert.Contains("flowLayoutPanel1.Controls.Add(button2);", written);
        Assert.Contains("private Button button2;", written);

        // The cell of a table child goes into Controls.Add, its span is an extender of the table.
        var spanning = model.Components.First(c => c.Instance is TextBox { Text: "spans two columns" });
        Assert.Contains($"tableLayoutPanel1.Controls.Add({spanning.Name}, 1, 0);", written);
        Assert.Contains($"tableLayoutPanel1.SetColumnSpan({spanning.Name}, 2);", written);
        Assert.Contains("tableLayoutPanel1.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));", written);
    }

    [Fact]
    public void AClassicFileStaysClassic()
    {
        // GenLabs is .NET Framework designer code: this.-qualified, fully qualified, CodeDom casts.
        TestPlatform.Install();
        var written = WriteSample("GenLabs/MainForm.Designer.cs", out _);
        var lines = BodyLines(written);
        AssertLine(lines, "this.components = new System.ComponentModel.Container();");
        AssertLine(lines, "this.timer1 = new System.Windows.Forms.Timer(this.components);");
        AssertLine(lines, "((System.ComponentModel.ISupportInitialize)(this.pictureBox)).BeginInit();");
        AssertLine(lines, "this.pictureBox.BackColor = System.Drawing.SystemColors.ActiveCaptionText;");
        AssertLine(lines, "this.timer1.Tick += new System.EventHandler(this.Timer1_Tick);");
        AssertLine(lines, "this.label6.Font = new System.Drawing.Font(\"Microsoft Sans Serif\", 8.25F, System.Drawing.FontStyle.Underline, System.Drawing.GraphicsUnit.Point, ((byte)(204)));");
        AssertLine(lines, "this.Load += new System.EventHandler(this.Form1_Load);");
        Assert.Contains("// \n", written.Replace("\r", ""));
        Assert.Contains("private System.Windows.Forms.TextBox Size;", written);
    }

    [Fact]
    public void TheRestOfTheFileIsUntouched()
    {
        TestPlatform.Install();
        var path = Sample("Strips/MainForm.Designer.cs");
        var source = File.ReadAllText(path);
        var written = new DesignerCodeWriter().Write(new DesignerCodeReader().ReadFile(path), source);
        static string Outside(string text)
        {
            int start = text.IndexOf("private void InitializeComponent()", StringComparison.Ordinal);
            int end = text.IndexOf("#endregion", StringComparison.Ordinal);
            return text.Substring(0, start) + text.Substring(end);
        }
        Assert.Equal(Outside(source), Outside(written));
    }

    // --- editing ---------------------------------------------------------------------------------

    private const string HelloExtra = """
        namespace HelloForms
        {
            partial class MainForm
            {
                private void okButton_Click(object? sender, EventArgs e) { }
            }
        }
        """;

    [Fact]
    public void AnAddedButtonIsCreatedLaidOutAndWired()
    {
        TestPlatform.Install();
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var model = new DesignerCodeReader().ReadFile(path);
        var form = (Form)model.Root.Instance;

        var button = new Button { Location = new Point(12, 105), Size = new Size(75, 23), TabIndex = 2, Text = "OK", UseVisualStyleBackColor = true };
        form.Controls.Add(button);
        var component = model.AddComponent(button);
        Assert.Equal("button2", component.Name);
        model.Rename(component, "okButton");
        model.BindEvent(component, "Click", "okButton_Click");

        var written = new DesignerCodeWriter().Write(model, File.ReadAllText(path));
        var lines = BodyLines(written);
        AssertLine(lines, "okButton = new Button();");
        AssertLine(lines, "okButton.Name = \"okButton\";");
        AssertLine(lines, "okButton.Click += okButton_Click;");
        AssertLine(lines, "Controls.Add(okButton);");
        Assert.Contains("        private Label label1;\r\n        private Button okButton;\r\n", written.ReplaceLineEndings("\r\n"));

        var type = Compile(Path.GetDirectoryName(path)!, path, written, HelloExtra).GetType("HelloForms.MainForm")!;
        var compiled = RealInitializeComponent(type);
        var ok = compiled.Controls.OfType<Button>().Single(b => b.Name == "okButton");
        Assert.Equal("OK", ok.Text);
        Assert.Equal(new Rectangle(12, 105, 75, 23), ok.Bounds);
    }

    [Fact]
    public void ARenamedComponentRenamesItsFieldAndEveryStatement()
    {
        TestPlatform.Install();
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var model = new DesignerCodeReader().ReadFile(path);
        model.Rename(model.Find("label1")!, "titleLabel");
        var written = new DesignerCodeWriter().Write(model, File.ReadAllText(path));
        Assert.DoesNotContain("label1.", written);
        Assert.DoesNotContain(" label1;", written);
        Assert.DoesNotContain("(label1)", written);
        Assert.Contains("private Label titleLabel;", written);
        Assert.Contains("titleLabel.Name = \"titleLabel\";", written);
        Assert.Contains("Controls.Add(titleLabel);", written);
        // User code that refers to label1 (MainForm.cs) is renamed by the host with Roslyn (Ф5.5), not here.
    }

    [Fact]
    public void ARemovedComponentLeavesNoTrace()
    {
        TestPlatform.Install();
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var model = new DesignerCodeReader().ReadFile(path);
        var button = model.Find("button1")!;
        ((Form)model.Root.Instance).Controls.Remove((Control)button.Instance);
        model.RemoveComponent(button);
        var written = new DesignerCodeWriter().Write(model, File.ReadAllText(path));
        Assert.DoesNotContain("button1", written);
        // The handler method stays in MainForm.cs, as in VS; the code still compiles.
        var type = Compile(Path.GetDirectoryName(path)!, path, written).GetType("HelloForms.MainForm")!;
        Assert.Single(RealInitializeComponent(type).Controls.Cast<Control>());
        // The freed name is given out again.
        Assert.Equal("button1", model.CreateUniqueName(typeof(Button)));
    }

    [Fact]
    public void NamesAreCheckedAndUnique()
    {
        TestPlatform.Install();
        var model = new DesignerCodeReader().ReadFile(Sample("HelloForms/MainForm.Designer.cs"));
        Assert.Equal("button2", model.CreateUniqueName(typeof(Button)));
        Assert.Equal("textBox1", model.CreateUniqueName(typeof(TextBox)));
        Assert.Throws<ArgumentException>(() => model.Rename(model.Find("button1")!, "label1"));
        Assert.Throws<ArgumentException>(() => model.Rename(model.Find("button1")!, "components"));
        Assert.Throws<ArgumentException>(() => model.Rename(model.Find("button1")!, "class"));
        Assert.Throws<ArgumentException>(() => model.Rename(model.Find("button1")!, "MainForm"));
        Assert.Throws<ArgumentException>(() => model.BindEvent(model.Find("button1")!, "Clack", "x"));
    }

    // --- values ----------------------------------------------------------------------------------

    private const string EmptyForm = """
        namespace Demo
        {
            partial class TestForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                }
            }
        }
        """;

    private const string EmptyFormBase = "namespace Demo { public partial class TestForm : Form { } }";

    [Fact]
    public void ValuesAreSpelledTheWayTheDesignerSpellsThem()
    {
        TestPlatform.Install();
        var model = new DesignerCodeReader().Read(EmptyForm, new[] { EmptyFormBase });
        var form = (Form)model.Root.Instance;

        var button = new Button
        {
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            BackColor = Color.FromArgb(10, 20, 30),
            ForeColor = Color.Red,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold | FontStyle.Italic),
            Padding = new Padding(5),
            Margin = new Padding(1, 2, 3, 4),
            Text = "Say \"hi\"\r\nнизко",
        };
        var box = new TextBox { PasswordChar = '*', BackColor = SystemColors.Info, Anchor = AnchorStyles.None };
        var label = new Label { Font = new Font("Arial", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 204), BackColor = Color.FromArgb(128, 1, 2, 3) };
        var spin = new NumericUpDown { Maximum = 1000, DecimalPlaces = 2, Increment = 0.25m };
        var list = new ListBox();
        list.Items.AddRange(new object[] { "a", "b" });
        var flow = new FlowLayoutPanel();
        var inFlow = new CheckBox();
        flow.Controls.Add(inFlow);
        flow.SetFlowBreak(inFlow, true);
        var menu = new MenuStrip();
        var item = new ToolStripMenuItem { Text = "&New", ShortcutKeys = Keys.Control | Keys.Shift | Keys.N };
        menu.Items.Add(item);
        var tip = new ToolTip();
        var timer = new System.Windows.Forms.Timer { Interval = 250 };
        form.Controls.AddRange(new Control[] { button, box, label, spin, list, flow, menu });
        tip.SetToolTip(button, "A hint");
        foreach (var c in new IComponent[] { button, box, label, spin, list, flow, inFlow, menu, item, tip, timer }) model.AddComponent(c);
        form.MainMenuStrip = menu;

        var written = new DesignerCodeWriter().Write(model, EmptyForm);
        var lines = BodyLines(written);
        AssertLine(lines, "button1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;");
        AssertLine(lines, "button1.BackColor = Color.FromArgb(10, 20, 30);");
        AssertLine(lines, "button1.ForeColor = Color.Red;");
        AssertLine(lines, "button1.Font = new Font(\"Segoe UI\", 12F, FontStyle.Bold | FontStyle.Italic);");
        AssertLine(lines, "button1.Padding = new Padding(5);");
        AssertLine(lines, "button1.Margin = new Padding(1, 2, 3, 4);");
        AssertLine(lines, "button1.Text = \"Say \\\"hi\\\"\\r\\nнизко\";");
        AssertLine(lines, "toolTip1.SetToolTip(button1, \"A hint\");");
        AssertLine(lines, "textBox1.PasswordChar = '*';");
        AssertLine(lines, "textBox1.BackColor = SystemColors.Info;");
        AssertLine(lines, "textBox1.Anchor = AnchorStyles.None;");
        AssertLine(lines, "label1.Font = new Font(\"Arial\", 8.25F, FontStyle.Regular, GraphicsUnit.Point, 204);");
        AssertLine(lines, "label1.BackColor = Color.FromArgb(128, 1, 2, 3);");
        AssertLine(lines, "numericUpDown1.Maximum = new decimal(new int[] { 1000, 0, 0, 0 });");
        AssertLine(lines, "numericUpDown1.Increment = new decimal(new int[] { 25, 0, 0, 131072 });");
        AssertLine(lines, "listBox1.Items.AddRange(new object[] { \"a\", \"b\" });");
        AssertLine(lines, "flowLayoutPanel1.SetFlowBreak(checkBox1, true);");
        AssertLine(lines, "toolStripMenuItem1.ShortcutKeys = Keys.Control | Keys.Shift | Keys.N;");
        AssertLine(lines, "menuStrip1.Items.AddRange(new ToolStripItem[] { toolStripMenuItem1 });");
        AssertLine(lines, "MainMenuStrip = menuStrip1;");
        AssertLine(lines, "components = new System.ComponentModel.Container();");
        AssertLine(lines, "toolTip1 = new ToolTip(components);");
        AssertLine(lines, "timer1 = new System.Windows.Forms.Timer(components);");
        AssertLine(lines, "timer1.Interval = 250;");
        AssertLine(lines, "((System.ComponentModel.ISupportInitialize)numericUpDown1).BeginInit();");
        Assert.Contains("private System.Windows.Forms.Timer timer1;", written);

        // What was written reads back into the same values, and compiles.
        var reread = new DesignerCodeReader().Read(written, new[] { EmptyFormBase });
        var button2 = (Button)reread.Find("button1")!.Instance;
        Assert.Equal(button.Font, button2.Font);
        Assert.Equal(button.Text, button2.Text);
        Assert.Equal(button.BackColor, button2.BackColor);
        Assert.Equal(1000m, ((NumericUpDown)reread.Find("numericUpDown1")!.Instance).Maximum);
        Assert.Equal(0.25m, ((NumericUpDown)reread.Find("numericUpDown1")!.Instance).Increment);
        Assert.Equal(Keys.Control | Keys.Shift | Keys.N, ((ToolStripMenuItem)reread.Find("toolStripMenuItem1")!.Instance).ShortcutKeys);
        Assert.Equal((byte)204, ((Label)reread.Find("label1")!.Instance).Font.GdiCharSet);
        Assert.True(((FlowLayoutPanel)reread.Find("flowLayoutPanel1")!.Instance).GetFlowBreak((Control)reread.Find("checkBox1")!.Instance));
        Assert.Equal(written, new DesignerCodeWriter().Write(reread, written));
        Compile("", null, written, EmptyFormBase);
    }

    [Fact]
    public void APlaceholderKeepsWhatTheDesignerCouldNotInterpret()
    {
        TestPlatform.Install();
        var source = """
            namespace Demo
            {
                partial class TestForm : Form
                {
                    private void InitializeComponent()
                    {
                        gauge1 = new MyApp.Controls.FancyGauge();
                        ((System.ComponentModel.ISupportInitialize)gauge1).BeginInit();
                        SuspendLayout();
                        gauge1.Location = new Point(10, 20);
                        gauge1.Size = new Size(100, 50);
                        gauge1.Needle = MyApp.Controls.NeedleStyle.Sharp;
                        gauge1.ValueChanged += gauge1_ValueChanged;
                        ((System.ComponentModel.ISupportInitialize)gauge1).EndInit();
                        Controls.Add(gauge1);
                        ResumeLayout(false);
                    }

                    private MyApp.Controls.FancyGauge gauge1;
                }
            }
            """;
        var model = new DesignerCodeReader().Read(source);
        var lines = BodyLines(new DesignerCodeWriter().Write(model, source));
        AssertLine(lines, "gauge1 = new MyApp.Controls.FancyGauge();");
        AssertLine(lines, "((System.ComponentModel.ISupportInitialize)gauge1).BeginInit();");
        AssertLine(lines, "gauge1.Needle = MyApp.Controls.NeedleStyle.Sharp;");
        AssertLine(lines, "gauge1.Location = new Point(10, 20);");
        AssertLine(lines, "gauge1.ValueChanged += gauge1_ValueChanged;");
        AssertLine(lines, "((System.ComponentModel.ISupportInitialize)gauge1).EndInit();");
    }

    // --- helpers ---------------------------------------------------------------------------------

    private static void AssertLine(List<string> lines, string statement) =>
        Assert.True(lines.Contains(statement), $"'{statement}' is not written. InitializeComponent:\n{string.Join("\n", lines)}");

    private static string WriteSample(string relative, out DesignerModel model)
    {
        var path = Sample(relative);
        model = new DesignerCodeReader().ReadFile(path);
        return new DesignerCodeWriter().Write(model, File.ReadAllText(path));
    }

    /// <summary>The statements of InitializeComponent, trimmed, one per line.</summary>
    private static List<string> BodyLines(string source)
    {
        var start = source.IndexOf('{', source.IndexOf("void InitializeComponent()", StringComparison.Ordinal)) + 1;
        var end = source.IndexOf("\n        }", start, StringComparison.Ordinal);
        if (end < 0) end = source.IndexOf("\n    }", start, StringComparison.Ordinal);
        return source.Substring(start, end - start).Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
    }
}
