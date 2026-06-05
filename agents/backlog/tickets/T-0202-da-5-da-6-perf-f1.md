---
id: T-0202
title: Customer disputes feature â†’ own generated client + cleansia-table/form/error archetype
status: draft
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-01
depends_on: [T-0196]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: [nswag-regen]
sprint: 3
source: findings DA-5/DA-6/PERF-F1
---

## Context

Wave-3 consistency cleanup of the **customer** disputes feature
(`libs/cleansia-customer-features/disputes/`). Three findings converge on the same two files
(`disputes.facade.ts`, `disputes.component.ts`):

- **DA-5 / PERF-F1 (cross-app client coupling, SSR bundle risk):** the customer feature imports its
  DTOs/enums from the **partner** generated client. `disputes.facade.ts:15-21` and
  `disputes.component.ts:6-10` pull `DisputeListItem`, `DisputeReason`, `DisputeStatus`,
  `CreateDisputeCommand`, `AddDisputeMessageCommand`, `OrderListItem` from
  `@cleansia/partner-services` â€” even though the runtime calls correctly go through
  `CustomerClient.disputeClient` (`@cleansia/customer-services`). `DisputeReason`/`DisputeStatus` are
  used as **runtime** values (`DisputeReason.QualityIssue`, `switch (status?.value) { case
  DisputeStatus.Pending }`), so the partner NSwag client is dragged into the customer module graph
  and onto the **SSR server build**. If the two separately-generated specs diverge, the screen binds
  to types that do not match what `CustomerClient` returns â€” a latent break on the next regen.

- **DA-6 (list-feature archetype violation):** the disputes list reimplements paging/tables/forms/
  error-handling by hand instead of the canonical customer archetype:
  - **C6:** `PaginatorModule` + a hand-rolled table template instead of `cleansia-table` fed by a
    `getDisputesTableDefinition()` returning `{ columns, actions }` from a `disputes.models.ts`
    (there is no `disputes.models.ts` today; the locales already define `pages.disputes.table.*`,
    so a table was intended).
  - **D3:** the create dialog uses a plain `createForm` object with a manual
    `createFieldError()` string-switch and raw PrimeNG/`FormsModule` inputs, not `cleansia-*` bound
    by `formControlName` + `ErrorPipe` on an `fb.nonNullable.group(...)`.
  - **C3/C4:** `createDispute`/`sendMessage` use bare `.subscribe({ next, error })` with `takeUntil`
    only (no `catchError(() => of(null)) â†’ finalize(...)`) and surface failures via hardcoded
    `translate.instant('pages.disputes.create_error' | 'send_error')` instead of
    `SnackbarService.showApiError(err)`, so the backend `BusinessErrorMessage` reason is never shown.

This is a **refactor, not a behavior change** â€” the disputes screen must look and behave identically
to the user; we are removing the smells, not changing the contract.

> **Note (scope boundary):** DA-7 (the empty `errors.*` object in the 5 customer locales) is a
> separate ticket; without those keys `showApiError` cannot resolve a specific message. This ticket
> wires `showApiError` (the canonical C4 path) so it lights up automatically once DA-7 lands; until
> then it degrades to the API's generic message â€” still an improvement over a hardcoded string, and
> behavior-identical at the snackbar boundary.

## Acceptance criteria

- [ ] **AC1 (characterization-test-first, TEST-FIRST per testing.md)** â€” Given the disputes feature
      has **no spec today**, When work starts, Then a Jest **facade** characterization spec is added
      **first** (redâ†’greenâ†’refactor; visible before the refactor in the diff/commits and noted in the
      status log) pinning the *current* observable behavior: `loadDisputes` dispatches
      `loadCustomerDisputes({ offset, limit })`; `createDispute` calls
      `customerClient.disputeClient.create` and on success shows the success snackbar + invokes the
      callback, on error surfaces an error snackbar; `sendMessage` toggles `sendingMessage`, calls
      `addMessage`, and reloads the detail on success. The spec is **green against the existing code**
      before any refactor.

- [ ] **AC2 (DA-5/PERF-F1 â€” own client)** â€” Given the customer disputes feature, When the refactor
      is complete, Then `DisputeListItem`, `DisputeReason`, `DisputeStatus`, `CreateDisputeCommand`,
      `AddDisputeMessageCommand`, and `OrderListItem` are imported from **`@cleansia/customer-services`**
      and **no file under `libs/cleansia-customer-features/disputes/`** imports from
      `@cleansia/partner-services` (grep is clean). The runtime enum values (`DisputeReason.*`,
      `DisputeStatus.*`) resolve from the customer client.

- [ ] **AC3 (PERF-F1 â€” bundle)** â€” Given the customer app build, When a production/SSR build runs,
      Then `@cleansia/partner-services` is no longer pulled into the disputes chunk via this feature
      (verified by bundle analysis / Nx dep graph showing no `cleansia-customer-features/disputes â†’
      partner-services` edge). Evidence attached at review.

- [ ] **AC4 (DA-6/C6 â€” cleansia-table)** â€” Given the disputes list, When rendered, Then it uses
      `<cleansia-table>` fed by a single `getDisputesTableDefinition(...)` returning
      `{ columns, actions }` in a new `disputes.models.ts`; `PaginatorModule` + the hand-rolled
      table markup are removed; server-side paging (`offset`/`limit`) is preserved (C5). The visible
      columns/rows/sort/paging match the pre-refactor screen.

