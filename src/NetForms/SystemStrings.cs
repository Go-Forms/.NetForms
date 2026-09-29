using System.Collections.Generic;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// The strings WinForms takes from the operating system (user32's message box buttons, comctl32's
/// calendar "Today:"), so they follow the UI language (decision 118): English, and Russian for a Russian
/// UI culture - the language of the project's users. Other languages fall back to English.
/// </summary>
internal static class SystemStrings
{
    private static readonly Dictionary<string, string> s_ru = new()
    {
        ["OK"] = "ОК",
        ["Cancel"] = "Отмена",
        ["&Abort"] = "&Прервать",
        ["&Retry"] = "&Повторить",
        ["&Ignore"] = "&Пропустить",
        ["&Yes"] = "&Да",
        ["&No"] = "&Нет",
        ["&Try Again"] = "&Повторить",
        ["&Continue"] = "&Продолжить",
        ["Help"] = "Справка",
        ["&Close"] = "&Закрыть",
        ["See details"] = "Подробнее",
        ["Hide details"] = "Скрыть подробности",
        ["Today:"] = "Сегодня:",
        // RichTextBox.UndoActionName/RedoActionName and its errors: WinForms' own resources (SR), which the
        // Russian language pack of the Windows Desktop runtime translates as below.
        ["Unknown"] = "Неизвестно",
        ["Typing"] = "Ввод с клавиатуры",
        ["Delete"] = "Удалить",
        ["Drag and Drop"] = "Перетаскивание",
        ["Cut"] = "Вырезать",
        ["Paste"] = "Вставить",
        ["File format is not valid."] = "Недопустимый формат файла.",
        ["File type is not valid."] = "Недопустимый тип файла.",
        ["SelTabCount out of range."] = "SelTabCount вне допустимого диапазона.",
        ["Value '{0}' is not a valid value for 'end'.  'end' must be greater than or equal to 'start', or -1."] =
            "Значение {0} недопустимо для параметра end.  Значение end должно быть больше или равно значению start или равно -1.",
        // The print dialogs (comdlg32's PrintDlg and PageSetupDlg, which Windows localizes) and WinForms' own print
        // preview and status dialog (SR, translated by the Russian language pack).
        ["Print"] = "Печать",
        ["Printer"] = "Принтер",
        ["&Name:"] = "&Имя:",
        ["Status:"] = "Состояние:",
        ["Type:"] = "Тип:",
        ["Ready"] = "Готов",
        ["Ready (default printer)"] = "Готов (принтер по умолчанию)",
        ["No printers are installed."] = "Принтеры не установлены.",
        ["P&roperties..."] = "&Свойства...",
        ["Print to fi&le"] = "Печать в &файл",
        ["Page range"] = "Диапазон страниц",
        ["&All"] = "&Все",
        ["Selectio&n"] = "Выделенный &фрагмент",
        ["Current pa&ge"] = "&Текущая страница",
        ["Pa&ges"] = "&Страницы",
        ["from"] = "с",
        ["to"] = "по",
        ["Copies"] = "Копии",
        ["Number of &copies:"] = "&Число копий:",
        ["C&ollate"] = "&Разобрать по копиям",
        ["&Print"] = "&Печать",
        ["&Help"] = "&Справка",
        ["This value does not lie within the page range.\nEnter a number between {0} and {1}."] =
            "Это значение выходит за пределы диапазона страниц.\nВведите число от {0} до {1}.",
        ["The first page number must not be greater than the last."] = "Номер первой страницы не может быть больше номера последней.",
        ["{0} Document Properties"] = "Свойства документа: {0}",
        ["Paper"] = "Бумага",
        ["Paper si&ze:"] = "&Размер бумаги:",
        ["Paper &source:"] = "&Подача бумаги:",
        ["Si&ze:"] = "&Размер:",
        ["&Source:"] = "&Подача:",
        ["Orientation"] = "Ориентация",
        ["P&ortrait"] = "&Книжная",
        ["L&andscape"] = "&Альбомная",
        ["Print in co&lor"] = "&Цветная печать",
        ["Print on &both sides:"] = "&Двусторонняя печать:",
        ["None"] = "Нет",
        ["Flip on long edge"] = "Переворот по длинному краю",
        ["Flip on short edge"] = "Переворот по короткому краю",
        ["Print &quality:"] = "&Качество печати:",
        ["Page Setup"] = "Параметры страницы",
        ["Margins (millimeters)"] = "Поля (мм)",
        ["Margins (inches)"] = "Поля (дюймы)",
        ["&Left:"] = "&Левое:",
        ["&Right:"] = "&Правое:",
        ["&Top:"] = "&Верхнее:",
        ["&Bottom:"] = "&Нижнее:",
        ["P&rinter..."] = "&Принтер...",
        ["Save Print Output As"] = "Сохранение результатов печати",
        ["PDF document (*.pdf)|*.pdf|All files (*.*)|*.*"] = "Документ PDF (*.pdf)|*.pdf|Все файлы (*.*)|*.*",
        ["Print preview"] = "Предварительный просмотр",
        ["Zoom"] = "Масштаб",
        ["Auto"] = "Авто",
        ["One page"] = "Одна страница",
        ["Two pages"] = "Две страницы",
        ["Three pages"] = "Три страницы",
        ["Four pages"] = "Четыре страницы",
        ["Six pages"] = "Шесть страниц",
        ["Page"] = "Страница",
        ["Printing"] = "Печать",
        ["Canceling Print..."] = "Отмена печати...",
        ["Page {0} of {1}"] = "Страница {0} документа {1}",
        // BindingNavigator (SR, the Russian language pack).
        ["Move first"] = "Переместить в начало",
        ["Move previous"] = "Переместить назад",
        ["Move next"] = "Переместить вперед",
        ["Move last"] = "Переместить в конец",
        ["Add new"] = "Добавить",
        ["of {0}"] = "для {0}",
        ["Total number of items"] = "Общее число элементов",
        ["Current position"] = "Текущее положение",
        ["Position"] = "Положение",
    };

    public static string Get(string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" && s_ru.TryGetValue(english, out var ru) ? ru : english;
}
