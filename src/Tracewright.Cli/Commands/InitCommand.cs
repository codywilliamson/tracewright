using System.CommandLine;
using Tracewright.Core.Onboarding;
using Tracewright.Core.Rendering;

namespace Tracewright.Cli.Commands;

/// <summary>
/// `tracewright init` — onboards the repository containing the current directory (D-025).
/// Human-facing: refusing to run outside a repository is an error, not a silent no-op.
/// </summary>
public sealed class InitCommand
{
    public Command Build()
    {
        var command = new Command("init", "set up Tracewright capture in this repository");
        command.SetAction(_ => Run());
        return command;
    }

    private static int Run()
    {
        try
        {
            var steps = RepositoryInitializer.Run(Environment.CurrentDirectory);
            foreach (var line in InitTextRenderer.Render(steps))
            {
                Console.WriteLine(line);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}
