using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Tracewright.Core;

/// <summary>
/// `tracewright emit git post-commit` translation: queries git directly rather than trusting a
/// relayed payload (spec §6). Hook context: never break a commit — this type throws on any git
/// failure (e.g. run outside a repo) and leaves exit-0-on-error to the CLI command that calls it.
/// One firing = one event; no dedup, no lineage (D-023).
/// </summary>
public static class GitCommitAdapter
{
    public const string EmitterName = "git";
    private const string EventType = "git.commit";
    private const string EnvHintPrefix = "CLAUDE";
    private const char FieldSeparator = '\x1f';

    public static EventEnvelope Build(string cwd, IReadOnlyDictionary<string, string?> environmentVariables)
    {
        var fields = RunGit(cwd, "log", "-1",
                $"--format=%H{FieldSeparator}%P{FieldSeparator}%an{FieldSeparator}%ae{FieldSeparator}%aI{FieldSeparator}%cI{FieldSeparator}%s",
                "HEAD")
            .TrimEnd('\n')
            .Split(FieldSeparator);

        var sha = fields[0];
        var parents = fields[1].Length == 0 ? [] : fields[1].Split(' ');
        var authorName = fields[2];
        var authorEmail = fields[3];
        var authorDate = fields[4];
        var committerDate = fields[5];
        var subject = fields[6];

        var branch = RunGit(cwd, "rev-parse", "--abbrev-ref", "HEAD").Trim();
        var worktreeId = RunGit(cwd, "rev-parse", "--show-toplevel").Trim();
        var gitVersion = RunGit(cwd, "--version").Trim();
        var files = ParseNameStatus(RunGit(cwd, "diff-tree", "--no-commit-id", "--name-status", "-r", "--root", sha));
        var envHints = CollectEnvHints(environmentVariables);

        var payload = JsonSerializer.Serialize(new
        {
            sha,
            parents,
            branch,
            author_name = authorName,
            author_email = authorEmail,
            author_date = authorDate,
            committer_date = committerDate,
            subject,
            files,
            env_hints = envHints,
        });

        return new EventEnvelope
        {
            EventId = Ulid.NewUlid(),
            OccurredAt = Timestamp.Format(DateTimeOffset.Parse(committerDate, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)),
            ReceivedAt = Timestamp.Now(),
            Kind = EvidenceKind.Observed,
            EventType = EventType,
            EmitterName = EmitterName,
            EmitterVersion = gitVersion,
            AdapterVersion = AdapterVersion.Current,
            OriginalEvent = "post-commit",
            RepositoryId = RepositoryResolver.Resolve(cwd),
            WorktreeId = worktreeId,
            Payload = payload,
        };
    }

    private static List<Dictionary<string, string>> ParseNameStatus(string nameStatusOutput) =>
        nameStatusOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 2))
            .Select(parts => new Dictionary<string, string> { ["status"] = parts[0], ["path"] = parts[1] })
            .ToList();

    private static Dictionary<string, string> CollectEnvHints(IReadOnlyDictionary<string, string?> environmentVariables) =>
        environmentVariables
            .Where(kv => kv.Key.StartsWith(EnvHintPrefix, StringComparison.Ordinal) && kv.Value is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!);

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
