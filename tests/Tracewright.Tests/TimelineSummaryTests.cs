using Tracewright.Abstractions;
using Tracewright.Core;

namespace Tracewright.Tests;

public sealed class TimelineSummaryTests
{
    private static EventEnvelope Envelope(string eventType, string payload, string? originalEvent = null) => new()
    {
        EventId = Ulid.NewUlid(),
        OccurredAt = "2026-08-13T10:00:00.000Z",
        ReceivedAt = "2026-08-13T10:00:00.100Z",
        Kind = EvidenceKind.Observed,
        EventType = eventType,
        EmitterName = "claude-code",
        AdapterVersion = "0.1.0",
        OriginalEvent = originalEvent,
        Payload = payload,
    };

    [Fact]
    public void Session_started_summarizes_source()
    {
        var e = Envelope("claude.session.started", """{"source":"startup"}""");
        Assert.Equal("source=startup", TimelineSummary.For(e));
    }

    [Fact]
    public void Prompt_submitted_quotes_user_input()
    {
        var e = Envelope("claude.prompt.submitted", """{"user_input":"implement event persistence"}""");
        Assert.Equal("\"implement event persistence\"", TimelineSummary.For(e));
    }

    [Fact]
    public void Prompt_submitted_falls_back_to_prompt_field()
    {
        var e = Envelope("claude.prompt.submitted", """{"prompt":"do the thing"}""");
        Assert.Equal("\"do the thing\"", TimelineSummary.For(e));
    }

    [Theory]
    [InlineData("claude.tool.started")]
    [InlineData("claude.tool.succeeded")]
    [InlineData("claude.tool.failed")]
    public void Tool_events_use_bash_command_text(string eventType)
    {
        var e = Envelope(eventType, """{"tool_name":"Bash","tool_input":{"command":"dotnet test"}}""");
        Assert.Equal("Bash: dotnet test", TimelineSummary.For(e));
    }

    [Fact]
    public void Tool_events_use_terse_first_property_for_non_bash_tools()
    {
        var e = Envelope("claude.tool.succeeded", """{"tool_name":"Read","tool_input":{"file_path":"/x/y.cs"}}""");
        Assert.Equal("Read: /x/y.cs", TimelineSummary.For(e));
    }

    [Fact]
    public void Tool_events_with_no_tool_input_show_just_the_tool_name()
    {
        var e = Envelope("claude.tool.succeeded", """{"tool_name":"Bash"}""");
        Assert.Equal("Bash", TimelineSummary.For(e));
    }

    [Fact]
    public void Turn_completed_summarizes_stop_reason()
    {
        var e = Envelope("claude.turn.completed", """{"stop_reason":"end_turn"}""");
        Assert.Equal("stop_reason=end_turn", TimelineSummary.For(e));
    }

    [Fact]
    public void Agent_started_summarizes_agent_type()
    {
        var e = Envelope("claude.agent.started", """{"agent_type":"explorer"}""");
        Assert.Equal("agent_type=explorer", TimelineSummary.For(e));
    }

    [Fact]
    public void Task_created_summarizes_task_name()
    {
        var e = Envelope("claude.task.created", """{"task_name":"refactor"}""");
        Assert.Equal("refactor", TimelineSummary.For(e));
    }

    [Fact]
    public void Task_completed_includes_result()
    {
        var e = Envelope("claude.task.completed", """{"task_name":"refactor","result":"success"}""");
        Assert.Equal("refactor (result=success)", TimelineSummary.For(e));
    }

    [Fact]
    public void Session_ended_summarizes_reason()
    {
        var e = Envelope("claude.session.ended", """{"reason":"clear"}""");
        Assert.Equal("reason=clear", TimelineSummary.For(e));
    }

    [Fact]
    public void Git_commit_summarizes_short_sha_and_subject()
    {
        var e = Envelope("git.commit", """{"sha":"83bd41abcdef","subject":"feat: event store"}""");
        Assert.Equal("83bd41a \"feat: event store\"", TimelineSummary.For(e));
    }

    [Fact]
    public void Unknown_event_type_falls_back_to_original_event()
    {
        var e = Envelope("claude.hook.unknown", """{"anything":"here"}""", originalEvent: "SomeNewHook");
        Assert.Equal("SomeNewHook", TimelineSummary.For(e));
    }

    [Fact]
    public void Unknown_event_type_with_no_original_event_is_empty()
    {
        var e = Envelope("claude.hook.unknown", """{"anything":"here"}""");
        Assert.Equal("", TimelineSummary.For(e));
    }

    [Fact]
    public void Missing_fields_render_empty_rather_than_invented()
    {
        var e = Envelope("claude.session.started", """{}""");
        Assert.Equal("", TimelineSummary.For(e));
    }

    [Fact]
    public void Long_text_is_truncated_for_display_with_ellipsis()
    {
        var longInput = new string('x', 100);
        var e = Envelope("claude.prompt.submitted", $$"""{"user_input":"{{longInput}}"}""");

        var summary = TimelineSummary.For(e);

        Assert.Equal("\"" + new string('x', 60) + "…\"", summary);
    }

    [Fact]
    public void AgentType_reads_agent_type_from_payload()
    {
        var e = Envelope("claude.tool.started", """{"tool_name":"Bash","agent_type":"explorer"}""");
        Assert.Equal("explorer", TimelineSummary.AgentType(e));
    }

    [Fact]
    public void AgentType_is_null_when_payload_lacks_it()
    {
        var e = Envelope("claude.tool.started", """{"tool_name":"Bash"}""");
        Assert.Null(TimelineSummary.AgentType(e));
    }
}
