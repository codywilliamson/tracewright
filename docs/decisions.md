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

**D-014 — Trust boundary is the local machine; `kind` classifies provenance semantics, not tamper-resistance.** *(2026-08-11)*
Tracewright is not tamper-proof and does not pretend to be; anything on the machine
can pipe JSON at any `emit` command. Defaults encode the epistemic model instead:
`emit raw` defaults to `kind: asserted`; claiming `observed` requires an explicit
`--kind observed`. A human recording a note falls into asserted without thinking;
claiming observation is a deliberate act; future deterministic integrations keep a
legitimate escape hatch.
*Why:* EvidenceKind is about provenance semantics, not cryptographic trustworthiness — pretending otherwise is theater.
*Evidence:* design grilling 2026-08-11, Q1.

**D-015 — Raw ingress is itself an adapter; Tracewright-owned envelope fields are stamped, never delegated.** *(2026-08-11)*
`emit raw` runs as adapter `tracewright.raw`. Tracewright always stamps `event_id`,
`received_at`, `adapter_version`; caller-supplied values for those are **rejected,
not silently overwritten**. Caller supplies kind, event_type, emitter identity,
correlation ids, `occurred_at` (defaults to invocation time), raw_ref, payload.
Historical replay/import, if ever needed, is a separate future ingest mode — not a
loosening of `emit raw`.
*Why:* no adapter-less loophole in the provenance model; silent overwrite hides caller bugs an audit would later trip over.
*Evidence:* design grilling 2026-08-11, Q7.

**D-016 — Dual timestamps; best-effort ordering; no corrective heuristics without evidence.** *(2026-08-11)*
`occurred_at` is stamped as the adapter's first action on invocation, before parsing
or DB work; `received_at` is the persistence timestamp. Async hooks can in principle
persist out of order; v0.1 renders best-effort chronological order and adds no
rendering heuristics unless dogfooding shows real inversions — the evidence for that
fix will be in the ledger itself.
*Evidence:* design grilling 2026-08-11, Q2.

**D-017 — Chronology wins; grouping is presentation metadata, never permission to reorder.** *(2026-08-11)*
The timeline renders strict best-effort chronological order. Session/repository
headers reprint whenever the stream switches; interleaved sessions render as
interleaved — concurrency is evidence, and grouping it away would obscure it.
*Evidence:* design grilling 2026-08-11, Q3.

**D-018 — `docs/decisions.md` is the canonical decision log; no `docs/adr/`.** *(2026-08-11)*
Agent-facing docs point here. The log already has IDs, cross-references, and
supersede-don't-edit semantics; migrating to per-file ADRs buys churn, and two
decision systems create permanent ambiguity.
*Evidence:* design grilling 2026-08-11, Q4.

**D-019 — Invariant: Tracewright preserves uncertainty rather than normalizing it away.** *(2026-08-11)*
Architectural invariant, not an edge-case collection. Manifestations: carrier vs
claim (D-012), nullable correlation ids (D-003), unattributed/unanchored rendering
(D-010), best-effort ordering without corrective heuristics (D-016),
asserted-by-default raw ingress (D-014), verbatim payload retention ahead of any
derivation (D-006). Every point where Tracewright could guess, tidy, or coerce is a
point where the ledger stops being trustworthy.
*Evidence:* design grilling 2026-08-11, wrap-up.

**D-020 — Raw ingress semantics, completed.** *(2026-08-11, extends D-014, D-015)*
`emit raw` rejects `kind: derived` — derived means "Tracewright produced it from
other evidence, with links," which is definitionally false for any external caller;
the kind stays reserved for in-process derivation. `event_type` is free-form
(convention, not enforcement: adapters own `claude.*` and `git.*`, `tracewright.*`
is reserved for future derived events, manual events use unprefixed dotted names
like `note.recorded`). `emitter_name` defaults to `manual` when omitted.
*Why:* observed-via-raw has a legitimate future caller; derived-via-raw has none. Prefix enforcement would be the tamper-theater D-014 declined — the adapter stamp already preserves provenance.
*Evidence:* design grilling 2026-08-11, Q8–Q10.

**D-021 — The timeline's default view is the whole ledger; narrowing is a deliberate act.** *(2026-08-11)*
No filter shows all repositories plus unanchored events (24h window). Bare `--repo`
resolves the repository by walking up from cwd (same resolution adapters use);
`--repo <id>` is explicit. No silent auto-scoping to the current repo.
*Why:* auto-scoping would render the timeline narrower than the evidence without saying so — a D-019 violation in miniature.
*Evidence:* design grilling 2026-08-11, Q11.

**D-022 — `repo.id` is an opaque unique string, not a ULID.** *(2026-08-11)*
Repository identity needs uniqueness and stability, not sortability. Manual
onboarding: `uuidgen > .tracewright/repo.id`, commit it. No generator command;
no auto-creation from hooks (a git hook must never write into the working tree
as a side effect). `event_id` keeps ULID for its real reason.
*Evidence:* design grilling 2026-08-11, Q12.

**D-023 — Every `post-commit` firing is one `git.commit` event; no dedup, no lineage.** *(2026-08-11)*
Amends, cherry-picks, and rebases produce multiple events with distinct shas —
each commit-object creation genuinely happened, and dedup would be derivation by
stealth. `post-rewrite` capture (old→new lineage) is deferred; revisit if amends
prove common during dogfooding.
*Evidence:* design grilling 2026-08-11, Q13.

**D-024 — The store lazy-bootstraps; an empty ledger is a truthful state.** *(2026-08-11)*
Any write path creates `~/.tracewright/`, the database, and the schema on first
use — adapters run from hooks that must never fail and cannot depend on a human
setup step. Read commands on a missing/empty store report "no events recorded"
and exit 0. Migration check on every open.
*Evidence:* design grilling 2026-08-11, Q14.

---

## Open (deliberately)

- Git hook interop: `core.hooksPath` vs dispatcher/chaining (commit-guard sets it
  wholesale; Tracewright must coexist with it)
- Promotion mechanism (D-004 defers it)
- What a "Tracewright Session" projection means (D-003 defers until real data)
- Whether `CLAUDE*` env at post-commit time carries a usable session marker
  (verify during implementation)
- Codex adapter (validates protocol independence; post-v0.1)
- Historical replay/import ingest mode (explicitly not `emit raw` — D-015; future)
- `post-rewrite` capture for rewrite lineage (D-023 defers; revisit if amends are common)
