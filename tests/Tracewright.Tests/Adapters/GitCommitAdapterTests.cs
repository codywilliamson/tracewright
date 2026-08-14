using Tracewright.Core.Primitives;
using Tracewright.Core.Adapters;
using Tracewright.Abstractions;
using System.Diagnostics;
using System.Text.Json;

namespace Tracewright.Tests;

public sealed class GitCommitAdapterTests : IDisposable
{
    private readonly string _repoDir;

    public GitCommitAdapterTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        RunGit(_repoDir, "init", "-b", "main");
        RunGit(_repoDir, "config", "user.name", "Test User");
        RunGit(_repoDir, "config", "user.email", "test@example.com");
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDir))
        {
            Directory.Delete(_repoDir, recursive: true);
        }
    }

    [Fact]
    public void Build_captures_sha_subject_and_no_parents_for_root_commit()
    {
        File.WriteAllText(Path.Combine(_repoDir, "a.txt"), "hello");
        RunGit(_repoDir, "add", "a.txt");
        RunGit(_repoDir, "commit", "-m", "first commit");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        var expectedSha = RunGit(_repoDir, "rev-parse", "HEAD").Trim();
        Assert.Equal("git.commit", envelope.EventType);
        Assert.Equal(EvidenceKind.Observed, envelope.Kind);
        Assert.Equal("git", envelope.EmitterName);

        var payload = JsonDocument.Parse(envelope.Payload).RootElement;
        Assert.Equal(expectedSha, payload.GetProperty("sha").GetString());
        Assert.Empty(payload.GetProperty("parents").EnumerateArray());
        Assert.Equal("first commit", payload.GetProperty("subject").GetString());
    }

    [Fact]
    public void Build_captures_branch_and_worktree_id()
    {
        CommitFile("a.txt", "hello", "first commit");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        var payload = JsonDocument.Parse(envelope.Payload).RootElement;
        Assert.Equal("main", payload.GetProperty("branch").GetString());

        var expectedToplevel = RunGit(_repoDir, "rev-parse", "--show-toplevel").Trim();
        Assert.Equal(expectedToplevel, envelope.WorktreeId);
    }

    [Fact]
    public void Build_captures_name_status_file_list()
    {
        CommitFile("a.txt", "hello", "add a");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        var payload = JsonDocument.Parse(envelope.Payload).RootElement;
        var files = payload.GetProperty("files").EnumerateArray().ToList();
        var file = Assert.Single(files);
        Assert.Equal("A", file.GetProperty("status").GetString());
        Assert.Equal("a.txt", file.GetProperty("path").GetString());
    }

    [Fact]
    public void Build_captures_parent_sha_on_second_commit()
    {
        CommitFile("a.txt", "hello", "first");
        var firstSha = RunGit(_repoDir, "rev-parse", "HEAD").Trim();
        CommitFile("b.txt", "world", "second");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        var payload = JsonDocument.Parse(envelope.Payload).RootElement;
        var parents = payload.GetProperty("parents").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal([firstSha], parents);
    }

    [Fact]
    public void Build_sets_occurred_at_from_committer_timestamp_not_invocation_time()
    {
        CommitFile("a.txt", "hello", "first");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        var committerIso = RunGit(_repoDir, "log", "-1", "--format=%cI").Trim();
        var expected = Timestamp.Format(DateTimeOffset.Parse(committerIso));
        Assert.Equal(expected, envelope.OccurredAt);
    }

    [Fact]
    public void Build_captures_claude_prefixed_env_vars_into_env_hints()
    {
        CommitFile("a.txt", "hello", "first");
        var env = new Dictionary<string, string?>
        {
            ["CLAUDE_PROJECT_DIR"] = "/repo",
            ["CLAUDE_SESSION_ID"] = "sess-1",
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/home/u",
        };

        var envelope = GitCommitAdapter.Build(_repoDir, env);

        var payload = JsonDocument.Parse(envelope.Payload).RootElement;
        var hints = payload.GetProperty("env_hints");
        Assert.Equal("/repo", hints.GetProperty("CLAUDE_PROJECT_DIR").GetString());
        Assert.Equal("sess-1", hints.GetProperty("CLAUDE_SESSION_ID").GetString());
        Assert.False(hints.TryGetProperty("PATH", out _));
        Assert.False(hints.TryGetProperty("HOME", out _));
    }

    [Fact]
    public void Build_resolves_repository_id_from_marker()
    {
        Directory.CreateDirectory(Path.Combine(_repoDir, ".tracewright"));
        File.WriteAllText(Path.Combine(_repoDir, ".tracewright", "repo.id"), "repo-id-123");
        CommitFile("a.txt", "hello", "first");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        Assert.Equal("repo-id-123", envelope.RepositoryId);
    }

    [Fact]
    public void Build_captures_git_version_as_emitter_version()
    {
        CommitFile("a.txt", "hello", "first");

        var envelope = GitCommitAdapter.Build(_repoDir, new Dictionary<string, string?>());

        Assert.False(string.IsNullOrWhiteSpace(envelope.EmitterVersion));
        Assert.Contains("git version", envelope.EmitterVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_throws_when_run_outside_a_git_repository()
    {
        var nonRepoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(nonRepoDir);
        try
        {
            Assert.ThrowsAny<Exception>(() => GitCommitAdapter.Build(nonRepoDir, new Dictionary<string, string?>()));
        }
        finally
        {
            Directory.Delete(nonRepoDir, recursive: true);
        }
    }

    private void CommitFile(string relativePath, string content, string message)
    {
        File.WriteAllText(Path.Combine(_repoDir, relativePath), content);
        RunGit(_repoDir, "add", relativePath);
        RunGit(_repoDir, "commit", "-m", message);
    }

    private static string RunGit(string workingDirectory, params string[] args)
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

        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
        }

        return stdout;
    }
}
