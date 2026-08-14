using Tracewright.Abstractions;
using Microsoft.Data.Sqlite;
using Tracewright.Core;

namespace Tracewright.Tests;

public sealed class EventStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;
    private readonly EventStore _store;

    public EventStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "tracewright.db");
        _store = new EventStore(_dbPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private static EventEnvelope MakeEnvelope(
        string? eventId = null,
        string occurredAt = "2026-08-13T10:00:00.000Z",
        string receivedAt = "2026-08-13T10:00:00.100Z",
        EvidenceKind kind = EvidenceKind.Observed,
        string eventType = "claude.tool.succeeded",
        string emitterName = "claude-code",
        string? emitterVersion = "2.1.227",
        string adapterVersion = "0.1.0",
        string? originalEvent = "PostToolUse",
        string? sessionId = "session-1",
        string? promptId = "prompt-1",
        string? toolUseId = "tool-1",
        string? agentId = null,
        string? parentId = null,
        string? repositoryId = "repo-1",
        string? worktreeId = "/repo",
        string? rawRef = """{"transcript_path":"/tmp/t.jsonl"}""",
        string payload = """{"tool_name":"Bash"}""") => new()
    {
        EventId = eventId ?? Ulid.NewUlid(),
        OccurredAt = occurredAt,
        ReceivedAt = receivedAt,
        Kind = kind,
        EventType = eventType,
        EmitterName = emitterName,
        EmitterVersion = emitterVersion,
        AdapterVersion = adapterVersion,
        OriginalEvent = originalEvent,
        SessionId = sessionId,
        PromptId = promptId,
        ToolUseId = toolUseId,
        AgentId = agentId,
        ParentId = parentId,
        RepositoryId = repositoryId,
        WorktreeId = worktreeId,
        RawRef = rawRef,
        Payload = payload
    };

    [Fact]
    public void DefaultPath_points_to_user_profile_tracewright_directory()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".tracewright",
            "tracewright.db");

        Assert.Equal(expected, EventStore.DefaultPath);
    }

    [Fact]
    public void Append_creates_database_and_schema_on_first_use()
    {
        Assert.False(File.Exists(_dbPath));

        _store.Append(MakeEnvelope());

        Assert.True(File.Exists(_dbPath));
    }

    [Fact]
    public void Append_seeds_meta_schema_version()
    {
        _store.Append(MakeEnvelope());

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = 'schema_version'";

        Assert.Equal("1", (string?)command.ExecuteScalar());
    }

    [Fact]
    public void Query_on_missing_database_returns_empty_and_does_not_create_file()
    {
        var results = _store.Query(new EventQuery());

        Assert.Empty(results);
        Assert.False(File.Exists(_dbPath));
    }

    [Fact]
    public void FindByIdPrefix_on_missing_database_returns_empty_and_does_not_create_file()
    {
        var results = _store.FindByIdPrefix("01J9");

        Assert.Empty(results);
        Assert.False(File.Exists(_dbPath));
    }

    [Fact]
    public void Append_and_query_round_trips_every_field()
    {
        var envelope = MakeEnvelope();
        _store.Append(envelope);

        var results = _store.Query(new EventQuery());

        Assert.Equal(envelope, Assert.Single(results));
    }

    [Fact]
    public void Append_and_query_round_trips_nulls()
    {
        var envelope = MakeEnvelope(
            emitterVersion: null,
            originalEvent: null,
            sessionId: null,
            promptId: null,
            toolUseId: null,
            agentId: null,
            parentId: null,
            repositoryId: null,
            worktreeId: null,
            rawRef: null);
        _store.Append(envelope);

        var results = _store.Query(new EventQuery());

        Assert.Equal(envelope, Assert.Single(results));
    }

    [Fact]
    public void Query_orders_by_occurred_at_then_received_at_then_event_id()
    {
        var latest = MakeEnvelope(eventId: "01AAAAAAAAAAAAAAAAAAAAAAAA", occurredAt: "2026-08-13T10:00:01.000Z", receivedAt: "2026-08-13T10:00:02.000Z");
        var tieLowerId = MakeEnvelope(eventId: "01BBBBBBBBBBBBBBBBBBBBBBBB", occurredAt: "2026-08-13T10:00:00.000Z", receivedAt: "2026-08-13T10:00:00.000Z");
        var tieHigherId = MakeEnvelope(eventId: "01CCCCCCCCCCCCCCCCCCCCCCCC", occurredAt: "2026-08-13T10:00:00.000Z", receivedAt: "2026-08-13T10:00:00.000Z");

        _store.Append(latest);
        _store.Append(tieLowerId);
        _store.Append(tieHigherId);

        var results = _store.Query(new EventQuery());

        Assert.Equal(
            [tieLowerId.EventId, tieHigherId.EventId, latest.EventId],
            results.Select(r => r.EventId));
    }

    [Fact]
    public void Query_breaks_occurred_at_ties_by_received_at()
    {
        var receivedLater = MakeEnvelope(eventId: "01AAAAAAAAAAAAAAAAAAAAAAAA", occurredAt: "2026-08-13T10:00:00.000Z", receivedAt: "2026-08-13T10:00:05.000Z");
        var receivedEarlier = MakeEnvelope(eventId: "01ZZZZZZZZZZZZZZZZZZZZZZZZ", occurredAt: "2026-08-13T10:00:00.000Z", receivedAt: "2026-08-13T10:00:01.000Z");

        _store.Append(receivedLater);
        _store.Append(receivedEarlier);

        var results = _store.Query(new EventQuery());

        Assert.Equal([receivedEarlier.EventId, receivedLater.EventId], results.Select(r => r.EventId));
    }

    [Fact]
    public void Query_filters_by_repository_id()
    {
        _store.Append(MakeEnvelope(repositoryId: "repo-a"));
        _store.Append(MakeEnvelope(repositoryId: "repo-b"));

        var results = _store.Query(new EventQuery { RepositoryId = "repo-a" });

        Assert.Equal("repo-a", Assert.Single(results).RepositoryId);
    }

    [Fact]
    public void Query_filters_by_session_id()
    {
        _store.Append(MakeEnvelope(sessionId: "session-a"));
        _store.Append(MakeEnvelope(sessionId: "session-b"));

        var results = _store.Query(new EventQuery { SessionId = "session-a" });

        Assert.Equal("session-a", Assert.Single(results).SessionId);
    }

    [Fact]
    public void Query_filters_by_since()
    {
        _store.Append(MakeEnvelope(occurredAt: "2026-08-13T09:00:00.000Z"));
        _store.Append(MakeEnvelope(occurredAt: "2026-08-13T11:00:00.000Z"));

        var results = _store.Query(new EventQuery { Since = "2026-08-13T10:00:00.000Z" });

        Assert.Equal("2026-08-13T11:00:00.000Z", Assert.Single(results).OccurredAt);
    }

    [Fact]
    public void Query_filters_by_until()
    {
        _store.Append(MakeEnvelope(occurredAt: "2026-08-13T09:00:00.000Z"));
        _store.Append(MakeEnvelope(occurredAt: "2026-08-13T11:00:00.000Z"));

        var results = _store.Query(new EventQuery { Until = "2026-08-13T10:00:00.000Z" });

        Assert.Equal("2026-08-13T09:00:00.000Z", Assert.Single(results).OccurredAt);
    }

    [Fact]
    public void Query_filters_by_kind()
    {
        _store.Append(MakeEnvelope(kind: EvidenceKind.Observed));
        _store.Append(MakeEnvelope(kind: EvidenceKind.Asserted));

        var results = _store.Query(new EventQuery { Kind = EvidenceKind.Asserted });

        Assert.Equal(EvidenceKind.Asserted, Assert.Single(results).Kind);
    }

    [Theory]
    [InlineData("claude.tool.*", "claude.tool.succeeded", true)]
    [InlineData("claude.tool.*", "claude.tool.failed", true)]
    [InlineData("claude.tool.*", "claude.session.started", false)]
    [InlineData("*.commit", "git.commit", true)]
    [InlineData("git.commit", "git.commit", true)]
    [InlineData("git.commit", "git.commitx", false)]
    public void Query_translates_event_type_glob(string glob, string eventType, bool shouldMatch)
    {
        _store.Append(MakeEnvelope(eventType: eventType));

        var results = _store.Query(new EventQuery { EventTypeGlob = glob });

        Assert.Equal(shouldMatch, results.Count == 1);
    }

    [Fact]
    public void Query_glob_escapes_underscore_as_literal_character()
    {
        _store.Append(MakeEnvelope(eventType: "a_b"));
        _store.Append(MakeEnvelope(eventType: "aXb"));

        var results = _store.Query(new EventQuery { EventTypeGlob = "a_b" });

        Assert.Equal("a_b", Assert.Single(results).EventType);
    }

    [Fact]
    public void Query_glob_escapes_percent_as_literal_character()
    {
        _store.Append(MakeEnvelope(eventType: "50%off"));
        _store.Append(MakeEnvelope(eventType: "50xxxoff"));

        var results = _store.Query(new EventQuery { EventTypeGlob = "50%off" });

        Assert.Equal("50%off", Assert.Single(results).EventType);
    }

    [Fact]
    public void Schema_check_constraint_rejects_invalid_kind()
    {
        _store.Append(MakeEnvelope());

        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO events (event_id, occurred_at, received_at, kind, event_type, emitter_name, adapter_version, payload)
            VALUES ('bad-id', '2026-01-01T00:00:00Z', '2026-01-01T00:00:00Z', 'invalid', 'x', 'manual', '0.1.0', '{}')
            """;

        var ex = Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
        Assert.Contains("CHECK", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindByIdPrefix_exact_match()
    {
        var envelope = MakeEnvelope(eventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        _store.Append(envelope);

        var results = _store.FindByIdPrefix("01ARZ3NDEKTSV4RRFFQ69G5FAV");

        Assert.Equal(envelope.EventId, Assert.Single(results).EventId);
    }

    [Fact]
    public void FindByIdPrefix_matches_by_prefix()
    {
        var envelope = MakeEnvelope(eventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV");
        _store.Append(envelope);

        var results = _store.FindByIdPrefix("01ARZ3");

        Assert.Equal(envelope.EventId, Assert.Single(results).EventId);
    }

    [Fact]
    public void FindByIdPrefix_returns_multiple_matches()
    {
        var first = MakeEnvelope(eventId: "01ARZ3AAAAAAAAAAAAAAAAAAAA");
        var second = MakeEnvelope(eventId: "01ARZ3BBBBBBBBBBBBBBBBBBBB");
        var unrelated = MakeEnvelope(eventId: "01OTHERCCCCCCCCCCCCCCCCCCC");
        _store.Append(first);
        _store.Append(second);
        _store.Append(unrelated);

        var results = _store.FindByIdPrefix("01ARZ3");

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.StartsWith("01ARZ3", r.EventId));
    }

    [Fact]
    public void FindByIdPrefix_no_match_returns_empty()
    {
        _store.Append(MakeEnvelope(eventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV"));

        var results = _store.FindByIdPrefix("ZZZZZZ");

        Assert.Empty(results);
    }

    [Fact]
    public void Query_throws_when_schema_version_is_newer_than_known()
    {
        _store.Append(MakeEnvelope());
        BumpSchemaVersion("999");

        var ex = Assert.Throws<InvalidOperationException>(() => _store.Query(new EventQuery()));
        Assert.Contains("schema_version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Append_throws_when_schema_version_is_newer_than_known()
    {
        _store.Append(MakeEnvelope());
        BumpSchemaVersion("999");

        Assert.Throws<InvalidOperationException>(() => _store.Append(MakeEnvelope()));
    }

    private void BumpSchemaVersion(string version)
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE meta SET value = $version WHERE key = 'schema_version'";
        command.Parameters.AddWithValue("$version", version);
        command.ExecuteNonQuery();
    }
}
