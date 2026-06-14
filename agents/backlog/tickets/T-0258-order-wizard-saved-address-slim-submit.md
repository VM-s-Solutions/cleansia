---
id: T-0258
title: "AUD-07c — extract saved-address collaborator + slim order-wizard facade (step-nav + submit) + C1/C3-migrate submit branches"
status: done
size: M
owner: frontend
created: 2026-06-13
updated: 2026-06-14
depends_on: [T-0251, T-0257]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0200 split (Batch 5F sub-step c); AUD-07
---

## Context
Child of the **T-0200 (AUD-07)** order-wizard god-facade rebuild (Batch **5F**). Final serial sub-step. Extracts
the saved-address concern and slims the orchestrating facade to step-navigation + submit:

- **Saved-address management** — `savedAddresses`/`selectedSavedAddressId`, `selectSavedAddress`,
  `isSavedAddressSelected`, `saveCurrentAddressAsSaved`, `updateAddressFromForm` → a saved-address collaborator.
- **Catalog/category filtering** (`services`/`packages`/`extras`/`countries`, `categories`/`filteredServices`,
  `setCategory`) is reduced to legitimate shared-store reads (`selectCustomerServices`/`selectCustomerPackages`/
  `SavedAddressStore`) — do NOT push per-feature state into NgRx (C8).
- **Step navigation + rebook prefill** (`activeStep`/`steps`/`stepIcons`, `nextStep`/`prevStep`/`goToStep`/
  `canProceed`, `prefillFromRebook`, `initialize`) stays in the slimmed orchestrating facade.
- **Order submission** — `submitOrder(...)` Card vs Cash branches (`paymentClient.createOrder` /
  `orderClient.createOrder`) — both branches gain the C3 pipe (`takeUntil → catchError(() => of(null)) →
  finalize`) and the facade extends `UnsubscribeControlDirective` (C1).

After this, the facade reads as orchestration (step-nav + submit) with materially reduced size/concern count.
**This is a refactor, NOT a behavior change** — same `canProceed` gating, same `prefillFromRebook` outputs, same
Card/Cash submit outcomes/navigation and error→`SnackbarService` mapping.

**Dependencies:** `blocked` on **T-0251** (C1 base) and **T-0257** (sub-step b — serial a→b→c on
`order-wizard/**`). After this lands, T-0200 (the epic) is `done`. Rebase on the post-T-0257 facade.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST / characterization spec)** — A facade Jest spec pins `canProceed()` per step,
  `prefillFromRebook` outputs, saved-address handling, and `submitOrder` taking the Card branch
  (`paymentClient.createOrder`) vs the Cash branch (`orderClient.createOrder`) with its success-navigation and
  error→`SnackbarService` mapping, against the **unchanged** (post-T-0257) facade, **green before** the refactor.
- [ ] **AC2 (extraction + slim-down)** — Saved-address moves into a focused collaborator; the orchestrating
  facade retains step-navigation + submit orchestration only; its line/concern count is materially reduced; no
  single collaborator reconstitutes the god-unit. Each non-trivial extracted unit has its own Jest spec.
- [ ] **AC3 (C1 + C3 migration of submit + facade)** — The facade extends `UnsubscribeControlDirective` + uses
  `takeUntil(this.destroyed$)` (the C1-line-86 deviation is gone — no `DestroyRef`/`takeUntilDestroyed`/bare
  `firstValueFrom` remain anywhere in the order-wizard facade tree); **both submit branches** use the C3 pipe
  (`catchError(() => of(null))` + `finalize`).
- [ ] **AC4 (behavior identical)** — AC1's spec re-runs **unchanged** and is **still green** — same `canProceed`
  gating, same Card/Cash submit outcomes/navigation, same error→snackbar mapping, same submit payloads
  (`CreateOrderCommand` unchanged).
- [ ] **AC5 (consistency clean — whole order-wizard surface)** — `check-consistency.mjs frontend
  --paths=…/order-wizard` reports **zero** violations for the order-wizard files (C1 and C3 clear across the
  whole facade tree; no new C2/C4/C8 introduced beyond legitimate shared catalog/`SavedAddressStore` reads).
