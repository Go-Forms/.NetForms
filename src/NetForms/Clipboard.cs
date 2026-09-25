using System;

namespace System.Windows.Forms;

/// <summary>The system clipboard. Text only for now; other formats arrive with drag-and-drop in Ф4.</summary>
public static class Clipboard
{
    public static void SetText(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        Application.Platform.SetClipboardText(text);
    }

    public static void SetText(string text, TextDataFormat format) => SetText(text);

    public static string GetText() => Application.Platform.GetClipboardText() ?? string.Empty;

    public static string GetText(TextDataFormat format) => GetText();

    public static bool ContainsText() => !string.IsNullOrEmpty(Application.Platform.GetClipboardText());

    public static bool ContainsText(TextDataFormat format) => ContainsText();

    public static void Clear() => Application.Platform.SetClipboardText(string.Empty);

    // The platform clipboard carries text. Other formats - images, file lists, objects - are kept in the
    // process (decision 116): the application reads back what it put, other applications see its text.
    private static IDataObject? s_data;
    private static string? s_dataText;

    public static void SetDataObject(object data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data is string s)
        {
            s_data = null;
            Application.Platform.SetClipboardText(s);
            return;
        }
        s_data = data as IDataObject ?? new DataObject(data);
        s_dataText = s_data.GetData(DataFormats.Text) as string ?? string.Empty;
        Application.Platform.SetClipboardText(s_dataText);
    }

    public static void SetDataObject(object data, bool copy) => SetDataObject(data);

    public static void SetDataObject(object data, bool copy, int retryTimes, int retryDelay) => SetDataObject(data);

    public static IDataObject? GetDataObject()
    {
        var text = Application.Platform.GetClipboardText();
        // Ours while nobody has replaced the text since.
        if (s_data != null && (text ?? string.Empty) == s_dataText) return s_data;
        s_data = null;
        return text == null ? null : new DataObject(DataFormats.Text, text);
    }

    public static void SetData(string format, object data) => SetDataObject(new DataObject(format, data));

    public static object? GetData(string format) => GetDataObject()?.GetData(format);

    public static bool ContainsData(string format) => GetDataObject()?.GetDataPresent(format) == true;

    public static void SetImage(System.Drawing.Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        SetDataObject(new DataObject(DataFormats.Bitmap, image));
    }

    public static System.Drawing.Image? GetImage() => GetData(DataFormats.Bitmap) as System.Drawing.Image;

    public static bool ContainsImage() => ContainsData(DataFormats.Bitmap);

    public static void SetFileDropList(System.Collections.Specialized.StringCollection filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        var paths = new string[filePaths.Count];
        filePaths.CopyTo(paths, 0);
        SetDataObject(new DataObject(DataFormats.FileDrop, paths));
    }

    public static System.Collections.Specialized.StringCollection GetFileDropList()
    {
        var result = new System.Collections.Specialized.StringCollection();
        if (GetData(DataFormats.FileDrop) is string[] paths) result.AddRange(paths);
        return result;
    }

    public static bool ContainsFileDropList() => ContainsData(DataFormats.FileDrop);

    public static void SetAudio(byte[] audioBytes) => SetAudio(new System.IO.MemoryStream(audioBytes ?? throw new ArgumentNullException(nameof(audioBytes))));

    public static void SetAudio(System.IO.Stream audioStream) => SetDataObject(new DataObject(DataFormats.WaveAudio, audioStream ?? throw new ArgumentNullException(nameof(audioStream))));

    public static System.IO.Stream? GetAudioStream() => GetData(DataFormats.WaveAudio) as System.IO.Stream;

    public static bool ContainsAudio() => ContainsData(DataFormats.WaveAudio);

    // --- typed access (.NET 9+) ---------------------------------------------------------------------

    public static void SetDataAsJson<T>(string format, T data)
    {
        var dataObject = new DataObject();
        dataObject.SetDataAsJson(format, data);
        SetDataObject(dataObject);
    }

    public static bool TryGetData<T>([System.Diagnostics.CodeAnalysis.NotNullWhen(true), System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T data) =>
        TryGetData(typeof(T).FullName!, out data);

    public static bool TryGetData<T>(string format, [System.Diagnostics.CodeAnalysis.NotNullWhen(true), System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T data)
    {
        data = default;
        var dataObject = GetDataObject();
        if (dataObject == null) return false;
        return (dataObject as ITypedDataObject ?? new DataObject(dataObject)).TryGetData(format, out data);
    }

    public static bool TryGetData<T>(string format, Func<System.Reflection.Metadata.TypeName, Type?> resolver,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true), System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T data)
    {
        data = default;
        var dataObject = GetDataObject();
        if (dataObject == null) return false;
        return (dataObject as ITypedDataObject ?? new DataObject(dataObject)).TryGetData(format, resolver, autoConvert: true, out data);
    }
}

