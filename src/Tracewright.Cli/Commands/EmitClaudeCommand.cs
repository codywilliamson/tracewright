using Tracewright.Abstractions;
using Tracewright.Cli.Infrastructure;
using Tracewright.Core.Adapters;
using System.CommandLine;

namespace Tracewright.Cli.Commands;

/// <summary>
/// `tracewright emit claude` — hook JSON on stdin -> event. Hook context: never fail the session,
/// so every failure path here logs and returns 0 rather than throwing.
/// </summary>
public sealed class EmitClaudeCommand(IEventStore store)
{
    public Command Build(string invocationTimestamp)
    {
        var command = new Command("claude", "record a Claude Code hook event from stdin");
        command.SetAction(_ => Run(invocationTimestamp));
        return command;
    }

    private int Run(string invocationTimestamp)
    {
        try
        {
            var stdin = Console.In.ReadToEnd();
            var envelope = ClaudeHookAdapter.Build(stdin, Environment.CurrentDirectory, invocationTimestamp);
            store.Append(envelope);
        }
        catch (Exception ex)
        {
            FailureLog.Append("emit-claude.log", ex);
        }

        return 0;
    }
}
