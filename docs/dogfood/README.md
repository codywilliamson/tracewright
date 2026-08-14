# Dogfood evidence

Tracewright records its own development. These are point-in-time exports from the
ledger that captured the work in this repository — evidence that the tool does what
the spec claims, checked in so it is reviewable without running anything.

| File | What it is |
|---|---|
| [`2026-08-14-timeline.txt`](2026-08-14-timeline.txt) | `tracewright timeline --repo` output: Claude Code hook events and git commits, in chronological order. |
| [`2026-08-14-emit-git-failures.log`](2026-08-14-emit-git-failures.log) | `~/.tracewright/logs/emit-git.log` — post-commit firings outside a git repo. Every one exited 0; the hook never broke a commit (spec §6). |

Regenerate the timeline export with:

```sh
NO_COLOR=1 tracewright timeline --repo > docs/dogfood/$(date +%F)-timeline.txt
```

## Why the database itself is not here

The ledger lives at `~/.tracewright/tracewright.db` and stays there (D-004, spec §9).
It is not gitignored by accident — event payloads are stored verbatim, and verbatim
means:

- `git.commit` events carry `env_hints`, which includes a live
  **`CLAUDE_CODE_MESSAGING_TOKEN`** value
- `claude.*` events carry `last_assistant_message`, transcript paths, and full tool
  inputs — the raw content of a working session

The timeline projection renders envelope metadata only (timestamp, kind, event type,
short id, correlation), never payloads, which is exactly why it is safe to commit and
the store is not. Payload redaction is an open item in the spec; until it exists,
exports are the only thing that leaves the machine.
