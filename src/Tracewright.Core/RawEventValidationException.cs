namespace Tracewright.Core;

/// <summary>
/// thrown by RawEventAdapter for caller mistakes (bad JSON, forbidden/unknown fields, bad --kind).
/// Human-facing: the CLI prints Message to stderr and exits nonzero — never swallowed.
/// </summary>
public sealed class RawEventValidationException(string message) : Exception(message);
