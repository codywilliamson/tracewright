using Tracewright.Abstractions;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Tracewright.Core.Storage;

/// <summary>
/// append-only SQLite event store (spec §2). Append lazy-bootstraps the directory, database,
/// and schema on first use (D-024); read methods never do — a missing store just means an
/// empty ledger. One connection per operation, no shared state, so parallel async hook
/// processes can write concurrently under WAL.
/// </summary>
public sealed class EventStore : IEventStore
{
    private const int SchemaVersion = 1;
    private const int BusyTimeoutMs = 5000;

    private const string SelectColumns = """
        event_id, occurred_at, received_at, kind, event_type,
        emitter_name, emitter_version, adapter_version, original_event,
        session_id, prompt_id, tool_use_id, agent_id, parent_id,
        repository_id, worktree_id, raw_ref, payload
        """;

    private const string OrderByClause = "ORDER BY occurred_at, received_at, event_id";

    private static readonly string[] SchemaStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS meta (
          key   TEXT PRIMARY KEY,
          value TEXT NOT NULL
        )
        """,
        """
        CREATE TABLE IF NOT EXISTS events (
          event_id        TEXT PRIMARY KEY,
          occurred_at     TEXT NOT NULL,
          received_at     TEXT NOT NULL,
          kind            TEXT NOT NULL CHECK (kind IN ('observed','asserted','derived')),
          event_type      TEXT NOT NULL,
          emitter_name    TEXT NOT NULL,
          emitter_version TEXT,
          adapter_version TEXT NOT NULL,
          original_event  TEXT,
          session_id      TEXT,
          prompt_id       TEXT,
          tool_use_id     TEXT,
          agent_id        TEXT,
          parent_id       TEXT,
          repository_id   TEXT,
          worktree_id     TEXT,
          raw_ref         TEXT,
          payload         TEXT NOT NULL
        )
        """,
        "CREATE INDEX IF NOT EXISTS ix_events_occurred ON events (occurred_at)",
        "CREATE INDEX IF NOT EXISTS ix_events_session  ON events (session_id, occurred_at)",
        "CREATE INDEX IF NOT EXISTS ix_events_repo     ON events (repository_id, occurred_at)",
        "CREATE INDEX IF NOT EXISTS ix_events_type     ON events (event_type)"
    ];

    private readonly string _dbPath;

    public EventStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".tracewright",
        "tracewright.db");

    public void Append(EventEnvelope envelope)
    {
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection();
        EnsureSchema(connection);
        CheckSchemaVersion(connection);

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO events ({SelectColumns})
            VALUES (
                $event_id, $occurred_at, $received_at, $kind, $event_type,
                $emitter_name, $emitter_version, $adapter_version, $original_event,
                $session_id, $prompt_id, $tool_use_id, $agent_id, $parent_id,
                $repository_id, $worktree_id, $raw_ref, $payload
            )
            """;
        AddEnvelopeParameters(command, envelope);
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<EventEnvelope> Query(EventQuery query)
    {
        if (!File.Exists(_dbPath))
        {
            return [];
        }

        using var connection = OpenConnection();
        CheckSchemaVersion(connection);

        using var command = connection.CreateCommand();
        var where = new List<string>();

        if (query.RepositoryId is not null)
        {
            where.Add("repository_id = $repository_id");
            command.Parameters.AddWithValue("$repository_id", query.RepositoryId);
        }

        if (query.SessionId is not null)
        {
            where.Add("session_id = $session_id");
            command.Parameters.AddWithValue("$session_id", query.SessionId);
        }

        if (query.Since is not null)
        {
            where.Add("occurred_at >= $since");
            command.Parameters.AddWithValue("$since", query.Since);
        }

        if (query.Until is not null)
        {
            where.Add("occurred_at <= $until");
            command.Parameters.AddWithValue("$until", query.Until);
        }

        if (query.EventTypeGlob is not null)
        {
            where.Add(@"event_type LIKE $event_type_glob ESCAPE '\'");
            command.Parameters.AddWithValue("$event_type_glob", GlobToLike(query.EventTypeGlob));
        }

        if (query.Kind is not null)
        {
            where.Add("kind = $kind");
            command.Parameters.AddWithValue("$kind", query.Kind.Value.ToText());
        }

        var sql = new StringBuilder($"SELECT {SelectColumns} FROM events");
        if (where.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", where));
        }
        sql.Append(' ').Append(OrderByClause);
        command.CommandText = sql.ToString();

        return ReadAll(command);
    }

