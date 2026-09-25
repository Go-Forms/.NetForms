using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;

namespace System.Windows.Forms;

/// <summary>Data in several formats at once: what the clipboard holds and what a drag carries.</summary>
public interface IDataObject
{
    object? GetData(string format, bool autoConvert);

    object? GetData(string format);

    object? GetData(Type format);

    bool GetDataPresent(string format, bool autoConvert);

    bool GetDataPresent(string format);

    bool GetDataPresent(Type format);

    string[] GetFormats(bool autoConvert);

    string[] GetFormats();

    void SetData(string format, bool autoConvert, object? data);

    void SetData(string format, object? data);

    void SetData(Type format, object? data);

    void SetData(object? data);
}

/// <summary>An <see cref="IDataObject"/> that hands out data typed (.NET 9+: TryGetData instead of GetData).</summary>
public interface ITypedDataObject : IDataObject
{
    bool TryGetData<T>([NotNullWhen(true), MaybeNullWhen(false)] out T data);

    bool TryGetData<T>(string format, [NotNullWhen(true), MaybeNullWhen(false)] out T data);

    bool TryGetData<T>(string format, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data);

    bool TryGetData<T>(string format, Func<TypeName, Type?> resolver, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data);
}

/// <summary>TryGetData on any <see cref="IDataObject"/>: typed if it is an <see cref="ITypedDataObject"/>, else through GetData.</summary>
public static class DataObjectExtensions
{
    public static bool TryGetData<T>(this IDataObject dataObject, [NotNullWhen(true), MaybeNullWhen(false)] out T data) =>
        Typed(dataObject).TryGetData(out data);

    public static bool TryGetData<T>(this IDataObject dataObject, string format, [NotNullWhen(true), MaybeNullWhen(false)] out T data) =>
        Typed(dataObject).TryGetData(format, out data);

    public static bool TryGetData<T>(this IDataObject dataObject, string format, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data) =>
        Typed(dataObject).TryGetData(format, autoConvert, out data);

    public static bool TryGetData<T>(this IDataObject dataObject, string format, Func<TypeName, Type?> resolver, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data) =>
        Typed(dataObject).TryGetData(format, resolver, autoConvert, out data);

    private static ITypedDataObject Typed(IDataObject dataObject)
    {
        ArgumentNullException.ThrowIfNull(dataObject);
        return dataObject as ITypedDataObject
            ?? throw new NotSupportedException($"'{dataObject.GetType().FullName}' does not implement ITypedDataObject.");
    }
}

/// <summary>
/// The WinForms data object: a store of formats, with the conversions WinForms makes when asked with
/// autoConvert (Text/UnicodeText/System.String are one, FileDrop answers FileName/FileNameW, a Bitmap answers
/// System.Drawing.Bitmap), or a wrapper around another <see cref="IDataObject"/>.
/// </summary>
public class DataObject : ITypedDataObject
{
    private readonly IDataObject? _inner;
    private readonly List<(string Format, object? Data, bool AutoConvert)> _entries = new();

    public DataObject()
    {
    }

    public DataObject(object data)
    {
        if (data is IDataObject inner && data is not DataObject) _inner = inner;
        else if (data is DataObject other) _inner = other;
        else SetData(data);
    }

    public DataObject(string format, object data) => SetData(format, data);

    // --- the store ------------------------------------------------------------------------------------

    private int IndexOf(string format)
    {
        for (int i = 0; i < _entries.Count; i++) if (string.Equals(_entries[i].Format, format, StringComparison.Ordinal)) return i;
        return -1;
    }

    /// <summary>The formats <paramref name="format"/> can be read as (itself first), as WinForms' GetMappedFormats.</summary>
    private static string[] Mapped(string format)
    {
        if (format is DataFormats.Text or DataFormats.UnicodeText or DataFormats.StringFormat)
            return [format, DataFormats.StringFormat, DataFormats.UnicodeText, DataFormats.Text];
        if (format is DataFormats.FileDrop or "FileName" or "FileNameW")
            return [format, DataFormats.FileDrop, "FileNameW", "FileName"];
        if (format == DataFormats.Bitmap || format == typeof(Bitmap).FullName)
            return [format, typeof(Bitmap).FullName!, DataFormats.Bitmap];
        return [format];
    }

    /// <summary>The text formats are one another however they were put, as the OS synthesizes them.</summary>
    private static bool IsText(string format) => format is DataFormats.Text or DataFormats.UnicodeText or DataFormats.StringFormat;

