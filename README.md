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

## Status

**v0.1 works and records its own development.** The ledger, the ingest path, both
adapters, and the read side are built and dogfooded into this repository — see
[`docs/dogfood/`](docs/dogfood/) for exported evidence.

Not built yet: derivation, promotion to project memory, a Codex adapter, and
anything that ranks or summarizes. Evidence ledger first, memory system second.

## Install

### Download a build

Download a self-contained binary from the latest green [CI
run](../../actions/workflows/ci.yml) — `tracewright-win-x64` or
`tracewright-linux-x64`. No .NET install required. Each artifact also ships a
`twr` shim, so both names work once the folder is on PATH. On Windows, unblock
the exe after extracting (`Unblock-File .\tracewright.exe`); on Linux,
`chmod +x tracewright twr`.

### Build from source

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Git.

```sh
git clone https://github.com/codywilliamson/tracewright
cd tracewright
```

**Linux / macOS** — swap `linux-x64` for `osx-arm64` on Apple silicon:

```sh
dotnet publish src/Tracewright.Cli -c Release --self-contained -r linux-x64 \
  -o ~/.local/share/tracewright/bin
ln -sf ~/.local/share/tracewright/bin/tracewright ~/.local/bin/tracewright
ln -sf ~/.local/share/tracewright/bin/twr ~/.local/bin/twr
```

**Windows (PowerShell)** — publishes one exe beside the `twr` shim, then puts
that folder on your user PATH:

```powershell
$dir = "$env:LOCALAPPDATA\Programs\tracewright"

dotnet publish src\Tracewright.Cli -c Release --self-contained -r win-x64 `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=embedded -o $dir

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if ($userPath -notlike "*$dir*") {
    $newPath = if ($userPath) { "$userPath;$dir" } else { $dir }
    [Environment]::SetEnvironmentVariable('Path', $newPath, 'User')
}
```

Open a new terminal, then check it: `tracewright --version` and `twr --version`.
Git for Windows supplies the `sh` that runs the post-commit hook, so
`tracewright init` behaves the same there as anywhere else.

**`twr` is an alias for `tracewright`** — every command below works under either
name. Publishing emits it next to the binary, so there is nothing to set up by
hand. Committed configuration (hooks, settings) always uses the canonical
`tracewright`, since a repo can't assume the alias is on PATH.

The store is created on first write at `~/.tracewright/tracewright.db`; reads
never create it. `TRACEWRIGHT_DB` overrides the path.

## Use

```sh
tracewright init                     # set up capture in a repository (see below)
tracewright timeline                 # last 24h, whole ledger
tracewright timeline --repo          # this repository (resolved from cwd)
tracewright timeline --since 7d --type 'claude.*' --kind observed
tracewright show 01KZ                # full envelope + verbatim payload, by id prefix
```

Recording is one ingest path, three entry points:

| Command | Source | Kind |
|---|---|---|
| `tracewright emit claude` | Claude Code hook JSON on stdin | observed |
| `tracewright emit git post-commit` | the current HEAD commit | observed |
| `tracewright emit raw` | envelope JSON on stdin | asserted (`--kind observed` to override) |

```sh
echo '{"event_type":"note.recorded","payload":{"text":"chose sqlite over jsonl"}}' \
  | tracewright emit raw
```

## Wire it into a repository

```sh
cd your-repo && twr init
```

`tracewright init` does three things and reports each one:

| It writes | Why |
|---|---|
| `.tracewright/repo.id` | repository identity — an opaque uuid. Commit it. |
| `.claude/settings.json` | the hook block for all 15 Claude Code events (`async: true`, so capture never blocks the agent). Commit it. |
| `post-commit` hook | records each commit. Placed where `core.hooksPath` points. |

It is idempotent and non-destructive: an existing `repo.id` is never
regenerated, your own settings and hooks are preserved, and a post-commit hook
it didn't write is left alone with instructions rather than overwritten. Both
hooks always exit 0 — a broken Tracewright must never break a commit or a
session.

## Development

```sh
dotnet build
dotnet test
dotnet format
```

CI runs the same three on every push. `Tracewright.Abstractions` holds the shared
models and the store contract; `Tracewright.Core` implements storage, adapters,
projections; `Tracewright.Cli` is composition and rendering only. Adding a command
without documenting it here fails the test suite.

## Documents

| Document | What it is |
|---|---|
| [`docs/specs/2026-08-11-v0.1-design.md`](docs/specs/2026-08-11-v0.1-design.md) | **The v0.1 spec** — envelope, schema, adapters, timeline. |
| [`docs/decisions.md`](docs/decisions.md) | Decision log — settled decisions and what's deliberately open. |
| [`docs/dogfood/`](docs/dogfood/) | Ledger exports from this repo's own development. |
| [`docs/seed.md`](docs/seed.md) | The original project seed (under the working name *Chronicler*), preserved verbatim. |
| [`docs/notes/`](docs/notes/) | Dated working notes: the challenge pass on the seed, the verified Claude Code hook surface, and the formulation pressure test. |
