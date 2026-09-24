using System.Diagnostics;

namespace NetForms.Tests;

/// <summary>
/// Runs the <c>dotnet</c> CLI from a test (templates, the converter, the corpus). Build servers are off:
/// an MSBuild node or the compiler server left running after the build inherits the redirected output,
/// so on Linux the output never ends and the test hangs reading it.
/// </summary>
internal static class Dotnet
{
    public static (int ExitCode, string Output) Run(string dir, params string[] args)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args) info.ArgumentList.Add(a);
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["DOTNET_NOLOGO"] = "1";
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        info.Environment["UseSharedCompilation"] = "false";
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, output.Result + error.Result);
    }

    /// <summary>Runs and asserts success, with the output in the failure message.</summary>
    public static void Succeed(string dir, params string[] args)
    {
        var (code, output) = Run(dir, args);
        Xunit.Assert.True(code == 0, $"dotnet {string.Join(' ', args)} failed:\n{output}");
    }
}
