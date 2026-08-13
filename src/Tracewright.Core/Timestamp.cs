using System.Globalization;

namespace Tracewright.Core;

/// <summary>
/// ISO-8601 UTC formatting, consistently "Z"-suffixed (not "+00:00") across every envelope timestamp.
/// </summary>
public static class Timestamp
{
    public static string Now() => Format(DateTimeOffset.UtcNow);

    public static string Format(DateTimeOffset value) =>
        value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
}
