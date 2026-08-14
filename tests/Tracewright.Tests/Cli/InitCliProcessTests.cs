using Tracewright.Core.Repositories;

namespace Tracewright.Tests;

public sealed class InitCliProcessTests : IDisposable
{
    private readonly string _repoDir;
    private readonly string _dbPath;

    public InitCliProcessTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        GitCli.Run(_repoDir, "init", "-b", "main");
        _dbPath = Path.Combine(_repoDir, "unused.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDir))
        {
            Directory.Delete(_repoDir, recursive: true);
        }
    }

    [Fact]
    public void Init_onboards_the_repository_it_runs_in()
    {
        var (exitCode, stdout, _) = CliProcessRunner.Run(["init"], _dbPath, _repoDir);

        Assert.Equal(0, exitCode);
        Assert.Contains("created   .tracewright/repo.id", stdout, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_repoDir, ".claude", "settings.json")));
        Assert.True(File.Exists(Path.Combine(_repoDir, ".git", "hooks", "post-commit")));
    }

    [Fact]
    public void Init_is_safe_to_run_again()
    {
        CliProcessRunner.Run(["init"], _dbPath, _repoDir);

        var (exitCode, stdout, _) = CliProcessRunner.Run(["init"], _dbPath, _repoDir);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("created", stdout, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_fails_outside_a_git_repository()
    {
        var plainDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(plainDir);
        try
        {
            var (exitCode, _, stderr) = CliProcessRunner.Run(["init"], _dbPath, plainDir);

            Assert.Equal(1, exitCode);
            Assert.Contains("not inside a git repository", stderr, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(plainDir, recursive: true);
        }
    }

    [Fact]
    public void Init_never_creates_the_ledger()
    {
        CliProcessRunner.Run(["init"], _dbPath, _repoDir);

        Assert.False(File.Exists(_dbPath));
    }
}
