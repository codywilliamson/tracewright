using System.Text.Json;

namespace Tracewright.Core;

/// <summary>
/// `tracewright emit raw` translation — the universal ingress, itself adapter `tracewright.raw`
/// (D-015). Unlike the hook adapters this one is human-facing: it validates strictly and throws
/// RawEventValidationException by name rather than guessing or silently dropping evidence.
/// </summary>
public static class RawEventAdapter
{
    public const string DefaultEmitterName = "manual";

    // envelope fields Tracewright stamps itself — a caller supplying these is a caller bug, not silently fixed (D-015).
    private static readonly HashSet<string> StampedFields = ["event_id", "received_at", "adapter_version"];

    private static readonly HashSet<string> AllowedFields =
    [
        "event_type", "payload", "emitter_name", "emitter_version", "original_event",
        "session_id", "prompt_id", "tool_use_id", "agent_id", "parent_id",
        "repository_id", "worktree_id", "raw_ref", "occurred_at",
    ];

    public static EventEnvelope Build(string stdin, string? kindFlag, string invocationTimestamp)
    {
        var kind = ParseKind(kindFlag);
        var root = ParseObject(stdin);

        foreach (var property in root.EnumerateObject())
        {
            if (StampedFields.Contains(property.Name))
            {
                throw new RawEventValidationException(
                    $"'{property.Name}' is stamped by tracewright and cannot be supplied");
            }

            if (property.Name == "kind")
            {
                throw new RawEventValidationException(
                    "kind must be supplied via --kind, not the JSON body");
            }

            if (!AllowedFields.Contains(property.Name))
            {
                throw new RawEventValidationException($"unknown field: '{property.Name}'");
            }
        }

        var eventType = RequireString(root, "event_type");
        var payload = RequireObjectRawText(root, "payload");

        return new EventEnvelope
        {
            EventId = Ulid.NewUlid(),
            OccurredAt = GetString(root, "occurred_at") ?? invocationTimestamp,
            ReceivedAt = Timestamp.Now(),
            Kind = kind,
            EventType = eventType,
            EmitterName = GetString(root, "emitter_name") ?? DefaultEmitterName,
            EmitterVersion = GetString(root, "emitter_version"),
            AdapterVersion = AdapterVersion.Current,
            OriginalEvent = GetString(root, "original_event"),
            SessionId = GetString(root, "session_id"),
            PromptId = GetString(root, "prompt_id"),
            ToolUseId = GetString(root, "tool_use_id"),
            AgentId = GetString(root, "agent_id"),
            ParentId = GetString(root, "parent_id"),
            RepositoryId = GetString(root, "repository_id"),
            WorktreeId = GetString(root, "worktree_id"),
            RawRef = GetRawText(root, "raw_ref"),
            Payload = payload,
        };
    }

    private static EvidenceKind ParseKind(string? kindFlag) => (kindFlag ?? "asserted") switch
    {
        "asserted" => EvidenceKind.Asserted,
        "observed" => EvidenceKind.Observed,
        "derived" => throw new RawEventValidationException(
            "--kind derived is rejected: derived is reserved for events Tracewright produces in-process"),
        var other => throw new RawEventValidationException(
            $"unknown --kind value '{other}' (expected asserted or observed)"),
    };

    private static JsonElement ParseObject(string stdin)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(stdin);
        }
        catch (JsonException ex)
        {
            throw new RawEventValidationException($"invalid JSON on stdin: {ex.Message}");
        }

        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new RawEventValidationException("expected a JSON object envelope on stdin");
        }

        return document.RootElement;
    }

    private static string RequireString(JsonElement root, string fieldName)
    {
        if (!root.TryGetProperty(fieldName, out var value))
        {
            throw new RawEventValidationException($"'{fieldName}' is required");
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new RawEventValidationException($"'{fieldName}' must be a string");
        }

        return value.GetString()!;
    }

    private static string RequireObjectRawText(JsonElement root, string fieldName)
    {
        if (!root.TryGetProperty(fieldName, out var value))
        {
            throw new RawEventValidationException($"'{fieldName}' is required");
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new RawEventValidationException($"'{fieldName}' must be a JSON object");
        }

        return value.GetRawText();
    }

    private static string? GetString(JsonElement root, string fieldName)
    {
        if (!root.TryGetProperty(fieldName, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new RawEventValidationException($"'{fieldName}' must be a string");
        }

        return value.GetString();
    }

    private static string? GetRawText(JsonElement root, string fieldName) =>
        root.TryGetProperty(fieldName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetRawText()
            : null;
}
