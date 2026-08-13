using System.CommandLine;
using Tracewright.Core;

namespace Tracewright.Cli;

/// <summary>
/// `tracewright show <event-id-prefix>` — full envelope + verbatim payload (spec §10). Prefix
/// resolution via FindByIdPrefix; ambiguous or missing prefixes are user errors, not crashes.
/// </summary>
public static class ShowCommand
{
    public static Command Build()
    {
        var prefixArgument = new Argument<string>("event-id-prefix");

        var command = new Command("show", "show the full envelope for an event id (prefix ok)");
        command.Add(prefixArgument);
        command.SetAction(parseResult => Run(parseResult.GetValue(prefixArgument)!));
        return command;
    }

    private static int Run(string prefix)
    {
        var store = new EventStore(DbPath.Resolve());
        var matches = store.FindByIdPrefix(prefix);

        switch (matches.Count)
        {
            case 0:
                Console.Error.WriteLine($"error: no event found matching prefix '{prefix}'");
                return 1;
            case 1:
                ShowConsoleRenderer.Render(matches[0]);
                return 0;
            default:
                Console.Error.WriteLine(
                    $"error: ambiguous prefix '{prefix}' matches {matches.Count} events: " +
                    string.Join(", ", matches.Select(m => m.EventId)));
                return 1;
        }
    }
}
