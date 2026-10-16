# Changelog

## Unreleased

- **Designer: Document Outline.** The left pane's *Outline* tab shows the form's controls as a tree, as in Visual
  Studio: a click selects, a drag puts a control into another container or in another place among its siblings
  (the z-order, so the docking order).
- **Designer: the TreeNode Editor.** A TreeView's nodes are edited as a tree, as in Visual Studio: added at the top or
  under a node, deleted, moved up, down, in and out, and each one's `Name`, `Text`, pictures from the tree's ImageList
  (`ImageKey`, `SelectedImageKey`, `StateImageKey`), `Tag`, `ToolTipText` and `Checked` set. A new node is named
  `Node0`, `Node1`… as VS names them. Before, only the texts could be edited, as indented lines (still there: *As text…*).
- **Designer: Visual Studio's editors for Anchor, Dock, colors and fonts.** Four bars around a box for `Anchor`, the six
  places for `Dock`, a color picker with Custom, Web and System colors (system colors now show their swatch), and a
  font picker: family, size, Bold/Italic/Underline/Strikeout, a sample.
- **Fixed: a font with two styles could not be read back.** `FontConverter` wrote `Segoe UI, 12pt, style=Bold, Italic`
  and then refused it; it now reads the whole style list, as in WinForms.
- **Fixed: a button's own BackColor did not show.** Setting `BackColor` turns `UseVisualStyleBackColor` off, as in
  WinForms (in the designer through the property, so `UseVisualStyleBackColor = false` is written, as VS writes it).
- **Designer: data binding in the property grid.** `DataSource` lists the form's binding sources and the classes of
  the project: a class picked for a grid or a list makes a `customerBindingSource` for it (`DataSource =
  typeof(Customer)`) and a grid gets a column per property, as in Visual Studio. `DisplayMember`, `ValueMember`,
  `DataMember` and `DataPropertyName` suggest the properties of the data source. The new **(DataBindings)** group binds
  `Text`, `Checked`, `Value` and the other bindable properties to a binding source's property
  (`new Binding("Text", customerBindingSource, "Name", true)`). While designed, a grid makes no columns of its own.
- **Fixed: a bound DataGridView threw away the columns the form gave it.** With `AutoGenerateColumns` on (the default),
  binding replaced every column — the designer's, with their headers, widths and types — by generated ones. Now, as
  in WinForms, a column whose `DataPropertyName` names a property shows it, and only the properties no column shows
  get a generated column.
- `[Bindable(true)]` on the properties WinForms marks (`Text`, `Tag`, `Checked`, `CheckState`, `Value`,
  `SelectedItem`, `SelectedIndex`, `Image`, `SplitterDistance`…).
- **Designer: a SplitContainer's splitter is dragged on the canvas** and sets `SplitterDistance`, as in Visual Studio.
  Before, a press on the splitter took the whole SplitContainer along.
- **Fixed: SplitContainer behaved oddly when resized and when designed.** The splitter's position is now kept as in
  WinForms: a window shrunk and grown back puts the splitter back where it was (before, each resize rounded it and a
  small window pushed it to the minimum for good); `FixedPanel` keeps a panel's size; `Panel1MinSize`,
  `Panel2MinSize` and `SplitterWidth` set between `BeginInit` and `EndInit` (as designer code does) no longer move the
  splitter before the size is known; the border is drawn by the panels (it was hidden under them); setting
  `SplitterDistance` raises `SplitterMoved`; the control cannot be made smaller than its two minimum panel sizes.
  A drag shows a bar where the splitter goes and moves the panels when let go, as in WinForms; `SplitterMoving` can
  cancel it. `SplitterPanel` has its public constructor; `OnSplitterMoving`/`OnSplitterMoved` are public, as in WinForms.
