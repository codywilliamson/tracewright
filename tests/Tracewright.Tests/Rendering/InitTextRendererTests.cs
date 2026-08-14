using Tracewright.Core.Onboarding;
using Tracewright.Core.Rendering;

namespace Tracewright.Tests;

public class InitTextRendererTests
{
    [Fact]
    public void Reports_each_step_with_its_status_and_target()
    {
        var lines = InitTextRenderer.Render([
            new InitStep(".tracewright/repo.id", InitStepStatus.Created, "commit this file"),
            new InitStep(".claude/settings.json", InitStepStatus.AlreadyPresent),
        ]);

        Assert.Equal("created   .tracewright/repo.id — commit this file", lines[0]);
        Assert.Equal("unchanged .claude/settings.json", lines[1]);
    }

    [Fact]
    public void Declined_steps_carry_the_instruction_forward()
    {
        var lines = InitTextRenderer.Render([
            new InitStep(".git/hooks/post-commit", InitStepStatus.Declined, "add it yourself"),
        ]);

        Assert.Equal("declined  .git/hooks/post-commit — add it yourself", lines[0]);
    }
}
