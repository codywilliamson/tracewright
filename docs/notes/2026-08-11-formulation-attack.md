# Attacking the refined formulation

**Date:** 2026-08-11
**Target:**

> Chronicler is an evidence ledger for agentic software development that can
> progressively distill trustworthy project memory from the history it observes.

The formulation is right. These are the three places it bends under pressure, and
what each one costs.

---

## Attack 1 — "Observes" is doing hidden work: observer relativity

Chronicler itself observes almost nothing. It receives JSON on stdin from processes
claiming to be hooks. `dotnet test` failing is observed *by the Claude Code harness*
and relayed. The only events Chronicler can witness first-hand are git state (the
post-commit adapter can `rev-parse HEAD` itself) and its own ingestion.

So `Observed` cannot mean "Chronicler saw it." The workable definition:

> **Observed** = produced by instrumentation with no language model or human claim
> in the reporting chain. **Asserted** = the informational content originates from
> a model's or human's claim. **Derived** = produced by Chronicler from prior
> events, with links.

This definition decides the edge cases mechanically:

| Case | Kind | Why |
|---|---|---|
| `PostToolUseFailure` for `dotnet test` | Observed | harness relayed a mechanical outcome |
| `Stop` with `last_assistant_message` | Observed event, **assertion material in payload** | turn-end is mechanical; the message content is the model's claim |
| `TaskCompleted(result: success)` | Observed act of assertion | hook fired mechanically; "success" is the agent's claim |
| `git.commit` | Observed | but the commit *message* inside is asserted narrative |
| Human runs `chronicler emit` to record a decision | Asserted | human claim, deterministically recorded |

The pattern: **carrier vs claim.** The envelope's `kind` classifies the claim the
event itself makes ("this happened"). Payloads of observed events routinely *carry*
unextracted assertion material. Turning that material into first-class asserted
events is derivation — later, with links, or explicit emission now.

Cost of ignoring this: we'd label `TaskCompleted(success)` as ground truth and the
discrepancy engine would be comparing assertions against assertions.

## Attack 2 — v0.1's ASSERTED lane is thinner than the dogfood timeline implies

The mock timeline shows `ASSERTED Task: implement event persistence` and
`ASSERTED Agent reports verification succeeded`. **No hook emits those as structured
assertions.** Verified: agent claims live in `last_assistant_message` payloads and
transcript text — inside observed carriers, not as asserted events.

In honest v0.1, the asserted lane contains exactly:
1. whatever a human explicitly records via `chronicler emit`
2. nothing else

The observed lane carries the assertion material (Stop payloads, task results,
prompts) for later extraction. That's fine — but the mock timeline should not be the
acceptance test as written, or v0.1 fails its own demo. The honest v0.1 rendering:

```
10:41:02 OBSERVED  claude.session.started
10:41:18 OBSERVED  claude.prompt.submitted   "implement event persistence"
10:43:51 OBSERVED  claude.tool.failed        Bash: dotnet test
10:45:22 OBSERVED  claude.tool.completed     Bash: dotnet test
10:45:40 OBSERVED  claude.turn.completed     last message: "…tests pass…"
10:46:01 OBSERVED  git.commit                83bd41a
```

Still audit-useful — the failed→passed→commit sequence is visible, which is the
point. The `ASSERTED`/`DERIVED` lines arrive when extraction or reflection exists.
(Related verified limitation: exit codes aren't structured in hook payloads —
success/failure binary only. See the hook-surface note, Q6.)

## Attack 3 — "Trustworthy" requires representing absence

The ledger is only trustworthy about what it captured, and capture has silent
boundaries: editor changes outside the agent, terminals without hooks, machines
without Chronicler, sessions before onboarding, hooks disabled for a day.

An audit system whose gaps are invisible is *worse* than no audit system — it
converts "we don't know" into a confident-looking "nothing happened." §2.3 already
says Unknown is a valid answer; the same principle must apply to the timeline:

- A commit with no correlated session activity renders as **unattributed**, visibly —
  not silently interleaved as if explained. (It's also the cheapest discrepancy
  detector we get, free in v0.1.)
- Instrumentation coverage is itself evidence: adapters installed, when, versions.
  `doctor` eventually reports what Chronicler *cannot* see, not just what's broken.

Cost of ignoring this: the first time the timeline confidently omits a hotfix made
in vim, trust in every other line evaporates.

---

## What survives

The formulation, amended in one word each:

> Chronicler is an evidence ledger for agentic software development that can
> progressively distill trustworthy project memory from the history it **records**
> — knowing who observed what, carrying whose claims, and showing where its own
> sight ends.

None of this expands v0.1. Attack 1 is a definition (free), Attack 2 subtracts
scope (the mock timeline), Attack 3 is one rendering rule (unattributed commits
display as such).
