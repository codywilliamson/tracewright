using Tracewright.Core.Primitives;
using Tracewright.Core.Adapters;
using Tracewright.Abstractions;

namespace Tracewright.Tests;

// shared envelope builder for the timeline test files — round-tripping through the store isn't
// needed here, projection tests build envelopes in memory and feed them straight to Core.
internal static class TimelineEnvelopeBuilder
{
    public static EventEnvelope Make(
        string occurredAt,
        string eventType = "claude.tool.succeeded",
        EvidenceKind kind = EvidenceKind.Observed,
        string emitterName = "claude-code",
        string? sessionId = null,
        string? agentId = null,
        string? repositoryId = null,
        string? eventId = null,
        string? originalEvent = null,
        string payload = "{}") => new()
    {
        EventId = eventId ?? Ulid.NewUlid(),
        OccurredAt = occurredAt,
        ReceivedAt = occurredAt,
        Kind = kind,
        EventType = eventType,
        EmitterName = emitterName,
        AdapterVersion = "0.1.0",
        OriginalEvent = originalEvent,
        SessionId = sessionId,
        AgentId = agentId,
        RepositoryId = repositoryId,
        Payload = payload,
    };
}
