using System.CommandLine;
using Tracewright.Cli;
using Tracewright.Core;

// occurred_at is stamped as the first action, before any parsing or DB work (D-016).
var invocationTimestamp = Timestamp.Now();

var emit = new Command("emit", "record an event into the ledger");
emit.Add(EmitClaudeCommand.Build(invocationTimestamp));
emit.Add(EmitGitCommand.Build());
emit.Add(EmitRawCommand.Build(invocationTimestamp));

var root = new RootCommand("tracewright: local-first evidence ledger for agentic development");
root.Add(emit);
root.Add(TimelineCommand.Build());
root.Add(ShowCommand.Build());

return root.Parse(args).Invoke();
