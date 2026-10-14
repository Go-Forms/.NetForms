using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Extender providers in the designer (decision 171): a ToolTip, HelpProvider, ErrorProvider or FlowLayoutPanel on
/// the form gives its controls properties in the grid - "ToolTip on toolTip1" - set and written through the provider
/// (<c>toolTip1.SetToolTip(button1, "...")</c>), as on the VS design surface. Before, the site of a designed
/// component offered no IExtenderListService and TypeDescriptor listed none of them.
/// </summary>
public sealed class DesignerExtenderTests
{
    public DesignerExtenderTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    private static DesignerOp Add(string type, string? parent = null) =>
        new() { Op = "add", Type = "System.Windows.Forms." + type, Parent = parent };

    private static DesignerOp Set(string id, string prop, string value) => new() { Op = "setProp", Id = id, Prop = prop, Value = value };

    [Fact]
    public void AToolTipGivesEveryControlAToolTipProperty()
    {
        using var s = HelloForms();
        Assert.DoesNotContain(s.GetProperties("button1"), r => r.Name.StartsWith("ToolTip", StringComparison.Ordinal));
        s.Apply(new[] { Add("ToolTip") });

        var row = s.GetProperties("button1").Single(r => r.Name == "ToolTip on toolTip1");
        Assert.Equal("", row.Value);
        Assert.Equal("text", row.Editor);
        Assert.False(row.Modified);
        // Not the form: a ToolTip does not extend it.
        Assert.DoesNotContain(s.GetProperties(""), r => r.Name == "ToolTip on toolTip1");

        s.Apply(new[] { Set("button1", "ToolTip on toolTip1", "Saves the file") });
        Assert.Contains("this.toolTip1.SetToolTip(this.button1, \"Saves the file\");".Replace("this.", ""), s.Source.Replace("this.", ""));
        Assert.DoesNotContain("button1.ToolTip", s.Source);
        row = s.GetProperties("button1").Single(r => r.Name == "ToolTip on toolTip1");
        Assert.Equal("Saves the file", row.Value);
        Assert.True(row.Modified);

        s.Apply(new[] { new DesignerOp { Op = "resetProp", Id = "button1", Prop = "ToolTip on toolTip1" } });
        Assert.DoesNotContain("SetToolTip(", s.Source);
    }

    [Fact]
    public void TwoToolTipsGiveTwoPropertiesEachByItsProvider()
    {
        using var s = HelloForms();
        s.Apply(new[] { Add("ToolTip"), Add("ToolTip") });
        var names = s.GetProperties("label1").Select(r => r.Name).Where(n => n.StartsWith("ToolTip on", StringComparison.Ordinal)).ToList();
        Assert.Equal(new[] { "ToolTip on toolTip1", "ToolTip on toolTip2" }, names.OrderBy(n => n));
        s.Apply(new[] { Set("label1", "ToolTip on toolTip2", "second") });
        Assert.Contains("toolTip2.SetToolTip(label1, \"second\");", s.Source.Replace("this.", ""));
        Assert.DoesNotContain("toolTip1.SetToolTip", s.Source);
    }

    [Fact]
    public void HelpProviderAndErrorProviderGiveTheirPropertiesWithTheirEditors()
    {
        using var s = HelloForms();
        s.Apply(new[] { Add("HelpProvider"), Add("ErrorProvider") });
        var rows = s.GetProperties("button1");
        Assert.Equal("bool", rows.Single(r => r.Name == "ShowHelp on helpProvider1").Editor);
        Assert.Equal("enum", rows.Single(r => r.Name == "HelpNavigator on helpProvider1").Editor);
        Assert.Equal("enum", rows.Single(r => r.Name == "IconAlignment on errorProvider1").Editor);
        Assert.Contains(rows, r => r.Name == "Error on errorProvider1");

        s.Apply(new[] { Set("button1", "HelpString on helpProvider1", "Press to save"), Set("button1", "IconAlignment on errorProvider1", "MiddleLeft") });
        var source = s.Source.Replace("this.", "");
        Assert.Contains("helpProvider1.SetHelpString(button1, \"Press to save\");", source);
        Assert.Contains("errorProvider1.SetIconAlignment(button1, ErrorIconAlignment.MiddleLeft);", source);
    }

    [Fact]
    public void AFlowLayoutPanelGivesItsControlsFlowBreakAndATableNoDuplicateCellRows()
    {
        using var s = HelloForms();
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.FlowLayoutPanel", Parent = "", X = 10, Y = 150, W = 200, H = 80 } });
        s.Apply(new[] { Add("Button", "flowLayoutPanel1") });
        Assert.Equal("bool", s.GetProperties("button2").Single(r => r.Name == "FlowBreak on flowLayoutPanel1").Editor);
        Assert.DoesNotContain(s.GetProperties("button1"), r => r.Name.StartsWith("FlowBreak", StringComparison.Ordinal));
        s.Apply(new[] { Set("button2", "FlowBreak on flowLayoutPanel1", "True") });
        Assert.Contains("flowLayoutPanel1.SetFlowBreak(button2, true);", s.Source.Replace("this.", ""));

        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.TableLayoutPanel", Parent = "", X = 220, Y = 150, W = 200, H = 80 } });
        s.Apply(new[] { Add("Button", "tableLayoutPanel1") });
        var cells = s.GetProperties("button3").Select(r => r.Name).Where(n => n.Contains("Column") || n.Contains("Row") || n.Contains("Cell")).ToList();
        Assert.Equal(new[] { "Column", "ColumnSpan", "Row", "RowSpan" }, cells.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void ACopyKeepsItsToolTip()
    {
        using var s = HelloForms();
        s.Apply(new[] { Add("ToolTip") });
        s.Apply(new[] { Set("button1", "ToolTip on toolTip1", "Saves the file") });
        s.Apply(new[] { new DesignerOp { Op = "duplicate", Ids = new[] { "button1" } } });
        Assert.Contains("toolTip1.SetToolTip(button2, \"Saves the file\");", s.Source.Replace("this.", ""));
    }

    [Fact]
    public void TheFileWithExtendersReadsBackTheSame()
    {
        using var s = HelloForms();
        s.Apply(new[] { Add("ToolTip") });
        s.Apply(new[] { Set("label1", "ToolTip on toolTip1", "A label") });
        using var again = DesignSurface.Load(s.Source, s.CompanionSource);
        Assert.Equal("A label", again.GetProperties("label1").Single(r => r.Name == "ToolTip on toolTip1").Value);
        Assert.Equal(s.Source, again.Source);
    }
}
