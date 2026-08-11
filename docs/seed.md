# Chronicler — Project Seed

> Original seed document, preserved verbatim as the input artifact for design.
> Authored by Cody, 2026-08-10. Do not edit — supersede it with new documents instead.

## Status

Early architecture / product definition.

This document captures the current thinking and decisions for Chronicler. It is intentionally not a complete specification. The next step is to challenge these assumptions, refine the domain model, and define a narrow v0.1 suitable for dogfooding.

---

## 1. Problem

Modern agentic development workflows generate a large amount of useful context during software development:

* why changes were made
* alternatives that were considered
* failed approaches
* assumptions
* review findings
* verification results
* agent delegation
* human interventions
* architectural decisions
* workflow changes
* relationships between tasks, commits, files, and decisions

Most of this context disappears when an agent session ends.

Git preserves what changed, but usually not why.

AGENTS.md, CLAUDE.md, architecture documents, and similar mechanisms preserve selected current knowledge, but they generally do not preserve the provenance and evolution of that knowledge.

Agent conversation history contains much of this information, but it is transient, verbose, difficult to query, and poorly suited as durable project memory.

Chronicler explores a different model:

Capture structured evidence about software-development activity and turn it into durable, queryable project memory.

The long-term question is:

Can we give coding agents relevant project memory without giving them the project's entire history?

---

## 2. Core Principles

### 2.1 Deterministic software first

Chronicler should not initially depend on an LLM to function.

The foundational pipeline is:

```
CAPTURE
   ↓
STORE
   ↓
DERIVE
   ↓
RETRIEVE
   ↓
SYNTHESIZE
```

Capture and storage must be deterministic.

Retrieval should be deterministic wherever practical.

LLMs may eventually participate primarily in:

* DERIVE
* SYNTHESIZE

The deterministic foundation should remain useful without them.

### 2.2 Evidence over narrative

Chronicler is an audit system, not an automatic documentation generator.

It should preserve:

* what happened
* when it happened
* who/what performed it
* what evidence supports it

It should avoid rewriting development history into a cleaner story than what actually occurred.

Failures, reversals, abandoned approaches, and human interventions are useful data.

### 2.3 Provenance is fundamental

Derived knowledge must point back to evidence.

Potential knowledge classifications:

**Fact** — Directly observed from an event or artifact.

**Decision** — Explicitly recorded during development.

**Inference** — Derived from available evidence.

**Unknown** — Available evidence cannot answer the question.

If Chronicler cannot determine why something happened, it should say so rather than manufacture an architectural explanation.

Example:

```
> chronicler ask "Why were records used for these models?"
No recorded decision explains the choice.
Records were introduced in commit abc123 during task T-14,
but no captured evidence contains a rationale.
```

This behavior is preferable to speculation.

---

## 3. Chronicler Is Not an Agent

An important architectural distinction:

Chronicler should not depend on an agent remembering to invoke it.

Instructions such as:

> Remember to record important decisions in Chronicler.

inside AGENTS.md or equivalent are inherently nondeterministic.

Instead, Chronicler should integrate with agent workflows through lifecycle hooks.

Chronicler sits outside the agent's decision loop.

Conceptually:

```
Developer
    ↓
Agent Workflow / Orchestrator
    │
    ├── Planning
    ├── Implementation
    ├── Review
    └── Verification
            │
            ↓
       Lifecycle Hooks
            │
            ↓
        Chronicler
```

The agent workflow emits structured lifecycle events whether or not the agent explicitly remembers Chronicler exists.

---

## 4. Hook Protocol

Chronicler should define a small agent-framework-independent protocol.

Potential lifecycle events include:

```
session.started
session.ended
task.started
task.completed
task.failed
agent.delegated
agent.completed
agent.failed
tool.completed
review.finding
verification.completed
decision.recorded
assumption.recorded
failure.recorded
human.intervention
workflow.changed
```

Exact event taxonomy remains to be designed.

The important architectural principle is:

Agent frameworks translate their lifecycle into Chronicler's protocol.

For example:

```
Claude Code
Codex
Custom workflow
Future agent system
       │
       ▼
Chronicler Hook Protocol
       │
       ▼
Event Store
```

Chronicler should not make its core domain dependent upon Claude Code, Codex, or any particular agent framework.

