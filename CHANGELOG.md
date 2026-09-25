# Changelog

## 0.1.0-preview.6 — ready-made forms, copy and paste in the designer

- **Templates:** four forms made for a purpose - `dotnet new netforms-dialog` (OK/Cancel with `DialogResult`,
  `AcceptButton`/`CancelButton`), `netforms-aboutbox` (texts from the assembly attributes), `netforms-login` (user name
  and password) and `netforms-splash` (a borderless start-up screen).
- **Designer (extension 0.1.5):** copy, cut, paste and duplicate controls (also between forms), a right-click context
  menu, Set as Startup Form, Publish Application…, Check for Updates… (the projects' NetForms, the templates, the
  extension, the .NET SDK); `AcceptButton`/`CancelButton` are picked from the form's buttons.
- The NetForms library itself is unchanged since 0.1.0-preview.4.

## 0.1.0-preview.5 — tab pages and menus in the designer

- **Designer (extension 0.1.4):** TabPages, a MenuStrip's or ToolStrip's Items, DropDownItems and Columns open a
  collection editor that lists the elements by caption (`Приход`, not `TabPage: {Приход}`) — rename, add, remove and
  reorder them; a renamed tab page keeps its controls, `-` adds a menu separator. Before, OK failed with "Unable to
  cast object of type 'System.String' to type 'System.Windows.Forms.TabPage'". A click on a tab header on the canvas
  shows that page, so the controls of every page can be laid out in the designer.
- The NetForms library is unchanged since 0.1.0-preview.4.

## 0.1.0-preview.4 — the grid's row for new records

- **DataGridView — the row for new records** is a real row, as in WinForms: `Rows.Count` counts it, `Rows.Add` goes
  above it, `Rows.Clear()` keeps one, typing into it adds a row (`UserAddedRow`), entering it raises
  `DefaultValuesNeeded` (and `NewRowNeeded` in `VirtualMode`); the row header shows the star, and the pencil while the
  current row holds an edit. A shown grid makes its first cell current. `Image`/`byte[]` properties of a data source
  get image columns. **Behaviour change:** `Rows.Count` of an unbound grid with `AllowUserToAddRows` (the default) is
  one more than before, as in WinForms; skip the new row with `row.IsNewRow`.
- **System.Drawing:** `TextureBrush` is in `System.Drawing` and `FlushIntention` in `System.Drawing.Drawing2D`, as in
  WinForms (they were the other way round, so `new TextureBrush(image)` did not compile with `using System.Drawing;`).
- **TableLayoutPanel:** `TableLayoutControlCollection` is a type of `System.Windows.Forms`, as in WinForms (it was nested
  in the panel). `ImageList.Images.Keys` returns `System.Collections.Specialized.StringCollection`, a copy of the keys.
- **Documentation:** [Missing API by use](docs/api/usage.md) — which of the API NetForms lacks real projects use — and
  "What comes next" in the compatibility guide.

## 0.1.0-preview.3 — the designer keeps its place

- **Designer:** editing a property no longer resets the property grid — the selection, the scroll position, an open
  Anchor drop-down and the focus stay where they were.

## 0.1.0-preview.2 — data binding and grid editing

- **DataGridView:** the default cell styles carry the grid's font and follow it, as in WinForms — a custom cell's
  `Paint` gets `cellStyle.Font` (it was `null`), and `new Font(grid.DefaultCellStyle.Font, FontStyle.Bold)` works.
- **Data binding** is the WinForms implementation itself (`BindingContext`, `CurrencyManager`, `Binding`, `BindingSource`,
  `ListBindingHelper`, vendored from dotnet/winforms): a `DataSet` with a table as `DataMember`, master-detail through
  a `DataRelation`, `DisplayMember`/`ValueMember` over a `DataTable`, a grid's current row and its source's `Position`
  following each other. Bindings follow WinForms' rules: a control binds once it is created and has a binding context,
  and the default `DataSourceUpdateMode.OnValidation` writes the value when the control validates.
- **DataGridView editing** follows the WinForms model: editing controls (`IDataGridViewEditingControl`, custom ones
  such as Microsoft's calendar column), `EditingControlShowing`, `CurrentCellDirtyStateChanged`, `CellValidating`,
  `CellParsing`, `CellValidated`, `DataError` on a value that does not parse, `CommitEdit`/`CancelEdit`/`RefreshEdit`,
  the cell and row enter/leave/validating events, typing/F2/Enter/Escape/Tab, and `VirtualMode` (`RowCount`,
  `CellValuePushed`). The check box cell now commits its value when the cell is left, as in WinForms.
  `CellFormatting` gets the raw value; `DataGridViewDataErrorContexts` has the WinForms values.
- **Focus**: `Enter`, `Leave`, `Validating` and `Validated` reach every container the focus enters or leaves, as in WinForms.
- **Designer:** a **+** button in the Events tab creates a handler with the default name in one click; **✕** unbinds it.

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
