namespace Tracewright.Core.Projections;

/// <summary>
/// one rendered timeline entry — structured, so Cli can add color without re-deriving layout
/// (spec §7 rule 2). A non-null Header means the stream switched and must reprint (D-017).
/// </summary>
public sealed record TimelineLine
{
    public string? Header { get; init; }
    public required string LocalTime { get; init; }
    public required string KindText { get; init; }
    public required string EventType { get; init; }
    public required string Summary { get; init; }
    public required string ShortEventId { get; init; }
    public bool IsSubagent { get; init; }
    public string? AgentType { get; init; }
    public string? CorrelationNote { get; init; }
}