---

## 5. Two Sources of Truth

Chronicler should eventually correlate two different kinds of evidence.

### Agent/workflow evidence

Captures things such as:

* objective
* task lifecycle
* delegation
* agent output
* verification
* review findings
* decisions
* assumptions
* failures
* human intervention

This answers: **What did the development workflow believe it was doing?**

### Repository evidence

Git can independently provide:

* commits
* branches
* checkout activity
* merges
* rebases/rewrites
* actual file changes

This answers: **What actually happened to the repository?**

These sources should remain conceptually distinct.

Their discrepancies may themselves become valuable findings.

Examples:

* Agent reports successful completion but verification failed.
* Agent claims three files changed while Git shows twelve.
* Task completes but no corresponding repository change exists.
* Agent reports tests passed but recorded test execution failed.
* A commit contains unrelated changes not represented by the task.

Chronicler may eventually detect and expose these discrepancies.

---

## 6. Git Integration

Repository onboarding will likely install Git hooks.

Possible events include:

```
git.commit
git.checkout
git.merge
git.rewrite
```

Implementation should avoid destructively replacing existing user hooks.

A managed hook strategy or hook chaining should be investigated.

Example possibility:

```
.chronicler/
    config.yaml
    hooks/
        post-commit
        post-checkout
        post-merge
        post-rewrite
```

The exact implementation is unresolved.

Git integration is complementary to agent lifecycle integration; neither replaces the other.

---

## 7. Domain Model

Current conceptual model:

```
Project
  │
  ├── Repository
  ├── Repository
  │
  └── Session
        │
        ├── Task
        │     ├── Event
        │     ├── Decision
        │     ├── Evidence
        │     └── Artifact
        │
        └── Task
```

This model is preliminary and should be challenged before implementation.

---

## 8. Project != Repository

A Chronicler Project represents a logical body of work.

A Project may contain multiple repositories.

Example:

```
Project: Chronicler
Repositories:
- chronicler
- chronicler-docs
- chronicler-vscode
```

This avoids coupling project memory directly to Git repository boundaries.

A session belongs primarily to a Project and may involve zero, one, or multiple repositories.

Example:

```
Session
├── ProjectId
├── StartedAt
├── EndedAt
├── Objective
└── Repositories[]
```

Individual events may optionally reference a repository.

A project-level architectural decision may have no repository association.

A commit event necessarily does.

---

## 9. Repository Identity

Filesystem paths should not be repository identities because repositories move.

Remote URLs are useful but cannot be mandatory because local-only repositories exist.

Repository identity therefore needs further design.

Likely inputs include:

* generated stable Chronicler repository ID
* normalized remote URL when available
* Git root
* repository metadata

The Chronicler-generated ID should probably be authoritative internally.

---

## 10. Storage

Initial storage should be local.

Preferred starting point:

```
~/.chronicler/
    chronicler.db
    config.*
    logs/
```

SQLite is currently the preferred storage engine.

Reasons:

* local-first
* transactional
* queryable
* portable
* easy to inspect
* no external infrastructure
* supports multiple projects/repositories
* suitable for structured event history

The primary database should not live inside individual repositories.

Repository-local Chronicler files should contain configuration/integration information rather than historical state.

Potential:

```
repo/
    .chronicler/
        config.yaml
        hooks/
```

Exporting selected project memory to Markdown may be useful later, but Markdown should not be the primary datastore.

---

## 11. CLI First

Chronicler should initially be a CLI.

Potential command surface:

```
chronicler init
chronicler project ...
chronicler repo ...
chronicler session start
chronicler session end
chronicler sessions
chronicler timeline
chronicler record decision
chronicler record assumption
chronicler record failure
chronicler doctor
```

Future commands might include:

```
chronicler ask
chronicler context
```

The exact CLI should emerge from the domain rather than being finalized now.

---

## 12. Repository Onboarding

`chronicler init` should mean more than database registration.

Its responsibility is conceptually:

> Onboard this repository into Chronicler's capture protocol.

Likely responsibilities:

1. Detect Git repository.
2. Create or select a Chronicler Project.
3. Register repository.
4. Create repository-local Chronicler configuration.
5. Install/configure Git integration.
6. Install/configure supported agent workflow integrations.
7. Verify installation.

Example eventual experience:

