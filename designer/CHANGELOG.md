# Changelog

## Unreleased

- Events tab: a **+** button creates the handler with the default name in one click (no need to type the
  name and press Enter) and opens it; **✕** unbinds a handler, the method stays in the code.

## 0.1.0 — first preview

- Visual designer for `*.Designer.cs`: canvas painted by NetForms, toolbox, component tray,
  properties and events, snap lines, Format commands, lock, tab order, undo/redo.
- F7 / Shift+F7 between code and designer; renaming a control renames its uses in code.
- New Project / New Form / New User Control with `dotnet new` and the NetForms.Templates package from NuGet
  (installed on first use); new projects reference the NetForms package.
- Convert WinForms Project…: report, then switch the project to NetForms.
- English and Russian interface.
- One package per platform (Windows and Linux, x64 and arm64; macOS builds are untested).
