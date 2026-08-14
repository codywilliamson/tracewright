using Tracewright.Core.Primitives;

namespace Tracewright.Tests;

public sealed class RelativeTimeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parse_24h_subtracts_24_hours_from_now()
    {
        Assert.Equal(Timestamp.Format(Now.AddHours(-24)), RelativeTime.Parse("24h", Now));
    }

    [Fact]
    public void Parse_7d_subtracts_7_days_from_now()
    {
        Assert.Equal(Timestamp.Format(Now.AddDays(-7)), RelativeTime.Parse("7d", Now));
    }

    [Fact]
    public void Parse_30m_subtracts_30_minutes_from_now()
    {
        Assert.Equal(Timestamp.Format(Now.AddMinutes(-30)), RelativeTime.Parse("30m", Now));
    }

    [Fact]
    public void Parse_passes_through_iso_8601()
    {
        Assert.Equal("2026-08-01T00:00:00.0000000Z", RelativeTime.Parse("2026-08-01T00:00:00Z", Now));
    }

    [Theory]
    [InlineData("not-a-time")]
    [InlineData("24x")]
    [InlineData("h24")]
    [InlineData("")]
    public void Parse_throws_on_invalid_input(string input)
    {
        Assert.Throws<FormatException>(() => RelativeTime.Parse(input, Now));
    }
}