- [ ] **AC5 (DA-6/D3 â€” reactive form)** â€” Given the create-dispute dialog, When opened, Then it is an
      `fb.nonNullable.group(...)` reactive form with `cleansia-*` controls bound by `formControlName`
      and field errors via `ErrorPipe`; the plain `createForm` object, `createFieldError()` switch,
      `markCreateTouched`, and `FormsModule`/`InputTextModule`/`TextareaModule`/`SelectModule`
      `ngModel` usage are removed. Validation outcomes (required, min-10, max-2000) are unchanged.

- [ ] **AC6 (DA-6/C3+C4 â€” error pipe)** â€” Given a failed `create`/`addMessage` call, When it errors,
      Then the facade uses the canonical client pipe `takeUntil(this.destroyed$) â†’ catchError(() =>
      of(null)) â†’ finalize(...)` and surfaces the error via `SnackbarService.showApiError(err)` (not
      a hardcoded `translate.instant('...create_error')`); the loading/`sendingMessage` flags reset
      in `finalize`, not inline in `catchError`.

- [ ] **AC7 (behavior unchanged)** â€” Given the refactor, When the AC1 characterization spec and any
      facade specs run, Then they are **green** with no assertion changes that alter observed
      behavior; the disputes list, create dialog, and detail dialog are functionally identical to the
      user (manual QA walk-through evidence attached).

- [ ] **AC8 (consistency gate)** â€” Given the touched area, When
      `node agents/tools/check-consistency.mjs` runs scoped to
      `libs/cleansia-customer-features/disputes/`, Then it reports **clean** (no C3/C4/C6/D3 or
      wrong-client deviations) for these files; `npx nx lint cleansia-customer-features-disputes` and
      `npx nx test` for the feature pass.

## Out of scope

- **DA-7** â€” adding `errors.dispute.*` / `errors.address.*` / `errors.file.*` keys to the 5 customer
  locales (separate ticket); this ticket only routes errors through `showApiError`.
- **PERF-F2** â€” lazy-loading the order-select options on dialog-open instead of `ngOnInit` (separate
  perf ticket).
- The **partner** disputes feature and any backend dispute handlers/controllers (DA-2/DA-3/DA-4,
  D-01 bundle).
- Migrating the **other 18** customer features that also import `@cleansia/partner-services` â€” those
  are their own tickets; this ticket is the disputes feature only.
- No backend, DTO, endpoint, or contract changes. No new translation keys beyond what already exists.

## Implementation notes

- **TEST-FIRST (testing.md, "changing existing untested code"):** this is untested code being
  refactored, so write the **characterization spec first** (AC1), confirm it passes against the
  current implementation, then apply the canonical pattern with the spec staying green. The status
  log must show "red â†’ green" ordering; reviewer enforces Gate 6 on commit order.
- **Canonical patterns (consistency.md):**
  - **C6** â€” `cleansia-table` + a single `getXxxTableDefinition()` returning `{ columns, actions }`
    in `*.models.ts`; never `p-table`/`PaginatorModule` or split column/action getters.
  - **C5** â€” server-side paging only (`offset`/`limit`); no client-side slicing.
  - **C3** â€” the exact client pipe `takeUntil(this.destroyed$) â†’ catchError(() => of(null)) â†’
    finalize(() => this.loading.set(false))`; reset flags in `finalize`, never in `catchError`.
  - **C4** â€” errors via `SnackbarService.showApiError`/`showError`, never inline strings.
  - **C8** â€” the disputes facade already mixes `store.dispatch`/`store.select` (NgRx) with signals;
    keep the NgRx reads (the customer disputes/orders stores are pre-existing cross-feature state) â€”
    do **not** rip NgRx out as part of this ticket, only fix the client source + table/form/error
    smells. (The C8 NgRx-vs-signals question is its own tracked deviation.)
  - **D2/D3** â€” `fb.nonNullable.group(...)`, `cleansia-*` by `formControlName`, errors via
    `ErrorPipe`; no `fb.group({})` mixing, no raw PrimeNG/`ngModel` form fields.
  - **C7** â€” keep `standalone: true` + `ChangeDetectionStrategy.OnPush` (already present).
- **Serialization / collisions:** per TICKET-MAP, the disputes feature files
  (`disputes.facade.ts`, `disputes.component.ts`, `disputes.component.html`, new
  `disputes.models.ts`, new spec) are touched only by this ticket; do not run concurrently with any
  other ticket editing these files.
- **manual_step: nswag-regen (owner-only â€” DO NOT run):** PERF-F1's fix presumes the **customer**
  generated client already emits the equivalent dispute DTOs/enums (`DisputeListItem`,
  `DisputeReason`, `DisputeStatus`, `CreateDisputeCommand`, `AddDisputeMessageCommand`,
  `OrderListItem`). The frontend dev must **first verify** these exist under
  `libs/core/customer-services/.../client/`. If any are **missing**, this ticket is **blocked** on
  the owner regenerating the customer client (`npm run generate-customer-client`) â€” flag it and hold;
  the PM never runs it. No backend serialization is needed since no contract changes.
- **No serialization of data shape:** this is purely an import-source + presentation refactor; the
  command/response wire shapes are unchanged.

## Status log
- 2026-06-01 â€” draft (created by pm)

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
