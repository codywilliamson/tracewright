using Tracewright.Abstractions;
using System.Text.Json;

namespace Tracewright.Core;

/// <summary>
/// plain-text rendering of a full envelope for `tracewright show` (spec §10) — every field, nulls
/// as "—", payload and raw_ref pretty-printed verbatim. No console dependency; Cli just prints it.
/// </summary>
public static class EnvelopeTextRenderer
{
    private const string NullPlaceholder = "—";

    public static IReadOnlyList<string> Render(EventEnvelope envelope)
    {
        var lines = new List<string>
        {
            $"event_id: {envelope.EventId}",
            $"occurred_at: {envelope.OccurredAt}",
            $"received_at: {envelope.ReceivedAt}",
            $"kind: {envelope.Kind.ToText()}",
            $"event_type: {envelope.EventType}",
            $"emitter_name: {envelope.EmitterName}",
            $"emitter_version: {envelope.EmitterVersion ?? NullPlaceholder}",
            $"adapter_version: {envelope.AdapterVersion}",
            $"original_event: {envelope.OriginalEvent ?? NullPlaceholder}",
            $"session_id: {envelope.SessionId ?? NullPlaceholder}",
            $"prompt_id: {envelope.PromptId ?? NullPlaceholder}",
            $"tool_use_id: {envelope.ToolUseId ?? NullPlaceholder}",
            $"agent_id: {envelope.AgentId ?? NullPlaceholder}",
            $"parent_id: {envelope.ParentId ?? NullPlaceholder}",
            $"repository_id: {envelope.RepositoryId ?? NullPlaceholder}",
            $"worktree_id: {envelope.WorktreeId ?? NullPlaceholder}",
            "raw_ref:",
        };
        lines.AddRange(Indent(PrettyJsonOrPlaceholder(envelope.RawRef)));
        lines.Add("payload:");
        lines.AddRange(Indent(PrettyJsonOrPlaceholder(envelope.Payload)));
        return lines;
    }

    private static IEnumerable<string> Indent(IEnumerable<string> lines) =>
        lines.Select(line => "  " + line);

    private static IReadOnlyList<string> PrettyJsonOrPlaceholder(string? json)
    {
        if (json is null)
        {
            return [NullPlaceholder];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var pretty = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
            return pretty.Split('\n');
        }
        catch (JsonException)
        {
            return [json];
        }
    }
}
