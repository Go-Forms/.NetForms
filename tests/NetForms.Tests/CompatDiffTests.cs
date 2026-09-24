using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NetForms.Compat;
using Xunit;
using Xunit.Abstractions;

namespace NetForms.Tests;

/// <summary>
/// The measurable "how much of a copy is this" test. NetForms runs the shared scenarios;
/// when NETFORMS_WINFORMS_SCENARIOS points at the JSON produced by tests/NetForms.Compat
/// (real WinForms, Windows CI), every "exact/" key must match and every "text/" key must be
/// within a few pixels. Without the file the scenarios still run and their output is saved,
/// so a regression in our own numbers shows up as a diff of netforms-scenarios.json.
/// </summary>
public class CompatDiffTests
{
    private readonly ITestOutputHelper _output;

    public CompatDiffTests(ITestOutputHelper output) => _output = output;

    private const int TextTolerance = 3;

    /// <summary>
    /// Differences kept on purpose, each with the decision (docs/PLAN.md) that explains it. Anything else
    /// that differs fails the test.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDifferences = new(StringComparer.Ordinal)
    {
        // A WinForms control without a window keeps the negative width SetBounds gave it; once the
        // window exists Win32 clamps it to 0 - which is what NetForms always does (decision 110).
        ["exact/dock/huge-left/body"] = "negative size before the handle exists",
    };