    private static object? Convert(string asked, object? stored)
    {
        // FileName/FileNameW are one file: the first of the list.
        if (asked is "FileName" or "FileNameW" && stored is string[] { Length: > 0 } files) return new[] { files[0] };
        return stored;
    }

    public virtual object? GetData(string format, bool autoConvert)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_inner != null) return _inner.GetData(format, autoConvert);
        int i = IndexOf(format);
        if (i >= 0) return _entries[i].Data;
        if (!autoConvert) return null;
        foreach (var mapped in Mapped(format))
        {
            int j = IndexOf(mapped);
            if (j >= 0 && (_entries[j].AutoConvert || IsText(mapped))) return Convert(format, _entries[j].Data);
        }
        return null;
    }

    public virtual object? GetData(string format) => GetData(format, autoConvert: true);

    public virtual object? GetData(Type format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return GetData(format.FullName!);
    }

    public virtual bool GetDataPresent(string format, bool autoConvert)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (_inner != null) return _inner.GetDataPresent(format, autoConvert);
        if (IndexOf(format) >= 0) return true;
        if (!autoConvert) return false;
        foreach (var mapped in Mapped(format))
        {
            int j = IndexOf(mapped);
            if (j >= 0 && (_entries[j].AutoConvert || IsText(mapped))) return true;
        }
        return false;
    }

    public virtual bool GetDataPresent(string format) => GetDataPresent(format, autoConvert: true);

    public virtual bool GetDataPresent(Type format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return GetDataPresent(format.FullName!);
    }

    public virtual string[] GetFormats(bool autoConvert)
    {
        if (_inner != null) return _inner.GetFormats(autoConvert);
        var result = new List<string>();
        foreach (var (format, _, convert) in _entries)
        {
            if (!result.Contains(format)) result.Add(format);
            if (!autoConvert || !convert && !IsText(format)) continue;
            foreach (var mapped in Mapped(format)) if (!result.Contains(mapped)) result.Add(mapped);
        }
        return result.ToArray();
    }

    public virtual string[] GetFormats() => GetFormats(autoConvert: true);

    public virtual void SetData(string format, bool autoConvert, object? data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        if (_inner != null)
        {
            _inner.SetData(format, autoConvert, data);
            return;
        }
        // A Bitmap given as "Bitmap" is stored as such; the same object then also answers System.Drawing.Bitmap.
        int i = IndexOf(format);
        if (i >= 0) _entries[i] = (format, data, autoConvert);
        else _entries.Add((format, data, autoConvert));
    }

    public virtual void SetData(string format, object? data) => SetData(format, autoConvert: true, data);

    public virtual void SetData(Type format, object? data)
    {
        ArgumentNullException.ThrowIfNull(format);
        SetData(format.FullName!, data);
    }

    public virtual void SetData(object? data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (_inner != null)
        {
            _inner.SetData(data);
            return;
        }
        // As WinForms: a Bitmap goes in as "Bitmap", anything else under its type's name - a string as System.String,
        // which autoConvert also reads as Text and UnicodeText.
        if (data is Bitmap) SetData(DataFormats.Bitmap, data);
        else SetData(data.GetType().FullName!, data);
    }

    // --- JSON (.NET 9+) --------------------------------------------------------------------------------

    /// <summary>Marks data put with SetDataAsJson: read back typed, or as JSON by another process.</summary>
    internal sealed class JsonData
    {
        public JsonData(object? value, Type type)
        {
            Value = value;
            Type = type;
        }

        public object? Value { get; }

        public Type Type { get; }

        public string Json => JsonSerializer.Serialize(Value, Type);
    }

    public void SetDataAsJson<T>(T data) => SetDataAsJson(typeof(T).FullName!, data);

    public void SetDataAsJson<T>(string format, T data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(data);
        if (typeof(T) == typeof(DataObject)) throw new InvalidOperationException("DataObject cannot be serialized as JSON; use SetData or SetDataObject.");
        SetData(format, new JsonData(data, typeof(T)));
    }

    // --- typed access ----------------------------------------------------------------------------------

    public bool TryGetData<T>([NotNullWhen(true), MaybeNullWhen(false)] out T data) => TryGetData(typeof(T).FullName!, out data);

    public bool TryGetData<T>(string format, [NotNullWhen(true), MaybeNullWhen(false)] out T data) => TryGetData(format, autoConvert: true, out data);

    public bool TryGetData<T>(string format, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data) =>
        TryGetDataCore(format, null, autoConvert, out data);

    public bool TryGetData<T>(string format, Func<TypeName, Type?> resolver, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data) =>
        TryGetDataCore(format, resolver, autoConvert, out data);

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected virtual bool TryGetDataCore<T>(string format, Func<TypeName, Type?>? resolver, bool autoConvert, [NotNullWhen(true), MaybeNullWhen(false)] out T data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        data = default;
        var value = _inner is ITypedDataObject typed && _inner is not DataObject
            ? (typed.TryGetData<T>(format, autoConvert, out var t) ? t : null)
            : GetData(format, autoConvert);
        switch (value)
        {
            case T direct:
                data = direct;
                return true;
            case JsonData json when json.Value is T same:
                data = same;
                return true;
            case JsonData json:
                // Another type than the one put: go through the JSON, as a receiving process would.
                data = JsonSerializer.Deserialize<T>(json.Json);
                return data != null;
            case string text when typeof(T) != typeof(string) && text.Length > 0 && text[0] is '{' or '[':
                try
                {
                    data = JsonSerializer.Deserialize<T>(text);
                    return data != null;
                }
                catch (JsonException)
                {
                    return false;
                }
            default:
                return false;
        }
    }

    // --- the convenience formats -----------------------------------------------------------------------

    public virtual bool ContainsAudio() => GetDataPresent(DataFormats.WaveAudio, autoConvert: false);

    public virtual bool ContainsFileDropList() => GetDataPresent(DataFormats.FileDrop, autoConvert: true);

    public virtual bool ContainsImage() => GetDataPresent(DataFormats.Bitmap, autoConvert: true);

    public virtual bool ContainsText() => ContainsText(TextDataFormat.UnicodeText);

    public virtual bool ContainsText(TextDataFormat format)
    {
        if (!Enum.IsDefined(format)) throw new InvalidEnumArgumentException(nameof(format), (int)format, typeof(TextDataFormat));
        return GetDataPresent(ConvertToDataFormats(format), autoConvert: false)
            || format is TextDataFormat.Text or TextDataFormat.UnicodeText && GetDataPresent(DataFormats.UnicodeText, autoConvert: true);
    }

    public virtual Stream? GetAudioStream() => GetData(DataFormats.WaveAudio, autoConvert: false) as Stream;

    public virtual StringCollection GetFileDropList()
    {
        var result = new StringCollection();
        if (GetData(DataFormats.FileDrop, autoConvert: true) is string[] paths) result.AddRange(paths);
        return result;
    }

    public virtual Image? GetImage() => GetData(DataFormats.Bitmap, autoConvert: true) as Image;

    public virtual string GetText() => GetText(TextDataFormat.UnicodeText);

    public virtual string GetText(TextDataFormat format)
    {
        if (!Enum.IsDefined(format)) throw new InvalidEnumArgumentException(nameof(format), (int)format, typeof(TextDataFormat));
        var autoConvert = format is TextDataFormat.Text or TextDataFormat.UnicodeText;
        return GetData(ConvertToDataFormats(format), autoConvert) as string ?? string.Empty;
    }

    public virtual void SetAudio(byte[] audioBytes) => SetAudio(new MemoryStream(audioBytes ?? throw new ArgumentNullException(nameof(audioBytes))));

    public virtual void SetAudio(Stream audioStream) => SetData(DataFormats.WaveAudio, autoConvert: false, audioStream ?? throw new ArgumentNullException(nameof(audioStream)));

    public virtual void SetFileDropList(StringCollection filePaths)
    {
        ArgumentNullException.ThrowIfNull(filePaths);
        var paths = new string[filePaths.Count];
        filePaths.CopyTo(paths, 0);
        SetData(DataFormats.FileDrop, autoConvert: true, paths);
    }

    public virtual void SetImage(Image image) => SetData(DataFormats.Bitmap, autoConvert: true, image ?? throw new ArgumentNullException(nameof(image)));

    public virtual void SetText(string textData) => SetText(textData, TextDataFormat.UnicodeText);

    public virtual void SetText(string textData, TextDataFormat format)
    {
        ArgumentException.ThrowIfNullOrEmpty(textData);
        if (!Enum.IsDefined(format)) throw new InvalidEnumArgumentException(nameof(format), (int)format, typeof(TextDataFormat));
        SetData(ConvertToDataFormats(format), autoConvert: false, textData);
    }

    private static string ConvertToDataFormats(TextDataFormat format) => format switch
    {
        TextDataFormat.Text => DataFormats.Text,
        TextDataFormat.Rtf => DataFormats.Rtf,
        TextDataFormat.Html => DataFormats.Html,
        TextDataFormat.CommaSeparatedValue => DataFormats.CommaSeparatedValue,
        _ => DataFormats.UnicodeText,
    };
}
