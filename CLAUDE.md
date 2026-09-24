# .NetForms

Кроссплатформенная (Windows + Linux) реализация WinForms на C#. Цель — **копия
`System.Windows.Forms`**: тот же API, та же модель разработки (`Program.cs` →
`Application.Run(new MainForm())`, два файла на форму: `MainForm.cs` +
`MainForm.Designer.cs` с `InitializeComponent()`), тот же визуальный дизайнер —
но одинаково работающая на обеих ОС.

Полный план, архитектура, дорожная карта и принятые решения: **`docs/PLAN.md`** —
читать перед любой работой. Стартовое задание для первой сессии: `PROMPT.md`.

## Ключевые решения (не пересматривать без обсуждения)

- **Платформенный слой — Avalonia 12** (окно, ввод, IME, буфер обмена, нативные
  диалоги, DPI, GPU-поверхность). Avalonia используется как замена Win32, **не** как
  набор виджетов: контролы Avalonia в публичном API не появляются.
- **Своё дерево `Control`, своя отрисовка через SkiaSharp.** `Invalidate` → `OnPaint`,
  хит-тест, фокус, mouse capture, z-order, Anchor/Dock — наши, по образцу WinForms.
  Пользователь должен мочь написать `class MyControl : Control` и переопределить `OnPaint`.
- **Неймспейсы — `System.Windows.Forms` и `System.Drawing`** (drop-in замена). Бренд и
  имя пакета — `NetForms`. `Point`/`Size`/`Rectangle`/`Color` берём из
  `System.Drawing.Primitives`, не пишем свои.
- **Эталон поведения — настоящий WinForms.** `dotnet/winforms` и Mono SWF (оба MIT)
  читаем как спецификацию; чисто управляемые куски (layout-движок
  `TableLayoutPanel`/`FlowLayoutPanel`, `DataGridView`, `ToolStrip`, `PropertyGrid`)
  можно вендорить с сохранением копирайтов в `THIRD-PARTY-NOTICES.md`.
- **Пиксельную совместимость с Win32-темой не преследуем.** Своя подключаемая тема,
  выглядящая как WinForms, но чище.
- **Метрики текста** — эталон `TextRenderer.MeasureText`, реализованный на Skia.
- **DPI** решается на первом этапе: работаем в device-independent пикселях.

## Связь с GoForms

Предыдущая инкарнация идеи — `../GoForms` (Go + Fyne). **Она остаётся и не трогается.**
Оттуда переиспользуем:

- семантику layout (`../GoForms/GoForms/layout.go`, `layoutpanels.go`), политику ширин
  `DataGridView` (`datagridview.go`), геометрию рамки `GroupBox`, маски `MaskedTextBox`
  (`inputcontrols.go`) — как спецификацию, код переписывается;
- матрицы golden-тестов (`golden_dock_test.go`, `responsiveness_matrix_test.go`,
  `anchor_columns_test.go`) — переносятся один в один;
- webview дизайнера (`../GoForms/GoFormsDesigner/media/*`) — почти как есть, он работает
  с JSON-моделью формы и не знает про язык. Go-CLI (`tool/*.go`) заменяется Roslyn;
  каталог типов не нужен — свойства/события берутся рефлексией + атрибутами
  `System.ComponentModel`.

## Структура (целевая)

```
src/
  NetForms.Platform/           IWindowBackend, IInputSource, IClipboard, INativeDialogs
  NetForms.Platform.Avalonia/  реализация на Avalonia
  NetForms.Drawing/            System.Drawing на SkiaSharp
  NetForms/                    System.Windows.Forms: Control, Form, Application, контролы
tests/
  NetForms.Tests/              golden-рендер офскрин + поведение
  NetForms.Compat/             дифф-тесты против настоящего WinForms (только Windows CI)
samples/                       Program.cs + MainForm.cs + MainForm.Designer.cs
designer/                      VS Code расширение (позже, фаза Ф5)
docs/PLAN.md
```

## Правила

- Целевой TFM — `net10.0` (если SDK не установлен — `net8.0`, зафиксировать в плане).
- Каждый контрол — отдельный файл, публичный API повторяет сигнатуры WinForms
  дословно (имена свойств, событий, типов аргументов, значения по умолчанию).
- Любая новая семантика layout/событий подтверждается тестом; расхождение с WinForms —
  задокументированная осознанная разница, а не случайность.
- CI: `ubuntu-latest` + `windows-latest`, headless-рендер на обеих.
