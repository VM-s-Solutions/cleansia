---
id: T-0557
title: There is no request-body limit anywhere — Kestrel's ~28.6 MB default is the real ceiling on every intake path. Decide the host-level shape (ADR)
status: ready
size: S
owner: architect
created: 2026-08-05
updated: 2026-08-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [architect, backend]
security_touching: true
manual_steps: []
sprint: 15
source: scoped **out** of T-0548 (`97bb7265`) by the backend lane with its reason recorded — the right
  number is not derivable from the avatar path. Routed to `architect` and filed by the PM 2026-08-05
---

## Context

`MaxRequestBodySize` appears **nowhere in the solution** — PM-verified at HEAD across `src/**/*.cs` and
`src/**/*.json`, zero hits. So the effective ceiling on every intake path on all five hosts is
**Kestrel's ~28.6 MB default**, by accident rather than by decision.

**Why the avatar fix (`97bb7265`) does not close this.** That fix makes the *answer* correct — an
oversized avatar is now rejected before it is decoded — but it does not stop the *allocation*: the
request body is fully buffered by the server before any validator runs. Validation cannot be the
outermost bound; only the host can.

**Why the number is not derivable from the avatar path, which is the reason this was scoped out rather
than done.** Three intake paths accept **unbounded arrays and none has a count cap**. A single
host-wide limit therefore silently caps those three as well — so choosing a number is choosing a
policy for paths whose requirements were never stated. Picking one from the avatar's 10 MB would be
picking it for endpoints nobody measured.

**Why an attribute is the wrong shape, stated up front because it is the tempting one.** A
per-endpoint `[RequestSizeLimit]` is exactly what the *next* endpoint forgets — which is precisely how
the avatar gap arose, and how `SaveMyDocuments` (T-0556) still has no cap today. The recommended shape
is **host-level and config-driven in `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs`**, so a
new host inherits it and a new endpoint cannot opt out by omission.

This is an **architect decision, not a backend one**: it sets a platform-wide ceiling that interacts
with every current and future intake path, and the trade-off (a limit low enough to matter vs. high
enough not to break a legitimate multi-document upload) is exactly the kind that belongs in an ADR
rather than in a startup file nobody re-reads.

## Acceptance criteria

- [ ] **AC1 — the decision is made by a panel, not by an author.** Given `agents/process/deliberation.md`,
      When the ADR is written, Then **author ≠ challenger ≠ lead** as distinct instances, and §Verdict
      declares the composition. The ADR number is collision-checked **immediately before the file is
      written** (highest at HEAD is **0042**; **T-0547** is reserved by ADR-0042).
- [ ] **AC2 — the ADR states the number and derives it.** Given the chosen limit, When it is recorded,
      Then the ADR names the largest legitimate request the platform accepts today (with the endpoint
      and the evidence), and the chosen ceiling is justified against it — not against the avatar path
      alone.
- [ ] **AC3 — the three unbounded-array paths are enumerated and their fate stated.** Given that a
      host-wide limit caps them implicitly, When the ADR is written, Then each is named with its file
      path and the ADR says whether it also owes an explicit count cap, and in which ticket. **A
      host-wide limit that silently becomes those endpoints' policy without naming them is the failure
      mode this AC exists to prevent.**
- [ ] **AC4 — the shape is host-level and config-driven.** Given the ruling, When implementation is
      specified, Then the limit is applied in `CleansiaStartupBase` (inherited by all five hosts) and
      read from configuration, with the default in source. The ADR states explicitly that a
      per-endpoint attribute is rejected, and why (an attribute is what the next endpoint forgets).
- [ ] **AC5 — a guard proves a new host cannot omit it.** Given a hypothetical sixth host, When it is
      registered without opting in, Then a test fails. Model: `WebSdkContentGlobTests` (T-0538), which
      goes red on a **new** host that reintroduces the defect. A rule with no witness is guidance.
- [ ] **AC6 — the rejection is legible to a client.** Given a request over the limit, When it is
      rejected, Then the client receives a stable, translatable failure rather than a truncated
      connection — and the ADR states what each of the three generated clients sees.
- [ ] **AC7 — the catalog entry carries its enforcer and tier** (ADR-0032). If the outcome adds a
      `agents/knowledge/patterns-backend.md` entry, it names its enforcer and declares its tier, and its
      routing follows whatever ADR-0033's routing test resolves to at that time (see T-0549/T-0551).

## Out of scope

- **Per-endpoint validation limits.** `SaveMyDocuments` is **T-0556** and does not wait for this ticket
  — a correct endpoint answer and a correct host ceiling are two different guarantees and both are owed.
- Re-opening the avatar limit shipped in `97bb7265` (T-0548).
- Rate limiting, which is ADR-0003's subject and a different control.

## Implementation notes

**Files this ticket touches (decision first, implementation after acceptance):**
- `agents/backlog/adr/<NNNN>-<slug>.md` + `agents/backlog/adr/challenges/<NNNN>-<topic>.md` — the panel.
- `src/Cleansia.Config/Abstractions/CleansiaStartupBase.cs` — the host-level limit (AC4).
- The five host projects under `src/Cleansia.Web.*` / `src/Cleansia.Web.Mobile.*` — inheritance, and the
  AC5 guard test in `src/Cleansia.Tests/`.
- `src/Cleansia.Core.AppServices/Features/EmployeeDocuments/SaveMyDocuments.cs` and the other unbounded
  array paths — **named in the ADR (AC3), edited by their own tickets.**

⚠️ **Nothing is implemented before the ADR is accepted.** The routing precedent from this sprint is
explicit that a described fix is not a fix — but the inverse also holds: an implemented decision that
skipped its panel is not a decision.

### Staleness detectability (sprint-15 §D3)

The implementation half names **product paths under `src/`** (`CleansiaStartupBase.cs`), so the
candidate-3 path rule can flag it. The decision half lives under `agents/backlog/adr/**`, which no path
signal covers — re-verify by hand whether the ADR file exists at each checkpoint.

## Status log
- 2026-08-05 — created `ready` by pm, owner `architect`. Filed from the T-0548 lane's scope-out with its
  stated reason carried in rather than paraphrased: the number is not derivable from the avatar path
  because three intake paths take unbounded arrays with no count cap, so a host-wide limit sets policy
  for endpoints nobody has measured. `security_touching: true` — an unbounded intake is a resource
  exhaustion surface on all five hosts.

## Review
<!-- architect panel + reviewer/security write verdicts here; PM reconciles before advancing state -->
