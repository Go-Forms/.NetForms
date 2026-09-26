<p align="center"><img src="eng/branding/icon-128.png" width="96" alt="NetForms logo"></p>

<h1 align="center">NetForms</h1>

<p align="center"><b>Windows Forms for Windows and Linux.</b><br>
<a href="https://go-forms.github.io/.NetForms/"><b>Website</b></a> ·
<a href="https://go-forms.github.io/.NetForms/docs/">Documentation</a> ·
<a href="https://go-forms.github.io/.NetForms/docs/install.html">Install</a> ·
<a href="README.ru.md">По-русски</a></p>

<p align="center">
<a href="https://github.com/Go-Forms/.NetForms/actions/workflows/ci.yml"><img src="https://github.com/Go-Forms/.NetForms/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
<a href="https://www.nuget.org/packages/NetForms"><img src="https://img.shields.io/nuget/vpre/NetForms?label=NuGet" alt="NuGet"></a>
<a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT"></a>
</p>

The same `System.Windows.Forms` API, the same `Program.cs` / `MainForm.cs` / `MainForm.Designer.cs`, the same
designer workflow — on .NET 10, painted identically on both systems by SkiaSharp, with Avalonia as the
platform layer.

```diff
-    <TargetFramework>net8.0-windows</TargetFramework>
-    <UseWindowsForms>true</UseWindowsForms>
+    <TargetFramework>net10.0</TargetFramework>
+    <PackageReference Include="NetForms" Version="0.1.0-preview.7" />
```

That is the whole migration for most projects — the code does not change. `netforms-convert` does it for
you, .NET Framework projects included.

| The control gallery — the same pixels on Windows and Linux | A Visual Studio WinForms project after `netforms-convert`, running on Linux |
|---|---|
| ![Gallery](docs/images/gallery-controls.png) | ![HelloForms on Linux, from the NuGet package](docs/images/hello-linux.png) |

## Get it

Everything comes from NuGet.

```sh
# a new app
dotnet new install NetForms.Templates::0.1.0-preview.7
dotnet new netforms -n MyApp && cd MyApp && dotnet run

# an existing WinForms app
dotnet tool install -g NetForms.Convert --prerelease
netforms-convert MyApp.csproj --apply
```

Needs the .NET 10 SDK; on Linux, a few system libraries. Step by step for Windows, Ubuntu, Debian, Fedora,
Astra Linux, RED OS and ALT: **[Install and set up](https://go-forms.github.io/.NetForms/docs/install.html)**.

Visual designer: **NetForms Designer** for VS Code (Marketplace and Open VSX) — see [designer/](designer/README.md).

![The designer in VS Code](docs/images/designer.png)

## Documentation

On the website: **<https://go-forms.github.io/.NetForms/docs/>** (English and Russian). The API reference is
Microsoft's — <https://learn.microsoft.com/dotnet/desktop/winforms/> — since NetForms is a copy of it. NetForms'
own pages (the same Markdown is in [docs/](docs/README.md)):

- [Install and set up](https://go-forms.github.io/.NetForms/docs/install.html)
- [Getting started](https://go-forms.github.io/.NetForms/docs/getting-started.html)
- [Moving a WinForms project](https://go-forms.github.io/.NetForms/docs/migrating.html)
- [Compatibility guide](https://go-forms.github.io/.NetForms/docs/compatibility.html) — .NET versions, OSes, the state of every control, what is missing
- [API coverage](https://go-forms.github.io/.NetForms/docs/api/) — generated, type by type
- [The visual designer](https://go-forms.github.io/.NetForms/docs/designer.html)
- [Control libraries in the designer](https://go-forms.github.io/.NetForms/docs/control-libraries.html) — your own controls, NuGet packages, `.dll`s in the toolbox

## Status

Preview. Measured, not guessed:

- **API:** 747 of the 1254 public types of `System.Windows.Forms` + `System.Drawing.Common` complete,
  144 partial, 363 missing ([coverage](docs/api/README.md)).
- **Behaviour:** 504/504 tests; layout, event order, text metrics and designer output diffed against the real
  WinForms on Windows and rendered offscreen on both OSes.
- **Real projects:** 7/7 customer .NET Framework projects and 28/45 open-source WinForms projects convert
  and build without manual edits.
- **Designer:** your project's own controls and control libraries built for NetForms (NuGet, `.dll`, other
  projects) in the toolbox and live on the canvas ([control libraries](docs/control-libraries.md)).
- Not yet: screen readers (the accessibility model is there), `WebBrowser`, dark theme, third-party control
  packages built for the real WinForms.

## Repository

```
src/NetForms                  System.Windows.Forms: Control, Form, Application, the controls
src/NetForms.Drawing          System.Drawing on SkiaSharp
src/NetForms.Platform*        platform layer (Avalonia 12)
src/NetForms.Design*          designer host: reads/writes InitializeComponent with Roslyn
designer/                     VS Code extension
templates/                    dotnet new templates (NuGet package NetForms.Templates)
tools/NetForms.Convert        WinForms → NetForms converter (netforms-convert)
tests/                        golden rendering, behaviour, diff tests against real WinForms, corpus
site/                         the website; docs/ is rendered into it
docs/PLAN.md                  architecture, roadmap, decision log (Russian)
```

Build and test: `dotnet test NetForms.slnx`; extension: `cd designer && npm ci && npm test`; website:
`cd site && npm ci && npm run build`. Releases go out when `<Version>` changes on `main`:
[docs/RELEASING.md](docs/RELEASING.md).

MIT licensed — see [LICENSE](LICENSE) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
