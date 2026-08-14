using Tracewright.Core.Onboarding;

namespace Tracewright.Core.Rendering;

/// <summary>one line per step, so `init` says what it touched instead of claiming success.</summary>
public static class InitTextRenderer
{
    public static IReadOnlyList<string> Render(IReadOnlyList<InitStep> steps) =>
        [.. steps.Select(step => Label(step.Status) + step.Target + Suffix(step.Note))];

    private static string Label(InitStepStatus status) => status switch
    {
        InitStepStatus.Created => "created   ",
        InitStepStatus.Updated => "updated   ",
        InitStepStatus.AlreadyPresent => "unchanged ",
        InitStepStatus.Declined => "declined  ",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string Suffix(string? note) => note is null ? "" : $" — {note}";
}
