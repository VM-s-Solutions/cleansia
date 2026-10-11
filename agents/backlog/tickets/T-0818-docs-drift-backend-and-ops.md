---
id: T-0818
title: Correct the docs that drifted from the code: backend pages, request logging, local test commands, owner rulings
size: XS
owner: pm
created: 2026-10-11
updated: 2026-10-11
depends_on: [T-0808]
blocks: []
stories: []
adrs: []
layers: [docs]
security_touching: false
---

## Context

Docs findings still true on master, per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets).

## Doing

- [BE-06/WCD-20] `infrastructure.md`: DEV ships the five middleware categories' redacted request and response lines at Information, as `main.bicep` says → `/architecture/request-logging`.
- [BE-08] The digest's freshness docs say that only the hold-expiry and released-window sources are bounded at sweep start. This is the zero-behaviour fix: the Wave D handover ruled "no new sweep-start upper bound".
- [BE-09] The hold resolver's role page and `security-rules.md` cite the current lines.
- [BE-10/WCD-30] `patterns-backend.md` states ADR-0039 D3.2 as shipped, and adds the one-entity-many-windows shape: one commitment read, a pure Domain twin, and a PostgreSQL agreement test.
- [WCD-19] The README and local-orchestration pages show CI's exact `dotnet test` form.
- Owner rulings [D4]: the overview's F11 paragraph records that asking after sign-in was declined on 2026-10-11, and the F10 note says the bar-less hero is final.

## NOT doing

- No ADR edit.
- No sweep of the 68 old citation misses (a separate decision).

## Acceptance criteria (done looks like)

- [ ] **AC1** — Every edited sentence matches the code, the infrastructure template or Mike's ruling.
- [ ] **AC2** — The repository checkers and the docs build pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.

## Review

Pending.
