# Tracewright

**A local-first evidence ledger for agentic software development.**

Tracewright captures lifecycle events from coding agents and repository activity
from Git, classifies every record by provenance, and produces a trustworthy
timeline of what actually happened during development. Over time, selected
evidence can be distilled into portable project memory.

> Agent output is not truth. Git is not intent. Test results are not rationale.
> Memory without provenance becomes lore.

Tracewright's first job is to preserve evidence without pretending every piece of
evidence has the same truth value. Every record is classified:

- **Observed** — emitted by a deterministic mechanism, no model or human claim in
  the reporting chain
- **Asserted** — an explicit claim by a human or model, preserved as a claim
- **Derived** — produced by Tracewright from other evidence, with links back to it

And the timeline shows where its sight ends: activity Tracewright cannot
attribute renders as *unattributed* — truthful evidence, not a failure state.

## Sequencing

1. **Auditability first** — prove what happened during an agentic coding session
2. **Project memory later** — distill trustworthy history once it exists
3. **Context engineering after that** — give agents the project's memory, not the
   project's entire history

Tracewright is an evidence ledger first and a memory system second.

## Status

**Design phase — building in public. Nothing is built yet.**

| Document | What it is |
|---|---|
| [`docs/specs/2026-08-11-v0.1-design.md`](docs/specs/2026-08-11-v0.1-design.md) | **The v0.1 spec** — envelope, schema, adapters, timeline. |
| [`docs/decisions.md`](docs/decisions.md) | Decision log — settled decisions and what's deliberately open. |
| [`docs/seed.md`](docs/seed.md) | The original project seed (under the working name *Chronicler*), preserved verbatim. |
| [`docs/notes/`](docs/notes/) | Dated working notes: the challenge pass on the seed, the verified Claude Code hook surface, and the formulation pressure test. |

v0.1 is five pieces: an append-only event store (SQLite), a single ingest path
(`tracewright emit`), a Claude Code hook adapter, a Git post-commit adapter, and
a timeline. The first milestone is recursive: install Tracewright into this
repository so the rest of its development is recorded by the tool itself.
