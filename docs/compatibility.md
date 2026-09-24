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

The member-by-member list — what exists and what is missing for each of the 1254 public types of
`System.Windows.Forms` and `System.Drawing.Common` — is generated: **[API coverage](api/README.md)**.

State as of version **0.1.0-preview.1** (September 2026).

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
.NET Framework 4.8/4.8.1 projects and **27 of 45** open-source WinForms projects convert and build with no
manual edit. The rest fail for the reasons in the last rows of the table (8 × BinaryFormatter,
4 × Windows-only components) or for API NetForms does not have yet (printing, some data binding).

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
| `ComboBox` | ✅ Works · 6 missing | `DropDown`, `DropDownList`, `Simple`; the list opens outside the form. `AutoCompleteMode` is stored, suggestions are not shown yet. |
| `DateTimePicker` | ✅ Works · 1 missing | Custom formats, field editing with arrows, drop-down calendar, `ShowUpDown`, `ShowCheckBox`. |
| `Label` | ✅ Works · 12 missing | Missing: `Image`/`ImageList` on a label, `PreferredWidth/Height`. |
| `LinkLabel` | ✅ Works · 3 missing | |
| `ListBox` | ✅ Works · 10 missing | Owner draw, multi-select. Missing: `CustomTabOffsets`, `Sort()` override hook. |
| `ListView` | ✅ Works · 43 missing | All views (Details, List, SmallIcon, LargeIcon, Tile), groups, check boxes, label edit, sorting. Missing: `VirtualMode`, column reordering by drag, `HotTracking`, insertion mark. |
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
| `TreeView` | ✅ Works · 9 missing | Check boxes, images, label edit, owner draw. Missing: `ItemDrag`, `NodeMouseHover`. |
| `WebBrowser` | ❌ Missing | Internet Explorer control; will not come (see §5). |
| `DomainUpDown` | ❌ Missing | |
| `HScrollBar`, `VScrollBar`, `TrackBar` | ✅ Works | |

### Containers

| Control | Status | Notes |
|---|---|---|
| `Panel`, `GroupBox` | ✅ Works · 1 missing each | `AutoScroll`, `AutoSize`, `BorderStyle`. |
| `FlowLayoutPanel` | ✅ Works · API complete | Layout engine ported from dotnet/winforms, including the `SetFlowBreak` quirk. |
| `TableLayoutPanel` | ✅ Works · 2 missing | Percent/absolute/auto-size rows and columns, spans. |
| `SplitContainer` | ✅ Works · 3 missing | |
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
| `DataGridView` | ✅ Works · 292 missing | Columns of every kind (text, check box, combo box, button, link, image) and per-cell types (`row.Cells[i] = new DataGridViewButtonCell()`), `CellContentClick`, editing, sorting, selection modes, auto-size policies, `DataSource` binding (lists, `DataTable`), frozen columns, virtual scrolling. Custom cells (a `DataGridViewCell` subclass overriding `Paint`) work; in their `Paint` the `cellStyle.Font` is `null` for now (a bug: use `DataGridView.Font`). Missing: `EditingControl` and `EditingControlShowing`, `CellValidating`, `CellParsing`, `CurrentCellDirtyStateChanged`, custom editing controls (`IDataGridViewEditingControl`, e.g. a date-picker column), `VirtualMode`; the rest of the missing count is mostly protected `Process*Key`/`On*Changed` hooks and `AutoResize*` methods. |
| `BindingSource` | ✅ Works · 10 missing | Over lists and a `DataTable`: `Position`, `Filter`, `Sort`, `AddNew`. Missing: `ApplySort`, `AllowNew`, `CurrencyManager`; a `DataSet` with `DataMember = "Table"` gives no rows yet, and a `BindingSource` over another one with a `DataRelation` as `DataMember` (master-detail) is empty. |
| `BindingNavigator` | ❌ Missing | |
| ADO.NET itself (`DataSet`, `DataTable`, `DataAdapter`, providers) | Part of .NET, not of WinForms: works on Linux as it is. Edits made in a bound grid set `RowState`, so `adapter.Update(table)` saves them. The provider decides the platform: SQL Server (`Microsoft.Data.SqlClient`), PostgreSQL, MySQL, SQLite, Firebird, Oracle run on Linux; **Access through `System.Data.OleDb` is Windows-only** (`PlatformNotSupportedException` on Linux, the converter warns). |
| `Control.DataBindings` (`Binding`) | ⚠️ Partial | Simple property binding works, over objects and `DataTable` rows, following `Position`; `BindingContext`/`CurrencyManager` are missing. `ComboBox`/`ListBox` with `DisplayMember`/`ValueMember` over a `DataTable` show empty text for now. |
| `PropertyGrid` | ✅ Works · 31 missing | Categories, type editors, expandable objects. Missing: the command pane and its colours. |

