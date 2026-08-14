using Microsoft.Extensions.DependencyInjection;
using Tracewright.Abstractions;
using Tracewright.Cli.Commands;
using Tracewright.Cli.Infrastructure;
using Tracewright.Core.Primitives;
using Tracewright.Core.Storage;

// occurred_at is stamped as the first action, before any parsing or DB work (D-016).
var invocationTimestamp = Timestamp.Now();

using var services = new ServiceCollection()
    .AddSingleton<IEventStore>(new EventStore(DbPath.Resolve()))
    .AddSingleton<InitCommand>()
    .AddSingleton<EmitClaudeCommand>()
    .AddSingleton<EmitGitCommand>()
    .AddSingleton<EmitRawCommand>()
    .AddSingleton<TimelineCommand>()
    .AddSingleton<ShowCommand>()
    .AddSingleton<RootCommandFactory>()
    .BuildServiceProvider();

var root = services.GetRequiredService<RootCommandFactory>().Build(invocationTimestamp);
return root.Parse(args).Invoke();
