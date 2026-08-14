using Tracewright.Abstractions;
using Tracewright.Cli.Infrastructure;
using Tracewright.Core.Adapters;
using System.Collections;
using System.CommandLine;

namespace Tracewright.Cli.Commands;

/// <summary>
/// `tracewright emit git post-commit` — queries git directly, emits git.commit (spec §6). Hook
/// context: always exits 0; a broken Tracewright must never break a commit.
/// </summary>
public sealed class EmitGitCommand(IEventStore store)
{
    public Command Build()
    {
        var postCommit = new Command("post-commit", "record the current HEAD commit as a git.commit event");
        postCommit.SetAction(_ => Run());

        var git = new Command("git", "git-sourced events");
        git.Add(postCommit);
        return git;
    }

    private int Run()
    {
        try
        {
            var envelope = GitCommitAdapter.Build(Environment.CurrentDirectory, CurrentEnvironment());
            store.Append(envelope);
        }
        catch (Exception ex)
        {
            FailureLog.Append("emit-git.log", ex);
        }

        return 0;
    }

    private static Dictionary<string, string?> CurrentEnvironment() =>
        Environment.GetEnvironmentVariables()
            .Cast<DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value);
}
