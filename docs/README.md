# NetForms documentation

NetForms is a copy of Windows Forms. **The API reference is Microsoft's** — every page of
[learn.microsoft.com/dotnet/desktop/winforms](https://learn.microsoft.com/dotnet/desktop/winforms/) and
[the `System.Windows.Forms` API reference](https://learn.microsoft.com/dotnet/api/system.windows.forms)
applies, in the same namespaces, with the same names and defaults. These pages describe only what is
NetForms' own.

| Page | |
|---|---|
| [Install and set up](install.md) | The .NET SDK on Windows and each Linux family, system libraries, templates and the converter from NuGet, VS Code and the designer, closed networks, shipping to Linux. |
| [Getting started](getting-started.md) | Install, `dotnet new netforms`, first form, publish for Windows and Linux. |
| [Moving a WinForms project](migrating.md) | Why WinForms does not run on Linux, `netforms-convert`, what the report means, troubleshooting. |
| [Compatibility guide](compatibility.md) | Supported .NET versions and OSes, which projects move over, status of every control and subsystem, Windows-only API, intentional differences. |
| [API coverage](api/README.md) | Generated: every public type of WinForms and `System.Drawing.Common`, complete / partial / missing, with the missing members and a link to Microsoft's page. |
| [The visual designer](designer.md) | The VS Code extension. |
| [Releasing](RELEASING.md) | For maintainers: publishing to NuGet, the VS Code Marketplace, Open VSX, GitHub Pages. |
| [The story](story/angry-csproj.md) | A script: how people who could not open a WinForms project on Linux ended up rewriting its `.csproj` (in Russian). |
| [Документация на русском](ru/README.md) | The same pages in Russian. |
| [Plan and decision log](PLAN.md) | Architecture, roadmap and every design decision (in Russian). |
