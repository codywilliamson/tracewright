using Tracewright.Core;

namespace Tracewright.Tests;

public sealed class EnvelopeTextRendererTests
{
    private static EventEnvelope MinimalEnvelope() => new()
    {
        EventId = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        OccurredAt = "2026-08-13T10:00:00.000Z",
        ReceivedAt = "2026-08-13T10:00:00.100Z",
        Kind = EvidenceKind.Observed,
        EventType = "claude.tool.succeeded",
        EmitterName = "claude-code",
        AdapterVersion = "0.1.0",
        Payload = """{"tool_name":"Bash"}""",
    };

    [Fact]
    public void Renders_every_scalar_field()
    {
        var envelope = MinimalEnvelope() with
        {
            EmitterVersion = "2.1.227",
            OriginalEvent = "PostToolUse",
            SessionId = "session-1",
            PromptId = "prompt-1",
            ToolUseId = "tool-1",
            AgentId = "agent-1",
            ParentId = "parent-1",
            RepositoryId = "repo-1",
            WorktreeId = "/repo",
        };

        var lines = EnvelopeTextRenderer.Render(envelope);

        Assert.Contains("event_id: 01ARZ3NDEKTSV4RRFFQ69G5FAV", lines);
        Assert.Contains("occurred_at: 2026-08-13T10:00:00.000Z", lines);
        Assert.Contains("received_at: 2026-08-13T10:00:00.100Z", lines);
        Assert.Contains("kind: observed", lines);
        Assert.Contains("event_type: claude.tool.succeeded", lines);
        Assert.Contains("emitter_name: claude-code", lines);
        Assert.Contains("emitter_version: 2.1.227", lines);
        Assert.Contains("adapter_version: 0.1.0", lines);
        Assert.Contains("original_event: PostToolUse", lines);
        Assert.Contains("session_id: session-1", lines);
        Assert.Contains("prompt_id: prompt-1", lines);
        Assert.Contains("tool_use_id: tool-1", lines);
        Assert.Contains("agent_id: agent-1", lines);
        Assert.Contains("parent_id: parent-1", lines);
        Assert.Contains("repository_id: repo-1", lines);
        Assert.Contains("worktree_id: /repo", lines);
    }

    [Fact]
    public void Renders_nulls_as_an_em_dash()
    {
        var lines = EnvelopeTextRenderer.Render(MinimalEnvelope());

        Assert.Contains("emitter_version: —", lines);
        Assert.Contains("original_event: —", lines);
        Assert.Contains("session_id: —", lines);
        Assert.Contains("prompt_id: —", lines);
        Assert.Contains("tool_use_id: —", lines);
        Assert.Contains("agent_id: —", lines);
        Assert.Contains("parent_id: —", lines);
        Assert.Contains("repository_id: —", lines);
        Assert.Contains("worktree_id: —", lines);
    }

    [Fact]
    public void Renders_a_null_raw_ref_as_an_em_dash_under_its_section()
    {
        var lines = EnvelopeTextRenderer.Render(MinimalEnvelope());

        var rawRefIndex = lines.ToList().IndexOf("raw_ref:");
        Assert.Equal("  —", lines[rawRefIndex + 1]);
    }

    [Fact]
    public void Pretty_prints_the_payload_verbatim()
    {
        var envelope = MinimalEnvelope() with { Payload = """{"tool_name":"Bash","tool_input":{"command":"ls"}}""" };

        var lines = EnvelopeTextRenderer.Render(envelope).ToList();

        var payloadIndex = lines.IndexOf("payload:");
        Assert.True(payloadIndex >= 0);
        Assert.Equal("  {", lines[payloadIndex + 1]);
        Assert.Contains(lines, l => l.Contains("\"tool_name\": \"Bash\""));
    }

    [Fact]
    public void Pretty_prints_raw_ref_when_present()
    {
        var envelope = MinimalEnvelope() with { RawRef = """{"transcript_path":"/tmp/t.jsonl"}""" };

        var lines = EnvelopeTextRenderer.Render(envelope).ToList();

        var rawRefIndex = lines.IndexOf("raw_ref:");
        Assert.Equal("  {", lines[rawRefIndex + 1]);
        Assert.Contains(lines, l => l.Contains("\"transcript_path\": \"/tmp/t.jsonl\""));
    }
}
