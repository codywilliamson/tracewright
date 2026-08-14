# tracewright

## Agent skills

### Issue tracker

Issues live in this repo's GitHub Issues (`codywilliamson/tracewright`), managed via the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

Default label vocabulary: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### README

`README.md` is the user-facing contract: status, install, and every command.
`ReadmeCommandCoverageTests` fails the build when a command goes undocumented;
status and behaviour claims are on you to update in the same commit that changes them.

### Domain docs

Single-context: `CONTEXT.md` at the repo root; decisions live in `docs/decisions.md` (no `docs/adr/`). See `docs/agents/domain.md`.
