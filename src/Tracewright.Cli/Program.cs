using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using Tracewright.Abstractions;
using Tracewright.Cli.Commands;
using Tracewright.Cli.Infrastructure;
using Tracewright.Core.Primitives;
using Tracewright.Core.Storage;

// occurred_at is stamped as the first action, before any parsing or DB work (D-016).
var invocationTimestamp = Timestamp.Now();

using var services = new ServiceCollection()
    .AddSingleton<IEventStore>(new EventStore(DbPath.Resolve()))
    .AddSingleton<EmitClaudeCommand>()
    .AddSingleton<EmitGitCommand>()
    .AddSingleton<EmitRawCommand>()
    .AddSingleton<TimelineCommand>()
    .AddSingleton<ShowCommand>()
    .BuildServiceProvider();

var emit = new Command("emit", "record an event into the ledger");
emit.Add(services.GetRequiredService<EmitClaudeCommand>().Build(invocationTimestamp));
emit.Add(services.GetRequiredService<EmitGitCommand>().Build());
emit.Add(services.GetRequiredService<EmitRawCommand>().Build(invocationTimestamp));

var root = new RootCommand("tracewright: local-first evidence ledger for agentic development");
root.Add(emit);
root.Add(services.GetRequiredService<TimelineCommand>().Build());
root.Add(services.GetRequiredService<ShowCommand>().Build());

return root.Parse(args).Invoke();
