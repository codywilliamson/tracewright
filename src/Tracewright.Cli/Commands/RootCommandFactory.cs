using System.CommandLine;

namespace Tracewright.Cli.Commands;

/// <summary>
/// builds the whole command tree, so Program stays an entry point and the README
/// coverage test has something to walk.
/// </summary>
public sealed class RootCommandFactory(
    InitCommand init,
    EmitClaudeCommand emitClaude,
    EmitGitCommand emitGit,
    EmitRawCommand emitRaw,
    TimelineCommand timeline,
    ShowCommand show)
{
    public RootCommand Build(string invocationTimestamp)
    {
        var emit = new Command("emit", "record an event into the ledger");
        emit.Add(emitClaude.Build(invocationTimestamp));
        emit.Add(emitGit.Build());
        emit.Add(emitRaw.Build(invocationTimestamp));

        var root = new RootCommand("tracewright: local-first evidence ledger for agentic development");
        root.Add(init.Build());
        root.Add(emit);
        root.Add(timeline.Build());
        root.Add(show.Build());
        return root;
    }
}
