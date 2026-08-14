using Tracewright.Cli.Rendering;
using Tracewright.Core.Repositories;
using Tracewright.Core.Primitives;
using Tracewright.Core.Projections;
using Tracewright.Abstractions;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Tracewright.Cli.Commands;

/// <summary>
/// `tracewright timeline` — chronological read-side projection (spec §7). Filters are additive;
/// with none given at all, defaults to the last 24h across the whole ledger (D-021). Bare --repo
/// narrows to the repository resolved from cwd; --repo &lt;id&gt; is explicit — no silent auto-scoping.
/// </summary>
public sealed class TimelineCommand(IEventStore store)
{
    private const string DefaultWindow = "24h";

    public Command Build()
    {
        var repoOption = new Option<string?>("--repo") { Arity = ArgumentArity.ZeroOrOne };
        var sessionOption = new Option<string?>("--session");
        var sinceOption = new Option<string?>("--since");
        var untilOption = new Option<string?>("--until");
        var typeOption = new Option<string?>("--type");
        var kindOption = new Option<string?>("--kind");

        var command = new Command("timeline", "chronological projection of the event ledger");
        command.Add(repoOption);
        command.Add(sessionOption);
        command.Add(sinceOption);
        command.Add(untilOption);
        command.Add(typeOption);
        command.Add(kindOption);

        command.SetAction(parseResult =>
            Run(parseResult, repoOption, sessionOption, sinceOption, untilOption, typeOption, kindOption));
        return command;
    }

    private int Run(
        ParseResult parseResult,
        Option<string?> repoOption,
        Option<string?> sessionOption,
        Option<string?> sinceOption,
        Option<string?> untilOption,
        Option<string?> typeOption,
        Option<string?> kindOption)
    {
        var anyFilterGiven = parseResult.GetResult(repoOption) is not null
            || parseResult.GetResult(sessionOption) is not null
            || parseResult.GetResult(sinceOption) is not null
            || parseResult.GetResult(untilOption) is not null
            || parseResult.GetResult(typeOption) is not null
            || parseResult.GetResult(kindOption) is not null;

        if (!TryResolveRepository(parseResult, repoOption, out var repositoryId, out var repoError))
        {
            Console.Error.WriteLine(repoError);
            return 1;
        }

        if (!TryResolveWindow(parseResult, sinceOption, untilOption, anyFilterGiven, out var since, out var until, out var timeError))
        {
            Console.Error.WriteLine(timeError);
            return 1;
        }

        if (!TryResolveKind(parseResult, kindOption, out var kind, out var kindError))
        {
            Console.Error.WriteLine(kindError);
            return 1;
        }

        var sessionId = parseResult.GetValue(sessionOption);
        var query = new EventQuery
        {
            RepositoryId = repositoryId,
            SessionId = sessionId,
            Since = since,
            Until = until,
            EventTypeGlob = parseResult.GetValue(typeOption),
            Kind = kind,
        };

        var envelopes = store.Query(query);

        if (envelopes.Count == 0)
        {
            Console.WriteLine("no events recorded");
            return 0;
        }

        TimelineConsoleRenderer.Render(TimelineProjection.Build(envelopes));
        PrintUnattributedNoteIfNeeded(query, sessionId);
        return 0;
    }

    private static bool TryResolveRepository(
        ParseResult parseResult, Option<string?> repoOption, out string? repositoryId, out string error)
    {
        error = "";
        repositoryId = null;

        var repoResult = parseResult.GetResult(repoOption);
        if (repoResult is null)
        {
            return true;
        }

        if (repoResult.Tokens.Count > 0)
        {
            repositoryId = parseResult.GetValue(repoOption);
            return true;
        }

        repositoryId = RepositoryResolver.Resolve(Environment.CurrentDirectory);
        if (repositoryId is not null)
        {
            return true;
        }

        error = "error: --repo given with no value, and no repository found from the current directory";
        return false;
    }

    private static bool TryResolveWindow(
        ParseResult parseResult, Option<string?> sinceOption, Option<string?> untilOption, bool anyFilterGiven,
        out string? since, out string? until, out string error)
    {
        error = "";
        since = null;
        until = null;

        var now = DateTimeOffset.UtcNow;
        try
        {
            if (parseResult.GetResult(sinceOption) is not null)
            {
                since = RelativeTime.Parse(parseResult.GetValue(sinceOption)!, now);
            }

            if (parseResult.GetResult(untilOption) is not null)
            {
                until = RelativeTime.Parse(parseResult.GetValue(untilOption)!, now);
            }

            if (!anyFilterGiven)
            {
                since = RelativeTime.Parse(DefaultWindow, now);
            }

            return true;
        }
        catch (FormatException ex)
        {
            error = $"error: {ex.Message}";
            return false;
        }
    }

    private static bool TryResolveKind(
        ParseResult parseResult, Option<string?> kindOption, out EvidenceKind? kind, out string error)
    {
        error = "";
        kind = null;

        var kindResult = parseResult.GetResult(kindOption);
        if (kindResult is null)
        {
            return true;
        }

        var kindValue = parseResult.GetValue(kindOption)!;
        try
        {
            kind = EvidenceKindText.Parse(kindValue);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            error = $"error: invalid --kind value '{kindValue}' (expected observed, asserted, or derived)";
            return false;
        }
    }

    // --session excludes by correlation; when unattributed events share the window, say so
    // instead of silently narrowing the view (spec §8) — needs a second query without --session.
    private void PrintUnattributedNoteIfNeeded(EventQuery query, string? sessionId)
    {
        if (sessionId is null)
        {
            return;
        }

        var withoutSession = query with { SessionId = null };
        var unattributedCount = store.Query(withoutSession).Count(e => e.SessionId is null);
        var note = TimelineProjection.FormatUnattributedNote(unattributedCount);
        if (note is null)
        {
            return;
        }

        Console.WriteLine();
        Console.WriteLine(note);
    }
}
