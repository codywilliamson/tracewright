namespace Tracewright.Core.Onboarding;

/// <summary>what `tracewright init` did to one path, reported rather than assumed.</summary>
public sealed record InitStep(string Target, InitStepStatus Status, string? Note = null);
