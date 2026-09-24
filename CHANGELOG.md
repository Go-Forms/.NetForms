# Changelog

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
