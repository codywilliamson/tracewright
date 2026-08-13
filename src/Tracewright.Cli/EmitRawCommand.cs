using System.CommandLine;
using Tracewright.Core;

namespace Tracewright.Cli;

/// <summary>
/// `tracewright emit raw` — envelope JSON on stdin (spec §10, adapter tracewright.raw, D-015).
/// Human-facing: unlike the hook commands, validation errors print to stderr and exit nonzero.
/// </summary>
public static class EmitRawCommand
{
    public static Command Build(string invocationTimestamp)
    {
        var kindOption = new Option<string>("--kind") { DefaultValueFactory = _ => "asserted" };

        var command = new Command("raw", "record an envelope JSON from stdin");
        command.Add(kindOption);
        command.SetAction(parseResult => Run(parseResult.GetValue(kindOption), invocationTimestamp));
        return command;
    }

    private static int Run(string? kindFlag, string invocationTimestamp)
    {
        try
        {
            var stdin = Console.In.ReadToEnd();
            var envelope = RawEventAdapter.Build(stdin, kindFlag, invocationTimestamp);
            new EventStore(DbPath.Resolve()).Append(envelope);
            Console.WriteLine(envelope.EventId);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}
