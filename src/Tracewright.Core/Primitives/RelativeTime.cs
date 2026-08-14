using System.Globalization;
using System.Text.RegularExpressions;

namespace Tracewright.Core.Primitives;

/// <summary>
/// parses --since/--until values (spec §7 rule 5): relative durations (30m, 24h, 7d) measured
/// back from "now", or ISO-8601 passthrough. Always returns a UTC ISO string for EventQuery.
/// </summary>
public static partial class RelativeTime
{
    public static string Parse(string input, DateTimeOffset now)
    {
        var match = RelativePattern().Match(input);
        if (match.Success)
        {
            var amount = int.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
            var unit = match.Groups["unit"].Value.ToLowerInvariant();
            var duration = unit switch
            {
                "m" => TimeSpan.FromMinutes(amount),
                "h" => TimeSpan.FromHours(amount),
                "d" => TimeSpan.FromDays(amount),
                _ => throw new FormatException($"unknown relative time unit '{unit}'"),
            };
            return Timestamp.Format(now - duration);
        }

        if (DateTimeOffset.TryParse(
                input, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return Timestamp.Format(parsed);
        }

        throw new FormatException($"invalid time value '{input}' (expected relative like 24h/7d/30m, or ISO-8601)");
    }

    [GeneratedRegex(@"^(?<amount>\d+)(?<unit>[mhd])$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativePattern();
}
