using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Xml.Linq;

namespace NetForms.Design.Serialization;

/// <summary>
/// The <c>ComponentResourceManager</c> the designer gives <c>InitializeComponent</c> in place of the real
/// one: its resources come straight from the form's <c>.resx</c> next to the designer file (the project is
/// never compiled, so there is no embedded <c>.resources</c> to read). Values are made the way the
/// runtime makes them: strings as they are, base64 bytes and typed strings through the named type's
/// <see cref="TypeConverter"/>, <c>ResXFileRef</c> files read from disk.
/// </summary>
public sealed class ResxResources : ComponentResourceManager
{
    private readonly ResourceSet _set;

    /// <summary>The path of the .resx that was read.</summary>
    public string Path { get; }

    /// <summary>Whether the form is localizable (<c>$this.Localizable</c> metadata): its properties live in the .resx.</summary>
    public bool Localizable { get; }

    private ResxResources(string path, Dictionary<string, object?> values, bool localizable)
    {
        Path = path;
        Localizable = localizable;
        _set = new ResourceSet(new Reader(values));
    }

    /// <summary>The .resx of a designer file: <c>MainForm.Designer.cs</c> → <c>MainForm.resx</c>.</summary>
    public static string? ResxPathOf(string designerPath)
    {
        var name = System.IO.Path.GetFileName(designerPath);
        if (!name.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) return null;
        return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(designerPath) ?? "", name[..^".Designer.cs".Length] + ".resx");
    }

    /// <summary>Reads <paramref name="path"/>; <paramref name="resolveType"/> maps a .resx type name to a type (null: unknown).</summary>
    public static ResxResources Load(string path, Func<string, Type?> resolveType)
    {
        var document = XDocument.Load(path);
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        // An alias can be declared twice (VS writes one per edit session); as ResXResourceReader, the last wins.
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in document.Root!.Elements("assembly").Where(a => a.Attribute("alias") != null && a.Attribute("name") != null))
            aliases[(string)a.Attribute("alias")!] = (string)a.Attribute("name")!;
        foreach (var data in document.Root.Elements("data"))
        {
            var name = (string?)data.Attribute("name");
            if (name == null) continue;
            values[name] = new Pending(name, (string?)data.Attribute("type"), (string?)data.Attribute("mimetype"),
                data.Element("value")?.Value ?? "", path, aliases, resolveType);
        }
        bool localizable = document.Root.Elements("metadata")
            .Any(m => (string?)m.Attribute("name") == "$this.Localizable" && string.Equals(m.Element("value")?.Value?.Trim(), "True", StringComparison.OrdinalIgnoreCase));
        return new ResxResources(path, values, localizable);
    }

    protected override ResourceSet InternalGetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) => _set;

    public override ResourceSet GetResourceSet(CultureInfo culture, bool createIfNotExists, bool tryParents) => _set;

    public override object? GetObject(string name) => GetObject(name, null);

    public override object? GetObject(string name, CultureInfo? culture) => Materialize(_set.GetObject(name));

    public override string? GetString(string name) => GetString(name, null);

    /// <summary>
    /// A localizable form's <c>resources.ApplyResources(button1, "button1")</c>: every <c>button1.Xxx</c>
    /// resource set on the object's property <c>Xxx</c> (the base class would hand the properties the
    /// unconverted entries).
    /// </summary>
    public override void ApplyResources(object value, string objectName, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(objectName);
        var properties = TypeDescriptor.GetProperties(value);
        foreach (DictionaryEntry entry in _set)
        {
            var key = (string)entry.Key;
            if (!key.StartsWith(objectName + ".", StringComparison.Ordinal)) continue;
            var property = properties[key[(objectName.Length + 1)..]];
            if (property == null || property.IsReadOnly) continue;
            property.SetValue(value, Materialize(entry.Value));
        }
    }

    public override string? GetString(string name, CultureInfo? culture) => Materialize(_set.GetObject(name)) as string;

    /// <summary>A value is converted when it is asked for, so one resource the designer cannot read fails only where it is used.</summary>
    private static object? Materialize(object? value) => value is Pending pending ? pending.Value : value;

    private sealed class Pending
    {
        private readonly string _name, _mimeType, _text, _resxPath;
        private readonly string? _type;
        private readonly IReadOnlyDictionary<string, string> _aliases;
        private readonly Func<string, Type?> _resolve;
        private object? _value;
        private bool _done;

        public Pending(string name, string? type, string? mimeType, string text, string resxPath,
            IReadOnlyDictionary<string, string> aliases, Func<string, Type?> resolve)
        {
            (_name, _type, _mimeType, _text, _resxPath, _aliases, _resolve) = (name, type, mimeType ?? "", text, resxPath, aliases, resolve);
        }

        public object? Value
        {
            get
            {
                if (!_done) { _value = Make(); _done = true; }
                return _value;
            }
        }

        private object? Make()
        {
            if (_mimeType.Contains("binary.base64", StringComparison.Ordinal)) return FromBinaryFormatted();
            if (_mimeType.Contains("soap.base64", StringComparison.Ordinal))
                throw new NotSupportedException($"The resource '{_name}' in {System.IO.Path.GetFileName(_resxPath)} is SOAP-serialized; .NET cannot read it any more.");
            if (_type == null && _mimeType.Length == 0) return _text;

            var type = _type == null ? null : Resolve(_type);
            if (type == typeof(FileRefMarker))
                return FromFileRef();
            if (_type != null && type == null)
                throw new NotSupportedException($"The resource '{_name}' has a type the designer does not know: {_type}.");

            if (_mimeType.Contains("bytearray.base64", StringComparison.Ordinal))
            {
                var bytes = Convert.FromBase64String(_text);
                return type == null || type == typeof(byte[]) ? bytes : ConvertBytes(type, bytes);
            }
            if (type == typeof(string) || type == null) return _text;
            var converter = TypeDescriptor.GetConverter(type);
            return converter.CanConvertFrom(typeof(string)) ? converter.ConvertFromInvariantString(_text)
                : throw new NotSupportedException($"The resource '{_name}' ({type.FullName}) cannot be made from its text.");
        }

        /// <summary>
        /// A BinaryFormatter record (ImageList.ImageStream): read the way the built application reads it -
        /// pre-serialized into .resources and back through System.Resources.Extensions, which rebuilds
        /// [Serializable] types from the NRBF records without BinaryFormatter (decision 123).
        /// </summary>
        private object? FromBinaryFormatted()
        {
            var bytes = Convert.FromBase64String(_text);
            try
            {
                string typeName = System.Formats.Nrbf.NrbfDecoder.Decode(new MemoryStream(bytes)).TypeName.AssemblyQualifiedName;
                using var resources = new MemoryStream();
                using (var writer = new System.Resources.Extensions.PreserializedResourceWriter(resources))
                {
#pragma warning disable SYSLIB0011 // only copies the bytes, as MSBuild's GenerateResource does
                    writer.AddBinaryFormattedResource(_name, bytes, typeName);
#pragma warning restore SYSLIB0011
                    writer.Generate();
                }
                using var reader = new System.Resources.Extensions.DeserializingResourceReader(new MemoryStream(resources.ToArray()));
                var e = reader.GetEnumerator();
                return e.MoveNext() ? e.Value : null;
            }
            catch (Exception ex) when (ex is not NotSupportedException)
            {
                throw new NotSupportedException($"The resource '{_name}' in {System.IO.Path.GetFileName(_resxPath)} is BinaryFormatter-serialized " +
                    $"and cannot be read without BinaryFormatter: {ex.Message}", ex);
            }
        }

        /// <summary><c>ResXFileRef</c>: "path;type[;encoding]", the path relative to the .resx.</summary>
        private object? FromFileRef()
        {
            var parts = _text.Split(';');
            var file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_resxPath) ?? "", parts[0].Trim());
            var type = parts.Length > 1 ? Resolve(parts[1].Trim()) : null;
            if (type == typeof(string))
            {
                var encoding = parts.Length > 2 ? System.Text.Encoding.GetEncoding(parts[2].Trim()) : System.Text.Encoding.UTF8;
                return File.ReadAllText(file, encoding);
            }
            var bytes = File.ReadAllBytes(file);
            return type == null || type == typeof(byte[]) ? bytes : ConvertBytes(type, bytes);
        }

        private object? ConvertBytes(Type type, byte[] bytes)
        {
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.CanConvertFrom(typeof(byte[]))) return converter.ConvertFrom(bytes);
            if (type == typeof(MemoryStream) || type == typeof(Stream)) return new MemoryStream(bytes);
            throw new NotSupportedException($"The resource '{_name}' ({type.FullName}) cannot be made from bytes.");
        }

        /// <summary>"System.Drawing.Bitmap, System.Drawing" (the part after the comma may be an alias of an assembly element).</summary>
        private Type? Resolve(string typeName)
        {
            var comma = typeName.IndexOf(',');
            var full = (comma < 0 ? typeName : typeName[..comma]).Trim();
            return full switch
            {
                "System.String" => typeof(string),
                "System.Byte[]" => typeof(byte[]),
                "System.Resources.ResXFileRef" => typeof(FileRefMarker),
                _ => _resolve(full) ?? Type.GetType(full),
            };
        }
    }

    /// <summary>Stands for System.Resources.ResXFileRef (a WinForms type): its FullName is all that is asked.</summary>
    private sealed class FileRefMarker { }

    private sealed class Reader : IResourceReader
    {
        private readonly Dictionary<string, object?> _values;
        public Reader(Dictionary<string, object?> values) => _values = values;
        public IDictionaryEnumerator GetEnumerator() => new Hashtable(_values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Close() { }
        public void Dispose() { }
    }
}