    public IReadOnlyList<EventEnvelope> FindByIdPrefix(string prefix)
    {
        if (!File.Exists(_dbPath))
        {
            return [];
        }

        using var connection = OpenConnection();
        CheckSchemaVersion(connection);

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT {SelectColumns} FROM events
            WHERE substr(event_id, 1, length($prefix)) = $prefix
            {OrderByClause}
            """;
        command.Parameters.AddWithValue("$prefix", prefix);

        return ReadAll(command);
    }

    private static IReadOnlyList<EventEnvelope> ReadAll(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var results = new List<EventEnvelope>();
        while (reader.Read())
        {
            results.Add(ReadEnvelope(reader));
        }
        return results;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();
        ApplyPragmas(connection);
        return connection;
    }

    private static void ApplyPragmas(SqliteConnection connection)
    {
        using (var walCommand = connection.CreateCommand())
        {
            walCommand.CommandText = "PRAGMA journal_mode=WAL";
            walCommand.ExecuteNonQuery();
        }

        using var busyCommand = connection.CreateCommand();
        busyCommand.CommandText = $"PRAGMA busy_timeout={BusyTimeoutMs}";
        busyCommand.ExecuteNonQuery();
    }

    private static void AddEnvelopeParameters(SqliteCommand command, EventEnvelope envelope)
    {
        command.Parameters.AddWithValue("$event_id", envelope.EventId);
        command.Parameters.AddWithValue("$occurred_at", envelope.OccurredAt);
        command.Parameters.AddWithValue("$received_at", envelope.ReceivedAt);
        command.Parameters.AddWithValue("$kind", envelope.Kind.ToText());
        command.Parameters.AddWithValue("$event_type", envelope.EventType);
        command.Parameters.AddWithValue("$emitter_name", envelope.EmitterName);
        command.Parameters.AddWithValue("$emitter_version", (object?)envelope.EmitterVersion ?? DBNull.Value);
        command.Parameters.AddWithValue("$adapter_version", envelope.AdapterVersion);
        command.Parameters.AddWithValue("$original_event", (object?)envelope.OriginalEvent ?? DBNull.Value);
        command.Parameters.AddWithValue("$session_id", (object?)envelope.SessionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$prompt_id", (object?)envelope.PromptId ?? DBNull.Value);
        command.Parameters.AddWithValue("$tool_use_id", (object?)envelope.ToolUseId ?? DBNull.Value);
        command.Parameters.AddWithValue("$agent_id", (object?)envelope.AgentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$parent_id", (object?)envelope.ParentId ?? DBNull.Value);
        command.Parameters.AddWithValue("$repository_id", (object?)envelope.RepositoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$worktree_id", (object?)envelope.WorktreeId ?? DBNull.Value);
        command.Parameters.AddWithValue("$raw_ref", (object?)envelope.RawRef ?? DBNull.Value);
        command.Parameters.AddWithValue("$payload", envelope.Payload);
    }

    private static EventEnvelope ReadEnvelope(SqliteDataReader reader) => new()
    {
        EventId = reader.GetString(0),
        OccurredAt = reader.GetString(1),
        ReceivedAt = reader.GetString(2),
        Kind = EvidenceKindText.Parse(reader.GetString(3)),
        EventType = reader.GetString(4),
        EmitterName = reader.GetString(5),
        EmitterVersion = reader.IsDBNull(6) ? null : reader.GetString(6),
        AdapterVersion = reader.GetString(7),
        OriginalEvent = reader.IsDBNull(8) ? null : reader.GetString(8),
        SessionId = reader.IsDBNull(9) ? null : reader.GetString(9),
        PromptId = reader.IsDBNull(10) ? null : reader.GetString(10),
        ToolUseId = reader.IsDBNull(11) ? null : reader.GetString(11),
        AgentId = reader.IsDBNull(12) ? null : reader.GetString(12),
        ParentId = reader.IsDBNull(13) ? null : reader.GetString(13),
        RepositoryId = reader.IsDBNull(14) ? null : reader.GetString(14),
        WorktreeId = reader.IsDBNull(15) ? null : reader.GetString(15),
        RawRef = reader.IsDBNull(16) ? null : reader.GetString(16),
        Payload = reader.GetString(17)
    };

    private static void EnsureSchema(SqliteConnection connection)
    {
        foreach (var sql in SchemaStatements)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        SeedMetaIfMissing(connection, "schema_version", SchemaVersion.ToString(CultureInfo.InvariantCulture));
        SeedMetaIfMissing(connection, "created_at", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
    }

    private static void SeedMetaIfMissing(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO meta (key, value)
            SELECT $key, $value
            WHERE NOT EXISTS (SELECT 1 FROM meta WHERE key = $key)
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private static void CheckSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = 'schema_version'";
        if (command.ExecuteScalar() is not string text)
        {
            return;
        }

        var version = int.Parse(text, CultureInfo.InvariantCulture);
        if (version > SchemaVersion)
        {
            throw new InvalidOperationException(
                $"tracewright.db schema_version {version} is newer than this build supports " +
                $"(known version {SchemaVersion}). Upgrade tracewright.");
        }
    }

    // translates a `*`-only glob to a SQL LIKE pattern, escaping literal % and _ (spec §7).
    private static string GlobToLike(string glob)
    {
        var sb = new StringBuilder(glob.Length);
        foreach (var c in glob)
        {
            switch (c)
            {
                case '%':
                case '_':
                    sb.Append('\\').Append(c);
                    break;
                case '*':
                    sb.Append('%');
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
