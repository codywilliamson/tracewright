using Tracewright.Core.Repositories;

namespace Tracewright.Core.Onboarding;

/// <summary>
/// `tracewright init` (D-025): repository identity, the Claude Code hook block, and the git
/// post-commit hook. Explicit, idempotent, and non-destructive — it reports what it found
/// rather than overwriting it. Git hooks still never write into the working tree on their own.
/// </summary>
public static class RepositoryInitializer
{
    private const string RepoIdPath = ".tracewright/repo.id";
    private const string SettingsPath = ".claude/settings.json";

    public static IReadOnlyList<InitStep> Run(string workingDirectory)
    {
        var root = ResolveRepositoryRoot(workingDirectory);
        return [WriteRepositoryId(root), WriteClaudeSettings(root), PostCommitHook.Install(root)];
    }

    private static string ResolveRepositoryRoot(string workingDirectory)
    {
        try
        {
            return GitCli.Run(workingDirectory, "rev-parse", "--show-toplevel").Trim();
        }
        catch (InvalidOperationException)
        {
            throw new InvalidOperationException($"{workingDirectory} is not inside a git repository");
        }
    }

    // opaque unique string, not a ULID — identity needs stability, not sortability (D-022).
    private static InitStep WriteRepositoryId(string root)
    {
        var path = Path.Combine(root, ".tracewright", "repo.id");
        if (File.Exists(path))
        {
            return new InitStep(RepoIdPath, InitStepStatus.AlreadyPresent);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"{Guid.NewGuid()}\n");
        return new InitStep(RepoIdPath, InitStepStatus.Created, "commit this file");
    }

    private static InitStep WriteClaudeSettings(string root)
    {
        var path = Path.Combine(root, ".claude", "settings.json");
        var existing = File.Exists(path) ? File.ReadAllText(path) : null;

        var merged = ClaudeHookSettings.Merge(existing);
        if (merged is null)
        {
            return new InitStep(SettingsPath, InitStepStatus.AlreadyPresent);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, merged + "\n");
        return new InitStep(
            SettingsPath,
            existing is null ? InitStepStatus.Created : InitStepStatus.Updated,
            "commit this file");
    }
}
