using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using NetForms.Design.Serialization;
using Xunit;

namespace NetForms.Tests;

/// <summary>
/// Ф5.1: <see cref="DesignerCodeReader"/> reads <c>InitializeComponent()</c> without compiling it.
/// The gate: for every form in <c>samples/</c> the object tree the reader builds is the tree the
/// compiled <c>InitializeComponent()</c> builds — names, types, bounds, dock, anchor, z-order and
/// every browsable property of every component.
/// </summary>
public class DesignerCodeReaderTests
{
    public static string RepoRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "NetForms.slnx"))) return dir.FullName;
            throw new InvalidOperationException("NetForms.slnx not found above " + AppContext.BaseDirectory);
        }
    }

    internal static string Sample(string relative) => Path.Combine(RepoRoot, "samples", relative);

    public static TheoryData<string, Type> SampleForms => new()
    {
        { "HelloForms/MainForm.Designer.cs", typeof(HelloForms.MainForm) },
        { "Gallery/GalleryForm.Designer.cs", typeof(Gallery.GalleryForm) },
        { "Gallery/CollectionsForm.Designer.cs", typeof(Gallery.CollectionsForm) },
        { "Gallery/ResourcesForm.Designer.cs", typeof(Gallery.ResourcesForm) },
        { "Gallery/ColumnsForm.Designer.cs", typeof(Gallery.ColumnsForm) },
        { "Strips/MainForm.Designer.cs", typeof(Strips.MainForm) },
        { "MdiDemo/MainForm.Designer.cs", typeof(MdiDemo.MainForm) },
        { "MdiDemo/DocumentForm.Designer.cs", typeof(MdiDemo.DocumentForm) },
        { "GenLabs/MainForm.Designer.cs", typeof(Form1) },
    };

    // --- the gate --------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(SampleForms))]
    public void ReaderBuildsTheTreeThatInitializeComponentBuilds(string designerFile, Type formType)
    {
        var model = new DesignerCodeReader().ReadFile(Sample(designerFile));
        Assert.Equal(formType.Name, model.ClassName);
        Assert.Equal(formType.Namespace, model.Namespace);
        Assert.True(model.RootType.IsAssignableFrom(formType), $"{model.RootType} is not a base of {formType}");

        var real = RealInitializeComponent(formType);
        var realNames = formType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(f => (Name: f.Name, Value: f.GetValue(real)))
            .Where(f => f.Value != null && (f.Value is IComponent || f.Value is IContainer))
            .ToList();
        var modelNames = model.Components.Where(c => c.Name != null).Select(c => (Name: c.Name!, Value: (object?)c.Instance)).ToList();

        Assert.Equal(realNames.Select(n => n.Name).OrderBy(n => n), modelNames.Select(n => n.Name).OrderBy(n => n));

        var expected = Snapshot(real, model.RootType, realNames!);
        var actual = Snapshot(model.Root.Instance, model.RootType, modelNames!);
        AssertSameLines(expected, actual);
    }

    /// <summary>
    /// What the compiled application gets: the form's base constructor, then its own
    /// <c>InitializeComponent()</c> — and nothing the form's constructor does afterwards
    /// (<c>MdiDemo</c> opens documents, <c>Gallery</c> fills lists), which the designer never sees either.
    /// </summary>
    internal static Form RealInitializeComponent(Type formType)
    {
        var form = (Form)RuntimeHelpers.GetUninitializedObject(formType);
        var baseCtor = formType.BaseType!.GetConstructor(Type.EmptyTypes)!;
        baseCtor.Invoke(form, null);
        formType.GetMethod("InitializeComponent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        return form;
    }

    internal static List<string> Snapshot(object root, Type rootType, List<(string Name, object Value)> components)
    {
        var names = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        foreach (var (name, value) in components) names[value] = name;
        names[root] = "(root)";
        string NameOf(object o) => names.TryGetValue(o, out var n) ? n : o is Control { Name.Length: > 0 } c ? c.Name : "(inline " + o.GetType().Name + ")";

        var lines = new List<string>();

        void Tree(Control c, string indent)
        {
            var type = ReferenceEquals(c, root) ? "(root)" : c.GetType().FullName;
            lines.Add($"{indent}{NameOf(c)} : {type} bounds={c.Bounds} dock={c.Dock} anchor={c.Anchor} text={c.Text}");
            if (c is ToolStrip strip) Items(strip.Items, indent + "    ");
            foreach (Control child in c.Controls) Tree(child, indent + "  ");
        }

        void Items(ToolStripItemCollection items, string indent)
        {
            foreach (ToolStripItem item in items)
            {
                lines.Add($"{indent}{NameOf(item)} : {item.GetType().FullName} text={item.Text}");
                if (item is ToolStripDropDownItem dd && dd.HasDropDownItems) Items(dd.DropDownItems, indent + "    ");
            }
        }

        Tree((Control)root, "");

        foreach (var (name, value) in components.OrderBy(c => c.Name, StringComparer.Ordinal).Prepend(("(root)", root)))
        {
            var props = ReferenceEquals(value, root)
                ? TypeDescriptor.GetProperties(rootType, new Attribute[] { BrowsableAttribute.Yes })
                : TypeDescriptor.GetProperties(value, new Attribute[] { BrowsableAttribute.Yes });
            foreach (PropertyDescriptor p in props)
            {
                if (p.SerializationVisibility == DesignerSerializationVisibility.Hidden) continue;
                string text;
                try { text = Format(p.GetValue(value), p, NameOf); }
                catch (Exception ex) { text = "!" + ex.GetType().Name; }
                lines.Add($"{name}.{p.Name} = {text}");
            }
        }
        return lines;
    }

    private static string Format(object? v, PropertyDescriptor p, Func<object, string> nameOf)
    {
        switch (v)
        {
            case null: return "null";
            case string s: return "\"" + s + "\"";
            case IComponent component: return nameOf(component);
            case ICollection collection: return "count=" + collection.Count;
            case Type t: return t.FullName!;
        }
        try
        {
            var converter = p.Converter;
            if (converter != null && converter.CanConvertTo(typeof(string)) && converter.GetType() != typeof(TypeConverter))
                return converter.ConvertToInvariantString(v) ?? "null";
        }
        catch (Exception) { }
        return v is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : v.GetType().IsValueType ? v.ToString()! : v.GetType().FullName!;
    }

    internal static void AssertSameLines(List<string> expected, List<string> actual, string what = "The reader's tree differs from InitializeComponent():")
    {
        var diff = new List<string>();
        int n = Math.Max(expected.Count, actual.Count);
        for (int i = 0; i < n && diff.Count < 20; i++)
        {
            var e = i < expected.Count ? expected[i] : "<none>";
            var a = i < actual.Count ? actual[i] : "<none>";
            if (e != a) diff.Add($"#{i}\n  compiled: {e}\n  reader:   {a}");
        }
        Assert.True(diff.Count == 0, what + "\n" + string.Join("\n", diff));
    }

    // --- what the model records ------------------------------------------------------------------

    [Fact]
    public void BaseClassComesFromTheCompanionFile()
    {
        var model = new DesignerCodeReader().ReadFile(Sample("HelloForms/MainForm.Designer.cs"));
        Assert.Equal("HelloForms", model.Namespace);
        Assert.Equal("MainForm", model.ClassName);
        Assert.Equal("Form", model.RootTypeName);
        Assert.Same(typeof(Form), model.RootType);
        Assert.True(model.Root.IsRoot);
        Assert.Equal(new[] { "components", "button1", "label1" }, model.Fields.Select(f => f.Name));
        Assert.Equal("Button", model.Fields[1].TypeName);
        Assert.Equal("private", model.Fields[1].Modifiers);
    }

    [Fact]
    public void EventHandlersAreRecordedNotSubscribed()
    {
        var model = new DesignerCodeReader().ReadFile(Sample("HelloForms/MainForm.Designer.cs"));
        var click = Assert.Single(model.Events);
        Assert.Equal("button1", click.Component.Name);
        Assert.Equal("Click", click.EventName);
        Assert.Equal("button1_Click", click.HandlerName);

        // Nothing is wired: clicking in the designer must not run user code (which does not exist here).
        var button = (Button)model.Find("button1")!.Instance;
        button.PerformClick();
        Assert.Equal("label1", ((Label)model.Find("label1")!.Instance).Text);
    }

    [Fact]
    public void AssignmentsAndStatementsKeepSourceOrderAndText()
    {
        var model = new DesignerCodeReader().ReadFile(Sample("HelloForms/MainForm.Designer.cs"));
        var button = model.Find("button1")!;
        Assert.Equal(new[] { "Anchor", "Location", "Name", "Size", "TabIndex", "Text", "UseVisualStyleBackColor" },
            button.Assignments.Select(a => a.Path));
        var location = button.Assignments[1];
        Assert.Equal(new Point(197, 105), location.Value);
        Assert.Equal("new Point(197, 105)", location.Source);
        Assert.True(location.Applied);

        Assert.Equal(DesignerStatementKind.Create, model.Statements[0].Kind);
        Assert.Equal("button1 = new Button();", model.Statements[0].Source);
        var suspend = model.Statements[2];
        Assert.Equal(DesignerStatementKind.Call, suspend.Kind);
        Assert.Same(model.Root, suspend.Target);
        Assert.Equal("SuspendLayout", suspend.Member);
        var clientSize = model.Statements.Single(s => s.Member == "ClientSize");
        Assert.Same(model.Root, clientSize.Target);
        Assert.Equal(new Size(284, 140), clientSize.Assignment!.Value);
        Assert.Contains(model.Statements, s => s.Kind == DesignerStatementKind.Call && s.Member == "Controls.Add" && s.Target == model.Root);
    }

    [Fact]
    public void ClassicThisQualifiedStyleAndAFieldThatShadowsAFormProperty()
    {
        // GenLabs is designer code of the .NET Framework era: this.-qualified, fully-qualified types,
        // new EventHandler(...), a Font with a GDI charset — and a TextBox field named "Size".
        var model = new DesignerCodeReader().ReadFile(Sample("GenLabs/MainForm.Designer.cs"));
        Assert.Null(model.Namespace);
        Assert.Equal("Form1", model.ClassName);
        Assert.Equal("System.Windows.Forms.Form", model.RootTypeName);

        var sizeBox = Assert.IsType<TextBox>(model.Find("Size")!.Instance);
        Assert.Equal(new Point(69, 17), sizeBox.Location);
        Assert.Equal(167, sizeBox.Width); // the height follows the font, as for a compiled TextBox
        var form = (Form)model.Root.Instance;
        Assert.Equal(new Size(594, 505), form.ClientSize);
        Assert.Equal(FormWindowState.Maximized, form.WindowState);

        var timer = Assert.IsType<System.Windows.Forms.Timer>(model.Find("timer1")!.Instance);
        Assert.Equal(1, timer.Interval);
        Assert.False(timer.Enabled);
        Assert.Contains(model.Events, e => e.Component.Name == "timer1" && e.EventName == "Tick" && e.HandlerName == "Timer1_Tick");
        Assert.Contains(model.Events, e => e.Component.IsRoot && e.EventName == "Load" && e.HandlerName == "Form1_Load");

        var label6 = (Label)model.Find("label6")!.Instance;
        Assert.Equal(FontStyle.Underline, label6.Font.Style);
        Assert.Equal(8.25f, label6.Font.Size);
        Assert.Equal(RightToLeft.Yes, label6.RightToLeft);

        var beginInit = model.Statements.First(s => s.Member == "BeginInit");
        Assert.Equal("pictureBox", beginInit.Target!.Name);
    }

    [Fact]
    public void InlineObjectsBecomeAnonymousComponents()
    {
        var model = new DesignerCodeReader().ReadFile(Sample("Gallery/GalleryForm.Designer.cs"));
        var inline = model.Components.Where(c => c.IsInline).ToList();
        Assert.Equal(12, inline.Count);
        var one = inline[0];
        Assert.IsType<Button>(one.Instance);
        Assert.Equal(new[] { "Text", "AutoSize" }, one.Assignments.Select(a => a.Path));
        Assert.Equal("One", ((Button)one.Instance).Text);

        var flow = (FlowLayoutPanel)model.Find("flowLayoutPanel1")!.Instance;
        Assert.Same(one.Instance, flow.Controls[0]);

        // SetColumnSpan(GetControlFromPosition(1, 0), 2): a call whose argument is itself a call.
        var table = (TableLayoutPanel)model.Find("tableLayoutPanel1")!.Instance;
        Assert.Equal(2, table.GetColumnSpan(table.GetControlFromPosition(1, 0)!));
    }

    [Fact]
    public void NestedObjectsAreAttributedToTheirComponent()
    {
        var model = new DesignerCodeReader().ReadFile(Sample("Gallery/GalleryForm.Designer.cs"));
        var add = model.Statements.First(s => s.Member == "Panel1.Controls.Add");
        Assert.Equal("splitContainer1", add.Target!.Name);
        var suspend = model.Statements.First(s => s.Member == "Panel2.SuspendLayout");
        Assert.Equal("splitContainer1", suspend.Target!.Name);
    }

    // --- the subset and its limits ---------------------------------------------------------------

    private const string Header = """
        namespace Demo
        {
            partial class TestForm : Form
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {

        """;

    private const string Footer = """
                }

                private Button button1;
                private MyApp.Controls.FancyGauge gauge1;
            }
        }
        """;

    private static DesignerModel ReadBody(string body) => new DesignerCodeReader().Read(Header + body + Footer, filePath: "TestForm.Designer.cs");

    /// <summary>1-based line of the first body line inside <see cref="Header"/>.</summary>
    private static int BodyLine => Header.Split('\n').Length;

    [Fact]
    public void ALoopIsNotGuessedAtButReportedWithItsLine()
    {
        var ex = Assert.Throws<DesignerCodeException>(() => ReadBody("""
                        button1 = new Button();
                        for (int i = 0; i < 3; i++) Controls.Add(new Button());

            """));
        Assert.Equal(BodyLine + 1, ex.Line);
        Assert.Equal("TestForm.Designer.cs", ex.FilePath);
        Assert.StartsWith("TestForm.Designer.cs(" + (BodyLine + 1) + ",", ex.Message);
    }

    [Fact]
    public void AnUnknownPropertyIsReportedWithItsLine()
    {
        var ex = Assert.Throws<DesignerCodeException>(() => ReadBody("""
                        button1 = new Button();
                        button1.Colour = Color.Red;

            """));
        Assert.Equal(BodyLine + 1, ex.Line);
        Assert.Contains("Colour", ex.Reason);
    }

    [Fact]
    public void ALambdaHandlerIsRefused()
    {
        var ex = Assert.Throws<DesignerCodeException>(() => ReadBody("""
                        button1 = new Button();
                        button1.Click += (s, e) => { };

            """));
        Assert.Equal(BodyLine + 1, ex.Line);
    }

    [Fact]
    public void ASetterThatThrowsIsReportedWithTheSettersMessage()
    {
        var ex = Assert.Throws<DesignerCodeException>(() => ReadBody("""
                        var bar = new ProgressBar();
                        bar.Value = 500;

            """));
        Assert.Equal(BodyLine + 1, ex.Line);
        Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
    }

    [Fact]
    public void AControlOfAnUnknownTypeGetsAPlaceholder()
    {
        var model = ReadBody("""
                        gauge1 = new MyApp.Controls.FancyGauge();
                        SuspendLayout();
                        gauge1.Location = new Point(10, 20);
                        gauge1.Size = new Size(100, 50);
                        gauge1.Needle = MyApp.Controls.NeedleStyle.Sharp;
                        gauge1.ValueChanged += gauge1_ValueChanged;
                        Controls.Add(gauge1);
                        ResumeLayout(false);

            """);
        var gauge = model.Find("gauge1")!;
        Assert.True(gauge.IsPlaceholder);
        Assert.Equal("MyApp.Controls.FancyGauge", gauge.TypeName);
        var placeholder = Assert.IsType<DesignerPlaceholder>(gauge.Instance);
        Assert.Equal(new Rectangle(10, 20, 100, 50), placeholder.Bounds);
        Assert.Same(placeholder, ((Form)model.Root.Instance).Controls[0]);

        var needle = gauge.Assignments.Single(a => a.Path == "Needle");
        Assert.False(needle.Applied);
        Assert.Equal("MyApp.Controls.NeedleStyle.Sharp", needle.Source);
        Assert.Equal("gauge1_ValueChanged", Assert.Single(gauge.Events).HandlerName);
    }

    [Fact]
    public void LiteralsCastsOperatorsAndTargetTypedNew()
    {
        var model = ReadBody("""
                        button1 = new Button();
                        button1.Location = new(3 * 4, 20 - 1);
                        button1.Anchor = (AnchorStyles)((AnchorStyles.Top | AnchorStyles.Left) | AnchorStyles.Right);
                        button1.Text = "a" + "\t" + @"b\c" + 1;
                        button1.BackColor = Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), 0);
                        button1.Padding = new Padding(1, 2, 3, 4);
                        button1.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                        button1.Tag = null;
                        button1.Enabled = !true;

            """);
        var button = (Button)model.Find("button1")!.Instance;
        Assert.Equal(new Point(12, 19), button.Location);
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, button.Anchor);
        Assert.Equal("a\tb\\c1", button.Text);
        Assert.Equal(Color.FromArgb(255, 128, 0), button.BackColor);
        Assert.Equal(new Padding(1, 2, 3, 4), button.Padding);
        Assert.True(button.Font.Bold);
        Assert.False(button.Enabled);
    }

    [Fact]
    public void AFormWithoutAKnownBaseClassIsRefused()
    {
        var source = """
            namespace Demo
            {
                partial class Lonely
                {
                    private void InitializeComponent() { }
                }
            }
            """;
        var ex = Assert.Throws<DesignerCodeException>(() => new DesignerCodeReader().Read(source));
        Assert.Contains("base class", ex.Reason);

        var derived = source.Replace("partial class Lonely", "partial class Lonely : MyApp.BaseForm");
        ex = Assert.Throws<DesignerCodeException>(() => new DesignerCodeReader().Read(derived));
        Assert.Contains("MyApp.BaseForm", ex.Reason);
    }

    [Fact]
    public void AUserControlIsDesignedAsAUserControl()
    {
        var model = new DesignerCodeReader().Read("""
            partial class Card
            {
                private void InitializeComponent()
                {
                    label1 = new System.Windows.Forms.Label();
                    label1.Text = "Title";
                    Controls.Add(label1);
                    Size = new System.Drawing.Size(200, 80);
                }

                private System.Windows.Forms.Label label1;
            }
            """, new[] { "public partial class Card : System.Windows.Forms.UserControl { }" });
        Assert.Same(typeof(UserControl), model.RootType);
        Assert.Equal(new Size(200, 80), ((Control)model.Root.Instance).Size);
    }
}
