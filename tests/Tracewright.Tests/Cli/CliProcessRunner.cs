using Tracewright.Cli.Infrastructure;
using System.Diagnostics;

namespace Tracewright.Tests;

// shared subprocess runner for CLI process-level tests (timeline, show) — same pattern
// EmitCliProcessTests uses, factored out so it isn't copy-pasted per file.
internal static class CliProcessRunner
{
    private static readonly string CliDllPath = typeof(Tracewright.Cli.Infrastructure.DbPath).Assembly.Location;

    public static (int ExitCode, string StdOut, string StdErr) Run(
        IReadOnlyList<string> args, string dbPath, string? workingDirectory = null, string stdin = "")
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory ?? Path.GetTempPath(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(CliDllPath);
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["TRACEWRIGHT_DB"] = dbPath;
        startInfo.Environment["NO_COLOR"] = "1";

        using var process = Process.Start(startInfo)!;
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
