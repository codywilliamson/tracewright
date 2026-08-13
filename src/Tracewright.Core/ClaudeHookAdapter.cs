using System.Text.Json;

namespace Tracewright.Core;

/// <summary>
/// `tracewright emit claude` translation: hook JSON on stdin -> event (spec §5). Hook context:
/// this adapter never throws for malformed input — it always produces *some* envelope, falling
/// back to claude.hook.unknown with the raw text captured. Only a truly broken store write is
/// the caller's problem to log-and-exit-0.
/// </summary>
public static class ClaudeHookAdapter
{
    public const string EmitterName = "claude-code";
    private const string UnknownEventType = "claude.hook.unknown";

    // spec §5 mapping table — anything not here (or a missing hook_event_name) is unknown.
    private static readonly Dictionary<string, string> EventTypeByHook = new()
    {
        ["SessionStart"] = "claude.session.started",
        ["UserPromptSubmit"] = "claude.prompt.submitted",
        ["PreToolUse"] = "claude.tool.started",
        ["PostToolUse"] = "claude.tool.succeeded",
        ["PostToolUseFailure"] = "claude.tool.failed",
        ["Stop"] = "claude.turn.completed",
        ["SubagentStart"] = "claude.agent.started",
        ["SubagentStop"] = "claude.agent.completed",
        ["TaskCreated"] = "claude.task.created",
        ["TaskCompleted"] = "claude.task.completed",
        ["SessionEnd"] = "claude.session.ended",
        ["PreCompact"] = "claude.context.compacting",
        ["PostCompact"] = "claude.context.compacted",
        ["WorktreeCreate"] = "claude.worktree.created",
        ["WorktreeRemove"] = "claude.worktree.removed",
    };

    public static EventEnvelope Build(string stdin, string processCwd, string invocationTimestamp)
    {
        if (TryParseObject(stdin, out var root))
        {
            return BuildFromPayload(root, stdin, processCwd, invocationTimestamp);
        }

        // malformed or non-object JSON: still capture as unknown, raw text wrapped as a JSON string payload.
        return new EventEnvelope
        {
            EventId = Ulid.NewUlid(),
            OccurredAt = invocationTimestamp,
            ReceivedAt = Timestamp.Now(),
            Kind = EvidenceKind.Observed,
            EventType = UnknownEventType,
            EmitterName = EmitterName,
            EmitterVersion = null,
            AdapterVersion = AdapterVersion.Current,
            OriginalEvent = null,
            RepositoryId = RepositoryResolver.Resolve(processCwd),
            Payload = JsonSerializer.Serialize(stdin),
        };
    }

    private static EventEnvelope BuildFromPayload(
        JsonElement root, string rawPayload, string processCwd, string invocationTimestamp)
    {
        var hookEventName = GetString(root, "hook_event_name");
        var eventType = hookEventName is not null && EventTypeByHook.TryGetValue(hookEventName, out var mapped)
            ? mapped
            : UnknownEventType;

        var cwd = GetString(root, "cwd") ?? processCwd;
        var transcriptPath = GetString(root, "transcript_path");

        return new EventEnvelope
        {
            EventId = Ulid.NewUlid(),
            OccurredAt = invocationTimestamp,
            ReceivedAt = Timestamp.Now(),
            Kind = EvidenceKind.Observed,
            EventType = eventType,
            EmitterName = EmitterName,
            EmitterVersion = GetString(root, "version"),
            AdapterVersion = AdapterVersion.Current,
            OriginalEvent = hookEventName,
            SessionId = GetString(root, "session_id"),
            PromptId = GetString(root, "prompt_id"),
            ToolUseId = GetString(root, "tool_use_id"),
            AgentId = GetString(root, "agent_id"),
            RepositoryId = RepositoryResolver.Resolve(cwd),
            RawRef = transcriptPath is null
                ? null
                : JsonSerializer.Serialize(new { transcript_path = transcriptPath }),
            Payload = rawPayload,
        };
    }

    private static bool TryParseObject(string stdin, out JsonElement root)
    {
        try
        {
            var document = JsonDocument.Parse(stdin);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                root = document.RootElement;
                return true;
            }
        }
        catch (JsonException)
        {
            // fall through — malformed input, handled by the caller as unknown
        }

        root = default;
        return false;
    }

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
