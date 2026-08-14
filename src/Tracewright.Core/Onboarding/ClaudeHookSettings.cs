using System.Text.Json;
using System.Text.Json.Nodes;
using Tracewright.Core.Adapters;

namespace Tracewright.Core.Onboarding;

/// <summary>
/// the `.claude/settings.json` hook block (spec §5) and how to merge it into a file that may
/// already hold the user's own settings. Committed config always names the canonical
/// `tracewright`, never the `twr` alias.
/// </summary>
public static class ClaudeHookSettings
{
    public const string Command = "tracewright";

    // matcher hooks fire per tool; the rest are session/task lifecycle and take no matcher.
    private static readonly string[] ToolHookEvents = ["PreToolUse", "PostToolUse", "PostToolUseFailure"];

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    /// <summary>every hook the adapter knows how to translate — one source of truth (spec §5).</summary>
    public static IReadOnlyList<string> HookEvents { get; } = [.. ClaudeHookAdapter.KnownHookEvents];

    /// <summary>
    /// adds a tracewright hook group for any event that lacks one, leaving every other key and
    /// every foreign hook exactly as found. Returns null when the file already covers everything.
    /// </summary>
    public static string? Merge(string? existingJson)
    {
        var root = Parse(existingJson);
        var hooks = root["hooks"] as JsonObject;
        if (hooks is null)
        {
            hooks = [];
            root["hooks"] = hooks;
        }

        var changed = false;
        foreach (var hookEvent in HookEvents)
        {
            if (hooks[hookEvent] is not JsonArray groups)
            {
                groups = [];
                hooks[hookEvent] = groups;
            }

            if (AlreadyHooked(groups))
            {
                continue;
            }

            groups.Add(HookGroup(hookEvent));
            changed = true;
        }

        return changed ? root.ToJsonString(WriteOptions) : null;
    }

    private static JsonObject Parse(string? existingJson)
    {
        if (string.IsNullOrWhiteSpace(existingJson))
        {
            return [];
        }

        return JsonNode.Parse(existingJson) as JsonObject
            ?? throw new InvalidOperationException(".claude/settings.json is not a JSON object");
    }

    private static bool AlreadyHooked(JsonArray groups) =>
        groups.Any(group => group?["hooks"] is JsonArray hooks
            && hooks.Any(hook => hook?["command"]?.GetValue<string>() == Command));

    private static JsonObject HookGroup(string hookEvent)
    {
        var group = new JsonObject();
        if (ToolHookEvents.Contains(hookEvent))
        {
            group["matcher"] = "*";
        }

        group["hooks"] = new JsonArray(new JsonObject
        {
            ["type"] = "command",
            ["command"] = Command,
            ["args"] = new JsonArray("emit", "claude"),
            ["async"] = true,
        });
        return group;
    }
}
