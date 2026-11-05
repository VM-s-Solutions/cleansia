---
id: T-0903
title: Pre-take location — Prague district from the postcode and a snapped distance band from home (ADR-0079)
size: M
owner: Michael Chaban
created: 2026-10-31
updated: 2026-10-31
depends_on: []
blocks: [T-0902]
stories: []
adrs: [ADR-0079]
layers: [backend, ios, android, docs]
security_touching: true
---

## Context

Mike, 31 October 2026: *"Prague 4 is nice"* (show the district) and *"Also fix the findings"*, which
included a distance band from the cleaner's home in place of the device-location distance the phones worked
out. Decision record: `docs/decisions/adr-0079.md` (proposed; Mike's acknowledgement of the location
ceiling moves it to accepted). Evidence: `/Users/michael/cleansia-work.noindex/partner-orders-build-20261031/evidence/backend/`.

## Acceptance criteria

- [x] **AC1** — `OrderMappers.BuildApproximateAddress` returns `Praha N` when the postcode, all whitespace
  removed, is five digits starting with 1 and the city matches Praha/Prague (second digit 0 → `Praha 10`);
  every other address keeps the old output.
- [x] **AC2** — `OrderListItem.homeDistanceBand` (`HomeDistanceBand` 1–5: under 3, 3–6, 6–10, 10–20, over 20 km)
  is measured from the cleaner's stored home to the centre of the job's 0.02° × 0.03° cell, read once per
  request; null for admins, for customers, and when either point has no coordinates.
- [x] **AC3** — The band is computed before redaction and the coordinates stay null on the wire; it is not
  logged, cached or written into the contract facts.
- [x] **AC4** — Three TS clients, both committed mobile specs and the iOS models regenerated; additions only.
- [x] **AC5** — Both Partner apps show the band on Available cards and in each card's spoken sentence; the
  phones read no device location for Orders.

## Out of scope

Snapping the `IsWithinRadius` paths (seat-open push, new-jobs digest); the band on `PendingOfferItem`; the
partner web Available table; a server distance sort.

## Work record

- 2026-10-31 — Built locally (uncommitted). Unit 9,183/0, integration 1,042/0 on real Postgres (including 4
  new `AvailableOrdersHomeDistanceBandTests`), host tests 518/0, Release build 0 errors, Angular typecheck
  clean, `check-docs-refs` 0 unresolved. Red first for the district, the band, the snap (an unsnapped band
  failed the same-cell test) and the handler wiring. Correctness and security reviews: approve, no blocker
  or should-fix.
- Commands run (local only, never DEV/PRO): `npm run generate-clients` (NSwag 14.7.1), the mobile spec
  re-dump from a local AppHost, `openapi-generator` for the iOS models.

## Found, not fixed

- `RequestLoggingMiddleware` on Partner Mobile logs the request body of a home-address update at
  Information, which includes the stored coordinates (S6). Predates this ticket.
- `IsWithinRadius` (seat-open push, new-jobs digest) still measures to the exact job point, so the
  platform-wide ceiling is finer than the band's cell.
- NSwag types the nullable enum as non-nullable in the TS clients (house behaviour); no web reader today.
- Local stack: the AppHost needs `Parameters:postgres-password` and waits silently without it; first start on
  an empty database crashed Customer Mobile in `LegalDocumentSeeder` (25P02); the `functions` resource fails
  to launch.
- Test nits: no no-break-space postcode case; the no-coordinates integration case seeds no address rather
  than an ungeocoded one; one summary example in `OrderMappers` is inexact; the `SnapToBandCell` pointer
  targets the top of S4, not `#pre-take-location`.
