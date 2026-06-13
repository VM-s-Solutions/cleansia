---
id: T-0256
title: "AUD-07a — extract quote/pricing collaborator from order-wizard facade + C3-migrate its stream"
status: blocked
size: M
owner: —
created: 2026-06-13
updated: 2026-06-13
depends_on: [T-0251]
blocks: [T-0257]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0200 split (Batch 5F sub-step a); AUD-07
---

## Context
Child of the **T-0200 (AUD-07)** order-wizard god-facade rebuild (Batch **5F**). The facade
`src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts` is
**~977 lines** today (title elsewhere cites 1048 — drifted post-Wave-0) braiding seven concerns. This first
sub-step extracts the **live server-quote engine**:

- the `toObservable(this.quoteInputs)` → `debounceTime → distinctUntilChanged → switchMap(QuoteOrder) →
  catchError → subscribe` stream, plus `refreshQuoteNow()`,
- the `quote`/`quoting`/`lastQuotedInputs` signals,
- the express-surcharge `isExpressSlot`/`displayedTotalPrice` computeds.

These move into a focused quote/pricing collaborator (service or sub-facade). The extracted stream is **migrated
onto the C3 canonical pipe** (`takeUntil(this.destroyed$) → catchError(() => of(null)) →
finalize(() => this.quoting.set(false))` — reset via `finalize`, not inline in `subscribe`), and onto the C1
`UnsubscribeControlDirective` cleanup paradigm. **This is a refactor, NOT a behavior change** — same quote/total
values and express math for the same inputs.

**Dependency:** `blocked` on **T-0251** (the T-0196 C* / 5C.D sub-stream that establishes the C1
`UnsubscribeControlDirective` base across the customer facades, including the order-wizard facade's C1 paradigm).
Do not start until T-0251 is `done`. `blocks: [T-0257]` — the three AUD-07 sub-steps land **serially** on
`order-wizard/**` (a → b → c); never concurrent.

**Lane note:** T-0218 (Wave-4 a11y) already touched the order-wizard markup/components; the **facade** is this
ticket's surface and was not refactored by T-0218 — verify no merge surprise.

## Acceptance criteria
- [ ] **AC1 (TEST-FIRST / characterization spec)** — A facade Jest spec pins the current quote behavior (the
  three data states; the debounced live quote populating `quote()`/`totalPrice()`; the
  `isExpressSlot`/`displayedTotalPrice` math) against the **unchanged** facade and is **green before** the
  refactor (per `testing.md`; status log shows spec first).
- [ ] **AC2 (extraction)** — The quote/pricing concern moves into a focused, separately-providable collaborator;
  the orchestrating facade's line/concern count drops; the collaborator has its own Jest spec.
- [ ] **AC3 (C1 + C3 migration of the quote stream)** — The extracted collaborator extends
  `UnsubscribeControlDirective` + uses `takeUntil(this.destroyed$)` (no `DestroyRef`/`takeUntilDestroyed`/bare
  `firstValueFrom` in the migrated code); the quote stream resets `quoting` via `finalize`, not inline in
  `subscribe`.
- [ ] **AC4 (behavior identical)** — AC1's spec re-runs **unchanged** and is **still green** — same quote/total
  values, same express math.
- [ ] **AC5 (consistency clean for the touched code)** — `check-consistency.mjs frontend
  --paths=…/order-wizard` reports zero C1/C3 violations for the quote/pricing code touched; no new C2/C8 introduced.
- [ ] **AC6** — The `cleansia-app` (customer) project builds/lint/test green; Reviewer confirms refactor-only and
  **no generated-client (NSwag) edit** → **no nswag-regen**.

## Out of scope
- Promo+referral+city-serviced extraction (sub-step b, T-0257) and saved-address + facade slim-down + submit
  C1/C3 (sub-step c, T-0258).
- Any wizard behavior change (express-surcharge rate/lead-time constants, quote semantics); the component
  template/`.ts` beyond mechanical wiring; any NSwag client or backend change.

## Implementation notes
- **TEST-FIRST:** characterization Jest spec green before the split commit (Gate 6).
- **Canonical pattern:** `knowledge/consistency.md` §C — C1 (`UnsubscribeControlDirective` +
  `takeUntil(this.destroyed$)`), C3 (the exact pipe; reset in `finalize`); samples in
  `knowledge/patterns-frontend.md`.
- **Serialization — sole editor of `libs/cleansia-customer-features/order-wizard/**` this window.** The three
  AUD-07 sub-steps run **serially** (a → b → c). No concurrent editor of `order-wizard/**`. New collaborator
  files are fresh (no collision).

## Status log
- 2026-06-13 — blocked (created by pm — split of T-0200 / AUD-07, Batch 5F sub-step a). Blocked on **T-0251**
  (the T-0196 C* base / 5C.D establishing the C1 `UnsubscribeControlDirective` base). DoR otherwise met: AC
  observable (characterization spec), sized M, no migration/regen, refactor-only, lane-isolated on
  `order-wizard/**`, `blocks: [T-0257]` (serial a→b→c). Promotes to `ready` when T-0251 is `done`.
  Reviewer-per-developer.

## Review
<!-- reviewer writes verdict here; PM reconciles before advancing state -->
