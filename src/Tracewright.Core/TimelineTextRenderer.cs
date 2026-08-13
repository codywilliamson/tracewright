namespace Tracewright.Core;

/// <summary>
/// plain-text rendering of a TimelineRenderModel (spec §7 example) — no color, no console
/// dependency, so the layout rules are testable without a terminal. Cli adds Spectre color on
/// top of the same TimelineLine fields.
/// </summary>
public static class TimelineTextRenderer
{
    public static IReadOnlyList<string> Render(TimelineRenderModel model)
    {
        var output = new List<string>();
        foreach (var line in model.Lines)
        {
            if (line.Header is not null)
            {
                if (output.Count > 0)
                {
                    output.Add("");
                }

                output.Add(line.Header);
            }

            output.AddRange(RenderLine(line));
        }

        return output;
    }

    private static IEnumerable<string> RenderLine(TimelineLine line)
    {
        var indent = line.IsSubagent ? "    " : "  ";
        var parts = new List<string> { line.LocalTime, line.KindText, line.EventType };

        if (line.IsSubagent)
        {
            parts.Add($"agent={line.AgentType ?? "subagent"}");
        }

        if (line.Summary.Length > 0)
        {
            parts.Add(line.Summary);
        }

        if (line.CorrelationNote is null)
        {
            parts.Add($"[{line.ShortEventId}]");
            yield return indent + string.Join("  ", parts);
            yield break;
        }

        yield return indent + string.Join("  ", parts);
        yield return indent + $"{line.CorrelationNote}  [{line.ShortEventId}]";
    }
}
