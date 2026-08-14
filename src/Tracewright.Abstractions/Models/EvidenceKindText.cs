namespace Tracewright.Abstractions;

/// <summary>
/// lowercase text form of EvidenceKind — matches the events.kind CHECK constraint values verbatim.
/// </summary>
public static class EvidenceKindText
{
    public static string ToText(this EvidenceKind kind) => kind switch
    {
        EvidenceKind.Observed => "observed",
        EvidenceKind.Asserted => "asserted",
        EvidenceKind.Derived => "derived",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown evidence kind")
    };

    public static EvidenceKind Parse(string text) => text switch
    {
        "observed" => EvidenceKind.Observed,
        "asserted" => EvidenceKind.Asserted,
        "derived" => EvidenceKind.Derived,
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, "unknown evidence kind")
    };
}
