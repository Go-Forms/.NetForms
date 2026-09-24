# Стартовый промт для первой сессии

Скопируй в Claude Code, запущенный в этой папке:

---

Прочитай `CLAUDE.md` и `docs/PLAN.md` целиком. Мы начинаем проект .NetForms —
кроссплатформенную копию WinForms на C# (Avalonia 12 как платформенный слой,
своё дерево контролов и отрисовка через SkiaSharp, неймспейсы `System.Windows.Forms`
и `System.Drawing`). Предыдущая версия идеи на Go лежит в `../GoForms` — она остаётся
как есть, оттуда только берём спецификацию и тесты, как описано в CLAUDE.md.

Сделай **фазу Ф0 — вертикальный срез**:

1. Проверь установленный .NET SDK (`dotnet --version`), выбери TFM по правилу из
   CLAUDE.md. Создай решение по целевой структуре из CLAUDE.md: `NetForms.Platform`,
   `NetForms.Platform.Avalonia`, `NetForms.Drawing`, `NetForms`, `NetForms.Tests`,
   `samples/HelloForms`.
2. Реализуй минимум: `Application.Run(Form)`, `Form` (Text, ClientSize, Show/Close,
   Load/Resize/FormClosing), `Control` (Bounds/Location/Size, Parent, Controls,
   Visible/Enabled, Font/ForeColor/BackColor, Anchor/Dock, Invalidate → OnPaint,
   Click/MouseDown/MouseUp/MouseMove/Enter/Leave, Focus), `Label`, `Button`,
   `PaintEventArgs` с `Graphics`, у которого есть `Clear`, `FillRectangle`,
   `DrawRectangle`, `DrawLine`, `DrawString`, `MeasureString`.
3. Avalonia используется только внутри `NetForms.Platform.Avalonia`: одно окно на
   `Form`, один custom-drawn `Control` Avalonia на всё клиентское поле, Skia-lease для
   рисования, события ввода транслируются в наше дерево. Никаких Avalonia-типов в
   публичном API.
4. `samples/HelloForms` — это `Program.cs`, `MainForm.cs`, `MainForm.Designer.cs`,
   написанные **ровно так, как их сгенерировала бы Visual Studio** для WinForms:
   кнопка и подпись, по клику меняется текст, кнопка заякорена Bottom|Right и должна
   ехать при ресайзе окна.
5. Тесты: офскрин-рендер формы в `SKBitmap` без открытия окна + проверка, что
   Anchor/Dock дают те же координаты, что матрицы из
   `../GoForms/GoForms/anchor_columns_test.go` и `golden_dock_test.go` (перенеси кейсы).
6. `dotnet build` и `dotnet test` должны проходить; запусти сэмпл и убедись, что
   окно открывается, клик работает, ресайз двигает кнопку.

Критерий готовности Ф0: тот же код `HelloForms` собирается и с настоящим
`System.Windows.Forms` на Windows без единой правки. Не расширяй набор контролов
дальше Label/Button — это Ф2. Если по ходу нужно принять решение, которого нет
в `docs/PLAN.md`, — запиши его в раздел «Решения» PLAN.md и продолжай.

---
