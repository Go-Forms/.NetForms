<p align="center"><img src="eng/branding/icon-128.png" width="96" alt="NetForms logo"></p>

# NetForms

**Windows Forms for Windows and Linux.** The same `System.Windows.Forms` API, the same
`Program.cs` / `MainForm.cs` / `MainForm.Designer.cs`, the same designer workflow — on .NET 10,
painted identically on both systems by SkiaSharp, with Avalonia as the platform layer.

```diff
-    <TargetFramework>net8.0-windows</TargetFramework>
-    <UseWindowsForms>true</UseWindowsForms>
+    <TargetFramework>net10.0</TargetFramework>
+    <PackageReference Include="NetForms" Version="0.1.0-preview.1" />
```

That is the whole migration for most projects — the code does not change.
`netforms-convert` does it for you, .NET Framework projects included.

| The control gallery — the same pixels on Windows and Linux | A Visual Studio WinForms project after `netforms-convert`, running on Linux |
|---|---|
| ![Gallery](docs/images/gallery-controls.png) | ![HelloForms on Linux, from the NuGet package](docs/images/hello-linux.png) |

## Get it

```sh
# a new app
dotnet new install NetForms.Templates::0.1.0-preview.1
dotnet new netforms -n MyApp && cd MyApp && dotnet run

# an existing WinForms app
dotnet tool install -g NetForms.Convert --prerelease
netforms-convert MyApp.csproj --apply
```

Visual designer: **NetForms Designer** for VS Code (Marketplace and Open VSX) — see [designer/](designer/README.md).

![The designer in VS Code](docs/images/designer.png)

## Documentation

The API reference is Microsoft's: <https://learn.microsoft.com/dotnet/desktop/winforms/>.
NetForms' own pages are in [docs/](docs/README.md):

- [Getting started](docs/getting-started.md)
- [Moving a WinForms project](docs/migrating.md)
- [Compatibility guide](docs/compatibility.md) — .NET versions, OSes, status of every control, what is missing
- [API coverage](docs/api/README.md) — generated, type by type
- [The visual designer](docs/designer.md)

## Status

Preview. Measured, not guessed:

- **API:** 633 of the 1254 public types of `System.Windows.Forms` + `System.Drawing.Common` complete,
  164 partial, 457 missing ([coverage](docs/api/README.md)).
- **Behaviour:** 380/380 tests; layout, event order, text metrics and designer output diffed against the real WinForms on Windows (layout coordinates, event
  order, text metrics, designer serialization) and rendered offscreen on both OSes.
- **Real projects:** 7/7 customer .NET Framework projects and 27/45 open-source WinForms projects convert
  and build without manual edits.
- Not yet: printing, accessibility, drag-and-drop, `WebBrowser`, dark theme.

## Repository

```
src/NetForms                  System.Windows.Forms: Control, Form, Application, the controls
src/NetForms.Drawing          System.Drawing on SkiaSharp
src/NetForms.Platform*        platform layer (Avalonia 12)
src/NetForms.Design*          designer host: reads/writes InitializeComponent with Roslyn
designer/                     VS Code extension
templates/                    dotnet new templates
tools/NetForms.Convert        WinForms → NetForms converter (netforms-convert)
tests/                        golden rendering, behaviour, diff tests against real WinForms, corpus
site/                         project website (GitHub Pages)
docs/PLAN.md                  architecture, roadmap, decision log (Russian)
```

Build and test: `dotnet test NetForms.slnx`; extension: `cd designer && npm ci && npm test`.
Releasing: [docs/RELEASING.md](docs/RELEASING.md).

MIT licensed — see [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
