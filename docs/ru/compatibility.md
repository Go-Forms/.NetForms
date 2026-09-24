# Совместимость

NetForms — копия Windows Forms. **Справочник по API — от Microsoft:** каждый тип, член, событие и
значение по умолчанию должны быть ровно такими, как описано в
[learn.microsoft.com/ru-ru/dotnet/api/system.windows.forms](https://learn.microsoft.com/ru-ru/dotnet/api/system.windows.forms)
и в [документации по Windows Forms](https://learn.microsoft.com/ru-ru/dotnet/desktop/winforms/).
Здесь — только то, чего эта документация сказать не может:

1. [На чём работает NetForms](#1-на-чём-работает-netforms) — версии .NET, операционные системы.
2. [Какие проекты переносятся, а какие нет](#2-какие-проекты-переносятся).
3. [Контролы и компоненты](#3-контролы-и-компоненты) — состояние каждого элемента панели.
4. [Подсистемы](#4-подсистемы) — раскладка, рисование, текст, ресурсы, данные, буфер обмена, печать…
5. [API, существующий только в Windows](#5-api-только-для-windows) — `Handle`, `WndProc`, P/Invoke, COM.
6. [Осознанные отличия](#6-осознанные-отличия) от WinForms.
7. [Как измеряется совместимость](#7-как-измеряется-совместимость).

Полный список по членам — что есть и чего нет для каждого из 1254 публичных типов
`System.Windows.Forms` и `System.Drawing.Common` — генерируется автоматически:
**[покрытие API](../api/README.md)** (на английском).

Состояние на версию **0.1.0-preview.2** (сентябрь 2026).

---

## 1. На чём работает NetForms

### .NET

| Целевая платформа (TFM) | Работает? | Почему |
|---|---|---|
| `net10.0` | ✅ **Да — только она** | В пакетах есть только `lib/net10.0`. |
| `net10.0-windows` | ✅ Только Windows | Для проектов «только под Windows», которым нужна отрисовка NetForms (`netforms-convert --target windows`). TFM с `-windows` на Linux не собирается. |
| `net9.0`, `net8.0`, `net7.0`, `net6.0`, `net5.0`, `netcoreapp3.1` | ❌ Нет | NuGet выдаёт `NU1202: Package NetForms … is not compatible with net8.0`. Перейдите на `net10.0` — конвертер делает это сам. |
| `net8.0-windows` + `UseWindowsForms` (любой TFM с `-windows` и настоящим WinForms) | ❌ Не на Linux | При сборке — `NETSDK1100`; с `EnableWindowsTargeting=true` собирается, но при запуске: *Framework: 'Microsoft.WindowsDesktop.App' … No frameworks were found*. Именно это NetForms и заменяет. |
| .NET Framework 2.0 – 4.8.1 (`net48`, `net472`, …) | ❌ Не напрямую | NetForms — не библиотека .NET Framework. Конвертер переводит такие проекты в SDK-формат на `net10.0` (см. §2). |
| Библиотеки `netstandard2.0/2.1` | ✅ Если не используют WinForms | Библиотека .NET Standard и так не может ссылаться на WinForms; обычные библиотеки с логикой работают как есть. |
| Mono (`mono`, `msbuild` из Mono) | ❌ Нет | У Mono свой старый `System.Windows.Forms` на libgdiplus; NetForms нужна среда .NET 10. |

Для сборки нужен **.NET 10 SDK**, для запуска — **среда выполнения .NET 10** (или публикация
self-contained, со средой внутри).

### Операционные системы

| ОС | Состояние |
|---|---|
| Windows 10 / 11, x64 и arm64 | ✅ Поддерживается, проверяется в CI (`windows-latest`). |
| Linux x64 / arm64 с X11 или Wayland через XWayland | ✅ Поддерживается, проверяется в CI (`ubuntu-latest`), на Ubuntu 24.04/26.04 и WSL. |
| macOS | ⚠️ Не цель этой версии. Avalonia и SkiaSharp там работают и нативные библиотеки в пакетах есть, но ничего не проверяется. |
| Windows 7 / 8.1, 32-битная Windows, Linux x86/ARM32 | ❌ .NET 10 там не работает, либо нет нативных библиотек. |
| Браузер (WebAssembly), Android, iOS | ❌ Не цель. WinForms — API для настольных приложений. |

**Пакеты Linux**, нужные приложению при запуске: среда .NET 10 (с ICU), fontconfig, `libX11`,
`libICE`, `libSM` (X11-бэкенд Avalonia) и хотя бы одно семейство TrueType-шрифтов. В настольном
дистрибутиве всё это уже есть; команды для Debian/Ubuntu/Astra, Fedora/РЕД ОС и ALT — в разделе
[Установка и настройка](install.md#2-системные-библиотеки-linux).

Если шрифт `Segoe UI` (шрифт WinForms по умолчанию) не установлен, берётся ближайший установленный
шрифт без засечек; метрики текста тогда немного отличаются от Windows (см. §4, «Текст»).

---

## 2. Какие проекты переносятся

| Что у вас | Что будет |
|---|---|
| WinForms-проект в SDK-формате, .NET Core 3.1 – .NET 10 (`<UseWindowsForms>true</UseWindowsForms>`) | ✅ `netforms-convert App.csproj --apply`: `UseWindowsForms` → `PackageReference NetForms`, `net8.0-windows` → `net10.0`, два неявных `using` прописываются явно. Код не трогается. |
| Проект .NET Framework 2.0 – 4.8.1 в старом формате (`ToolsVersion`, `packages.config`, `<Reference Include="System.Windows.Forms" />`) | ✅ Конвертер сам переводит его в SDK-формат (upgrade-assistant не нужен): список файлов, `DependentUpon`, ресурсы, `packages.config` → `PackageReference`, подпись `.snk`, `AssemblyInfo.cs`. Библиотеки того же решения без WinForms тоже переводятся на `net10.0`. |
| Решение целиком (`.sln`, `.slnx`) или папка | ✅ `netforms-convert MySolution.sln --apply` переводит каждый проект. |
| WinForms-проект на VB.NET (`.vbproj`) | ❌ Не поддерживается. Дизайнер, шаблоны и конвертер — только для C#; прикладная модель `Microsoft.VisualBasic` (`My.Application`, `WindowsFormsApplicationBase`) не реализована. |
| WinForms-проект на C++/CLI (`.vcxproj`) | ❌ C++/CLI в .NET есть только под Windows. |
| WPF или WinForms с WPF внутри (`ElementHost`, `UseWPF`) | ❌ Это не WinForms. Конвертер сообщает об `UseWPF`. |
| Проект с NuGet-пакетом, у которого `FrameworkReference Microsoft.WindowsDesktop.App` | ❌ Такой пакет требует настоящую среду Windows Desktop (`NETSDK1136`). Ищите кроссплатформенную версию пакета. |
| Проект с `BinaryFormatter` (напрямую или в `.resx`, кроме картинок `ImageList`) | ❌ `BinaryFormatter` удалён из .NET 9+ — на любой ОС, не только в NetForms. `ImageList.ImageStream` из дизайнера VS NetForms читает сам. |
| Проект с WebView2, CefSharp, ActiveX (`AxHost`), COM-ссылками | ❌ Нативные компоненты только для Windows. Конвертер отмечает `COMReference` как ошибку. |
| Сторонние пакеты контролов WinForms из NuGet (ZedGraph, OxyPlot.WindowsForms, ScottPlot.WinForms, FastColoredTextBox, ObjectListView, DockPanelSuite…) | ❌ Пока нет. Проверено на этих шести 2026-09-24: пакеты для .NET Framework собраны против подписанной сборки `System.Windows.Forms, PublicKeyToken=b77a5c561934e089`, а фасад NetForms ею не является, и сборка останавливается с `CS0012`; пакеты для `net*-windows` требуют среду Windows Desktop (`NETSDK1136`, DockPanelSuite). Контролы, исходники которых лежат в вашем проекте, компилируются как ваш собственный код. |

Конвертер ничего не угадывает: без `--apply` он показывает, что изменит, и каждое место, которое не
будет работать на Linux (с файлом и строкой); после `--apply` исходный проект лежит рядом с новым как
`*.csproj.winforms.bak`. Подробнее — [Перевод WinForms-проекта](migrating.md).

Измерено на корпусе реальных проектов (`tests/corpus/corpus.json`): **7 из 7** проектов заказчика на
.NET Framework 4.8/4.8.1 и **28 из 45** открытых WinForms-проектов переводятся и собираются без ручных
правок. Остальные не проходят по причинам из последних строк таблицы (8 × BinaryFormatter,
4 × компоненты только для Windows) или из-за API, которого в NetForms пока нет (печать, системные значки).

---

## 3. Контролы и компоненты

Обозначения. **Работает** — реализовано, рисуется NetForms, поведение сверено с настоящим WinForms
дифф-тестами. **API полный** — есть каждый публичный и защищённый член; *нет N* — список в
[покрытии API](../api/System.Windows.Forms.md).

### Основные контролы

| Контрол | Состояние | Примечания |
|---|---|---|
| `Button` | ✅ Работает · нет 1 | `Image`, `ImageList`, `TextImageRelation`, `FlatStyle`, `FlatAppearance`, `DialogResult`, `AcceptButton`/`CancelButton`. |
| `CheckBox` | ✅ Работает · нет 1 | Три состояния, `Appearance.Button`, размеры AutoSize как в VS (`checkBox1` → 83×19). |
| `CheckedListBox` | ✅ Работает · нет 2 | |
| `ComboBox` | ✅ Работает · нет 4 | `DropDown`, `DropDownList`, `Simple`; список открывается за пределами формы. `DataSource` с `DisplayMember`/`ValueMember` (списки, `DataTable`, пути в `DataSet`), выбор следует за позицией источника данных. `AutoCompleteMode` сохраняется, подсказки пока не показываются. |
| `DateTimePicker` | ✅ Работает · нет 1 | Свои форматы, правка полей стрелками, выпадающий календарь, `ShowUpDown`, `ShowCheckBox`. |
| `Label` | ✅ Работает · нет 12 | Нет: `Image`/`ImageList` у надписи, `PreferredWidth/Height`. |
| `LinkLabel` | ✅ Работает · нет 3 | |
| `ListBox` | ✅ Работает · нет 7 | Owner draw, множественный выбор, `DataSource`/`DisplayMember`/`ValueMember`, как у `ComboBox`. Нет: `CustomTabOffsets`, переопределяемого `Sort()`. |
| `ListView` | ✅ Работает · нет 43 | Все виды (Details, List, SmallIcon, LargeIcon, Tile), группы, флажки, правка подписей, сортировка. Нет: `VirtualMode`, перестановки столбцов мышью, `HotTracking`, метки вставки. |
| `MaskedTextBox` | ✅ Работает · нет 1 | Маски через тот же `MaskedTextProvider`, что и в WinForms. |
| `MonthCalendar` | ✅ Работает · нет 8 | Показывается один месяц (`CalendarDimensions` сохраняется). |
| `NotifyIcon` | ✅ Работает · API полный | Значок в трее через ОС (StatusNotifierItem на Linux); контекстное меню рисует оболочка. См. §6. |
| `NumericUpDown` | ✅ Работает · нет 3 | |
| `PictureBox` | ✅ Работает · нет 9 | Все `SizeMode`. `Load()`/`Load(path)` читают файл; одно присваивание `ImageLocation` его не загружает, URL не скачиваются. Нет `LoadAsync`. |
| `ProgressBar` | ✅ Работает · нет 5 | Включая `Marquee`. |
| `RadioButton` | ✅ Работает · нет 1 | |
| `RichTextBox` | ✅ Работает · нет 1 | Своё чтение и запись RTF: шрифты, цвета, жирный/курсив/подчёркивание, маркированные списки, выравнивание, ссылки, отмена. Пока нет: нумерованных списков, картинок/OLE-объектов, выравнивания по ширине, перетаскивания, IME. |
| `TextBox` | ✅ Работает · нет 1 | Выделение, отмена, буфер обмена, контекстное меню, многострочный режим, пароль, `CharacterCasing`. `AutoCompleteCustomSource` сохраняется, подсказки не показываются. |
| `ToolTip` | ✅ Работает · нет 2 | |
| `TreeView` | ✅ Работает · нет 9 | Флажки, картинки, правка подписей, owner draw. Нет: `ItemDrag`, `NodeMouseHover`. |
| `WebBrowser` | ❌ Нет | Контрол Internet Explorer; не появится (см. §5). |
| `DomainUpDown` | ❌ Нет | |
| `HScrollBar`, `VScrollBar`, `TrackBar` | ✅ Работают | |

### Контейнеры

| Контрол | Состояние | Примечания |
|---|---|---|
| `Panel`, `GroupBox` | ✅ Работают · нет по 1 | `AutoScroll`, `AutoSize`, `BorderStyle`. |
| `FlowLayoutPanel` | ✅ Работает · API полный | Движок раскладки перенесён из dotnet/winforms, вместе с особенностью `SetFlowBreak`. |
| `TableLayoutPanel` | ✅ Работает · нет 2 | Строки и столбцы в процентах, пикселях и по содержимому, объединение ячеек. |
| `SplitContainer` | ✅ Работает · нет 3 | |
| `TabControl` | ✅ Работает · нет 13 | Нет: `ImageList` у вкладок, `DeselectTab`, `RightToLeftLayout`. |
| `Splitter` | ❌ Нет | Используйте `SplitContainer`. |
| `UserControl` | ✅ Работает · API полный | |
| `Form` | ✅ Работает · нет 34 | Модальные и немодальные, `Owner`, `TopMost`, `StartPosition`, `FormBorderStyle`, `AcceptButton`, `KeyPreview`, MDI, `TopLevel = false`. Нет: `ShowAsync`/`ShowDialogAsync` (.NET 9+), цветов заголовка и скругления углов (только Windows 11), `DpiChanged`. |
| MDI (`IsMdiContainer`, `MdiParent`, `LayoutMdi`, `MdiWindowListItem`) | ✅ Работает | |

### Меню и панели инструментов

| Контрол | Состояние | Примечания |
|---|---|---|
| `MenuStrip`, `ContextMenuStrip`, `ToolStrip`, `StatusStrip` | ✅ Работают · нет немногого | Все стандартные элементы (`ToolStripButton`, `…MenuItem`, `…DropDownButton`, `…SplitButton`, `…ComboBox`, `…TextBox`, `…ProgressBar`, `…Label`, `…Separator`), горячие клавиши, мнемоники, переполнение, professional-рендерер. Нет: перетаскивания элементов (`AllowItemReorder`), объектов настроек `LayoutStyle`. |
| `ToolStripContainer`, `ToolStripPanel` | ❌ Нет | Пристыковываемые перетаскиваемые панели. |
| `MainMenu`, `ContextMenu`, `ToolBar`, `StatusBar`, `DataGrid` | ❌ Нет | Контролы .NET Framework 1.x — **удалены из самого .NET** в .NET Core 3.1; в настоящем WinForms на .NET 10 они тоже не работают. Замените на `MenuStrip`, `ContextMenuStrip`, `ToolStrip`, `StatusStrip`, `DataGridView`. |

### Данные

| Контрол | Состояние | Примечания |
|---|---|---|
| `DataGridView` | ✅ Работает · нет 242 | Столбцы всех видов (текст, флажок, список, кнопка, ссылка, картинка) и типы отдельных ячеек (`row.Cells[i] = new DataGridViewButtonCell()`), `CellContentClick`, правка, сортировка, режимы выделения, политики ширин, привязка `DataSource`/`DataMember` через `BindingContext` формы (списки, `DataTable`, таблица `DataSet`, отношение для мастер-детали; текущая строка и `Position` источника следуют друг за другом; отношения не становятся столбцами), закреплённые столбцы, виртуальная прокрутка. Свои ячейки (наследник `DataGridViewCell` с переопределённым `Paint`) работают и получают унаследованный стиль вместе со шрифтом; стили по умолчанию несут шрифт сетки и следуют за ним, как в WinForms. Правка — по модели WinForms: контрол `EditType` ячейки (`DataGridViewTextBoxEditingControl`, `DataGridViewComboBoxEditingControl` или свой `IDataGridViewEditingControl` — столбец-календарь из документации Microsoft работает как опубликован) в `EditingPanel`, `EditingControlShowing`, «грязная» ячейка и `CurrentCellDirtyStateChanged`, `CellValidating`/`CellParsing`/`CellValidated` и `DataError` при фиксации, `CellEnter`/`CellLeave`/`RowEnter`/`RowLeave`/`RowValidating`, `BeginEdit`/`EndEdit`/`CommitEdit`/`CancelEdit`/`RefreshEdit`, `EditMode`, ввод/F2/Enter/Escape/Tab; ячейка-флажок правит себя сама (`IDataGridViewEditingCell`) и фиксируется при уходе, как в WinForms. `VirtualMode` с `RowCount`, `CellValueNeeded`/`CellValuePushed`, `RowDirtyStateNeeded`, `CancelRowEdit`. Нет: потока новой строки `NewRowNeeded`/`UserAddedRow`, копирования в буфер, остальных защищённых `Process*Key`/`On*Changed` и методов `AutoResize*`. |
| `BindingSource` | ✅ Работает · API: полный | Сама реализация WinForms (взята из dotnet/winforms): списки, `DataTable`, `DataSet` с таблицей в `DataMember`, мастер-деталь — второй `BindingSource` с `DataRelation` в `DataMember`, `Position`, `Filter`, `Sort`, `AddNew`, `CurrencyManager`. |
| `BindingNavigator` | ❌ Нет | |
| Сам ADO.NET (`DataSet`, `DataTable`, `DataAdapter`, провайдеры) | Часть .NET, а не WinForms: на Linux работает как есть. Правка в привязанной сетке выставляет `RowState`, поэтому `adapter.Update(table)` её сохраняет. Платформу определяет провайдер: SQL Server (`Microsoft.Data.SqlClient`), PostgreSQL, MySQL, SQLite, Firebird, Oracle работают на Linux; **Access через `System.Data.OleDb` — только Windows** (на Linux — `PlatformNotSupportedException`, конвертер предупреждает). |
| `Control.DataBindings`, `Binding`, `BindingContext`, `CurrencyManager` | ✅ Работает · API: полный | Сама реализация WinForms (взята из dotnet/winforms) с её правилами: контрол привязывается, когда он создан и у него есть `BindingContext` (форма даёт и то и другое); по умолчанию `DataSourceUpdateMode.OnValidation` — значение уходит в источник при проверке контрола; `Format`/`Parse`, `FormatString`, `NullValue`, `BindingComplete` (при включённом форматировании), `ErrorProvider` над `IDataErrorInfo`. Дизайнер читает и пишет `DataBindings.Add(new Binding(...))` так же, как Visual Studio. |
| `PropertyGrid` | ✅ Работает · нет 31 | Категории, редакторы типов, раскрываемые объекты. Нет: панели команд и её цветов. |

### Компоненты и диалоги

| Компонент | Состояние |
|---|---|
| `Timer`, `ImageList`, `ErrorProvider`, `HelpProvider`, `BackgroundWorker` | ✅ (`HelpProvider` ❌ нет; `BackgroundWorker` — часть самого .NET и работает как есть) |
| `MessageBox`, `TaskDialog` | ✅ Свои диалоги в теме NetForms; надписи на кнопках — на языке интерфейса (английский, русский). |
| `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog` | ✅ Системные диалоги ОС (портал / GTK на Linux). Нет нескольких свойств (`ClientGuid`, свои места). |
| `ColorDialog`, `FontDialog` | ✅ Свои диалоги. |
| `PrintDialog`, `PrintPreviewDialog`, `PrintPreviewControl`, `PageSetupDialog`, `PrintDocument` | ❌ Нет — всего пространства имён `System.Drawing.Printing`. Следующее в очереди. |
| `NotifyIcon` | ✅ См. выше. |

---

## 4. Подсистемы

| Область | Состояние |
|---|---|
| **Цикл сообщений** | `Application.Run(Form)`, `Run(ApplicationContext)`, `DoEvents`, `Exit`, `Idle`, `ThreadException` (необработанное исключение в обработчике показывает диалог «продолжить/выйти», как в WinForms), `IMessageFilter` (видит `WM_KEYDOWN`/`WM_KEYUP`). `ApplicationConfiguration.Initialize()` работает. `Control.Invoke`/`BeginInvoke`/`InvokeAsync` передают вызов в UI-поток. |
| **Раскладка** | `Anchor`, `Dock`, `Margin`/`Padding`, `AutoSize`/`AutoSizeMode`, `MinimumSize`/`MaximumSize`, `SuspendLayout`/`ResumeLayout`, `AutoScroll`. Координаты совпадают с настоящим WinForms — это проверяют дифф-тесты при каждой сборке. |
| **Фокус и клавиатура** | Порядок обхода, `TabStop`, мнемоники (`&Файл`), `AcceptButton`/`CancelButton`, `ProcessCmdKey`/`ProcessDialogKey`/`IsInputKey`/`PreviewKeyDown`, оба порядка событий фокуса WinForms (`Select()` и `Focus()`), проверка (`Validating`/`Validated`, `AutoValidate`). |
| **Мышь** | Щелчок, двойной щелчок, захват, колесо, вход/выход/задержка, курсоры (именованные; курсор из файла `.cur` показывается стрелкой). `Cursor.Position` можно прочитать, но не задать. |
| **Рисование (`System.Drawing`)** | `Graphics` на SkiaSharp: линии, фигуры, пути, кривые, регионы, отсечение, преобразования, `LinearGradientBrush`, `HatchBrush`, `TextureBrush`, картинки с `ImageAttributes`/`ColorMatrix`, `LockBits`, `RotateFlip`, значки, `ControlPaint`. Нет: `PathGradientBrush`, `BufferedGraphics`, `ImageAnimator` (анимированный GIF), метафайлов (`Metafile`, EMF/WMF); `Graphics.CopyFromScreen` бросает `NotSupportedException`. Картинки декодирует Skia (PNG, JPEG, BMP, GIF — первый кадр, ICO, WebP); `Image.Save` пишет PNG, JPEG и WebP, для BMP и GIF — `NotSupportedException`. |
| **Текст** | Эталон — `TextRenderer.MeasureText`/`DrawText` (метрики GDI), `Graphics.MeasureString`/`DrawString` следуют правилам GDI+. С тем же файлом шрифта числа совпадают с Windows; с заменой (на Linux нет Segoe UI) отличаются на пиксель-два. Зеркалирование `RightToLeftLayout` не реализовано. |
| **DPI** | Раскладка идёт в логических пикселях 96 DPI, платформа масштабирует на экран (на HiDPI чётко). `AutoScaleMode`/`AutoScaleDimensions` сохраняются; `DeviceDpi` всегда 96, `DpiChanged` не возникает. |
| **Тема** | Одна встроенная светлая тема в духе WinForms с визуальными стилями. `Application.SetColorMode(Dark)` принимается, тёмная тема пока не рисуется. Типы `VisualStyleRenderer` есть; рисуют они через тему NetForms. |
| **Ресурсы (`.resx`)** | Строки, картинки, значки, типизированные значения, `ResXFileRef` читаются при выполнении через `ComponentResourceManager`, как в WinForms. Дизайнер открывает формы с `Localizable = true`, но пока не записывает их. `ImageList.ImageStream` (BinaryFormatter в формате дизайнера VS) NetForms читает; прочие ресурсы в BinaryFormatter — нет (см. §2). |
| **Настройки** | `Properties.Settings` (`ApplicationSettingsBase`), `ConfigurationManager`, `app.config` компилируются и работают: пакет NetForms приносит `System.Configuration.ConfigurationManager`, как и среда Windows Desktop. |
| **Буфер обмена** | Текст ходит через буфер ОС в обе стороны. Картинки, списки файлов, звук и свои форматы — только внутри приложения. |
| **Перетаскивание** | События и `AllowDrop` есть, чтобы код компилировался; платформа пока не начинает и не доставляет перетаскивание (`DoDragDrop` возвращает `None`). |
| **IME** | Готовый текст от методов ввода (китайский, японский, корейский…) приходит в `KeyPress`/`TextBox`. Строку набора показывает окно самого метода ввода, не сам контрол. |
| **Специальные возможности** | ❌ Пока нет. `AccessibleObject` и UI Automation отсутствуют; `AccessibleName`/`AccessibleDescription` сохраняются. Экранные дикторы контролы NetForms не видят. |
| **Печать** | ❌ Нет (см. §3). |
| **Справка** | `Help.ShowHelp` открывает файлы и URL системной программой; `F1` вызывает `HelpRequested`. Просмотрщики CHM есть только в Windows. |
| **Звук** | `System.Media.SoundPlayer` — часть .NET и там только для Windows; NetForms своего не добавляет. |

---

## 5. API только для Windows

У контролов NetForms **нет собственного окна ОС** (нет HWND). Всё, что его предполагает, компилируется,
но ничего не делает, или отсутствует:

| API | В NetForms |
|---|---|
| `Control.Handle`, `Form.Handle`, `ImageList.Handle` | `IntPtr.Zero`. `IsHandleCreated`, `HandleCreated`, `HandleDestroyed` следуют жизненному циклу WinForms. |
| `WndProc`, `DefWndProc`, `CreateParams`, `CreateHandle`, `FromHandle`, `NativeWindow` | Переопределения компилируются; платформа их не вызывает, сообщения не синтезируются. `NativeWindow` нет. |
| P/Invoke в `user32`, `gdi32`, `comctl32`, `uxtheme`, `dwmapi`, `shell32`… | На Linux — `DllNotFoundException` (в Windows работает, но передаваемые дескрипторы — нули). Конвертер перечисляет каждый такой вызов с файлом и строкой. |
| `Microsoft.Win32.Registry`, `Application.UserAppDataRegistry` | Реестр в .NET есть только в Windows; `UserAppDataRegistry` нет. Конвертер предупреждает. |
| ActiveX (`AxHost`), COM, `WebBrowser`, WebView2, CefSharp | Нет и не планируется. |
| `SendKeys`, `InputLanguage`, `SystemEvents`, состояние питания | Нет. |
| `Screen` | Настоящие мониторы от платформы; `SystemInformation` отдаёт то, чем рисует NetForms, остальное — значения Windows 10/11 по умолчанию. |

---

## 6. Осознанные отличия

Отличия, которые известны, измерены и оставлены сознательно. Всё остальное, что отличается от WinForms, —
ошибка; пожалуйста, сообщите о ней.

- **Внешний вид.** Контролы выглядят как WinForms с визуальными стилями, но рисует их NetForms, а не
  `uxtheme`, — попиксельного совпадения с Windows не добиваемся. Вид одинаков на Windows и Linux.
- **Пути в стиле Windows в файловых API NetForms.** `Image.FromFile`, `new Bitmap(path)`, `new Icon(path)`,
  `Image.Save`, `PrivateFontCollection.AddFontFile` на Linux понимают `\` и не различают регистр, если
  точного имени нет, — так продолжает работать `Application.StartupPath + @"\img\x.png"`. `System.IO.File`
  так не умеет; конвертер предупреждает о таких строках.
- **Выпадающие меню** примерно на 10 px уже: колонка картинок рисуется под элементами, а не в отступе.
- **`NotifyIcon`**: оболочки Linux сообщают только о щелчке основной кнопкой, поэтому двойной щелчок
  синтезируется, события правой кнопки приходят только при открытии меню, `MouseMove` не приходит,
  меню рисует оболочка.
- **Значки `ErrorProvider`** не мигают.
- **`DataGridView`**: необработанный `DataError` не показывается окном сообщения (WinForms показывает); первая
  ячейка не становится текущей при создании окна (в WinForms становится); назначенный `DefaultCellStyle` с
  незаданными членами дозаполняется сам (WinForms отдаёт из геттера заполненную копию).
- **`Form.TopLevel = false`**: у встроенной формы нет рамки (WinForms её рисует).
- **`RichTextBox.Rtf`** записывает не-ASCII символы как `\uN?` (RichEdit — как `\'hh` в кодовой странице
  шрифта); читаются оба варианта.
- **Отрицательные размеры** докнутого контрола без родителя сразу обрезаются до 0 (WinForms делает это
  при создании окна).
- **Системные цвета**: `SystemColors.*` на Linux — собственная таблица .NET (классическая палитра Windows),
  в Windows — палитра ОС. Тема NetForms всегда рисует светлой палитрой Windows 10/11.

---

## 7. Как измеряется совместимость

- **Дифф-тесты против настоящего WinForms** (`tests/NetForms.Compat`, CI на Windows): одни и те же сценарии
  выполняются в `System.Windows.Forms` и в NetForms; сравниваются положения, размеры, порядок событий,
  метрики текста, атрибуты времени разработки и то, что записывает дизайнер. Набор — **402/402**; в CI на Windows он идёт со всеми тремя оракулами настоящего WinForms.
- **Golden-тесты отрисовки** (без окна, одинаковые картинки на Windows и Linux).
- **Покрытие API** — `dotnet run --project tools/NetForms.ApiDiff -- --markdown docs/api` заново строит
  [таблицы](../api/README.md). Сейчас: **662** из 1254 типов полные, **162** частично, **430** нет
  (в основном `EventArgs`, специальные возможности, печать и удалённые контролы 1.x).
- **Корпус реальных проектов** (`NETFORMS_CORPUS=1`, задача CI `corpus`): проекты переводятся и
  собираются без ручных правок, причина каждого провала записана в `tests/corpus/corpus.json`.
