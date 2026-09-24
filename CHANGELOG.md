# Changelog

## 0.1.0-preview.1 — first public preview

The first release on NuGet and the VS Code Marketplace.

- **NetForms** — `System.Windows.Forms` and `System.Drawing` for .NET 10 on Windows and Linux: the message
  loop, layout (`Anchor`, `Dock`, `TableLayoutPanel`, `FlowLayoutPanel`, `AutoSize`), focus and validation,
  the common controls, containers, `MenuStrip`/`ToolStrip`/`StatusStrip`, `ListView`, `TreeView`,
  `DataGridView`, `PropertyGrid`, `RichTextBox`, MDI, dialogs, `NotifyIcon`, `.resx` resources.
  Status of everything: [docs/compatibility.md](docs/compatibility.md).
- **NetForms.Templates** — `dotnet new netforms`, `netforms-form`, `netforms-usercontrol` (`--Namespace` for items); new
  projects reference the NetForms package from NuGet.
- **NetForms.Convert** — `netforms-convert`: SDK-style and .NET Framework WinForms projects to NetForms, with
  a report of what does not carry over.
- **NetForms Designer** for VS Code — the WinForms designer, offline editing, English and Russian; new projects and
  forms come from the NetForms.Templates package on NuGet.
- Documentation website in English and Russian: <https://go-forms.github.io/.NetForms/>.
- Not yet: printing, accessibility, drag-and-drop, dark theme, per-monitor DPI.
