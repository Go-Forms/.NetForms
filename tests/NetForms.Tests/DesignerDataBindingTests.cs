using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

public sealed class DesignerDataBindingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-binding-" + Guid.NewGuid().ToString("N"));

    public DesignerDataBindingTests()
    {
        TestPlatform.Install();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>The fixture library's build, copied as the "bin" of the project: its Customer and Order are the project's classes.</summary>
    private string ProjectBuild()
    {
        var config = AppContext.BaseDirectory.Contains($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}") ? "Release" : "Debug";
        var bins = Path.Combine(RepoRoot, "tests", "Fixtures", "ControlLibrary", "bin");
        var output = new[] { config, config == "Release" ? "Debug" : "Release" }.Select(c => Path.Combine(bins, c, "net10.0"))
            .First(d => File.Exists(Path.Combine(d, "ControlLibrary.dll")));
        var bin = Path.Combine(_dir, "bin");
        Directory.CreateDirectory(bin);
        foreach (var f in Directory.GetFiles(output)) File.Copy(f, Path.Combine(bin, Path.GetFileName(f)), overwrite: true);
        return Path.Combine(bin, "ControlLibrary.dll");
    }

    private const string VisualStudioForm = """
        namespace Demo
        {
            partial class CustomersForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    this.components = new System.ComponentModel.Container();
                    this.dataGridView1 = new System.Windows.Forms.DataGridView();
                    this.nameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
                    this.activeDataGridViewCheckBoxColumn = new System.Windows.Forms.DataGridViewCheckBoxColumn();
                    this.customerBindingSource = new System.Windows.Forms.BindingSource(this.components);
                    this.textBox1 = new System.Windows.Forms.TextBox();
                    ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
                    ((System.ComponentModel.ISupportInitialize)(this.customerBindingSource)).BeginInit();
                    this.SuspendLayout();
                    // 
                    // dataGridView1
                    // 
                    this.dataGridView1.AutoGenerateColumns = false;
                    this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
                    this.dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
                    this.nameDataGridViewTextBoxColumn,
                    this.activeDataGridViewCheckBoxColumn});
                    this.dataGridView1.DataSource = this.customerBindingSource;
                    this.dataGridView1.Location = new System.Drawing.Point(12, 12);
                    this.dataGridView1.Name = "dataGridView1";
                    this.dataGridView1.Size = new System.Drawing.Size(300, 150);
                    this.dataGridView1.TabIndex = 0;
                    // 
                    // nameDataGridViewTextBoxColumn
                    // 
                    this.nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
                    this.nameDataGridViewTextBoxColumn.HeaderText = "Name";
                    this.nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
                    // 
                    // activeDataGridViewCheckBoxColumn
                    // 
                    this.activeDataGridViewCheckBoxColumn.DataPropertyName = "Active";
                    this.activeDataGridViewCheckBoxColumn.HeaderText = "Active";
                    this.activeDataGridViewCheckBoxColumn.Name = "activeDataGridViewCheckBoxColumn";
                    // 
                    // customerBindingSource
                    // 
                    this.customerBindingSource.DataSource = typeof(ControlLibrary.Customer);
                    // 
                    // textBox1
                    // 
                    this.textBox1.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.customerBindingSource, "Name", true));
                    this.textBox1.Location = new System.Drawing.Point(12, 170);
                    this.textBox1.Name = "textBox1";
                    this.textBox1.Size = new System.Drawing.Size(100, 23);
                    this.textBox1.TabIndex = 1;
                    // 
                    // CustomersForm
                    // 
                    this.ClientSize = new System.Drawing.Size(330, 210);
                    this.Controls.Add(this.textBox1);
                    this.Controls.Add(this.dataGridView1);
                    this.Name = "CustomersForm";
                    ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
                    ((System.ComponentModel.ISupportInitialize)(this.customerBindingSource)).EndInit();
                    this.ResumeLayout(false);
                    this.PerformLayout();
                }

                private System.Windows.Forms.DataGridView dataGridView1;
                private System.Windows.Forms.DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
                private System.Windows.Forms.DataGridViewCheckBoxColumn activeDataGridViewCheckBoxColumn;
                private System.Windows.Forms.BindingSource customerBindingSource;
                private System.Windows.Forms.TextBox textBox1;
            }
        }
        """;

    private const string Code = "namespace Demo { public partial class CustomersForm : System.Windows.Forms.Form { public CustomersForm() { InitializeComponent(); } } }";

    private string WriteForm(string designer = VisualStudioForm)
    {
        var path = Path.Combine(_dir, "CustomersForm.Designer.cs");
        File.WriteAllText(path, designer);
        File.WriteAllText(Path.Combine(_dir, "CustomersForm.cs"), Code);
        return path;
    }

    private const string EmptyForm = """
        namespace Demo
        {
            partial class CustomersForm
            {
                private System.ComponentModel.IContainer components = null;

                private void InitializeComponent()
                {
                    this.SuspendLayout();
                    // 
                    // CustomersForm
                    // 
                    this.ClientSize = new System.Drawing.Size(400, 300);
                    this.Name = "CustomersForm";
                    this.ResumeLayout(false);
                }
            }
        }
        """;

    private static string Plain(DesignSurface s) => s.Source.Replace("this.", "").Replace("System.Windows.Forms.", "").Replace("System.Drawing.", "");

    private static DesignerOp Set(string id, string prop, string value) => new() { Op = "setProp", Id = id, Prop = prop, Value = value };

    private static DesignerOp Add(string type, int x = 10, int y = 10) => new() { Op = "add", Type = "System.Windows.Forms." + type, Parent = "", X = x, Y = y };

    [Fact]
    public void AVisualStudioFormWithBindingsOpensAndKeepsThem()
    {
        using var libraries = DesignerLibraries.Load(new[] { ProjectBuild() });
        using var s = DesignSurface.Open(WriteForm(), libraries);

        var text = s.GetProperties("textBox1").Single(r => r.Name == "DataBindings.Text");
        Assert.Equal(("Text", DesignSurface.BindingsCategory, "customerBindingSource - Name"), (text.Label, text.Category, text.Value));
        Assert.True(text.Modified);
        Assert.Contains("customerBindingSource - Age", text.Options!);
        Assert.DoesNotContain("customerBindingSource - Orders", text.Options!);
        Assert.DoesNotContain(s.GetProperties("textBox1"), r => r.Name == "DataBindings");

        var source = s.GetProperties("dataGridView1").Single(r => r.Name == "DataSource");
        Assert.Equal(("component", "customerBindingSource"), (source.Editor, source.Value));
        Assert.Contains("typeof(ControlLibrary.Customer)", source.Options!);
        Assert.Equal("typeof(ControlLibrary.Customer)", s.GetProperties("customerBindingSource").Single(r => r.Name == "DataSource").Value);
        Assert.Contains("Orders", s.GetProperties("customerBindingSource").Single(r => r.Name == "DataMember").Options!);
        Assert.Contains("Age", s.GetProperties("nameDataGridViewTextBoxColumn").Single(r => r.Name == "DataPropertyName").Options!);

        // An edit elsewhere leaves the bindings and the columns as they were: no columns of the grid's own.
        var before = Plain(s);
        s.Apply(new[] { Set("textBox1", "Width", "120") });
        Assert.Contains("textBox1.DataBindings.Add(new Binding(\"Text\", customerBindingSource, \"Name\", true));", Plain(s));
        Assert.Equal(2, ((DataGridView)s.Model.Find("dataGridView1")!.Instance).Columns.Count);
        static string WithoutSize(string code) => string.Join("\n", code.Split('\n').Where(l => !l.Contains("textBox1.Size")));
        Assert.Equal(WithoutSize(before), WithoutSize(Plain(s)));
    }

    [Fact]
    public void AClassOfTheProjectPickedForAGridMakesABindingSourceAndTheColumns()
    {
        using var libraries = DesignerLibraries.Load(new[] { ProjectBuild() });
        using var s = DesignSurface.Open(WriteForm(EmptyForm), libraries);
        s.Apply(new[] { Add("DataGridView") });
        s.Apply(new[] { Set("dataGridView1", "DataSource", "typeof(ControlLibrary.Customer)") });
        var code = Plain(s);
        Assert.Contains("customerBindingSource = new BindingSource(components);", code);
        Assert.Contains("customerBindingSource.DataSource = typeof(ControlLibrary.Customer);", code);
        Assert.Contains("dataGridView1.DataSource = customerBindingSource;", code);
        Assert.Contains("nameDataGridViewTextBoxColumn.DataPropertyName = \"Name\";", code);
        Assert.Contains("ageDataGridViewTextBoxColumn.HeaderText = \"Age\";", code);
        Assert.Contains("activeDataGridViewCheckBoxColumn.DataPropertyName = \"Active\";", code);
        Assert.DoesNotContain("ordersDataGridView", code);
        // The grid's own AutoGenerateColumns stays as it is (true): the columns are the form's.
        Assert.DoesNotContain("AutoGenerateColumns", code);

        // A second grid for the same class shares the binding source.
        s.Apply(new[] { Add("DataGridView", 10, 200) });
        s.Apply(new[] { Set("dataGridView2", "DataSource", "typeof(ControlLibrary.Customer)") });
        Assert.Contains("dataGridView2.DataSource = customerBindingSource;", Plain(s));
        Assert.DoesNotContain("customerBindingSource1", Plain(s));
        Assert.Contains("nameDataGridViewTextBoxColumn1.DataPropertyName = \"Name\";", Plain(s));

        // Read back, nothing changes and no column is made twice.
        using var again = DesignSurface.Open(WriteForm(s.Source), libraries);
        again.Apply(Array.Empty<DesignerOp>());
        Assert.Equal(s.Source, again.Source);
        Assert.Equal(3, ((DataGridView)again.Model.Find("dataGridView1")!.Instance).Columns.Count);
    }

    [Fact]
    public void AListAndAControlAreBoundThroughTheGrid()
    {
        using var libraries = DesignerLibraries.Load(new[] { ProjectBuild() });
        using var s = DesignSurface.Open(WriteForm(EmptyForm), libraries);
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.BindingSource" } });
        s.Apply(new[] { Set("bindingSource1", "DataSource", "typeof(ControlLibrary.Customer)"), Add("ComboBox"), Add("TextBox", 10, 50), Add("CheckBox", 10, 90) });

        s.Apply(new[] { Set("comboBox1", "DataSource", "bindingSource1") });
        var display = s.GetProperties("comboBox1").Single(r => r.Name == "DisplayMember");
        Assert.Equal(new[] { "Name", "Age", "Active" }, display.Options);
        s.Apply(new[] { Set("comboBox1", "DisplayMember", "Name"), Set("comboBox1", "ValueMember", "Age") });
        var code = Plain(s);
        Assert.Contains("comboBox1.DataSource = bindingSource1;", code);
        Assert.Contains("comboBox1.DisplayMember = \"Name\";", code);
        Assert.Contains("comboBox1.ValueMember = \"Age\";", code);

        // (DataBindings): the bindable properties of each control.
        Assert.Contains(s.GetProperties("checkBox1"), r => r.Name == "DataBindings.Checked");
        Assert.Contains(s.GetProperties("textBox1"), r => r.Name == "DataBindings.Tag");
        s.Apply(new[] { Set("textBox1", "DataBindings.Text", "bindingSource1 - Name"), Set("checkBox1", "DataBindings.Checked", "bindingSource1 - Active") });
        code = Plain(s);
        Assert.Contains("textBox1.DataBindings.Add(new Binding(\"Text\", bindingSource1, \"Name\", true));", code);
        Assert.Contains("checkBox1.DataBindings.Add(new Binding(\"Checked\", bindingSource1, \"Active\", true));", code);
        Assert.True(s.GetProperties("textBox1").Single(r => r.Name == "DataBindings.Text").Modified);

        // Bound elsewhere, then unbound.
        s.Apply(new[] { Set("textBox1", "DataBindings.Text", "bindingSource1 - Age") });
        Assert.Contains("new Binding(\"Text\", bindingSource1, \"Age\", true)", Plain(s));
        Assert.DoesNotContain("\"Name\", true)", Plain(s).Replace("comboBox1", ""));
        s.Apply(new[] { new DesignerOp { Op = "resetProp", Id = "textBox1", Prop = "DataBindings.Text" } });
        Assert.DoesNotContain("textBox1.DataBindings", Plain(s));

        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { Set("comboBox1", "DataSource", "typeof(Nowhere.Thing)") }));
    }
}
