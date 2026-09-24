using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetForms.Design.Serialization;

/// <summary>
/// Writes event handler stubs into the hand-written half of a form (<c>MainForm.cs</c>), the way the
/// Visual Studio designer does when an event is double-clicked: <c>private void button1_Click(object
/// sender, EventArgs e) { }</c> at the end of the class, unless a method of that name is already there.
/// </summary>
public static class EventHandlerWriter
{
    /// <summary>
    /// <paramref name="source"/> with a stub for <paramref name="handlerName"/> added to the class
    /// <paramref name="className"/>, typed after <paramref name="delegateType"/>'s <c>Invoke</c>. Returns
    /// the source unchanged when the class already has a member of that name.
    /// </summary>
    /// <param name="line">1-based line of the method's name, for "go to the handler".</param>
    public static string EnsureHandler(string source, string className, string handlerName, Type delegateType, out bool added, out int line)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(delegateType);
        if (!SyntaxFacts.IsValidIdentifier(handlerName)) throw new ArgumentException($"'{handlerName}' is not a valid method name.", nameof(handlerName));

        var tree = CSharpSyntaxTree.ParseText(source);
        var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().Where(c => c.Identifier.ValueText == className).ToList();
        if (classes.Count == 0) throw new ArgumentException($"The file has no class '{className}'.", nameof(source));

        foreach (var c in classes)
        {
            var existing = c.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == handlerName);
            if (existing != null)
            {
                added = false;
                line = existing.Identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                return source;
            }
        }

        var cls = classes[0];
        var newLine = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var classIndent = DesignerCodeStyle.LeadingWhitespace(source, cls.SpanStart);
        var memberIndent = cls.Members.Count > 0 ? DesignerCodeStyle.LeadingWhitespace(source, cls.Members[0].SpanStart) : classIndent + "    ";

        var stub = new StringBuilder();
        if (cls.Members.Count > 0) stub.Append(newLine);
        stub.Append(memberIndent).Append("private void ").Append(handlerName).Append('(').Append(Parameters(delegateType)).Append(')').Append(newLine);
        stub.Append(memberIndent).Append('{').Append(newLine);
        stub.Append(newLine);
        stub.Append(memberIndent).Append('}').Append(newLine);

        // Just before the line of the class's closing brace.
        int insertAt = source.LastIndexOf('\n', Math.Max(0, cls.CloseBraceToken.SpanStart - 1)) + 1;
        var result = source.Insert(insertAt, stub.ToString());
        added = true;
        line = source.Substring(0, insertAt).Count(ch => ch == '\n') + (cls.Members.Count > 0 ? 2 : 1);
        return result;
    }

    /// <summary><c>object sender, MouseEventArgs e</c> for a <c>MouseEventHandler</c>.</summary>
    public static string Parameters(Type delegateType)
    {
        var invoke = delegateType.GetMethod("Invoke") ?? throw new ArgumentException($"'{delegateType}' is not a delegate type.", nameof(delegateType));
        var ps = invoke.GetParameters();
        return string.Join(", ", ps.Select((p, i) => $"{TypeName(p.ParameterType)} {(ps.Length == 2 ? (i == 0 ? "sender" : "e") : p.Name)}"));
    }

    /// <summary>The name VS writes: short for the namespaces a WinForms project imports implicitly.</summary>
    private static string TypeName(Type t)
    {
        if (t == typeof(object)) return "object";
        if (t == typeof(string)) return "string";
        if (t == typeof(int)) return "int";
        if (t == typeof(bool)) return "bool";
        var full = (t.FullName ?? t.Name).Replace('+', '.');
        return t.Namespace is "System" or "System.Windows.Forms" or "System.Drawing" ? full.Substring(t.Namespace.Length + 1) : full;
    }
}
