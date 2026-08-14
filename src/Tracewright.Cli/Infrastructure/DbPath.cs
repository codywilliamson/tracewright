using Tracewright.Core.Storage;

namespace Tracewright.Cli.Infrastructure;

/// <summary>
/// db path resolution shared by every emit command: TRACEWRIGHT_DB wins when set (testability
/// seam, and protects the real ledger during dogfood verification), else EventStore.DefaultPath.
/// </summary>
public static class DbPath
{
    private const string EnvVarName = "TRACEWRIGHT_DB";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable(EnvVarName) is { Length: > 0 } value
            ? value
            : EventStore.DefaultPath;
}
