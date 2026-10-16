using System.ComponentModel;
using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>What the grid's visual editors get from the host (decision 183): the color picker's web and system colors with their swatches, the font families.</summary>
public sealed class DesignerPropertyEditorTests
{
    public DesignerPropertyEditorTests() => TestPlatform.Install();

    private static DesignSurface HelloForms()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        return DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
    }

    [Fact]
    public void AColorRowCarriesTheWebAndSystemColors()
    {
        using var s = HelloForms();
        var row = s.GetProperties("button1").Single(r => r.Name == "BackColor");
        Assert.Equal("color", row.Editor);
        Assert.Contains("Red", row.Options!);
        Assert.DoesNotContain("Control", row.Options!);
        Assert.Contains("Control", row.SystemOptions!);
        Assert.Matches("^#[0-9a-f]{6}$", row.Swatches!["Control"]);
    }

    [Fact]
    public void AFontRowCarriesTheFamilies()
    {
        using var s = HelloForms();
        var row = s.GetProperties("button1").Single(r => r.Name == "Font");
        Assert.Equal("font", row.Editor);
        Assert.NotEmpty(row.Options!);

        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "button1", Prop = "Font", Value = "Segoe UI, 12pt, style=Bold, Italic" } });
        Assert.Contains("button1.Font = new Font(\"Segoe UI\", 12F, FontStyle.Bold | FontStyle.Italic);", s.Source.Replace("this.", ""));
    }

    [Fact]
    public void AFontWithSeveralStylesReadsBackFromItsText()
    {
        var converter = TypeDescriptor.GetConverter(typeof(Font));
        using var font = new Font("Segoe UI", 12f, FontStyle.Bold | FontStyle.Underline);
        var text = converter.ConvertToInvariantString(font)!;
        Assert.Equal("Segoe UI, 12pt, style=Bold, Underline", text);
        using var back = (Font)converter.ConvertFromInvariantString(text)!;
        Assert.Equal((12f, FontStyle.Bold | FontStyle.Underline), (back.Size, back.Style));
    }

    [Fact]
    public void AButtonWithAColorOfItsOwnDropsTheVisualStyleBackground()
    {
        using var s = HelloForms();
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "button1", Prop = "BackColor", Value = "Red" } });
        var code = s.Source.Replace("this.", "");
        Assert.Contains("button1.BackColor = Color.Red;", code);
        Assert.Contains("button1.UseVisualStyleBackColor = false;", code);

        // At run time too.
        using var button = new Button();
        Assert.True(button.UseVisualStyleBackColor);
        button.BackColor = Color.Red;
        Assert.False(button.UseVisualStyleBackColor);
    }
}
