using Tracewright.Abstractions;
using Tracewright.Core;

namespace Tracewright.Tests;

public class EvidenceKindTextTests
{
    [Theory]
    [InlineData(EvidenceKind.Observed, "observed")]
    [InlineData(EvidenceKind.Asserted, "asserted")]
    [InlineData(EvidenceKind.Derived, "derived")]
    public void ToText_and_Parse_round_trip(EvidenceKind kind, string text)
    {
        Assert.Equal(text, kind.ToText());
        Assert.Equal(kind, EvidenceKindText.Parse(text));
    }

    [Fact]
    public void Parse_rejects_unknown_text()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EvidenceKindText.Parse("unknown"));
    }
}
