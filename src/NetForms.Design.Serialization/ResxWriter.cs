using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NetForms.Design.Serialization;

/// <summary>
/// Writes the form's <c>.resx</c> as Visual Studio does: a picture or an icon as the bytes of its file
/// (<c>type="System.Drawing.Bitmap, System.Drawing"</c>, <c>bytearray.base64</c> - what its TypeConverter makes and
/// reads back), an <see cref="ImageListStreamer"/> as the BinaryFormatter record VS writes (<c>binary.base64</c>,
/// written here without BinaryFormatter: one class with one byte array). An existing file keeps everything it has;
/// an entry is replaced in place, a new one goes at the end, an entry no longer used is taken out.
/// </summary>
public static class ResxWriter
{
    private const string DrawingAlias = "System.Drawing";
    private const string DrawingAssembly = "System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";

    /// <summary>
    /// <paramref name="resx"/> (null: a new file) with the entries of <paramref name="put"/> written and those of
    /// <paramref name="remove"/> taken out. Returns null when there is no file and nothing to put.
    /// </summary>
    public static string? Update(string? resx, IReadOnlyDictionary<string, object> put, IEnumerable<string> remove, string newLine = "\r\n")
    {
        if (resx == null && put.Count == 0) return null;
        string nl = resx != null && !resx.Contains("\r\n", StringComparison.Ordinal) ? "\n" : resx != null ? "\r\n" : newLine;
        var text = new StringBuilder(resx ?? NewFile.Replace("\r\n", "\n").Replace("\n", nl) + nl + "</root>");
        var s = text.ToString();

        foreach (var name in remove)
        {
            if (put.ContainsKey(name)) continue;
            var m = Entry(s, name);
            if (m == null) continue;
            int start = LineStart(s, m.Index);
            int end = m.Index + m.Length;
            if (s.AsSpan(end).StartsWith(nl)) end += nl.Length;
            s = s.Remove(start, end - start);
        }

        foreach (var (name, value) in put)
        {
            var element = Element(name, value, nl, out bool drawing);
            var m = Entry(s, name);
            if (m != null)
            {
                s = s.Remove(m.Index, m.Length).Insert(m.Index, element.TrimStart());
            }
            else
            {
                int close = s.LastIndexOf("</root>", StringComparison.Ordinal);
                if (close < 0) throw new InvalidOperationException("The .resx has no </root>.");
                int at = LineStart(s, close);
                var add = new StringBuilder();
                // The assembly the type names point at, declared once before the first entry that uses it.
                if (drawing && !Regex.IsMatch(s, "<assembly\\s+alias=\"" + Regex.Escape(DrawingAlias) + "\""))
                    add.Append("  <assembly alias=\"").Append(DrawingAlias).Append("\" name=\"").Append(DrawingAssembly).Append("\" />").Append(nl);
                add.Append(element).Append(nl);
                s = s.Insert(at, add.ToString());
            }
        }
        return s;
    }

    /// <summary>Whether the designer can keep <paramref name="value"/> in the .resx (and so set it from a file).</summary>
    public static bool CanWrite(object? value) => value is Image or Icon or ImageListStreamer;

