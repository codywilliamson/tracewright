namespace Tracewright.Core;

/// <summary>
/// read-side filters for EventStore.Query. a null filter is not applied (spec §7).
/// </summary>
public sealed record EventQuery
{
    public string? RepositoryId { get; init; }
    public string? SessionId { get; init; }
    public string? Since { get; init; }
    public string? Until { get; init; }
    public string? EventTypeGlob { get; init; }
    public EvidenceKind? Kind { get; init; }
}
