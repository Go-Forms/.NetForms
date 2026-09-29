using System.Text.Json;
using System.Text.Json.Nodes;
using NetForms.Design;
using NetForms.Design.Serialization;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// The designer and a text editor on the same form (decision 167): what is written to MainForm.cs or
/// MainForm.Designer.cs while the designer is open is taken in before the designer's next edit, never
/// overwritten with the copy the designer read when it opened the form.
/// </summary>
public sealed class DesignerExternalEditTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netforms-external-" + Guid.NewGuid().ToString("N"));

    public DesignerExternalEditTests()
    {
        TestPlatform.Install();
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    /// <summary>HelloForms' MainForm.Designer.cs and MainForm.cs in a temp folder; returns the designer file.</summary>
    private string CopyHelloForms()
    {
        var source = Sample("HelloForms/MainForm.Designer.cs");
        var target = Path.Combine(_dir, Path.GetFileName(source));
        File.Copy(source, target);
        File.Copy(DesignerCodeReader.CompanionPath(source)!, DesignerCodeReader.CompanionPath(target)!);
        return target;
    }

    private static void Edit(string path, string oldText, string newText)
    {
        var text = File.ReadAllText(path);
        if (text.Contains("\r\n"))
        {
            oldText = oldText.Replace("\n", "\r\n");
            newText = newText.Replace("\n", "\r\n");
        }
        Assert.Contains(oldText, text);
        File.WriteAllText(path, text.Replace(oldText, newText));
    }

    private static DesignerOp Move(string id, int x, int y) => new() { Op = "setBounds", Id = id, X = x, Y = y };

    [Fact]
    public void CodeWrittenWhileTheDesignerIsOpenSurvivesTheNextEdit()
    {
        // The report: edit MainForm.cs, run (fine), move a control in the designer, run - the code edit is gone.
        var path = CopyHelloForms();
        var code = DesignerCodeReader.CompanionPath(path)!;
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;

        Edit(code, "clicks++;", "clicks += 2;");
        surface.Apply(new[] { Move("button1", 150, 90) });

        Assert.Contains("clicks += 2;", File.ReadAllText(code));
        Assert.DoesNotContain("clicks++;", File.ReadAllText(code));
        Assert.Contains("button1.Location = new Point(150, 90);", File.ReadAllText(path));

        // And again: every edit starts from the files as they are.
        Edit(code, "clicks += 2;", "clicks += 3;");
        surface.Apply(new[] { Move("label1", 20, 30) });
        Assert.Contains("clicks += 3;", File.ReadAllText(code));
        Assert.Contains("button1.Location = new Point(150, 90);", File.ReadAllText(path));
        Assert.Contains("label1.Location = new Point(20, 30);", File.ReadAllText(path));
    }

    [Fact]
    public void TheDesignerFileEditedAsTextIsTakenInBeforeTheNextEdit()
    {
        var path = CopyHelloForms();
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;
        surface.Apply(new[] { Move("label1", 20, 30) });

        Edit(path, "button1.Text = \"Click me\";", "button1.Text = \"Press me\";");
        surface.Apply(new[] { Move("button1", 150, 90) });

        var text = File.ReadAllText(path);
        Assert.Contains("button1.Text = \"Press me\";", text);
        Assert.Contains("button1.Location = new Point(150, 90);", text);
        Assert.Contains("label1.Location = new Point(20, 30);", text);
        Assert.Equal("Press me", ((Button)surface.Model.Find("button1")!.Instance).Text);

        // The history made of the old text is gone: undo goes back to the text edit, not before it.
        Assert.True(surface.Undo());
        Assert.Contains("button1.Text = \"Press me\";", File.ReadAllText(path));
        Assert.DoesNotContain("new Point(150, 90)", File.ReadAllText(path));
        Assert.False(surface.CanUndo);
    }

    [Fact]
    public void AHandlerStubIsAddedToTheCodeAsItIsNow()
    {
        var path = CopyHelloForms();
        var code = DesignerCodeReader.CompanionPath(path)!;
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;

        Edit(code, "private int clicks;", "private int clicks;\n        private string greeting = \"hi\";");
        surface.Apply(new[] { new DesignerOp { Op = "setEvent", Id = "label1", Event = "Click", Handler = "label1_Click" } });

        var text = File.ReadAllText(code);
        Assert.Contains("private string greeting = \"hi\";", text);
        Assert.Contains("private void label1_Click(object sender, EventArgs e)", text);
    }

    [Fact]
    public void UndoDoesNotPutBackCodeEditedSince()
    {
        var path = CopyHelloForms();
        var code = DesignerCodeReader.CompanionPath(path)!;
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;
        surface.Apply(new[] { new DesignerOp { Op = "setEvent", Id = "label1", Event = "Click", Handler = "label1_Click" } });

        // The new handler gets a body in the code editor; then the designer's edit is undone.
        Edit(code, "private void label1_Click(object sender, EventArgs e)\n        {\n", "private void label1_Click(object sender, EventArgs e)\n        {\n            clicks = 0;\n");
        Assert.True(surface.Undo());

        Assert.DoesNotContain("label1.Click += label1_Click;", File.ReadAllText(path));
        Assert.Contains("clicks = 0;", File.ReadAllText(code)); // the code is the user's: kept, stub and all
        Assert.True(surface.Redo());
        Assert.Contains("label1.Click += label1_Click;", File.ReadAllText(path));
        Assert.Contains("clicks = 0;", File.ReadAllText(code));
    }

    [Fact]
    public void UndoRightAfterAnEditStillRemovesTheStubItAdded()
    {
        var path = CopyHelloForms();
        var code = DesignerCodeReader.CompanionPath(path)!;
        var original = File.ReadAllText(code);
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;
        surface.Apply(new[] { new DesignerOp { Op = "setEvent", Id = "label1", Event = "Click", Handler = "label1_Click" } });
        Assert.True(surface.Undo());
        Assert.Equal(original, File.ReadAllText(code));
    }

    [Fact]
    public void ADesignerFileThatNoLongerReadsIsNotOverwritten()
    {
        var path = CopyHelloForms();
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;

        Edit(path, "button1.TabIndex = 0;", "button1.TabIndex = ;");
        var broken = File.ReadAllText(path);
        Assert.Throws<DesignerCodeException>(() => surface.Apply(new[] { Move("label1", 20, 30) }));
        Assert.Equal(broken, File.ReadAllText(path));
        Assert.Throws<DesignerCodeException>(() => surface.Apply(new[] { Move("label1", 20, 30) }));
        Assert.Equal(broken, File.ReadAllText(path));

        // Fixed in the editor: the designer goes on from the fixed file.
        Edit(path, "button1.TabIndex = ;", "button1.TabIndex = 0;");
        surface.Apply(new[] { Move("label1", 20, 30) });
        Assert.Contains("label1.Location = new Point(20, 30);", File.ReadAllText(path));
    }

    [Fact]
    public void SavingWritesOnlyWhatTheDesignerChanged()
    {
        var path = CopyHelloForms();
        var code = DesignerCodeReader.CompanionPath(path)!;
        using var surface = DesignSurface.Open(path);
        surface.AutoSave = true;
        surface.Apply(new[] { Move("label1", 20, 30) });
        Edit(code, "clicks++;", "clicks += 2;");
        Edit(path, "button1.Text = \"Click me\";", "button1.Text = \"Press me\";");
        surface.Save();
        Assert.Contains("clicks += 2;", File.ReadAllText(code));
        Assert.Contains("button1.Text = \"Press me\";", File.ReadAllText(path));
    }

    [Fact]
    public void ReloadingTheLibrariesAfterABuildDoesNotWriteTheFormBack()
    {
        // A build (dotnet run) changes bin/ and obj/; the extension then reloads the project's libraries into
        // the host, which reopens the form. The code saved just before the build must stay as it is.
        var path = CopyHelloForms();
        var code = DesignerCodeReader.CompanionPath(path)!;
        using var protocol = new DesignerProtocol();
        JsonObject Call(object request) => JsonNode.Parse(protocol.Handle(JsonSerializer.Serialize(request)))!.AsObject();

        Assert.Null(Call(new { id = 1, method = "open", @params = new { path } })["error"]);
        Assert.Null(Call(new { id = 2, method = "apply", @params = new { ops = new object[] { new { op = "setBounds", id = "button1", x = 150, y = 90 } } } })["error"]);
        Edit(code, "clicks++;", "clicks += 2;");
        Edit(path, "button1.Text = \"Click me\";", "button1.Text = \"Press me\";");

        Assert.Null(Call(new { id = 3, method = "libraries", @params = new { assemblies = Array.Empty<string>() } })["error"]);
        Assert.Contains("clicks += 2;", File.ReadAllText(code));
        Assert.Contains("button1.Text = \"Press me\";", File.ReadAllText(path));

        Assert.Null(Call(new { id = 4, method = "apply", @params = new { ops = new object[] { new { op = "setBounds", id = "label1", x = 20, y = 30 } } } })["error"]);
        Assert.Contains("clicks += 2;", File.ReadAllText(code));
        var text = File.ReadAllText(path);
        Assert.Contains("button1.Text = \"Press me\";", text);
        Assert.Contains("button1.Location = new Point(150, 90);", text);
        Assert.Contains("label1.Location = new Point(20, 30);", text);
    }
}
