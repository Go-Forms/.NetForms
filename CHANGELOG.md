# Changelog

## 0.1.0-preview.8 — print dialogs finished, new controls, NetForms.ExtraControls

- **Printing:** `PrintDialog` has the **Properties…** button of the Win32 dialog — the printer's document properties
  (paper, tray, orientation, colour, duplex, print quality), written to the document's `DefaultPageSettings` and to
  `PrinterSettings.Duplex` on OK, as WinForms does with the DEVMODE. **Help** (`ShowHelp`) raises `HelpRequest`; typing a
  page number picks *Pages*; a backwards page range keeps the dialog open. `PageSetupDialog` has the **Printer…** button
  (`AllowPrinter`): another printer, with its own papers and trays. **Print to file** without `PrintFileName` asks where
  to save (the spooler's *Save Print Output As*) — before, it silently printed nothing. The print dialogs, the print
  preview and the printing status speak Russian on a Russian UI. New sample: `samples/Printing`.
- **`Control.OnPrint`**: `DrawToBitmap` paints each control through it (WM_PRINTCLIENT), so overriding it works as in
  WinForms. Also `NotifyInvalidate`, `RaisePaintEvent`/`RaiseKeyEvent`/`RaiseMouseEvent`, `RtlTranslate*`,
  `DefaultImeMode`, `IsMirrored`, `IsAncestorSiteInDesignMode`.
- **New controls:** `DomainUpDown` (with `UpDownBase`'s protected hooks as in WinForms: `OnChanged`,
  `OnTextBoxTextChanged(object, EventArgs)`, `OnTextBoxKeyDown`, …), `HelpProvider` (F1 help: a pop-up or a help file,
  an extender provider in the designer), `Splitter` (the docked splitter, with its drag bar), `BindingNavigator` (the
  record navigator of a `BindingSource`; dropped in the designer it comes with its standard items, as in VS), and the
  renderers `ButtonRenderer`, `CheckBoxRenderer`, `RadioButtonRenderer`, `ComboBoxRenderer`, `ProgressBarRenderer`,
  `ScrollBarRenderer` — owner-drawn controls paint in the theme of the real ones. `ToolStripItem`: `IsDisposed`,
  `AccessibleName`/`Description`/`Role`, `RightToLeftAutoMirrorImage`.
- **The corpus of real projects needs no missing API any more** (25,887 references in 63 projects): `ImageFormat` with
  its GDI+ `Guid` and `Icon`/`Tiff`/`Wmf`/`Emf`/`Exif`/`Heif`, `ImageList.Images.Add(string, Icon)` and `Remove`,
  `LinkLabel.OverrideCursor`/`PointInLink`, `OpenFileDialog.SafeFileName(s)`, `TabPages.Add`/`Insert` by key and image,
  the `Font` constructors with a character set. MDBEditor, GetStockIcon and xrails-login-ui build now: 31 of 45
  open-source projects (was 28). **Bug fix:** `Image.Save` as BMP or GIF threw `NotSupportedException` (Skia has no
  encoders for them); NetForms now writes BMP, GIF and TIFF itself, and Icon/EMF/WMF as PNG, as GDI+ does.
  **Signature fixes** (source-compatible for callers): `TabPages.Remove` and `ImageList.Images.Remove` return `void`,
  `Images.Add(Icon)` returns `void`, `Images.Add(Image, Color)` and `AddStrip` return the index, as in WinForms.
- **Behaviour change — dialogs:** a button's `DialogResult` closes a modal form after its `Click` handlers, as in
  WinForms: a handler that sets `DialogResult = DialogResult.None` (validation on OK) keeps the form open. Before, the form
  had already closed.
- **Designer:** a `PrintPreviewDialog` of a form is in the component tray (it was on neither the canvas nor the tray);
  `BindingNavigator` and `Splitter` are in the toolbox.
- **NetForms.ExtraControls** — a new package: `ToggleSwitch`, `RatingStars`, `CircularProgressBar`, `GradientPanel`,
  `ColorPickerButton` and the `CountdownTimer` component, with toolbox icons. Add it in the designer with **Add Control
  Library → NuGet package** (`NetForms.ExtraControls`); its source is the worked example of a control library.
- **Releasing** is a command: a push to `main` no longer publishes; *Actions → Release → Run workflow* (publish, version)
  or a tag does (docs/RELEASING.md).

## 0.1.0-preview.7 — control libraries in the designer, printing, drag and drop

- **Designer — the project's own controls and control libraries:** the toolbox has the project's controls, user
  controls and components from its last build (a **<Project> Components** group, as in Visual Studio), live on the
  canvas with their own properties, events and painting. **NetForms: Add Control Library…** adds controls from a NuGet
  package, a `.dll`, a local `.nupkg`/feed, another project, a folder of the project or a package the project already
  references; each library is checked before it is added without running its code (built for NetForms, for the real
  WinForms, Windows-only, no .NET 10-compatible framework). **Remove Control Library**, **Rescan Toolbox**; the toolbox
  groups are kept in `.vscode/netforms.json`. Restricted Mode lists the controls but loads none of their code. A form can
  derive from a form of the project.
- **A control whose painting throws is drawn as a red cross**, as in WinForms: the exception reaches the application
  once, then the control shows the cross. In the designer, a control whose constructor or `OnPaint` throws is a red
  cross with the error, and the form still opens.
- **Designer:** a placeholder (a control of an unknown type) keeps its constructor arguments — `new MyGauge(components)`
  was written back as `new MyGauge()`.
- **Printing — `System.Drawing.Printing` complete:** `PrintDocument` with WinForms' page loop (`BeginPrint`,
  `QueryPageSettings`, `PrintPage`, `EndPrint`, cancel), `PageSettings`/`PrinterSettings` with the printer's paper
  sizes, trays, resolutions, duplex and colour, `Margins`, `PrinterUnitConvert`, `OriginAtMargins`; a page's `Graphics`
  works in 1/100 inch with text at its physical size. Printers come from CUPS on Linux (a job is a PDF given to `lp`)
  and from winspool/GDI on Windows; print to file writes PDF. **`PrintDialog`, `PageSetupDialog`,
  `PrintPreviewControl`, `PrintPreviewDialog`, `PrintControllerWithStatusDialog`**; the preview keeps pages as vector
  drawings and works without a printer. The designer's toolbox has a Printing group.
