# Getting started

NetForms is Windows Forms for .NET 10 on Windows and Linux. If you have written a WinForms application,
you already know how to write a NetForms one: the code is the same. This page covers what is different —
getting the tools and creating the project.

## 1. Install

- **.NET 10 SDK** — <https://dotnet.microsoft.com/download>. On Ubuntu 24.04+: `sudo apt install dotnet-sdk-10.0`.
- **Linux only**: a few system libraries (a desktop distribution already has them).
- Optional, for the visual designer: **VS Code** with the **NetForms Designer** extension
  (Marketplace / Open VSX: `netforms.netforms-designer`) and the C# extension.

Step by step for Windows and each Linux family: [Install and set up](install.md).

## 2. Create a project

From the command line:

```sh
dotnet new install NetForms.Templates::0.1.0-preview.4
dotnet new netforms -n HelloForms
cd HelloForms
dotnet run
```

Or in VS Code: **NetForms: Create New Project…**.

You get the three files Visual Studio creates for a WinForms app, and a project file that differs from
Visual Studio's in one place — the reference:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>HelloForms</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NetForms" Version="0.1.0-preview.4" />
  </ItemGroup>

  <ItemGroup>
    <!-- What <UseWindowsForms>true</UseWindowsForms> adds: the WinForms namespaces as implicit usings. -->
    <Using Include="System.Drawing" />
    <Using Include="System.Windows.Forms" />
  </ItemGroup>

</Project>
```

`Program.cs`:

```csharp
namespace HelloForms
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
```

Add forms and user controls with `dotnet new netforms-form -n SettingsForm` and
`dotnet new netforms-usercontrol -n ColorPicker` (inside the project folder, after the first
`dotnet build` or `dotnet restore`; in a subfolder pass the namespace: `--Namespace HelloForms.Views`),
or **NetForms: New Form…** in VS Code. VS Code installs the same template package from NuGet itself.

## 3. Write code as in WinForms

Everything in the [Microsoft WinForms documentation](https://learn.microsoft.com/dotnet/desktop/winforms/)
applies: controls, events, layout with `Anchor`/`Dock`, `TableLayoutPanel`, custom controls with
`OnPaint`, data binding to `DataGridView`, dialogs. For example, a custom control:

```csharp
public class Gauge : Control
{
    private int _value;

    public int Value
    {
        get => _value;
        set { _value = value; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.FillRectangle(Brushes.SteelBlue, 0, 0, Width * Value / 100, Height);
        TextRenderer.DrawText(e.Graphics, $"{Value}%", Font, ClientRectangle, ForeColor);
    }
}
```

Before relying on something rare, check [the compatibility guide](compatibility.md) and
[API coverage](api/README.md).

## 4. Design forms

Open `MainForm.Designer.cs` in VS Code with the extension installed — it opens as a canvas. See
[the designer](designer.md). The file it writes is what Visual Studio writes, so the same project can be
edited in Visual Studio on Windows.

## 5. Ship it

```sh
# framework-dependent: needs the .NET 10 runtime on the target machine
dotnet publish -c Release -r linux-x64 --self-contained false

# self-contained: everything in one folder, no .NET needed
dotnet publish -c Release -r linux-x64 --self-contained true
dotnet publish -c Release -r win-x64 --self-contained true
```

A build made on Windows runs on Linux and the other way round (framework-dependent, `-r` omitted):
SkiaSharp's native libraries for both are in the package.
