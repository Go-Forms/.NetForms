# .NetForms — перенос идеи GoForms на C#

## Контекст

Сейчас есть `GoForms` — WinForms-подобный фреймворк на Go поверх Fyne:

| Модуль | Объём | Что это |
|---|---|---|
| `GoForms/` | ~7.8k строк кода + 2.5k тестов | ядро: `Control`, `Form`, `Event[T]`, 40+ контролов, Anchor/Dock |
| `GoFormsDesigner/` | ~7k строк TS/JS (webview) + 6.3k строк Go (CLI) | визуальный дизайнер для VS Code, читает/пишет `*-designer.go` |
| `GoFormsShowcase/`, `GoFormsDemo/` | ~2.5k строк | витрина и демо |

**Цель заказчика:** «.NetForms» — максимально полная копия WinForms на C#, работающая
одинаково на Windows и Linux, с той же моделью разработки (`Program.cs` как точка входа,
два файла на форму: `MainForm.cs` + `MainForm.Designer.cs` с `InitializeComponent()`)
и с визуальным редактором, как в самой Visual Studio.

**Почему вопрос вообще возник.** README GoForms честно перечисляет то, во что упёрлась
реализация на Fyne — и это не мелочи, а системные ограничения:

1. **Хит-тест.** Fyne отдаёт событие первому объекту, реализующему любой интерфейс
   взаимодействия. Пришлось класть прозрачный `interactionArea` поверх каждого виджета
   и вручную пробрасывать события вниз — `interaction.go`, 319 строк чистого обхода.
2. **Стилизация.** Fyne не даёт per-instance шрифт/цвет для встроенных виджетов. Отсюда
   `FontSupported()` — API, который говорит «на этом контроле шрифт работает, а на этом нет».
3. **Нет колбэка ресайза окна** → собственный `fyne.Layout` на каждом контейнере только
   ради того, чтобы узнать о резайзе и поднять `Form.Resize`.
4. **Диалоги.** Fyne рисует диалоги на UI-горутине, поэтому блокирующий `ShowDialog` —
   дедлок. Весь API диалогов уехал на колбэки, а `ShowDialog()` нельзя звать из обработчика.
5. **`Handled` — фикция.** Fyne не даёт отменить уже отправленное событие клавиатуры;
   модификаторы приходят только с мышиными событиями, не с клавиатурными.
6. **`widget.Table` владеет своим скроллом** → 845 строк `datagridview.go` с ручной
   политикой ширин и «выращиванием» контрола вместо вертикального скролла.
7. **`widget.Card` вставляет собственные отступы** → `GroupBox` рисует рамку сам.
8. **Go не даёт виртуальной диспетчеризации через встраивание** → `SetBounds`/`SetSize`/
   `SetLocation` переобъявлены вручную в `panel.go`, `groupbox.go`, `scrollbox.go`,
   `splitter.go`, `statusstrip.go`, `numericupdown.go`. Забудешь — контрол молча
   игнорирует ресайз.

Пункты 1–7 — про Fyne, пункт 8 — про Go. **В C# на Avalonia исчезают все восемь.**

---

## 1. Прямой ответ: есть ли в C# аналог Fyne

Да, и не один — но «аналог» распадается на два разных уровня.

**Fyne = окно + GPU-канвас + свой набор виджетов, нарисованных программно.**
В .NET это два отдельных слоя, и это к лучшему — можно взять только нижний.

| Кандидат | Модель | Рендер | Linux | Вердикт для нас |
|---|---|---|---|---|
| **Avalonia 12** | retained-mode, свой композитор | **Skia, рисует всё сам** | X11 + Wayland, first-class | **Ближайший аналог Fyne и лучший выбор.** Пиксель-в-пиксель одинаково на всех ОС |
| **SkiaSharp + Silk.NET/OpenTK** | только окно + канвас | Skia | да | «Fyne без виджетов». Максимальный контроль, но нужно писать ввод, IME, буфер обмена, DPI самому |
| **Eto.Forms** | Forms-подобный API | **нативные бэкенды** (WinForms/WPF/GTK/Cocoa) | да | Отпадает: разный вид и разное поведение на разных ОС — прямо противоречит «одинаково везде» |
| **Uno Platform** | WinUI/XAML | Skia или нативно | да | Тяжёлый, XAML-центричный, чужая модель |
| **.NET MAUI** | XAML | нативно | **нет desktop Linux** | Отпадает |
| **Gtk#/GirCore** | GTK | GTK | да | GTK-вид, не WinForms |
| **ImGui.NET** | immediate-mode | OpenGL/Vulkan | да | Другая парадигма, нет состояния контролов |
| **System.Windows.Forms** | — | GDI+/comctl32 | **только Windows** | Не решение, но **эталон и источник истины** |

Avalonia рисует UI сама через Skia, а не оборачивает нативные контролы, поэтому даёт
идентичный результат на всех платформах; на сентябрь 2026 это версия 12.x с новым
composition-рендерером, поддержкой Native AOT и заметной коммерческой базой.

**Ключевое отличие от Fyne — Avalonia отдаёт нам всё, чего не хватало:**

| Что нужно WinForms | Fyne | Avalonia |
|---|---|---|
| Абсолютное позиционирование | через свой `fyne.Layout` | `Canvas` из коробки (и мы вообще не обязаны им пользоваться) |
| Виртуальные методы (`OnPaint`, `OnResize`) | нет в Go | `virtual`/`override` — родная модель |
| Роутинг событий с реальным `Handled` | нет | tunneling + bubbling, `e.Handled = true` работает |
| Модификаторы на клавиатуре | нет | `KeyModifiers` на каждом key-событии |
| Модальный `ShowDialog()` | дедлок | `ShowDialog(owner)` с вложенным циклом/await |
| Per-instance шрифт и цвет | нет | любой контрол, любой кисти |
| Своя отрисовка | ограниченно | `Render(DrawingContext)` + прямой доступ к `SKCanvas` через Skia-lease |
| Событие ресайза | нет | `SizeChanged` |

---

## 2. Что значит «абсолютная копия WinForms»

Это три независимые оси, и их надо разделить, иначе задача бесконечная.

* **Совместимость API (исходников).** Существующий код WinForms компилируется без правок:
  те же имена типов, свойств, событий, те же сигнатуры. **Это главная ось — на неё целимся.**
* **Совместимость поведения.** Таб-порядок, порядок событий (`Enter`→`GotFocus`→`Validating`),
  семантика `Anchor`/`Dock`, автоскролл, `AutoSize` — совпадают с оригиналом.
  **Целимся, проверяем дифф-тестами против настоящего WinForms на Windows.**
* **Пиксельная совместимость.** Совпадение вплоть до пикселя с Win32-темой.
  **Не целимся.** Вместо этого — своя тема, которая выглядит как WinForms, но чище
  (это и есть «но лучше» из формулировки задачи). Тема должна быть подключаемой.

Практический критерий готовности v1: **топ-95% реально используемого API** —
~60 контролов, `System.Drawing` в объёме, нужном для `OnPaint`, диалоги, layout-панели,
меню/тулбары, data binding.

---

## 3. Три стратегии реализации

### A. «GoForms-подход, только на Avalonia» — обёртка над контролами Avalonia
Каждый `NetForms.Button` держит внутри `Avalonia.Controls.Button`.

*Плюсы:* быстрый старт, ввод/IME/скроллы уже работают.
*Минусы:* тот же потолок, что с Fyne. Нельзя нормально сделать `OnPaint`,
`ControlStyles`, `SetStyle(UserPaint)`, наследование контролов пользователем
(`class MyControl : Control` с собственной отрисовкой) — а это фундаментальная
часть WinForms. Вид зависит от тем Avalonia.

### B. Своя модель `Control` + своя отрисовка, Avalonia — как замена Win32 ⭐
Avalonia даёт ровно то, что в WinForms давала ОС: окно, поток событий ввода, IME,
буфер обмена, нативные файловые диалоги, DPI, GPU-поверхность. Всё, что выше
(дерево контролов, WM_PAINT→`OnPaint`, хит-тест, фокус, capture, z-order,
инвалидация, layout) — наше, написанное по образцу WinForms.

*Плюсы:* полный контроль, честный `OnPaint`, пользовательские контролы работают,
одинаковый вид везде, тестируемость офскрин-рендером, платформенный слой изолирован
(потом можно добавить нативный Win32-бэкенд для «настоящего» вида на Windows).
*Минусы:* объём. Текстовый ввод с выделением/IME/undo придётся написать самим.

