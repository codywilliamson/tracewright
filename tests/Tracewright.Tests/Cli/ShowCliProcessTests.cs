using Tracewright.Core.Storage;
using static Tracewright.Tests.TimelineEnvelopeBuilder;

namespace Tracewright.Tests;

/// <summary>
/// process-level tests for `tracewright show` (spec §10): exact/ambiguous/no-match prefix
/// resolution and exit codes.
/// </summary>
public sealed class ShowCliProcessTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public ShowCliProcessTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "tracewright.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Exact_prefix_match_renders_the_full_envelope_and_exits_zero()
    {
        var store = new EventStore(_dbPath);
        store.Append(Make(
            "2026-08-13T10:00:00.000Z", eventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV",
            payload: """{"tool_name":"Bash"}"""));

        var result = CliProcessRunner.Run(["show", "01ARZ3NDEKTSV4RRFFQ69G5FAV"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("event_id: 01ARZ3NDEKTSV4RRFFQ69G5FAV", result.StdOut);
        Assert.Contains("payload:", result.StdOut);
        Assert.Contains("tool_name", result.StdOut);
    }

    [Fact]
    public void Unique_short_prefix_resolves_to_the_matching_event()
    {
        var store = new EventStore(_dbPath);
        store.Append(Make("2026-08-13T10:00:00.000Z", eventId: "01ARZ3NDEKTSV4RRFFQ69G5FAV"));

        var result = CliProcessRunner.Run(["show", "01ARZ3"], _dbPath);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("01ARZ3NDEKTSV4RRFFQ69G5FAV", result.StdOut);
    }

    [Fact]
    public void No_match_errors_and_exits_nonzero()
    {
        var result = CliProcessRunner.Run(["show", "ZZZZZZ"], _dbPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("no event found", result.StdErr);
    }

    [Fact]
    public void Ambiguous_prefix_lists_the_matches_and_exits_nonzero()
    {
        var store = new EventStore(_dbPath);
        store.Append(Make("2026-08-13T10:00:00.000Z", eventId: "01ARZ3AAAAAAAAAAAAAAAAAAAA"));
        store.Append(Make("2026-08-13T10:00:01.000Z", eventId: "01ARZ3BBBBBBBBBBBBBBBBBBBB"));

        var result = CliProcessRunner.Run(["show", "01ARZ3"], _dbPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ambiguous", result.StdErr);
        Assert.Contains("01ARZ3AAAAAAAAAAAAAAAAAAAA", result.StdErr);
        Assert.Contains("01ARZ3BBBBBBBBBBBBBBBBBBBB", result.StdErr);
    }
}
