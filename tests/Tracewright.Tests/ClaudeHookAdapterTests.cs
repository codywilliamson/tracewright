using Tracewright.Abstractions;
using Tracewright.Core;

namespace Tracewright.Tests;

public sealed class ClaudeHookAdapterTests : IDisposable
{
    private const string InvocationTimestamp = "2026-08-13T10:00:00.0000000Z";
    private readonly string _tempDir;

    public ClaudeHookAdapterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Theory]
    [InlineData("SessionStart", "claude.session.started")]
    [InlineData("UserPromptSubmit", "claude.prompt.submitted")]
    [InlineData("PreToolUse", "claude.tool.started")]
    [InlineData("PostToolUse", "claude.tool.succeeded")]
    [InlineData("PostToolUseFailure", "claude.tool.failed")]
    [InlineData("Stop", "claude.turn.completed")]
    [InlineData("SubagentStart", "claude.agent.started")]
    [InlineData("SubagentStop", "claude.agent.completed")]
    [InlineData("TaskCreated", "claude.task.created")]
    [InlineData("TaskCompleted", "claude.task.completed")]
    [InlineData("SessionEnd", "claude.session.ended")]
    [InlineData("PreCompact", "claude.context.compacting")]
    [InlineData("PostCompact", "claude.context.compacted")]
    [InlineData("WorktreeCreate", "claude.worktree.created")]
    [InlineData("WorktreeRemove", "claude.worktree.removed")]
    public void Build_maps_known_hook_names_to_event_types(string hookEventName, string expectedEventType)
    {
        var stdin = $$"""{"hook_event_name":"{{hookEventName}}"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal(expectedEventType, envelope.EventType);
        Assert.Equal(EvidenceKind.Observed, envelope.Kind);
        Assert.Equal(hookEventName, envelope.OriginalEvent);
    }

    [Fact]
    public void Build_maps_unrecognized_hook_name_to_unknown_and_preserves_original_event()
    {
        var stdin = """{"hook_event_name":"SomeFutureHook"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("claude.hook.unknown", envelope.EventType);
        Assert.Equal("SomeFutureHook", envelope.OriginalEvent);
    }

    [Fact]
    public void Build_maps_missing_hook_event_name_to_unknown()
    {
        var stdin = """{"session_id":"s1"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("claude.hook.unknown", envelope.EventType);
        Assert.Null(envelope.OriginalEvent);
    }

    [Fact]
    public void Build_copies_correlation_fields_when_present()
    {
        var stdin = """
            {"hook_event_name":"PostToolUse","session_id":"sess-1","prompt_id":"prompt-1",
             "tool_use_id":"tool-1","agent_id":"agent-1"}
            """;

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("sess-1", envelope.SessionId);
        Assert.Equal("prompt-1", envelope.PromptId);
        Assert.Equal("tool-1", envelope.ToolUseId);
        Assert.Equal("agent-1", envelope.AgentId);
    }

    [Fact]
    public void Build_leaves_correlation_fields_null_when_absent()
    {
        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Null(envelope.SessionId);
        Assert.Null(envelope.PromptId);
        Assert.Null(envelope.ToolUseId);
        Assert.Null(envelope.AgentId);
        Assert.Null(envelope.WorktreeId);
    }

    [Fact]
    public void Build_never_invents_worktree_id()
    {
        // the claude adapter has no verified worktree source (D-019) — must stay null even
        // when other correlation ids are present.
        var stdin = """{"hook_event_name":"PostToolUse","session_id":"s1"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Null(envelope.WorktreeId);
    }

    [Fact]
    public void Build_stores_payload_verbatim()
    {
        var stdin = """{"hook_event_name":"PostToolUse","tool_input":{"command":"ls -la"}}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal(stdin, envelope.Payload);
    }

    [Fact]
    public void Build_sets_raw_ref_pointer_when_transcript_path_present()
    {
        var stdin = """{"hook_event_name":"Stop","transcript_path":"/home/u/.claude/projects/x/session.jsonl"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal(
            """{"transcript_path":"/home/u/.claude/projects/x/session.jsonl"}""",
            envelope.RawRef);
    }

    [Fact]
    public void Build_leaves_raw_ref_null_when_transcript_path_absent()
    {
        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Null(envelope.RawRef);
    }

    [Fact]
    public void Build_resolves_repository_id_by_walking_up_from_payload_cwd()
    {
        var nested = Path.Combine(_tempDir, "a", "b");
        Directory.CreateDirectory(nested);
        var markerDir = Path.Combine(_tempDir, ".tracewright");
        Directory.CreateDirectory(markerDir);
        File.WriteAllText(Path.Combine(markerDir, "repo.id"), "repo-xyz");

        var payloadCwd = nested.Replace("\\", "\\\\");
        var stdin = $$"""{"hook_event_name":"SessionStart","cwd":"{{payloadCwd}}"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("repo-xyz", envelope.RepositoryId);
    }

    [Fact]
    public void Build_falls_back_to_process_cwd_when_payload_cwd_absent()
    {
        var markerDir = Path.Combine(_tempDir, ".tracewright");
        Directory.CreateDirectory(markerDir);
        File.WriteAllText(Path.Combine(markerDir, "repo.id"), "repo-from-process-cwd");

        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("repo-from-process-cwd", envelope.RepositoryId);
    }

    [Fact]
    public void Build_leaves_repository_id_null_when_no_marker_found()
    {
        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Null(envelope.RepositoryId);
    }

    [Fact]
    public void Build_captures_emitter_version_from_payload_when_present()
    {
        var stdin = """{"hook_event_name":"SessionStart","version":"2.1.227"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("2.1.227", envelope.EmitterVersion);
    }

    [Fact]
    public void Build_leaves_emitter_version_null_when_payload_has_no_version_field()
    {
        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Null(envelope.EmitterVersion);
    }

    [Fact]
    public void Build_stamps_occurred_at_from_invocation_timestamp()
    {
        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal(InvocationTimestamp, envelope.OccurredAt);
    }

    [Fact]
    public void Build_sets_adapter_and_emitter_identity()
    {
        var stdin = """{"hook_event_name":"SessionStart"}""";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("claude-code", envelope.EmitterName);
        Assert.Equal("0.1.0", envelope.AdapterVersion);
    }

    [Fact]
    public void Build_on_malformed_json_captures_unknown_with_raw_text_wrapped_as_json_string()
    {
        const string stdin = "{not valid json";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("claude.hook.unknown", envelope.EventType);
        Assert.Equal(EvidenceKind.Observed, envelope.Kind);
        Assert.Equal("\"{not valid json\"", envelope.Payload);
    }

    [Fact]
    public void Build_on_empty_stdin_captures_unknown_without_throwing()
    {
        var envelope = ClaudeHookAdapter.Build("", _tempDir, InvocationTimestamp);

        Assert.Equal("claude.hook.unknown", envelope.EventType);
        Assert.Equal("\"\"", envelope.Payload);
    }

    [Fact]
    public void Build_on_non_object_json_captures_unknown()
    {
        const string stdin = "[1,2,3]";

        var envelope = ClaudeHookAdapter.Build(stdin, _tempDir, InvocationTimestamp);

        Assert.Equal("claude.hook.unknown", envelope.EventType);
        Assert.Equal("\"[1,2,3]\"", envelope.Payload);
    }
}
