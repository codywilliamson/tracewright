using Tracewright.Core.Rendering;

namespace Tracewright.Tests;

public sealed class ShortEventIdTests
{
    [Fact]
    public void Of_takes_first_four_and_last_two_chars_with_ellipsis_between()
    {
        Assert.Equal("01J9…4F", ShortEventId.Of("01J9XXXXXXXXXXXXXXXXXXXX4F"));
    }

    [Fact]
    public void Of_returns_short_ids_unchanged()
    {
        Assert.Equal("abcdef", ShortEventId.Of("abcdef"));
    }
}
