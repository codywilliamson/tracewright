using System.Diagnostics;

namespace Tracewright.Core.Repositories;

/// <summary>
/// runs git and returns stdout, throwing on a nonzero exit. Callers decide what a failure
/// means: the commit adapter turns it into an exit-0 log line, init turns it into a user error.
/// </summary>
public static class GitCli
{
    public static string Run(string workingDirectory, params string[] args)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("failed to start git");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr.Trim()}");
        }

        return stdout;
    }
}
