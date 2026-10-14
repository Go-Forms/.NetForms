using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// A FlowLayoutPanel on the design surface (decision 173): its controls go by their order in Controls (the first is
/// first in the flow). A control added without a place goes at the end, as VS appends; a drop puts it where the
/// insertion mark was; a drag reorders. Before, a new control was put first in Controls (it came first in the flow,
/// reversing the order of adding), and a drag inside the panel did nothing.
/// </summary>
public sealed class DesignerFlowTests
{
    public DesignerFlowTests() => TestPlatform.Install();

    private static DesignSurface WithFlow()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var s = DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.FlowLayoutPanel", Parent = "", X = 10, Y = 150, W = 300, H = 80 } });
        return s;
    }

    private static DesignerOp AddButton(int? index = null) =>
        new() { Op = "add", Type = "System.Windows.Forms.Button", Parent = "flowLayoutPanel1", Index = index };

    private static string[] Order(DesignSurface s) =>
        ((FlowLayoutPanel)s.Model.Find("flowLayoutPanel1")!.Instance).Controls.Cast<Control>().Select(c => c.Name).ToArray();

    /// <summary>The order the file adds them in (what the flow reads back).</summary>
    private static string[] WrittenOrder(DesignSurface s) =>
        s.Source.Replace("this.", "").Split('\n').Select(l => l.Trim())
            .Where(l => l.StartsWith("flowLayoutPanel1.Controls.Add(", StringComparison.Ordinal))
            .Select(l => l["flowLayoutPanel1.Controls.Add(".Length..^2]).ToArray();

    [Fact]
    public void ControlsAddedOneAfterAnotherFlowInThatOrder()
    {
        using var s = WithFlow();
        s.Apply(new[] { AddButton() });
        s.Apply(new[] { AddButton() });
        s.Apply(new[] { AddButton() });
        Assert.Equal(new[] { "button2", "button3", "button4" }, Order(s));
        Assert.Equal(new[] { "button2", "button3", "button4" }, WrittenOrder(s));
        // Laid out left to right in that order.
        var view = s.Render();
        var xs = new[] { "button2", "button3", "button4" }.Select(id => view.Items.Single(i => i.Id == id).X).ToArray();
        Assert.True(xs[0] < xs[1] && xs[1] < xs[2]);
        Assert.Equal(new int?[] { 0, 1, 2 }, new[] { "button2", "button3", "button4" }.Select(id => view.Items.Single(i => i.Id == id).FlowIndex));
        Assert.Equal("LeftToRight", view.Items.Single(i => i.Id == "flowLayoutPanel1").Flow);
    }

    [Fact]
    public void ADropGoesWhereTheMarkWas()
    {
        using var s = WithFlow();
        s.Apply(new[] { AddButton(), AddButton() });
        s.Apply(new[] { AddButton(index: 1) });
        Assert.Equal(new[] { "button2", "button4", "button3" }, Order(s));
        s.Apply(new[] { new DesignerOp { Op = "setParent", Id = "button1", Parent = "flowLayoutPanel1", Index = 0 } });
        Assert.Equal(new[] { "button1", "button2", "button4", "button3" }, Order(s));
    }

    [Fact]
    public void ADragInsideTheFlowReordersIt()
    {
        using var s = WithFlow();
        s.Apply(new[] { AddButton(), AddButton(), AddButton() });
        s.Apply(new[] { new DesignerOp { Op = "reorder", Id = "flowLayoutPanel1", Ids = new[] { "button4", "button2", "button3" } } });
        Assert.Equal(new[] { "button4", "button2", "button3" }, WrittenOrder(s));
        // An insertion point counted with the control still in place: button4 (first) to after button3.
        s.Apply(new[] { new DesignerOp { Op = "setParent", Id = "button4", Parent = "flowLayoutPanel1", Index = 3 } });
        Assert.Equal(new[] { "button2", "button3", "button4" }, Order(s));
        Assert.True(s.Undo());
        Assert.Equal(new[] { "button4", "button2", "button3" }, Order(s));
    }

    [Fact]
    public void ACopyGoesAtTheEndAndAReorderOfOthersIsRefused()
    {
        using var s = WithFlow();
        s.Apply(new[] { AddButton(), AddButton() });
        s.Apply(new[] { new DesignerOp { Op = "duplicate", Ids = new[] { "button2" } } });
        Assert.Equal(new[] { "button2", "button3", "button4" }, Order(s));
        var ex = Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "reorder", Id = "flowLayoutPanel1", Ids = new[] { "button1" } } }));
        Assert.Contains("not in flowLayoutPanel1", ex.Message);
    }
}
