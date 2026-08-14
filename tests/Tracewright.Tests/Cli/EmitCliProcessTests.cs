using Tracewright.Cli.Infrastructure;
using System.Diagnostics;

namespace Tracewright.Tests;

/// <summary>
/// drives the built CLI as a subprocess for the handful of assertions that are really about
/// process exit codes (spec §9): hook commands always exit 0, `emit raw` exits nonzero on
/// caller error. Everything else is covered by calling the adapters directly.
/// </summary>
public sealed class EmitCliProcessTests : IDisposable
{
    private static readonly string CliDllPath = typeof(DbPath).Assembly.Location;

    private readonly string _tempDir;
    private readonly string _dbPath;

    public EmitCliProcessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "tracewright.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Emit_claude_exits_zero_on_malformed_stdin()
    {
        var result = Run("emit claude", stdin: "{not valid json", workingDirectory: _tempDir);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Emit_git_post_commit_exits_zero_outside_a_git_repository()
    {
        var result = Run("emit git post-commit", stdin: "", workingDirectory: _tempDir);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Emit_raw_exits_nonzero_and_writes_stderr_on_invalid_input()
    {
        var result = Run("emit raw", stdin: "not json", workingDirectory: _tempDir);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.StdErr);
    }

    [Fact]
    public void Emit_raw_exits_zero_and_prints_event_id_on_success()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{"text":"hi"}}""";

        var result = Run("emit raw", stdin, workingDirectory: _tempDir);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(26, result.StdOut.Trim().Length);
    }

    private (int ExitCode, string StdOut, string StdErr) Run(string arguments, string stdin, string workingDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(CliDllPath);
        foreach (var arg in arguments.Split(' '))
        {
            startInfo.ArgumentList.Add(arg);
        }

        startInfo.Environment["TRACEWRIGHT_DB"] = _dbPath;

        using var process = Process.Start(startInfo)!;
        process.StandardInput.Write(stdin);
        process.StandardInput.Close();

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout, stderr);
    }
}
