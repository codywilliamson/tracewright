namespace Tracewright.Abstractions;

/// <summary>
/// one event = one row (spec §1). immutable — no update or delete code path exists anywhere.
/// </summary>
public sealed record EventEnvelope
{
    public required string EventId { get; init; }
    public required string OccurredAt { get; init; }
    public required string ReceivedAt { get; init; }
    public required EvidenceKind Kind { get; init; }
    public required string EventType { get; init; }
    public required string EmitterName { get; init; }
    public string? EmitterVersion { get; init; }
    public required string AdapterVersion { get; init; }
    public string? OriginalEvent { get; init; }
    public string? SessionId { get; init; }
    public string? PromptId { get; init; }
    public string? ToolUseId { get; init; }
    public string? AgentId { get; init; }
    public string? ParentId { get; init; }
    public string? RepositoryId { get; init; }
    public string? WorktreeId { get; init; }
    public string? RawRef { get; init; }
    public required string Payload { get; init; }
}
