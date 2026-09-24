using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.Design.Serialization;

/// <summary>
/// How <see cref="DesignerCodeWriter"/> spells <c>InitializeComponent</c>. Two dialects exist in the
/// wild, and a file keeps the one it was written in, so that a small edit gives a small diff:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>Modern</b> - the .NET designer (VS 2022, .NET 6+): <c>button1.Text = "OK";</c>, short type
/// names where the project's implicit usings make them unambiguous, <c>button1.Click += button1_Click;</c>,
/// one-line arrays.</item>
/// <item><b>Classic</b> - the CodeDom designer of .NET Framework: <c>this.button1.Text = "OK";</c>, fully
/// qualified types, <c>new System.EventHandler(this.button1_Click)</c>, CodeDom's parenthesised casts
/// (<c>((byte)(204))</c>) and one array element per line.</item>
/// </list>
/// </remarks>
public sealed record DesignerCodeStyle
{
    /// <summary><c>this.</c>-qualified members, full type names, CodeDom casts and delegate creation.</summary>
    public bool Classic { get; init; }

    /// <summary>The comment lines around a block header: <c>//</c> or, as CodeDom wrote them, <c>// </c>.</summary>
    public string CommentLine { get; init; } = "//";

    /// <summary>Indentation of a statement inside <c>InitializeComponent</c>.</summary>
    public string Indent { get; init; } = "            ";

    public string NewLine { get; init; } = "\r\n";

    public static DesignerCodeStyle Modern { get; } = new();

    public static DesignerCodeStyle ClassicCodeDom { get; } = new() { Classic = true, CommentLine = "// " };

    /// <summary>The style a designer file is already written in; <see cref="Modern"/> for an empty method.</summary>
    public static DesignerCodeStyle Detect(string source, MethodDeclarationSyntax initializeComponent)
    {
        var body = initializeComponent.Body;
        var statements = body?.Statements ?? default;
        bool classic = statements.Any(s => s is ExpressionStatementSyntax es && StartsWithThis(es.Expression));

        var newLine = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var text = body?.ToFullString() ?? "";
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r').TrimStart()).ToList();
        string comment = lines.Contains("// ") ? "// " : lines.Contains("//") ? "//" : classic ? "// " : "//";

        string indent;
        if (statements.Count > 0)
        {
            indent = LeadingWhitespace(source, statements[0].SpanStart);
        }
        else
        {
            var methodIndent = LeadingWhitespace(source, initializeComponent.SpanStart);
            indent = methodIndent + "    ";
        }
        return new DesignerCodeStyle { Classic = classic, CommentLine = comment, Indent = indent, NewLine = newLine };
    }

    private static bool StartsWithThis(ExpressionSyntax e)
    {
        while (true)
        {
            switch (e)
            {
                case AssignmentExpressionSyntax a: e = a.Left; continue;
                case InvocationExpressionSyntax i: e = i.Expression; continue;
                case MemberAccessExpressionSyntax m:
                    if (m.Expression is ThisExpressionSyntax) return true;
                    e = m.Expression;
                    continue;
                case ParenthesizedExpressionSyntax p: e = p.Expression; continue;
                case CastExpressionSyntax c: e = c.Expression; continue;
                default: return false;
            }
        }
    }

    /// <summary>The whitespace between the start of the line and <paramref name="position"/>.</summary>
    internal static string LeadingWhitespace(string source, int position)
    {
        int start = source.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
        int end = start;
        while (end < position && (source[end] == ' ' || source[end] == '\t')) end++;
        return source.Substring(start, end - start);
    }
}
