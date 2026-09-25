# Changelog

## 0.1.3

- A DataGridView on the canvas shows its row for new records (the star in the row header), as the Visual Studio
  designer does: NetForms 0.1.0-preview.4 makes it a real row.
- New projects reference NetForms 0.1.0-preview.4.

## 0.1.2

- The property grid keeps its place while you edit: after a change the selection, the scroll position, an open
  flags drop-down (Anchor: Top, then Left, then Right without looking for it again) and the field Tab moved to
  stay as they were. Before, each change reloaded the form a moment later, selected the form and rebuilt the
  grid from the top.
- Renaming a control keeps it selected under its new name.
- New projects reference NetForms 0.1.0-preview.3.

## 0.1.1

- Events tab: a **+** button creates the handler with the default name in one click (no need to type the
  name and press Enter) and opens it; **✕** unbinds a handler, the method stays in the code.
- New projects reference NetForms 0.1.0-preview.2 (data binding and grid editing, see the NetForms changelog).

## 0.1.0 — first preview

- Visual designer for `*.Designer.cs`: canvas painted by NetForms, toolbox, component tray,
  properties and events, snap lines, Format commands, lock, tab order, undo/redo.
- F7 / Shift+F7 between code and designer; renaming a control renames its uses in code.
- New Project / New Form / New User Control with `dotnet new` and the NetForms.Templates package from NuGet
  (installed on first use); new projects reference the NetForms package.
- Convert WinForms Project…: report, then switch the project to NetForms.
- English and Russian interface.
- One package per platform (Windows and Linux, x64 and arm64; macOS builds are untested).
