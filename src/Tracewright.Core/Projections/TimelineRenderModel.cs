namespace Tracewright.Core.Projections;

/// <summary>
/// the timeline projection's output — a flat chronological sequence of lines, nothing stored or
/// derived persistently (spec §7).
/// </summary>
public sealed record TimelineRenderModel
{
    public required IReadOnlyList<TimelineLine> Lines { get; init; }
}
