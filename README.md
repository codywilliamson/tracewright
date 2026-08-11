# Chronicler

Durable, queryable project memory for agentic development.

Git preserves what changed. Chronicler is an experiment in preserving *why* —
capturing structured evidence about development activity (decisions, assumptions,
failed approaches, verification results, human interventions) and turning it into
memory an agent can be given later.

The long-term question:

> Can we give coding agents relevant project memory without giving them the
> project's entire history?

## Status

**Design only. Nothing is built.**

| Document | What it is |
|---|---|
| [`docs/decisions.md`](docs/decisions.md) | **Start here.** Settled decisions D-001…D-010 and what's deliberately open. |
| [`docs/seed.md`](docs/seed.md) | The original project seed. Preserved verbatim. |
| [`docs/notes/2026-08-10-first-pass-analysis.md`](docs/notes/2026-08-10-first-pass-analysis.md) | Challenge pass on the seed. |
| [`docs/notes/2026-08-10-open-questions.md`](docs/notes/2026-08-10-open-questions.md) | The questions that drove the challenge pass (now mostly settled — see decisions). |
| [`docs/notes/2026-08-11-claude-code-hook-surface.md`](docs/notes/2026-08-11-claude-code-hook-surface.md) | Verified hook surface: events, correlation ids, payloads, caveats. |
| [`docs/notes/2026-08-11-formulation-attack.md`](docs/notes/2026-08-11-formulation-attack.md) | Pressure test of the "evidence ledger" formulation. |

Next step: v0.1 spec (envelope + SQLite schema + adapter mapping), designed against
the verified hook data.
