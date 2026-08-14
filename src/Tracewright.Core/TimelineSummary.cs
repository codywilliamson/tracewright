using Tracewright.Abstractions;
using System.Text.Json;

namespace Tracewright.Core;

/// <summary>
/// best-effort per-type summary text from an event's payload (spec §5 mapping table, §7 rule 2).
/// missing fields render empty — never invented (D-019). Display-only truncation; storage is
/// untouched.
/// </summary>
public static class TimelineSummary
{
    private const int TruncateLength = 60;
    private const string BashToolName = "Bash";

    public static string For(EventEnvelope envelope)
    {
        var root = ParsePayload(envelope.Payload);

        return envelope.EventType switch
        {
            "claude.session.started" => LabeledField(root, "source", "source"),
            "claude.prompt.submitted" => PromptSummary(root),
            "claude.tool.started" or "claude.tool.succeeded" or "claude.tool.failed" => ToolSummary(root),
            "claude.turn.completed" => LabeledField(root, "stop_reason", "stop_reason"),
            "claude.session.ended" => LabeledField(root, "reason", "reason"),
            "git.commit" => CommitSummary(root),
            var t when t.StartsWith("claude.agent.", StringComparison.Ordinal) => AgentSummary(root),
            var t when t.StartsWith("claude.task.", StringComparison.Ordinal) => TaskSummary(root, t),
            _ => envelope.OriginalEvent ?? "",
        };
    }

    // agent_type labels subagent indentation regardless of event_type (spec §7 rule 3) — best
    // effort, only the events that actually carry it will show a label.
    public static string? AgentType(EventEnvelope envelope) =>
        GetString(ParsePayload(envelope.Payload), "agent_type");

    private static JsonElement? ParsePayload(string payload)
    {
        try
        {
            return JsonDocument.Parse(payload).RootElement;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string LabeledField(JsonElement? root, string propertyName, string label)
    {
        var value = GetString(root, propertyName);
        return value is null ? "" : $"{label}={Truncate(value)}";
    }

    private static string PromptSummary(JsonElement? root)
    {
        var text = GetString(root, "user_input") ?? GetString(root, "prompt");
        return text is null ? "" : $"\"{Truncate(text)}\"";
    }

    private static string ToolSummary(JsonElement? root)
    {
        var toolName = GetString(root, "tool_name");
        if (toolName is null)
        {
            return "";
        }

        var toolInput = GetObject(root, "tool_input");
        var inputSummary = toolName == BashToolName
            ? GetString(toolInput, "command")
            : FirstPropertySummary(toolInput);

        return inputSummary is null ? toolName : $"{toolName}: {Truncate(inputSummary)}";
    }

    private static string? FirstPropertySummary(JsonElement? toolInput)
    {
        if (toolInput is not { } input)
        {
            return null;
        }

        foreach (var property in input.EnumerateObject())
        {
            return property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();
        }

        return null;
    }

    private static string CommitSummary(JsonElement? root)
    {
        var sha = GetString(root, "sha");
        var subject = GetString(root, "subject");
        if (sha is null && subject is null)
        {
            return "";
        }

        var shortSha = sha is null ? "" : sha[..Math.Min(7, sha.Length)];
        return subject is null ? shortSha : $"{shortSha} \"{Truncate(subject)}\"".TrimStart();
    }

    private static string AgentSummary(JsonElement? root) => LabeledField(root, "agent_type", "agent_type");

    private static string TaskSummary(JsonElement? root, string eventType)
    {
        var taskName = GetString(root, "task_name");
        if (taskName is null)
        {
            return "";
        }

        if (eventType != "claude.task.completed")
        {
            return taskName;
        }

        var result = GetString(root, "result");
        return result is null ? taskName : $"{taskName} (result={result})";
    }

    private static string? GetString(JsonElement? root, string propertyName) =>
        root is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement? GetObject(JsonElement? root, string propertyName) =>
        root is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(propertyName, out var value)
        && value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static string Truncate(string text)
    {
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= TruncateLength ? singleLine : singleLine[..TruncateLength] + "…";
    }
}
