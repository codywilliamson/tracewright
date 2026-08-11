# Claude Code hook surface — verified findings

**Date:** 2026-08-11
**Sources:** official hooks reference (code.claude.com/docs/en/hooks, fetched today) and
local artifacts on this machine — transcripts under `~/.claude/projects/`, hook scripts
in `~/.claude/hooks/`, `~/.claude/settings.json`. Local Claude Code version: **2.1.177**
(latest changelog shows 2.1.221 — one finding below is version-gated).

Answers the nine questions from the 2026-08-11 design discussion, in order.

---

## Q1. Which hook events give useful deterministic observations?

The surface is much larger than the seed assumed (~30 events). The ones that matter
for capture, with proposed normalized types:

| Hook event | → Chronicler type | Notes |
|---|---|---|
| `SessionStart` | `claude.session.started` | `source`: startup / resume / clear / compact / fork |
| `UserPromptSubmit` | `claude.prompt.submitted` | `user_input` — human-authored content |
| `PreToolUse` | `claude.tool.started` | records the *attempt*; survives tool hangs/crashes |
| `PostToolUse` | `claude.tool.completed` | includes `tool_input` **and** `tool_response` |
| `PostToolUseFailure` | `claude.tool.failed` | deterministic failure signal (see Q6) |
| `SubagentStart` / `SubagentStop` | `claude.agent.delegated` / `.completed` | `agent_id`, `agent_type`; Stop includes `last_assistant_message` |
| `TaskCreated` / `TaskCompleted` | `claude.task.created` / `.completed` | `task_name`, `result: success\|failed` — see epistemic note below |
| `Stop` | `claude.turn.completed` | `last_assistant_message`, `stop_reason` |
| `SessionEnd` | `claude.session.ended` | `reason`: clear / logout / other / … |
| `PreCompact` / `PostCompact` | `claude.context.compacted` | marks context loss — memory-relevant later |
| `WorktreeCreate` / `WorktreeRemove` | `claude.worktree.created` / `.removed` | |

