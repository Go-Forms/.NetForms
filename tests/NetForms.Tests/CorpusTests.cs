using System.Text;
using System.Text.Json;
using NetForms.Converter;
using Xunit;
using Xunit.Abstractions;
using static NetForms.Tests.DesignerCodeReaderTests;

namespace NetForms.Tests;

/// <summary>
/// Ф6.К: real WinForms projects, converted by the converter and built with <c>dotnet build</c> - the
/// customer's .NET Framework projects first, then open projects (tests/corpus/corpus.json, each at a
/// pinned commit). Every WinForms project of a repository is expected to build, unless the manifest
/// names it in <c>knownFailures</c> with the reason; a project that starts building is reported too, so
/// the list only shrinks. Needs the network (git): runs when NETFORMS_CORPUS=1. Clones are cached in
/// NETFORMS_CORPUS_DIR (default: the temp folder). The summary - the "builds without a hand edit"
/// metric - goes to render-out/corpus.md.
/// </summary>
public sealed class CorpusTests
{
    private readonly ITestOutputHelper _output;

    public CorpusTests(ITestOutputHelper output) => _output = output;

    public sealed record Entry(string Name, string Url, string Commit, string Group, string? License, Dictionary<string, string>? KnownFailures);

    private static List<Entry> Manifest() =>
        JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(Path.Combine(RepoRoot, "tests", "corpus", "corpus.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip })!;

    public static IEnumerable<object[]> Repositories() => Manifest().Select(e => new object[] { e.Name });

    private static bool Enabled => Environment.GetEnvironmentVariable("NETFORMS_CORPUS") == "1";

    [Theory]
    [MemberData(nameof(Repositories))]
    public void ARealProjectConvertsAndBuilds(string name)
    {
        if (!Enabled)
        {
            _output.WriteLine("NETFORMS_CORPUS is not 1: the corpus (network, minutes) is skipped.");
            return;
        }
        var entry = Manifest().Single(e => e.Name == name);
        var source = Checkout(entry);
        var work = Path.Combine(Path.GetTempPath(), "netforms-corpus-work", entry.Name);
        DeleteTree(work);
        CopyTree(source, work);

        // The whole solution first, then the builds: a WinForms project found before the library it references
        // (the order differs between file systems) would otherwise build against the library's old project.
        var winForms = new List<string>();
        foreach (var project in SolutionProjects.Find(work))
        {
            var report = new ProjectConverter().Apply(project, ConvertTarget.Cross, RepoRoot);
            if (report.UsesWindowsForms) winForms.Add(project);
        }
        var results = new List<(string Project, bool Built, string Errors)>();
        foreach (var project in winForms)
        {
            var (code, output) = Dotnet.Run(work, "build", project, "-nologo", "-v", "quiet", "-clp:NoSummary");
            var errors = string.Join("\n", output.Split('\n').Where(l => l.Contains(": error ")).Select(l => l.Trim()).Distinct().Take(10));
            // A failure without a compiler error (a crashed node, a locked file) keeps the end of the output.
            if (code != 0 && errors.Length == 0) errors = string.Join("\n", output.Split('\n').TakeLast(15));
            results.Add((Path.GetRelativePath(work, project).Replace('\\', '/'), code == 0, errors));
        }
        Summarize(entry, results);

        Assert.NotEmpty(results);
        var known = entry.KnownFailures ?? new Dictionary<string, string>();
        var unexpected = results.Where(r => !r.Built && !known.ContainsKey(r.Project)).ToList();
        var fixedNow = results.Where(r => r.Built && known.ContainsKey(r.Project)).Select(r => r.Project).ToList();
        Assert.True(unexpected.Count == 0, "Do not build:\n" + string.Join("\n\n", unexpected.Select(r => r.Project + "\n" + r.Errors)));
        Assert.True(fixedNow.Count == 0, "Build now - take them off knownFailures in corpus.json: " + string.Join(", ", fixedNow));
    }

    private static readonly object s_summaryLock = new();

    private void Summarize(Entry entry, List<(string Project, bool Built, string Errors)> results)
    {
        foreach (var r in results) _output.WriteLine($"{(r.Built ? "OK  " : "FAIL")} {r.Project}{(r.Built ? "" : "\n" + r.Errors)}");
        var outDir = Path.Combine(AppContext.BaseDirectory, "render-out");
        Directory.CreateDirectory(outDir);
        var line = new StringBuilder($"| {entry.Group} | [{entry.Name}]({entry.Url}) | {results.Count(r => r.Built)}/{results.Count} |");
        foreach (var r in results.Where(r => !r.Built))
            line.Append($" {r.Project}: {(entry.KnownFailures?.GetValueOrDefault(r.Project) ?? "unexpected")};");
        lock (s_summaryLock) File.AppendAllText(Path.Combine(outDir, "corpus.md"), line + Environment.NewLine);
    }

    /// <summary>The repository at its pinned commit, fetched once into the cache.</summary>
    private static string Checkout(Entry entry)
    {
        var cache = Environment.GetEnvironmentVariable("NETFORMS_CORPUS_DIR") is { Length: > 0 } dir ? dir : Path.Combine(Path.GetTempPath(), "netforms-corpus");
        var repo = Path.Combine(cache, entry.Name);
        if (Directory.Exists(repo) && Git(repo, "rev-parse", "HEAD").Output.Trim() == entry.Commit) return repo;
        DeleteTree(repo);
        Directory.CreateDirectory(repo);
        Assert.Equal(0, Git(repo, "init", "-q").Code);
        Assert.Equal(0, Git(repo, "fetch", "-q", "--depth", "1", entry.Url, entry.Commit).Code);
        Assert.Equal(0, Git(repo, "-c", "advice.detachedHead=false", "checkout", "-q", "FETCH_HEAD").Code);
        return repo;
    }

    private static (int Code, string Output) Git(string dir, params string[] args)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git") { WorkingDirectory = dir, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) info.ArgumentList.Add(a);
        using var p = System.Diagnostics.Process.Start(info)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var error = p.StandardError.ReadToEndAsync();
        p.WaitForExit();
        return (p.ExitCode, output.Result + error.Result);
    }

    /// <summary>Deletes a folder with its contents - read-only files too, which git's objects are on Windows.</summary>
    private static void DeleteTree(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(dir, recursive: true);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(from, f);
            if (rel.StartsWith(".git" + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
            var target = Path.Combine(to, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(f, target);
        }
    }
}