    /// <summary>The names <c>resources.GetObject("...")</c> asks for in a designer file.</summary>
    public static HashSet<string> ReferencedNames(string source)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(source, "resources\\.GetObject\\(\"((?:[^\"\\\\]|\\\\.)*)\"\\)")) names.Add(Regex.Unescape(m.Groups[1].Value));
        return names;
    }

    private static Match? Entry(string s, string name)
    {
        var m = Regex.Match(s, "<data\\s+name=\"" + Regex.Escape(System.Security.SecurityElement.Escape(name)) + "\"[^>]*?(/>|>.*?</data>)", RegexOptions.Singleline);
        return m.Success ? m : null;
    }

    private static int LineStart(string s, int index)
    {
        int i = index;
        while (i > 0 && s[i - 1] is ' ' or '\t') i--;
        return i;
    }

    private static string Element(string name, object value, string nl, out bool drawing)
    {
        drawing = false;
        string attributes;
        byte[] bytes;
        switch (value)
        {
            case ImageListStreamer streamer:
                attributes = "mimetype=\"application/x-microsoft.net.object.binary.base64\"";
                bytes = BinaryRecord(streamer.Serialize());
                break;
            case Icon or Image:
                drawing = true;
                var typeName = value is Icon ? "System.Drawing.Icon" : "System.Drawing.Bitmap";
                attributes = $"type=\"{typeName}, {DrawingAlias}\" mimetype=\"application/x-microsoft.net.object.bytearray.base64\"";
                bytes = (byte[])TypeDescriptor.GetConverter(value).ConvertTo(value, typeof(byte[]))!;
                break;
            default:
                throw new NotSupportedException($"The designer cannot keep a {value.GetType().Name} in the .resx.");
        }
        var b = new StringBuilder();
        b.Append("  <data name=\"").Append(System.Security.SecurityElement.Escape(name)).Append("\" ").Append(attributes).Append('>').Append(nl);
        b.Append("    <value>").Append(nl);
        var base64 = Convert.ToBase64String(bytes);
        for (int i = 0; i < base64.Length; i += 80)
            b.Append("        ").Append(base64, i, Math.Min(80, base64.Length - i)).Append(nl);
        b.Append("</value>").Append(nl);
        b.Append("  </data>");
        return b.ToString();
    }

    /// <summary>
    /// The BinaryFormatter (NRBF) record of an ImageListStreamer, as WinForms writes it: the header, the library
    /// System.Windows.Forms, the class with its one member Data (a byte array), the array, the end. System.Resources.
    /// Extensions reads it back into the type without BinaryFormatter (decision 123).
    /// </summary>
    public static byte[] BinaryRecord(byte[] data)
    {
        using var stream = new MemoryStream();
        using var w = new BinaryWriter(stream, Encoding.UTF8);
        w.Write((byte)0); w.Write(1); w.Write(-1); w.Write(1); w.Write(0);              // SerializedStreamHeader
        w.Write((byte)12); w.Write(2); w.Write("System.Windows.Forms, Culture=neutral, PublicKeyToken=b77a5c561934e089"); // BinaryLibrary
        w.Write((byte)5); w.Write(1); w.Write("System.Windows.Forms.ImageListStreamer");   // ClassWithMembersAndTypes
        w.Write(1); w.Write("Data");
        w.Write((byte)7);                                                                    // BinaryType: PrimitiveArray
        w.Write((byte)2);                                                                    // PrimitiveType: Byte
        w.Write(2);                                                                          // library id
        w.Write((byte)9); w.Write(3);                                                        // MemberReference → 3
        w.Write((byte)15); w.Write(3); w.Write(data.Length); w.Write((byte)2); w.Write(data); // ArraySinglePrimitive
        w.Write((byte)11);                                                                   // MessageEnd
        w.Flush();
        return stream.ToArray();
    }

    /// <summary>The head of a new .resx, as Visual Studio starts one.</summary>
    private const string NewFile = """
<?xml version="1.0" encoding="utf-8"?>
<root>
  <!--
    Microsoft ResX Schema

    Version 2.0

    The primary goals of this format is to allow a simple XML format
    that is mostly human readable. The generation and parsing of the
    various data types are done through the TypeConverter classes
    associated with the data types.
  -->
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:import namespace="http://www.w3.org/XML/1998/namespace" />
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="metadata">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" />
              </xsd:sequence>
              <xsd:attribute name="name" use="required" type="xsd:string" />
              <xsd:attribute name="type" type="xsd:string" />
              <xsd:attribute name="mimetype" type="xsd:string" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="assembly">
            <xsd:complexType>
              <xsd:attribute name="alias" type="xsd:string" />
              <xsd:attribute name="name" type="xsd:string" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" msdata:Ordinal="1" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
              <xsd:attribute ref="xml:space" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
""";
}