### C. Форк `dotnet/winforms` или Mono `System.Windows.Forms`
Оба под MIT. У Mono — полноценная управляемая реализация WinForms с рисованием
через `Theme`-абстракцию и драйверами `XplatUI` (X11/Win32/Cocoa); есть готовые
портирования Mono SWF на .NET Core
([DanielVanNoord/System.Windows.Forms](https://github.com/DanielVanNoord/System.Windows.Forms),
[AlexanderSemenyak — форк](https://github.com/AlexanderSemenyak/System.Windows.Forms.OnCore.From.Mono)).

*Плюсы:* мгновенная широта покрытия API.
*Минусы:* ~400k строк чужого легаси уровня .NET Framework 4.x, всё завязано на
GDI+ (`libgdiplus`, cairo, не поддерживается) и на HWND/WndProc; с .NET 7+
GDI-графика на не-Windows кидает `PlatformNotSupportedException`. Отладка чужого
кода на новом графическом стеке дороже, чем написание своего.

### Рекомендация: **B, а C использовать как каменоломню**

Обе кодовые базы под MIT — их можно **читать как спецификацию** (точная семантика
таб-порядка, порядка событий, автоскролла) и **точечно вендорить** те куски, которые
целиком управляемые и не завязаны на Win32, с сохранением копирайта:

* layout-движок `TableLayoutPanel` / `FlowLayoutPanel` (чистый managed, сотни
  выверенных строк, полностью портируемый);
* `DataGridView` и его политика ширин;
* `ToolStrip` / `MenuStrip` / рендереры;
* `PropertyGrid`;
* `ComponentModel`-инфраструктура дизайнера.

Это экономит месяцы и одновременно повышает точность.

---

## 4. Архитектура

```
NetForms.Platform      IWindowBackend, IInputSource, IClipboard, INativeDialogs, IFontSource
   └─ .Avalonia        реализация: Avalonia Window + custom Render + Skia lease
   └─ .Win32 (потом)   опциональный нативный бэкенд

NetForms.Drawing       System.Drawing поверх SkiaSharp:
                       Graphics, Pen, Brush, Font, Bitmap, Region, GraphicsPath,
                       TextRenderer, StringFormat
                       Point/Size/Rectangle/Color — берём готовые из
                       System.Drawing.Primitives (уже кроссплатформенны в BCL)

NetForms.Core          Control + ControlCollection, Form, Application (Run/DoEvents/Exit),
                       модель сообщений (Invalidate → OnPaint), фокус, таб-порядок,
                       mouse capture, z-order, Invoke/BeginInvoke + SynchronizationContext,
                       Anchor/Dock/LayoutEngine, Padding/Margin/AutoSize, Timer

NetForms.Controls      контролы (см. дорожную карту)

NetForms.Design        атрибуты ComponentModel: [Category], [DefaultValue], [Browsable],
                       [DesignerSerializationVisibility] — по ним дизайнер строит панель
                       свойств рефлексией

NetFormsDesigner       расширение VS Code: webview (переиспользуем!) + Roslyn-CLI
```

**Решение по неймспейсам (важное).** Пишем сразу в `System.Windows.Forms` и
`System.Drawing`, а не в `NetForms.*`. Тогда существующий проект переезжает заменой
одной строки в `.csproj` (`<UseWindowsForms>` → `<PackageReference Include="NetForms">`),
и это буквально то, что просили — «абсолютная копия». Риск неоднозначности типов
возникает только если в одном проекте включить и настоящий WinForms, и наш пакет;
лечится `extern alias`. Бренд/имя пакета при этом — `NetForms`.

---

## 5. Что переиспользуется из GoForms

**Переезжает как спецификация (код перепишется, семантика — нет):**

| Источник | Что забираем |
|---|---|
| `GoForms/layout.go` (266) | модель `layoutBaseline`: якоря считаются от design-time геометрии, доки съедают клиентскую область первыми |
| `GoForms/layoutpanels.go` (513) | `FlowLayoutPanel`/`TableLayoutPanel` — Absolute/Percent/AutoSize треки, спаны |
| `GoForms/datagridview.go` (845) | политика `SizeFixed`/`SizeToContent`/`SizeFill`, поведение при выключенном скролле |
| `GoForms/groupbox.go` | геометрия рамки с разрывом под заголовок (константы уже вынесены под дизайнер) |
| `GoForms/inputcontrols.go` (534) | маски `MaskedTextBox` (`0 9 L ? A #`), `CheckedListBox`, `MonthCalendar`, `DomainUpDown` |
| golden-тесты (`golden_dock_test.go`, `responsiveness_matrix_test.go`, `anchor_columns_test.go`) | сами матрицы кейсов — переносятся в C# один в один |

**Переезжает почти как есть — webview дизайнера** (`GoFormsDesigner/media/*`, ~4k строк
JS): канвас, drag/resize, снапы к соседям, направляющие, панель свойств, отрисовка
превью каждого типа контрола, `dockLayout.js`, `gridLayout.js`, `renderPlan.js`.
Он не знает про Go — он работает с JSON-моделью формы.

**Выбрасывается целиком:**
`interaction.go` (обход хит-теста Fyne), переобъявления `SetBounds` в шести файлах,
колбэчные диалоги, `FontSupported()` и вся логика «здесь шрифт не применится»,
самодельный `fyne.Layout` ради события ресайза.

**Переписывается:** `GoFormsDesigner/tool/*.go` (6.3k строк парсинга и переписывания
Go-AST) → Roslyn. Это станет **проще**, а не сложнее:

* `catalog.go` (658 строк ручного каталога типов) → **не нужен**: в .NET дизайнер
  получает свойства, события и их типы **рефлексией** над сборкой + атрибутами
  `ComponentModel` — ровно так, как это делает настоящий дизайнер VS;
* `parse.go` (1010) / `apply.go` → `CSharpSyntaxRewriter` над `InitializeComponent()`;
* `rename.go` → `Renamer.RenameSymbolAsync` из Roslyn Workspaces, бесплатно;
* `codegen.go` → генерация синтаксиса + штатный форматтер Roslyn.

---

## 6. Дорожная карта

**Ф0 — вертикальный срез (дни).**
Avalonia-окно; своё дерево `Control`; `Form`, `Label`, `Button`; `Invalidate`→`OnPaint`
через Skia; клик, фокус; `Anchor`/`Dock`; `Application.Run(new MainForm())`.
*Критерий:* код из стандартного WinForms-туториала компилируется без правок и
одинаково работает на Ubuntu и Windows.

**Ф1 — ядро.**
`NetForms.Drawing` на Skia (пути, заливки, клип, трансформы, изображения, текст,
`TextRenderer.MeasureText`); двойная буферизация; mouse capture; полный таб-порядок и
порядок событий фокуса/валидации; `Invoke`/`BeginInvoke`; `Timer`; `MessageBox`.
*Критерий:* golden-тесты рендера офскрин + дифф-тесты порядка событий против настоящего
WinForms на windows-latest в CI.

**Ф2 — базовые контролы.**
`TextBox` (выделение, undo, IME, контекстное меню), `CheckBox`, `RadioButton`, `Panel`,
`GroupBox`, `PictureBox`, `ProgressBar`, `ComboBox`, `ListBox`, `NumericUpDown`,
`TrackBar`, `TabControl`, `SplitContainer`, `ScrollBar`, `ToolTip`, `LinkLabel`.

**Ф3 — layout и контейнеры.**
`TableLayoutPanel`, `FlowLayoutPanel` (вендорим из `dotnet/winforms`), `AutoScroll`,
`AutoSize`/`AutoSizeMode`, `Margin`/`Padding`, `MinimumSize`/`MaximumSize`.

**Ф4 — тяжёлые контролы.**
`ListView` (все четыре вида), `TreeView`, `DataGridView` (+ `BindingSource`,
`INotifyPropertyChanged`), `MenuStrip`/`ToolStrip`/`StatusStrip`/`ContextMenuStrip`,
`PropertyGrid`, диалоги (`OpenFileDialog`/`SaveFileDialog` через нативные порталы,
`ColorDialog`, `FontDialog`), MDI (решить: нужен ли).

**Ф5 — дизайнер и шаблоны.**
Дизайнер как приложение на самом NetForms (см. «План Ф5» ниже — решение пересмотрено
относительно первоначального «ретаргет webview»); Roslyn для кода туда-обратно;
`dotnet new netforms`; команды «новая форма», «открыть дизайнер», генерация заглушек
обработчиков.

**Ф6 — полировка.**
DPI-масштабирование, темы (классическая + современная), `.resx` и локализация,
доступность через automation-peers Avalonia, Native AOT, публикация single-file.

**Ф6.К — корпус реальных проектов: «перевести по кнопке и собрать» (задача заказчика, 2026-09-23).**
Конвертер (`tools/NetForms.Convert`) + `dotnet build` должны переводить и собирать настоящие WinForms-
проекты — в первую очередь проекты заказчика на **.NET Framework 4.8/4.8.1** (старый формат `.csproj`):

| Репозиторий | Что это |
|---|---|
| <https://github.com/TrewCar/Fractals> | фракталы (кривые Коха, Леви), две формы |
| <https://github.com/TrewCar/The-Koch-curve> | кривая Коха |
| <https://github.com/TrewCar/Sapper> | сапёр; картинки из `Picture\` рядом с exe (`@"\Picture\x.png"`) |
| <https://github.com/TrewCar/Find-Exit-Labirint> | генерация и проход лабиринта |
| <https://github.com/TrewCar/ANT-Lengton> | муравей Лэнгтона, два проекта |
| <https://github.com/TrewCar/One-dimensional_cellular_automaton> | одномерный клеточный автомат |

Плюс расширенный корпус открытых проектов с разрешающими лицензиями (игры, калькуляторы, редакторы,
свои контролы, сборник примеров Surviving-WinForms) — `tests/corpus/corpus.json`, с закреплёнными
коммитами. *Критерий:* все проекты заказчика — «перевёл → собрал → запустил на Linux» без ручных правок;
по расширенному корпусу — процент «собралось без правки» растёт и публикуется, каждый провал разобран:
недостающий API NetForms, недоработка конвертера или осознанно не переносимое (Win32/COM/WebView2/CefSharp).
Журнал — решения 111+.

---

## 7. Оценка и риски

**Объём.** Ядро + ~60 контролов ≈ 40–60k строк C#. Для сравнения: GoForms — 10k строк
на 40 контролов, но там контролы были тонкими обёртками над готовыми виджетами Fyne;
здесь каждый контрол рисуется и обрабатывает ввод сам, то есть в 3–5 раз объёмнее.
Вендоринг MIT-кода срезает, по грубой оценке, четверть работы.

**Главные риски:**

1. **`TextBox` — самая недооценённая часть.** Выделение, прокрутка, undo/redo, IME,
   RTL, автодополнение. *Митигация:* на Ф2 допустимо временно захостить внутри
   `Avalonia.Controls.TextBox` как «нативный» под-контрол и заменить своей реализацией
   позже — платформенный слой это позволяет.
2. **Метрики шрифтов.** WinForms меряет текст двумя разными способами:
   `Graphics.MeasureString` (GDI+) и `TextRenderer.MeasureText` (GDI), и они не совпадают.
   *Решение:* эталон — `TextRenderer`, поверх Skia, с калибровочными тестами.
3. **DPI.** Решить на Ф0, а не потом: работаем в device-independent пикселях,
   `AutoScaleMode` эмулируем.
4. **Объём как таковой.** Нужны замеряемые ворота: каждая фаза закрывается,
   когда соответствующие примеры из корпуса совместимости компилируются и проходят.

---

## 8. Верификация

* **Golden-тесты рендера.** Форма рисуется офскрин в `SKBitmap`, сравнивается с
  эталонным PNG. Культура уже есть — `golden_dock_test.go`, `golden_grid_test.go`.
* **Дифф-тесты против настоящего WinForms.** На `windows-latest` та же форма собирается
  и с `System.Windows.Forms`, и с `NetForms`; сравниваются позиции, размеры,
  порядок событий, метрики текста. Это и есть измеримая «копийность».
* **Корпус совместимости API.** Набор реальных WinForms-примеров, который компилируется
  против нашей сборки; процент собравшихся — метрика прогресса, публикуемая в README.
* **CI:** `ubuntu-latest` + `windows-latest`, headless-рендер на обеих.

---

## 9. Решения, которые нужно принять до старта

1. **Судьба Go-версии.** Рекомендация: `GoForms` замораживается в maintenance
   (багфиксы, без новых контролов), webview дизайнера становится общим для обоих.
2. **Неймспейс:** `System.Windows.Forms` (drop-in, рекомендуется) или `NetForms.*`.
3. **Имя и лицензия.** Проверить занятость `NetForms` на NuGet; лицензия MIT
   (обязательна при вендоринге кода из `dotnet/winforms` / Mono — с сохранением их
   копирайтов в `THIRD-PARTY-NOTICES`).
4. **MDI** — входит в v1 или нет. → **Решено: входит** (Ф4, решение 47).
5. **Целевой TFM:** `net10.0` или `net8.0`+`net10.0`.

---

## Источники

- [Avalonia UI](https://avaloniaui.net/) · [Avalonia.Skia на NuGet (12.1.2, сентябрь 2026)](https://www.nuget.org/packages/Avalonia.Skia/) · [поддерживаемые платформы](https://docs.avaloniaui.net/docs/supported-platforms)
- [MAUI vs Avalonia в 2026](https://www.ctco.blog/posts/maui-vs-avalonia-2026-cross-platform-dotnet-ui/)
- [Обзор кроссплатформенных UI-фреймворков .NET](https://docs.lextudio.com/blog/the-story-about-net-cross-platform-ui-frameworks-dd4a9433d0ea) · [Eto.Forms](https://www.libhunt.com/r/Eto)
- [Mono SWF на .NET Core](https://github.com/DanielVanNoord/System.Windows.Forms) · [форк](https://github.com/AlexanderSemenyak/System.Windows.Forms.OnCore.From.Mono) · [libgdiplus](https://github.com/mono/libgdiplus) · [Mono WinForms FAQ](https://www.mono-project.com/docs/faq/winforms/)
- [System.Drawing на .NET Core (dotnet/runtime#21980)](https://github.com/dotnet/runtime/issues/21980)

---

## Решения (журнал)

Принятые по ходу работы решения, которых не было в плане выше. Дата — когда принято.

### Ф0 (2026-09-10)

1. **TFM — `net10.0`.** Установлен SDK 10.0.104; правило из CLAUDE.md выполнено без
   отката на `net8.0`. Avalonia 12.1.1, SkiaSharp 3.119.4 (версию диктует
   `Avalonia.Skia`). Версии пакетов централизованы в `Directory.Packages.props`.
2. **Формат решения — `NetForms.slnx`** (то, что `dotnet new sln` создаёт в .NET 10).
3. **Слои и зависимости.** `NetForms` (System.Windows.Forms) ссылается на
   `NetForms.Platform` (интерфейсы `IPlatform`/`IPlatformWindow`/`IWindowHost`, без
   единого типа Avalonia) **и на `NetForms.Platform.Avalonia`** как на бэкенд по
   умолчанию: так пакет `NetForms` остаётся единственной строкой в `.csproj`
   приложения. Бэкенд подменяем через внутренний `Application.Platform` — тесты
   ставят туда `TestPlatform` и никогда не инициализируют Avalonia.
4. **Только Avalonia, без её тем и виджетов.** Пакеты `Avalonia.Themes.*` не
   подключаются; единственный templated-контрол — `Window`, его шаблон (голый
   `ContentPresenter`) задан в коде `NetFormsApp`. Внутри окна ровно один
   `FormSurface : Avalonia.Controls.Control`.
5. **Отрисовка — двухфазная, через Skia-lease.** `FormSurface.Render` на UI-потоке
   записывает `OnPaint` всего дерева в `SKPicture` (пользовательский код `OnPaint`
   выполняется строго на UI-потоке, как в WinForms), а `ICustomDrawOperation`
   воспроизводит картинку на render-потоке через `ISkiaSharpApiLeaseFeature` прямо
   на GPU-поверхность. Пока перерисовывается всё клиентское поле; dirty-регионы —
   Ф1 вместе с двойной буферизацией.
6. **Layout — порт `DefaultLayout` из dotnet/winforms**, а не `arrangeControls` из
   GoForms: док обрабатывается от конца `Controls` к началу (нижний по z-order
   докуется первым), якоря считаются целочисленной арифметикой GDI против
   `DisplayRectangle`, `AnchorStyles.None` двигает на половину дельты,
   `ResumeLayout(false)` перезахватывает якоря всех детей. Два осознанных
   расхождения с GoForms, оба в пользу WinForms: (а) *все* `Dock.Fill` получают
   остаток, а не только первый; (б) порядок докования — обратный z-order, а не
   порядок добавления. Golden-матрицы GoForms перенесены с учётом этого
   (`tests/NetForms.Tests/DockLayoutGoldenTests.cs`, `AnchorColumnsTests.cs`).
7. **`Form.Size == Form.ClientSize`.** Окно Avalonia задаётся размером клиентской
   области; рамку в `Size` не включаем. Дизайнер всегда пишет `ClientSize`, так что
   сгенерированный код не затрагивается. Пересмотреть в Ф6 (`FrameSize` у Avalonia
   есть).
8. **`AutoScaleMode`/`AutoScaleDimensions` принимаются и игнорируются.** Работаем в
   DIP при 96 DPI (9pt = 12px везде); масштабирование по шрифту эмулируем в Ф6.
9. **Шрифт по умолчанию — `Segoe UI, 9pt`** (как WinForms на Win10). На Linux
   `FontResolver` подставляет первую найденную из `Noto Sans → Open Sans →
   Cantarell → Ubuntu → DejaVu Sans → Liberation Sans → Arial`, иначе default Skia.
   Метрики текста: `TextRenderer.MeasureText` добавляет GDI-паддинг ⌈em/6⌉ слева и
   ⌈em/4⌉ справа (снимается `NoPadding`); `Graphics.MeasureString` — по em/6 с обеих
   сторон. Калибровка по настоящему WinForms — Ф1.
10. **`SystemColors` — из BCL как есть.** На Linux .NET отдаёт палитру эпохи XP
    (`Control = #ECE9D8`), а не Win10 (`#F0F0F0`). Подменять не стали: пользовательский
    код сравнивает `BackColor == SystemColors.Control`, и подмена сломала бы drop-in.
    Цвета собственной темы (кнопка и т.п.) живут в `Theme.cs`; вопрос «какая
    палитра у формы по умолчанию» — к подключаемым темам в Ф6.
11. **`ApplicationConfiguration.Initialize()`** — публичный статический класс в
    глобальном неймспейсе сборки `NetForms` (в настоящем WinForms его генерирует
    SDK в сборку приложения). Без этого `Program.cs` из шаблона VS не собирается.
12. **Порядок событий клика** — `GotFocus → MouseDown → Click → MouseClick → MouseUp`
    для `Button` (фокус на mouse-down, как у нативного класса BUTTON), фокус:
    `Leave(old) → Enter(new) → LostFocus(old) → GotFocus(new)`. Проверить
    дифф-тестом на Windows в Ф1, пока — зафиксировано тестом `HelloFormsTests`.
13. **Модальность и вложенные циклы** — `IPlatform.RunMessageLoop` вкладывается
    (`DispatcherFrame`), поэтому `Form.ShowDialog()` уже работает на вложенном цикле;
    подробная семантика (`Owner`, блокировка владельца) — Ф1.
14. **`Control.DrawToBitmap`** рисует только клиентскую область (WinForms для `Form`
    захватывает и рамку окна). Это основной инструмент golden-тестов: рендер идёт в
    `System.Drawing.Bitmap`, под которым лежит `SKBitmap` (BGRA8888, premul).

### Ф1 (2026-09-10)

15. **`System.Drawing` доведён до рабочего объёма:** `GraphicsPath` (фигуры по семантике
    GDI+, `PathPoints/PathTypes`, `AddString` через контуры глифов, `Flatten`/`Widen`),
    `Region` (над `SKRegion`, бесконечность — флагом), `Matrix` (порядок Prepend/Append
    как в GDI+), `LinearGradientBrush`/`TextureBrush`/`HatchBrush` (8×8 тайлы),
    `Icon`/`SystemIcons` (векторные), `PrivateFontCollection`/`InstalledFontCollection`.
    У `Graphics` появилось **базовое состояние**: `ResetClip`/`ResetTransform`/`SetClip(Replace)`
    возвращают к состоянию на момент создания, а каждый контрол рисует через *свой*
    `Graphics`, поэтому `OnPaint` не может вылезти за собственные границы.
16. **Двойная буферизация — на уровне окна, не контрола.** `FormSurface` держит raster
    back-buffer в device-пикселях (HiDPI — через масштаб канваса), `Invalidate(rect)`
    копит dirty-прямоугольник, перерисовывается только он, снимок буфера блитится через
    Skia-lease. `ControlStyles.OptimizedDoubleBuffer`/`DoubleBuffered` принимаются и
    ни на что не влияют — мерцать нечему. Запись в `SKPicture` (Ф0) заменена.
17. **Порядок событий фокуса** — как в `ContainerControl.UpdateFocusedControl`:
    `Leave(old) → Validating(old) → Validated(old) → Enter(new) → LostFocus(old) →
    GotFocus(new)`; отмена `Validating` оставляет фокус. Для `Control.Focus()` WinForms
    даёт другой порядок (`LostFocus` раньше `Leave`) — зафиксировано в compat-сценарии
    `focus/b-focus`, будет сверено на Windows CI.
18. **Фокусные рамки** показываются только после клавиатурной навигации (аналог
    `UISF_HIDEFOCUS`), не при программном или мышином фокусе.
19. **Модальность** — `Form.ShowDialog(owner)` через `IPlatformWindow.ShowModal` (Avalonia
    `ShowDialog(owner)` блокирует владельца) + вложенный `DispatcherFrame`; модальная
    форма при закрытии прячется, а не уничтожается (как в WinForms). `MessageBox` —
    собственная форма (`MessageBoxForm`: иконка, перенос текста до 400px, полоса кнопок
    88×26), крестик у Yes/No и Abort/Retry/Ignore отключён, как в Win32.
20. **Таймеры и синхронизация:** `Timer` через `IPlatformTimer` (`DispatcherTimer`),
    `WindowsFormsSynchronizationContext` ставится в `Application.Run`. `Screen` — через
    `IPlatform.GetScreens()`. Курсоры — по имени через `IPlatformWindow.SetCursor`.
21. **Golden-тесты детерминированы шрифтом из репозитория:** `tests/NetForms.Tests/Fonts/DejaVuSans*.ttf`
    (лицензия Bitstream Vera/DejaVu, файл `LICENSE-DejaVu.txt`) грузится через
    `PrivateFontCollection`, эталоны в `tests/NetForms.Tests/Golden/*.png` записываются
    при первом запуске; сравнение с допуском 16/канал и ≤0.2% пикселей.
22. **Дифф-тесты против настоящего WinForms:** `tests/Shared/CompatScenarios.cs`
    компилируется и в `tests/NetForms.Compat` (настоящий `System.Windows.Forms`,
    `net10.0-windows`, вне решения), и в наш тест-проект. На windows-latest CI Compat
    пишет JSON, `CompatDiffTests` сверяет: ключи `exact/` — побайтно, `text/` — с
    допуском 3px (GDI vs Skia), `info/` — только в отчёт. Без JSON тест лишь сохраняет
    наш выхлоп (`render-out/netforms-scenarios.json`).

### Ф2 (2026-09-11)

23. **Набор Ф2 реализован полностью:** `Panel`, `GroupBox`, `CheckBox`, `RadioButton`,
    `PictureBox`, `ProgressBar`, `LinkLabel`, `HScrollBar`/`VScrollBar`, `ListBox`
    (+`ListControl`: DataSource/DisplayMember/ValueMember), `TextBox`/`TextBoxBase`,
    `ComboBox`, `NumericUpDown`/`UpDownBase`, `TrackBar`, `TabControl`/`TabPage`,
    `SplitContainer`/`SplitterPanel`, `ToolTip`, плюс `Clipboard`/`DataObject`.
    Витрина — `samples/Gallery` (код в стиле дизайнера), она же golden-тест.
24. **Полосы прокрутки — своё ядро `ScrollBarCore`.** WinForms рисует их в неклиентской
    области нативно; у нас одно ядро (диапазон, ползунок, автоповтор, колесо, отрисовка)
    и для контролов `H/VScrollBar`, и для встроенных полос `ListBox`/`TextBox`
    (в Ф3 — `ScrollableControl.AutoScroll`). Встроенные полосы живут внутри
    `ClientRectangle` и не появляются в `Controls`.
25. **TextBox хранит переводы строк как `"\r\n"`** — два символа, как Win32 edit:
    `SelectionStart`/`SelectionLength`/`GetFirstCharIndexFromLine` совпадают с WinForms по
    индексам, каретка перешагивает пару целиком. Однострочный режим выкидывает переводы.
    Каретка — `Timer` 530 мс, undo/redo — снимки текста на каждую правку, IME — через
    `TextInput` платформы (составленная строка вставляется как ввод).
26. **Всплывающие окна без активации (`PopupForm`).** Список `ComboBox` и `ToolTip` —
    отдельные top-level окна (`FormBorderStyle.None`, `ShowWithoutActivation`, `TopMost`,
    `Owner`), поэтому выпадающий список выходит за границы формы, как в Win32. Закрываются
    по клику в окне-владельце (`Form.MouseDownAnywhere`), его деактивации, перемещению
    или закрытию. Редактируемый `ComboBox` и `UpDownBase` держат внутри дочерний `TextBox`
    (он виден в `Controls` — расхождение с WinForms, где это HWND без обёртки; исправить,
    когда появится механизм скрытых дочерних контролов).
27. **`TabControl.DisplayRectangle` = 4px от рамки, верх = высота полосы вкладок + 3** —
    ровно координаты, которые дизайнер VS пишет в `tabPage.Location = new Point(4, 24)`.
    Страница с `UseVisualStyleBackColor` — цвета `Window` (белая), как при включённых
    визуальных стилях.
28. **`SplitContainer` раскладывает панели сам** (не через Dock/Anchor), `FixedPanel`
    решает, кто поглощает изменение размера: `Panel2` — растёт первая, `Panel1` —
    вторая, `None` — пропорционально; `SplitterDistance` зажат `Panel1MinSize`/`Panel2MinSize`.
29. **Padding/Margin по умолчанию берутся из `DefaultPadding`/`DefaultMargin`** в
    конструкторе `Control` (было: всегда `Empty`/3), так что `GroupBox` получает 3, а
    `Label` — (3,0,3,0), как в WinForms.

### Ф3 (2026-09-11)

30. **`FlowLayoutPanel`/`TableLayoutPanel` написаны заново по семантике WinForms**, а не
    вендорены: без сети исходники dotnet/winforms недоступны, а семантика известна и
    закреплена compat-сценариями (`flow/*`, `table/*`), которые Windows CI сверит с
    оригиналом. Flow: порядок — порядок коллекции, `Margin` каждого ребёнка, перенос по
    `WrapContents`, `SetFlowBreak`, положение поперёк ряда по `Anchor` (Top/Bottom/оба =
    растяжение/ни одного = центр). Table: `Absolute` → пиксели, `AutoSize` → самый широкий
    односпановый ребёнок (`GetPreferredSize` для AutoSize-детей, иначе `Size`), `Percent`
    → доля остатка с нормализацией процентов, остаток без процентных треков — последнему
    AutoSize-треку; явные ячейки, затем «плывущие» в первую свободную по строкам,
    `GrowStyle` добавляет строки/столбцы или бросает `ArgumentException`; ребёнок в ячейке
    — по `Anchor`/`Dock` внутри `Margin`. Расхождения с GoForms: проценты нормализуются,
    AutoSize-трек меряет содержимое, а не делит остаток поровну.
31. **`AutoScroll` — модель WinForms буквально:** `DisplayRectangle` = объединение
    клиентской области и экстента детей, его начало = `-AutoScrollPosition`, при
    прокрутке дети физически переезжают (`Left` становится отрицательным). Экстент — по
    детям без Dock и без Right/Bottom-якорей, плюс `AutoScrollMargin`, не меньше
    `AutoScrollMinSize`. Полосы — `ScrollBarCore` поверх детей через новые хуки
    `Control.OnPaintOverlay`/`IsOverlayPoint` (полоса перехватывает хит-тест).
32. **`AutoSize` контейнеров** — через `Control.GetPreferredSize` по умолчанию (экстент
    детей + отступы дисплейного прямоугольника), пересчёт после каждого `OnLayout`.
    `AutoSizeMode` по умолчанию: `GrowAndShrink` для одиночных контролов (Label),
    `GrowOnly` для `Panel`/`GroupBox`/`Form`/`ButtonBase` (задокументированные значения
    WinForms); «указанный размер» для GrowOnly — последний `Size`, заданный кодом.

### Ф4 (2026-09-11)

33. **Сеть есть — исходники `dotnet/winforms` читаем напрямую.** Ограничение из решения 30
    («без сети, пишем по памяти») снято: все константы полос взяты из
    `src/System.Windows.Forms/.../Controls/ToolStrips/*.cs` и подтверждены дифф-прогоном.
    Для новых узлов сначала смотрим эталон, потом пишем.
34. **Дифф-прогон против настоящего WinForms работает локально**, а не только в CI (машина
    разработки — Windows):
    `dotnet run --project tests/NetForms.Compat -- out.json`, затем
    `NETFORMS_WINFORMS_SCENARIOS=out.json dotnet test tests/NetForms.Tests`.
    Это цикл обратной связи в секунды — им и выверялись метрики Ф4.
35. **`ToolStrip` — элементы это компоненты, а не контролы.** Полоса сама их раскладывает,
    рисует через `Renderer` и маршрутизирует к ним мышь и клавиатуру; настоящими детьми
    (`Controls`) становятся только контролы внутри `ToolStripControlHost`. Константы
    (сверены дифф-прогоном): `DefaultSize` 100×25, `Padding` (0,0,1,0), `Margin` элемента
    (0,1,0,2), grip — 3px с полем 2 и на всю высоту дисплейного прямоугольника (первый
    элемент начинается с x=7), `ImageScalingSize` 16×16, кнопка переполнения 16px,
    `MenuStrip` 200×24 с `Padding` (6,2,0,2), `StatusStrip` 200×22 с `Padding` (1,0,14,0).
    Геттер `LayoutStyle` возвращает **разрешённый** стиль (`StackWithOverflow` →
    `HorizontalStackWithOverflow`), как в WinForms.
36. **Вокруг содержимого элемента — рамка 2px с каждой стороны** (`BorderWidth` из
    `ToolStripItemInternalLayout`). Без неё пункт «File» получался 31px вместо 37.
37. **`Margin`/`Padding`/`DisplayStyle` элемента вычисляются лениво** от `DefaultXxx`, а не
    фиксируются в конструкторе: у `ToolStripMenuItem` они зависят от того, где элемент
    оказался (на полосе меню — `Padding` (4,0,4,0), в выпадающем списке — (0,1,0,1)), а
    владелец известен только после добавления в коллекцию.
38. **Отрисовка — по модели WinForms:** каждый тип элемента переопределяет `OnPaint` и зовёт
    методы `ToolStripRenderer` (`DrawButtonBackground`, `DrawItemText`, `DrawArrow`, …);
    полоса перед вызовом транслирует `Graphics` в начало элемента, поэтому рендерер рисует
    от (0,0). Рендереры: `ToolStripProfessionalRenderer` поверх `Theme` и
    `ToolStripSystemRenderer` (= professional без поля изображений — осознанная разница: у
    нас одна тема). `ProfessionalColorTable` реализована подмножеством свойств.
39. **`ToolStripDropDown` — отдельное неактивируемое окно поверх `PopupForm`** (как у
    `ComboBox` в Ф2): выпадающий список выходит за границы формы. Цепочка открытых меню
    живёт в `ToolStripManager`; клавиши приходят в неё из `Form.HandleKeyDown` →
    `ToolStripManager.ProcessCmdKey` (горячие клавиши пунктов + навигация стрелками, Enter,
    Escape), Alt+буква — `Form.ProcessMenuMnemonic` по `MainMenuStrip`. Открытые меню,
    исчезнувшие вместе с формой, отбрасываются при опросе `ActiveDropDown`, иначе они
    молча съедали бы клавиши всего приложения.
40. **Геометрия выпадающего меню — осознанная разница.** WinForms прячет колонку картинок
    в `Padding` выпадающего списка (33,2,1,2) и элементы начинаются после неё; у нас
    элементы занимают всю ширину, а колонка рисуется под ними. Текст при этом начинается
    на том же x=33 (25 поля + 8 отступа). Ширина меню расходится примерно на 10px —
    отслеживается ключом `info/menu/dropdown-preferred`.

41. **`ListView` — одна раскладка на все пять видов.** Элементы размещаются в «контентных»
    координатах (`PerformItemLayout`), а видовая область по ним прокручивается, поэтому
    хит-тест, выделение, клавиатура и прокрутка общие для Details/List/SmallIcon/LargeIcon/Tile.
    Группы (`ShowGroups`) реализованы как вертикальные полосы с заголовком и работают во всех
    видах, кроме `List` (как и в WinForms). `HideSelection` по умолчанию **False** — с .NET 5
    WinForms поменял значение, дифф-прогон это подтвердил.
42. **Метрики текста приведены к GDI** (найдено дифф-прогоном, четыре ошибки):
    `TextRenderer.MeasureText` возвращал высоту с external leading — теперь это `tmHeight`
    (ascent + descent), как у `GetTextExtentPoint32`, тогда как `Font.Height` leading
    по-прежнему учитывает. `TextBoxBase.PreferredHeight` считается по формуле WinForms
    (`Font.Height + 7` с рамкой, `+3` без) вместо вывода из собственных отступов — было 26
    против 23; отступ текста от рамки стал 1px. `ComboBox`/`NumericUpDown` — `Font.Height + 7`
    (было +8). `Label.GetPreferredSize` добавляет 6px по вертикали («иначе текст обрезается
    при AutoSize» — комментарий из dotnet/winforms).

43. **`TreeView` — плоский список видимых строк.** На каждом пересчёте раскрытая часть дерева
    разворачивается в `_rows`, и хит-тест, клавиатура и прокрутка работают с индексами строк,
    а само дерево остаётся деревом. Все проверенные умолчания (`Indent` 19, размер 121×97,
    `PathSeparator`, `HideSelection`, `ItemHeight`) совпали с WinForms с первого прогона.
44. **`DataGridView` — значения не дублируются.** У несвязанной сетки значение лежит в ячейке,
    у связанной `DataGridViewCell.Value` читает и пишет свойство привязанного объекта
    (`GetBoundValue`/`SetBoundValue`), так что сетка и данные не расходятся и второй копии
    значений не существует. Столбцы при `AutoGenerateColumns` строятся по
    `PropertyDescriptor`-ам (bool → `DataGridViewCheckBoxColumn`), `IBindingList.ListChanged`
    перестраивает строки. Политика ширин: `Fill` делит остаток по `FillWeight`, остальные
    режимы меряют заголовок и/или содержимое — семантика из `GoForms/datagridview.go`,
    сверена с WinForms по умолчаниям. Высота строки — `Font.Height + 9` (25 при 9pt), как в
    WinForms. Стили каскадируют: сетка → строка → чётная строка → столбец → ячейка.
45. **`BindingSource`, `Binding`, `Control.DataBindings`.** `BindingSource` оборачивает список
    (или тип — тогда создаётся пустой `BindingList<T>`), ведёт `Position`/`Current`, пробрасывает
    `ListChanged` и реализует `IBindingListView`/`ITypedList`. `Binding` связывает свойство
    контрола со свойством источника в обе стороны через `PropertyDescriptor` и его
    `AddValueChanged`, с хуками `Format`/`Parse`. Расхождение с WinForms: у нас нет
    `CurrencyManager`/`BindingContext` — их место занимает сам `BindingSource`.

46. **Диалоги: файловые — системные, цвет и шрифт — свои.** `OpenFileDialog`/`SaveFileDialog`/
    `FolderBrowserDialog` уходят в платформенный слой (`IPlatform.ShowOpenFileDialog` и соседи),
    а он — в `StorageProvider` Avalonia: на Linux это XDG-портал, на Windows — системный
    common item dialog, то есть настоящие диалоги ОС, а не нарисованная имитация. Фильтр
    WinForms (`"Текст|*.txt|Все|*.*"`) разбирается в `FilePickerFileType`. `ColorDialog` и
    `FontDialog` — наши собственные формы: системного аналога, одинакового на обеих ОС, нет.
    Тесты ходят через шов платформы: `TestPlatform` отдаёт заранее положенный результат и
    записывает, о чём его спросили.
47. **MDI входит в v1** (закрывает открытый вопрос §9.4) — по прямому требованию заказчика.
    **Дочернее окно MDI — это не окно, а контрол** внутри `MdiClient` родителя: оно само рисует
    заголовок и рамку и сообщает клиентскую область, смещённую на них. Ради этого в ядро
    добавлены `Control.ClientOrigin` и `Control.ClientSizeCore`: координаты детей отсчитываются
    от клиентской области (как в WinForms), а отрисовка, хит-тест и `PointToForm`/`PointFromForm`
    учитывают смещение. Оконное состояние (поверхность, фокус, capture, курсор) живёт в
    `Control.TopLevelForm` — для обычного контрола это `FindForm()`, для содержимого MDI-ребёнка
    родитель. Метрики рамки взяты из настоящего WinForms дифф-прогоном: 8px рамка и заголовок
    23px, то есть у дочернего окна 300×200 клиентская область 284×161. `LayoutMdi` расставляет
    каскадом (активное окно — крайнее справа-снизу) и плиткой; максимизированное окно занимает
    клиентскую область и следует за её изменением, а заголовок родителя показывает
    «Родитель - [Ребёнок]». Пример — `samples/MdiDemo` (меню, панель, статус-строка, список окон).

48. **Два бага живого окна, которые офскрин-тесты поймать не могли** (найдены прогоном
    настоящего приложения с синтетическими кликами через user32 и снимками экрана):
    - `Window.Position` в Avalonia — позиция **рамки**, а у нас `Form.Size` это клиентский
      размер (решение 7), поэтому `Form.Location` обязан быть началом **клиентской** области.
      Раньше `PointToScreen` прибавлял позицию рамки, и каждое всплывающее окно (меню,
      список `ComboBox`, подсказка) уезжало ровно на высоту заголовка — на Windows это
      (8, 31). Теперь `AvaloniaWindow` отдаёт `PointToScreen(0,0)`; так как `PositionChanged`
      приходит **раньше** `Opened`, когда толщина рамки ещё неизвестна, начало пересчитывается
      в `Opened`.
    - Показ неактивируемого всплывающего окна всё равно вызывает у владельца короткую деактивацию,
      и `PopupForm` закрывал меню в тот же момент, когда клик его открыл: визуально «меню не
      работает по клику». Теперь деактивация владельца проверяется отложенно (`Application.Post`):
      если приложение действительно потеряло передний план, владелец так и останется неактивным
      и меню закроется, а короткий провал активации переживается.

49. **`PropertyGrid` поверх `TypeDescriptor`.** Строки берутся из `PropertyDescriptor`-ов, поэтому
    всё, что описывает себя атрибутами `ComponentModel`, отображается как в дизайнере: категории,
    `[Description]` в панели помощи, `[Browsable(false)]` скрывает строку, `IsReadOnly` гасит
    значение. Раскрываемые значения (`Size`, `Point`, `Padding`) разворачиваются через
    `TypeConverter.GetProperties`. Редактор — выпадающий список для перечислений, `bool` и
    `GetStandardValues`, иначе текстовое поле; запись идёт через `TypeConverter.ConvertFromString`
    сразу во все выбранные объекты (`SelectedObjects`). Как и WinForms, после установки
    `SelectedObject` выделяется первое свойство. Добавка сверх WinForms — свойство `RootGridItem`
    (в оригинале корень достаётся цепочкой `SelectedGridItem.Parent`), помечена в коде.

50. **Развёрнутая форма сообщает свой настоящий размер уже в `Load`.** WinForms создаёт окно
    сразу развёрнутым, поэтому код в `Form_Load`, который читает `Width`/`Height` (а перенесённые
    приложения делают это постоянно — см. `samples/GenLabs`, где так выделяется `Bitmap` под
    лабиринт), видит размер рабочей области. У нас `Load` поднимался до показа окна и отдавал
    размер из дизайнера; `Bitmap` получался маленьким, а логика считала по новым размерам —
    «picturebox брал прошлые замеры, а работал по новым». Теперь при создании окна в состоянии
    `Maximized` границы формы сразу берутся из рабочей области экрана, а платформа уточняет их
    своим `Resized`.

### Ф4 закрыта

181 тест, из них 176 проходят; 5 падающих — это golden-эталоны с текстом из «Открытых вопросов».
Все контролы Ф4 сведены с настоящим WinForms дифф-прогоном (решение 34): расхождений по ключам
`exact/` и `text/` у полос, `ListView`, `TreeView`, `DataGridView`, MDI и `PropertyGrid` нет.

**Задел на Ф5 (дизайнер), выявленный `PropertyGrid`:** наши контролы не размечены атрибутами
`[Category]`, `[Description]`, `[DefaultValue]`, `[Browsable]` — у настоящей `Button` шесть
категорий свойств, у нашей одна («Misc»). Дизайнер строит панель свойств рефлексией именно по
этим атрибутам (см. §5), так что разметку публичного API придётся сделать до или в начале Ф5.
Ключ `info/propertygrid/root-children` держит эту разницу измеримой.

### Открытые вопросы Ф4

- ~~**Golden-эталоны с текстом «поплыли».**~~ Закрыто в Ф6, решение 105. `primitives`, `messagebox` и три страницы витрины
  расходятся с записанными PNG на ~1% пикселей — исключительно в сглаживании глифов
  (геометрия, позиции и раскладка совпадают). Ни один файл в `NetForms.Drawing`, ни
  шрифты, ни версия SkiaSharp (3.119.4) не менялись с момента записи эталонов, так что
  причина внешняя по отношению к коду. Переключение `SKFontEdging`/`SKFontHinting` эталон
  не воспроизводит. Эталоны намеренно **не** перезаписаны — нужно решение: либо признать
  рендеринг текста непортируемым и сделать его герметичным (это же условие того, чтобы
  одни и те же PNG сходились на ubuntu-latest и windows-latest), либо ослабить допуск.
- ~~**Расхождения с WinForms из Ф1–Ф3, оставшиеся после правок решения 42**~~ — закрыты в Ф6, решения
  109–110 (кроме `dock/huge-left`, оставленного осознанно). Исторический список: (полосы и
  `ListView` из Ф4 сведены полностью). Проверяются командой из решения 34:
  - `autoscroll/near`, `autoscroll/far`, `autoscroll/position`, `autoscroll/display` —
    прокрутка контейнера уезжает не туда и `DisplayRectangle` вычитает ширину полосы,
    чего WinForms не делает;
  - `autosize/panel-grow`, `panel-growonly`, `panel-shrink` — WinForms не меняет размер
    панели с `AutoSize` в этом сценарии, а мы меряем содержимое;
  - `controls/textbox-lines`, `textbox-text` — однострочный `TextBox` у нас выкидывает
    переводы строк и при программной установке текста (решение 25), WinForms — нет;
  - `controls/trackbar-vertical-size` — при `Orientation.Vertical` не меняются местами
    ширина и высота;
  - `dock/huge-left/body` — WinForms оставляет докнутому ребёнку отрицательную ширину,
    у нас размеры зажаты нулём;
  - `flow/break-b` — положение после `SetFlowBreak` расходится на высоту строки;
  - `anchor/padding/fill` — `Dock.Fill` в WinForms игнорирует `Padding` родителя;
  - `text/controls/checkbox-preferred`, `radiobutton-preferred` — высота 17 против 22:
    нужен такой же вертикальный запас, как у `Label` (решение 42), величина не выяснена;
  - `focus/b-focus` — задокументировано решением 17.

### Ф5.0 — разметка публичного API атрибутами (2026-09-22)

51. **Разметка не написана руками, а сгенерирована из настоящего WinForms.** `tests/Shared/AttributeDump.cs`
    снимает через `TypeDescriptor` всю дизайн-тайм-метаданность (`Category`, `Description`,
    `DefaultValue`, `Browsable`, `Localizable`, `DesignerSerializationVisibility`) всех 118 компонентов
    `System.Windows.Forms`; файл компилируется и в `NetForms.Compat` (эталон, Windows), и в наш
    тест-проект, поэтому два дампа сравниваются ключ в ключ. Генератор
    `tools/NetForms.Markup` (Roslyn) переносит эталонные значения на наши члены. 933 члена размечены
    первым проходом. Ручной набор такого объёма был бы и дольше, и менее точен.
52. **Культура дампа принудительно инвариантная.** `PropertyDescriptor.Category`/`.Description`
    возвращают **локализованные** строки: на русской машине `Category` у `Anchor` — «Макет». Дамп
    ставит `CultureInfo.CurrentUICulture = InvariantCulture`, поэтому в исходники попадает
    `[Category("Layout")]`, а в рантайме обе стороны локализуются одинаково (`CategoryAttribute`
    сам переводит известные имена). Исключение: категории, которых нет в BCL («Property Changed»,
    «Private»), у нас останутся английскими там, где WinForms их переводит.
53. **Скрытие унаследованных членов — переобъявлением, как в WinForms.** `TypeDescriptor` читает
    атрибуты с самого члена, поэтому «у `ProgressBar` нет `Text`» выражается только повторным
    объявлением `Text` с `[Browsable(false)]`. Второй проход генератора (`shadow`) нашёл 330 таких
    членов, свёл их вверх по нашей иерархии (различие, общее для всех наследников, объявляется на
    базе) и написал 323 переобъявления за три раунда. **Переобъявление обязано указывать
    `Browsable`/`DesignerSerializationVisibility`/`Localizable` явно, даже когда значение
    «по умолчанию»**: атрибуты наследуются, и `[Browsable(true)]` — единственный способ отменить
    унаследованный `[Browsable(false)]` (так и сделано у `ButtonBase.AutoSize`).
54. **Ворота Ф5.0 закрыты.** `AttributeDiffTests.DesignTimeMetadataMatchesRealWinForms`: 6661 член
    в 68 типах — расхождений с настоящим WinForms **ноль**. `ControlsExposeSeveralPropertyCategories`:
    у `Button`, `TextBox`, `Label`, `ListView`, `DataGridView` больше не одна категория «Misc».
    Ключ `info/propertygrid/root-children` из Ф4 закрыт.
55. **`DefaultValueTests.DeclaredDefaultsMatchAFreshInstance`** — второй ворот: 993 объявленных
    умолчания сверяются с тем, что реально отдаёт новый экземпляр. Список исключений в тесте
    разделён на две части: (а) там, где настоящий WinForms так же непоследователен (проверено по
    эталонному дампу, не на глаз) — `GroupBox/PictureBox.TabStop`, `ImageList.Images`,
    `TreeView.LineColor`, `ToolStripXxx.Name`; (б) наши осознанные отличия (см. открытые вопросы).
56. **Дамп сравнивает и фактические значения нового экземпляра** (`MemberDump.Actual`), не только
    атрибуты. Это поймало настоящие ошибки умолчаний, исправленные здесь же: `ComboBox.MaxLength`
    (было 32767 от внутреннего `TextBox`, стало 0 — «без ограничения»), `LinkLabel.TabStop` (false
    до появления текста, как `UpdateSelectability` в WinForms), `ProgressBar.TabStop` и
    `SplitContainer.TabStop` (остаются true: `TabStop` не связан с `Selectable`),
    `TreeView.LineColor` (`Color.Empty` до установки, рисование — через `EffectiveLineColor`),
    `FontDialog.Color` (`Black`), `PropertyGrid.SelectedItemWithFocus*Color` (`SystemColors`),
    `BindingSource.Sort`/`Filter` (`string?`, null до установки), `ToolStripItem.AutoToolTip`
    (true у кнопочных, через `DefaultAutoToolTip`), `ToolStripMenuItem.Overflow` (`Never`, через
    `DefaultOverflow`), `ToolStrip.CausesValidation` (false), `ScrollBar.DefaultMargin` (пусто).
57. **`PaddingConverter` написан** и повешен на `Padding` (`[TypeConverter]`). Без него `Padding` в
    панели свойств показывался как `{Left=3,Top=3,...}`, не редактировался и не сериализовался.
    Конвертер даёт строку «3, 3, 3, 3», разворачивает значение в `All/Left/Top/Right/Bottom`,
    поддерживает `CreateInstance` и отдаёт `InstanceDescriptor`, поэтому дизайнер сможет писать
    `new Padding(3)`, а не `new Padding(3, 3, 3, 3)`. `Point`/`Size`/`Color` конвертеры из BCL
    работают, проверено дампом.
58. **Найдены и исправлены два расхождения публичного API**, которых разметка бы не стерпела:
    `TabControl.DrawMode` имел тип `DrawMode`, а должен `TabDrawMode` (новый enum, без
    `OwnerDrawVariable`); удалён мёртвый `FolderBrowserDialogRootFolder` — такого типа в WinForms нет,
    `FolderBrowserDialog.RootFolder` и у нас, и там `Environment.SpecialFolder`.
59. **Копирайт.** Тексты `[Description]` — из ресурсов `System.Windows.Forms` (dotnet/winforms, MIT);
    происхождение и лицензия записаны в `THIRD-PARTY-NOTICES.md`.

### Открытые вопросы Ф5.0

Все измеримы: `tests/.../render-out/attrs-diff.txt` (должен быть пуст) и `defaults-diff.txt`
(560 строк — отчёт о том, чем новый экземпляр отличается от настоящего WinForms).

- ~~**`ToolStrip.AutoSize` у нас false, в WinForms true.**~~ Закрыто в Ф5.2, решение 70.
- ~~**`Dock` у выпадающих списков.**~~ Закрыто в Ф5.2: выпадающий список больше не докается в своё окно
  (решение 71), `Dock` — `None`, как в WinForms.
- **`ImeMode`.** Наш `Control.ImeMode` стартует с `Inherit`, WinForms — с `NoControl`, а у контролов
  без текстового ввода (`Button`, `Label`, `ScrollBar`) — `Disable`. Нужен `DefaultImeMode`.
  35 строк в `defaults-diff.txt`.
- **`ForeColor` текстовых контролов** — у нас `ControlText`, в WinForms `WindowText`
  (`TextBox`, `ListBox`, `ListView`, `ComboBox`, `NumericUpDown`); у `ProgressBar` — `Highlight`.
- **`ClientSize` контролов с рамкой** — WinForms вычитает рамку (`TextBox` 100×23 → клиент 96×19),
  у нас клиент равен размеру. Это же расхождение видно в `ListBox`/`ListView`/`NumericUpDown`.
- **`Font` по умолчанию** в отчёте расходится всегда: эталонный процесс без
  `ApplicationConfiguration.Initialize()` отдаёт «Microsoft Sans Serif, 8.25pt», мы — Segoe UI 9pt
  (решение 9). Шум, не ошибка; фильтровать при чтении отчёта.
- **Покрытие API**: 50 типов и 2785 членов есть в WinForms и отсутствуют у нас. Это метрика
  совместимости (см. §8), а не ворота Ф5.0; сейчас печатается в лог теста.

### Ф5.1 — чтение designer-файла без компиляции (2026-09-22)

60. **`src/NetForms.Design.Serialization` — интерпретатор, а не компилятор.** `DesignerCodeReader`
    разбирает `*.Designer.cs` Roslyn-ом (`Microsoft.CodeAnalysis.CSharp` 4.9.2, версия — в
    `Directory.Packages.props`) и **исполняет** операторы `InitializeComponent()` рефлексией над
    NetForms: конструкторы, сеттеры, `SuspendLayout`/`ResumeLayout` и раскладка работают по-настоящему,
    поэтому результат — то же дерево объектов, что даёт скомпилированный вызов. Модель
    (`DesignerModel`) = живые объекты + то, чего объекты сами не помнят: имена полей, события с именами
    обработчиков, присваивания и все операторы в исходном порядке с исходным текстом (понадобится
    записи в Ф5.2). Корень — экземпляр **базового** класса формы (`Form`, `UserControl`), как в
    дизайнере VS; базовый класс ищется во всех частях partial-класса, поэтому `ReadFile` читает рядом
    лежащий `MainForm.cs`.
61. **Разрешение имён — правила C#, сведённые к дизайнерскому подмножеству:** локальные → поля класса →
    члены корня → типы → пространства имён; `this.X` — сначала поле (у GenLabs поле-`TextBox` с именем
    `Size` перекрывает `Form.Size`), правило «Color Color» (`AutoScaleMode = AutoScaleMode.Font`).
    Неявные using-и WinForms-проекта (`System.Windows.Forms`, `System.Drawing`, …) идут **первыми**, так
    что `Timer` — это `System.Windows.Forms.Timer`. `typeof(MainForm)` (собственный класс, не
    скомпилирован) подменяется типом корня. Перегрузки выбираются по C#-правилам неявных преобразований
    (тождество > ссылочное > числовое > сужение константы), `params` и необязательные параметры
    поддерживаются, ref struct-перегрузки (`decimal(ReadOnlySpan<int>)`) пропускаются.
62. **Вне подмножества — отказ с позицией, а не догадка.** Циклы, условия, лямбды, `-=`, неизвестный член
    известного типа, исключение из сеттера — `DesignerCodeException` с `файл(строка,колонка)` и
    исходным исключением внутри. Обработчики событий **записываются, но не подписываются** (их методы
    не скомпилированы): клик в дизайнере не исполняет код пользователя. **Неизвестный тип** (контрол из
    проекта пользователя) даёт `DesignerPlaceholder` — `Control`, рисующий имя типа пунктирной рамкой:
    общие свойства `Control` к нему применяются, остальные сохраняются с `Applied = false` и исходным
    текстом, чтобы запись в Ф5.2 их не потеряла.
63. **Объекты, созданные inline** (`flowLayoutPanel1.Controls.Add(new Button { Text = "One" })` в
    `samples/Gallery`) — анонимные компоненты с `Name == null`, их присваивания из инициализатора
    записаны. Дизайнер VS такого кода не пишет никогда; как их записывать — вопрос Ф5.2 (см. ниже).
64. **Ворота Ф5.1 закрыты.** `DesignerCodeReaderTests.ReaderBuildsTheTreeThatInitializeComponentBuilds`
    для всех шести форм `samples/` (`HelloForms`, `Gallery`, `Strips`, `MdiDemo` ×2, `GenLabs`).
    Эталон — **только** `InitializeComponent()`, без остального конструктора формы (MdiDemo открывает
    документы, Gallery заполняет списки — дизайнер этого тоже не видит): объект создаётся без
    конструктора, на нём вызывается конструктор базового класса, затем `InitializeComponent`.
    Сравниваются дерево (имя, тип, `Bounds`, `Dock`, `Anchor`, `Text`, порядок z, элементы полос и их
    выпадающих списков) **и каждое browsable-свойство каждого компонента** через `TypeDescriptor` —
    расхождений ноль. Для этого `samples/GenLabs` подключён к тестам (`AssemblyName` = `GenLabs`, чтобы
    не столкнуться с `HelloForms`), а закомментированные в нём `BeginInit`/`EndInit` возвращены.
65. **Дыры API, найденные ридером, закрыты:**
    - `ISupportInitialize` у `PictureBox`, `DataGridView` (явная реализация) и `NumericUpDown`,
      `TrackBar`, `SplitContainer` (публичные методы) — ровно как в WinForms (проверено рефлексией по
      настоящей сборке). Код дизайнера VS пишет `((ISupportInitialize)x).BeginInit()` для этих контролов,
      и без интерфейса он не компилировался.
    - **`UserControl` отсутствовал вовсе.** Добавлен: 150×150, `BorderStyle`, `AutoSizeMode`
      (GrowOnly), событие `Load` — один раз при создании. Вместе с ним `AutoValidate` (перечисление,
      свойство и событие на `ContainerControl`, `Inherit` берёт значение ближайшего контейнера сверху) —
      и с семантикой: при смене фокуса `Disable` не валидирует, `EnableAllowFocusChange` отпускает фокус
      несмотря на отменённый `Validating`. Атрибуты — генератором (`shadow` дописал 8 переобъявлений),
      ворота Ф5.0 по-прежнему дают ноль расхождений.
66. **Баг раскладки, найденный воротами:** `PreferredSize` контейнера учитывал докнутых детей как
    якорных — с их `Margin`, поэтому форма GenLabs с `AutoSize = true` вырастала с 594×505 до 600×511
    (и в ридере, и в скомпилированном приложении). Перенесено измерение `DefaultLayout`
    (`LayoutDockedControls(measureOnly)` + `GetAnchorPreferredSize` + `ComputeAnchoredBounds(measureOnly)`
    из dotnet/winforms): `Fill`-ребёнок добавляет свой предпочтительный размер, только если сам `AutoSize`,
    `Top`/`Bottom` — только высоту, `Left`/`Right` — только ширину, якорные к правому/нижнему краю
    сохраняют расстояние до него. Четыре новых сценария `exact/autosize/pref-*` совпадают с WinForms.

### Открытые вопросы Ф5.1

- **Inline-объекты при записи.** Либо писать их обратно inline (сохраняет файл, но это не формат VS),
  либо давать им имена по `NameCreationService` при первой правке формы (формат VS, но большой дифф).
  Решить в начале Ф5.2; ридер поддерживает оба варианта.
- **Дизайн-тайм-подмена свойств — задача хоста (Ф5.3).** Ридер применяет значения буквально:
  `Visible = false` прячет контрол, `Timer.Enabled = true` запустил бы таймер, `WindowState = Maximized`
  у формы выставлен. Дизайнер VS «затеняет» такие свойства (`ControlDesigner.ShadowProperties`): значение
  хранится в модели, а на живом объекте — дизайн-тайм-вариант. `DesignSurface` должен делать то же.
- **`AutoScaleDimensions` из чужой машины.** GenLabs записан при `6F, 13F` (MS Sans Serif 8.25pt);
  настоящий WinForms на машине с Segoe UI 9pt масштабирует форму ×7/6 (проверено: 594×505 → 693×583),
  мы — нет (решение 8). Для дизайнера это значит: превью не совпадёт с тем, что покажет WinForms на
  другой машине, но совпадает с тем, что покажет NetForms. Вернуться в Ф6 вместе с DPI.
- ~~**`.resx`**~~ — закрыто в Ф6, решения 107–108.
- **Формы, унаследованные от формы того же проекта** (`MainForm : BaseForm`), не открываются — базовый
  класс не скомпилирован. Сообщение об ошибке говорит об этом прямо. Решается тем же этапом, что и
  пользовательские контролы (компиляция проекта в память).

### Ф5.2 — запись designer-файла (2026-09-22)

67. **Inline-объекты получают имена полей при записи** (закрывает открытый вопрос Ф5.1). В формате дизайнера
    VS inline-объектов нет вообще: CodeDom-десериализатор VS такой файл не откроет, а любой компонент на
    форме — поле. Поэтому `DesignerModel.NameInlineComponents()` перед записью даёт им имена по правилу
    `NameCreationService` (`button2`, `textBox4` — первое свободное), ставит `Control.Name`/`ToolStripItem.Name`,
    и дальше они пишутся как обычные компоненты. Цена — большой дифф при первом сохранении
    рукописного файла (`samples/Gallery`), зато второй и все последующие сохранения диффа не дают.
    Файлы, созданные VS, inline-объектов не содержат — для них вопроса нет.
68. **Оракул «что писать» снят с настоящего WinForms**, как разметка в Ф5.0. Писатель пишет ровно те
    свойства, у которых `PropertyDescriptor.ShouldSerializeValue` = true, значит этот ответ у наших
    контролов обязан совпадать с WinForms. Два измерения: `tests/Shared/SerializationDump.cs`
    (все компоненты шести форм `samples/` после `InitializeComponent`, скомпилированных и против настоящего
    WinForms — `NetForms.Compat` теперь собирает сэмплы, — и против нас; `SerializationDiffTests`) и колонка
    `ShouldSerialize` у свежего экземпляра в `AttributeDump` (все 118 типов; `AttributeDiffTests`).
    Режимы `--attrs`/`--serialization` эталонного процесса работают под настройками `ApplicationConfiguration.Initialize()`
    (Segoe UI 9pt, `SetCompatibleTextRenderingDefault(false)`) — иначе каждый свежий контрол WinForms
    «просит» записать `UseCompatibleTextRendering`. До работы расходились 683 свойства свежих экземпляров
    и 1382 ключа сэмплов; теперь ноль, кроме поимённого списка объяснённых исключений в самих тестах
    (масштабирование GenLabs 6×13→7×15 по решению 8, затенённые дизайнером `Visible`/`Enabled` у `TabPage`,
    внутренние контролы `ComboBox`/`NumericUpDown` по решению 26, `ListBox.Size` из-за IntegralHeight).
    Значения, зависящие от метрик текста (`Size`, `Location`, `Font`), сравниваются только по наличию.
69. **`ShouldSerializeXxx`/`ResetXxx` — около 130 методов**, по образцу WinForms: амбиентные свойства
    (`BackColor`, `ForeColor`, `Font`, `Cursor`, `RightToLeft`) пишутся, только если заданы на самом
    контроле; `Enabled`/`Visible` — по собственному флагу; `Margin`/`Padding`/`Min/MaximumSize` — против
    `DefaultXxx`; элементы полос, `DataGridView` (стили ячеек — против снимка стилей нового экземпляра),
    `NumericUpDown`, `LinkLabel`, `ToolTip` (задержки — против того, что следует из `AutomaticDelay`) и т.д.
    Методы `internal` (часто `virtual`), а не `private`: `TypeDescriptor` ищет метод на типе, объявившем
    свойство, а многие свойства переобъявлены с `new` (решение 53) — `internal` метод базы оттуда виден,
    `private` нет. Несколько «умолчаний», которые на деле были явно заданными значениями, переделаны
    в настоящие умолчания: `BackColor = Window` у `ListView`/`TreeView`, `Control` у `PropertyGrid`, курсоры
    `TextBox` (I-beam) и `LinkLabel` (рука) — теперь через `DefaultCursor`; геттер `Control.Cursor`, как в
    WinForms, отдаёт нестандартный `DefaultCursor` раньше, чем курсор родителя.
70. **`ToolStrip.AutoSize = true`, как в WinForms** (закрывает открытый вопрос Ф5.0). Мешало то, что наш
    `AutoSize` у докнутого контрола менял оба размера. Теперь семантика `DefaultLayout.xGetDockedSize`:
    докнутый AutoSize-ребёнок получает предпочтительный размер только поперёк дока (высоту у Top/Bottom,
    ширину у Left/Right), вдоль — остаток; сам себя он не ресайзит, а просит родителя переложить.
    `Control.LayoutAutoSize` — это AutoSize «с точки зрения раскладки» (`CommonProperties.GetAutoSize`): у
    `TextBox` он false, его фиксированная высота — отдельный механизм. Предпочтительный размер полосы, как
    `GetPreferredSizeHorizontal`: поперёк не меньше `DefaultSize − Padding` (25 у ToolStrip, 22 у StatusStrip,
    24 у MenuStrip). Выяснено дифф-прогоном (новые ключи `stripautosize/*`, все совпадают): у `ToolStripLabel`
    нет 2px-рамки вокруг содержимого (`ToolStripLabelLayout.borderSize = 0`), у `ToolStripComboBox`/`ToolStripTextBox`
    поле (1,0,1,0) на полосе и 2 в выпадающем меню, а контрол фиксированной высоты центрируется в элементе
    (комбо 23px в полосе 25px — на y=1).
71. **Другие расхождения с WinForms, найденные оракулом и воротами Ф5.2, исправлены:**
    - `AutoSizeMode` был объявлен на `ButtonBase`, т.е. был и у `CheckBox`/`RadioButton` (в WinForms только
      у `Button`), и флажки с `AutoSize` не сжимались (GrowOnly). Перенесён в `Button`.
    - `ToolStripDropDown` больше не докается `Fill` внутрь своего окна: окно открывается по предпочтительному
      размеру, AutoSize-список сам ему равен, фиксированный растягивается хостом. `Dock` — `None`.
    - `MenuStrip.GripMargin` стартовал с 2 вместо собственного `DefaultGripMargin` (пусто).
    - `ToolStripItem.Size = …` выключал `AutoSize`; в WinForms сеттер только задаёт границы (VS пишет `Size`
      каждого элемента, и они остаются AutoSize).
    - `ListBox.ItemHeight` в режиме `DrawMode.Normal` теперь следует шрифту, как в WinForms (там
      значение спрашивается у нативного списка); заданное число действует только в owner-draw.
    - **Настоящий баг:** чтение `ToolStrip.DisplayedItems` строило новую `ToolStripItemCollection`, чья
      `Insert` сначала удаляла каждый элемент из настоящих `Items` полосы. Теперь это read-only представление,
      как в WinForms (изменяющие методы бросают `NotSupportedException`).
72. **`FontConverter`** (строка «Segoe UI, 9pt, style=Bold» и `InstanceDescriptor` по самому короткому
    конструктору, сохраняющему значение: charset 1 и пункты — умолчания), висит на `Font`.
    **`TableLayoutPanel`, `FlowLayoutPanel`, `ToolTip` стали `IExtenderProvider`** с `[ProvideProperty]` и
    атрибутами на `GetXxx`, как в WinForms (`Column`/`Row`/`CellPosition` — Hidden, их позиция пишется в
    `Controls.Add(child, column, row)`).
73. **`DesignerCodeWriter`** (`src/NetForms.Design.Serialization`) пишет `InitializeComponent` из живых объектов
    в каноническом порядке VS: сохранённые локальные переменные, `components = new Container()` (если хоть
    одному компоненту нужен конструктор `(IContainer)`), создания в порядке компонентов, затем для каждого
    `BeginInit` (ISupportInitialize) → `SuspendLayout` вложенных (`splitContainer1.Panel1`) → свой
    `SuspendLayout`, блоки компонентов с заголовком `// name`, блок корня, в конце в том же порядке
    `ResumeLayout(false)` вложенных → `EndInit` → свой `ResumeLayout(false)` (+ `PerformLayout()`, если
    среди детей есть AutoSize — правило `ControlCodeDomSerializer`; у полос всегда). В блоке — свойства по
    алфавиту так, как сортирует `PropertyDescriptorCollection.Sort` (культурно, регистр вторичен:
    `Checked` раньше `CheckOnClick`), коллекции-Content (`Controls.Add` по одному, в TLP — с ячейкой;
    `Items.AddRange` у полос; `AddRange`, где он есть, кроме стилей TLP), вложенные компоненты под своим
    подзаголовком, extender-свойства (`toolTip1.SetToolTip(button1, …)` в блоке кнопки, по имени свойства),
    `Name`, затем события по имени. Значения — литералы, флаговые перечисления разложены как
    `EnumConverter` (для `Keys` модификаторы первыми, как `KeysConverter`), остальное — через
    `InstanceDescriptor` конвертера. Правила `ControlDesigner` поверх ответа компонента: `Location` и
    `Size` у контрола на поверхности пишутся всегда (у выпадающих меню — лоток компонентов — нет),
    `Visible` никогда не пишется у корня, `TabPage` и выпадающих списков; `AutoScaleDimensions`/`AutoScaleMode`
    корня (в WinForms Hidden) — первыми строками блока, как у `ContainerControlCodeDomSerializer`.
    Остальной файл не трогается; поля идут за компонентами: переименование переименовывает объявление,
    удаление убирает строку, новые — после последнего поля-компонента.
74. **Диалект файла сохраняется.** `DesignerCodeStyle.Detect`: если операторы квалифицированы `this.`, файл
    пишется «классическим» CodeDom-стилем (полные имена типов, `((byte)(204))`, `new System.EventHandler(this.x)`,
    массив по элементу на строку, `// ` с пробелом), иначе современным .NET-стилем. Короткое имя типа
    пишется, только если неявные using-и WinForms-проекта и using-и файла делают его однозначным, поэтому
    `System.Windows.Forms.Timer` остаётся полным (есть `System.Threading.Timer`) — ровно как у VS.
    GenLabs (классика) возвращается почти байт-в-байт: отличаются только размеры AutoSize-меток и полей
    (наши метрики текста) и выброшенная закомментированная строка внутри метода (VS делает так же).
75. **Чего писатель не умеет выразить, он не угадывает.** Значение без кодовой формы (элементы `ListView`,
    узлы `TreeView`, стили ячеек `DataGridView` — VS пишет их через локальные переменные) берётся дословно из
    исходного присваивания/вызова, если оно всё ещё верно; иначе — `NotSupportedException` с именем
    свойства. Присваивания и вызовы заглушек (`DesignerPlaceholder`) пишутся дословно; читатель теперь
    принимает `((ISupportInitialize)gauge1).BeginInit()` у заглушки.
76. **API правки модели** (понадобится хосту Ф5.3): `AddComponent` (имя по `NameCreationService`),
    `RemoveComponent`, `Rename` (поле и `Name`; ссылки в пользовательском коде — задача Ф5.5 через Roslyn),
    `BindEvent`/`UnbindEvent` (одно событие — один обработчик, как вкладка Events), `CreateUniqueName`.
77. **Ворота Ф5.2 закрыты.** `DesignerCodeWriterTests.WrittenFileCompilesToTheSameTree` для всех шести форм
    `samples/`: прочитать → записать → (1) записанный файл вместе с остальными исходниками сэмпла
    компилируется Roslyn-ом в память, и его `InitializeComponent()` строит то же дерево и те же значения всех
    browsable-свойств, что и оригинал (после того как раскладка отработала — записанный файл, как у VS,
    вызывает `PerformLayout` там, где рукописный сэмпл не вызывал); (2) читатель читает записанный файл в то
    же дерево, что даёт его компиляция; (3) повторная запись не меняет ни байта. Плюс тесты формата
    (HelloForms — файл VS — возвращается байт-в-байт, кроме размера AutoSize-метки; порядок и стиль;
    классический диалект), правок (добавить кнопку с обработчиком → компилируется и работает;
    переименовать; удалить) и написания значений (перечисления, `Keys`, цвета, шрифты, `decimal`,
    экранирование строк, extender-ы, `Timer(components)`), с проверкой, что всё читается обратно.
78. **Среда: .NET 10.0.12 поменял `SystemColors` на Windows.** Во время сессии VS Installer обновил SDK
    (10.0.102 → 10.0.401, рантайм 10.0.12); теперь `SystemColors.Control` на Windows — `#F0F0F0`
    (системная палитра), а golden-эталоны записаны с `#ECE9D8`. Поэтому `gallery-*` и `messagebox`
    расходятся уже на 6–20% пикселей вместо ~1% — фон, а не код. Вопрос «сделать golden герметичными»
    из открытых вопросов Ф4 стал обязательным (см. ниже).

### Ф5.3 — хост дизайнера и протокол (2026-09-22)

79. **`src/NetForms.Design` — сервисы дизайн-тайма, `tools/NetFormsDesigner.Host` — процесс.** Хост — тонкий
    цикл «строка JSON из stdin → строка JSON в stdout» над `DesignerProtocol` (в библиотеке, поэтому тесты
    гоняют протокол в процессе, без UI); всё, что кто-то напечатает в `Console`, уходит в stderr и не может
    сломать поток. Методы: `hello`, `open {path, autoSave}`, `render`, `apply {ops}`, `undo`, `redo`,
    `properties {id}`, `events {id}`, `toolbox`, `save`, `close`. Ошибки трёх видов: `code` (файл не читается —
    с позицией, решение 62), `edit` (правка отклонена, ничего не изменилось), `internal`.
80. **Файл — источник истины.** `DesignSurface.Apply` применяет пакет правок к живым объектам, пишет файл
    (`DesignerCodeWriter`) и **перечитывает** его (`DesignerCodeReader`): модель, которую видит клиент, — ровно
    та, что даёт новый файл, а проверка «записанное читается» бесплатна. Любая ошибка в пакете (правка, запись,
    перечитывание) откатывает и исходник, и модель. Undo/redo — снимки двух файлов (designer-файл и
    code-behind, куда пишутся заглушки обработчиков), как история GoFormsDesigner; с `autoSave` (по умолчанию)
    каждое действие сразу пишется на диск, BOM сохраняется.
81. **Словарь правок — как у GoFormsDesigner** (порт webview в Ф5.4 остаётся тонким): `setBounds`, `setForm`,
    `setProp` (текст конвертера в инвариантной культуре, компонент — по имени), `resetProp`, `setItems`,
    `setEvent` (пустой обработчик — отвязать), `add`, `remove`, `setParent`, `rename`, `bringToFront`,
    `sendToBack`. Адрес — имя поля; `""` — корень; `splitContainer1.Panel1` — вложенный компонент.
    `add` повторяет `InitializeNewComponent` дизайнеров VS: `Text = имя` у кнопок, меток, групп, вкладок и
    элементов полос, `AutoSize` у меток и флажков, `UseVisualStyleBackColor`, две вкладки у нового
    `TabControl`, `MainMenuStrip` для первого `MenuStrip`, `TabIndex` = число соседей, новый контрол —
    наверху z-order. `remove` уносит детей, элементы полос и меню, колонки и обнуляет ссылки на удалённое
    (`MainMenuStrip`, `ContextMenuStrip`).
82. **Ничего не исполняется.** Хост ставит `HeadlessPlatform` (друг сборки NetForms — `InternalsVisibleTo`, как
    сборки дизайнера VS у WinForms): окон нет, таймеры не тикают, ввод до контролов не доходит — клик в
    дизайнере выделяет, а не нажимает. `Visible`/`Enabled` — теневые свойства дизайнера (как у
    `ControlDesigner`): скрытый кодом контрол рисуется и выделяется, а записываемое значение хранится в модели
    (`DesignerComponent.ShadowProperties`), писатель берёт его оттуда (закрывает открытый вопрос Ф5.1).
83. **Вид для клиента** (`DesignerView`): PNG клиентской области, нарисованный самим NetForms, и прямоугольник
    каждого компонента в координатах канвы, в порядке отрисовки (клиент бьёт хит-тест с конца), с флагами
    `container`/`movable`/`resizable`, `dock`, `anchor`, `margin`/`padding` (для направляющих), элементы полос
    отдельно (`kind: item`), лоток компонентов (`Timer`, `ToolTip`, `ContextMenuStrip`). Рамку окна и
    заголовок рисует клиент. Панель свойств — `TypeDescriptor` + `[Browsable]`, с видом редактора (`bool`,
    `enum`, `flags`, `color`, `font`, `component` со списком подходящих компонентов, `collection`) и
    признаком «пишется в файл» (жирный в сетке VS); события — с параметрами обработчика и именем по
    умолчанию (`button1_Click`, `MainForm_Load`). Тулбокс сгруппирован как в VS, в нём только то, что в
    NetForms уже есть.
84. **`EventHandlerWriter`**: заглушка `private void button1_Click(object sender, EventArgs e) { }` в конце класса
    code-behind, если метода с таким именем там нет; хост возвращает файл и строку — для «перейти к обработчику».
85. **Найдено и исправлено по дороге:** `Control.BringToFront()`/`SendToBack()` отсутствовали (API WinForms);
    геометрия `TabControl` сведена с настоящим контролом (новые ключи `tabs/*`): вкладка высотой шрифт + 4 на
    y=2, ширина = текст без GDI-отступов + 2×`Padding.X`, страница с y = 24 при Segoe UI 9pt (было 25 —
    после первой правки в дизайнере страницы уезжали на пиксель), `ItemSize` без явного значения отдаёт
    размер первой вкладки.
86. **Ворота Ф5.3 закрыты.** `DesignerHostTests`: открыть копию `samples/Gallery`, подвинуть контрол,
    сохранить, перечитать — модель та же, кроме подвинутого; каждый сэмпл открывается и рисуется; добавление
    с умолчаниями VS, удаление с детьми и ссылками, undo/redo обоих файлов, откат неудачного пакета, теневые
    `Visible`/`Enabled`, перенос в контейнер, вложенные панели, панель свойств и события, переименование,
    протокол строка-в-строку с ошибками всех трёх видов. Плюс ручной прогон настоящего процесса через
    stdin/stdout.

### Открытые вопросы Ф5.2

- ~~**Golden-эталоны.**~~ Закрыто в Ф6, решение 105. Теперь их ломает не только сглаживание текста, но и палитра ОС. Предложение: в
  тестовой платформе подменять системную палитру фиксированной (через тему, а не через BCL — решение 10
  про `SystemColors` для пользовательского кода остаётся), а эталоны перезаписать один раз осознанно.
- ~~**Коллекции без кодовой формы**~~ — закрыто в Ф5.5, решение 100.
- ~~**Метрики текста в записанных размерах.**~~ Закрыто в Ф6, решение 110: причина — GDI+-эталон Label. Размер AutoSize-метки пишется нашим (37×22 против 38×15 у VS
  при Segoe UI 9pt): у WinForms метка сжимается к 15 только в дизайнере без раскладки; `PreferredSize`
  совпадает (37×21 против 37×22). Для файлов, которые правят и VS, и NetForms, это дребезг в пару пикселей.
- **Шрифт «Microsoft Sans Serif» на Windows** (растровый .fon) Skia не находит — подставляется запасной,
  поэтому высота `TextBox` в GenLabs 23, а не 20.

### Ф5.4–Ф5.7 — расширение VS Code, шаблоны, перевод проектов (2026-09-22/23)

87. **Расширение — `designer/`** (TypeScript, собирается esbuild в один `dist/extension.js`). Custom text
    editor `netforms.designer` на `*.Designer.cs` с приоритетом `default` — файл формы открывается холстом,
    как в VS; «Open as Text» возвращает текст. Один процесс хоста (`tools/NetFormsDesigner.Host`) на открытый
    редактор, протокол Ф5.3 строками JSON; хост ищется: настройка `netforms.designerHostPath` → `host/` внутри
    расширения → сборка в копии NetForms выше открытого файла (удобно при разработке самого NetForms).
    Команды: Open Visual Designer, Open as Text, View Code (F7), View Designer (Shift+F7), Tidy Designer File,
    Create New Project…, New Form…, Convert WinForms Project…, Run Project, Set Designer Host Path…, Check Setup.
    Правка файла как текста (или git-ом) → холст перечитывается после сохранения; свои записи хост делает
    сам (файл — источник истины, решение 80), поэтому undo/redo — свои (Ctrl+Z/Ctrl+Y внутри холста), как у
    GoFormsDesigner.
88. **Webview — только хром, форма — PNG от хоста.** `media/geometry.js` — чистая математика без DOM (привязка
    к краям, центрам и отступам `Margin`/`Padding` соседей и контейнера, изменение размера за одну кромку,
    выравнивания Format по последнему выделенному, равные интервалы, хит-тест самого глубокого видимого,
    запрет бросить контейнер внутрь себя, кромки для докнутых); `media/designer.js` — выделение (Ctrl/Shift —
    группа, Escape — уровень вверх, клик по заголовку/фону — форма), перетаскивание группой в пределах одного
    контейнера, перенос в другой контейнер, грипы формы, стрелки (Ctrl — по 8, Shift — размер), Delete, масштаб,
    режим «порядок обхода» (щелчки задают `TabIndex`), на передний/задний план, **блокировка** (выделять и
    смотреть можно, двигать нельзя), тулбокс с поиском (клик — в выбранный контейнер, перетаскивание — в точку
    сброса, компоненты — в лоток), панель свойств по категориям с редактором под тип (галочка, список
    перечисления, флажки флагов, цвет, коллекции строк построчно, сброс к умолчанию) и вкладка событий
    (двойной щелчок — заглушка и переход к ней). Двойной щелчок по контролу — событие по умолчанию
    (`[DefaultEvent]`). **Докнутый контрол** обведён пунктиром, не таскается и тянется только за свободную
    кромку (Top → низ, Fill → никак), как в дизайнере WinForms.
    Выделение перекрашивается на месте, без перестроения холста: перестроение между двумя кликами съедало
    `dblclick` (найдено UI-тестом).
89. **Локализация: английский и русский.** Заголовки команд и описания настроек — `package.nls.json` /
    `package.nls.ru.json`; сообщения расширения — `vscode.l10n.t` с `l10n/bundle.l10n.ru.json`; webview получает
    тот же словарь (`vscode.l10n.bundle`) в сообщении `init`, ключ — английский текст. В словаре и имена,
    приходящие от хоста: группы тулбокса и категории свойств WinForms («Внешний вид», «Поведение», …), как в
    русской VS. Описания свойств (`[Description]` из ресурсов WinForms, решение 59) остаются английскими.
    Язык — язык интерфейса VS Code, своей настройки нет.
90. **Тесты расширения** (`npm test`, node --test): математика холста (13), полнота перевода (каждая строка
    `T(...)`/`l10n.t(...)` есть в русском словаре с теми же `{0}`; каждый `%ключ%` package.json — в обоих
    nls), протокол с настоящим хостом, и **UI-тест**: `test/ui/harness.js` поднимает webview в настоящем
    браузере (headless Edge/Chrome через puppeteer-core) с подменой `acquireVsCodeApi`, которая говорит с
    настоящим хостом, — мышью двигает, блокирует, тянет за ручку, задаёт `Text`, добавляет из тулбокса,
    отменяет/повторяет, удаляет, двойным щелчком вешает `Click`, и каждое действие проверяется в файле на
    диске; скриншоты — в `test/ui/out/`; второй прогон — в русском интерфейсе. Плюс ручная проверка в настоящем
    VS Code (форма открывается холстом, скриншот сверен).
91. **Шаблоны `dotnet new`** (`templates/`, пакет `NetForms.Templates`): `netforms` (Program.cs, MainForm.cs,
    MainForm.Designer.cs, .csproj с неявными using-ами, которые давал `UseWindowsForms`), `netforms-form`,
    `netforms-usercontrol`. Пока пакета `NetForms` на NuGet нет, `--FrameworkPath <копия NetForms>` ставит
    `ProjectReference` вместо `PackageReference`. Item-шаблоны берут пространство имён из проекта
    (`bind msbuild:RootNamespace`) и, как `dotnet new class` в SDK, требуют C#-проект, прошедший restore
    (`project-capability` constraint), — иначе в файле остался бы заполнитель. Designer-файлы шаблонов —
    ровно то, что пишет наш писатель (открыть новую форму и сохранить — ноль диффа). Расширение создаёт
    проекты и формы из тех же файлов (копирует их в себя при упаковке), подставляя имя и пространство имён
    само, без `dotnet new`.
92. **Упаковка.** `npm run package` → `.vsix` с хостом и конвертером, опубликованными в `host/`
    (framework-dependent, нужен .NET 10). Опубликованное прореживается (`scripts/prune-host.js`): нативные
    SkiaSharp/HarfBuzz — только для платформ десктопного VS Code (win-x64/arm64, linux-x64/arm64, osx),
    без нативных pdb (300 МБ одних Windows-символов) и без сателлитных сборок Roslyn: 190 МБ → 41 МБ.
    Проверено: `.vsix` ставится в изолированную папку расширений, установленный хост открывает и рисует Gallery.
93. **Найдено шаблонами:** корень-`UserControl` получал в файле `Location` и `TabIndex`. `Location` корня —
    тень `DocumentDesigner` с умолчанием (0, 0) (у `Form` это уже было через `ShouldSerializeLocation`),
    `TabIndex` корня VS не пишет вовсе. Оба правила — в писателе, для любого корня.
94. **Конвертер WinForms → NetForms** (`tools/NetForms.Convert`, пространство имён `NetForms.Converter` —
    `NetForms.Convert` заслоняло `System.Convert`). `Analyze` ничего не меняет и возвращает отчёт: SDK-стиль
    или нет, TFM, использует ли WinForms, уже ли на NetForms, список правок проекта, проблемы с файлом и
    строкой, и какие формы откроет дизайнер (`DesignerCodeReader`, решение 62). Исходники компилируются
    Roslyn-ом (как библиотека — важно, компилируется ли, а не есть ли `Main`) против сборок NetForms;
    ошибка про тип/член, чьё имя есть в `winforms-types.txt` (1245 публичных типов настоящих
    `System.Windows.Forms`+`System.Drawing`, снятых `tests/NetForms.Compat --types`), — «нет в NetForms»,
    остальное — «ошибка компиляции». Отдельный синтаксический проход ищет то, что компилируется, но не
    переносится: P/Invoke в `user32`/`gdi32`/`comctl32`/…, `WndProc`/`CreateParams`, `Registry`, `AxHost`,
    бинарные ресурсы в `.resx` (до Ф6), `UseWPF`, `Application*`-свойства проекта. Для «Windows + Linux»
    Windows-only API — предупреждение, для «только Windows» — справка.
95. **`Apply`**: только SDK-стиль (старый формат — отчёт «сначала upgrade-assistant», файл не трогается);
    исходный `.csproj` → `.csproj.winforms.bak`; убирается `UseWindowsForms`, `net8.0-windows` →
    `net10.0` (или `net10.0-windows` в режиме «только Windows»), добавляются ссылка на NetForms (пакет
    или `ProjectReference` на копию) и `<Using Include="System.Drawing" />`/`System.Windows.Forms`.
    Повторный запуск ничего не делает (`AlreadyNetForms`). CLI: `NetForms.Convert <csproj> [--apply]
    [--target cross|windows] [--framework-path <копия>] [--json]`; команда расширения показывает отчёт
    Markdown-превью со ссылками на строки и спрашивает режим перед правкой.
96. **Ворота Ф5.6 закрыты.** `TemplateTests`: designer-файлы шаблонов — неподвижная точка писателя; каждый
    шаблон компилируется против NetForms; `dotnet new install` (изолированный hive) → `dotnet new netforms` →
    restore → `netforms-form` + `netforms-usercontrol` (пространство имён проекта) → `dotnet build` без ошибок.
    `.vsix` собирается, ставится, его хост работает (решение 92).
97. **Ворота Ф5.4 закрыты** по чек-листу GoFormsDesigner (README): перемещение и размер, грипы формы, выбор
    формы кликом и Escape, группа Ctrl/Shift, привязка с линией, свои undo/redo, Format по последнему
    выделенному, масштаб, блокировка, порядок обхода, пунктир докнутых, F7/Shift+F7, лоток компонентов,
    русский интерфейс, ничего из сети (CSP `default-src 'none'`). Не перенесено осознанно: редактор темы
    (темы — Ф6), сборка под WebAssembly/Android (у NetForms таких целей нет), отладка F5 (её даёт C#-расширение
    VS Code по обычному `launch.json`; своя команда — только `Run Project`, `dotnet run` в терминале).
98. **Ворота Ф5.7 закрыты в малом**, корпус — открыт. `ConvertTests`: проект ровно как его создаёт VS
    (`net8.0-windows`, `UseWindowsForms`, HelloForms) анализируется без изменений, переводится и собирается
    `dotnet build` против NetForms; Windows-only код и отсутствующий API (`RichTextBox`) попадают в отчёт с
    местом; старый формат не трогается; режим «только Windows» оставляет `-windows`. Прогон по корпусу
    реальных WinForms-проектов с метрикой «собралось без ручной правки» — открытый вопрос ниже.

### Открытые вопросы Ф5.4–Ф5.7

- **Корпус для конвертера** (ворота Ф5.7 в полном виде): набор реальных WinForms-проектов (примеры
  dotnet/winforms, открытые приложения) в CI с процентом «собралось без правки» в README. Нужен выбор корпуса
  и лицензий — это решение заказчика.
- ~~**LICENSE.**~~ Создан при подготовке к публикации, решение 127.
- ~~**Платформенные `.vsix`.**~~ Сделаны, решение 129.
- **Хост требует установленный .NET 10.** Self-contained-публикация хоста увеличит пакет на ~70 МБ на
  платформу; решать вместе с платформенными пакетами. `Check Setup` показывает, найден ли dotnet.
- ~~**Остаток Ф5.5:** переименование ссылок в коде, коллекции без кодовой формы.~~ Закрыто, решения 99–104.

### Ф5.5 — остаток: переименование в коде, коллекции через локальные переменные (2026-09-23)

99. **Переименование уносит с собой код формы.** `CodeRenamer` (NetForms.Design.Serialization) компилирует
    Roslyn-ом парный файл (`MainForm.cs`) вместе с designer-файлом *до* переименования и меняет только те
    идентификаторы, что семантически ссылаются на поле компонента, — локальная переменная или параметр с тем
    же именем, строка, комментарий остаются. Обработчики, названные по компоненту (`button1_Click` →
    `okButton_Click`), переименовываются вместе с объявлением, вызовами и всеми привязками (событие другого
    контрола на тот же метод тоже), если метод объявлен в коде формы и новое имя свободно; обработчик с
    другим именем (`Save`) не трогается. Всё — один шаг undo. Видит только парный файл: поле компонента
    `private`, других файлов проекта оно не касается.
100. **Объекты без однострочной формы — локальными переменными, как у VS.** `TreeNode`, `ListViewItem`,
    `ListViewGroup`, `DataGridViewCellStyle`: объявления наверху метода (`TreeNode treeNode1 = new
    TreeNode("Child");`), их свойства — в блоке владельца прямо перед строкой, которая их использует
    (`treeNode1.Name = …` перед `Nodes.AddRange`, стиль — перед `ColumnHeadersDefaultCellStyle = …`).
    Узлы — в обратном обходе (дети раньше родителя: конструктор родителя их принимает), конструкторы — как у
    `TreeNodeConverter`/`ListViewItemConverter`/`ListViewGroupConverter` (текст; текст и картинки; массив
    подэлементов с `-1`; заголовок и выравнивание группы). Нумерация — по типу, в порядке сериализации;
    поэтому свойства теперь обходятся в порядке имён, как это делает сериализатор VS (группы `ListView`
    существуют раньше элементов, которые на них ссылаются). Локальные переменные этих типов из исходного
    файла не копируются, а пишутся заново, их имена не считаются занятыми.
    **Проверено настоящим сериализатором VS:** `System.Windows.Forms.Design` из рантайма Windows Desktop
    поднимает `DesignSurface` в процессе, `TypeCodeDomSerializer` сериализует форму, `CSharpCodeProvider`
    печатает C# — порядок, конструкторы и места присваиваний совпали. (Отличия голой `DesignSurface` от VS:
    имена локальных переменных в нижнем регистре — у VS свой сервис имён, — и теневые `Enabled`/`Visible`.)
    **100а.** Элемент со стилем пишется ровно по `ListViewItemConverter` (сверено тем же сериализатором):
    подэлемент со своим цветом/шрифтом делает весь элемент массивом `ListViewSubItem[]`, где у
    стилизованных — *вычисленные* цвета и шрифт (`SystemColors.Window`, `new Font("Segoe UI", 9F)`), у
    остальных `(null, text)`; элемент со своим стилем — `(string[], imageIndex, fore, back, font)` с
    *собственными* значениями (`Color.Empty`, `null`); с `ImageKey` — `(text, "key")`. Добавлены
    недостающие конструкторы WinForms `ListViewItem(string[], int|string, Color, Color, Font)` и
    `(ListViewSubItem[], string)`; стиль первого подэлемента становится стилем элемента, как в WinForms.
101. **Оракул атрибутов расширен на не-компоненты**, которые дизайнер пишет по свойствам: `TreeNode`,
    `ListViewItem`, `ListViewGroup`, `DataGridViewCellStyle`, `DataGridViewRow`. Разметка сгенерирована
    `tools/NetForms.Markup` (mark + shadow); найдено и исправлено: `new ListViewGroup()` — заголовок
    «ListViewGroup» и `Name = null`; конструкторы `ListViewGroup(string, HorizontalAlignment)` и
    `ListViewItem(string[], string imageKey)`; `ShouldSerialize` цветов узла, всех «пустых» членов стиля
    ячейки, `DefaultCellStyle` полосы (только созданный и непустой), `Height` строки (умолчание —
    `DefaultFont.Height + 9`, как в WinForms, было 22) и `Resizable`; `ToString` стиля — как в WinForms.
    Генератор `shadow` дал `DataGridViewRow.Index`/`DataGridView`/`State` публичные сеттеры — исправлено руками
    (в WinForms они только для чтения); на это стоит смотреть при каждом его запуске.
102. **Эффект дескриптора в дизайнере:** у свежего `TreeView` без окна WinForms отдаёт `LineColor = Empty`
    и просит его записать, а в VS окно у контрола есть, и значение читается как умолчание. Писатель не
    пишет пустой `LineColor` — правило рядом с правилами `ControlDesigner`.
103. **`samples/Gallery/CollectionsForm`** — designer-файл в формате VS (узлы с вложенностью, группа и
    элементы `ListView` с подэлементами, стиль заголовков `DataGridView`). Он во всех воротах (читатель,
    писатель, хост, оракул сериализации), и писатель воспроизводит его **байт-в-байт**. Известная разница в
    оракуле сериализации: у WinForms полосы прокрутки `DataGridView` — дочерние контролы (не компоненты,
    VS их не пишет), у нас они рисуются.
104. **Редакторы коллекций в панели свойств.** Хост отдаёт коллекцию строками с форматом
    (`DesignerPropertyRow.ItemsFormat`): `lines` (строка — элемент), `tree` (узел на строку, два пробела на
    уровень; в webview Tab/Shift+Tab сдвигают строку), `columns` (подэлементы `ListViewItem` через `|`).
    Узел или элемент с неизменным текстом и местом остаётся тем же объектом (имя, картинка, группа, отметка
    сохраняются), новый узел получает имя по тексту. Ступенька отступа больше чем на уровень — ошибка правки.
    Тесты: хост (`TreeNodesAndListViewItems…`: правка → файл в формате VS → читается в то же дерево) и UI в
    браузере (Enter, Tab, «Third» → `new TreeNode("Second", new TreeNode[] { treeNode3 })` на диске).
    Проверка синтаксиса скриптов webview добавлена в `npm test` (двойной обратный слэш в heredoc однажды
    уже сломал `designer.js`).

**Ворота Ф5.5 закрыты:** «перетащить кнопку, переименовать, задать Text, повесить Click» — UI-тест и тесты
хоста (переименование уносит код и обработчик, проект компилируется); Tidy — повторная запись канонична
(ворота Ф5.2, неподвижная точка на всех сэмплах).

### Открытые вопросы Ф5.5

- **CodeDom как оракул формата целиком.** Режим `NetForms.Compat --codedom` мог бы строить сценарные формы
  на настоящей `DesignSurface` и сравнивать классический вывод VS с нашим классическим диалектом. Мешают
  отличия голой поверхности от VS (сервис имён, теневые свойства `ControlDesigner`, отсутствие
  `BeginInit`/`EndInit` без загрузчика VS); нужен свой `DesignerLoader`, повторяющий VS. Пока — разовая
  сверка (решение 100).
- Стиль подэлемента `ListViewItem` задаётся пока только в коде (в редакторе строк его нет); записывается он
  как у VS — см. решение 100а.

### Ф6 — полировка (2026-09-23)

105. **Golden-эталоны герметичны.** `HermeticRendering` (NetForms.Drawing, internal) — два переключателя,
    выключенных в приложении и включённых для всего тестового процесса (`[ModuleInitializer]`, а не в
    `TestPlatform.Install` — иначе режим зависел от порядка тестов, и `primitives` записывался то так, то так):
    - **палитра системных цветов**: системный цвет (`SystemColors.Control`…) превращается в Skia-цвет через
      зафиксированную палитру в единственной точке перевода (`SkiaConvert.ToSK`) и в арифметике
      `ControlPaint.Light/Dark`. Палитра — стандартная светлая Windows 10/11 (как её отдаёт `GetSysColor`):
      то, что пользователь WinForms видит по умолчанию. Причина прежнего «дрейфа»: в .NET 10 на Windows
      `KnownColorTable` хранит для системных цветов индексы `COLOR_*` и читает ОС (плюс новая таблица
      `AlternateSystemColors` — тёмный режим), а вне Windows у BCL своя таблица эпохи XP (`#ECE9D8`) —
      эталоны когда-то оказались записаны с ней. Пользовательский код по-прежнему видит `SystemColors` BCL
      (решение 10);
    - **текст контурами**: строка рисуется как контуры глифов (`SKFont.GetTextPath`), залитые растеризатором
      Skia. Обычный вывод глифов идёт через DirectWrite на Windows и FreeType на Linux — их сглаживание
      разное и меняется с обновлениями ОС (это и был ~1% «плавающих» пикселей при неизменном коде);
      контуры берутся из файла шрифта (DejaVu из `tests/NetForms.Tests/Fonts`).
    Эталоны перезаписаны один раз осознанно (они и так устарели: у витрины добавилась вкладка Panels и
    поменялась геометрия вкладок, решение 85). **Весь набор зелёный: 263/263** — впервые с Ф4. Закрывает
    открытые вопросы «Golden-эталоны» Ф4 и Ф5.2.
106. **Набор проверен на Linux: 263/263** (WSL Ubuntu 26.04, .NET 10 в `~/.dotnet` без root, `libicu`
    распакован рядом через `apt-get download` + `dpkg -x`; сценарий — `tools/wsl-test.sh`, та же половина CI,
    что `ubuntu-latest`, только локально). Найдено и исправлено:
    - FreeType хинтует контуры глифов на мелких кеглях, DirectWrite — нет: в герметичном режиме шрифт без
      хинтинга и с линейными (нехинтованными) метриками — и контуры, и ширины берутся из файла шрифта. На
      Windows эталоны от этого не сдвинулись, `CompatDiffTests` показывает ровно прежний список Ф4;
    - тесты, сравнивавшие пиксель с `SystemColors.Control.ToArgb()`, сравнивают с тем, *как он рисуется*
      (`HermeticRendering.Resolve`): вне Windows BCL отдаёт свою таблицу, а рисуется зафиксированная палитра;
    - тест хоста сравнивал границы вкладок из файла (посчитаны VS с Segoe UI) с границами после раскладки;
      на машине без Segoe UI полоса вкладок на 2 пикселя ниже — теперь раскладываются обе стороны.
    **CI** дополнен: на windows-latest три оракула (сценарии, атрибуты, сериализация) идут в тесты
    переменными окружения; отдельная задача `designer` на обеих ОС собирает хост и гоняет `npm test`
    (математика, перевод, протокол, UI в headless Chrome/Edge) и выкладывает скриншоты UI.
107. **`.resx` — картинки и значки из ресурсов формы, как у VS.** Цепочка имён: .resx пишет
    `type="System.Drawing.Bitmap, System.Drawing"`, `System.Drawing.dll` общего фреймворка .NET пересылает
    `Bitmap`/`Icon`/`Font`… в `System.Drawing.Common`, а его нет. Обработчик `AssemblyLoadContext.Resolving`
    не годится (рантайм требует, чтобы имя сборки совпало), поэтому NetForms везёт **фасад
    `src/NetForms.Drawing.Common` с именем сборки `System.Drawing.Common`**, который пересылает в
    `NetForms.Drawing` все 58 типов, общих с настоящим `System.Drawing.Common` (список сгенерирован по
    `winforms-types.txt`; тест падает, если новый тип забыли переслать). Побочный выигрыш: библиотеки,
    собранные против `System.Drawing.Common`, связываются с NetForms. Там же видна метрика покрытия: 132
    публичных типа настоящего `System.Drawing.Common` у нас пока нет (`BufferedGraphics`, `ImageAnimator`,
    `PathGradientBrush`, `BitmapData`…).
    Дальше — как в WinForms: `ImageConverter`/`IconConverter` (байты файла ↔ картинка, PNG на выходе) на
    `Image`/`Icon`; `System.Resources.Extensions` у NetForms (читает предсериализованные ресурсы);
    `GenerateResourceUsePreserializedResources` ставит `buildTransitive/NetForms.props` пакета, а проект со
    ссылкой на `NetForms.csproj` — сам (шаблон при `--FrameworkPath` и конвертер его пишут).
    `SkiaSharp.NativeAssets.Linux` теперь безусловно: приложение, собранное на Windows, запускается на Linux.
    **В дизайнере** `new ComponentResourceManager(typeof(Form1))` превращается в `ResxResources` — ресурсы
    берутся из `.resx` рядом с designer-файлом (проект не компилируется): строки, base64-байты через
    конвертер типа, типизированные строки (`Point`, `Size`), `ResXFileRef`. Значение создаётся при первом
    обращении, так что нечитаемый ресурс (BinaryFormatter — `ImageList.ImageStream`) ломает только своё место
    и понятным сообщением. Писатель оставляет `(Image)resources.GetObject("…")` дословно (правило исходного
    выражения, решение 75), пока значение то же.
    **Ворота:** `samples/Gallery/ResourcesForm` (+ `.resx` в формате VS: картинка `PictureBox` и значок формы)
    — во всех воротах (читатель, писатель байт-в-байт, хост, оракул сериализации: настоящий WinForms грузит
    ту же форму со встроенным .resx), `ResourcesTests` (пиксели картинки и значка после `GetObject`, старые
    имена сборок связываются с нашими типами) — на Windows и на Linux. Конвертер больше не считает картинки в
    `.resx` проблемой и называет только BinaryFormatter-записи.
108. **Локализуемая форма (`Localizable = true`) открывается, но не пишется.** Её свойства — в `.resx` по
    культурам через `resources.ApplyResources(button1, "button1")` (`ResxResources` применяет их сам: базовый
    класс отдал бы свойствам сырые записи). Писатель такой файл отказывается переписывать с объяснением —
    иначе он «разлокализовал» бы форму, выбросив вызовы; хост превращает отказ в ошибку правки, файл не
    меняется. Запись `.resx` — следующий шаг (открытый вопрос).
109. **Список расхождений Ф4 закрыт — по оракулу, каждый пункт.** Исправлено (каждое подтверждено
    `CompatDiffTests`, где мало данных — добавлены сценарии):
    - `Control.DisplayRectangle` — клиентская область; `Padding` вычитает только `ScrollableControl`
      (`anchor/padding/fill`: обычный `Control` докает на всю клиентскую область);
    - `TrackBar.Orientation` меняет местами ширину и высоту только у созданного контрола — или на
      поверхности дизайнера (в VS у контролов есть окна). Для этого хост **ситует компоненты**
      (`DesignSite`: имя, `DesignMode = true`, без сервисов), как делает VS;
    - однострочный `TextBox` хранит переводы строк, которые положил код (`Text`, `AppendText`): у Win32
      edit их нельзя только напечатать (`Lines` = 2). Вставка пользователем по-прежнему их выкидывает —
      оракул её не покрывает, гадать о правиле натива не стали;
    - `FlowLayoutPanel`: строка, которую закончил `SetFlowBreak`, считает высоту **вместе со следующим
      контролом** — особенность WinForms, воспроизведена; установлено тремя новыми сценариями
      (`flow/break*`, `break2*`, `break3*`: обычный перенос так не делает, более высокая строка просто
      побеждает);
    - AutoSize контейнера применяется раскладкой **родителя**: `Panel` без родителя не растёт (верхний
      `Form` — сам себе); с родителем — ровно прежние числа (`autosize/parented-*`);
    - `AutoScrollPosition` прокручивает только созданный контрол (в конструкторе — ничего, в `Load` — да);
      `DisplayRectangle` несозданного не вычитает полосы прокрутки (геометрия самих полос не тронута);
    - порядок событий фокуса — **два**, как в WinForms (решение 17 это предвидело): `Select()`/Tab/
      `ActiveControl` — `Leave, Validating, Validated, Enter, LostFocus, GotFocus` (UpdateFocusedControl до
      смены фокуса окна); `Focus()` и клик — `LostFocus` первым (Win32 SetFocus раньше).
    Оставлено осознанно: `dock/huge-left` — WinForms без окна хранит отрицательную ширину (-100), после
    создания окна Win32 зажимает её в 0, как мы всегда. `CompatDiffTests` получил таблицу
    `KnownDifferences` (ключ → причина): всё остальное различие — падение.
110. **Оракул сценариев запускается как приложение** (визуальные стили, GDI-текст, Segoe UI 9 —
    `ApplicationConfiguration.Initialize`), а не с голыми умолчаниями WinForms. С ними новый `CheckBox`
    меряет текст через **GDI+** (`UseCompatibleTextRendering = true`), и «текстовые» эталоны были GDI+-ными —
    таких чисел не видит ни одно приложение. Найдено пробами (`info/check/*`): под GDI+ `CheckBox` 77×22 и
    `Label` +6 по вертикали, под GDI — 79×19 и ровно измеренный текст. Отсюда исправлено:
    - `Label.GetPreferredSize` — буфер 6px только при `UseCompatibleTextRendering` (решение 42 был подогнан
      под GDI+); AutoSize-метка «label1» теперь 38×15 — **ровно как пишет VS** (закрывает открытый вопрос
      Ф5.2 «метрики текста в записанных размерах»: причина была не в дизайнере);
    - `CheckBox`/`RadioButton` — измеренный текст + 19/+18 по ширине и +4 по высоте, пустые — 15×14/14×13
      («checkBox1» → 83×19, как у VS);
    - захват `ToolStrip` — 5px (с визуальными стилями; 3 — без них), первая кнопка с x = 9.
    **Итог: 272/272 со всеми тремя оракулами** (сценарии, атрибуты, сериализация) на Windows и 272/272 на
    Linux. Витрина перезаписана (сдвинулись строки флажков).

### Ф6.К — корпус реальных проектов: перевод по кнопке (2026-09-23, Linux)

Сессия шла на нативном Linux (Ubuntu, .NET SDK 10.0.104, X11) — не WSL. Набор .NET: **280/280** (решение 106
дополнено: герметичный режим подменяет отсутствующее семейство шрифтов на DejaVu из `tests/NetForms.Tests/Fonts`,
а не на то, что установлено в системе — на этой машине «Segoe UI» уходил в Noto Sans с другой высотой строки, и три
теста ToolStrip ловили 23 вместо 22; `HermeticRendering.FallbackFontFiles`). `TemplateTests` зависал на Linux: сервер
сборки MSBuild/VBCSCompiler, оставшийся после `dotnet build`, наследовал перенаправленный stdout, и чтение не
кончалось — запуск `dotnet` из тестов теперь один (`tests/NetForms.Tests/Dotnet.cs`), с выключенными серверами.

111. **Конвертер переводит проекты .NET Framework (старый формат `.csproj`) сам** — без upgrade-assistant
    (`tools/NetForms.Convert/LegacyProject.cs`). Новый проект — SDK-стиль с глобами: метаданные перечисленных
    файлов (`DependentUpon`, `Generator`, `LastGenOutput`…) — через `Update`, а то, чего старый проект не
    компилировал (файл лежит на диске, но исключён из проекта), — `Remove`: набор компилируемых файлов тот же,
    а форма, добавленная потом шаблоном или дизайнером, подхватывается без правки проекта. Переносятся
    `OutputType`, `RootNamespace`, `AssemblyName`, `ApplicationIcon`, `StartupObject`, `LangVersion`,
    `AllowUnsafeBlocks` (из любой конфигурации), подпись `.snk`; при `Properties\AssemblyInfo.cs` —
    `GenerateAssemblyInfo=false` (и `Deterministic=false`, если версия с `*` — не в комментарии); ссылки на
    сборки фреймворка опускаются (в .NET они неявны), `HintPath`-ссылки остаются, `packages.config` →
    `PackageReference`, `ProjectReference` сохраняются, `COMReference` — ошибка отчёта. Подпапка с другим
    проектом исключается целиком (`Remove="Sub\**"`). Регистр путей, который прощает только Windows,
    исправляется на реальный. Файлы, которые программа читала из закоммиченного `bin\Debug` (Sapper:
    `bin\Debug\Picture\*.png` рядом с `Picture\*.png`), получают `CopyToOutputDirectory` — в SDK-выводе
    (`bin/Debug/net10.0`) их иначе нет; файл, существующий только в старом выводе, — предупреждение.
    Библиотеки без WinForms из того же решения тоже переводятся (net10.0, без NetForms): SDK-проект не
    может ссылаться на проект .NET Framework. CLI принимает проект, `.sln`/`.slnx` или папку.
    Анализ компилирует и проекты по `ProjectReference` (иначе каждое использование библиотеки — ложная ошибка).
    NetForms приносит `System.Configuration.ConfigurationManager` (он в общем фреймворке WindowsDesktop — без
    него не компилируется `Properties\Settings.Designer.cs` каждого шаблона VS), а build-логика Avalonia
    больше не течёт в приложение (`PrivateAssets="build;buildTransitive;…"`: её задача
    `GenerateAvaloniaResources` падала на `ApplicationIcon` пользователя).
112. **Пути Windows в файловых API NetForms.** `Image.FromFile`, `new Bitmap(path)`, `new Icon(path)`,
    `Image.Save`, `PrivateFontCollection.AddFontFile` на Linux читают путь, как Windows: если файла с таким
    именем нет, `\` — разделитель, имена сравниваются без учёта регистра (`WindowsPath`, NetForms.Drawing).
    Осознанная разница с «чистым» .NET, ради кода вида `Directory.GetCurrentDirectory() + @"\Picture\x.png"`
    (Sapper). `File.*` и прочий BCL так не умеют — конвертер находит строки-пути с `\` и абсолютные `C:\…`:
    в аргументе API NetForms — справка, иначе — предупреждение с местом. `Image.FromFile` несуществующего
    файла — `FileNotFoundException`, как в GDI+.
113. **Исправления исходников конвертером** (`SourceFixes.cs`) — только то, что не может изменить смысл
    программы: `using` пространства имён, которого в .NET нет (`System.Windows.Controls`,
    `System.Runtime.Remoting.Messaging` — IDE вставила и забыли), удаляется: он ничего не импортирует;
    `ResXFileRef` в `.resx` с регистром, который прощает только Windows, исправляется; тип, вынесенный в .NET в
    NuGet (CS1069 называет сборку: `System.Data.SqlClient`, `System.IO.Ports`, …), — добавляется
    `PackageReference` (для Windows-only — предупреждение). Анализ показывает правки (`Fixes`), `--apply` их
    делает, оригинал — рядом, `.winforms.bak`. Несуществующий `ApplicationIcon` и прочие файлы проекта —
    выбрасываются с предупреждением (сборка на них падала бы и на Windows); PFX-подпись — снимается
    (.NET подписывает только `.snk`).
114. **Недостающий API — по корпусу и по эталону.** `tools/NetForms.ApiDiff` сравнивает публичную и protected
    поверхность настоящих `System.Windows.Forms` + `System.Drawing.Common` (референсные сборки пакета
    `Microsoft.WindowsDesktop.App.Ref`, `MetadataLoadContext`) с NetForms — **работает на Linux**, в отличие
    от оракулов Ф4/Ф5; `--show <Type>` печатает настоящий тип целиком (сигнатуры переносятся дословно).
    Исходная метрика: **нет 566 типов из 1254 и 2372 членов существующих типов**. Сделано: `VisualStyles`
    (перечисления и дерево `VisualStyleElement` — вендорены из dotnet/winforms, константы Win32 подставлены
    числами); `Control`: `BackgroundImage`/`BackgroundImageLayout` (отрисовка — порт `ControlPaint.DrawBackgroundImage`),
    `PreviewKeyDown` (первым, до меню и диалоговых клавиш; `IsInputKey` пропускает их), `HelpRequested` по F1 с
    всплытием, события drag-and-drop (объявлены; платформа drag пока не начинает и не доставляет —
    `DoDragDrop` возвращает `None`), `Region`, `Scale`/`ScaleControl`/`GetScaledBounds`, `DeviceDpi` = 96,
    `Invoke<T>`, `BeginInvoke(Action)`, `InvokeAsync`, `GetChildAtPoint`, `IsMnemonic`, события `…Changed`
    для `RightToLeft`/`CausesValidation`/`ContextMenuStrip`/`ImeMode`, `InitLayout` (из `Controls.Add`),
    `ShowFocusCues` — `protected internal virtual`, как в WinForms. **Win32-крючки** — `WndProc`, `DefWndProc`,
    `CreateParams`, `CreateHandle`, `FromHandle` — есть, чтобы переопределения компилировались, но платформа их
    не вызывает: окна у контрола NetForms нет (конвертер по-прежнему предупреждает). `Form`: `TopLevel`
    (`false` — форма как обычный дочерний контрол, в панели: показывается и закрывается внутри родителя; рамки
    у встроенной формы нет — у WinForms есть), `Closing`/`Closed` (перед `FormClosing`/`FormClosed`, те же
    аргументы), `AutoScaleBaseSize`/`AutoScale`/`GetAutoScaleSize`, `OwnedForms`, `SizeGripStyle`, `HelpButton`,
    `TransparencyKey`, `RestoreBounds`, события размеров. `ButtonBase`: `ImageList`/`ImageIndex`/`ImageKey`,
    `TextImageRelation`, `FlatAppearance` (цвета и толщина рамки Flat), и **кнопка наконец рисует `Image`**.
    `HandleDestroyed` у формы поднимается после `FormClosed`.
115. **Исключения цикла сообщений — `Application.ThreadException`.** Каждый вызов из платформы в код
    приложения (ввод, отрисовка, размеры, активация, закрытие, тики `Timer`) идёт через
    `Application.Dispatch`: исключение уходит в `ThreadException`, если на него подписаны; без подписчика в
    настоящем приложении — диалог «продолжить/выйти» (как `ThreadExceptionDialog`, пока на `MessageBox`);
    `SetUnhandledExceptionMode(ThrowException)` и тестовая платформа/дизайнер без подписчика — проброс, как
    раньше. Ещё: `ApplicationContext` и `Run(ApplicationContext)`, `IMessageFilter` (фильтры видят
    `WM_KEYDOWN`/`WM_KEYUP` до всего остального), `CurrentCulture` — `CultureInfo` (было `string`),
    `UseVisualStyles`, `SetColorMode`/`ColorMode`.

116. **Мелкий API, на который натыкался корпус.** `Cursor`: `Current` (показывается на активной форме до
    следующего движения мыши, как WM_SETCURSOR), `Position` (чтение; запись — ничего: платформа не умеет
    переносить указатель), конструкторы из `Stream`/файла/ресурса (картинка хранится, показывается стрелка —
    платформа знает только именованные курсоры). `Clipboard`: картинки, `FileDropList`, звук, произвольные
    данные — **внутри процесса** (приложение читает, что положило; ОС видит только текст, пока его никто не
    сменил). `Help`/`HelpNavigator` (файл или URL открывается системной программой), перегрузки `MessageBox`
    со справкой, `SystemIcons.GetStockIcon`/`StockIconId`, `ContainerControl.ParentForm`, `ImageList(IContainer)`,
    `AutoCompleteStringCollection` + `AutoCompleteCustomSource` у `TextBox`/`ComboBox` (подсказки пока не
    показываются). Drawing: `LockBits`/`UnlockBits`/`BitmapData` (буфер в раскладке GDI+: BGR(A), строки
    кратны 4, ARGB не умноженный — у нас внутри premultiplied), `ColorMatrix`/`ImageAttributes` в `DrawImage`
    (матрица — `SKColorFilter`, ключ цвета/гамма/порог/таблица перекраски — попиксельно), `RotateFlip`,
    `DrawIcon`, `Bitmap(w, h, Graphics)`/`(Type, resource)`/`(…, scan0)`, полный `PixelFormat` и
    `RotateFlipType`, `ToolboxBitmapAttribute`; `GraphicsState` переехал в `System.Drawing.Drawing2D`, как в
    оригинале. Фасад `System.Drawing.Common` теперь генерируется: `NetForms.ApiDiff --forwards`.
117. **`MaskedTextBox`, `MonthCalendar`, `DateTimePicker`.** Маски — через `MaskedTextProvider` BCL (та же
    логика, что у WinForms: что принимается, что отвергается, куда встаёт каретка); в `TextBoxBase` появился
    крючок `EditCore` — каждая правка пользователя (ввод, Backspace/Delete, вставка) идёт через маску,
    `MaskInputRejected` с причиной, `TextMaskFormat`, `HidePromptOnLeave`, `ValidatingType` +
    `TypeValidationCompleted`, Insert переключает перезапись. `/` и `:` в маске — разделители культуры (как у
    WinForms). `MonthCalendar`: один месяц (`CalendarDimensions` хранится, раскладка 1×1), выбор диапазона до
    `MaxSelectionCount`, клавиатура, жирные даты, «Сегодня», размер — как у WinForms (≈227 px при Segoe UI 9).
    `DateTimePicker`: шаблон формата разбирается на поля (d/M/y/H/h/m/s/t), Left/Right выбирают поле,
    Up/Down меняют (день — по кругу внутри месяца, как Win32), цифры набираются; месяц рядом с днём — в
    родительном падеже («23 сентября»); выпадающий `MonthCalendar` (F4, Alt+↓, кнопка; Enter/клик —
    выбрать, время дня сохраняется), `ShowUpDown`, `ShowCheckBox`/`Checked`.
118. **Системные метрики и owner-draw.** `SystemInformation` (метрики, которыми рисует сам NetForms, для
    остального — значения Windows 10/11 по умолчанию, экраны — от платформы). `ControlPaint`: `DrawButton`,
    `DrawCheckBox`, `DrawRadioButton`, `DrawScrollButton`, `DrawComboButton`, `DrawBorder3D`, `DrawCaptionButton`,
    `DrawMenuGlyph`, `DrawSizeGrip`, `DrawGrid`, `DrawStringDisabled`, `DrawImageDisabled`, `DrawSelectionFrame`,
    `DrawGrabHandle`… — нашей темой; XOR-рисование на экране (`DrawReversibleFrame`) — ничего не рисует, экранного
    DC нет. **Строки «от ОС»** (кнопки `MessageBox`, «Сегодня:» календаря) следуют языку интерфейса:
    английский и русский (`SystemStrings`); тесты закрепляют en-US UI-культуру.

**Корпус на конец сессии:** проекты заказчика — **7 из 7** (6 репозиториев, все .NET Framework 4.8/4.8.1)
переводятся и собираются без ручной правки; открытые — **22 из 45** WinForms-проектов (в начале — 12 из 44).
Остальные 23 — в `knownFailures` с причиной: 8 — BinaryFormatter (удалён в .NET 9) и 4 — Windows-only
(CefSharp, WebView2, пакет с `FrameworkReference` на WindowsDesktop) — осознанно не переносимое; прочие —
недостающий API NetForms (RichTextBox, TaskDialog, ErrorProvider, NotifyIcon, печать, ImageListStreamer,
`Microsoft.VisualBasic.Devices`, пробелы `Binding`/`Icon`), один пакет, чья build-задача падает на .NET 10,
и один `OverrideCursor` (WPF).

Итог по API (`NetForms.ApiDiff`): **нет 495 типов из 1254** (было 566) и **1860** недостающих членов
существующих типов (было 2372). Набор .NET — **326/326** на Linux.

### Ф6.К, продолжение — провалы корпуса по списку (2026-09-23, Linux)

Исходники dotnet/winforms снова читались как эталон (локальная копия; сеть в этой сессии была нестабильной).

119. **`NotifyIcon` — через платформу, `IPlatform.CreateTrayIcon`** (метод интерфейса с реализацией по умолчанию
    `null`: тестовая платформа и headless-платформа дизайнера трея не имеют и ничего не реализуют). Avalonia —
    `TrayIcon`: на Windows это `Shell_NotifyIcon`, на Linux — StatusNotifierItem по D-Bus. Иконка уходит PNG-байтами,
    `Visible` без `Icon` в трей ничего не добавляет (как `_added` в WinForms). Ограничения оболочек — осознанные
    разницы: они сообщают только щелчок основной кнопкой, поэтому **двойной щелчок синтезируется** (второй щелчок в
    пределах `DoubleClickTime`: `DoubleClick`, `MouseDoubleClick`, `MouseDown(2)`, `MouseUp`, без второго `Click` — порядок
    `WM_LBUTTONDBLCLK`); правый щелчок известен, только когда открывает меню, — тогда `MouseDown(Right)`, `Opening`/`Opened`
    меню, `MouseUp`, `Click`, `MouseClick(Right)`; `MouseMove` не приходит никогда. **Меню рисует оболочка** (`NativeMenu`),
    оно строится из пунктов `ContextMenuStrip` (текст без `&`, `Enabled`, `Checked`, подменю, разделители; клик —
    `PerformClick`) и перестраивается при каждом открытии, так что обработчик `Opening`, дописывающий пункты, работает.
    **Всплывающая подсказка** — своё неактивируемое окно (`PopupForm`) в правом нижнем углу рабочей области: одинаково
    на обеих ОС; `timeout` игнорируется, как в Windows начиная с Vista (5 с); щелчок — `BalloonTipClicked`, истечение,
    замена новой подсказкой или скрытие значка — `BalloonTipClosed`.
120. **`TaskDialog` — своя форма в стиле `MessageBox`**, 17 публичных типов по эталонной поверхности. Семантика — из
    dotnet/winforms: результат — нажатая кнопка страницы (стандартные кнопки — новый экземпляр при каждом обращении,
    равны по виду, `==`); `AllowCloseDialog = false` оставляет диалог открытым; Escape/крестик/Alt+F4 работают только при
    `AllowCancel` или кнопке Cancel (она и нажимается; без неё результат — невидимая «заглушка» Cancel); страница без
    видимых кнопок получает OK; Help не закрывает, а поднимает `HelpRequest` (и F1); `Navigate` меняет страницу в том же
    окне (`Destroyed` старой, `Created` новой), а обработчик `Click`, навигировавший, диалог не закрывает. Проверки страницы
    перед показом — эталонные (одна отмеченная радиокнопка, нельзя смешать свои кнопки и command links, пустой текст,
    `DefaultButton` из коллекции). **Показанная страница обновляется вживую** (текст, заголовок, `Caption`, значки,
    прогресс и его состояние, `Enabled` кнопок, флажки) — окно перекладывается и меняет размер, а структуру менять
    нельзя (`InvalidOperationException` с текстом эталона). Вид: заголовок синим 12pt, значок 32×32 или цветная полоса
    для `Shield*Bar`, ссылки `<a href>` при `EnableLinks` (иначе разметка — текст), серая полоса с раскрывашкой, флажком
    и кнопками (свои — первыми, стандартные — в порядке comctl32), сноска с разделителем; ряда кнопок нет, если он пуст.
    `ProgressBar` получил внутреннее состояние полосы (`PBM_SETSTATE`: красная Error, жёлтая Paused). Строки кнопок и
    «Подробнее/Скрыть подробности» — через `SystemStrings` (en/ru). `Handle` — `IntPtr.Zero`: окна ОС нет.
121. **`ErrorProvider` и «украшения» контрола.** В WinForms значки ошибок — отдельное дочернее окно поверх соседей.
    У нас окон у контролов нет, поэтому в ядро добавлен общий механизм **украшений** (`Control.AddAdornment`): внутренний
    контрол, который рисуется после детей родителя и первым получает хит-тест, но не входит в `Controls`, раскладку и
    таб-порядок (им же когда-нибудь можно закрыть «внутренние контролы видны в `Controls`» из решения 26). Значок ловит мышь
    только непрозрачными пикселями (у WinForms — регион окна по маске значка). Всё остальное — из эталона: размещение
    `GetIconBounds` (6 выравниваний, `IconPadding`, RTL), значок 16×16, мигание (фаза 10 — пять миганий, `BlinkRate = 0` ⇒
    `NeverBlink`, `AlwaysBlink`), появление после `HandleCreated` и исчезновение с `VisibleChanged` контрола или родителя,
    подсказка с текстом ошибки сразу при наведении, `HasErrors`, `Clear`. Одно осознанное отличие: WinForms мигает
    нечётными значками одного родителя в противофазе с чётными, у нас все в фазе. **Ошибки из данных** — через
    `IDataErrorInfo`: у нас нет `BindingContext`/`CurrencyManager` (решение 45), менеджер — сам `BindingSource`, а
    привязки — `DataBindings` контролов внутри `ContainerControl` на тот же источник. Для этого `Binding` теперь, как
    эталон, кладёт в `BindingCompleteEventArgs.ErrorText` текст `IDataErrorInfo[поле]` (состояние `DataError`) или
    сообщение исключения; `ErrorText`/`Exception` стали только для чтения, добавлены конструкторы эталона и
    `Binding.OnBindingComplete`. Дизайнер: `add` для `ErrorProvider` ставит `ContainerControl = корень`, как `Site`-сеттер
    у VS, файл пишется в формате VS (`BeginInit`/`EndInit`, `errorProvider1.ContainerControl = this;`).
122. **ApiDiff видит операторы.** Раньше `IsSpecialName` отбрасывал и операторы, а `implicit operator` решает, компилируется
    ли код (`Footnote = "…"`). Нашлись недостающие `==`/`!=` у `Cursor` и `Message` — добавлены (у `Message` ещё
    `IEquatable<Message>`, `GetLParam`, `ToString`). Метрика после сессии: **нет 491 типа из 1254** (было 495) и **1858**
    членов существующих типов — с учётом операторов, то есть честнее прежней.
123. **`ImageList.ImageStream` и фасад `System.Windows.Forms`.** Картинки `ImageList`, добавленные в дизайнере VS, лежат
    в .resx BinaryFormatter-записью `ImageListStreamer`; BinaryFormatter в .NET 9+ удалён. Проверено опытом:
    `System.Resources.Extensions` 10 (он уже в зависимостях NetForms) **сам восстанавливает `[Serializable]`-тип с
    конструктором `(SerializationInfo, StreamingContext)` из NRBF-записей без BinaryFormatter** — нужен только тип по
    имени `System.Windows.Forms.ImageListStreamer, System.Windows.Forms`. Отсюда три части:
    - **`ImageListStreamer`** — `[Serializable]`, поле `Data`: RLE WinForms («MSFt», пары «счётчик, байт») поверх потока
      `ImageList_Write` comctl32, который NetForms разбирает сам: `ILHEAD` (0x4C49, версия 0x101), полоса картинок BMP
      по 4 в ряд (1/4/8/16/24/32 бита), маска 1 бит; прозрачность для каждой картинки отдельно — альфа, если она есть,
      иначе маска (как comctl32). Запись — тот же формат (`ILP_DOWNLEVEL`: 32 бита + маска), читается обратно тем же
      кодом. `ImageList.ImageStream`: `null` у пустого списка, установка заменяет картинки, размер и глубину и сохраняет
      порядок для последующих `Images.SetKeyName`; пока картинки не менялись, геттер отдаёт тот же объект (писатель
      дизайнера видит «значение не изменилось» и оставляет строку `resources.GetObject(...)` дословно). Добавлены
      `ShouldSerialize`/`Reset` `ColorDepth`/`ImageSize`/`TransparentColor` как в эталоне (первые два — только у пустого
      списка, `TransparentColor` — если не `LightGray`).
    - **Фасад `src/NetForms.WindowsForms`** (сборка `System.Windows.Forms` 10.0.0.0) пересылает в NetForms все общие с
      настоящим WinForms типы — 499, генерируются `NetForms.ApiDiff --forwards-winforms`, тест ловит забытый. В отличие
      от фасада `System.Drawing.Common` (решение 107), он ссылается на NetForms, поэтому ссылаться на него из NetForms
      нельзя (цикл): **`NetForms.csproj` собирает фасад сам сразу после компиляции** (цель `BuildWinFormsFacade`: restore,
      затем build против свежего `NetForms.dll` и ссылок проекта) и отдаёт его зависимым копируемым файлом. Итог — в
      выводе любого приложения, теста, хоста дизайнера и проекта корпуса лежит `System.Windows.Forms.dll`; тип из .resx
      и из сторонней библиотеки находится во время выполнения. Ссылкой при компиляции он не становится (ошибка `CS0012`
      у кода, который использует WinForms-тип из чужой библиотеки, остаётся — решается пакетом, где фасад лежит в `lib/`).
      Фасада нет в решении, он строится только через NetForms.
    - **Дизайнер** читает BinaryFormatter-записи .resx тем же путём, что приложение: запись заворачивается в
      предсериализованный ресурс и читается `DeserializingResourceReader` — дизайнер видит ровно то, что покажет
      рантайм, для любого `[Serializable]`-типа. Писатель, как `ImageListCodeDomSerializer`/`ImageListDesigner`, пишет
      у `ImageList` с картинками `ColorDepth` (эмпирика VS) и после свойств — `Images.SetKeyName(i, "…")`. Конвертер
      больше не называет проблемой BinaryFormatter-записи `[Serializable]`-типов NetForms (остальные — по-прежнему).
    **Ворота:** настоящий `ImageStream` из VS (Surviving-WinForms, MIT — `THIRD-PARTY-NOTICES.md`) — фикстура теста и
    `imageList1` в `samples/Gallery/ResourcesForm` (все ворота дизайнера: читатель, писатель байт-в-байт, компиляция
    записанного файла с ресурсами, как их встраивает MSBuild, хост); тест пути рантайма через `DeserializingResourceReader`;
    путь «прозрачность по маске» — поток NetForms с обнулённой альфой.

**Среда:** системные часы этой машины однажды ушли назад (файлы получили метки «из будущего»), и инкрементальная сборка
MSBuild молча оставила устаревшие сборки — тест видел старый код. Если результат не сходится с исходником:
`dotnet build NetForms.slnx --no-incremental`.

**Корпус и метрики на конец сессии** — см. ниже, «Состояние тестов».

### Ф6.К, продолжение — RichTextBox (2026-09-23/24, Windows)

Сессия шла на Windows 11 (.NET SDK 10.0.401, русская локаль). Это позволило впервые с Ф5 гонять оракулы против
настоящего WinForms локально — и проверить ими новый контрол до того, как он «застынет».

124. **`RichTextBox` — на движке `TextBoxBase`, со своей моделью форматирования.** `TextBoxBase` получил внутренние
    крючки, через которые наследник меняет раскладку, не переписывая редактор: перевод строки (`LineBreak`: "\r\n" у
    поля ввода, "\n" у RichEdit — индексы, `TextLength`, `Lines` как в WinForms) и принудительный конец строки
    (`NextLineBreak`), ширина диапазона от начала строки (`MeasureRange`), высота строки (`LineHeightOf`; вершины
    строк кэшируются, прокрутка по-прежнему построчная), доступная ширина и отступ строки (`LineWrapWidth`,
    `LineOffsetX` — теперь учитывается и в попадании мышью, и в позиции каретки), отрисовка строки (`PaintLine`),
    уведомления о правке (`OnTextSplicing` до замены, `OnTextReplaced`), о выделении и прокрутке, состояние для
    отмены (`CaptureUndoExtra`), имя действия отмены, группировка набора (RichEdit: один шаг на серию символов),
    предел отмены (100, EM_SETUNDOLIMIT), копирование/вставка (`CopyCore`/`PasteCore`), нажатие мыши (`MouseDownCore`).
    Перенос строк ищет подходящую длину бисекцией (было — посимвольно, O(n²) измерений на абзац). Поведение
    `TextBox`/`MaskedTextBox` не изменилось (весь прежний набор зелёный).
    Модель (`RichText.cs`): формат символа на каждый символ (`RichCharFormat`: шрифт, размер в твипах, стиль, цвет,
    фон, смещение, защита), формат абзаца на каждый абзац (`RichParaFormat`: выравнивание, отступы в твипах,
    маркер, табуляции). Строки — высотой по самому высокому фрагменту, фрагменты на общей базовой линии; табуляции —
    к стопам абзаца, дальше каждые полдюйма; маркер — в висячем отступе; ссылки (`DetectUrls`) — синие,
    подчёркнутые, курсор-рука, `LinkClicked` по нажатию (как EN_LINK на WM_LBUTTONDOWN); поле выделения
    (`ShowSelectionMargin`), `ZoomFactor` (тысячные с округлением вверх, как EM_SETZOOM), `RightMargin`, полосы
    прокрутки RichEdit — только когда текст не помещается (`Forced*` — всегда, неактивные), горизонтальная — только
    без переноса. Защищённый текст отказывает в правке (набор, удаление, `SelectedText`, форматирование) и поднимает
    `Protected`; `Text` заменяет его без вопросов. Ctrl+L/E/R/J — выравнивание (выключается
    `RichTextShortcutsEnabled`). `Find` (строка и набор символов, все флаги и проверки аргументов), `LoadFile`/
    `SaveFile` во всех форматах (UTF-8 с BOM распознаётся, как у RichEdit), копирование кладёт в буфер и текст, и RTF
    (внутри процесса, решение 116), `CanPaste`/`Paste(DataFormats.Format)`; добавлены `DataFormats.Format`/
    `GetFormat` и недостающие имена форматов.
    **RTF** (`RtfReader`/`RtfWriter`): чтение — таблицы шрифтов (с кодировкой шрифта для \'hh: кириллица из
    WordPad/Word читается) и цветов, символьные и абзацные свойства, \uN с \ucN, спецсимволы, маркеры \pn и списки
    Word, поля — по результату; картинки, объекты, стили, колонтитулы пропускаются. \line — мягкий перенос RichEdit
    (U+000B: новая визуальная строка, не новая строка `Lines`). Последний \par документа — финальная метка абзаца, не
    перевод строки; вставка через `SelectedRtf` его сохраняет (как у RichEdit).
    **Всё это сверено с настоящим RichEdit** — сценарий `RichText` в `tests/Shared/CompatScenarios.cs` (50 ключей;
    RTF — без генератора, `\ansicpg`, `\lang*` и `\fcharset`, которые зависят от сборки RichEdit и языка системы).
    Оракул опроверг несколько догадок, код исправлен по нему:
    - `Text = …` (и простой текст из файла, и `Rtf = ""`) сбрасывает **всё** форматирование, и абзацное, к формату
      по умолчанию; даже тот же текст с форматированием (RichTextBox переопределяет `Text`).
    - Формат по умолчанию — шрифт, как его получает RichEdit через WM_SETFONT: LOGFONT в целых пикселях, поэтому
      Arial 10 читается как **9.75** (Arial 11 — 11.25). Шрифт, заданный при наличии текста, применяется точно (SCF_ALL).
    - Абзацы, слитые удалением перевода строки, берут формат **первого** (а не «метки абзаца»); перевод строки,
      вставленный в абзац, делит его на два одинаковых; вставленный RTF отдаёт исходный формат последнему абзацу.
    - Цвета возвращаются через COLORREF и `ColorTranslator.FromOle`: `SelectionColor == Color.Red` — **true**.
    - Смешанное выделение: разные шрифты — `SelectionFont == null`, разные размеры — 13 пт, стиль — только общий;
      цвет — `Color.Empty`, выравнивание — Left, маркер — false; пустое выделение — `SelectedRtf == ""`.
    - RTF-писатель повторяет RichEdit байт-в-байт (кроме не-ASCII, см. ниже): `\pard` только где меняется формат
      абзаца (после пустой строки), `\pntext` на каждом маркированном абзаце, порядок `\fi \li \ri \q* \tx` и
      `\cf \highlight \ul \b \i \strike \protect \up \f \fs`, `\fs` при любом изменении размера (9.75→10 даёт
      повторный `\fs20`), пробел-разделитель только перед текстом, `\~`, `\line`, `\tab`. RTF без `\fs` — 12 пт (так
      по спецификации и у RichEdit), `\plain` — тоже.
    Осознанные разницы: символы вне ASCII пишутся `\uN?` (RichEdit — `\'hh` в кодовой странице шрифта; читаются
    одинаково, кодировку шрифта модель не хранит); у `Justify` нет растяжки (выравнивание влево, как и читает
    `SelectionAlignment`); OLE-объекты, картинки, нумерованные списки, `AutoWordSelection`, `EnableAutoDragDrop`,
    IME — объявлены, поведения нет. Имена действий отмены и тексты исключений — из ресурсов WinForms, en/ru через
    `SystemStrings` (русский языковой пакет Windows Desktop переводит их — оракул на этой машине вернул «Неизвестно»).
    **Дизайнер:** `RichTextBox` в панели элементов (он там уже значился по имени), писатель, как `RichTextBoxDesigner`,
    пишет `Text = ""` всегда; пример — `richTextBox1` в `samples/Gallery/CollectionsForm` (все ворота дизайнера и
    оракул сериализации: совпадает). Атрибуты (категории, описания SR, `DefaultValue`, видимость) совпали с
    эталоном — `AttributeDiffTests` не нашёл по RichTextBox ни одного расхождения. Фасад `System.Windows.Forms`:
    +12 типов. **Ворота:** `RichTextBoxTests` (23 теста, golden `richtextbox-formatted`), `CompatDiffTests`
    с оракулом, `DesignerCodeWriterTests` (файл байт-в-байт).
125. **Корпус: `SuperAdventure` собирается.** Кроме RichTextBox ему не хватало `DataGridView.ScrollBars` и
    `ColumnHeadersHeightSizeMode` (+ `DataGridViewColumnHeadersHeightSizeMode`, `DataGridViewAutoSizeModeEventArgs`,
    `AutoResizeColumnHeadersHeight`, `ColumnHeadersHeightChanged`) — добавлены по эталону: в режиме AutoSize высота
    заголовков — по самому высокому тексту (+8, сверено оракулом: 23 при Segoe UI 9, 33 при Arial 16), заданная
    вручную высота запоминается и возвращается при выходе из AutoSize, `ColumnHeadersHeight` в этом режиме не
    сериализуется. `CorpusTests` сначала переводит все проекты решения, потом собирает: на Windows порядок обхода
    другой, и WinForms-проект собирался раньше, чем переведена его библиотека (MSB3644 про .NET Framework 4.5).

126. **Долг оракулов закрыт: все ворота с настоящим WinForms зелёные.** API решений 114–118 добавлялся на Linux, где
    оракулов нет, и `AttributeDiffTests` на Windows нашёл **202** расхождения атрибутов. Почти все — члены, которые
    WinForms переобъявляет на конкретных контролах, чтобы спрятать от дизайнера (`BackgroundImage*` и их события у
    списков, полей ввода, полос прокрутки, календарей…, `ImeModeChanged`, `CausesValidationChanged`, `Click`/`Paint`
    у `DateTimePicker`/`MonthCalendar`, `AcceptsTab`/`Multiline`/`WordWrap` у `MaskedTextBox`…). Они добавлены на
    базовые типы (наследники получают их с атрибутами) генератором по списку расхождений: виртуальные свойства —
    `override`, прочие и события — `new` с переадресацией на базу; атрибуты скрытого члена TypeDescriptor сливает
    с базовыми, поэтому достаточно `[Browsable(false)]`/`[DesignerSerializationVisibility(Hidden)]`. Вручную —
    категории (`ParentForm`, `MonthCalendar.SingleMonthSize`/`TodayDateSet`), `ErrorProvider.HasErrors` (виден),
    `MaskedTextBox.Text` (`DefaultValue("")`), `MonthCalendar.Size` (`Localizable(false)`). `SerializationDiffTests`
    нашёл `DataGridViewColumn.HeaderTextAlignment` — выдуманное свойство, которого в WinForms нет (писатель вписывал
    его в designer-файлы): удалено. Итог на Windows: **379/379 со всеми тремя оракулами** (сценарии, атрибуты,
    сериализация).

### Открытые вопросы Ф6

- **Запись `.resx` дизайнером**: новая картинка из панели свойств, правка локализуемой формы, смена
  культуры. Нужен писатель `.resx` в формате VS (заголовок, порядок записей, `assembly`-алиасы).
- ~~**BinaryFormatter-ресурсы**~~ — `ImageListStreamer` закрыт решением 123; прочие `[Serializable]`-типы читаются тем
  же путём `System.Resources.Extensions`, если тип находится по имени.
- **Недостающие типы `System.Drawing.Common`** — 132 (см. решение 107); порядок — по частоте в корпусе Ф5.7.
- **Фасад `System.Windows.Forms`** — сделан для времени выполнения (решение 123). Осталось: при компиляции он не
  виден, поэтому код, использующий WinForms-тип из сторонней библиотеки, получает `CS0012`; решено NuGet-пакетом
  NetForms, где фасад лежит в `lib/` (решение 128; для `ProjectReference` на копию NetForms — по-прежнему `CS0012`). Пакеты с `FrameworkReference` на WindowsDesktop (NETSDK1136) так не спасти.
- **Свойства-расширители в панели свойств дизайнера** («Error on errorProvider1», «ToolTip on toolTip1»): писатель их
  пишет, читатель читает, но хост не показывает их в панели свойств — нужен `IExtenderProviderService` у `DesignSite`.
- **Оставшиеся провалы корпуса** — `tests/corpus/corpus.json`, `knownFailures` с причинами; порядок — решение 146
  (`compatibility.md` § 8): печать, `ImageList.Images.Add(string, Icon)`, `LinkLabel.OverrideCursor`;
  ~~`Binding` (tetris-oop)~~ (решение 141); автодополнение в `TextBox`/`ComboBox`. (`ErrorProvider`, `NotifyIcon`, `TaskDialog`, `ImageListStreamer`,
  `RichTextBox` — решения 119–125.)
- **Оракулы после работы на Linux**: новый API, сделанный без Windows, проверять оракулами при первой возможности —
  долг решений 114–118 (решение 126) набрался именно так. Оракулы запускаются локально (см. «Дифф-прогон» ниже).
- **RichTextBox, чего нет**: не-ASCII в RTF как \'hh (нужна кодировка шрифта в `RichCharFormat`), растяжка `Justify`,
  нумерованные списки, картинки и OLE-объекты, `AutoWordSelection`, перетаскивание, IME.
- **Автоподстановка в `AutoCompleteCustomSource`**, drag-and-drop платформы, `Cursor.Position` на запись,
  собственные курсоры из файлов, `WndProc` для синтезированных сообщений — объявлены, поведения нет.
- **Запуск проектов заказчика на Linux** проверен сборкой и тестами конвертера; ручной прогон окон
  (Sapper и др.) — за заказчиком.
- **`DataGridView` после решения 142**: WinForms делает первую ячейку текущей при создании окна, при добавлении
  строк в сетку без текущей ячейки и при входе фокусом (`MakeFirstDisplayedCellCurrentCell`) — у нас нет (скажется на
  golden-картинках и выделении); поток новой строки (`NewRowNeeded`, `UserAddedRow`, `DefaultValuesNeeded`, новая
  строка в привязанной сетке), значок ошибки строки/ячейки (`ErrorText` хранится, не рисуется), карандаш
  `ShowEditingIcon`, окно сообщения для необработанного `DataError`, копирование в буфер.
- **Значения перечислений против WinForms**: `DataGridViewDataErrorContexts` расходился по значениям — ApiDiff
  сравнивает члены, но не значения констант; стоит добавить сверку значений всех enum.
- **Выборка открытого кода для `ApiDiff --usage`** (решение 146): корпус почти собирается и остальные ~430
  недостающих типов не упорядочивает. Нужен список популярных открытых WinForms-репозиториев с закреплёнными коммитами
  (код только компилируется для подсчёта и в репозиторий не попадает), клоны — в ту же папку.



### Ф7 — подготовка к публикации (2026-09-24, Linux)

Сессия на Linux (Ubuntu 24.04, .NET SDK 10.0.112 из `noble-updates`; `dot.net`/`builds.dotnet.microsoft.com` в этой
среде закрыты прокси, apt — открыт).

127. **Лицензия и метаданные пакетов.** `LICENSE` — MIT, правообладатель «NetForms contributors» (как `Authors`;
    §9 назвал MIT, правообладателя заказчик может поменять в `LICENSE` и `Copyright`). В `Directory.Build.props`:
    `PackageLicenseExpression`, иконка (`eng/branding/icon.png`, из `logo.svg` скриптом `render-icons.js`), README
    пакета (`eng/package/README.md` — отдельный от корневого: nuget.org не показывает относительные ссылки и картинки),
    Source Link и `.snupkg`, `ContinuousIntegrationBuild` в Actions. **`IsPackable=false` по умолчанию**, включён
    явно у пяти пакетов времени выполнения, шаблонов и конвертера. Имя `NetForms` на nuget.org 2026-09-24 свободно.
128. **Пакеты: по одному на сборку, приложение ссылается на `NetForms`.** `NetForms` → `NetForms.Drawing`,
    `NetForms.Drawing.Common` (`PackageId` задан явно: сборка называется `System.Drawing.Common`, а этот id —
    Microsoft), `NetForms.Platform`, `NetForms.Platform.Avalonia`. Фасад `System.Windows.Forms.dll` лежит в
    `lib/net10.0` пакета `NetForms` рядом с `NetForms.dll` — ссылка компиляции, как и планировалось в «Открытых
    вопросах Ф6» (`CS0012` для библиотек, собранных против WinForms, при ссылке на пакет не возникает).
    Исключения build-логики Avalonia (`PrivateAssets`) переходят в `exclude="Build,Analyzers,BuildTransitive"`
    зависимостей. **Одна версия** — `0.1.0-preview.1` в `Directory.Build.props`; конвертер берёт её из своей
    сборки (`AssemblyInformationalVersion` без `+commit`), шаблон `netforms-app` называет её в тексте, равенство
    держит `TemplateTests.TheAppTemplateReferencesTheReleasedPackageVersion`.
    **`NetForms.Convert` — .NET tool** (`netforms-convert`): 600 МБ натива Skia/HarfBuzz на 20 платформ и символов
    Windows превышали лимит nuget.org; цель `PruneToolRuntimes` оставляет, как `prune-host.js`, только настольные
    платформы и без `.pdb` — 42 МБ.
    **Проверено сквозным путём** (локальный фид из `artifacts/pkg`, изолированные `NUGET_PACKAGES` и hive шаблонов):
    `dotnet new netforms` + `netforms-form` → сборка из пакетов, фасады в выводе; проект «как из VS»
    (`net10.0-windows` + `UseWindowsForms`) — `NETSDK1100`, с `EnableWindowsTargeting` — «Microsoft.WindowsDesktop.App …
    No frameworks were found»; после `netforms-convert --apply` (tool из пакета) — собирается и **открывает окно
    под Xvfb** (`docs/images/hello-linux.png`).
129. **Расширение — к Marketplace и Open VSX.** `package.json`: иконка, `preview`, `galleryBanner`, `repository`/
    `homepage`/`bugs` (флаг `--allow-missing-repository` убран), README страницы расширения со скриншотом UI-теста,
    CHANGELOG, `LICENSE` копируется из корня в `vscode:prepublish`. **Пакеты по платформам**:
    `npm run package:targets` → `vsce package --target` для win32/linux/darwin × x64/arm64, `prune-host.js` по
    `NETFORMS_VSCE_TARGET` оставляет натив одной платформы — 13 МБ вместо 41; универсальный пакет остаётся для
    ручной установки. `NETFORMS_TEST_HOST` направляет protocol-тест на хост из распакованного `.vsix` — хост из
    `linux-x64` прошёл. `npm test` — 20/20 (headless Chromium из Playwright через `NETFORMS_TEST_BROWSER`).
130. **Выпуск — по тегу** (`.github/workflows/release.yml`): тег `v<Version>` сверяется с `Directory.Build.props`,
    сборка, тесты, `dotnet pack`, `dotnet nuget push` (секрет `NUGET_API_KEY`), `vsce publish` каждого платформенного
    `.vsix` (`VSCE_PAT`; версия с дефисом → `--pre-release`), `ovsx publish` (`OVSX_PAT`, необязателен), GitHub
    Release с файлами и корневым `CHANGELOG.md`. Ручной запуск — сухой прогон. Регламент и разовые действия
    владельца (publisher, токены, Pages) — `docs/RELEASING.md`.
131. **Документация — «эталон Microsoft + наше».** Справочник API не пишется: NetForms — копия, и страницы
    learn.microsoft.com — его документация. Свои страницы (`docs/`, английский — для nuget.org/Marketplace):
    getting-started, migrating (настоящие тексты ошибок, отчёт конвертера по его реальным категориям),
    **compatibility** (какие TFM/ОС/проекты работают и какие нет, статус каждого контрола и подсистемы,
    Win32-API, осознанные разницы — утверждения сверены с кодом), designer. **Покрытие API генерируется**:
    `NetForms.ApiDiff --markdown docs/api` — по странице на пространство имён, у каждого типа статус и ссылка на
    страницу Microsoft, у частичных — список недостающих членов. Итог: **633 полных, 164 частичных, 457 нет из
    1254; 1841 недостающий член** (совпадает с прежней метрикой).
132. **Сайт** — `site/` (статический, английский + `ru/`), публикуется `.github/workflows/pages.yml`. Идея
    оформления — поверхность дизайнера WinForms: точечная сетка привязки, форма с маркерами выделения, у которой
    кнопка с `Anchor = Bottom, Right` едет за краем при «дыхании» ширины (единственная анимация, выключается
    `prefers-reduced-motion`), таблицы в стиле PropertyGrid. Цифры состояния — из `compatibility.md`.
    Сценарий ролика «Злой csproj» (`promo/angry-csproj.md`, вне документации и сайта) — ошибки и `git diff --stat` в нём сняты вживую.
133. **RTF сравнивается с оракулом с точностью до записи таблицы шрифтов.** На windows-latest `CompatDiffTests` падал на
    всех `exact/rtb/*` (и на первом коммите тоже): RichEdit раннера держит для одного шрифта две записи — с голым
    `\fcharset` и без — и переключается между ними (`\f1` на тот же Arial), а RichEdit машины, где снимались эталоны
    (решение 126), — одну. Документ один и тот же, байты разные; зависит от версии msftedit, не от NetForms. Для
    RTF-значений сравнение идёт после нормализации (`NormalizeRtfFonts`): шрифт — имя плюс ненулевой charset, таблица —
    множество таких шрифтов, `\fN`/`\pnfN` заменены именем, переключение на уже действующий шрифт (с учётом групп и
    `\deff`) выброшено. Другое начертание, другой charset, текст и прочие свойства по-прежнему считаются различием —
    это закреплено тестом `RtfCompareIgnoresOnlyHowTheFontTableIsWritten` на всех семи парах из лога CI.
    Первая версия исправления на CI не сработала: значения сценариев хранят RTF экранированным (`Esc` в
    `CompatScenarios`: `\\` вместо `\`, `\r`/`\n` вместо переводов строк), а проверка «это RTF» ждала `{\rtf`.
    Теперь сравнение раскодирует значение (`Unescape`), и тест идёт по закодированным строкам; сквозной прогон с
    эталонным JSON в том виде, как его пишет раннер, — 7 из 7 «равны с точностью до таблицы шрифтов».
134. **Шаблоны и расширение — только из NuGet.** Режим `--FrameworkPath` (ссылка на копию NetForms) убран из шаблона
    `netforms`: новый проект всегда ссылается на пакет `NetForms`. Шаблоны формы и пользовательского контрола получили
    параметр `--Namespace` (coalesce с `msbuild:RootNamespace`). Расширение больше не везёт копию шаблонов и не
    спрашивает «пакет или копия»: **Create New Project / New Form** ставят `NetForms.Templates::<netformsVersion>` из
    NuGet (`dotnet new install`, если установлена другая версия или никакой; установленная версия читается из
    `dotnet new uninstall` при `DOTNET_CLI_UI_LANGUAGE=en`) и зовут `dotnet new`; форма получает пространство имён
    проекта плюс папки (как VS) через `--Namespace`, а в проекте без restore — с `--force` (ограничение
    «C#-проект» иначе не выполнено; файлы проверены на отсутствие заранее). `netformsVersion` в
    `designer/package.json` равен `<Version>` (тест `designer/test/version.test.js`). Настройка
    `netforms.frameworkPath` удалена; у конвертера `--framework-path` остался для работы над самим NetForms и
    корпуса. Сквозной тест `TemplateTests` проходит путь пользователя: пять пакетов и шаблоны пакуются из копии
    исходников (чтобы не делить bin/obj с параллельными тестами) под своей версией, шаблоны ставятся из `.nupkg`,
    проект восстанавливает NetForms из этого фида и собирается, фасады в выводе; версии за собой удаляются из кэша.
135. **Выпуск — это смена версии в `main`.** `ci.yml`: задача `release-check` после зелёных `build-test` и
    `designer` на пуше в `main` проверяет, есть ли тег `v<Version>`; нет — вызывает `release.yml` как reusable
    workflow (`publish`, `tested` — тесты не гоняются второй раз). `release.yml` собирает, пакует, публикует в
    nuget.org (**Trusted Publishing** через `NuGet/login@v1` при переменной `NUGET_USER`, иначе секрет
    `NUGET_API_KEY`), в Marketplace (`VSCE_PAT`) и Open VSX (`OVSX_PAT`, по желанию) с `--skip-duplicate`, затем
    GitHub Release, который и создаёт тег. Без учётных данных NuGet выпуск не начинается и тег не создаётся —
    иначе версия считалась бы выпущенной. Ручной запуск — сухой прогон или настоящий выпуск; ручной тег тоже
    работает. Actions обновлены до версий на Node 24 (checkout v7, setup-dotnet v6, setup-node v7, cache v6,
    upload-artifact v7, download-artifact v8, configure-pages v6, upload-pages-artifact v5, deploy-pages v5);
    все workflow проверены `actionlint`.
136. **Сторонние пакеты контролов WinForms из NuGet не работают — проверено.** ZedGraph, OxyPlot.WindowsForms,
    ScottPlot.WinForms, FastColoredTextBox, ObjectListView (сборки .NET Framework, берутся через AssetTargetFallback
    с NU1701) компилируются против `System.Windows.Forms, Version=4.0.0.0, PublicKeyToken=b77a5c561934e089`; фасад
    NetForms не подписан, Roslyn не считает его той же сборкой — `CS0012`. DockPanelSuite (сборка под .NET Core) несёт
    `FrameworkReference Microsoft.WindowsDesktop.App.WindowsForms` — `NETSDK1136`. Во время выполнения .NET строгие
    имена не проверяет, так что дело только в компиляции. Возможный путь — public signing фасадов открытыми ключами
    Microsoft (ECMA-ключ `00000000000000000400000000000000` даёт токен `b77a5c561934e089`; так подписывают сборки
    dotnet/runtime и так делал Mono) плюс снятие WindowsDesktop-FrameworkReference у пакетов buildTransitive-целью —
    **решение заказчика**, не принято. Утверждение решения 128 «фасад в lib/ устраняет CS0012» было неверным и
    исправлено в коде и документации.
137. **Документация на сайте.** `site/build.mjs` (Node, `marked`) рендерит `docs/*.md` (англ.), `docs/ru/*.md` (рус.)
    и `docs/api/*.md` в оформление сайта: меню разделов, оглавление страницы, якоря заголовков (как у GitHub, с
    кириллицей), таблицы с прокруткой; ссылки между `.md` становятся ссылками между страницами, остальное в
    репозитории — на GitHub. Markdown остаётся источником и так же читается на GitHub. `pages.yml` собирает и
    публикует `_site`. Новые страницы: `install.md` (установка по ОС: Windows, Ubuntu, Debian, Fedora, Astra/РЕД
    ОС/ALT через скрипт; системные библиотеки по семействам; шаблоны и конвертер; VS Code; закрытые сети; поставка
    на Linux) и русские переводы всех страниц. Главная страница отправляет русскоязычный браузер на `ru/` при первом
    заходе; выбор EN/RU запоминается. Проверено: 0 битых ссылок и якорей на 32 страницах, нет горизонтальной
    прокрутки на 390 px. README разделён на `README.md` и `README.ru.md` со ссылками на сайт.
138. **Первый выпуск и публикация расширения без токена.** `0.1.0-preview.1` вышел 2026-09-24 через Trusted
    Publishing (все семь пакетов на nuget.org; проверено с чистого кэша: `dotnet new netforms` + элементы → сборка
    из пакетов, `netforms-convert --apply` на WinForms-проекте → сборка). Расширение — одно (`netforms.netforms-designer`),
    шесть платформенных сборок под этим id, VS Code берёт свою; в Marketplace и Open VSX уходят только они (раньше
    шаблон `netforms-designer-*.vsix` захватывал и универсальный пакет, собранный без `--pre-release`). Публикация
    вынесена в задачу `vscode-publish` (окружение `marketplace`): managed identity в Microsoft Entra ID через GitHub
    OIDC и `vsce publish --azure-credential` (секреты `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`; id для страницы
    *Members* издателя печатает `marketplace-identity.yml`), при отказе или без неё — `VSCE_PAT`. Причина: глобальные
    токены Azure DevOps, единственные, что принимает Marketplace, отключаются 2026-12-01, а `vsce publish --oidc`
    Marketplace пока не поддерживает. `github-release` ждёт `vscode-publish`: сбой публикации расширения не создаёт
    тег, и следующий пуш в `main` повторяет выпуск (опубликованное пропускается).

**Состояние тестов на конец сессии (Ф7, 2026-09-24, Linux):** .NET — **381/381** (в т.ч. `TemplateTests` через пакеты, решение 134, и нормализация RTF, решение 133);
оракулы WinForms — в CI на Windows (решение 133). Расширение — 21/21 (+ тест версии), плюс protocol-тест на хосте из
`linux-x64.vsix`.

### После выпуска — пробелы данных и дизайнер (2026-09-24, Linux)

139. **Вкладка «События»: обработчик одной кнопкой.** Заказчик: чтобы создать обработчик, приходилось вводить имя
    и нажимать Enter (двойной щелчок по строке события, как в VS, работал, но его не находили). У события без
    обработчика теперь кнопка **+** («Создать обработчик button1_Click»): та же операция, что двойной щелчок, —
    `setEvent` с именем по умолчанию, хост пишет заготовку в `MainForm.cs`, редактор открывает её. У привязанного —
    **→** (перейти) и **✕** (отвязать, `setEvent` с `""`; метод остаётся в коде, как в VS). Ввод своего имени и
    Enter работают по-прежнему. UI-тест проходит оба пути по файлам на диске.
140. **Стили `DataGridView` по умолчанию несут шрифт сетки.** Баг из проверки выпуска: в `Paint` своей ячейки
    `cellStyle.Font == null` — `DefaultCellStyle`, `ColumnHeadersDefaultCellStyle`, `RowHeadersDefaultCellStyle`
    создавались без шрифта, и рисование подставляло `DataGridView.Font` само. Теперь, как в WinForms
    (`DefaultDefaultCellStyle`), у всех трёх `Font = grid.Font`, и шрифт «окружающий»: следует за `Font` сетки (в т.ч.
    унаследованным от формы), пока стилю не дали свой (другой объект `Font` — сравнение по ссылке, как `!=` в WinForms).
    `ShouldSerialize*` сравнивают с умолчанием на текущем шрифте, так что форма со своим шрифтом не пишет стили.
    **Осознанная разница:** WinForms при неполном назначенном `DefaultCellStyle` отдаёт из геттера заполненную
    копию (правки в ней теряются); мы дозаполняем пустые члены самого назначенного объекта — результат геттера тот
    же, правки не теряются. `null` восстанавливает стиль по умолчанию, как там. `DataGridViewCell.Paint` стал
    `protected virtual` (был `protected internal`: сборка с `InternalsVisibleTo` не могла переопределить его как в
    WinForms); сетка зовёт его через внутренний `PaintCell`. Тесты: `ACustomCellPaintsWithTheGridsFont`,
    `TheDefaultStylesFollowTheGridsFontUntilTheyHaveTheirOwn`; оракул — `exact/dgv/style-*` (сценарий компилируется
    и против настоящего WinForms: `dotnet build tests/NetForms.Compat -p:EnableWindowsTargeting=true` на Linux).
141. **Привязка данных — реализация WinForms, взятая целиком.** Решение 45 («нет `BindingContext`/`CurrencyManager`,
    их место занимает `BindingSource`») снято: на нём не работали `DataSet` + `DataMember = "Таблица"`, мастер-деталь
    через `DataRelation`, `DisplayMember` над `DataTable` (отражение по типу `DataRowView` не видит столбцов) — всё, что
    в WinForms делает `CurrencyManager`. Из dotnet/winforms (коммит c3cf021, MIT, `THIRD-PARTY-NOTICES.md`) перенесены
    как есть `DataBinding/*` (`Binding`, `BindingContext`, `BindingManagerBase`, `CurrencyManager`, `PropertyManager`,
    `Related*Manager`, `BindingSource`, `ListBindingHelper`, коллекции привязок, `BindableComponent`, `Formatter`) и
    `ListControl`; атрибуты `SR*` раскрыты в `Category`/`Description`, строки исключений — `DataBinding/SR.cs`,
    внутренние помощники — `VendorHelpers.cs`. Наше — стыковка: `Control` реализует `IBindableComponent`
    (`BindingContext` от родителя, `BindingContextChanged`, перепривязка при смене родителя и при создании),
    `ContainerControl` создаёт контекст по требованию и в `OnCreateControl` привязывает всё внутри (как в WinForms),
    `SplitContainer` берёт родительский; окно формы вызывает `OnCreateControl` всего дерева (детей первыми), `Created`
    у контрола без формы — после `CreateControl()`. `ListBox`/`ComboBox` — под API `ListControl` (`SetItemCore`,
    `RefreshItem`, выбор ↔ `DataManager.Position`, `Items` при `DataSource` — `ArgumentException`), `SelectedValue`
    без `DataSource` — `null`, как там. `DataGridView` берёт список у `BindingContext[DataSource, DataMember]`:
    таблица `DataSet`, путь отношения, `BindingSource`; текущая строка и `Position` следуют друг за другом; свойства-
    списки (отношения) — не столбцы; связанный менеджер при движении родителя меняет список — сетка перечитывает его.
    `ErrorProvider` перенесён на `ContainerControl.BindingContext[DataSource, DataMember]` и `Bindings` менеджера.
    **Поведение стало как в WinForms, и три наших теста его не отражали:** привязка не работает, пока контрол не создан
    и без контекста; по умолчанию (`OnValidation`) значение уходит в источник при проверке, а не на каждое изменение;
    `BindingComplete` приходит только при `formattingEnabled: true` (так пишет дизайнер VS). Тесты переписаны.
    Проверено: `DataBindingTests` (DataSet, мастер-деталь сетками и `BindingSource`, путь отношения, `DisplayMember`/
    `ValueMember`, общий `CurrencyManager`, поздний контрол), круговой проход дизайнера с
    `DataBindings.Add(new Binding(...))`, оракул `exact/binding/*`; фасад `System.Windows.Forms` дополнен (15 типов).
    Корпус: tetris-oop собирается (28 из 45 открытых). ApiDiff: 652 полных, 160 частичных, 442 нет; 1787 членов.
142. **Правка в `DataGridView` — модель WinForms.** Прежняя правка была своей: `TextBox`/`ComboBox` поверх ячейки,
    значение уходило в ячейку сразу, событий не было. Теперь по `DataGridView.Methods.cs` dotnet/winforms:
    - ячейка правится контролом своего `EditType` (`IDataGridViewEditingControl`: `DataGridViewTextBoxEditingControl`,
      `DataGridViewComboBoxEditingControl` — перенесены, свой пользовательский — как столбец-календарь Microsoft) в
      `EditingPanel`; последний контрол переиспользуется. Ячейка-флажок правит себя сама (`IDataGridViewEditingCell`):
      щелчок меняет редактируемое значение, `Value` меняется при фиксации (уход с ячейки, `EndEdit`, `CommitEdit`) —
      как в WinForms, где для немедленного значения ловят `CurrentCellDirtyStateChanged` и зовут `CommitEdit`;
    - `BeginEdit` → `CellBeginEdit`, `InitializeEditingControl`, `EditingControlShowing`, `ApplyCellStyleToEditingControl`,
      `PrepareEditingControlForEdit`; правка делает ячейку «грязной» (`NotifyCurrentCellDirty`,
      `CurrentCellDirtyStateChanged`); фиксация — `CellValidating` (отмена оставляет ячейку и правку), `CellParsing`
      (или `ParseFormattedValue` через `Formatter` WinForms), значение, `CellValidated`; ошибка разбора — `DataError`
      с `Parsing|Commit` и `Cancel = true` (ячейка остаётся в правке); `CancelEdit` возвращает исходное и не выходит из
      правки, Escape — выходит; смена текущей ячейки мышью/клавиатурой валидирует (`CellLeave`, `RowLeave`, …,
      `RowValidating`, `RowEnter`, `CellEnter`), присваивание `CurrentCell` в коде — тоже (`ScrollIntoView` →
      `CommitEditForOperation`, подтверждено оракулом); уход фокуса из
      сетки фиксирует правку (`OnValidating`);
    - клавиатура: символ начинает правку (`KeyEntersEditMode`), F2 — с кареткой в конце, Enter фиксирует и спускается,
      Tab ходит по ячейкам (`StandardTab`), клавиши редактора, которые он не хочет (`EditingControlWantsInputKey`),
      сетка перехватывает в `ProcessKeyPreview`; для этого `RaiseKeyDown/Up/Press` идут через `ProcessKeyMessage`
      (сначала `ProcessKeyPreview` родителей), а однострочный `TextBox` берёт Up/Down как свои (DLGC_WANTARROWS
      edit-контрола — фокус стрелками из него не уходит);
    - форматирование — как `DataGridViewCell.GetFormattedValue` WinForms: `CellFormatting` получает «сырое» значение
      (раньше — уже отформатированную строку), затем `Formatter.FormatObject`; сигнатура `GetFormattedValue`,
      `Paint`, `OnClick`/`OnContentClick` — защищённые, как в WinForms; `ParseFormattedValue` бросает на неверном вводе;
      значения `DataGridViewDataErrorContexts` были не те (и без `[Flags]`) — исправлены;
    - `VirtualMode`: `RowCount`/`ColumnCount`, `CellValueNeeded` для показа и правки, `CellValuePushed` при фиксации,
      `RowDirtyStateNeeded`, `CancelRowEdit`;
    - **фокус** (общая модель, не только сетка): `Leave` и `Validating/Validated` получает каждый покидаемый контрол
      до общего предка (внутренние первыми), `Enter` — каждый входимый (внешние первыми), как в
      `ContainerControl.UpdateFocusedControl`; раньше — только сам контрол. Сверяется оракулом `exact/focus/nested-*`.
    Проверено: `DataGridViewEditingTests` (порядок событий, отмена валидации, `DataError`, `CellParsing`,
    `EditingControlShowing`, клавиатура, `CancelEdit`, уход фокуса, комбо-ячейка, `VirtualMode`, **столбец-календарь из
    «How to: Host Controls in Windows Forms DataGridView Cells» — код из документации без правок**), оракул
    `exact/dgv/edit-*` и `formatting-raw`. Осознанные отличия (compatibility.md): необработанный `DataError` без окна
    сообщения; первая ячейка не становится текущей при создании окна. ApiDiff: 662 полных, 162 частичных, 430 нет;
    1712 членов.
143. **Второй выпуск — `0.1.0-preview.2`.** Заказчик попросил закончить на зафиксированном шаге и выпустить
    сделанное (решения 139–142). Версия — в `Directory.Build.props`, шаблоне `netforms-app` и `netformsVersion`
    расширения; само расширение — `0.1.1` (Marketplace не принимает ту же версию дважды; новые проекты ссылаются на
    `0.1.0-preview.2`). Раздел «Unreleased» в `CHANGELOG.md` и `designer/CHANGELOG.md` стал разделом выпуска; версия в
    README, `docs/*` (англ. и рус.), `eng/package/README.md` и на главных страницах сайта — новая. Выпуск, как в
    решении 135: пуш смены версии в `main` после зелёного CI предыдущего коммита.
144. **Инспектор дизайнера не сбрасывается при правке свойства.** Жалоба заказчика: после каждого изменения
    свойства «навигация сбрасывается, будто редактор открывается заново» — Anchor приходилось искать и раскрывать на
    каждый флаг. Причин две. (1) Хост сам сохраняет файл, VS Code сообщает об этом изменением документа, когда до него
    доходит наблюдатель файлов; расширение отличало свои записи по времени (окно 1,5 с), опоздавшее событие вызывало
    `reopen` с `reset` — выделялась форма. Теперь свои записи узнаются по содержимому (текст файла после каждого
    `apply`/`undo`/`redo`, последние восемь), событие во время записи перепроверяется после неё, а `reopen` сохраняет
    выделение, если его компоненты на месте. (2) Вебвью на каждый ответ перестраивал инспектор через «Loading…»:
    прокрутка — в начало, раскрытые флаги — закрыты, фокус — потерян. Теперь при том же выделении инспектор остаётся
    как есть до прихода новых значений, а перестройка сохраняет прокрутку, фокус (и недописанный текст — без коммита,
    который браузер присылает при удалении поля в фокусе) и раскрытые флаги своего компонента. Переименованный
    контрол остаётся выделенным под новым именем. Проверено: UI-тест «the property grid keeps its place…» (три флага
    Anchor подряд, прокрутка, выделение, фокус после Tab).
145. **Третий выпуск — `0.1.0-preview.3`.** Заказчик попросил выпустить исправление решения 144. Библиотека с
    `0.1.0-preview.2` не менялась, но выпуск расширения идёт только вместе с версией в `Directory.Build.props`
    (решение 135), поэтому версия поднята везде, как в решении 143; расширение — `0.1.2`. Выпуск — пуш смены версии в
    `main` после зелёного CI коммита с исправлением.

**Состояние тестов на конец сессии (2026-09-24, Linux):** .NET — **402/402**, расширение — **21/21**; оракулы
WinForms (`exact/binding/*`, `exact/dgv/edit-*`, `exact/dgv/style-*`, `exact/focus/nested-*`) и сверка атрибутов —
зелёные в CI на Windows.

### Что использует реальный код — порядок работ по корпусу (2026-09-24, Linux)

146. **Недостающий API ранжируется по использованию — `NetForms.ApiDiff --usage <папка> [--out docs/api/usage.md]`**
    (пункт 2 «С чего начать»). Каждый C#-проект каждого репозитория в папке (клоны корпуса — каталог `CorpusTests`,
    по папке на репозиторий) компилируется Roslyn-ом **против настоящих эталонных сборок WinForms** (тот же
    `Microsoft.WindowsDesktop.App.Ref`, что у ApiDiff), и каждая ссылка на тип или член WinForms привязывается так, как её
    привязывает компилятор: перегрузка, объявляющий тип, переопределяемый член (`override OnPaint` требует члена),
    конструктор в `new`/атрибуте, индексатор, пользовательский оператор; `var` не считается. Неявные using-и SDK-проекта с
    `ImplicitUsings` и `<Using Include>` добавляются; файл относится к ближайшему `.csproj` над ним. Компиляция против
    WinForms, а не против NetForms, — поэтому видно и то, что у нас не компилируется. Ссылка сверяется со списком
    недостающего: тип целиком или член существующего типа; член типа, которого у нас нет целиком, засчитывается типу —
    **кроме** случая, когда к нему обращаются через тип, который у нас есть и в котором этот член есть (`Controls.Count`:
    в WinForms `Count` объявлен на `ArrangedElementCollection`, которого нет, а у нашего `ControlCollection` он есть).
    Ключ члена (вид, имя, типы параметров, у методов и возвращаемый) один для рефлексии (что нам недостаёт) и для Roslyn
    (что использует код) — это держит тест на десяти типах, от `Control` до `GraphicsPath`. Результат —
    `docs/api/usage.md` (по типам и по членам, число репозиториев и обращений, где) и раздел 8 «Что дальше» в
    `compatibility.md`. Тесты: `ApiUsageTests`.
    **Итог по корпусу:** 24 репозитория, 63 проекта, 25 887 ссылок на API WinForms; до исправлений ниже недостающих — 16
    типов и членов, после — **11**, и все они в трёх проектах, которые не собираются: печать (MDBEditor);
    `ImageList.Images.Add(string, Icon)` (Surviving-WinForms, GetStockIcon); `LinkLabel.OverrideCursor` (xrails-login-ui,
    оба проекта — **не WPF**, как было записано в `corpus.json`: наследник `LinkLabel` задаёт защищённое свойство);
    мелочь в тех же проектах (`ImageFormat.Icon/Tiff/Wmf`, `OpenFileDialog.SafeFileName`, `TabPages.Remove`,
    `Font(FontFamily, float, FontStyle, GraphicsUnit, byte)`). Корпус почти целиком собирается, поэтому ранжировать
    остальные ~430 недостающих типов он уже не может — нужна выборка открытого кода, которому достаточно компилироваться
    против WinForms (открытый вопрос ниже).
    **Найдено и исправлено — типы не на своём месте.** Они есть, но в другом пространстве имён или вложенные: код,
    называющий их, не компилируется, а таблицы покрытия считают их отсутствующими. `TextureBrush` был в
    `System.Drawing.Drawing2D` (в WinForms — `System.Drawing`; `new TextureBrush(image)` не компилировался с `using
    System.Drawing;`), `FlushIntention` — наоборот, в `System.Drawing` (там — `Drawing2D`), `TableLayoutControlCollection`
    был вложен в `TableLayoutPanel` (там — тип верхнего уровня; из-за этого `TableLayoutPanel.Controls` стоял первым в
    рейтинге: 6 репозиториев, 197 обращений — компилировались, пока тип не называли явно), у `ImageList.ImageCollection`
    был выдуманный вложенный `StringCollection` (в WinForms `Keys` — `System.Collections.Specialized.StringCollection`,
    копия ключей с `""` у картинки без ключа). Класс ошибок закрыт тестом
    `EveryTypeNamedLikeAWinFormsTypeIsWhereWinFormsDeclaresIt`: публичный тип NetForms с именем типа WinForms лежит там же,
    где в WinForms. Фасады перегенерированы (`--forwards`, `--forwards-winforms`). ApiDiff: **664 полных, 163 частичных,
    427 нет; 1718 членов** (было 662/162/430, 1712: `TextureBrush` стал частичным — нет перегрузок с `RectangleF`/
    `ImageAttributes` и `…Transform(…, MatrixOrder)`).
    Заодно: расширение в Marketplace владелец выкладывает **вручную** (заказчик, 2026-09-24) — пропуск публикации в
    `vscode-publish` без секретов ожидаем.

**Состояние тестов на конец сессии (решение 146, 2026-09-24, Linux):** .NET — **415/415** (+13: `ApiUsageTests`,
`ImageKeysAreACopyInTheBclStringCollection`); оракулы WinForms — в CI на Windows.

---

# План Ф5 — визуальный дизайнер

Задание следующей сессии. Критерий заказчика: **редактирование работает полностью офлайн**.
База выбрана после проверки того, что реально можно форкнуть (см. «Что проверено»).

## Решение: расширение VS Code, превью рисует сам NetForms

Дизайнер живёт **в VS Code**, как и GoFormsDesigner: пользовательский редактор для
`*.Designer.cs`, тулбокс, панель свойств, команды в палитре. Требование заказчика —
повторить весь функционал GoFormsDesigner (список ниже), работая полностью офлайн.

Отличие от GoForms в одном, но важном месте. Там канва рисует каждый контрол **заново на JS**
(`media/renderPlan.js`, `gridLayout.js`, `dockLayout.js`), и расхождение с настоящим рендером
приходится ловить golden-фикстурой, которую генерирует Go-тест фреймворка. У нас контролов
вшестеро больше, и `DataGridView`, `ListView`, `TreeView`, `ToolStrip`, MDI пришлось бы
реализовать второй раз — с гарантированным дрейфом.

Поэтому **webview остаётся тонким клиентом**:

```
VS Code ──► webview (HTML/JS)          ──► хром редактирования: выделение, ручки,
   │           ▲            │                перетаскивание, направляющие, свойства,
   │           │ PNG+модель │ правки          события, выравнивание, undo
   │           │            ▼
   └────► NetFormsDesigner.Host (долгоживущий процесс .NET)
             • держит ЖИВОЙ экземпляр формы (Control, OnPaint, layout — настоящие)
             • Control.DrawToBitmap → PNG канвы
             • Roslyn: читает и переписывает *.Designer.cs
```

Что это даёт: **превью совпадает с рантаймом по построению** — это тот же код, что рисует
приложение, а не его имитация. Новый контрол появляется в дизайнере сам, без строчки JS.
Из портируемого webview уходят ровно те файлы, что рисуют превью, — остаётся хром
редактирования, который от фреймворка не зависит.

Офлайн: всё локально — расширение, webview и процесс .NET. Сеть не нужна ни при
редактировании, ни при запуске. Node нужен только чтобы собрать `.vsix` (как и у GoForms),
Roslyn (`Microsoft.CodeAnalysis.CSharp`) восстанавливается один раз при сборке хоста.

**Задержка.** PNG перерисовывается по событию (отпустили мышь, изменили свойство), а во время
перетаскивания webview двигает рамку выделения поверх последнего кадра — как это делают все
редакторы с внешним рендером. Процесс хоста долгоживущий (протокол по stdin/stdout), а не
запускается на каждый вызов, как Go-CLI в GoForms.

### Что проверено (чтобы не возвращаться к вопросу)

| Кандидат на форк | Лицензия | Вердикт |
|---|---|---|
| Дизайнер форм Visual Studio (`Microsoft.WinForms.Designer.*`) | закрытый | исходников нет, форкать нечего |
| `System.Windows.Forms.Design` (dotnet/winforms) | MIT | движок есть (`ControlDesigner.cs` ~98 КБ, `CommandSet`, `ComponentTray`), но приварен к HWND: `ControlDesigner` подменяет оконную процедуру и ловит `WM_*` через `IDesignerTarget`/`ChildWindowTarget`. У нас окон нет — это не форк, а переписывание слоя ввода |
| SharpDevelop FormsDesigner | архив, LGPL | построен на том же `DesignSurface` — та же проблема |
| webview из `../GoForms/GoFormsDesigner` | наш | **берём за основу UI** (~3.8k строк JS): канва, ручки, привязки, панель свойств, события, выравнивание. Не берём только превью контролов (`renderPlan.js`, `gridLayout.js`, `dockLayout.js`) — вместо них рисует настоящий NetForms |

**Ключевая находка:** переносимая половина инфраструктуры дизайнера уже лежит в BCL —
`System.ComponentModel.Design` даёт `IDesignerHost`, `ISelectionService`,
`IComponentChangeService`, `UndoEngine`, `INameCreationService`, `DesignerVerb`,
`DesignerActionList`. Всё чисто управляемое, без Win32. Из dotnet/winforms имеет смысл
вендорить только **чистую математику** — расчёт направляющих
(`Design/Behavior/DragAssistanceManager`), с копирайтом в `THIRD-PARTY-NOTICES.md`.

## Целевая структура

```
src/
  NetForms.Design/                   сервисы дизайн-тайма, без UI
    DesignSurface.cs                 держит живой корневой Control + DesignerHost
    DesignerHost.cs                  IDesignerHost, IContainer
    SelectionService.cs              ISelectionService
    ComponentChangeService.cs        IComponentChangeService
    NameCreationService.cs           button1, button2, … как в дизайнере VS
    DesignerUndoEngine.cs            наследник UndoEngine из BCL
    SnapLines.cs                     расчёт привязок (математика вендорится, MIT)
    DesignerRenderer.cs              DrawToBitmap канвы + карта попаданий для клиента
  NetForms.Design.Serialization/     код туда-обратно на Roslyn
    DesignerCodeReader.cs            InitializeComponent → модель
    DesignerCodeWriter.cs            модель → InitializeComponent
    EventHandlerWriter.cs            заглушки обработчиков в MainForm.cs
    DesignerTidy.cs                  чистка избыточного (аналог Tidy из GoForms)
    DesignerFileSet.cs               MainForm.cs + MainForm.Designer.cs + .resx
tools/
  NetFormsDesigner.Host/             долгоживущий процесс: JSON-протокол по stdin/stdout
                                     open/render/select/move/resize/set-property/
                                     wire-event/undo/redo/tidy/save
designer/                            расширение VS Code (порт GoFormsDesigner)
  src/extension.ts                   команды: новый проект, новая форма, открыть дизайнер,
                                     путь к фреймворку, Tidy, редактор темы
  src/designerEditorProvider.ts      пользовательский редактор для *.Designer.cs
  src/hostClient.ts                  клиент протокола (заменяет src/goTool.ts)
  media/designer.js                  канва, ручки, привязки, панель свойств, события,
                                     выравнивание, undo — порт из GoForms
  media/theme.js, theme.css          редактор темы
  templates/                         шаблоны проекта и формы
```

Файлы `media/renderPlan.js`, `gridLayout.js`, `dockLayout.js` из GoForms **не переносятся**:
их работу выполняет `DesignerRenderer` настоящим рендером.

## Порядок работ и ворота

**Ф5.0 — разметка публичного API атрибутами (предусловие, без неё дизайнер слепой). ✅ сделано,
см. решения 51–59.**
`[Category]`, `[Description]`, `[DefaultValue]`, `[Browsable]`, `[DesignerSerializationVisibility]`,
`[EditorBrowsable]` на свойства ~60 контролов. Сейчас у нашей `Button` одна категория «Misc»,
у настоящей — шесть; это уже измеряется ключом `info/propertygrid/root-children`.
*Ворота:* дифф-прогон даёт то же число категорий, что и WinForms, для `Button`, `TextBox`,
`Label`, `ListView`, `DataGridView`; `[DefaultValue]` совпадает с фактическим значением
свойства у нового экземпляра (тест-обход рефлексией по всем контролам).

**Ф5.1 — чтение designer-файла без компиляции. ✅ сделано, см. решения 60–66.**
`InitializeComponent()` — очень ограниченное подмножество C#: создание объектов, присваивание
свойств, `Controls.Add`, `SuspendLayout`/`ResumeLayout`, подписка на события, `AddRange`.
Читаем его Roslyn-ом и **интерпретируем** рефлексией над сборкой NetForms, без компиляции
проекта пользователя. Так дизайнер открывает форму мгновенно и офлайн.
*Ворота:* все формы из `samples/` (`HelloForms`, `Gallery`, `Strips`, `MdiDemo`, `GenLabs`)
читаются в модель, и дерево контролов совпадает с тем, что даёт настоящий вызов
`InitializeComponent()` — сравнение по именам, типам, `Bounds`, `Dock`/`Anchor`, порядку z.

**Ф5.2 — запись designer-файла. ✅ сделано, см. решения 67–78.**
Модель → `InitializeComponent` в каноническом порядке дизайнера VS (объявления, `SuspendLayout`,
блоки свойств по контролам с комментарием-заголовком, `ResumeLayout`), поля класса внизу.
Остальной файл (usings, `Dispose`, `#region`) сохраняется как есть.
*Ворота:* round-trip — прочитать → записать → прочитать снова даёт ту же модель, а для
`samples/*/*.Designer.cs` результат компилируется и даёт идентичное дерево. Это и есть
корпус совместимости дизайнера.

**Ф5.3 — хост и протокол. ✅ сделано, см. решения 79–86.**
`NetFormsDesigner.Host` — долгоживущий процесс: открывает файл, держит живую форму,
отдаёт PNG канвы и модель (дерево контролов, их прямоугольники, свойства, события),
принимает правки и пишет их в файл. Ввод в дизайн-тайме контролам **не отдаётся**: клик
по кнопке выделяет её, а не нажимает — у нас это фильтр в `DesignSurface`, без подмены
оконных процедур. Выделение/ручки/направляющие рисует клиент поверх PNG, поэтому хост
отдаёт и геометрию привязок (`SnapLines`).
*Ворота:* сценарные тесты протокола без UI — открыть `samples/Gallery`, подвинуть контрол,
сохранить, перечитать и получить ту же модель.

**Ф5.4 — расширение VS Code: канва. ✅ сделано, см. решения 87–90, 97.**
Порт `designerEditorProvider.ts` и `media/designer.js` из GoFormsDesigner с заменой
Go-CLI на клиент протокола. Приёмка — функционал GoFormsDesigner один в один:
перетаскивание и изменение размера; грипы формы справа/снизу/угол; выбор формы кликом по
фону, заголовку, рядом и по Escape; Ctrl/Shift-выделение группы и перетаскивание группой;
привязка к левым/правым краям и центрам соседей с линией-подсказкой; undo/redo своей
историей снимков; команды выравнивания Format по последнему выделенному.
*Ворота:* ручной чек-лист по README GoFormsDesigner плюс автотесты JS на математику
привязок и выравнивания.

**Ф5.5 — расширение: свойства, события, тулбокс, Tidy. ✅ сделано, см. решения 84, 88, 99–104.**
Панель свойств: `Name` с переименованием поля, всех ссылок и обработчиков, названных по
нему; редактор под тип — галочка для `bool`, спиннер для чисел, список для перечислений,
ряд галочек для флагов (`Anchor`); `Anchor`/`Dock`/`TabIndex`/`TabStop` у каждого контрола.
События: свои, затем унаследованные от `Control`; привязка создаёт заглушку в парном
`MainForm.cs` с правильной сигнатурой (`object sender, EventArgs e`,
`MouseEventArgs`, `KeyEventArgs`), перепривязка переписывает существующую строку.
Тулбокс: контролы по категориям (берутся рефлексией + атрибуты из Ф5.0), перетаскивание
создаёт экземпляр с именем от `NameCreationService`. `DesignerTidy` после каждой правки
убирает перекрытые сеттеры, сложенные подписки, повторные `Controls.Add`, дубли полей.
*Ворота:* «перетащить кнопку, переименовать, задать Text, повесить Click» даёт
компилирующийся проект, который запускается и реагирует на клик; Tidy на замусоренном
файле даёт файл, читающийся в ту же модель.

**Ф5.6 — команды и шаблоны. ✅ сделано, см. решения 91–93, 96** (кроме `Edit Theme` — темы в Ф6).
`NetForms: Create New Project` (пустой и пример), `New Form...` (палитра и правый клик по
папке), `Open Visual Designer`, `Set Framework Path`, `Tidy Designer File`, `Edit Theme`
(визуальный редактор `Theme.cs`, как в GoForms). Плюс `dotnet new netforms` и
`dotnet new netforms-form` для тех, кто без VS Code.
*Ворота:* `dotnet new netforms && dotnet run` в пустой папке даёт работающее окно;
`npm run package` собирает `.vsix`, он ставится и активируется.

**Ф5.7 — перевод проекта WinForms → NetForms (идея заказчика, 2026-09-22). ✅ сделано в малом, см.
решения 94–95, 98; корпус — открытый вопрос.**
Команда расширения `NetForms: Convert WinForms Project…` (палитра и правый клик по `.csproj`) и тот же
конвертер как CLI (`dotnet netforms convert <path>`), чтобы переносить без VS Code. Раз API — drop-in
(`System.Windows.Forms`/`System.Drawing`, решение §4), перевод — это в основном правка проекта, а не кода:
- `.csproj`: `<UseWindowsForms>true</UseWindowsForms>` → `<PackageReference Include="NetForms" />` плюс
  `<Using Include="System.Drawing" />`/`<Using Include="System.Windows.Forms" />` (неявные using-и, которые
  давал `UseWindowsForms`); `net8.0-windows` → `net10.0` (без `-windows`, чтобы собиралось на Linux);
  `<ApplicationHighDpiMode>`/`ApplicationVisualStyles` → то, что понимает наш `ApplicationConfiguration`;
  старый формат проекта .NET Framework (`packages.config`, `<Reference Include="System.Windows.Forms" />`) —
  сначала в SDK-стиль, как делает `upgrade-assistant`.
- **Отчёт совместимости до правки**: Roslyn-компиляция исходников пользователя против сборки NetForms —
  каждый отсутствующий у нас тип/член WinForms (метрика покрытия из §8 и Ф5.0, «50 типов, 2785 членов»),
  P/Invoke в `user32`/`gdi32`/`comctl32`, `WndProc`/`Handle`/`CreateParams`, `Microsoft.Win32.Registry`,
  COM/ActiveX, `System.Drawing.Common`-специфика, `.resx` с бинарными ресурсами (до Ф6) — список «что
  перенесётся как есть / что нужно поправить руками», с переходом к строке. Конвертер ничего не ломает
  молча: проект переписывается, только если пользователь согласился, исходная версия остаётся в
  `.csproj.winforms.bak` (или в git), и вся правка — один отменяемый шаг.
- Режимы: «только Windows» (NetForms, но `-windows` TFM оставить) и «Windows + Linux» (без `-windows`,
  плюс `SkiaSharp.NativeAssets.Linux` и проверка отчёта на Windows-only API).
- Designer-файлы проверяются нашим `DesignerCodeReader`: форма, которую он не открывает, попадает в отчёт
  с позицией (решение 62), значит и в дизайнере она не откроется.
*Ворота:* корпус реальных WinForms-примеров (§8) переводится командой и собирается против NetForms на
Windows и Linux CI; процент собравшихся без ручной правки — метрика в README; отчёт для каждого
несобравшегося указывает на причину.

## Что осознанно не делаем в Ф5

- **Не компилируем проект пользователя.** Поэтому его собственные контролы (наследники
  `Control` из того же проекта) в дизайнере не отрисуются — они получат заглушку с именем типа.
  Полная поддержка — отдельный этап: компиляция проекта в память Roslyn-ом и загрузка в
  `AssemblyLoadContext`. Записать как известное ограничение.
- ~~**Не поддерживаем `.resx`**~~ — чтение сделано в Ф6 (решения 107–108), запись — открытый вопрос Ф6.
- **Не делаем отдельное десктоп-приложение.** Оно возможно позже почти бесплатно: хост уже
  держит живую форму и умеет её рисовать, останется другой клиент. Но заказчику нужен
  редактор в VS Code, и он же лучше закрывает требование офлайна (всё локально).

## Риски

1. **Интерпретация `InitializeComponent`.** Ручной designer-код бывает нестандартным (циклы,
   вызовы методов). Митигация: интерпретатор понимает объявленное подмножество, всё остальное
   — не ошибка, а «этот файл дизайнер открыть не может» с указанием строки.
2. **Формат генерируемого кода.** Если он разойдётся с тем, что пишет VS, у переносимых
   проектов будет шумный git-дифф. Митигация: корпус `samples/*.Designer.cs` как эталон
   формата (ворота Ф5.2).
3. **Отзывчивость канвы.** Рендер живёт в другом процессе, и наивная перерисовка на каждый
   кадр перетаскивания будет тормозить. Митигация заложена в архитектуру: во время жеста
   клиент двигает рамку сам поверх последнего PNG, а перерисовка идёт по завершении жеста
   и по изменению свойства. Если окажется мало — следующий шаг не «рисовать на JS», а отдавать
   PNG только изменившегося прямоугольника.
4. **Порт JS из GoForms.** Код написан под семантику GoForms (сеттеры `SetDock`, каталог типов
   из `catalog.go`). Митигация: каталог типов в C# не нужен вовсе — свойства, события и их
   типы берутся рефлексией и атрибутами (Ф5.0), поэтому часть JS, завязанная на каталог,
   упрощается, а не переносится.

## С чего начать следующую сессию

Промт для запуска (скопировать в Claude Code в этой папке):

> Прочитай `CLAUDE.md`, в `docs/PLAN.md` — раздел «С чего начать следующую сессию» и решения 127–146. Работай сразу
> в `main` (разрешено заказчиком). Пункты 1–2 сделаны (решения 139–146); дальше пункт 3 — по списку «What comes next»
> в `docs/compatibility.md` § 8 (он же `docs/api/usage.md`).
> Каждое изменение — тест, сценарий оракула, `docs/compatibility.md` (англ. и рус.), `NetForms.ApiDiff --markdown
> docs/api` и `--usage <клоны корпуса> --out docs/api/usage.md`; пуш — после зелёных `dotnet test NetForms.slnx` и
> `designer: npm test`. Решения, которых нет в плане, записывай в журнал (со 147).

**Состояние на 2026-09-24.** В `main` после выпуска — решение 146 (порядок работ по использованию, четыре типа
перенесены на свои места); не выпущено. Выпущен `0.1.0-preview.3` (решение 145): инспектор дизайнера не сбрасывается при правке
свойства. До него — `0.1.0-preview.2` (решение 143): привязка данных WinForms, правка в `DataGridView`,
кнопка «+» обработчика в дизайнере. До него вышел `0.1.0-preview.1` (решение 138): семь пакетов на nuget.org, GitHub Release с тегом,
сайт <https://go-forms.github.io/.NetForms/>. Путь пользователя проверен с чистого кэша: шаблоны → проект → сборка из
пакетов, `netforms-convert --apply` → сборка. Расширение собирается (шесть платформенных `.vsix` + универсальный) и
лежит в GitHub Release; в Marketplace его выкладывает владелец вручную. CI и сайт зелёные.

Порядок работ:

0. **Проверить, что `main` зелёный** (CI на Ubuntu и Windows, Site), и прочитать решения 127–138.
1. ~~**Пробелы, найденные при проверке выпуска**~~ — **сделано 2026-09-24**: шрифт стилей (решение 140), привязка
   WinForms целиком (141), правка в `DataGridView` (142); кнопка «+» создания обработчика в дизайнере (139). Что
   осталось от сетки — «Открытые вопросы Ф6». Исходная постановка:
   1. **Баг:** в `Paint` своей ячейки (`DataGridViewCell` с переопределённым `Paint`) `cellStyle.Font == null` —
      должен приходить унаследованный стиль (`InheritedStyle`, шрифт сетки). Тест на офскрин-рендер.
   2. **Привязка к данным ADO.NET** — наша часть (адаптеры СУБД — SqlClient, Npgsql — отдельные пакеты, заказчик их
      отложил): `BindingSource` над `DataSet` с `DataMember = "Table"` даёт 0 строк; master-detail через `DataRelation`
      как `DataMember` пуст; `ComboBox`/`ListBox` с `DisplayMember`/`ValueMember` над `DataTable` показывают пустой
      текст; связь `DataRelation` лишней колонкой в сетке; нет `BindingContext`/`CurrencyManager`. Эталон —
      `dotnet/winforms` (`ListBindingHelper`, `CurrencyManager`, `BindingContext`, `RelatedCurrencyManager`),
      можно вендорить (MIT, в `THIRD-PARTY-NOTICES.md`).
   3. **Редактирование в `DataGridView`:** `EditingControl`, `EditingControlShowing`, `CellValidating`/`CellValidated`,
      `CellParsing`, `CurrentCellDirtyStateChanged`, `IDataGridViewEditingControl` (приёмка — колонка-календарь из
      документации Microsoft «How to: Host Controls in DataGridView Cells»), затем `VirtualMode`.
2. ~~**Ранжировать недостающее по востребованности**~~ — **сделано по корпусу 2026-09-24** (решение 146):
   `ApiDiff --usage`, `docs/api/usage.md`, `compatibility.md` § 8. Осталось: выборка популярных открытых WinForms-проектов
   (клонировать в ту же папку, что корпус, — собираться с NetForms им не нужно), чтобы упорядочить остальные ~430 типов.
3. **Корпус Ф6.К** — по § 8 `compatibility.md`: печать (`PrintDocument`, `PrintPageEventArgs`, `PrintDialog`, затем
   `PrintPreviewDialog`/`PageSetupDialog`; MDBEditor, с ним `ImageFormat.Icon/Tiff/Wmf`, `OpenFileDialog.SafeFileName`,
   `TabPages.Remove`), `ImageList.Images.Add(string, Icon)` (GetStockIcon), `LinkLabel.OverrideCursor` (xrails-login-ui, с
   ним `Font(FontFamily, float, FontStyle, GraphicsUnit, byte)`); вне сканера — `Microsoft.VisualBasic.Devices`
   (minesweeper), ссылка NLog, которую не переносит конвертер (NLogUtility); автодополнение в `TextBox`/`ComboBox`.
4. **Ф6 — полировка:** запись `.resx` дизайнером, DPI/AutoScale (per-monitor), темы и тёмная тема (+ `Edit Theme` в
   расширении), доступность (automation peers Avalonia), drag-and-drop, AOT/single-file. Дизайнер: свойства-
   расширители в панели свойств (`IExtenderProviderService`).
5. **Выпуск и инфраструктура.**
   - Расширение в Marketplace владелец выкладывает вручную (`.vsix` из GitHub Release); автоматическая публикация
     (`VSCE_PAT` до 30.11.2026, managed identity, `--oidc` — `docs/RELEASING.md`) остаётся на случай, если он передумает.
   - Зарезервировать префикс `NetForms.` на nuget.org (заявка владельца).
   - Следующий выпуск (`0.1.0-preview.4`): версия в `Directory.Build.props`, `templates/netforms-app/NetFormsApp1.csproj`,
     `designer/package.json` (`netformsVersion`), `CHANGELOG.md` (раздел «Unreleased»), `designer/CHANGELOG.md`, ApiDiff
     и цифры покрытия; пуш в `main` выпускает сам.
6. **Решения заказчика — без ответа не делать:** public signing фасадов открытым ключом Microsoft (решение 136),
   чтобы компилировались сторонние пакеты контролов (ZedGraph, OxyPlot, ScottPlot, FastColoredTextBox,
   ObjectListView); для DockPanelSuite ещё и снятие `FrameworkReference` WindowsDesktop.
7. Держать рядом `../GoForms/GoFormsDesigner/README.md` — список приёмки дизайнера.

Особенности облачной среды (Linux): .NET SDK — `apt-get update && apt-get install -y dotnet-sdk-10.0` (`dot.net`
закрыт прокси); `azuresearch-*.nuget.org` и `marketplace.visualstudio.com` закрыты — `dotnet new install <id>` из
nuget.org здесь не работает, `.nupkg` брать с `api.nuget.org/v3-flatcontainer/…` и ставить файлом; `grep` — ugrep,
молча пропускает часть файлов (`TextBox.cs`) — искать `grep -a` или Python; окончания строк сохранять пофайлово
(часть файлов в CRLF).

Дифф-прогон против настоящего WinForms (решение 34) остаётся главным инструментом проверки.
Теперь их три, все запускаются локально на Windows:

```powershell
# поведение и метрики
dotnet run --project tests/NetForms.Compat -- out.json
$env:NETFORMS_WINFORMS_SCENARIOS = "out.json"

# дизайн-тайм-метаданные (Ф5.0)
dotnet run --project tests/NetForms.Compat -- --attrs attrs.json
$env:NETFORMS_WINFORMS_ATTRS = "attrs.json"

# что пишет дизайнер: ShouldSerialize каждого свойства форм samples/ (Ф5.2)
dotnet run --project tests/NetForms.Compat -- --serialization ser.json
$env:NETFORMS_WINFORMS_SERIALIZATION = "ser.json"

# список публичных типов WinForms для конвертера (Ф5.7), пишется в tools/NetForms.Convert
dotnet run --project tests/NetForms.Compat -- --types tools/NetForms.Convert/winforms-types.txt

dotnet test NetForms.slnx
```

Расширение: `cd designer && npm install && npm test` (математика, перевод, протокол, UI в headless Edge —
нужен собранный хост), `npm run package` — `.vsix`.

**Состояние тестов на конец сессии (Ф6.К, RichTextBox, 2026-09-24, Windows):** .NET — **379/379**, в том числе
**со всеми тремя оракулами настоящего WinForms** (`NETFORMS_WINFORMS_SCENARIOS`, `_ATTRS`, `_SERIALIZATION` — решение
126); корпус (`NETFORMS_CORPUS=1`) — **24/24**: проекты заказчика 7 из 7, открытые — **27 из 45** WinForms-проектов
собираются без ручной правки (было 22), остальные 18 — в `knownFailures` с причинами. ApiDiff: **нет 457 типов из
1254** (было 491) и 1841 член существующих типов. Linux (WSL Ubuntu) — тоже **379/379**, golden `richtextbox-formatted`,
записанный на Windows, совпал. Расширение не менялось (20/20).

Предыдущее состояние (Ф6.К, 2026-09-23, Linux): .NET — **326/326** (в т.ч. 24 случая
`CorpusTests`: без `NETFORMS_CORPUS=1` они пропускают прогон). Оракулы WinForms в этой сессии не запускались
(нужен Windows): `AttributeDiffTests`/`CompatDiffTests` для нового API (решения 114–118) проверит Windows CI —
атрибуты скопированы из исходников WinForms, но расхождения возможны. Прежнее состояние на Windows с
оракулами — 272/272. Расширение не менялось (20/20).