Skip for v0.1: `Notification`, `MessageDisplay`, `FileChanged`, `Elicitation*`,
`ConfigChange`, `InstructionsLoaded`, `PermissionRequest`/`PermissionDenied`.
(Permission denials are a real human-intervention signal — worth revisiting, but
requests are noisy and the intervention story isn't in v0.1.)

**Capture broadly, normalize narrowly.** Volume is a non-issue locally and append-only
storage is cheap; curation belongs in projections, not at the capture boundary.

## Q2. What stable IDs / correlation are provided?

A natural correlation spine exists, nested exactly the way we want:

```
session_id                      every hook payload
  └── prompt_id                 every payload ≥ v2.1.196  ⚠ this machine runs 2.1.177
        └── tool_use_id         Pre/PostToolUse — matches the transcript tool_use block id
              └── agent_id      present on hooks fired inside a subagent (+ agent_type)
```

Plus per payload: `transcript_path`, `cwd`, `permission_mode`, `hook_event_name`, and
`CLAUDE_PROJECT_DIR` in env. Transcript records each carry a stable `uuid`,
`parentUuid` (chains), `sessionId`, `timestamp`, `version`, `isSidechain`.

**Design consequences:**
- All correlation fields must be nullable — `prompt_id` doesn't exist on this
  machine's version yet. Adapters record what's present, never invent.
- `tool_use_id` appears in both the hook payload and the transcript record → the
  join between "hook saw it" and "transcript said it" is free.

## Q3. How are subagents represented?

Cleanly, three ways at once:
- `SubagentStart`/`SubagentStop` hooks with `agent_id`, `agent_type`
- every hook fired *inside* a subagent carries `agent_id` — so tool events
  self-identify their owner
- transcripts: `isSidechain: true`, separate files at
  `<session>/subagents/agent-<id>.jsonl`, linked to the parent's `tool_use` block

The seed's "how are parallel agents represented?" question dissolves: parallel
subagents are just events with different `agent_id`s under one `session_id`.

## Q4. How are tasks represented?

`TaskCreated`/`TaskCompleted` hooks with `task_name`, `description`, `result`.
This settles a seed §21 question: **Chronicler records external task identifiers;
it does not own a Task concept.** The harness owns tasks; we observe them.

Epistemic note: `TaskCompleted(result: success)` is an *observed act of assertion* —
the hook firing is mechanical, but "success" is the agent's own claim about its work.
The event is Observed; the payload is assertion material. (See the epistemics note in
the companion doc.)

## Q5. How are worktrees represented?

`WorktreeCreate`/`WorktreeRemove` hook events exist. On the git side, every worktree
checks out its own copy of the working tree, so a committed `.chronicler/repo.id`
resolves identically in all worktrees — worktree identity is `cwd` + repo id, no
special machinery.

## Q6. Can we observe tool command + result/exit status deterministically?

**Partially — this changes the flagship example.**

- `tool_input.command` — yes, verbatim, at Pre and Post.
- Success vs failure — yes, deterministically: `PostToolUse` fires on success,
  `PostToolUseFailure` on failure. Binary, but mechanical.
- **Numeric exit codes — no.** The docs are explicit that `tool_response` is text
  output; exit codes are not a structured field. `dotnet test → exit 1` as shown in
  the dogfood timeline is not directly capturable. What we get is
  `Bash "dotnet test" → failed` plus the output text (parseable later at derive time,
  not at capture time).

Discrepancy detection survives fine — "agent asserted verification succeeded /
observed tool failure" only needs the binary. But the timeline mock in the design
discussion overstates what capture provides; the honest v0.1 line is `OBSERVED
Bash: dotnet test → failed`.

(The `exitCode` fields the local transcripts *do* contain are for **hook script**
executions — `attachment.hook_success` records — not for tool runs. Easy to confuse.)

## Q7. What payload data is potentially sensitive?

Everything with content: `user_input` (prompts), `tool_input.command` (may embed
secrets inline), `tool_response` (stdout can contain env dumps, tokens, keys),
`last_assistant_message`, and the transcript files themselves. Claude Code strips
`OTEL_*` from hook env but hooks otherwise inherit full env — the adapter must never
log its own environment.

v0.1 boundary (already decided by the two-store split): **all of the above stays in
the machine-local raw store; none of it auto-promotes.** Redaction machinery deferred,
boundary not.

## Q8. What to preserve raw vs normalize?

Store the **entire hook payload verbatim** as the event payload (JSON column).
Normalize onto the envelope only what queries need: type mapping, evidence kind,
correlation ids, timestamps, emitter identity. Nothing else. Fields earn promotion
to indexed columns when a real query needs them — not before.

The adapter also records: `hook_event_name` as `original_event`, its own adapter
version, and the Claude Code version when discoverable. Adapters evolve; historical
events must state which translator produced them.

## Q9. What should `chronicler init` eventually manage?

Better answer than expected, on three counts:

1. **Hook config is committable.** Hooks can live in *project*
   `.claude/settings.json` — meaning the Claude Code integration travels with the
   repo, exactly like the planned `.chronicler/` config. Per-machine setup reduces
   to "chronicler is on PATH."
2. **Exec form kills quoting hell.** `{"type": "command", "command": "chronicler",
   "args": ["emit", "claude"]}` — no shell involved, cross-platform, no
   Windows-vs-bash escaping. This is the installation shape.
3. **`async: true` exists.** Capture hooks can run non-blocking — hook latency
   affects event freshness only, **not agent speed**. This mostly dissolves the
   NativeAOT-for-latency argument: build normally, measure, optimize only if the
   git-hook path (which *is* synchronous) hurts.

Also relevant: Claude Code supports **HTTP hooks** natively (POST payload to a local
endpoint). If a daemon is ever justified, it's not new plumbing — it's a config
change from command hooks to HTTP hooks. The escape hatch is already built into the
platform. Reinforces: no daemon in v0.1.

Git-hook interop (`core.hooksPath` vs chaining) remains the one unresolved
installation question — unchanged from yesterday's notes.

---

## Verified caveats

- **`transcript_path` lags.** Docs: the file is written asynchronously. Never read
  the transcript synchronously inside a hook; store the pointer, dereference at
  derive/inspect time.
- **`Stop` can block and inject** (exit 2 or `decision: "block"` + reason /
  `additionalContext`) — confirmed. The structured-reflection mechanism from the
  first-pass analysis is real, for later.
- **Hook events vary by version** (this machine: 2.1.177; `prompt_id` needs 2.1.196+;
  changelog shows events still being added). The adapter must treat unknown events
  and missing fields as normal, not errors.

---

## Draft envelope (grounded in real fields — input to the spec, not final)

```
event_id        ULID (sortable)
occurred_at     source timestamp
recorded_at     ingest timestamp
type            chronicler dotted type        e.g. claude.tool.completed, git.commit
kind            observed | asserted | derived
emitter         adapter name + adapter version + emitter version + original_event
correlation     session_id? prompt_id? tool_use_id? agent_id? task_id?
                repo_id? worktree?
evidence_ref    transcript_path? + record uuid?          (pointer, never a copy)
links           [event_id]                               (derived → its inputs)
payload         full raw JSON from the source
```

Single SQLite `events` table, envelope fields as indexed columns, payload as JSON.
Schema detail belongs in the spec, designed against this data.
