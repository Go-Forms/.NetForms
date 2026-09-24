using System;
using System.IO;
using System.Linq;

namespace System.Drawing;

/// <summary>
/// Reads a file path the way Windows does, for the APIs of NetForms that open files (Ф6, decision 112).
/// WinForms code is written against Windows: <c>Application.StartupPath + @"\Picture\closed.png"</c>, a
/// file named <c>Closed.png</c> loaded as <c>closed.png</c>. On Windows nothing changes. Elsewhere, when
/// the path as given names no file, a backslash is taken as a separator and each name is matched
/// ignoring case - what the program meant on the system it was written for. Only NetForms' own APIs do
/// this; <c>File.ReadAllText</c> and the rest of the BCL do not, and the converter reports such paths.
/// </summary>
internal static class WindowsPath
{
    /// <summary>The existing file the path means, or the path with its separators fixed when there is none.</summary>
    public static string ForReading(string path)
    {
        if (OperatingSystem.IsWindows() || path.Length == 0 || File.Exists(path)) return path;
        var slashed = path.Replace('\\', '/');
        if (File.Exists(slashed)) return slashed;
        return MatchIgnoringCase(slashed) ?? slashed;
    }

    /// <summary>A path to write to: separators fixed, and an existing folder found ignoring case.</summary>
    public static string ForWriting(string path)
    {
        if (OperatingSystem.IsWindows() || path.Length == 0) return path;
        var slashed = path.Replace('\\', '/');
        var dir = Path.GetDirectoryName(slashed);
        if (string.IsNullOrEmpty(dir) || Directory.Exists(dir)) return slashed;
        var found = MatchIgnoringCase(dir, directory: true);
        return found == null ? slashed : Path.Combine(found, Path.GetFileName(slashed));
    }

    private static string? MatchIgnoringCase(string path, bool directory = false)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full) ?? "/";
        var current = root;
        var parts = full.Substring(root.Length).Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            bool last = i == parts.Length - 1;
            var exact = Path.Combine(current, parts[i]);
            if (last ? (directory ? Directory.Exists(exact) : File.Exists(exact)) : Directory.Exists(exact)) { current = exact; continue; }
            if (!Directory.Exists(current)) return null;
            var entries = last && !directory ? Directory.EnumerateFiles(current) : Directory.EnumerateDirectories(current);
            var match = entries.FirstOrDefault(e => string.Equals(Path.GetFileName(e), parts[i], StringComparison.OrdinalIgnoreCase));
            if (match == null) return null;
            current = match;
        }
        return current;
    }
}