### Components and dialogs

| Component | Status |
|---|---|
| `Timer`, `ImageList`, `ErrorProvider`, `HelpProvider`* , `BackgroundWorker` | ✅ (`HelpProvider` ❌ missing; `BackgroundWorker` is part of .NET itself and works as-is) |
| `MessageBox`, `TaskDialog` | ✅ Own dialogs in the NetForms theme; button captions follow the UI language (English, Russian). |
| `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog` | ✅ Native dialogs of the OS (portal / GTK on Linux). A few properties missing (`ClientGuid`, custom places). |
| `ColorDialog`, `FontDialog` | ✅ Own dialogs. |
| `PrintDialog`, `PrintPreviewDialog`, `PrintPreviewControl`, `PageSetupDialog`, `PrintDocument` | ❌ Missing — the whole `System.Drawing.Printing` namespace. Next on the list. |
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
| **Clipboard** | Text goes through the OS clipboard both ways. Images, file lists, audio and custom formats work inside the application only. |
| **Drag and drop** | Events and `AllowDrop` exist so code compiles; the platform does not start or deliver drags yet (`DoDragDrop` returns `None`). |
| **IME** | Committed text from input methods (Chinese, Japanese, Korean…) arrives in `KeyPress`/`TextBox`. The composition string is shown by the input method's own window, not inline. |
| **Accessibility** | ❌ Not yet. `AccessibleObject` and UI Automation are missing; `AccessibleName`/`AccessibleDescription` are stored. Screen readers do not see NetForms controls. |
| **Printing** | ❌ Missing (see §3). |
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
- **`Form.TopLevel = false`**: an embedded form has no frame (WinForms draws one).
- **`RichTextBox.Rtf`** writes non-ASCII characters as `\uN?` (RichEdit uses `\'hh` in the font's code page); both are read.
- **Negative sizes** of an unparented docked control are clamped to 0 immediately (WinForms does it when the window is created).
- **System colours**: `SystemColors.*` are .NET's own table on Linux (the classic Windows palette),
  the OS palette on Windows. The NetForms theme always paints with the Windows 10/11 light palette.

---

## 7. How compatibility is measured

- **Diff tests against the real WinForms** (`tests/NetForms.Compat`, Windows CI): the same scenarios
  run on `System.Windows.Forms` and on NetForms; positions, sizes, event order, text metrics, design-time
  attributes and what the designer serializes are compared. The suite: **381/381**; on Windows CI it runs with all three oracles of the real WinForms.
- **Golden rendering tests** (offscreen, identical images on Windows and Linux).
- **API coverage** — `dotnet run --project tools/NetForms.ApiDiff -- --markdown docs/api`
  regenerates [the tables](api/README.md). Today: **633** of 1254 types complete, **164** partial,
  **457** missing (most of them `EventArgs`, accessibility, printing and the removed 1.x controls).
- **Corpus of real projects** (`NETFORMS_CORPUS=1`, CI job `corpus`): converted and built without
  manual edits, with the reason for every failure recorded in `tests/corpus/corpus.json`.