    [Fact]
    public void ScenariosMatchRealWinForms()
    {
        TestPlatform.Install();
        var ours = CompatScenarios.Run();

        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "netforms-scenarios.json"), JsonSerializer.Serialize(ours, new JsonSerializerOptions { WriteIndented = true }));

        var referencePath = Environment.GetEnvironmentVariable("NETFORMS_WINFORMS_SCENARIOS");
        if (string.IsNullOrEmpty(referencePath) || !File.Exists(referencePath))
        {
            _output.WriteLine("NETFORMS_WINFORMS_SCENARIOS not set: real-WinForms comparison skipped (runs on Windows CI).");
            Assert.NotEmpty(ours);
            return;
        }

        var theirs = JsonSerializer.Deserialize<SortedDictionary<string, string>>(File.ReadAllText(referencePath))!;
        var failures = new List<string>();
        var textDiffs = new List<string>();
        foreach (var (key, expected) in theirs)
        {
            if (!ours.TryGetValue(key, out var actual))
            {
                failures.Add($"{key}: missing in NetForms");
                continue;
            }
            if (key.StartsWith("info/", StringComparison.Ordinal))
            {
                if (expected != actual) _output.WriteLine($"info: {key}: WinForms={expected} NetForms={actual}");
                continue;
            }
            if (key.StartsWith("text/", StringComparison.Ordinal))
            {
                if (!WithinTolerance(expected, actual, TextTolerance))
                {
                    textDiffs.Add($"{key}: WinForms={expected} NetForms={actual}");
                }
                continue;
            }
            if (expected == actual) continue;
            if (IsRtf(expected) && IsRtf(actual) && NormalizeRtfFonts(expected) == NormalizeRtfFonts(actual))
            {
                _output.WriteLine($"rtf equal up to the font table: {key}");
                continue;
            }
            if (KnownDifferences.TryGetValue(key, out var why))
            {
                _output.WriteLine($"known difference: {key}: WinForms={expected} NetForms={actual} ({why})");
                continue;
            }
            failures.Add($"{key}: WinForms={expected} NetForms={actual}");
        }

        foreach (var d in textDiffs) _output.WriteLine("text metric outside tolerance: " + d);
        Assert.True(failures.Count == 0, "Behaviour differs from WinForms:\n" + string.Join("\n", failures));
        Assert.True(textDiffs.Count == 0, "Text metrics differ from WinForms by more than " + TextTolerance + "px:\n" + string.Join("\n", textDiffs));
    }

    /// <summary>
    /// The font-table normalization on the pairs windows-latest produced (CRLF written as |): equal documents
    /// compare equal, a different face, charset or text does not.
    /// </summary>
    [Fact]
    public void RtfCompareIgnoresOnlyHowTheFontTableIsWritten()
    {
        static string R(string s) => NormalizeRtfFonts(s.Replace("|", "\r\n"));

        // The seven exact/rtb/* pairs of the first windows-latest run with the oracles (WinForms, NetForms).
        var pairs = new (string Key, string WinForms, string NetForms)[]
        {
            ("empty-rtf",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil Arial;}}|\viewkind4\uc1 |\pard\f0\fs20\par|}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}}|\viewkind4\uc1 |\pard\f0\fs20\par|}|"),
            ("rtf-bold-red",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}{\f1\fnil Arial;}}|{\colortbl ;\red255\green0\blue0;}|\viewkind4\uc1 |\pard\cf1\b\fs20 Hello\cf0\b0\f1\fs20  world\par|}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}}|{\colortbl ;\red255\green0\blue0;}|\viewkind4\uc1 |\pard\cf1\b\f0\fs20 Hello\cf0\b0\fs20  world\par|}|"),
            ("rtf-mixed",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}{\f1\fnil Arial;}}|{\colortbl ;\red255\green0\blue0;}|\viewkind4\uc1 |\pard\cf1\b\fs20 Hell\b0\fs24 o\cf0  w\f1\fs20 orld\par|}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}}|{\colortbl ;\red255\green0\blue0;}|\viewkind4\uc1 |\pard\cf1\b\f0\fs20 Hell\b0\fs24 o\cf0  w\fs20 orld\par|}|"),
            ("rtf-para",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil Arial;}{\f1\fnil\fcharset2 Symbol;}}|\viewkind4\uc1 |\pard{\pntext\f1\'B7\tab}{\*\pn\pnlvlblt\pnf1\pnindent0{\pntxtb\'B7}}\li450\ri150\tx600\tx1200\f0\fs20 onetwo\par||\pard three\par|}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}{\f1\fnil\fcharset2 Symbol;}}|\viewkind4\uc1 |\pard{\pntext\f1\'B7\tab}{\*\pn\pnlvlblt\pnf1\pnindent0{\pntxtb\'B7}}\li450\ri150\tx600\tx1200\f0\fs20 onetwo\par||\pard three\par|}|"),
            ("rtf-plain",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil Arial;}}|\viewkind4\uc1 |\pard\f0\fs20 one\par|two\par|three\par|four\par|}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}}|\viewkind4\uc1 |\pard\f0\fs20 one\par|two\par|three\par|four\par|}|"),
            ("rtf-props-order",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}{\f1\fnil Arial;}{\f2\fnil\fcharset2 Symbol;}}|{\colortbl ;\red255\green255\blue0;}|\viewkind4\uc1 |\pard\fi150\li150\ri75\qc\tx450\highlight1\ul\b\i\strike\protect\up6\fs20 one\highlight0\ulnone\b0\i0\strike0\protect0\up0\f1\fs20\par||\pard{\pntext\f2\'B7\tab}{\*\pn\pnlvlblt\pnf2\pnindent0{\pntxtb\'B7}}\qr two\par|}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}{\f1\fnil\fcharset2 Symbol;}}|{\colortbl ;\red255\green255\blue0;}|\viewkind4\uc1 |\pard\fi150\li150\ri75\qc\tx450\highlight1\ul\b\i\strike\protect\up6\f0\fs20 one\highlight0\ulnone\b0\i0\strike0\protect0\up0\fs20\par||\pard{\pntext\f1\'B7\tab}{\*\pn\pnlvlblt\pnf1\pnindent0{\pntxtb\'B7}}\qr two\par|}|"),
            ("selectedrtf",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}}|{\colortbl ;\red255\green0\blue0;}|\uc1 |\pard\cf1\b\fs20 el}|",
                @"{\rtf1\ansi\deff0\nouicompat{\fonttbl{\f0\fnil\fcharset Arial;}}|{\colortbl ;\red255\green0\blue0;}|\uc1 |\pard\cf1\b\f0\fs20 el}|"),
        };
        foreach (var (key, winForms, netForms) in pairs) Assert.True(R(winForms) == R(netForms), key);

        var boldRedNetForms = pairs[1].NetForms;
        // What still counts: another face, another charset, other text or formatting.
        Assert.NotEqual(R(boldRedNetForms), R(boldRedNetForms.Replace("Arial", "Calibri")));
        Assert.NotEqual(R(boldRedNetForms), R(boldRedNetForms.Replace("\\fcharset Arial", "\\fcharset204 Arial")));
        Assert.NotEqual(R(boldRedNetForms), R(boldRedNetForms.Replace("world", "World")));
        Assert.NotEqual(R(boldRedNetForms), R(boldRedNetForms.Replace("\\b0", "")));
        Assert.NotEqual(
            R(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Arial;}{\f1\fnil Calibri;}}\pard a\f1 b\par}"),
            R(@"{\rtf1\ansi\deff0{\fonttbl{\f0\fnil Arial;}{\f1\fnil Calibri;}}\pard a b\par}"));
    }

    private static bool IsRtf(string s) => s.StartsWith(@"{\rtf", StringComparison.Ordinal);

    private static readonly Regex s_fontEntry = new(@"\{\\f(\d+)((?:\\[a-z]+-?\d*\s?)*)([^;{}]*);\}", RegexOptions.Compiled);
    private static readonly Regex s_token = new(@"\\'[0-9a-fA-F]{2}|\\([a-z]+)(-?\d+)? ?|\\[^a-z]|[{}]|[^\\{}]+", RegexOptions.Compiled);

    /// <summary>
    /// The same RTF document with its font table reduced to what it means. RichEdit versions write the
    /// table differently: the one on the CI runners keeps a second entry for the same face (with and
    /// without a bare \fcharset) and switches between the two, the one the references were taken on
    /// keeps one (decision 133). Here a font is its name plus a non-default charset, the table is the set
    /// of those, every \fN / \pnfN names the font itself, and a switch to the font already in effect
    /// (tracked per group, starting from \deff) is dropped. Control words are rewritten with one
    /// delimiting space so dropping one cannot glue its neighbours. Everything else - text, colours,
    /// sizes, paragraph properties, a different face or charset - still has to match.
    /// </summary>
    internal static string NormalizeRtfFonts(string rtf)
    {
        var fonts = new Dictionary<string, string>(StringComparer.Ordinal);
        var tableStart = rtf.IndexOf(@"{\fonttbl", StringComparison.Ordinal);
        int tableEnd = -1;
        if (tableStart >= 0)
        {
            int depth = 0;
            for (int i = tableStart; i < rtf.Length; i++)
            {
                if (rtf[i] == '\\') { i++; continue; }
                if (rtf[i] == '{') depth++;
                else if (rtf[i] == '}' && --depth == 0) { tableEnd = i; break; }
            }
            foreach (Match m in s_fontEntry.Matches(rtf.Substring(tableStart, tableEnd - tableStart + 1)))
            {
                var charset = Regex.Match(m.Groups[2].Value, @"\\fcharset(\d+)");
                var cs = charset.Success && charset.Groups[1].Value != "0" ? "/" + charset.Groups[1].Value : "";
                fonts[m.Groups[1].Value] = m.Groups[3].Value.Trim() + cs;
            }
        }
        string Font(string n) => fonts.TryGetValue(n, out var f) ? f : "#" + n;

        var body = tableStart >= 0
            ? rtf.Substring(0, tableStart) + "{\\fonttbl " + string.Join(";", fonts.Values.Distinct().OrderBy(f => f, StringComparer.Ordinal)) + "}" + rtf.Substring(tableEnd + 1)
            : rtf;
        var deff = Regex.Match(rtf, @"\\deff(\d+)");
        var current = deff.Success ? Font(deff.Groups[1].Value) : "";
        var stack = new Stack<string>();
        var sb = new StringBuilder();
        foreach (Match m in s_token.Matches(body))
        {
            var word = m.Groups[1].Success ? m.Groups[1].Value : null;
            var arg = m.Groups[2].Value;
            if (m.Value == "{") { stack.Push(current); sb.Append('{'); }
            else if (m.Value == "}") { if (stack.Count > 0) current = stack.Pop(); sb.Append('}'); }
            else if (word == "f")
            {
                var f = Font(arg);
                if (f != current) { current = f; sb.Append(@"\f<").Append(f).Append("> "); }
            }
            else if (word is "deff" or "pnf") sb.Append('\\').Append(word).Append('<').Append(Font(arg)).Append("> ");
            else if (word == "plain") { current = deff.Success ? Font(deff.Groups[1].Value) : ""; sb.Append(@"\plain "); }
            else if (word != null) sb.Append('\\').Append(word).Append(arg).Append(' ');
            else sb.Append(m.Value);
        }
        return sb.ToString();
    }

    private static bool WithinTolerance(string expected, string actual, int tolerance)
    {
        var e = expected.Split(',');
        var a = actual.Split(',');
        if (e.Length != a.Length) return false;
        for (int i = 0; i < e.Length; i++)
        {
            if (!int.TryParse(e[i], out var ev) || !int.TryParse(a[i], out var av)) return e[i] == a[i];
            if (Math.Abs(ev - av) > tolerance) return false;
        }
        return true;
    }
}
