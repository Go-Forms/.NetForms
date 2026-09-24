using System;

namespace NetForms.Design.Serialization;

/// <summary>
/// "The designer cannot open this file": <c>InitializeComponent</c> contains something outside the
/// subset the designer understands (a loop, a lambda, an unknown member), or the file does not parse.
/// Carries the position so the editor can point at the offending line.
/// </summary>
public sealed class DesignerCodeException : Exception
{
    public DesignerCodeException(string message, string? filePath, int line, int column, Exception? inner = null)
        : base(Format(message, filePath, line, column), inner)
    {
        Reason = message;
        FilePath = filePath;
        Line = line;
        Column = column;
    }

    /// <summary>The message without the position prefix.</summary>
    public string Reason { get; }

    public string? FilePath { get; }

    /// <summary>1-based line.</summary>
    public int Line { get; }

    /// <summary>1-based column.</summary>
    public int Column { get; }

    private static string Format(string message, string? filePath, int line, int column) =>
        $"{filePath ?? "InitializeComponent"}({line},{column}): {message}";
}