- **Drag and drop:** `DoDragDrop` works — `QueryContinueDrag`, `GiveFeedback`, `DragEnter`/`DragOver`/`DragLeave`/
  `DragDrop` as in WinForms, within a form and across the application's forms; files and text dropped from other
  applications arrive as `FileDrop`/`Text`. `DataObject` is WinForms' own (format conversions, `SetDataAsJson`,
  `TryGetData<T>`); `DoDragDropAsJson`, `Clipboard.TryGetData`/`SetDataAsJson`. `ListView.ItemDrag`/`ItemMouseHover`.
- **TreeView — API complete:** `ItemDrag`, `NodeMouseHover`, `HotTracking`, node tool tips, a node's own
  `ContextMenuStrip`, state images and image keys, `GetItemRenderStyles`, `RightToLeftLayout`, `TreeNode.Handle`/
  `FromHandle`/serialization, the rest of `TreeNodeCollection`. **Behaviour change:** as in the native tree, a press
  on a node selects it on release and `NodeMouseClick` comes after the release; the right button does not select.
- **Panel, GroupBox, ScrollableControl — API complete:** `DockPadding`, the scroll state, `ScrollToControl` (now
  called by `ScrollControlIntoView`, so overriding it works), `SetAutoScrollMargin`, `GroupBoxRenderer`.
- **Accessibility model:** `AccessibleObject`, `Control.ControlAccessibleObject`, `AccessibilityObject`,
  `CreateAccessibilityInstance`, `AccessibleRole`, `QueryAccessibilityHelp` (the bridge to screen readers is next).

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
