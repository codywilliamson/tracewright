# Open questions — read this first

**Date:** 2026-08-10
**For:** tomorrow's session

Each question has my recommendation so you can approve, reject, or argue rather
than start from a blank page. Rationale is in
[`2026-08-10-first-pass-analysis.md`](./2026-08-10-first-pass-analysis.md).

The first four are load-bearing — they change the architecture. The rest are
smaller and can be decided as we go.

---

## Blocking

**Q1. Does distilled memory get committed to the repo?**
The seed says history stays in `~/.chronicler` and out of the repo. But then
project memory dies with the machine, and the §17 thesis doesn't hold for anyone
but you.
→ **Recommend: split.** Raw high-volume capture stays machine-local. A small
distilled layer (decisions, assumptions, failures, task↔commit links) is committed
to `.chronicler/memory/`. Solves portability, keeps secrets local, and makes
recorded decisions reviewable in PRs. *This contradicts §10 — biggest thing to argue.*

**Q2. Is the v0.1 wedge audit or memory?**
§2.2 calls Chronicler an audit system; §17 describes a memory system. Same
capture and same store, different read side — so it's a sequencing question, not
a fork. But it determines what gets built first.
→ **Recommend: audit.** Discrepancy detection ("agent said tests passed, the run
exited 1") is deterministic, needs no LLM, and is useful on the second event.
Memory has a cold-start problem you'd feel for weeks. Audit-first is the version
that's useful before it's finished — which is what makes dogfooding actually happen.

**Q3. Does Chronicler own session lifecycle, or observe it?**
Explicit `session start` / `session end` with a tracked active session will break
on worktrees, parallel agents, two terminals, and crashes.
→ **Recommend: observe.** Events carry a `correlation_id` from the emitter (Claude
Code's own session id). A session becomes a projection over shared correlation
ids, not a stateful object. Crash safety, concurrency, and nesting all fall out free.

**Q4. Does every event record observed vs asserted?**
→ **Recommend: yes, on the envelope, from the first event.** §5's discrepancy
detection is not implementable without it. Cheap now; retrofitting means
reclassifying the entire history.

---

## Secondary

**Q5. v0.1 scope.** §20 is ~7 areas. I'd cut it to: event store, `chronicler emit`
(JSON on stdin), Claude Code hook adapter, git post-commit hook, `chronicler
timeline`. No projects, no `init`, no `doctor`, no `record` commands — a decision
is just an event through `emit`. Rationale: cross the §18 dogfooding line as fast
as possible, then let capture inform everything after.

**Q6. Stack.** C#/net10.0 + NativeAOT, mirroring `worktree-commander`
(System.CommandLine, Spectre.Console, xUnit, Directory.Packages.props). This cuts
against your global TypeScript preference — the argument is that hook startup
latency is a real requirement (runs on every commit) and NativeAOT wins there and
on distribution. Worth measuring before committing. NativeAOT means
source-generated JSON and constrained DI — fine if decided now, painful later.

**Q7. Git hook installation.** `commit-guard` sets `core.hooksPath` wholesale.
Chronicler may end up installed alongside it in the same repo, and §6 says don't
destructively replace. Needs a real answer — probably a dispatcher directory that
chains to any pre-existing hook.

**Q8. Is the transcript a first-class evidence source?** §1 dismisses it. I think
that's right as a *store* and wrong as *evidence* — it's the only artifact that
actually contains reasoning. Recommend citing it by pointer (session + message
offset), never copying it into memory.

**Q9. Project entity.** Real, but YAGNI for v0.1.
→ **Recommend:** nullable `project_id` on the envelope, no commands, no table.

**Q10. Repository identity.** → **Recommend:** `.chronicler/repo.id`, committed.
Survives moves, renames, and missing remotes. Clones sharing an id is correct
under Q1's model, not a problem.

---

## To verify before designing further

Things I believe but did not check tonight — don't treat as established:

- [ ] Claude Code's actual hook surface, and whether `Stop` can block/inject
      (§4's taxonomy and the reflection mechanism both depend on this)
- [ ] Claude Code transcript format and on-disk location (needed for Q8)
- [ ] Codex's hook surface — one adapter doesn't test a framework-independent
      protocol; the second one does
- [ ] NativeAOT cold-start latency on your machine (needed for Q6)

---

## Where this stops

No code, no scaffolding, no project files — nothing built, per your instruction.
Next step after we settle Q1–Q4 is a real design doc in
`docs/superpowers/specs/`, then an implementation plan.
