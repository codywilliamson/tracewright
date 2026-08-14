using Tracewright.Core.Adapters;
using Tracewright.Core.Storage;
using Tracewright.Abstractions;

namespace Tracewright.Tests;

public sealed class RawEventAdapterTests
{
    private const string InvocationTimestamp = "2026-08-13T10:00:00.0000000Z";

    [Fact]
    public void Build_succeeds_with_minimal_required_fields()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{"text":"hi"}}""";

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal("note.recorded", envelope.EventType);
        Assert.Equal("""{"text":"hi"}""", envelope.Payload);
    }

    [Fact]
    public void Build_defaults_kind_to_asserted()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal(EvidenceKind.Asserted, envelope.Kind);
    }

    [Fact]
    public void Build_allows_explicit_observed_kind()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        var envelope = RawEventAdapter.Build(stdin, "observed", InvocationTimestamp);

        Assert.Equal(EvidenceKind.Observed, envelope.Kind);
    }

    [Fact]
    public void Build_rejects_kind_derived()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        var ex = Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, "derived", InvocationTimestamp));

        Assert.Contains("derived", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_rejects_unknown_kind_flag_value()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, "bogus", InvocationTimestamp));
    }

    [Fact]
    public void Build_rejects_kind_field_inside_json_body()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{},"kind":"observed"}""";

        var ex = Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, null, InvocationTimestamp));

        Assert.Contains("--kind", ex.Message);
    }

    [Theory]
    [InlineData("event_id")]
    [InlineData("received_at")]
    [InlineData("adapter_version")]
    public void Build_rejects_stamped_fields_by_name(string stampedField)
    {
        var stdin = $$"""{"event_type":"note.recorded","payload":{},"{{stampedField}}":"caller-value"}""";

        var ex = Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, null, InvocationTimestamp));

        Assert.Contains(stampedField, ex.Message);
    }

    [Fact]
    public void Build_rejects_unknown_top_level_field_by_name()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{},"typo_field":"x"}""";

        var ex = Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, null, InvocationTimestamp));

        Assert.Contains("typo_field", ex.Message);
    }

    [Fact]
    public void Build_requires_event_type()
    {
        const string stdin = """{"payload":{}}""";

        var ex = Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, null, InvocationTimestamp));

        Assert.Contains("event_type", ex.Message);
    }

    [Fact]
    public void Build_requires_payload()
    {
        const string stdin = """{"event_type":"note.recorded"}""";

        var ex = Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, null, InvocationTimestamp));

        Assert.Contains("payload", ex.Message);
    }

    [Fact]
    public void Build_requires_payload_to_be_a_json_object()
    {
        const string stdin = """{"event_type":"note.recorded","payload":"not an object"}""";

        Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build(stdin, null, InvocationTimestamp));
    }

    [Fact]
    public void Build_defaults_emitter_name_to_manual()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal("manual", envelope.EmitterName);
    }

    [Fact]
    public void Build_defaults_occurred_at_to_invocation_timestamp()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal(InvocationTimestamp, envelope.OccurredAt);
    }

    [Fact]
    public void Build_honors_caller_supplied_occurred_at()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{},"occurred_at":"2020-01-01T00:00:00Z"}""";

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal("2020-01-01T00:00:00Z", envelope.OccurredAt);
    }

    [Fact]
    public void Build_captures_optional_correlation_and_emitter_fields()
    {
        const string stdin = """
            {"event_type":"decision.recorded","payload":{},"emitter_name":"human",
             "emitter_version":"1.0","original_event":"manual-note","session_id":"s1",
             "prompt_id":"p1","tool_use_id":"t1","agent_id":"a1","parent_id":"par1",
             "repository_id":"repo1","worktree_id":"/repo","raw_ref":{"note":"x"}}
            """;

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal("human", envelope.EmitterName);
        Assert.Equal("1.0", envelope.EmitterVersion);
        Assert.Equal("manual-note", envelope.OriginalEvent);
        Assert.Equal("s1", envelope.SessionId);
        Assert.Equal("p1", envelope.PromptId);
        Assert.Equal("t1", envelope.ToolUseId);
        Assert.Equal("a1", envelope.AgentId);
        Assert.Equal("par1", envelope.ParentId);
        Assert.Equal("repo1", envelope.RepositoryId);
        Assert.Equal("/repo", envelope.WorktreeId);
        Assert.Equal("""{"note":"x"}""", envelope.RawRef);
    }

    [Fact]
    public void Build_stamps_fresh_event_id_and_adapter_version()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{}}""";

        var envelope = RawEventAdapter.Build(stdin, null, InvocationTimestamp);

        Assert.Equal(26, envelope.EventId.Length);
        Assert.Equal("0.1.0", envelope.AdapterVersion);
    }

    [Fact]
    public void Build_round_trips_into_the_store()
    {
        const string stdin = """{"event_type":"note.recorded","payload":{"text":"hello"}}""";
        var envelope = RawEventAdapter.Build(stdin, "observed", InvocationTimestamp);

        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            var store = new EventStore(Path.Combine(tempDir, "tracewright.db"));
            store.Append(envelope);

            var results = store.Query(new EventQuery());

            Assert.Equal(envelope, Assert.Single(results));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Build_rejects_invalid_json_on_stdin()
    {
        Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build("not json", null, InvocationTimestamp));
    }

    [Fact]
    public void Build_rejects_non_object_json_on_stdin()
    {
        Assert.Throws<RawEventValidationException>(
            () => RawEventAdapter.Build("[1,2,3]", null, InvocationTimestamp));
    }
}
