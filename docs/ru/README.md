# Документация NetForms

NetForms — копия Windows Forms. **Справочник по API — от Microsoft:** каждая страница
[learn.microsoft.com/ru-ru/dotnet/desktop/winforms](https://learn.microsoft.com/ru-ru/dotnet/desktop/winforms/) и
[справочника `System.Windows.Forms`](https://learn.microsoft.com/ru-ru/dotnet/api/system.windows.forms)
применима — в тех же пространствах имён, с теми же именами и значениями по умолчанию. Здесь описано только
то, что относится к самому NetForms.

| Страница | |
|---|---|
| [Установка и настройка](install.md) | .NET SDK на Windows и каждом семействе Linux (Ubuntu, Debian, Fedora, Astra, РЕД ОС, ALT), системные библиотеки, шаблоны и конвертер из NuGet, VS Code и дизайнер, закрытые сети, поставка приложения на Linux. |
| [Первое приложение](getting-started.md) | `dotnet new netforms`, первая форма, свой контрол, публикация. |
| [Перевод WinForms-проекта](migrating.md) | Почему WinForms не запускается на Linux, `netforms-convert`, что значит отчёт, неполадки. |
| [Совместимость](compatibility.md) | Какие версии .NET и ОС поддерживаются, какие проекты переносятся, состояние каждого контрола и подсистемы, API только для Windows, осознанные отличия. |
| [Покрытие API](../api/README.md) (англ.) | Генерируется автоматически: каждый публичный тип WinForms и `System.Drawing.Common` — полный, частичный или отсутствующий, со списком недостающих членов и ссылкой на страницу Microsoft. |
| [Визуальный дизайнер](designer.md) | Расширение VS Code: как устроено, команды, настройки. |
| [Выпуск версий](../RELEASING.md) | Для разработчиков NetForms: публикация в NuGet, VS Code Marketplace, Open VSX, GitHub Pages. |
| [План и журнал решений](../PLAN.md) | Архитектура, дорожная карта и каждое принятое решение. |
| [English documentation](../README.md) | Те же страницы на английском. |
