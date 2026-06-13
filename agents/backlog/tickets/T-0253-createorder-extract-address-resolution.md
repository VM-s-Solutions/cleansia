---
id: T-0253
title: "AUD-06a — extract address-resolution + serviced-area collaborator out of CreateOrder.Handler"
status: ready
size: M
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0118, T-0212]
blocks: [T-0254]
stories: []
adrs: [0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0199 split (Batch 5D sub-step a); AUD-06
---

## Context
Child of the **T-0199 (AUD-06)** CreateOrder god-handler decomposition (Batch **5D — RUNS ALONE on the
`CreateOrder.cs` cluster**). `src/Cleansia.Core.AppServices/Features/Orders/CreateOrder.cs` injects 15
dependencies and braids four unrelated concerns inline. This first sub-step lifts out the **address-resolution +
serviced-area gating** concern:

- `ResolveAddressAsync` (saved-vs-inline address, ownership check, country `IsServicedAsync`, single-serviced-
  country fallback),
- the inline `serviceCityRepository.CityIsServicedAsync` city check,
- `addressGeocoder.PopulateCoordinatesAsync`.

These move into a named collaborator (e.g. an address-resolution / serviced-area service) behind an interface,
injected into the handler and registered in DI. **This is a refactor, NOT a behavior change** — the externally
observable behavior (success `Response`, every `BusinessResult.Failure` error code, side-effect ordering) stays
**identical**.

**CRITICAL ACCEPTANCE GATE:** **T-0212's CreateOrder characterization suite**
(`src/Cleansia.Tests/Features/Orders/CreateOrder*CharacterizationTests.cs`, 20 cases, Wave-4 4A) is the green
regression net. It MUST be green before the first decomposition commit and **stay green UNCHANGED** through this
sub-step (AC1/AC3). A diff that modifies T-0212's assertions to make them pass fails review.

**Sequencing:** depends on **T-0118✓** (the Cash-branch enqueue is its post-commit dispatch / outbox seam,
preserved by sub-step c) and **T-0212✓** (the net). `blocks: [T-0254]` — the three AUD-06 sub-steps land
**serially** on `CreateOrder.cs`; this is the first. Rebase target: current `master` (`ee95a57f`, post-Wave-4).

## Acceptance criteria
- [ ] **AC1 (T-0212 net green, unchanged)** — Before this sub-step's first commit, T-0212's CreateOrder
  characterization suite is green against the **unchanged** handler (status log records it). The suite file is
  **not modified** by this ticket.
- [ ] **AC2 (decomposition — address concern extracted)** — `ResolveAddressAsync`, the city-serviced check, and
  geocoding are extracted into a named collaborator behind an interface, injected into the handler and
  registered in DI. The handler's direct dependency count drops; no new collaborator reconstitutes the god-unit.
  The extracted unit has its own unit tests.
- [ ] **AC3 (behavior identical)** — T-0212 re-runs **unchanged** and is **still green** — same success shape,
  same error codes (`NotFound` saved-address missing/cross-user, `CountryNotServiced`, `CountryRequired`,
  `CityNotServiced`), same side-effect ordering (address resolve → city/country gate → geocode → …).
- [ ] **AC4 (consistency clean)** — `node agents/tools/check-consistency.mjs backend --paths=src/Cleansia.Core.AppServices/Features/Orders`
  reports zero new violations; the collaborator follows §B canon (B7 rich domain methods, B9 `entity.MapToDto()`
  — no inline projection re-introduced).
- [ ] **AC5** — `dotnet test src/Cleansia.Tests` green; Reviewer confirms refactor-only and no `Command`/`Response`
  contract change → **no nswag-regen, no migration**.

## Out of scope
- Promo preview/apply extraction (sub-step b, T-0254) and payment-side-effect/late-referral extraction +
  final handler slim-down (sub-step c, T-0255).
- Any `Command`/`Response` wire shape, endpoint, or validator-behavior change; adding the missing CreateOrder
  idempotency guard (B8 gap — behavior change, tracked elsewhere); re-architecting the post-commit dispatch /
  outbox (owned by T-0118).

## Implementation notes
- **TEST-FIRST / characterization net:** T-0212 is the net (already shipped) — do not advance until it is green
  against the pre-refactor handler; keep it green and unmodified through the refactor (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §B — B7 rich domain methods (no direct entity property sets
  in the collaborator), B8 narrow provider-specific try/catch preserved, B9 `entity.MapToDto()`. The collaborator
  is a plain injected service behind an interface, registered in DI alongside the existing Orders services.
- **Serialization — LANE-ISOLATED:** `CreateOrder.cs` is a serialized shared-file cluster. No other ticket
  touching `CreateOrder.cs` runs concurrently. The three AUD-06 sub-steps run **serially** (a → b → c); the
  collaborator's DI registration touches the Orders-feature registration — serialize against any concurrent
  Orders-feature DI edit (none this wave). 5D can run in parallel with 5B/5C/5E (none edit `CreateOrder.cs`).

## Status log
- 2026-06-13 — ready (created by pm — split of T-0199 / AUD-06, Batch 5D sub-step a). DoR met: AC observable
  (T-0212 stays green), sized M, deps T-0118✓/T-0212✓ done, no migration/regen, refactor-only, lane-isolated on
  `CreateOrder.cs`, `blocks: [T-0254]` (serial a→b→c). Reviewer-per-developer.

## Review
<!-- reviewer / optimizer write verdicts here; PM reconciles before advancing state -->
