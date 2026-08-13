using System.CommandLine;
using Tracewright.Core;

namespace Tracewright.Cli;

/// <summary>
/// `tracewright emit claude` — hook JSON on stdin -> event. Hook context: never fail the session,
/// so every failure path here logs and returns 0 rather than throwing.
/// </summary>
public static class EmitClaudeCommand
{
    public static Command Build(string invocationTimestamp)
    {
        var command = new Command("claude", "record a Claude Code hook event from stdin");
        command.SetAction(_ => Run(invocationTimestamp));
        return command;
    }

    private static int Run(string invocationTimestamp)
    {
        try
        {
            var stdin = Console.In.ReadToEnd();
            var envelope = ClaudeHookAdapter.Build(stdin, Environment.CurrentDirectory, invocationTimestamp);
            new EventStore(DbPath.Resolve()).Append(envelope);
        }
        catch (Exception ex)
        {
            FailureLog.Append("emit-claude.log", ex);
        }

        return 0;
    }
}
