# NetForms — Windows Forms for Windows and Linux

NetForms is `System.Windows.Forms` and `System.Drawing` for .NET 10 that runs the same on Windows and
Linux. Same namespaces, same `Program.cs` → `Application.Run(new MainForm())`, same
`MainForm.cs` + `MainForm.Designer.cs` with `InitializeComponent()`. Your WinForms code compiles
against it without changes.

Controls are painted by NetForms itself with SkiaSharp. Avalonia 12 is used only to reach the OS
(windows, input, IME, clipboard, native dialogs, DPI). Avalonia types never appear in the API.

## Use it

Replace `UseWindowsForms` in your project file with a package reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>   <!-- was net8.0-windows -->
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="NetForms" Version="0.1.0-preview.2" />
    <Using Include="System.Drawing" />
    <Using Include="System.Windows.Forms" />
  </ItemGroup>
</Project>
```

Or let the converter do it and tell you what will not carry over:

```sh
dotnet tool install -g NetForms.Convert --prerelease
netforms-convert MyApp.csproj            # report only
netforms-convert MyApp.csproj --apply    # rewrite the project (the original is kept as .winforms.bak)
```

New project from a template:

```sh
dotnet new install NetForms.Templates::0.1.0-preview.2
dotnet new netforms -n MyApp
```

## Packages

| Package | What it is |
|---|---|
| `NetForms` | What an application references. Brings the rest. |
| `NetForms.Drawing` | `System.Drawing` on SkiaSharp. |
| `NetForms.Drawing.Common` | Type-forwarding `System.Drawing.Common` facade (resources and libraries that name it). |
| `NetForms.Platform`, `NetForms.Platform.Avalonia` | The platform layer. |
| `NetForms.Templates` | `dotnet new netforms`, `netforms-form`, `netforms-usercontrol`. |
| `NetForms.Convert` | `netforms-convert`, a .NET tool that moves a WinForms project to NetForms. |

## Requirements

- .NET 10 SDK. The target framework is `net10.0` (not `net10.0-windows`).
- Windows 10+ (x64, arm64) or Linux with X11 or XWayland (x64, arm64). macOS is not a goal of this release.
- .NET Framework projects (old `.csproj` format, `packages.config`) are rewritten to SDK style by
  `netforms-convert` itself.
- Third-party WinForms control packages from NuGet do not work yet (they compile against the real,
  strong-named `System.Windows.Forms`).

## Documentation

Website and documentation (English and Russian): <https://go-forms.github.io/.NetForms/>. Installing on
Windows and each Linux family: <https://go-forms.github.io/.NetForms/docs/install.html>.

The API is the WinForms API, documented by Microsoft at
<https://learn.microsoft.com/dotnet/desktop/winforms/> and
<https://learn.microsoft.com/dotnet/api/system.windows.forms>.
What NetForms implements, what it does not yet, and where it differs on purpose:
<https://go-forms.github.io/.NetForms/docs/compatibility.html>.

MIT licensed. Source, issues and the visual designer for VS Code: <https://github.com/Go-Forms/.NetForms>.
