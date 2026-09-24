using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetForms.Converter;

/// <summary>The C# projects a command-line argument names: a project, a solution (.sln, .slnx), or a folder.</summary>
public static class SolutionProjects
{
    public static List<string> Find(string argument)
    {
        if (File.Exists(argument))
        {
            var full = Path.GetFullPath(argument);
            return Path.GetExtension(full).ToLowerInvariant() switch
            {
                ".csproj" => new List<string> { full },
                ".sln" or ".slnx" => FromSolution(full),
                _ => throw new IOException($"{argument} is not a C# project or a solution."),
            };
        }
        if (!Directory.Exists(argument)) throw new IOException($"{argument} does not exist.");

        var dir = Path.GetFullPath(argument);
        var top = Directory.GetFiles(dir, "*.csproj");
        if (top.Length == 1) return new List<string> { top[0] };
        var solutions = Directory.GetFiles(dir, "*.sln").Concat(Directory.GetFiles(dir, "*.slnx")).ToArray();
        if (solutions.Length == 1) return FromSolution(solutions[0]);
        var all = Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !Regex.IsMatch(Path.GetRelativePath(dir, p).Replace('\\', '/'), @"(^|/)(bin|obj)/", RegexOptions.IgnoreCase))
            .OrderBy(p => p, StringComparer.Ordinal).ToList();
        if (all.Count == 0) throw new IOException($"{argument} has no C# project.");
        return all;
    }

    /// <summary>The C# projects of a solution that exist on disk (a .sln writes Windows separators).</summary>
    public static List<string> FromSolution(string solution)
    {
        var dir = Path.GetDirectoryName(solution)!;
        IEnumerable<string> paths = solution.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? XDocument.Load(solution).Descendants().Where(e => e.Name.LocalName == "Project").Select(e => (string?)e.Attribute("Path") ?? "")
            : Regex.Matches(File.ReadAllText(solution), @"^Project\(""\{[^}]+\}""\)\s*=\s*""[^""]*"",\s*""([^""]+)""", RegexOptions.Multiline).Select(m => m.Groups[1].Value);
        return paths.Where(p => p.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFullPath(Path.Combine(dir, p.Replace('\\', '/'))))
            .Where(File.Exists).Distinct().ToList();
    }
}