public enum TextDataFormat
{
    Text = 0,
    UnicodeText = 1,
    Rtf = 2,
    Html = 3,
    CommaSeparatedValue = 4,
}

public static class DataFormats
{
    public const string Text = "Text";
    public const string UnicodeText = "UnicodeText";
    public const string StringFormat = "System.String";
    public const string Rtf = "Rich Text Format";
    public const string Html = "HTML Format";
    public const string FileDrop = "FileDrop";
    public const string Bitmap = "Bitmap";
    public const string WaveAudio = "WaveAudio";
    public const string CommaSeparatedValue = "Csv";
    public const string Dib = "DeviceIndependentBitmap";
    public const string Dif = "DataInterchangeFormat";
    public const string EnhancedMetafile = "EnhancedMetafile";
    public const string Locale = "Locale";
    public const string MetafilePict = "MetaFilePict";
    public const string OemText = "OEMText";
    public const string Palette = "Palette";
    public const string PenData = "PenData";
    public const string Riff = "RiffAudio";
    public const string Serializable = "WindowsForms10PersistentObject";
    public const string SymbolicLink = "SymbolicLink";
    public const string Tiff = "TaggedImageFileFormat";

    /// <summary>A clipboard format: its name and its id - the Win32 CF_* number, or one registered from 0xC000 on.</summary>
    public class Format
    {
        public Format(string name, int id)
        {
            Name = name;
            Id = id;
        }

        public string Name { get; }

        public int Id { get; }
    }

    private static readonly object s_lock = new();
    private static readonly System.Collections.Generic.List<Format> s_formats = new()
    {
        new(Text, 1), new(Bitmap, 2), new(MetafilePict, 3), new(SymbolicLink, 4), new(Dif, 5), new(Tiff, 6), new(OemText, 7),
        new(Dib, 8), new(Palette, 9), new(PenData, 10), new(Riff, 11), new(WaveAudio, 12), new(UnicodeText, 13),
        new(EnhancedMetafile, 14), new(FileDrop, 15), new(Locale, 16),
    };
    private static int s_nextId = 0xC000;

    /// <summary>The format named <paramref name="format"/> (case-sensitive, as RegisterClipboardFormat), registering it if new.</summary>
    public static Format GetFormat(string format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        lock (s_lock)
        {
            foreach (var f in s_formats) if (string.Equals(f.Name, format, StringComparison.Ordinal)) return f;
            var added = new Format(format, s_nextId++);
            s_formats.Add(added);
            return added;
        }
    }

    /// <summary>The format with id <paramref name="id"/>; an unknown id gets a "Format{id}" entry, as WinForms does.</summary>
    public static Format GetFormat(int id)
    {
        lock (s_lock)
        {
            foreach (var f in s_formats) if (f.Id == (id & 0xFFFF)) return f;
            var added = new Format("Format" + id, id & 0xFFFF);
            s_formats.Add(added);
            return added;
        }
    }
}
