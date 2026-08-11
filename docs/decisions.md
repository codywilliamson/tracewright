# Decision log

Manually maintained until Chronicler can record its own. One entry per settled
decision; supersede, don't edit. Evidence pointers reference the docs in this repo.

---

**D-001 — Audit is the v0.1 wedge; memory is the payoff.**
Progression: CAPTURE → AUDIT → DISTILL → RETRIEVE → SYNTHESIZE. v0.1 answers "what
did the agent workflow actually do, and what evidence proves it?" Memory features
wait for a corpus.
*Why:* audit is useful on the second event; memory has a cold-start problem.
*Evidence:* notes/2026-08-10-first-pass-analysis.md §9; design discussion 2026-08-11.

**D-002 — Epistemic classification on the envelope from the first event.**
Every event carries `kind: observed | asserted | derived`. Observed = no model or
human claim in the reporting chain. Asserted = content originates from a model/human
claim. Derived = produced by Chronicler, with links to inputs. Unknown is a query
result, not an event kind. Assertions are evidence; they just aren't observations.
*Why:* discrepancy detection is impossible without it; retrofitting epistemology is awful.
*Evidence:* first-pass-analysis §4; formulation-attack Attack 1 (carrier vs claim).

**D-003 — Sessions are projections, not lifecycle objects Chronicler owns.**
No foundational `session start/end`. Events carry emitter correlation ids
(session_id → prompt_id → tool_use_id → agent_id); "session" is a projection over
them. Don't define the projection precisely until real event data exists.
*Why:* crashes, worktrees, parallel terminals, and nesting stop being special cases.
*Evidence:* first-pass-analysis §3; hook-surface note Q2 confirms the id spine exists.

**D-004 — Two stores: local evidence, portable project memory.**
Raw high-volume, potentially sensitive capture stays in `~/.chronicler/` and never
leaves the machine. A small distilled layer (decisions, assumptions, failures,
task↔commit links) lives in-repo and may be committed — making project memory
PR-reviewable. Promotion (evidence → candidate → promoted memory) is a real concept
whose mechanism is deliberately unresolved; not in v0.1.
*Why:* resolves portability-vs-privacy without choosing a side.
*Evidence:* first-pass-analysis §6; design discussion 2026-08-11 §4.

**D-005 — v0.1 is five things.**
Event store; `chronicler emit` ingest; Claude Code hook adapter; git post-commit
hook; `chronicler timeline`. Milestone: install into this repo and produce a
trustworthy timeline correlating Claude Code activity with git activity.
Explicitly out: ask, context, derivation, promotion, daemon, Codex adapter, Durable
Task, embeddings, projects, `record` commands, discrepancy engine beyond trivial.
*Evidence:* design discussion 2026-08-11 §6.

**D-006 — Normalize narrowly, preserve raw payloads verbatim.**
Envelope carries type, kind, correlation, emitter identity (adapter + versions +
original event name). Full source payload stored as JSON. No lifecycle ontology; no
union of emitter taxonomies. Fields earn indexed columns when a query needs them.
*Evidence:* design discussion 2026-08-11 §7–8; hook-surface note Q8.

**D-007 — No daemon, no Durable Task, no NativeAOT commitment in v0.1.**
Hooks invoke the CLI directly; measure before optimizing. Claude Code's `async: true`
hooks mean capture latency never blocks the agent; HTTP hooks are the ready-made
daemon path if ever justified.
*Evidence:* design discussion 2026-08-11 §10–11; hook-surface note Q9.

**D-008 — Chronicler records external task identity; it does not own a Task concept.**
The harness owns tasks (`TaskCreated`/`TaskCompleted`); Chronicler observes them.
Same stance for any emitter concept: record identity, don't adopt ontology.
*Evidence:* hook-surface note Q4.

**D-009 — Events are evidence; knowledge is a projection over evidence.**
Everything enters through one ingest path (a decision is just an event), but the
long-term query model is not "query event rows" — Decision/knowledge objects are
projections built from evidence events, citing them.
*Evidence:* design discussion 2026-08-11 §12.

**D-010 — The timeline must represent absence.**
Uncorrelated commits render as unattributed, visibly. Instrumentation coverage is
itself evidence. The audit must show where its sight ends.
*Evidence:* formulation-attack Attack 3.

**D-011 — Renamed: Chronicler → Tracewright.** *(2026-08-11)*
Public/project name is Tracewright. "Chronicler" was collision-heavy (audit
trails, agent history, Chronicle-style naming). Tracewright implies a crafted,
inspectable trace rather than generic "AI memory" — which matches the positioning:
evidence ledger first, memory system second. Avoid "persistent memory for coding
agents" as the first framing; that space is noisy and overhyped. Entries D-001…
D-010 predate the rename and are left as written.
*Evidence:* design discussion 2026-08-11.

**D-012 — `kind` classifies why the record exists, not the truth of its payload.**
*(2026-08-11, extends D-002)*
Observed events routinely carry assertion material in their payloads
(`TaskCompleted(result: success)`, `last_assistant_message`, commit messages).
Payload claim material becomes first-class asserted evidence only via derivation
(with links) or explicit emission. Prevents labeling agents' claims as ground truth.
*Evidence:* notes/2026-08-11-formulation-attack.md Attack 1 (carrier vs claim).

**D-013 — v0.1 acceptance is constrained to verified capture.** *(2026-08-11,
extends D-005, D-010)*
No structured exit codes (success/failure binary via PostToolUse vs
PostToolUseFailure). No structured agent assertions. Correlation only via shared
identifiers, never heuristics. Unattributed/unknown states render explicitly.
Milestone: a trustworthy timeline of this repo's own development.
*Evidence:* formulation-attack Attack 2; specs/2026-08-11-v0.1-design.md.

---

## Open (deliberately)

- Git hook interop: `core.hooksPath` vs dispatcher/chaining (commit-guard sets it
  wholesale; Tracewright must coexist with it)
- Promotion mechanism (D-004 defers it)
- What a "Tracewright Session" projection means (D-003 defers until real data)
- Whether `CLAUDE*` env at post-commit time carries a usable session marker
  (verify during implementation)
- Codex adapter (validates protocol independence; post-v0.1)
