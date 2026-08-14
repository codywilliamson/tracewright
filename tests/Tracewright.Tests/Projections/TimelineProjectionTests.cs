using Tracewright.Core.Projections;
using static Tracewright.Tests.TimelineEnvelopeBuilder;

namespace Tracewright.Tests;

public sealed class TimelineProjectionTests
{
    [Fact]
    public void First_line_always_gets_a_header()
    {
        var model = TimelineProjection.Build([Make("2026-08-13T10:00:00.000Z", sessionId: "s1")]);

        Assert.Equal("session s1 (claude-code)", Assert.Single(model.Lines).Header);
    }

    [Fact]
    public void Header_does_not_reprint_for_consecutive_events_in_the_same_session()
    {
        var events = new[]
        {
            Make("2026-08-13T10:00:00.000Z", sessionId: "s1"),
            Make("2026-08-13T10:00:01.000Z", sessionId: "s1"),
        };

        var model = TimelineProjection.Build(events);

        Assert.NotNull(model.Lines[0].Header);
        Assert.Null(model.Lines[1].Header);
    }

    [Fact]
    public void Header_reprints_when_the_stream_switches()
    {
        var events = new[]
        {
            Make("2026-08-13T10:00:00.000Z", sessionId: "s1"),
            Make("2026-08-13T10:00:01.000Z", sessionId: "s2"),
        };

        var model = TimelineProjection.Build(events);

        Assert.Equal("session s1 (claude-code)", model.Lines[0].Header);
        Assert.Equal("session s2 (claude-code)", model.Lines[1].Header);
    }

    [Fact]
    public void Interleaved_sessions_reprint_headers_on_every_switch_never_reordering()
    {
        // chronology wins (D-017): s1, s2, s1 renders in that exact order, three header prints.
        var events = new[]
        {
            Make("2026-08-13T10:00:00.000Z", sessionId: "s1", eventId: "01AAAAAAAAAAAAAAAAAAAAAAAA"),
            Make("2026-08-13T10:00:01.000Z", sessionId: "s2", eventId: "01BBBBBBBBBBBBBBBBBBBBBBBB"),
            Make("2026-08-13T10:00:02.000Z", sessionId: "s1", eventId: "01CCCCCCCCCCCCCCCCCCCCCCCC"),
        };

        var model = TimelineProjection.Build(events);

        Assert.NotNull(model.Lines[0].Header);
        Assert.NotNull(model.Lines[1].Header);
        Assert.NotNull(model.Lines[2].Header);
        Assert.Equal("session s1 (claude-code)", model.Lines[2].Header);
    }

    [Fact]
    public void Repository_header_used_when_no_session_id()
    {
        var model = TimelineProjection.Build([Make("2026-08-13T10:00:00.000Z", repositoryId: "repo-1")]);

        Assert.Equal("repository repo-1", Assert.Single(model.Lines).Header);
    }

    [Fact]
    public void Session_header_takes_priority_over_repository_when_both_present()
    {
        var model = TimelineProjection.Build(
            [Make("2026-08-13T10:00:00.000Z", sessionId: "s1", repositoryId: "repo-1")]);

        Assert.Equal("session s1 (claude-code)", Assert.Single(model.Lines).Header);
    }

    [Fact]
    public void Unanchored_header_used_when_neither_session_nor_repository()
    {
        var model = TimelineProjection.Build([Make("2026-08-13T10:00:00.000Z")]);

        Assert.Equal("unanchored", Assert.Single(model.Lines).Header);
    }

    [Fact]
    public void Session_short_id_truncates_to_eight_characters()
    {
        var model = TimelineProjection.Build(
            [Make("2026-08-13T10:00:00.000Z", sessionId: "fe9f21f8-aaaa-bbbb-cccc-dddddddddddd")]);

        Assert.Equal("session fe9f21f8 (claude-code)", Assert.Single(model.Lines).Header);
    }

    [Fact]
    public void Unattributed_git_commit_gets_a_correlation_note()
    {
        var model = TimelineProjection.Build(
            [Make("2026-08-13T10:00:00.000Z", eventType: "git.commit", repositoryId: "repo-1")]);

        Assert.Equal("correlation: none — unattributed", Assert.Single(model.Lines).CorrelationNote);
    }

    [Fact]
    public void Git_commit_with_session_correlation_gets_no_note()
    {
        var model = TimelineProjection.Build(
            [Make("2026-08-13T10:00:00.000Z", eventType: "git.commit", sessionId: "s1")]);

        Assert.Null(Assert.Single(model.Lines).CorrelationNote);
    }

    [Fact]
    public void Non_commit_events_never_get_a_correlation_note_even_when_unattributed()
    {
        var model = TimelineProjection.Build([Make("2026-08-13T10:00:00.000Z")]);

        Assert.Null(Assert.Single(model.Lines).CorrelationNote);
    }

    [Fact]
    public void Subagent_events_are_marked_indented_and_labeled_with_agent_type()
    {
        var envelope = Make(
            "2026-08-13T10:00:00.000Z", eventType: "claude.tool.started", sessionId: "s1", agentId: "agent-1",
            payload: """{"tool_name":"Bash","agent_type":"explorer"}""");

        var line = Assert.Single(TimelineProjection.Build([envelope]).Lines);

        Assert.True(line.IsSubagent);
        Assert.Equal("explorer", line.AgentType);
    }

    [Fact]
    public void Subagent_events_without_agent_type_in_payload_have_null_agent_type()
    {
        var envelope = Make(
            "2026-08-13T10:00:00.000Z", eventType: "claude.tool.started", sessionId: "s1", agentId: "agent-1",
            payload: """{"tool_name":"Bash"}""");

        var line = Assert.Single(TimelineProjection.Build([envelope]).Lines);

        Assert.True(line.IsSubagent);
        Assert.Null(line.AgentType);
    }

    [Fact]
    public void Non_subagent_events_are_not_indented()
    {
        var line = Assert.Single(TimelineProjection.Build([Make("2026-08-13T10:00:00.000Z", sessionId: "s1")]).Lines);

        Assert.False(line.IsSubagent);
        Assert.Null(line.AgentType);
    }

    [Fact]
    public void FormatUnattributedNote_is_null_when_count_is_zero()
    {
        Assert.Null(TimelineProjection.FormatUnattributedNote(0));
    }

    [Fact]
    public void FormatUnattributedNote_reports_the_count_when_positive()
    {
        Assert.Equal(
            "3 unattributed events in this window — run without --session to see them",
            TimelineProjection.FormatUnattributedNote(3));
    }
}
