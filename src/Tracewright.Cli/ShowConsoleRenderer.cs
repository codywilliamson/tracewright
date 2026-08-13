using Spectre.Console;
using Tracewright.Core;

namespace Tracewright.Cli;

/// <summary>
/// Spectre.Console rendering for `tracewright show` — thin wrapper over EnvelopeTextRenderer.
/// </summary>
public static class ShowConsoleRenderer
{
    public static void Render(EventEnvelope envelope)
    {
        foreach (var line in EnvelopeTextRenderer.Render(envelope))
        {
            AnsiConsole.MarkupLine(Markup.Escape(line));
        }
    }
}
