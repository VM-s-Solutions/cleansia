---
id: T-0257
title: "AUD-07b — extract promo+referral validation + city-serviced collaborators from order-wizard facade + drop firstValueFrom"
status: blocked
size: M
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0251, T-0256]
blocks: [T-0258]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0200 split (Batch 5F sub-step b); AUD-07
---

## Context
Child of the **T-0200 (AUD-07)** order-wizard god-facade rebuild (Batch **5F**). Second serial sub-step. Extracts
two more concerns from `order-wizard.facade.ts`:

- **Promo + referral validation** — `setPromoCode`/`setReferralCode`/`validatePromoCodeNow`/
  `validateReferralCodeNow`/`clearPromoCode`/`clearReferralCode` with their `firstValueFrom` calls and
  `PromoCodeUiState`/`ReferralUiState`.
- **Service-area (city-serviced) check** — the `cityServiced` signal + `lastCityCheckKey` cache.

These move into focused collaborators (a promo+referral validation service and a city-serviced/service-area
service). The `firstValueFrom(...)` calls are converted to the C1 `UnsubscribeControlDirective` +
`takeUntil(this.destroyed$)` observable form (C1 forbids bare `firstValueFrom` as the default per consistency.md
line 86), and client calls use the C3 pipe. **This is a refactor, NOT a behavior change** — same
`PromoCodeUiState`/`ReferralUiState` results and same `cityServiced` `idle→pending→ok|rejected|error`
transitions.

**Dependencies:** `blocked` on **T-0251** (C1 base) and **T-0256** (sub-step a — serial a→b→c on
`order-wizard/**`). `blocks: [T-0258]`. Rebase on the post-T-0256 facade.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST / characterization spec)** — A facade Jest spec pins
  `validatePromoCodeNow`/`validateReferralCodeNow` returning the current `PromoCodeUiState`/`ReferralUiState` and
  the `cityServiced` `idle→pending→ok|rejected|error` transitions against the **unchanged** (post-T-0256) facade
  and is **green before** the refactor (status log shows spec first).
- [ ] **AC2 (extraction)** — Promo+referral validation and city-serviced check move into focused, separately-
  providable collaborators; the orchestrating facade's line/concern count drops further; each collaborator has
  its own Jest spec.
- [ ] **AC3 (C1 + C3 migration, drop firstValueFrom)** — The extracted collaborators extend
  `UnsubscribeControlDirective` + use `takeUntil(this.destroyed$)`; the `firstValueFrom` calls in
  promo/referral validation are converted to the `takeUntil` observable form (no bare `firstValueFrom` remains
  in the migrated code); every client call uses the C3 pipe.
- [ ] **AC4 (behavior identical)** — AC1's spec re-runs **unchanged** and is **still green** — same promo/referral
  states, same city-serviced transitions.
- [ ] **AC5 (consistency clean)** — `check-consistency.mjs frontend --paths=…/order-wizard` reports zero C1/C3
  violations for the promo/referral/city code; no new C2/C8 introduced.
- [ ] **AC6** — The `cleansia-app` project builds/lint/test green; Reviewer confirms refactor-only and **no NSwag
  edit** → **no nswag-regen**.

## Out of scope
- Quote/pricing extraction (sub-step a, T-0256) and saved-address + facade slim-down + submit C1/C3 (sub-step c,
  T-0258).
- Any change to promo/referral/city-serviced semantics; the component template/`.ts` beyond wiring; any NSwag
  client or backend change.

## Implementation notes
- **TEST-FIRST:** characterization Jest spec green before the split commit (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §C — C1 (drop `firstValueFrom`), C3 pipe, C4 errors via
  `SnackbarService` (unchanged mapping).
- **Serialization — sole editor of `order-wizard/**` this window, SERIAL after T-0256** (a → b → c). No
  concurrent `order-wizard/**` editor.

## Status log
- 2026-06-13 — blocked (created by pm — split of T-0200 / AUD-07, Batch 5F sub-step b). Blocked on **T-0251**
  (C1 base) + **T-0256** (sub-step a). DoR otherwise met: AC observable, sized M, no migration/regen,
  refactor-only, lane-isolated, `blocks: [T-0258]`. Promotes to `ready` when T-0251 and T-0256 are `done`.
  Reviewer-per-developer.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
