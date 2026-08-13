# Tracewright

Tracewright is a local-first evidence ledger for agentic software development: it
captures lifecycle events from coding agents and Git, classifies every record by
provenance, and projects trustworthy timelines from them. This glossary is the
canonical vocabulary; definitions here override synonyms used elsewhere.

## Evidence kinds

**Observed**:
A record emitted by a deterministic mechanism, with no language-model or human
claim in the reporting chain.
_Avoid_: verified, ground truth, fact

**Asserted**:
A record whose informational content originates from an explicit human or model
claim, preserved as a claim.
_Avoid_: unverified, alleged

**Derived**:
A record Tracewright produced from other evidence, linking back to its inputs.
_Avoid_: inferred, computed, synthesized

## Carrier and claim

**Carrier**:
An event, classified by the claim the event itself makes ("this happened").
Observed carriers routinely hold unextracted assertion material in their payloads.

**Claim**:
Assertion material inside an event's payload — a model's or human's statement
(commit message, task result, assistant message), preserved verbatim but never
certified by the carrier's kind.
_Avoid_: result, outcome (when meaning an agent's self-report)

## Absence

**Unattributed**:
An event Tracewright cannot associate with an actor or session context. Answers
"whose work does this belong to?" — e.g. a Git commit with no session correlation.
_Avoid_: orphaned, unknown

**Unanchored**:
An event Tracewright cannot associate with a repository. Answers "where does this
activity belong?" — e.g. a Claude event with no resolvable repository identity.
_Avoid_: unattributed (when repository identity is what's missing)

## Read side

**Projection**:
A view computed from stored events at read time, storing nothing and deriving
nothing persistent. Sessions and the timeline are projections.
_Avoid_: report, session object
