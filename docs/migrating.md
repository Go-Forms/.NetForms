# Moving a WinForms project to NetForms

A WinForms project does not build on Linux:

```text
$ dotnet build
error NETSDK1100: To build a project targeting Windows on this operating system,
set the EnableWindowsTargeting property to true.
```

and if you force it (`-p:EnableWindowsTargeting=true`), it builds and then does not start:

```text
$ dotnet run -p:EnableWindowsTargeting=true
You must install or update .NET to run this application.
Framework: 'Microsoft.WindowsDesktop.App', version '10.0.0' (x64)
No frameworks were found.
```

`Microsoft.WindowsDesktop.App` — the real WinForms — exists only for Windows. NetForms replaces it
with a NuGet package. In most projects the only file that changes is the `.csproj`.

## The converter

```sh
dotnet tool install -g NetForms.Convert --prerelease

netforms-convert MyApp.csproj              # analyse: what would change, what will not work
netforms-convert MyApp.csproj --apply      # rewrite the project
dotnet run
```

In VS Code: right-click the `.csproj` → **NetForms: Convert WinForms Project…** — the same analysis as a
report with clickable file/line links, then the same rewrite.

What it does to an SDK-style project:

```diff
 <Project Sdk="Microsoft.NET.Sdk">

   <PropertyGroup>
     <OutputType>WinExe</OutputType>
-    <TargetFramework>net8.0-windows</TargetFramework>
+    <TargetFramework>net10.0</TargetFramework>
     <Nullable>enable</Nullable>
-    <UseWindowsForms>true</UseWindowsForms>
     <ImplicitUsings>enable</ImplicitUsings>
   </PropertyGroup>

+  <ItemGroup>
+    <PackageReference Include="NetForms" Version="0.1.0-preview.3" />
+    <!-- What <UseWindowsForms>true</UseWindowsForms> added: the WinForms namespaces as implicit usings. -->
+    <Using Include="System.Drawing" />
+    <Using Include="System.Windows.Forms" />
+  </ItemGroup>
+
 </Project>
```

The original is saved as `MyApp.csproj.winforms.bak`. Running it again changes nothing.

Options:

| Option | |
|---|---|
| `--apply` | Write the changes (without it, only the report). |
| `--target cross` (default) | `net10.0`: builds and runs on Windows and Linux. |
| `--target windows` | `net10.0-windows`: Windows only, but with NetForms instead of the Windows Desktop runtime. Windows-only API is then reported for information, not as a warning. |
| `--framework-path <dir>` | For work on NetForms itself only: reference a NetForms checkout (`ProjectReference`) instead of the package. |
| `--json` | The report as JSON. |

The argument may be a project, a solution (`.sln`, `.slnx`) or a folder.

### .NET Framework projects

Old-format projects (`<Project ToolsVersion="15.0" …>`, `packages.config`, a list of `<Compile Include>`)
are rewritten to SDK-style by the converter itself:

- file list → globs, with `Remove` for files on disk the old project did not compile, `Update` for
  `DependentUpon`/generators;
- `OutputType`, `RootNamespace`, `AssemblyName`, `ApplicationIcon`, `StartupObject`, `LangVersion`,
  `AllowUnsafeBlocks`, `.snk` signing are carried over; `Properties\AssemblyInfo.cs` is kept
  (`GenerateAssemblyInfo=false`);
- framework assembly references are dropped (implicit in .NET), `HintPath` references kept,
  `packages.config` → `PackageReference`, project references kept;
- files the program read from a committed `bin\Debug` get `CopyToOutputDirectory`;
- libraries of the same solution without WinForms become `net10.0` class libraries.

Source files are touched only where the change cannot alter meaning: a `using` of a namespace .NET does
not have (`System.Runtime.Remoting.Messaging`) is removed, a type that moved to a NuGet package in .NET
(`System.Data.SqlClient`, `System.IO.Ports`) gets its `PackageReference`, `.resx` file references are fixed
to the real letter case.

## Doing it by hand

1. `TargetFramework`: `net10.0` (or `net10.0-windows` for Windows only).
2. Remove `<UseWindowsForms>true</UseWindowsForms>` (and `<EnableWindowsTargeting>` if you added it).
3. Add `<PackageReference Include="NetForms" Version="0.1.0-preview.3" />`.
4. If `ImplicitUsings` is on, add `<Using Include="System.Drawing" />` and `<Using Include="System.Windows.Forms" />`.

## What the report may tell you

| Category in the report | Meaning | What to do |
|---|---|---|
| `Missing in NetForms (type)` / `(member)` | Compiles against WinForms, not against NetForms yet. | Check [API coverage](api/README.md); replace it, or open an issue — the corpus decides what comes next. |
| `Compile error` | Would not compile against WinForms either, or a reference is missing. | Read the message; usually a package the old project got from the GAC. |
| `Windows API`, `Windows only` | P/Invoke into `user32`/`gdi32`/…; `Microsoft.Win32.Registry` or another Windows-only API or package. | Keep it behind `OperatingSystem.IsWindows()`, or replace it with managed API (`Properties.Settings` instead of the registry). |
| `Win32 messages` | A `WndProc` or `CreateParams` override. | Compiles, but is never called — see [Compatibility § 5](compatibility.md#5-windows-only-api). |
| `Windows path` | `@"C:\data"` or `@"\img\x.png"` passed to `File.*`. | `Path.Combine`. (NetForms' own `Image.FromFile` accepts such paths; that case is only *info*.) |
| `Resources` | A `.resx` entry only `BinaryFormatter` can read. | Re-add the resource as a file (`ResXFileRef`) or a string. |
| `COM`, `WPF` | COM reference, `UseWPF`. | Not portable. Keep a Windows-only build (`--target windows`) or replace the component. |
| `Project format`, `Signing`, `Output files` | Something of an old project that is left out (a missing file, a PFX key, a custom import). | Read the line; the build works without it or tells you what to add back. |
| `form FAIL MainForm.Designer.cs` | The designer cannot open that form. | It still compiles and runs; the message says why. |

## Troubleshooting

| Error | Cause |
|---|---|
| `NU1202: Package NetForms … is not compatible with net8.0` | The project still targets an older .NET. NetForms needs `net10.0`. |
| `NETSDK1100` | `UseWindowsForms` or a `-windows` TFM is still there. |
| `NETSDK1136` / a package brings `Microsoft.WindowsDesktop.App` | A dependency is built for the Windows Desktop runtime. Find a cross-platform version. |
| `CS0012: The type 'Control' is defined in an assembly that is not referenced … System.Windows.Forms, Version=4.0.0.0, PublicKeyToken=b77a5c561934e089` | A control library compiled against the real WinForms (ZedGraph, OxyPlot, ScottPlot, FastColoredTextBox…). Not supported yet: NetForms' `System.Windows.Forms` facade does not carry Microsoft's strong-name identity, so the compiler does not accept it in place of the real assembly. Use the library's source instead of the package, or wait for the facade to change (see the compatibility guide). |
| `DllNotFoundException: libSkiaSharp` / `libX11` on Linux | Missing system libraries — see [Compatibility § 1](compatibility.md#operating-systems). |
| The window opens but text looks different | No Segoe UI on Linux; a substitute font is used. Install the font or set `Font` explicitly. |
