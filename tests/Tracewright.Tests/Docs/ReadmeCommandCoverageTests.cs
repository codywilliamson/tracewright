using System.CommandLine;
using Tracewright.Cli.Commands;
using Tracewright.Core.Storage;

namespace Tracewright.Tests;

// drift guard: every command the CLI exposes must appear in the README. adding a
// command without documenting it fails here rather than rotting in the docs.
public class ReadmeCommandCoverageTests
{
    [Fact]
    public void ReadmeDocumentsEveryCommand()
    {
        var readme = File.ReadAllText(Path.Combine(RepositoryRoot(), "README.md"));

        var undocumented = CommandPaths(BuildRoot(), "tracewright")
            .Where(path => !readme.Contains(path, StringComparison.Ordinal))
            .ToList();

        Assert.Empty(undocumented);
    }

    // Build() never touches the store, so the path is irrelevant here.
    private static RootCommand BuildRoot()
    {
        var store = new EventStore(Path.Combine(Path.GetTempPath(), "unused.db"));
        return new RootCommandFactory(
            new EmitClaudeCommand(store),
            new EmitGitCommand(store),
            new EmitRawCommand(store),
            new TimelineCommand(store),
            new ShowCommand(store)).Build("2026-01-01T00:00:00Z");
    }

    // leaf commands only — `emit` alone isn't runnable, `emit claude` is.
    private static IEnumerable<string> CommandPaths(Command command, string prefix)
    {
        var children = command.Subcommands;
        if (children.Count == 0)
        {
            yield return prefix;
            yield break;
        }

        foreach (var child in children)
        {
            foreach (var path in CommandPaths(child, $"{prefix} {child.Name}"))
            {
                yield return path;
            }
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "README.md")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("README.md not found above the test binary");
    }
}
