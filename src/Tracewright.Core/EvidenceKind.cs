namespace Tracewright.Core;

/// <summary>
/// classifies why a record exists, not the truth of its payload (docs/decisions.md D-012).
/// </summary>
public enum EvidenceKind
{
    Observed,
    Asserted,
    Derived
}
