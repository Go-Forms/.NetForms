# Changelog

## 0.1.5

- **Copy, cut, paste and duplicate** controls: Ctrl+C / Ctrl+X / Ctrl+V / Ctrl+D (and VS Code's Edit menu). A copy
  carries everything inside it - child controls, a SplitContainer's panels, menu and tool strip items, columns,
  TableLayoutPanel cells and styles, list items - and pastes into another form too. A pasted control keeps its name
  when the form has it free (else `button2`), its handlers when pasted into the form it came from, and moves down and
  right when it would lie exactly on a control already there.
- **Context menu** on the canvas, the tray and the form's title, as in Visual Studio: View Code, Cut, Copy, Paste,
  Duplicate, Delete, Bring to Front, Send to Back, Lock Controls, Select the parent, Properties; on the form, Set as
  Startup Form.
- **New Form…** offers forms made for a purpose besides the empty one: Dialog, About Box, Login Form, Splash Screen
  (new templates `netforms-dialog`, `netforms-aboutbox`, `netforms-login`, `netforms-splash`).
- **NetForms: Set as Startup Form** points `Application.Run` in `Program.cs` at the form (Explorer context menu of a
  `*.Designer.cs`, the canvas' context menu).
- **NetForms: Publish Application…**: `dotnet publish` for Windows or Linux, x64 or ARM64, self-contained or
  framework-dependent, into `publish/<rid>` next to the project, then shows the folder.
- **NetForms: Check for Updates…** lists what is behind and updates what you tick: the NetForms package of the
  workspace's projects (to the newest on NuGet), the `dotnet new` templates, this extension (the `.vsix` of the newest
  GitHub release, then a reload) and the .NET SDK (the update command - winget, apt, dnf or Microsoft's install script
  - is typed into a terminal for you to check and run). The same check runs once a day and only speaks when there is
  something (`netforms.checkForUpdates`). New Project/New Form accept templates newer than the extension's.
- The property grid offers the form's buttons for `AcceptButton` and `CancelButton` (an `IButtonControl`, not a
  component type); before, the name typed there was quietly dropped.
- New projects reference NetForms 0.1.0-preview.6.

## 0.1.4

- TabControl: a click on a tab header on the canvas brings its page to the front, so the controls of every page can
  be laid out in the designer (as in Visual Studio, the choice is written as `SelectedIndex`). A click in the
  toolbox with the TabControl selected adds the control to the page it shows.
- TabPages, the Items of a MenuStrip, ToolStrip, StatusStrip or ContextMenuStrip, a menu item's DropDownItems,
  ListView.Columns and DataGridView.Columns open a collection editor that lists the elements by their captions
  (`Приход`, not `TabPage: {Приход}`): rename, add, remove and reorder them; a renamed tab page keeps its
  controls, a removed one takes them along, `-` adds a separator to a menu. Before, OK failed with
  "Unable to cast object of type 'System.String' to type 'System.Windows.Forms.TabPage'".
- New projects reference NetForms 0.1.0-preview.5.

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
