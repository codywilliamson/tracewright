using Tracewright.Core;

namespace Tracewright.Tests;

public sealed class RepositoryResolverTests : IDisposable
{
    private readonly string _tempDir;

    public RepositoryResolverTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Resolve_finds_marker_in_starting_directory()
    {
        WriteMarker(_tempDir, "repo-abc  \n");

        Assert.Equal("repo-abc", RepositoryResolver.Resolve(_tempDir));
    }

    [Fact]
    public void Resolve_walks_up_to_find_marker()
    {
        WriteMarker(_tempDir, "repo-root");
        var nested = Path.Combine(_tempDir, "a", "b", "c");
        Directory.CreateDirectory(nested);

        Assert.Equal("repo-root", RepositoryResolver.Resolve(nested));
    }

    [Fact]
    public void Resolve_returns_null_when_no_marker_found()
    {
        var nested = Path.Combine(_tempDir, "a", "b");
        Directory.CreateDirectory(nested);

        Assert.Null(RepositoryResolver.Resolve(nested));
    }

    private static void WriteMarker(string directory, string content)
    {
        var markerDir = Path.Combine(directory, ".tracewright");
        Directory.CreateDirectory(markerDir);
        File.WriteAllText(Path.Combine(markerDir, "repo.id"), content);
    }
}