```
$ chronicler init
Project: Chronicler
Repository: ~/src/chronicler
Installed:
✓ repository registration
✓ Git integration
✓ Claude Code workflow integration
✓ session capture
Run `chronicler doctor` to verify configuration.
```

Agent detection and automatic installation behavior require further design.

---

## 13. Agent Workflow Integration

Agent integration should use hooks wherever the agent framework supports them.

Conceptually:

```
before task
    ↓
task.started
agent executes
    ↓
agent/tool lifecycle events
review
    ↓
review.finding
verification
    ↓
verification.completed
after task
    ↓
task.completed
```

Semantic information creates a harder problem.

Some knowledge cannot be deterministically inferred from lifecycle hooks:

* why an architecture was selected
* why an alternative was rejected
* assumptions made during implementation
* reasoning behind a human override

The workflow may therefore include explicit structured reflection points.

For example, before completing a task:

1. Verify implementation.
2. Identify consequential decisions.
3. Identify assumptions worth preserving.
4. Identify failed approaches worth preserving.
5. Emit structured events.
6. Complete task.

This reflection may involve an agent, but its output remains explicitly classified as agent-provided evidence rather than objective fact.

---

## 14. Always-On Behavior

Chronicler does not initially need to be always running.

v0.1 can use explicit sessions:

```
chronicler session start "Implement repository tracking"
...development occurs...
chronicler session end
```

Agent workflow hooks can attach to the active session.

A daemon may eventually provide passive capture:

```
Agent Hooks
Git Hooks
IDE Integration
Shell Integration
       │
       ▼
Chronicler Daemon
       │
       ▼
Application Layer
       │
       ▼
SQLite
```

A daemon should not contain core business logic.

Potential architecture:

```
Chronicler.Cli
       │
       ▼
Chronicler.Application
       │
       ▼
Chronicler.Storage

Chronicler.Daemon
       │
       ▼
Chronicler.Application
```

Whether the daemon becomes necessary should be driven by actual requirements.

---

## 15. Durable Task

Durable Task is considered potentially useful but intentionally deferred.

Chronicler should first discover its actual domain and processing requirements without introducing orchestration concepts prematurely.

Initial event capture should remain simple:

```
event
  ↓
validate
  ↓
persist
```

Durable orchestration becomes justified if Chronicler later develops workflows involving:

* multiple processing stages
* resumability
* process failure recovery
* independently retryable operations
* long-running derivation
* asynchronous analysis
* expensive processing that should not restart from scratch

Potential future example:

```
Session Ends
    ↓
Process Session
    ├── Build timeline
    ├── Inspect repository changes
    ├── Correlate activity
    ├── Extract decisions
    ├── Detect failed attempts
    ├── Identify unresolved questions
    ├── Calculate metrics
    └── Build searchable snapshot
```

At that point Durable Task may be an excellent fit.

Until such a requirement exists, it should not be introduced.

---

## 16. Future Query Capability

A major potential feature is:

```
chronicler ask "<question>"
```

Example:

```
chronicler ask "Why are events normalized before persistence?"
```

Desired behavior:

```
Normalization was introduced during Session S-007 while
implementing the RSS connector.
The original GitHub implementation persisted source-specific
models directly.
Adding RSS exposed source-specific knowledge in the persistence
layer.
Decision D-018 moved normalization before persistence.

Evidence:
- Session S-007
- Task T-042
- Commit 8ac41de
- src/.../NormalizedEvent.cs

Alternative approaches recorded:
- source-specific persistence handlers
- generic JSON payload storage
```

Answers should always be grounded in captured evidence.

If evidence is insufficient, Chronicler says so.

---

## 17. Future Context Engineering Capability

Another major potential feature:

```
chronicler context
```

Example:

```
chronicler context \
    --task "Add webhook ingestion" \
    --files src/Ingestion
```

Rather than dumping all project history into an agent context window, Chronicler would retrieve a curated packet containing relevant historical information.

Potential contents:

```
Relevant architecture
Relevant decisions
Known constraints
Previous bugs
Failed approaches
Related tests
Recent work
Open questions
Important commits
```

This leads to a possible long-term product thesis:

> Give coding agents the project's memory, not the project's entire history.

Agent integration for consuming these context packets is explicitly out of scope initially.

