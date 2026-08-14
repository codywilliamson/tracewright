namespace Tracewright.Abstractions;

/// <summary>
/// append-only event store contract (spec §2). Append lazy-bootstraps on first use (D-024);
/// read methods never create the store — a missing ledger reads as empty.
/// </summary>
public interface IEventStore
{
    void Append(EventEnvelope envelope);

    IReadOnlyList<EventEnvelope> Query(EventQuery query);

    IReadOnlyList<EventEnvelope> FindByIdPrefix(string prefix);
}
