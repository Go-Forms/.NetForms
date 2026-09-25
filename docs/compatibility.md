# Compatibility guide

NetForms is a copy of Windows Forms. **Its API reference is Microsoft's:** every type, member, event
and default value is meant to be exactly what
[learn.microsoft.com/dotnet/api/system.windows.forms](https://learn.microsoft.com/dotnet/api/system.windows.forms)
and [the Windows Forms documentation](https://learn.microsoft.com/dotnet/desktop/winforms/) describe.
This guide covers only what that documentation cannot tell you:

1. [What runs NetForms](#1-what-runs-netforms) — .NET versions, operating systems.
2. [Which projects move over, and which do not](#2-which-projects-move-over).
3. [Controls and components](#3-controls-and-components) — status of each toolbox item.
4. [Subsystems](#4-subsystems) — layout, drawing, text, resources, data binding, clipboard, printing…
5. [Windows-only API](#5-windows-only-api) — `Handle`, `WndProc`, P/Invoke, COM.
6. [Intentional differences](#6-intentional-differences) from WinForms.
7. [How compatibility is measured](#7-how-compatibility-is-measured).
8. [What comes next](#8-what-comes-next) — the missing API real code uses, in order.

The member-by-member list — what exists and what is missing for each of the 1254 public types of
`System.Windows.Forms` and `System.Drawing.Common` — is generated: **[API coverage](api/README.md)**.

State as of version **0.1.0-preview.6** (September 2026).

---

## 1. What runs NetForms

### .NET

| Target framework | Works? | Why |
|---|---|---|
| `net10.0` | ✅ **Yes — the only one** | The packages ship `lib/net10.0` only. |
| `net10.0-windows` | ✅ Windows only | For "Windows only" projects that want NetForms rendering (`netforms-convert --target windows`). The `-windows` TFM does not build on Linux. |
| `net9.0`, `net8.0`, `net7.0`, `net6.0`, `net5.0`, `netcoreapp3.1` | ❌ No | NuGet fails with `NU1202: Package NetForms … is not compatible with net8.0`. Retarget to `net10.0` — the converter does it. |
| `net8.0-windows` + `UseWindowsForms` (any `-windows` TFM with the real WinForms) | ❌ Not on Linux | `NETSDK1100` at build; with `EnableWindowsTargeting=true` it builds, then fails at start: *Framework: 'Microsoft.WindowsDesktop.App' … No frameworks were found*. This is exactly what NetForms replaces. |
| .NET Framework 2.0 – 4.8.1 (`net48`, `net472`, …) | ❌ Not directly | NetForms is not a .NET Framework library. The converter moves such projects to SDK-style `net10.0` (see §2). |
| `netstandard2.0/2.1` libraries | ✅ If they do not use WinForms | A .NET Standard library cannot reference WinForms anyway; plain logic libraries work as-is. |
| Mono (`mono`, `msbuild` from Mono) | ❌ No | Mono has its own old `System.Windows.Forms` on libgdiplus; NetForms needs the .NET 10 runtime. |

You need the **.NET 10 SDK** to build and the **.NET 10 runtime** to run (or publish self-contained).

### Operating systems

| OS | Status |
|---|---|
| Windows 10 / 11, x64 and arm64 | ✅ Supported, tested in CI (`windows-latest`). |
| Linux x64 / arm64 with X11, or Wayland through XWayland | ✅ Supported, tested in CI (`ubuntu-latest`), on Ubuntu 24.04/26.04 and WSL. |
| macOS | ⚠️ Not a goal of this release. Avalonia and SkiaSharp run there and the native libraries are shipped, but nothing is tested. |
| Windows 7 / 8.1, 32-bit Windows, Linux x86/ARM32 | ❌ .NET 10 does not run there, or the natives are not shipped. |
| Browser (WebAssembly), Android, iOS | ❌ Not a target. WinForms is a desktop API. |

**Linux packages** an application needs at run time: the .NET 10 runtime (with ICU), fontconfig, `libX11`,
`libICE`, `libSM` (Avalonia's X11 backend) and at least one TrueType font family. A desktop distribution has
all of them; the commands for Debian/Ubuntu/Astra, Fedora/RED OS and ALT are in
[Install and set up](install.md#2-system-libraries-linux).

When `Segoe UI` (WinForms' default font) is not installed, the closest installed sans-serif family is
used; text metrics then differ slightly from Windows (see §4, Text).

---

## 2. Which projects move over

| What you have | What happens |
|---|---|
| SDK-style WinForms project, .NET Core 3.1 – .NET 10 (`<UseWindowsForms>true</UseWindowsForms>`) | ✅ `netforms-convert App.csproj --apply`: `UseWindowsForms` → `PackageReference NetForms`, `net8.0-windows` → `net10.0`, the two implicit usings spelled out. Code is not touched. |
| Old-format .NET Framework 2.0 – 4.8.1 project (`ToolsVersion`, `packages.config`, `<Reference Include="System.Windows.Forms" />`) | ✅ The converter rewrites it to SDK-style itself (no upgrade-assistant needed): file list, `DependentUpon`, resources, `packages.config` → `PackageReference`, `.snk` signing, `AssemblyInfo.cs`. Non-WinForms libraries of the same solution are converted to `net10.0` too. |
| Whole solution (`.sln`, `.slnx`) or folder | ✅ `netforms-convert MySolution.sln --apply` converts every project. |
| VB.NET WinForms project (`.vbproj`) | ❌ Not supported. The designer, the templates and the converter are C#-only; the `Microsoft.VisualBasic` application framework (`My.Application`, `WindowsFormsApplicationBase`) is not implemented. |
| C++/CLI WinForms project (`.vcxproj`) | ❌ C++/CLI is Windows-only in .NET. |
| WPF, or WinForms hosting WPF (`ElementHost`, `UseWPF`) | ❌ Not WinForms. The converter reports `UseWPF`. |
| Project that uses a NuGet package with `FrameworkReference Microsoft.WindowsDesktop.App` | ❌ That package demands the real Windows Desktop runtime (`NETSDK1136`). Look for a cross-platform version of the package. |
| Project using `BinaryFormatter` (directly, or in `.resx` other than `ImageList` images) | ❌ `BinaryFormatter` was removed from .NET 9+ — on every OS, not only in NetForms. `ImageList.ImageStream` from the VS designer is read by NetForms itself. |
| Project using WebView2, CefSharp, ActiveX (`AxHost`), COM references | ❌ Windows-only native components. The converter reports `COMReference` as an error. |
| Third-party WinForms control packages from NuGet (ZedGraph, OxyPlot.WindowsForms, ScottPlot.WinForms, FastColoredTextBox, ObjectListView, DockPanelSuite…) | ❌ Not yet. Checked with those six on 2026-09-24: packages built for .NET Framework compile against the strong-named `System.Windows.Forms, PublicKeyToken=b77a5c561934e089`, which NetForms' facade is not, so the build stops with `CS0012`; packages built for `net*-windows` demand the Windows Desktop runtime (`NETSDK1136`, DockPanelSuite). Controls whose source you include in your project compile like your own code. |

The converter never guesses: before `--apply` it prints what it would change and every place that will
not work on Linux (with file and line), and after it the original project is kept next to the new one
as `*.csproj.winforms.bak`. See [Migrating](migrating.md).

Measured on a corpus of real projects (`tests/corpus/corpus.json`): **7 of 7** of the customer's
.NET Framework 4.8/4.8.1 projects and **28 of 45** open-source WinForms projects convert and build with no
manual edit. Of the 17 that do not, 6 use `BinaryFormatter` and 4 Windows-only components (WebView2, CefSharp, a
package that demands the Windows Desktop runtime) — the last rows of the table; 5 need API NetForms does not have
yet (MDBEditor's printing is in now, it still needs `ImageFormat.Icon`/`Tiff`/`Wmf` and a few members;
`ImageList.Images.Add(string, Icon)`, `LinkLabel.OverrideCursor`, Visual Basic's `Microsoft.VisualBasic.Devices`); 2 stop on a package (a build task that fails on .NET 10, a package reference the
converter does not carry over). What gets added next: [§ 8](#8-what-comes-next).

---

## 3. Controls and components

Legend. **Works** — implemented, painted by NetForms, behaviour checked against real WinForms by the
diff tests. **API: complete** — every public/protected member exists; *N missing* — see
[API coverage](api/System.Windows.Forms.md) for the list.

### Common controls

| Control | Status | Notes |
|---|---|---|
| `Button` | ✅ Works · 1 missing | `Image`, `ImageList`, `TextImageRelation`, `FlatStyle`, `FlatAppearance`, `DialogResult`, `AcceptButton`/`CancelButton`. |
| `CheckBox` | ✅ Works · 1 missing | Three-state, `Appearance.Button`, AutoSize metrics identical to VS (`checkBox1` → 83×19). |
| `CheckedListBox` | ✅ Works · 2 missing | |
| `ComboBox` | ✅ Works · 4 missing | `DropDown`, `DropDownList`, `Simple`; the list opens outside the form. `DataSource` with `DisplayMember`/`ValueMember` (lists, `DataTable`, `DataSet` paths), the selection following the data source's position. `AutoCompleteMode` is stored, suggestions are not shown yet. |
| `DateTimePicker` | ✅ Works · 1 missing | Custom formats, field editing with arrows, drop-down calendar, `ShowUpDown`, `ShowCheckBox`. |
| `Label` | ✅ Works · 12 missing | Missing: `Image`/`ImageList` on a label, `PreferredWidth/Height`. |
| `LinkLabel` | ✅ Works · 3 missing | |
| `ListBox` | ✅ Works · 7 missing | Owner draw, multi-select, `DataSource`/`DisplayMember`/`ValueMember` as in `ComboBox`. Missing: `CustomTabOffsets`, `Sort()` override hook. |
| `ListView` | ✅ Works · 38 missing | All views (Details, List, SmallIcon, LargeIcon, Tile), groups, check boxes, label edit, sorting, `ItemDrag` (a press on one of several selected items keeps them all for the drag), `ItemMouseHover`. Missing: `VirtualMode`, column reordering by drag, `HotTracking`, insertion mark. |
| `MaskedTextBox` | ✅ Works · 1 missing | Masks via the same `MaskedTextProvider` WinForms uses. |
| `MonthCalendar` | ✅ Works · 8 missing | One month is shown (`CalendarDimensions` is stored). |
| `NotifyIcon` | ✅ Works · API complete | System tray via the OS (StatusNotifierItem on Linux); the context menu is drawn by the shell. See §6. |
| `NumericUpDown` | ✅ Works · 3 missing | |
| `PictureBox` | ✅ Works · 9 missing | All `SizeMode`s. `Load()`/`Load(path)` read a file; setting `ImageLocation` alone does not load it, and URLs are not fetched. Missing: `LoadAsync`. |
| `ProgressBar` | ✅ Works · 5 missing | `Marquee` included. |
| `RadioButton` | ✅ Works · 1 missing | |
| `RichTextBox` | ✅ Works · 1 missing | Own RTF reader/writer: fonts, colours, bold/italic/underline, bullets, alignment, links, undo. Not yet: numbered lists, images/OLE objects, justify, drag-and-drop, IME. |
| `TextBox` | ✅ Works · 1 missing | Selection, undo, clipboard, context menu, multiline, password, `CharacterCasing`. `AutoCompleteCustomSource` is stored, suggestions are not shown. |
| `ToolTip` | ✅ Works · 2 missing | |
| `TreeView` | ✅ Works · API complete | Check boxes, images by index or key, state images (`StateImageList`, `StateImageKey`), label edit, owner draw, `ItemDrag` (left and right button), `NodeMouseHover`, `HotTracking`, node tool tips (`ShowNodeToolTips`), a node's own `ContextMenuStrip`, `GetItemRenderStyles`, `TreeNode.Handle`/`FromHandle`, `TreeNode` serialization. As the native tree, a press on a node selects it on release, so dragging a node does not select it, and the right button does not select. `RightToLeftLayout` is stored, not mirrored (see §4, Text). |
| `WebBrowser` | ❌ Missing | Internet Explorer control; will not come (see §5). |
| `DomainUpDown` | ❌ Missing | |
| `HScrollBar`, `VScrollBar`, `TrackBar` | ✅ Works | |

### Containers

| Control | Status | Notes |
|---|---|---|
| `Panel`, `GroupBox` | ✅ Works · API complete | `AutoScroll`, `AutoSize`, `BorderStyle`, `DockPadding`, the scroll state (`HScroll`/`VScroll`, `GetScrollState`), `ScrollToControl` (override it to stop a panel jumping to the focused control), `SetAutoScrollMargin`, accessibility (`Client`, `Grouping`). `GroupBoxRenderer` draws the group box frame for owner-drawn controls. |
| `FlowLayoutPanel` | ✅ Works · API complete | Layout engine ported from dotnet/winforms, including the `SetFlowBreak` quirk. |
| `TableLayoutPanel` | ✅ Works · 1 missing | Percent/absolute/auto-size rows and columns, spans. |
| `SplitContainer` | ✅ Works · 2 missing | |
| `TabControl` | ✅ Works · 13 missing | Missing: `ImageList` on tabs, `DeselectTab`, `RightToLeftLayout`. |
| `Splitter` | ❌ Missing | Use `SplitContainer`. |
| `UserControl` | ✅ Works · API complete | |
| `Form` | ✅ Works · 34 missing | Modal/modeless, `Owner`, `TopMost`, `StartPosition`, `FormBorderStyle`, `AcceptButton`, `KeyPreview`, MDI, `TopLevel = false`. Missing: `ShowAsync`/`ShowDialogAsync` (.NET 9+), caption colours and corner preference (Windows 11 only), `DpiChanged`. |
| MDI (`IsMdiContainer`, `MdiParent`, `LayoutMdi`, `MdiWindowListItem`) | ✅ Works | |

### Menus and toolbars

| Control | Status | Notes |
|---|---|---|
| `MenuStrip`, `ContextMenuStrip`, `ToolStrip`, `StatusStrip` | ✅ Works · few missing | All standard items (`ToolStripButton`, `…MenuItem`, `…DropDownButton`, `…SplitButton`, `…ComboBox`, `…TextBox`, `…ProgressBar`, `…Label`, `…Separator`), shortcuts, mnemonics, overflow, professional renderer. Missing: dragging items (`AllowItemReorder`), `LayoutStyle` settings objects. |
| `ToolStripContainer`, `ToolStripPanel` | ❌ Missing | Docked, draggable toolbars. |
| `MainMenu`, `ContextMenu`, `ToolBar`, `StatusBar`, `DataGrid` | ❌ Missing | The .NET Framework 1.x controls — **removed from .NET itself** in .NET Core 3.1; they do not work in real WinForms on .NET 10 either. Replace them with `MenuStrip`, `ContextMenuStrip`, `ToolStrip`, `StatusStrip`, `DataGridView`. |

### Data

| Control | Status | Notes |
|---|---|---|
| `DataGridView` | ✅ Works · 236 missing | Columns of every kind (text, check box, combo box, button, link, image) and per-cell types (`row.Cells[i] = new DataGridViewButtonCell()`), `CellContentClick`, editing, sorting, selection modes, auto-size policies, `DataSource`/`DataMember` binding through the form's `BindingContext` (lists, `DataTable`, a `DataSet` table, a relation for master-detail; the current row and the source's `Position` follow each other; relations are not columns), frozen columns, virtual scrolling. Custom cells (a `DataGridViewCell` subclass overriding `Paint`) work and get the inherited style, font included; the default styles carry the grid's font and follow it, as in WinForms. Editing is the WinForms model: the cell's `EditType` control (`DataGridViewTextBoxEditingControl`, `DataGridViewComboBoxEditingControl`, or your own `IDataGridViewEditingControl` — Microsoft's calendar column works as published) in `EditingPanel`, `EditingControlShowing`, the dirty cell and `CurrentCellDirtyStateChanged`, `CellValidating`/`CellParsing`/`CellValidated` and `DataError` on commit, `CellEnter`/`CellLeave`/`RowEnter`/`RowLeave`/`RowValidating`, `BeginEdit`/`EndEdit`/`CommitEdit`/`CancelEdit`/`RefreshEdit`, `EditMode`, typing/F2/Enter/Escape/Tab; the check box cell edits itself (`IDataGridViewEditingCell`) and commits on leave, as in WinForms. `VirtualMode` with `RowCount`, `CellValueNeeded`/`CellValuePushed`, `RowDirtyStateNeeded`, `CancelRowEdit`. The **row for new records** of an unbound grid is a real row, as in WinForms: `Rows.Count` counts it, `IsNewRow`, `Rows.Add` goes above it, `Rows.Clear()` keeps one, typing into it adds a row (`UserAddedRow`), entering it raises `DefaultValuesNeeded` (and `NewRowNeeded` in `VirtualMode`); a shown grid makes its first cell current. A data source's `Image` and `byte[]` properties get image columns. Missing: the new row of a *bound* grid (adding records from the grid), clipboard copy, resizing and reordering columns with the mouse, the rest of the protected `Process*Key`/`On*Changed` hooks and `AutoResize*` methods. |
| `BindingSource` | ✅ Works · API: complete | The WinForms implementation itself (vendored from dotnet/winforms): lists, `DataTable`, a `DataSet` with a table as `DataMember`, master-detail with a `DataRelation` as the `DataMember` of a second `BindingSource`, `Position`, `Filter`, `Sort`, `AddNew`, `CurrencyManager`. |
| `BindingNavigator` | ❌ Missing | |
| ADO.NET itself (`DataSet`, `DataTable`, `DataAdapter`, providers) | Part of .NET, not of WinForms: works on Linux as it is. Edits made in a bound grid set `RowState`, so `adapter.Update(table)` saves them. The provider decides the platform: SQL Server (`Microsoft.Data.SqlClient`), PostgreSQL, MySQL, SQLite, Firebird, Oracle run on Linux; **Access through `System.Data.OleDb` is Windows-only** (`PlatformNotSupportedException` on Linux, the converter warns). |
| `Control.DataBindings`, `Binding`, `BindingContext`, `CurrencyManager` | ✅ Works · API: complete | The WinForms implementation itself (vendored from dotnet/winforms), with its rules: a control binds once it is created and has a `BindingContext` (a form gives both); the default `DataSourceUpdateMode.OnValidation` writes the value when the control validates; `Format`/`Parse`, `FormatString`, `NullValue`, `BindingComplete` (with formatting enabled), `ErrorProvider` over `IDataErrorInfo`. The designer reads and writes `DataBindings.Add(new Binding(...))` as Visual Studio does. |
| `PropertyGrid` | ✅ Works · 31 missing | Categories, type editors, expandable objects. Missing: the command pane and its colours. |

### Components and dialogs

| Component | Status |
|---|---|
| `Timer`, `ImageList`, `ErrorProvider`, `HelpProvider`* , `BackgroundWorker` | ✅ (`HelpProvider` ❌ missing; `BackgroundWorker` is part of .NET itself and works as-is) |
| `MessageBox`, `TaskDialog` | ✅ Own dialogs in the NetForms theme; button captions follow the UI language (English, Russian). |
| `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog` | ✅ Native dialogs of the OS (portal / GTK on Linux). A few properties missing (`ClientGuid`, custom places). |
| `ColorDialog`, `FontDialog` | ✅ Own dialogs. |
| `PrintDocument`, `PrintDialog`, `PageSetupDialog`, `PrintPreviewControl`, `PrintPreviewDialog`, `PrintControllerWithStatusDialog` | ✅ Works · API complete — see Printing in §4. The dialogs are NetForms' own forms (the same on Windows and Linux) and write back to `PrinterSettings`/`PageSettings` what the Win32 dialogs write. |
| `NotifyIcon` | ✅ See above. |

---

## 4. Subsystems

| Area | Status |
|---|---|
| **Message loop** | `Application.Run(Form)`, `Run(ApplicationContext)`, `DoEvents`, `Exit`, `Idle`, `ThreadException` (an unhandled exception in an event handler shows the continue/quit dialog, as in WinForms), `IMessageFilter` (sees `WM_KEYDOWN`/`WM_KEYUP`). `ApplicationConfiguration.Initialize()` works. `Control.Invoke`/`BeginInvoke`/`InvokeAsync` marshal to the UI thread. |
| **Layout** | `Anchor`, `Dock`, `Margin`/`Padding`, `AutoSize`/`AutoSizeMode`, `MinimumSize`/`MaximumSize`, `SuspendLayout`/`ResumeLayout`, `AutoScroll`. Coordinates match real WinForms — checked by the diff tests on every build. |
| **Focus and keyboard** | Tab order, `TabStop`, mnemonics (`&File`), `AcceptButton`/`CancelButton`, `ProcessCmdKey`/`ProcessDialogKey`/`IsInputKey`/`PreviewKeyDown`, both WinForms focus-event orders (`Select()` vs `Focus()`), validation (`Validating`/`Validated`, `AutoValidate`). |
| **Mouse** | Click, double click, capture, wheel, enter/leave/hover, cursors (named cursors; a cursor from a `.cur` file shows the arrow). `Cursor.Position` can be read, not set. |
| **Drawing (`System.Drawing`)** | `Graphics` on SkiaSharp: lines, shapes, paths, curves, regions, clipping, transforms, `LinearGradientBrush`, `HatchBrush`, `TextureBrush`, images with `ImageAttributes`/`ColorMatrix`, `LockBits`, `RotateFlip`, icons, `ControlPaint`. Missing: `PathGradientBrush`, `BufferedGraphics`, `ImageAnimator` (animated GIF), metafiles (`Metafile`, EMF/WMF); `Graphics.CopyFromScreen` throws `NotSupportedException`. Images are decoded by Skia (PNG, JPEG, BMP, GIF — first frame, ICO, WebP); `Image.Save` writes PNG, JPEG and WebP, while BMP and GIF throw `NotSupportedException`. |
| **Text** | `TextRenderer.MeasureText`/`DrawText` is the reference (GDI metrics), `Graphics.MeasureString`/`DrawString` follows GDI+ rules. With the same font file the numbers equal Windows; with a substitute font (no Segoe UI on Linux) they differ by a pixel or two. `RightToLeftLayout` mirroring is not implemented. |
| **DPI** | Everything is laid out in 96-DPI logical pixels and scaled by the platform to the screen (sharp on HiDPI). `AutoScaleMode`/`AutoScaleDimensions` are kept; `DeviceDpi` is always 96 and `DpiChanged` is never raised. |
| **Theme** | One built-in light theme that looks like WinForms with visual styles. `Application.SetColorMode(Dark)` is accepted, dark mode is not drawn yet. `VisualStyleRenderer` types exist; drawing goes through the NetForms theme. |
| **Resources (`.resx`)** | Strings, images, icons, typed values, `ResXFileRef` — read at run time through `ComponentResourceManager`, as in WinForms. The designer opens `Localizable = true` forms but does not write them yet. `ImageList.ImageStream` (BinaryFormatter in the VS designer's format) is read by NetForms. Other BinaryFormatter resources are not (see §2). |
| **Settings** | `Properties.Settings` (`ApplicationSettingsBase`), `ConfigurationManager`, `app.config` compile and run: the NetForms package brings `System.Configuration.ConfigurationManager`, as the Windows Desktop runtime does. |
| **Clipboard** | Text goes through the OS clipboard both ways. Images, file lists, audio and custom formats work inside the application only. `DataObject` is WinForms' own (format conversions: `Text`/`UnicodeText`/`System.String`, `FileDrop`/`FileNameW`, `Bitmap`; `SetDataAsJson`/`TryGetData<T>` of .NET 9+). |
| **Drag and drop** | ✅ The OLE protocol, run by NetForms: `DoDragDrop` is modal and returns the effect; the source gets `QueryContinueDrag` (Escape cancels, releasing the button drops) and `GiveFeedback` (standard drag cursors unless it draws its own); the control under the pointer with `AllowDrop` — or its nearest parent that has it, as OLE finds a registered window — gets `DragEnter`/`DragOver`/`DragLeave`/`DragDrop` with screen `X`/`Y`, `KeyState` and `Effect` exactly as in WinForms. Drags cross between the application's forms. **Drops from other applications** (files from the file manager, text) arrive through the platform as `FileDrop`/`Text`. Not yet: dragging *out* to another application, the drag image of `DoDragDrop(…, dragImage, …)`, `RichTextBox.EnableAutoDragDrop`. |
| **IME** | Committed text from input methods (Chinese, Japanese, Korean…) arrives in `KeyPress`/`TextBox`. The composition string is shown by the input method's own window, not inline. |
| **Accessibility** | 🟡 The model: `AccessibleObject`, `Control.ControlAccessibleObject`, `AccessibilityObject`/`CreateAccessibilityInstance` (custom controls describe themselves as in WinForms), `AccessibleRole`, names from the text or the label before the control, keyboard shortcuts from mnemonics, states, bounds, children, `DoDefaultAction`, `QueryAccessibilityHelp`. Not yet: the bridge to the OS (UI Automation / AT-SPI through Avalonia's automation peers) — screen readers do not see NetForms controls yet. |
| **Printing** | ✅ `System.Drawing.Printing` complete: `PrintDocument` with WinForms' page loop and events, `PageSettings`/`PrinterSettings` with the printer's paper sizes, trays, resolutions, duplex, colour and hard margins, `Margins`, `PrinterUnitConvert`, `QueryPageSettings` per page (landscape pages mixed in), `OriginAtMargins`, cancel from `BeginPrint`/`PrintPage`. A page's `Graphics` works in 1/100 inch with text at its physical size and `DpiX` of the printer. **Linux/macOS**: the printers and their options come from CUPS (`lpstat`, `lpoptions`); a job is a PDF handed to `lp` with copies, collation, duplex and colour mode. **Windows**: winspool and the driver (`DeviceCapabilities`, DEVMODE), a job goes through GDI (each page drawn by Skia at up to 300 dpi). **Print to file** writes PDF. Preview (`PrintPreviewControl`/`Dialog`) keeps each page as a vector drawing and replays it sharp at any zoom, and works on a machine without printers. `GetHdevmode`/`SetHdevmode`/`GetHdevnames` hand out DEVMODE/DEVNAMES blocks in the Win32 layout. |
| **Help** | `Help.ShowHelp` opens files and URLs with the system program; `F1` raises `HelpRequested`. CHM viewers exist on Windows only. |
| **Sound** | `System.Media.SoundPlayer` is part of .NET and Windows-only there; NetForms does not add one. |

---

## 5. Windows-only API

NetForms controls have **no native window** (no HWND). Everything that assumes one compiles but does
nothing, or is not there:

| API | In NetForms |
|---|---|
| `Control.Handle`, `Form.Handle`, `ImageList.Handle` | `IntPtr.Zero`. `IsHandleCreated`, `HandleCreated`, `HandleDestroyed` follow the WinForms life cycle. |
| `WndProc`, `DefWndProc`, `CreateParams`, `CreateHandle`, `FromHandle`, `NativeWindow` | Overrides compile; the platform never calls them, messages are not synthesised. `NativeWindow` is missing. |
| P/Invoke into `user32`, `gdi32`, `comctl32`, `uxtheme`, `dwmapi`, `shell32`… | Throws `DllNotFoundException` on Linux (works on Windows, but the handles you pass are zero). The converter lists every such call with file and line. |
| `Microsoft.Win32.Registry`, `Application.UserAppDataRegistry` | Registry is Windows-only in .NET; `UserAppDataRegistry` is missing. The converter warns. |
| ActiveX (`AxHost`), COM interop, `WebBrowser`, WebView2, CefSharp | Not available and not planned. |
| `SendKeys`, `InputLanguage`, `SystemEvents`, power status | Missing. |
| `Screen` | Real monitors from the platform; `SystemInformation` returns what NetForms draws with, and Windows 10/11 defaults for the rest. |

---

## 6. Intentional differences

Differences that are known, measured and kept on purpose. Everything else that differs from WinForms
is a bug — please report it.

- **Look.** Controls look like WinForms with visual styles, but are drawn by NetForms, not by
  `uxtheme` — pixel identity with Windows is not a goal. The look is the same on Windows and Linux.
- **Windows-style paths in NetForms file APIs.** `Image.FromFile`, `new Bitmap(path)`, `new Icon(path)`,
  `Image.Save`, `PrivateFontCollection.AddFontFile` accept `\` and ignore case on Linux when the exact
  name does not exist — so `Application.StartupPath + @"\img\x.png"` keeps working. `System.IO.File`
  does not do this; the converter warns about such strings.
- **Drop-down menus** are about 10 px narrower: the image column is painted under the items rather
  than kept in the padding.
- **`NotifyIcon`**: Linux tray hosts report only primary clicks, so double clicks are synthesised,
  right-button events arrive only when a context menu opens, `MouseMove` never arrives, and the menu
  is drawn by the shell.
- **`ErrorProvider`** icons do not blink.
- **`DataGridView`**: an unhandled `DataError` is not shown in a message box (WinForms shows one); a bound grid has
  no row for new records yet (WinForms has one when the list allows adding); a `DefaultCellStyle` assigned with members unset
  gets them filled on the object (WinForms returns a filled copy from the getter).
- **`Form.TopLevel = false`**: an embedded form has no frame (WinForms draws one).
- **`RichTextBox.Rtf`** writes non-ASCII characters as `\uN?` (RichEdit uses `\'hh` in the font's code page); both are read.
- **Negative sizes** of an unparented docked control are clamped to 0 immediately (WinForms does it when the window is created).
- **Printing**: `PreviewPageInfo.Image` is a `Bitmap` (WinForms: an EMF `Metafile`, which NetForms does not have); print preview does not need a printer (WinForms throws `InvalidPrinterException` without one); print to file writes PDF (WinForms: what the driver produces); `IsDirectPrintingSupported` is always `false`; on Windows pages are sent to the driver as bitmaps (at most 300 dpi), not as GDI drawing commands.
- **Drag and drop** within the application does not go through the OS (NetForms runs the protocol), so a drag cannot yet leave the application, and `DoDragDrop`'s drag image is not drawn.
- **System colours**: `SystemColors.*` are .NET's own table on Linux (the classic Windows palette),
  the OS palette on Windows. The NetForms theme always paints with the Windows 10/11 light palette.

---

## 7. How compatibility is measured

- **Diff tests against the real WinForms** (`tests/NetForms.Compat`, Windows CI): the same scenarios
  run on `System.Windows.Forms` and on NetForms; positions, sizes, event order, text metrics, design-time
  attributes and what the designer serializes are compared. The suite: **483/483**; on Windows CI it runs with all three oracles of the real WinForms.
- **Golden rendering tests** (offscreen, identical images on Windows and Linux).
- **API coverage** — `dotnet run --project tools/NetForms.ApiDiff -- --markdown docs/api`
  regenerates [the tables](api/README.md). Today: **747** of 1254 types complete, **144** partial,
  **363** missing (most of them `EventArgs`, the accessible objects of individual controls and the removed 1.x controls).
- **Corpus of real projects** (`NETFORMS_CORPUS=1`, CI job `corpus`): converted and built without
  manual edits, with the reason for every failure recorded in `tests/corpus/corpus.json`.

---

## 8. What comes next

What to add first is decided by the code people write, not by the length of the list:
`NetForms.ApiDiff --usage` compiles every project of the corpus against the real WinForms and counts each reference
to a type or member NetForms lacks — [Missing API by use](api/usage.md). Of 25,887 references to the WinForms API in
63 projects (24 repositories), 11 types and members were missing at the last count, and each of them stopped a build;
printing has been added since (decision 153):

| Next | Code that needs it |
|---|---|
| `ImageList.Images.Add(string, Icon)` | Surviving-WinForms (GetStockIcon sample) |
| `LinkLabel.OverrideCursor` — the protected property a derived link label sets | xrails-login-ui (both projects) |
| `ImageFormat.Icon`/`Tiff`/`Wmf`, `OpenFileDialog.SafeFileName`, `TabControl.TabPages.Remove`, `new Font(FontFamily, float, FontStyle, GraphicsUnit, byte)` | MDBEditor, xrails-login-ui |

The corpus already builds almost entirely, so this list is short. Ranking the other missing types takes a wider
sample of open-source WinForms code — it only has to compile against the real WinForms, not build with NetForms.
