using System.Text.Json;
using Tracewright.Core.Onboarding;
using Tracewright.Core.Repositories;

namespace Tracewright.Tests;

public sealed class RepositoryInitializerTests : IDisposable
{
    private const string EmitLine = "tracewright emit git post-commit";

    private readonly string _repoDir;

    public RepositoryInitializerTests()
    {
        _repoDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_repoDir);
        GitCli.Run(_repoDir, "init", "-b", "main");
    }

    public void Dispose()
    {
        if (Directory.Exists(_repoDir))
        {
            Directory.Delete(_repoDir, recursive: true);
        }
    }

    [Fact]
    public void Creates_repo_id_as_a_unique_string()
    {
        var steps = RepositoryInitializer.Run(_repoDir);

        var repoId = File.ReadAllText(RepoPath(".tracewright/repo.id")).Trim();
        Assert.True(Guid.TryParse(repoId, out _));
        Assert.Equal(InitStepStatus.Created, StatusOf(steps, ".tracewright/repo.id"));
    }

    [Fact]
    public void Never_regenerates_an_existing_repo_id()
    {
        Directory.CreateDirectory(RepoPath(".tracewright"));
        File.WriteAllText(RepoPath(".tracewright/repo.id"), "already-mine\n");

        var steps = RepositoryInitializer.Run(_repoDir);

        Assert.Equal("already-mine", File.ReadAllText(RepoPath(".tracewright/repo.id")).Trim());
        Assert.Equal(InitStepStatus.AlreadyPresent, StatusOf(steps, ".tracewright/repo.id"));
    }

    [Fact]
    public void Writes_a_hook_block_for_every_known_claude_event()
    {
        RepositoryInitializer.Run(_repoDir);

        var hooks = SettingsHooks();
        foreach (var hookEvent in ClaudeHookSettings.HookEvents)
        {
            Assert.True(hooks.TryGetProperty(hookEvent, out var groups), $"missing hook event {hookEvent}");
            var hook = groups.EnumerateArray().Single().GetProperty("hooks").EnumerateArray().Single();
            Assert.Equal("tracewright", hook.GetProperty("command").GetString());
            Assert.Equal(["emit", "claude"], hook.GetProperty("args").EnumerateArray().Select(a => a.GetString()));
            Assert.True(hook.GetProperty("async").GetBoolean());
        }
    }

    [Fact]
    public void Matches_all_tools_on_tool_hooks_only()
    {
        RepositoryInitializer.Run(_repoDir);

        var hooks = SettingsHooks();
        Assert.Equal("*", hooks.GetProperty("PreToolUse").EnumerateArray().Single().GetProperty("matcher").GetString());
        Assert.False(hooks.GetProperty("SessionStart").EnumerateArray().Single().TryGetProperty("matcher", out _));
    }

    [Fact]
    public void Preserves_unrelated_settings_and_foreign_hooks()
    {
        Directory.CreateDirectory(RepoPath(".claude"));
        File.WriteAllText(RepoPath(".claude/settings.json"), """
            {
              "model": "opus",
              "hooks": {
                "SessionStart": [{ "hooks": [{ "type": "command", "command": "somethingelse" }] }]
              }
            }
            """);

        var steps = RepositoryInitializer.Run(_repoDir);

        var root = JsonDocument.Parse(File.ReadAllText(RepoPath(".claude/settings.json"))).RootElement;
        Assert.Equal("opus", root.GetProperty("model").GetString());

        var sessionStart = root.GetProperty("hooks").GetProperty("SessionStart").EnumerateArray().ToList();
        Assert.Equal(2, sessionStart.Count);
        Assert.Equal("somethingelse", sessionStart[0].GetProperty("hooks").EnumerateArray().Single().GetProperty("command").GetString());
        Assert.Equal("tracewright", sessionStart[1].GetProperty("hooks").EnumerateArray().Single().GetProperty("command").GetString());
        Assert.Equal(InitStepStatus.Updated, StatusOf(steps, ".claude/settings.json"));
    }

    [Fact]
    public void Installs_a_post_commit_hook_that_cannot_fail_a_commit()
    {
        var steps = RepositoryInitializer.Run(_repoDir);

        var hook = File.ReadAllText(RepoPath(".git/hooks/post-commit"));
        Assert.Contains(EmitLine, hook, StringComparison.Ordinal);
        Assert.Contains("|| true", hook, StringComparison.Ordinal);
        Assert.Contains("exit 0", hook, StringComparison.Ordinal);
        Assert.Equal(InitStepStatus.Created, StatusOf(steps, ".git/hooks/post-commit"));
    }

    [Fact]
    public void Writes_the_hook_where_core_hooksPath_points()
    {
        GitCli.Run(_repoDir, "config", "core.hooksPath", ".githooks");

        RepositoryInitializer.Run(_repoDir);

        Assert.Contains(EmitLine, File.ReadAllText(RepoPath(".githooks/post-commit")), StringComparison.Ordinal);
        Assert.False(File.Exists(RepoPath(".git/hooks/post-commit")));
    }

    [Fact]
    public void Leaves_a_foreign_post_commit_hook_untouched()
    {
        var hookPath = RepoPath(".git/hooks/post-commit");
        File.WriteAllText(hookPath, "#!/bin/sh\ncommit-guard run\n");

        var steps = RepositoryInitializer.Run(_repoDir);

        Assert.Equal("#!/bin/sh\ncommit-guard run\n", File.ReadAllText(hookPath));
        Assert.Equal(InitStepStatus.Declined, StatusOf(steps, ".git/hooks/post-commit"));
        Assert.Contains(EmitLine, NoteOf(steps, ".git/hooks/post-commit"), StringComparison.Ordinal);
    }

    [Fact]
    public void Running_twice_changes_nothing_the_second_time()
    {
        RepositoryInitializer.Run(_repoDir);
        var before = Snapshot();

        var steps = RepositoryInitializer.Run(_repoDir);

        Assert.Equal(before, Snapshot());
        Assert.All(steps, step => Assert.Equal(InitStepStatus.AlreadyPresent, step.Status));
    }

    [Fact]
    public void Refuses_to_run_outside_a_git_repository()
    {
        var plainDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(plainDir);
        try
        {
            Assert.Throws<InvalidOperationException>(() => RepositoryInitializer.Run(plainDir));
            Assert.False(Directory.Exists(Path.Combine(plainDir, ".tracewright")));
        }
        finally
        {
            Directory.Delete(plainDir, recursive: true);
        }
    }

    [Fact]
    public void Initializes_from_a_subdirectory_into_the_repository_root()
    {
        var nested = RepoPath("src/deep");
        Directory.CreateDirectory(nested);

        RepositoryInitializer.Run(nested);

        Assert.True(File.Exists(RepoPath(".tracewright/repo.id")));
        Assert.False(Directory.Exists(Path.Combine(nested, ".tracewright")));
    }

    private string RepoPath(string relative) => Path.Combine(_repoDir, relative.Replace('/', Path.DirectorySeparatorChar));

    private JsonElement SettingsHooks() =>
        JsonDocument.Parse(File.ReadAllText(RepoPath(".claude/settings.json"))).RootElement.GetProperty("hooks");

    private string[] Snapshot() =>
        new[] { ".tracewright/repo.id", ".claude/settings.json", ".git/hooks/post-commit" }
            .Select(relative => File.ReadAllText(RepoPath(relative)))
            .ToArray();

    private static InitStepStatus StatusOf(IReadOnlyList<InitStep> steps, string target) =>
        steps.Single(step => step.Target == target).Status;

    private static string NoteOf(IReadOnlyList<InitStep> steps, string target) =>
        steps.Single(step => step.Target == target).Note ?? "";
}