---

## 18. Dogfooding Strategy

Chronicler should be built using the new agent workflow that Chronicler itself is intended to observe.

The first important milestone is:

> Build enough of Chronicler that the remainder of Chronicler's development can be recorded by Chronicler.

This creates a useful recursive experiment.

Early sessions build capture.

Later sessions use that capture.

Eventually Chronicler should be able to answer questions about decisions made during its own development.

Example acceptance test:

```
chronicler ask "Why does Chronicler use SQLite?"
```

The answer should derive from the actual captured history of the project rather than static documentation written after the fact.

A stronger future experiment:

1. Build a meaningful portion of Chronicler.
2. Start a completely fresh agent session.
3. Provide no previous conversation history.
4. Give the agent the repository plus a Chronicler-generated context packet.
5. Ask it to implement a nontrivial feature.
6. Evaluate whether the historical context improves its decisions.

This provides a concrete way to evaluate Chronicler as a context-engineering system.

---

## 19. Explicit Non-Goals for v0.1

Do not initially build:

* web UI
* desktop UI
* SaaS service
* cloud infrastructure
* multi-user collaboration
* remote synchronization
* IDE extensions
* semantic/vector search unless demonstrated necessary
* LLM-required capture
* automatic blog generation
* generalized observability platform
* Durable Task orchestration without an actual durability requirement
* deep integrations with every coding agent
* agent context injection

The initial system should be small enough to reason about.

---

## 20. Likely v0.1

The exact scope should be refined, but a reasonable first milestone is:

**Project/repository management**

* Initialize Chronicler.
* Create/register project.
* Register current Git repository.
* Resolve project/repository from working directory.

**Sessions**

* Start session.
* End session.
* Track active session.

**Event capture**

* Structured event model.
* Append-only event persistence.
* Provenance metadata.
* Basic workflow hook protocol.

**Git evidence**

* Capture selected repository lifecycle events.
* Associate commits/changes with active sessions where possible.

**Manual semantic evidence**

Support deterministic recording of explicit:

* decisions
* assumptions
* failures
* notes

**Inspection**

Provide commands to inspect:

* sessions
* session details
* chronological timeline
* decisions
* repository activity

**Integration health**

```
chronicler doctor
```

Verify repository registration, hooks, storage, and configured workflow integrations.

---

## 21. Questions to Resolve Before Implementation

The next design pass should challenge rather than blindly accept the current proposal.

Important unresolved questions include:

**Event model**

* Should events be strongly typed or envelope + payload?
* What fields belong on every event?
* How should event schemas evolve?
* Should events be immutable?
* How are corrections represented?

**Sessions**

* Can multiple sessions be active simultaneously?
* Is an active session global, per project, per repo, per process, or per agent?
* What happens after crashes?
* Can sessions nest?

**Tasks**

* Does Chronicler own the concept of a Task or merely record external task identifiers?
* Can tasks form hierarchies?
* How are parallel agents represented?

**Agent hooks**

* What lifecycle capabilities do Claude Code, Codex, and other likely environments actually expose?
* Which events can be captured deterministically?
* Which require semantic agent participation?
* How should adapter/version information be stored?

**Git**

* How should existing hooks be preserved?
* Is `core.hooksPath` appropriate?
* How are worktrees handled?
* How are rebases and amended commits represented?
* How should uncommitted work be associated with sessions?

**Project identity**

* How are projects created and identified?
* How does a second repository join an existing project?
* How should repository clones be treated?

**Storage**

* What should the SQLite schema look like?
* Should event payloads be relational, JSON, or hybrid?
* What indexing will future retrieval require?

**Privacy**

Agent activity may contain:

* source code
* prompts
* terminal output
* secrets
* environment variables
* proprietary information

Capture boundaries, redaction, and explicit exclusions need to be first-class design concerns rather than retrofitted later.

**Portability**

* Can history be exported?
* Can another developer import it?
* Which identities survive machines and repository clones?

---

## 22. Design Constraint for the Next Phase

Avoid designing Chronicler around imagined future AI features.

First make it excellent at answering:

> What happened during development, what evidence proves it, and what important context would otherwise have been lost?

Once that foundation exists, intelligent derivation, retrieval, questioning, and agent context generation can be layered on top.

The system should earn its complexity.
