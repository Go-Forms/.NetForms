# Control libraries in the designer

The designer's toolbox is not limited to the controls of NetForms. It has your project's own controls, and the
controls of any library the project references — a NuGet package, a `.dll`, another project of the solution. On the
canvas they are real: their constructors run, they paint themselves, and the property grid and the Events tab show
their own properties and events.

This page is the how-to. The design behind it (in Russian) is in [designer-control-libraries.md](designer-control-libraries.md).

- [Your project's own controls](#your-projects-own-controls)
- [Adding a library](#adding-a-library)
- [What the check says](#what-the-check-says)
- [Removing a library, rescanning](#removing-a-library-rescanning)
- [Where it is kept: `.vscode/netforms.json`](#where-it-is-kept-vscodenetformsjson)
- [Writing a control library for NetForms](#writing-a-control-library-for-netforms)
- [Safety: Workspace Trust](#safety-workspace-trust)
- [Troubleshooting](#troubleshooting)

## Your project's own controls

Nothing to do. Write a control in your project —

```csharp
namespace Shop.Controls;

public class StarRating : Control
{
    [DefaultValue(3), Category("Appearance"), Description("How many stars are lit.")]
    public int Stars { get; set; } = 3;

    protected override void OnPaint(PaintEventArgs e)
    {
        for (int i = 0; i < Stars; i++) e.Graphics.FillEllipse(Brushes.Gold, i * 20, 0, 18, 18);
    }
}
```

— build the project (`dotnet build`, F5, **NetForms: Rescan Toolbox**, or **↻** at the top of the toolbox), and it
appears in the **Shop Components** group at the top of the toolbox, as in Visual Studio. The group lists every public
control, user control and component of the project; forms are not listed (they are designed, not dropped).

The designer works with the **last successful build**. Change the control and build again: the open forms pick up the new
version by themselves, no restart. A control written since the last build is shown as a dashed placeholder with its type
name until the next build; nothing in the form is lost meanwhile.

A form can also derive from a form of the project (`public partial class OrderForm : BaseForm`): the canvas shows what
the base form puts on it, as Visual Studio does.

**A folder of its own group.** When the project has many controls, give a folder its own toolbox group: **NetForms: Add
Control Library… → Folder of this project…**, pick `Controls/Charts`, name the group. The controls of that folder's
namespace (`Shop.Controls.Charts`) move from *Shop Components* into the new group.

## Adding a library

**NetForms: Add Control Library…** — from the Command Palette, the **+ Control Library…** button in the toolbox, or the
context menu of a `.csproj` in the Explorer. Nothing is ever installed on its own when a form opens or a control is
dropped: a library comes in only through this command.

| Source | What you do | What happens to the project |
|---|---|---|
| **NuGet package…** | Type a name — nuget.org is searched as you type — then pick a version (the newest stable one is first). | The package is downloaded to a temporary folder and [checked](#what-the-check-says) first. Then `dotnet add package`. |
| **.dll file…** | Pick the file. | A `<Reference>` with a `<HintPath>` goes into the `.csproj`. A file outside the project is offered to be **copied into `libs/`** (with its `.pdb` and `.xml`): say yes, or the project will not build on another machine. |
| **.nupkg or local feed folder…** | Pick a `.nupkg`, or a folder of them and then the package. | Your own and your company's packages. The folder becomes a package source in the project's `nuget.config` (created, with nuget.org, if there is none), then as a NuGet package. The designer asks once more: the package is not from nuget.org. |
| **Folder of this project…** | Pick a folder, name the group. | Nothing: see above. |
| **Another project…** | Pick a `.csproj` of the workspace (or browse). | `dotnet add reference`. Its controls follow its builds: rebuild that project alone, and the designer takes its fresh output. |
| **NuGet package already in the project…** | Tick the packages to show. | Nothing: the packages come from `obj/project.assets.json` (what `dotnet restore` resolved), including those brought in by other packages. |

After the check the designer lists the controls and components it found — with their `[ToolboxBitmap]` icons — all
ticked. Untick what the toolbox should not show. Then the project is built and the library's group appears in the toolbox
of every open designer of the project.

Libraries you added before, in any project, are offered first (**Recent**): one pick, and the package or `.dll` is in the
new project and its toolbox.

Dependencies, versions, native parts, conflicts: all of that is MSBuild's and NuGet's work, exactly as when the
application runs — the designer loads the project's build output with its `.deps.json`.

## What the check says

Before a library is added, its assemblies are read **without running any of its code** (metadata only):

| The library is | The designer |
|---|---|
| built for NetForms (references the `NetForms` package) | ✅ adds it |
| built for the real System.Windows.Forms | ⚠️ asks: the designer can show its controls (NetForms' facade forwards `System.Windows.Forms`), but a project that uses it may not compile — `CS0012`, the strong name of `System.Windows.Forms` — and most likely will not build on Linux. See [Compatibility § third-party packages](compatibility.md#2-which-projects-move-over). |
| a package whose native parts are only in `runtimes/win-*` | ⚠️ asks: its controls will not work on Linux |
| a package only for `net*-windows` | ⚠️ asks: the project needs a Windows target framework to reference it |
| built for the .NET Framework (`net48`…), or for a .NET newer than 10, or not a .NET assembly | ❌ not added, with the reason |

For a package the check takes the `lib/` folder a `net10.0` project would take: `net10.0`, else the nearest older
`netX.0`, `netcoreapp3.1`, `netstandard2.1`, `netstandard2.0`.

A type goes to the toolbox as it would in Visual Studio: public, not abstract, not generic, a component (`IComponent`)
with a public parameterless constructor, not marked `[ToolboxItem(false)]` or `[DesignTimeVisible(false)]` (on it or a
base class), and not a form. Components without a place on the form (like `Timer`) go to the tray.

## Removing a library, rescanning

**NetForms: Remove Control Library** — pick the library, then **From the toolbox only** (the project keeps the reference
and still builds with it) or **From the toolbox and the project** (`dotnet remove package` / `dotnet remove reference`, or
the `<Reference>` taken out of the `.csproj`; a `.dll` in `libs/` stays on disk). Forms that use its controls keep them,
as placeholders that keep every line of their code.

**NetForms: Rescan Toolbox** (**↻** in the toolbox) builds the project and reads its controls and libraries again. The
designer also does it by itself after every build it sees, and after the `.csproj` or `.vscode/netforms.json` changes.

## Where it is kept: `.vscode/netforms.json`

References live where MSBuild reads them — the `.csproj`. What the toolbox shows lives in `.vscode/netforms.json` of the
workspace folder, per project. **Commit it**: colleagues get the same toolbox.

```json
{
	"projects": {
		"src/Shop/Shop.csproj": {
			"libraries": [
				{
					"id": "package:scottplot.winforms",
					"name": "ScottPlot.WinForms",
					"source": "package",
					"package": "ScottPlot.WinForms",
					"version": "5.0.47",
					"assemblies": ["ScottPlot.WinForms.dll"],
					"types": ["ScottPlot.WinForms.FormsPlot"]
				},
				{
					"id": "folder:controls/charts",
					"name": "Charts",
					"source": "folder",
					"path": "Controls/Charts",
					"namespace": "Shop.Controls.Charts",
					"assemblies": []
				}
			]
		}
	}
}
```

| Field | |
|---|---|
| `name` | The toolbox group. Edit it freely. |
| `source` | `package`, `dll`, `feed`, `folder`, `projectReference` or `projectPackage` (a package already in the project). |
| `assemblies` | The library's assembly files, looked for in the project's build output (a package of a class library, which is not copied there, is taken from the NuGet cache). |
| `types` | Only these types are shown; without it, all the library offers. |
| `projectComponents: false` | (on the project, beside `libraries`) hides the automatic *<Project> Components* group. |

A library whose reference has left the `.csproj` (removed by hand, a branch switched) is simply not shown — it does not
break the designer.

## Writing a control library for NetForms

A control library is a class library that references NetForms:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NetForms" Version="0.1.0-preview.7" />
    <Using Include="System.Drawing" />
    <Using Include="System.Windows.Forms" />
  </ItemGroup>
  <ItemGroup>
    <!-- The toolbox icon: 16×16, the bottom-left pixel's colour is transparent. -->
    <EmbeddedResource Include="Gauge.bmp" LogicalName="MyControls.Gauge.bmp" />
  </ItemGroup>
</Project>
```

What the designer reads is the same `System.ComponentModel` metadata Visual Studio reads:

| Attribute | In the designer |
|---|---|
| `[ToolboxBitmap(typeof(Gauge), "Gauge.bmp")]` | The toolbox icon (a resource `Namespace.Gauge.bmp` is found without the attribute too). |
| `[ToolboxItem(false)]`, `[DesignTimeVisible(false)]` | Not in the toolbox. |
| `[DefaultValue]`, `ShouldSerializeX()`/`ResetX()` | What is written to `*.Designer.cs`; bold in the property grid when it differs. |
| `[Category]`, `[Description]`, `[Browsable(false)]` | The property grid. |
| `[DefaultEvent]` | What a double-click on the control wires. |
| `[TypeConverter]` | How a value is shown and typed in the property grid. |
| `[DesignerSerializationVisibility(Content)]` | A collection or object written as its content. |

`Site.DesignMode` is `true` on the canvas, as on a Visual Studio design surface: skip timers, network and databases there.
Custom designers (`[Designer]`, smart tags) and `UITypeEditor`s are not used yet — the property grid edits the value as
text through its `TypeConverter`.

Pack it (`dotnet pack`) and add the `.nupkg` with **.nupkg or local feed folder…**, or publish it to nuget.org.

## Safety: Workspace Trust

A library's code runs in the designer host process — its constructors, property setters and `OnPaint` — as it does in
Visual Studio. So:

- In a folder VS Code does not trust (**Restricted Mode**) the toolbox lists the project's controls from their metadata
  only, greyed out: none of their code is loaded, and **Add Control Library** is not available. Trust the folder and they
  come alive.
- A package that is not from nuget.org (a `.nupkg`, a local feed) is confirmed once more before it is added.
- A control whose constructor or `OnPaint` throws is drawn as a **red cross** with the exception in it (as WinForms draws a
  control whose painting failed). The form still opens, and every line of that control's code is kept.
- The host runs in its own process: whatever a control does, VS Code does not go down with it.
- The assemblies are loaded from a copy, so your next `dotnet build` is never blocked by a locked `.dll`.

## Troubleshooting

| What you see | Why, and what to do |
|---|---|
| *Build the project to see its own controls here* in the toolbox | The project has not been built yet (no `bin/…/<Project>.dll`). Press **↻** or run `dotnet build`. |
| A control on the canvas is a dashed box with its type name | Its type is not in the last build (written since, or the build failed), or its library is not referenced. Build; check the NetForms output for the compiler's errors. |
| A red cross with an exception | The control's constructor or `OnPaint` threw on the canvas. Guard design-time code with `if (DesignMode) return;` or `Site?.DesignMode`. |
| The library's items are greyed out | Restricted Mode (trust the folder), or the loaded build is older than the library (build again). Hover an item for the reason. |
| *… cannot be used by the designer* when adding | See [What the check says](#what-the-check-says): usually a .NET Framework-only library. |
| The nuget.org search shows nothing | nuget.org search is not reachable (a proxy, a closed network): type the exact package id and press Enter; versions come from `api.nuget.org`. In a closed network use a local feed. |
| The build fails after adding a WinForms package | `CS0012` about `System.Windows.Forms`: the package was built for the real WinForms — see [Compatibility](compatibility.md). Remove it with **Remove Control Library → From the toolbox and the project**. |
