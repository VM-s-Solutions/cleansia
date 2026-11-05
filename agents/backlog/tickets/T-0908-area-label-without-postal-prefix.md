---
id: T-0908
title: The pre-take area label names the district or city only — no "· 180 xx" postal prefix
size: S
owner: Michael Chaban
created: 2026-11-01
updated: 2026-11-01
depends_on: []
blocks: []
stories: []
adrs: [ADR-0079]
layers: [backend, docs]
security_touching: true
---

## Context

Mike saw "Praha 8 · 180 xx" on the partner job card. `OrderMappers.BuildApproximateAddress` turns a Prague postcode into
its district only when the city reads exactly "Praha". A city stored as "Praha 8" falls through to the generic
"{city} · {first three digits} xx" form. Owner ruling 2026-11-01, in reply to "include the server fix or leave it out":
"You can remove it".

The label drops the postal prefix everywhere. A Prague postcode gives the district, as today. Any other address gives
the city alone. This is coarser than before, never finer. The distance band's snapped cell is already the effective
pre-take ceiling (ADR-0079).

## Acceptance criteria

- [x] **AC1** — `BuildApproximateAddress` returns the Prague district for a Prague postcode, whether the city reads
  "Praha", "Praha 8" or "Prague". It returns the trimmed city for any other address, and "" with no city. It never
  returns a postal digit.
- [x] **AC2** — Tests: the mapper's cases are rewritten red, then green. Every test or fixture in the tree that pins the
  old "· NNN xx" spelling is updated or confirmed to be only sample data.
- [x] **AC3** — Docs: business-rules and security-rules (pre-take location) describe the new label as an owner ruling
  dated 2026-11-01.

## Out of scope

- The distance band.
- The ADR-0079 acknowledgement.
- NSwag: the field stays a string.
- Migrations.
- PRO.

## Work record

- 2026-11-01 — Filed.
- 2026-11-02 — Built (round 4).
  - `BuildApproximateAddress` returns `Praha N` for a Prague postcode, whatever the city spelling ("Praha", "Praha 8",
    "Praha 8 - Karlín", "Prague"), the trimmed city otherwise, and "" with no city.
  - Mapper tests rewritten: 8 of 26 failed on the old code, then 26/26 passed. The lane's full `Cleansia.Tests` run
    passed 9187/9187, and the 45 integration tests that touch the label passed. Three host and integration tests that
    pinned the old spelling were updated.
  - Security review: APPROVED.
  - Docs: business-rules, security-rules, flows and features describe the new label, dated 2026-11-01.
  - The coordinator re-ran the label tests (149/149).
  - No NSwag change, no migration. DEV keeps the old label until this tree is deployed.
  - Open owner question, which predates this work: the city is passed through exactly as the customer typed it.
