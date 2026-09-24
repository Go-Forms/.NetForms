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
    };

    public static string Get(string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" && s_ru.TryGetValue(english, out var ru) ? ru : english;
}
