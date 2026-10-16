using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>The Document Outline's order (decision 185): a control's place among its siblings, 0 the front-most.</summary>
public sealed class DesignerOutlineTests
{
    public DesignerOutlineTests() => TestPlatform.Install();

    [Fact]
    public void AControlTakesItsPlaceAmongItsSiblings()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        using var s = DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
        string Order() => string.Join(",", s.Source.Split('\n').Where(l => l.Trim().StartsWith("Controls.Add(") || l.Trim().StartsWith("this.Controls.Add(")).Select(l => l.Trim().Replace("this.", "")));
        Assert.Equal("Controls.Add(label1);,Controls.Add(button1);", Order());
        s.Apply(new[] { new DesignerOp { Op = "setChildIndex", Id = "button1", Index = 0 } });
        Assert.Equal("Controls.Add(button1);,Controls.Add(label1);", Order());
        // Past the end: the back.
        s.Apply(new[] { new DesignerOp { Op = "setChildIndex", Id = "button1", Index = 9 } });
        Assert.Equal("Controls.Add(label1);,Controls.Add(button1);", Order());

        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.SplitContainer", Parent = "" } });
        Assert.Throws<DesignerEditException>(() => s.Apply(new[] { new DesignerOp { Op = "setChildIndex", Id = "splitContainer1.Panel2", Index = 0 } }));
    }
}