- **Designer: selection rectangle.** A drag on the form draws a rectangle that selects the controls it touches, as in
  Visual Studio; inside a selected panel or group box (or a SplitContainer's panel, a tab page) it selects that
  container's controls, and the container then moves by the ✥ glyph at its top left. Ctrl or Shift adds to the
  selection.
- **ToolStripContainer and ToolStripPanel.** Tool bars, menus and status bars in rows along the sides of a window: a
  strip joins the row its location falls in (a new one past the last), the panels size themselves to their rows and the
  `ContentPanel` takes the rest; at run time the user drags a strip by its grip along its row, to another row or to
  another side (a side panel stands it up). `Join`, `PointToRow`, `Rows`, `Locked`, `RowMargin`,
  `ToolStripRenderer.DrawToolStripPanelBackground`, `ToolStrip.Stretch` (a MenuStrip or StatusStrip takes its whole
  row). In the designer the container's panels are parts of it, as SplitContainer's are: strips are dropped into the
  side panels, other controls into the middle, and the code is the one Visual Studio writes.
- **Fixed: a ToolStrip standing up (docked left or right) was 100 pixels wide.** It is as wide as its widest item, as
  in WinForms; an empty ToolStrip is 111 × 25, as in WinForms.
- **Designer: every kind of strip item.** The Items editor of a ToolStrip, StatusStrip, MenuStrip or menu adds what
  Visual Studio's *Items Collection Editor* offers for it — label, split and drop-down button, separator, combo box,
  text box, progress bar (a status strip: status label, progress bar, drop-down and split button; a menu: menu item,
  combo box, text box, separator) — the kind picked beside each new line, the kind of each existing one shown. Before,
  every new item was a button, a menu item or a status label. A right-click on a strip offers *Edit Items…*, on a menu
  item *Edit DropDownItems…*.
- **Designer: pictures and icons are set from files and kept in the form's `.resx`, as in Visual Studio.** `Image`,
  `BackgroundImage`, a form's `Icon`, a menu item's `Image`: **…** in the property grid picks a file, **✕** clears it,
  and the grid shows a thumbnail. The designer writes `MainForm.resx` as Visual Studio does (the picture's bytes, read
  back by its type converter; a new file gets VS's header) and the code reads it with
  `resources.GetObject("button1.Image")`. An **ImageList**'s pictures are added from files, named, reordered and
  removed in the property grid; its `ImageStream` is written as the record Visual Studio writes (without
  BinaryFormatter). Pictures no longer used leave the `.resx`; undo and redo take the `.resx` along with the code.
- **Fixed: a form whose ImageList has pictures (`ImageList.ImageStream` in its `.resx`) did not open in the designer.**
  The designer host could not find the `System.Windows.Forms` name the `.resx` record gives (the facade NetForms puts
  beside itself was not in the host's dependency list).

## 0.1.3 — designer: menus, tool tips, flows and columns

- **Designer: the columns of a DataGridView and a ListView are selected by their headers.** A click on a column header
  selects the column; the property grid edits it — `Width`, `DataPropertyName`, `ReadOnly`, `AutoSizeMode`, its cell
  style, and for a DataGridView column its **ColumnType** (text box, check box, combo box, button, image, link), keeping
  its name, place and settings, as Visual Studio's *Edit Columns* dialog does. A new DataGridView column gets its `Name`
  (`Columns["…"]` finds it), and renaming the column renames both, as in Visual Studio. Before, only the columns'
  captions could be edited, and every new column was a text box column.
- **Designer: FlowLayoutPanel keeps its order and is reordered with the mouse.** A control added to a flow went first in
  it (so three buttons added one after another came out in reverse), and a drag inside the panel did nothing. Now a
  control goes at the end, or where it is dropped — a mark between the controls shows where — and a drag reorders the
  flow; the arrow keys move the selected control back or forth in it.
- **Designer: menus open on the canvas, as in Visual Studio.** A click on a menu item opens its drop-down under it (a
  sub-menu to the right of its menu), and a ContextMenuStrip selected in the component tray opens at the top left of the
  form. Every item in them can be selected, so an item at any depth gets its **Click** handler (double-click or the
  Events tab), `ShortcutKeys`, `Checked`, its own `DropDownItems` and the rest. Before, only the items of a strip on the
  form could be selected; the items of sub-menus and of a ContextMenuStrip were reachable only as captions.
- **Designer: ToolTip, HelpProvider, ErrorProvider and FlowLayoutPanel give their properties to the controls** —
  *ToolTip on toolTip1*, *HelpString on helpProvider1*, *Error on errorProvider1*, *FlowBreak on flowLayoutPanel1* —
  set in the property grid and written as Visual Studio writes them (`toolTip1.SetToolTip(button1, "...")`); a copied
  control keeps its tool tip. Before, none of them was in the grid, so a tool tip could not be set in the designer.

## 0.1.2 — TableLayoutPanel in the designer

- **Designer: TableLayoutPanel is laid out by cells, as in Visual Studio.** Before, a control dropped into a table
  flowed into the next free cell (a new one even pushed the others along), and dragging a control inside a table did
  nothing — the table's layout put it back. Now a control dropped from the toolbox or dragged from the form goes to the
  **cell under the mouse** (the cell is highlighted while dragging); a control dragged onto a taken cell **swaps** with
  the one there; the arrow keys move a control from cell to cell; a full table grows a row (or a column, by its
  `GrowStyle`). Every control of a table the designer edits is written as `Controls.Add(control, column, row)`, as
  Visual Studio writes it.
- **Columns and rows are edited.** The table's cells are drawn dashed on the canvas; the boundary between two columns
  (rows) is dragged to resize them (Absolute keeps pixels, Percent shares are recomputed, AutoSize becomes Absolute).
  The property grid has **Columns** and **Rows** (…): each column's size type (Absolute / Percent / AutoSize) and size,
  insert, remove, reorder — the controls go along with their columns; those of a removed column move to free cells,
  never deleted. A right-click in a table: *Insert Column Left/Right*, *Delete Column*, *Insert Row Above/Below*,
  *Delete Row*, *Edit Columns and Rows…*. `ColumnCount`/`RowCount` typed in the grid keep the styles in step. A
  control in a table has **Column**, **Row**, **ColumnSpan**, **RowSpan** in the grid (a wider span moves what it
  covers aside). A new table is 2 × 2 with 50% columns and rows, as Visual Studio makes it.

## 0.1.1 — PrintPreviewDialog in the designer

- **Designer bug fix — PrintPreviewDialog could not be added.** A click on it in the toolbox failed: the designer put the
  dialog, a top-level form, into the form's `Controls`. Now it goes to the component tray, as in Visual Studio, and so
  does a copy of it pasted or duplicated.

## 0.1.0 — the first stable release

The first release without a preview suffix: `dotnet add package NetForms` (and `NetForms.ExtraControls`,
`NetForms.Templates`, `NetForms.Convert`) no longer needs `--prerelease`. The API is 0.x — it still grows, and may
change where it differs from WinForms by mistake.

- **Designer bug fix — edits made in the code were lost.** With the designer and `MainForm.cs` (or
  `MainForm.Designer.cs` as text) open side by side, the next change in the designer wrote back the copy of both files it
  had read when the form was opened, undoing everything written in the code since. A build did the same: after
  `dotnet run` the designer reloaded the project's controls and wrote the form back. Now the designer reads both files
  again before every change, undo and redo, and writes only what the change itself touched (the designer file, and the
  code-behind when it adds a handler); undo never puts back a version of the code edited since. A designer file that no
  longer compiles is reported, not overwritten.
- **Run saves first:** *NetForms: Run* (and ▶ in the designer) saves the unsaved files before `dotnet run`, as Visual
  Studio does before F5; a change in the designer saves the form's open, unsaved code first.

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
