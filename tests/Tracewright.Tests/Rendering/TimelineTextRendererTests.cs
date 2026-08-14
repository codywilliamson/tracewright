using Tracewright.Core.Rendering;
using Tracewright.Core.Projections;
using static Tracewright.Tests.TimelineEnvelopeBuilder;

namespace Tracewright.Tests;

public sealed class TimelineTextRendererTests
{
    [Fact]
    public void Renders_the_spec_example_shape_for_a_session_and_an_unattributed_commit()
    {
        var events = new[]
        {
            Make("2026-08-13T10:41:02.000Z", eventType: "claude.session.started", sessionId: "s1",
                eventId: "01J9AAAAAAAAAAAAAAAAAAAA4F", payload: """{"source":"startup"}"""),
            Make("2026-08-13T10:46:01.000Z", eventType: "git.commit", repositoryId: "repo-1",
                eventId: "01J9BBBBBBBBBBBBBBBBBBBB80", payload: """{"sha":"83bd41a1234","subject":"feat: event st…"}"""),
        };

        var lines = TimelineTextRenderer.Render(TimelineProjection.Build(events));

        Assert.Equal(
        [
            "session s1 (claude-code)",
            $"  {Local(events[0].OccurredAt)}  OBSERVED  claude.session.started  source=startup  [01J9…4F]",
            "",
            "repository repo-1",
            $"  {Local(events[1].OccurredAt)}  OBSERVED  git.commit  83bd41a \"feat: event st…\"",
            "  correlation: none — unattributed  [01J9…80]",
        ], lines);
    }

    [Fact]
    public void Subagent_lines_indent_four_spaces_and_carry_an_agent_tag()
    {
        var envelope = Make(
            "2026-08-13T10:42:00.000Z", eventType: "claude.tool.started", sessionId: "s1", agentId: "agent-1",
            eventId: "01J9CCCCCCCCCCCCCCCCCCCC56", payload: """{"tool_name":"Bash","tool_input":{"command":"ls -la"},"agent_type":"explorer"}""");

        var lines = TimelineTextRenderer.Render(TimelineProjection.Build([envelope]));

        Assert.Equal(
        [
            "session s1 (claude-code)",
            $"    {Local(envelope.OccurredAt)}  OBSERVED  claude.tool.started  agent=explorer  Bash: ls -la  [01J9…56]",
        ], lines);
    }

    [Fact]
    public void Subagent_lines_without_a_known_agent_type_label_as_subagent()
    {
        var envelope = Make(
            "2026-08-13T10:42:00.000Z", eventType: "claude.tool.started", sessionId: "s1", agentId: "agent-1",
            eventId: "01J9DDDDDDDDDDDDDDDDDDDD56", payload: """{"tool_name":"Bash","tool_input":{"command":"ls"}}""");

        var lines = TimelineTextRenderer.Render(TimelineProjection.Build([envelope]));

        Assert.Contains("agent=subagent", lines[1]);
    }

    [Fact]
    public void Lines_with_no_summary_omit_the_empty_field_rather_than_a_stray_gap()
    {
        var envelope = Make(
            "2026-08-13T10:00:00.000Z", eventType: "claude.hook.unknown", sessionId: "s1",
            eventId: "01J9EEEEEEEEEEEEEEEEEEEE00", payload: "{}");

        var lines = TimelineTextRenderer.Render(TimelineProjection.Build([envelope]));

        Assert.Equal($"  {Local(envelope.OccurredAt)}  OBSERVED  claude.hook.unknown  [01J9…00]", lines[1]);
    }

    private static string Local(string occurredAt) =>
        DateTimeOffset.Parse(occurredAt).ToLocalTime().ToString("HH:mm:ss");
}
