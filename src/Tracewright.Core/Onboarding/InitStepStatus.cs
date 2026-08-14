namespace Tracewright.Core.Onboarding;

public enum InitStepStatus
{
    Created,
    Updated,
    AlreadyPresent,

    /// <summary>something was already there that init refuses to overwrite; Note says what to do.</summary>
    Declined,
}
