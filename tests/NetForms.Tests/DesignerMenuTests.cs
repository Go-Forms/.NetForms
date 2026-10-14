using System.ComponentModel;
using System.Text.Json.Nodes;
using NetForms.Design;
using Xunit;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Menus open on the design surface (decision 171): a menu item's drop-down shows under it (a sub-menu to the right),
/// a ContextMenuStrip at the top left of the form, and every item in them is on the canvas - so an item however
/// deep can be selected and given a handler. Before, only the items of a strip on the form were reachable.
/// </summary>
public sealed class DesignerMenuTests
{
    public DesignerMenuTests() => TestPlatform.Install();

    /// <summary>HelloForms with a menu: File → Open, Recent → (One, Two), Exit; and a ContextMenuStrip with Copy, Paste.</summary>
    private static (DesignSurface Surface, Dictionary<string, string> Ids) WithMenus()
    {
        var path = Sample("HelloForms/MainForm.Designer.cs");
        var s = DesignSurface.Load(File.ReadAllText(path), File.ReadAllText(Path.ChangeExtension(Path.ChangeExtension(path, null), ".cs")));
        s.Apply(new[] { new DesignerOp { Op = "add", Type = "System.Windows.Forms.MenuStrip", Parent = "" }, new DesignerOp { Op = "add", Type = "System.Windows.Forms.ContextMenuStrip" } });
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "menuStrip1", Prop = "Items", Values = new[] { "File" }, Ids = new[] { "" } } });
        var ids = new Dictionary<string, string> { ["File"] = TextToId(s, "File") };
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = ids["File"], Prop = "DropDownItems", Values = new[] { "Open", "Recent", "Exit" }, Ids = new[] { "", "", "" } } });
        foreach (var t in new[] { "Open", "Recent", "Exit" }) ids[t] = TextToId(s, t);
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = ids["Recent"], Prop = "DropDownItems", Values = new[] { "One", "Two" }, Ids = new[] { "", "" } } });
        s.Apply(new[] { new DesignerOp { Op = "setItems", Id = "contextMenuStrip1", Prop = "Items", Values = new[] { "Copy", "Paste" }, Ids = new[] { "", "" } } });
        foreach (var t in new[] { "One", "Two", "Copy", "Paste" }) ids[t] = TextToId(s, t);
        return (s, ids);
    }

    private static string TextToId(DesignSurface s, string text) =>
        s.Model.Components.Single(c => c.Instance is ToolStripItem { Text: var t } && t == text).Name!;

    [Fact]
    public void NoMenuIsOpenAtFirstAndOnlyTheStripsItemsAreOnTheCanvas()
    {
        var (s, ids) = WithMenus();
        using var _ = s;
        var view = s.Render();
        Assert.Null(view.DropDowns);
        Assert.Contains(view.Items, i => i.Id == ids["File"] && i.Kind == "item" && i.HasDropDown);
        Assert.DoesNotContain(view.Items, i => i.Id == ids["Open"]);
    }

    [Fact]
    public void AnOpenMenuItemShowsItsItemsUnderIt()
    {
        var (s, ids) = WithMenus();
        using var _ = s;
        s.OpenMenu = ids["File"];
        var view = s.Render();
        var file = view.Items.First(i => i.Id == ids["File"]);
        var menu = Assert.Single(view.DropDowns!);
        Assert.Equal(ids["File"], menu.Owner);
        Assert.Equal(file.X, menu.X);
        Assert.Equal(file.Y + file.H, menu.Y);
        Assert.True(menu.W > 20 && menu.H > 40);
        Assert.NotEmpty(menu.Png);

        var items = view.Items.Where(i => i.Parent == ids["File"]).ToList();
        Assert.Equal(new[] { ids["Open"], ids["Recent"], ids["Exit"] }, items.Select(i => i.Id));
        Assert.All(items, i => Assert.True(i.X >= menu.X && i.Y >= menu.Y && i.Y + i.H <= menu.Y + menu.H));
        Assert.True(items[1].HasDropDown);
        // The drop-down's own area comes before its items, so a click on its margin selects File.
        Assert.True(view.Items.FindIndex(i => i.Kind == "dropdown") < view.Items.FindIndex(i => i.Id == ids["Open"]));
    }

    [Fact]
    public void ASubMenuOpensBesideItsMenuAndALeafKeepsItsMenuOpen()
    {
        var (s, ids) = WithMenus();
        using var _ = s;
        s.OpenMenu = ids["One"];
        var view = s.Render();
        Assert.Equal(new[] { ids["File"], ids["Recent"] }, view.DropDowns!.Select(d => d.Owner));
        var (outer, inner) = (view.DropDowns![0], view.DropDowns[1]);
        var recent = view.Items.Single(i => i.Id == ids["Recent"] && i.Kind == "item");
        Assert.Equal(outer.X + outer.W, inner.X);
        Assert.Equal(recent.Y, inner.Y);
        Assert.Contains(view.Items, i => i.Id == ids["Two"] && i.Parent == ids["Recent"]);

        s.OpenMenu = ids["Exit"];
        Assert.Equal(new[] { ids["File"] }, s.Render().DropDowns!.Select(d => d.Owner));
    }

    [Fact]
    public void AContextMenuStripOpensAtTheTopLeftOfTheForm()
    {
        var (s, ids) = WithMenus();
        using var _ = s;
        s.OpenMenu = "contextMenuStrip1";
        var view = s.Render();
        var menu = Assert.Single(view.DropDowns!);
        Assert.Equal(("contextMenuStrip1", 0, 0), (menu.Owner, menu.X, menu.Y));
        Assert.Equal(new[] { ids["Copy"], ids["Paste"] }, view.Items.Where(i => i.Parent == "contextMenuStrip1").Select(i => i.Id));
        // Still in the tray.
        Assert.Contains(view.Tray, t => t.Id == "contextMenuStrip1");
    }

    [Fact]
    public void ADeepItemGetsItsHandlerAndPropertiesAndTheMenuStaysOpen()
    {
        var (s, ids) = WithMenus();
        using var _ = s;
        s.OpenMenu = ids["Two"];
        s.Apply(new[]
        {
            new DesignerOp { Op = "setEvent", Id = ids["Two"], Event = "Click", Handler = "two_Click" },
            new DesignerOp { Op = "setProp", Id = ids["Two"], Prop = "ShortcutKeys", Value = TypeDescriptor.GetConverter(typeof(Keys)).ConvertToInvariantString(Keys.Control | Keys.T) },
            new DesignerOp { Op = "setEvent", Id = ids["Paste"], Event = "Click", Handler = "paste_Click" },
        });
        var source = s.Source.Replace("this.", "");
        Assert.Contains($"{ids["Two"]}.Click += two_Click;", source);
        Assert.Contains($"{ids["Two"]}.ShortcutKeys = Keys.Control | Keys.T;", source);
        Assert.Contains($"{ids["Paste"]}.Click += paste_Click;", source);
        Assert.Contains("private void two_Click(object sender, EventArgs e)", s.CompanionSource);
        // After the edit (the form read again) the same menus are open.
        Assert.Equal(new[] { ids["File"], ids["Recent"] }, s.Render().DropDowns!.Select(d => d.Owner));
    }

    [Fact]
    public void PaintingAMenuIsNotAnEdit()
    {
        var (s, _) = WithMenus();
        using var __ = s;
        static string SizeLine(string source) => source.Split('\n').FirstOrDefault(l => l.Contains("contextMenuStrip1.Size")) ?? "";
        var before = SizeLine(s.Source);
        s.OpenMenu = "contextMenuStrip1";
        s.Render();
        s.Apply(new[] { new DesignerOp { Op = "setProp", Id = "label1", Prop = "Text", Value = "Hello" } });
        Assert.Equal(before, SizeLine(s.Source));
    }

    [Fact]
    public void TheProtocolOpensAndClosesMenus()
    {
        var path = Path.Combine(Path.GetTempPath(), "netforms-menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            var (s, ids) = WithMenus();
            var designer = Path.Combine(path, "MainForm.Designer.cs");
            File.WriteAllText(designer, s.Source);
            File.WriteAllText(Path.Combine(path, "MainForm.cs"), s.CompanionSource!);
            s.Dispose();
            using var protocol = new DesignerProtocol();
            Response(protocol.Handle(new JsonObject { ["id"] = 1, ["method"] = "open", ["params"] = new JsonObject { ["path"] = designer } }.ToJsonString()));
            var open = Response(protocol.Handle(new JsonObject { ["id"] = 2, ["method"] = "menu", ["params"] = new JsonObject { ["id"] = ids["File"] } }.ToJsonString()));
            Assert.Equal(ids["File"], (string?)open["openMenu"]);
            Assert.Single(open["dropDowns"]!.AsArray());
            var closed = Response(protocol.Handle(new JsonObject { ["id"] = 3, ["method"] = "menu", ["params"] = new JsonObject { ["id"] = "" } }.ToJsonString()));
            Assert.Null(closed["dropDowns"]);
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    private static JsonObject Response(string line)
    {
        var o = JsonNode.Parse(line)!.AsObject();
        Assert.Null(o["error"]);
        return o["result"]!.AsObject();
    }
}
