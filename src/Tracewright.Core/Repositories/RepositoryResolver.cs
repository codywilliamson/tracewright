namespace Tracewright.Core.Repositories;

/// <summary>
/// resolves repository identity by walking up from a starting directory looking for
/// .tracewright/repo.id (spec §6, D-022) — the same resolution every adapter and the
/// future timeline command uses. Never creates the marker; that's manual onboarding only.
/// </summary>
public static class RepositoryResolver
{
    private const string MarkerRelativePath = ".tracewright/repo.id";

    public static string? Resolve(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, MarkerRelativePath);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate).Trim();
            }

            dir = dir.Parent;
        }

        return null;
    }
}
