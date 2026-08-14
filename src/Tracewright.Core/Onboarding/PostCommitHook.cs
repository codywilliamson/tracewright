using Tracewright.Core.Repositories;

namespace Tracewright.Core.Onboarding;

/// <summary>
/// the `post-commit` hook (spec §6). Placement honours `core.hooksPath`; an existing foreign
/// hook is never rewritten — chaining is unsolved (decisions.md open list), and guessing at
/// someone else's hook is worse than telling them what to add.
/// </summary>
public static class PostCommitHook
{
    public const string EmitLine = "tracewright emit git post-commit";

    private const string Body = $"""
        #!/bin/sh
        # tracewright capture — must never break a commit (spec §6)
        {EmitLine} || true
        exit 0

        """;

    public static InitStep Install(string repositoryRoot)
    {
        var hooksDirectory = ResolveHooksDirectory(repositoryRoot);
        var hookPath = Path.Combine(hooksDirectory, "post-commit");
        var target = RelativePath(repositoryRoot, hookPath);

        if (File.Exists(hookPath))
        {
            return File.ReadAllText(hookPath).Contains(EmitLine, StringComparison.Ordinal)
                ? new InitStep(target, InitStepStatus.AlreadyPresent)
                : new InitStep(target, InitStepStatus.Declined,
                    $"another post-commit hook is installed — add `{EmitLine} || true` to it yourself");
        }

        Directory.CreateDirectory(hooksDirectory);
        File.WriteAllText(hookPath, Body);
        MakeExecutable(hookPath);
        return new InitStep(target, InitStepStatus.Created);
    }

    // --git-path resolves core.hooksPath for us, so commit-guard-style setups land correctly.
    private static string ResolveHooksDirectory(string repositoryRoot)
    {
        var path = GitCli.Run(repositoryRoot, "rev-parse", "--git-path", "hooks").Trim();
        return Path.IsPathRooted(path) ? path : Path.Combine(repositoryRoot, path);
    }

    private static void MakeExecutable(string hookPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(hookPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string RelativePath(string root, string fullPath) =>
        Path.GetRelativePath(root, fullPath).Replace(Path.DirectorySeparatorChar, '/');
}
