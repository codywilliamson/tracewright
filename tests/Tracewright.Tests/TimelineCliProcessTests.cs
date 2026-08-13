using Tracewright.Core;
using static Tracewright.Tests.TimelineEnvelopeBuilder;

namespace Tracewright.Tests;

/// <summary>
/// process-level tests for `tracewright timeline` — the pieces that depend on real argument
/// parsing, exit codes, and the default 24h window (spec §7). Projection/rendering correctness
/// itself is covered directly against Core in TimelineProjectionTests/TimelineTextRendererTests.
/// </summary>
public sealed class TimelineCliProcessTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public TimelineCliProcessTests()
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
    public void Empty_store_prints_no_events_recorded_and_exits_zero()
    {
        var result = CliProcessRunner.Run(["timeline"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("no events recorded", result.StdOut);
    }

    [Fact]
    public void No_filters_defaults_to_a_24h_window()
    {
        var store = new EventStore(_dbPath);
        var now = DateTimeOffset.UtcNow;
        store.Append(Make(Timestamp.Format(now.AddHours(-1)), sessionId: "recent"));
        store.Append(Make(Timestamp.Format(now.AddDays(-10)), sessionId: "old"));

        var result = CliProcessRunner.Run(["timeline"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("recent", result.StdOut);
        Assert.DoesNotContain("old", result.StdOut);
    }

    [Fact]
    public void Any_filter_disables_the_default_window()
    {
        var store = new EventStore(_dbPath);
        var now = DateTimeOffset.UtcNow;
        store.Append(Make(Timestamp.Format(now.AddDays(-10)), sessionId: "old"));

        var result = CliProcessRunner.Run(["timeline", "--type", "*"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("old", result.StdOut);
    }

    [Fact]
    public void Bare_repo_narrows_to_the_repository_resolved_from_cwd()
    {
        var store = new EventStore(_dbPath);
        var now = DateTimeOffset.UtcNow;
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1)), repositoryId: "repo-here"));
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1)), repositoryId: "repo-elsewhere"));

        var repoDir = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(Path.Combine(repoDir, ".tracewright"));
        File.WriteAllText(Path.Combine(repoDir, ".tracewright", "repo.id"), "repo-here");

        var result = CliProcessRunner.Run(["timeline", "--repo"], _dbPath, workingDirectory: repoDir);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("repo-here", result.StdOut);
        Assert.DoesNotContain("repo-elsewhere", result.StdOut);
    }

    [Fact]
    public void Explicit_repo_value_is_used_as_is()
    {
        var store = new EventStore(_dbPath);
        var now = DateTimeOffset.UtcNow;
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1)), repositoryId: "repo-a"));
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1)), repositoryId: "repo-b"));

        var result = CliProcessRunner.Run(["timeline", "--repo", "repo-b"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("repo-b", result.StdOut);
        Assert.DoesNotContain("repo-a", result.StdOut);
    }

    [Fact]
    public void Bare_repo_outside_any_onboarded_repository_errors_and_exits_nonzero()
    {
        var result = CliProcessRunner.Run(["timeline", "--repo"], _dbPath, workingDirectory: _tempDir);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("no repository found", result.StdErr);
    }

    [Fact]
    public void Session_filter_appends_a_trailing_note_when_unattributed_events_share_the_window()
    {
        var store = new EventStore(_dbPath);
        var now = DateTimeOffset.UtcNow;
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1)), sessionId: "s1"));
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1))));

        var result = CliProcessRunner.Run(["timeline", "--session", "s1"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("unattributed events in this window", result.StdOut);
    }

    [Fact]
    public void Session_filter_omits_the_note_when_no_unattributed_events_share_the_window()
    {
        var store = new EventStore(_dbPath);
        var now = DateTimeOffset.UtcNow;
        store.Append(Make(Timestamp.Format(now.AddMinutes(-1)), sessionId: "s1"));

        var result = CliProcessRunner.Run(["timeline", "--session", "s1"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("unattributed events", result.StdOut);
    }

    [Fact]
    public void Invalid_kind_errors_and_exits_nonzero()
    {
        var result = CliProcessRunner.Run(["timeline", "--kind", "bogus"], _dbPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.StdErr);
    }

    [Fact]
    public void Invalid_since_errors_and_exits_nonzero()
    {
        var result = CliProcessRunner.Run(["timeline", "--since", "not-a-time"], _dbPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.StdErr);
    }
}