- [ ] **AC6** — The `cleansia-app` project builds/lint/test green; Reviewer confirms refactor-only and **no NSwag
  edit** → **no nswag-regen**.

## Out of scope
- Quote/pricing (T-0256) and promo/referral/city-serviced (T-0257) extraction — done in the prior sub-steps.
- Any change to the submit payload (`CreateOrderCommand`) or post-submit navigation; the component template/`.ts`
  beyond wiring; any NSwag client or backend change; the de-triplication of the wizard against partner/mobile
  booking flows (separate tail tickets).

## Implementation notes
- **TEST-FIRST:** characterization Jest spec green before the split commit (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §C — C1, C3 (both submit branches), C4 (`SnackbarService`),
  C8 (keep NgRx for genuinely cross-feature catalog/`SavedAddressStore` reads only — do not add per-feature state
  into NgRx during the split).
- **Serialization — sole editor of `order-wizard/**` this window, SERIAL after T-0257** (a → b → c). When `done`,
  mark the T-0200 epic `done`.

## Status log
- 2026-06-13 — blocked (created by pm — split of T-0200 / AUD-07, Batch 5F sub-step c). Blocked on **T-0251**
  (C1 base) + **T-0257** (sub-step b). DoR otherwise met: AC observable, sized M, no migration/regen,
  refactor-only, lane-isolated. Promotes to `ready` when T-0251 and T-0257 are `done`. Reviewer-per-developer.
  Closes the T-0200 epic when `done`.
- 2026-06-14 — review (frontend). Implemented test-first per Gate 6:
  - **AC1** — augmented `order-wizard.facade.spec.ts` with characterization for `canProceed()` per step,
    `prefillFromRebook` outputs, saved-address handling, and submit-payload XOR branches (savedAddressId vs
    customerAddress) against the **unchanged** facade — **green before** the split (59 facade tests passing
    pre-refactor).
  - **AC2** — extracted saved-address into `order-saved-address.facade.ts` (@Injectable, extends
    `UnsubscribeControlDirective`, `connect()` with currentFormData/patchFormData deps), with its own
    `order-saved-address.facade.spec.ts`. Wizard facade re-exposes `savedAddresses`/`selectedSavedAddressId`
    and delegates `selectSavedAddress`/`isSavedAddressSelected`/`saveCurrentAddressAsSaved`/
    `updateAddressFromForm`/`applyAddressSuggestion` (mirrors the pricing/promo/service-area collaborator
    idiom). Facade now reads as step-nav + submit orchestration (701→662 lines; saved-address concern is a
    119-line focused collaborator). Catalog/category filtering left as legitimate shared-store reads (C8, no
    per-feature state pushed into NgRx).
  - **AC3** — wizard facade already extends `UnsubscribeControlDirective`; **both** submit branches migrated
    to the C3 pipe (`takeUntil(this.destroyed$) → catchError(() => of(null)) → finalize(submitting.set(false))`);
    error→`SnackbarService` handled via the `null`-response branch in `next`. No `DestroyRef`/
    `takeUntilDestroyed`/bare `firstValueFrom` remain in the wizard facade (the prior C1-line-86 deviation is
    gone — `firstValueFrom` no longer appears in `order-wizard.facade.ts`).
  - **AC4** — AC1's spec re-runs unchanged and stays green after the refactor (same `canProceed` gating,
    same Card/Cash outcomes/navigation, same snackbar mapping, same `CreateOrderCommand` payload).
  - **AC5** — `check-consistency.mjs frontend --paths=…/order-wizard` → `OK (18 files scanned)`, zero
    violations across the whole order-wizard surface (C1/C3 clean; no new C2/C4/C8).
  - **AC6** — `npx nx test cleansia-customer-order-wizard` → 6 suites / 119 tests green;
    `nx lint cleansia-customer-order-wizard` → clean; `nx build cleansia.app --configuration=production` →
    success. Refactor-only; no NSwag edit → **no nswag-regen**.
  - Note: pre-existing unrelated NG8102 warning in `wizard-summary-step.component.html:117` (`extra.price ?? 0`)
    — out of scope, reported not fixed. Closes the T-0200 epic when verified.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
