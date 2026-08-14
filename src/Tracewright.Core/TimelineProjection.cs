using Tracewright.Abstractions;
using System.Globalization;

namespace Tracewright.Core;

/// <summary>
/// builds the timeline render model from EventStore.Query results (spec §7). Never reorders —
/// the store's chronology IS the order (D-017). Headers reprint whenever the stream switches:
/// session_id wins when present, else repository_id, else the events are unanchored (spec §8).
/// </summary>
public static class TimelineProjection
{
    public static TimelineRenderModel Build(IReadOnlyList<EventEnvelope> events)
    {
        var lines = new List<TimelineLine>(events.Count);
        string? previousStreamKey = null;

        foreach (var envelope in events)
        {
            var streamKey = StreamKey(envelope);
            string? header = null;
            if (streamKey != previousStreamKey)
            {
                header = Header(envelope);
                previousStreamKey = streamKey;
            }

            lines.Add(BuildLine(envelope, header));
        }

        return new TimelineRenderModel { Lines = lines };
    }

    // --session filters by correlation; when unattributed events share the window, say so
    // rather than silently narrowing the view (spec §8).
    public static string? FormatUnattributedNote(int unattributedCount) =>
        unattributedCount > 0
            ? $"{unattributedCount} unattributed events in this window — run without --session to see them"
            : null;

    private static string StreamKey(EventEnvelope envelope) =>
        envelope.SessionId is not null ? $"session:{envelope.SessionId}"
        : envelope.RepositoryId is not null ? $"repository:{envelope.RepositoryId}"
        : "unanchored";

    private static string Header(EventEnvelope envelope)
    {
        if (envelope.SessionId is not null)
        {
            return $"session {ShortSessionId(envelope.SessionId)} ({envelope.EmitterName})";
        }

        return envelope.RepositoryId is not null
            ? $"repository {envelope.RepositoryId}"
            : "unanchored";
    }

    private static string ShortSessionId(string sessionId) =>
        sessionId.Length <= 8 ? sessionId : sessionId[..8];

    private static TimelineLine BuildLine(EventEnvelope envelope, string? header)
    {
        var isSubagent = envelope.AgentId is not null;
        var isUnattributedCommit = envelope.EventType == "git.commit" && envelope.SessionId is null;

        return new TimelineLine
        {
            Header = header,
            LocalTime = ToLocalTime(envelope.OccurredAt),
            KindText = envelope.Kind.ToText().ToUpperInvariant(),
            EventType = envelope.EventType,
            Summary = TimelineSummary.For(envelope),
            ShortEventId = ShortEventId.Of(envelope.EventId),
            IsSubagent = isSubagent,
            AgentType = isSubagent ? TimelineSummary.AgentType(envelope) : null,
            CorrelationNote = isUnattributedCommit ? "correlation: none — unattributed" : null,
        };
    }

    private static string ToLocalTime(string occurredAt) =>
        DateTimeOffset.Parse(occurredAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            .ToLocalTime()
            .ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
