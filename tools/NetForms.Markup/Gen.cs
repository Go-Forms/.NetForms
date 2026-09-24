// Shared bits of the markup generator: the data shape of the metadata dump and the rendering of
// a default value back into C# source.

using System.Globalization;
using System.Text;

static class Gen
{
    /// <summary>Enums we declare ourselves, so a default can be written as `BorderStyle.None`.</summary>
    public static readonly Dictionary<string, HashSet<string>> Enums = new(StringComparer.Ordinal);

    /// <summary>
    /// With <paramref name="explicitOverrides"/> the browsability, serialization and
    /// localizability are written out even when they hold their default value. That matters on a
    /// re-declared member: TypeDescriptor merges a derived member's attributes over the base
    /// member's, so the only way to say "visible again" on top of an inherited [Browsable(false)]
    /// is to say [Browsable(true)] here.
    /// </summary>
    public static List<string> AttributesFor(MemberDump m, bool explicitOverrides = false)
    {
        if (explicitOverrides)
        {
            var full = AttributesFor(m)
                .Where(a => !a.StartsWith("[Localizable", StringComparison.Ordinal)
                         && !a.StartsWith("[Browsable", StringComparison.Ordinal)
                         && !a.StartsWith("[DesignerSerializationVisibility", StringComparison.Ordinal))
                .ToList();
            full.Add($"[Localizable({(m.Localizable ? "true" : "false")})]");
            full.Add($"[Browsable({(m.Browsable ? "true" : "false")})]");
            full.Add($"[EditorBrowsable(EditorBrowsableState.{(m.Browsable ? "Always" : "Never")})]");
            full.Add($"[DesignerSerializationVisibility(DesignerSerializationVisibility.{m.Serialization})]");
            return full;
        }

        var list = new List<string>();

        if (!string.IsNullOrEmpty(m.Category) && m.Category != "Misc")
            list.Add($"[Category(\"{m.Category}\")]");

        if (!string.IsNullOrWhiteSpace(m.Description))
            list.Add($"[Description({Literal(m.Description!.Trim())})]");

        if (m.HasDefaultValue)
        {
            var expr = DefaultValueExpression(m);
            if (expr != null) list.Add($"[DefaultValue({expr})]");
        }

        if (m.Localizable) list.Add("[Localizable(true)]");
        if (!m.Browsable) list.Add("[Browsable(false)]");
        if (m.Serialization != "Visible")
            list.Add($"[DesignerSerializationVisibility(DesignerSerializationVisibility.{m.Serialization})]");

        return list;
    }

    public static string? DefaultValueExpression(MemberDump m)
    {
        if (m.DefaultValue is null) return "null";
        var v = m.DefaultValue;

        switch (m.Type)
        {
            case "Boolean": return v.ToLowerInvariant();
            case "String": return Literal(v);
            case "Char": return "'" + (v == "\\" ? "\\\\" : v == "'" ? "\\'" : v) + "'";
            case "Int32": return v;
            case "Int64": return v + "L";
            case "Single": return v + "F";
            case "Double": return v + "D";
            case "Decimal": return v + "M";
            case "Int16": return $"(short){v}";
            case "Byte": return $"(byte){v}";
        }

        if (Enums.TryGetValue(m.Type, out var members))
        {
            var parts = v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && parts.All(members.Contains))
                return string.Join(" | ", parts.Select(p => $"{m.Type}.{p}"));
        }

        // The dump carries short type names; nested ones need their declaring type back.
        var typeName = m.Type switch
        {
            "SpecialFolder" => "Environment.SpecialFolder",
            _ => m.Type,
        };
        return $"typeof({typeName}), {Literal(v)}";
    }

    public static string Literal(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (var c in s)
        {
            sb.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                _ => c.ToString(CultureInfo.InvariantCulture),
            });
        }
        return sb.Append('"').ToString();
    }
}

sealed class MemberDump
{
    public string Kind { get; set; } = "property";
    public string Type { get; set; } = "";
    public string DeclaredBy { get; set; } = "";
    public string Category { get; set; } = "";
    public bool Browsable { get; set; } = true;
    public bool ReadOnly { get; set; }
    public bool Localizable { get; set; }
    public string Serialization { get; set; } = "Visible";
    public bool HasDefaultValue { get; set; }
    public string? DefaultValue { get; set; }
    public bool HasDescription { get; set; }
    public string? Description { get; set; }
}

sealed class TypeDump
{
    public string BaseType { get; set; } = "";
    public bool IsAbstract { get; set; }
    public Dictionary<string, MemberDump> Members { get; set; } = new(StringComparer.Ordinal);
}
