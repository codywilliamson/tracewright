using Spectre.Console;
using Tracewright.Core;

namespace Tracewright.Cli;

/// <summary>
/// Spectre.Console rendering of a TimelineRenderModel — a thin coloring layer over the same
/// fields TimelineTextRenderer lays out in plain text (spec §7 rule 10: correctness and
/// grep-ability over decoration).
/// </summary>
public static class TimelineConsoleRenderer
{
    public static void Render(TimelineRenderModel model)
    {
        var printedHeader = false;
        foreach (var line in model.Lines)
        {
            if (line.Header is not null)
            {
                if (printedHeader)
                {
                    AnsiConsole.WriteLine();
                }

                AnsiConsole.MarkupLine($"[bold]{Markup.Escape(line.Header)}[/]");
                printedHeader = true;
            }

            RenderLine(line);
        }
    }

    private static void RenderLine(TimelineLine line)
    {
        var indent = line.IsSubagent ? "    " : "  ";
        var parts = new List<string> { line.LocalTime, $"[{KindColor(line.KindText)}]{line.KindText}[/]", Markup.Escape(line.EventType) };

        if (line.IsSubagent)
        {
            parts.Add(Markup.Escape($"agent={line.AgentType ?? "subagent"}"));
        }

        if (line.Summary.Length > 0)
        {
            parts.Add(Markup.Escape(line.Summary));
        }

        if (line.CorrelationNote is null)
        {
            parts.Add(DimBrackets(line.ShortEventId));
            AnsiConsole.MarkupLine(indent + string.Join("  ", parts));
            return;
        }

        AnsiConsole.MarkupLine(indent + string.Join("  ", parts));
        AnsiConsole.MarkupLine(indent + $"{Markup.Escape(line.CorrelationNote)}  {DimBrackets(line.ShortEventId)}");
    }

    private static string DimBrackets(string shortEventId) =>
        $"[dim]{Markup.Escape($"[{shortEventId}]")}[/]";

    private static string KindColor(string kindText) => kindText switch
    {
        "OBSERVED" => "blue",
        "ASSERTED" => "yellow",
        "DERIVED" => "magenta",
        _ => "grey",
    };
}
