namespace Tracewright.Cli.Infrastructure;

/// <summary>
/// best-effort error logging for hook-context commands (spec §9) — claude and git adapters must
/// never fail the caller, so failures land here instead of on stderr/exit code. Logging itself
/// is best-effort: a broken log must never break the hook.
/// </summary>
public static class FailureLog
{
    public static void Append(string fileName, Exception ex)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".tracewright", "logs");
            Directory.CreateDirectory(directory);

            var line = $"{DateTimeOffset.UtcNow:O} {ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(directory, fileName), line);
        }
        catch
        {
            // best-effort — swallow, never let logging itself fail the hook
        }
    }
}
