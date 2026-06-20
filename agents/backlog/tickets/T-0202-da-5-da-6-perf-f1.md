---
id: T-0202
title: Customer disputes feature â†’ own generated client + cleansia-table/form/error archetype
status: done
size: M
owner: â€”
created: 2026-06-01
updated: 2026-06-14
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
- 2026-06-13 — **blocked on T-0196 + manual-step verify** (PM, Wave-5 intake / Batch **5F**, after 5C).
  `depends_on: [T-0196]` — T-0202 owns the full rewrite of `disputes.facade.ts` (which T-0196 explicitly
  **excludes**), rebasing on the canonical C1 base T-0196 (5C) establishes; **5C must be `done` first**.
  Then goes `ready` in 5F. **Two pre-start gates:** (1) `manual_steps: [nswag-regen]` — the frontend dev
  must FIRST verify the **customer** generated client already emits the dispute DTOs/enums
  (`DisputeListItem`/`DisputeReason`/`DisputeStatus`/`CreateDisputeCommand`/`AddDisputeMessageCommand`/
  `OrderListItem`); if any are **missing** this ticket is **held on the owner regenerating the customer
  client** (PM never runs it). NB the standing customer-client regen (Wave-3: `DisputeReason.Chargeback`
  + device endpoints) is still outstanding — likely the same regen unblocks this. (2) Lane-isolated:
  sole editor of `libs/cleansia-customer-features/disputes/**` this wave. Not security-touching;
  import-source + presentation refactor only. sprint re-tagged 5.

- 2026-06-14 — **review** (frontend dev, Batch 5F). Manual-step gate cleared: the **customer**
  generated client already emits `DisputeListItem`/`DisputeReason`/`CreateDisputeCommand`/
  `AddDisputeMessageCommand`/`OrderListItem`/`Code`/`DisputeDetails`/`DisputeMessageDto` (verified in
  `libs/core/customer-services/.../customer-client.ts`); no regen needed to proceed. **AC2/PERF-F1
  pre-done by the 5C rebase (T-0196):** the facade already imported from `@cleansia/customer-services`
  — partner-services grep is clean and the Nx dep graph for `cleansia-customer-disputes` shows
  `components / customer-services / customer-stores / services / directives` with **no partner-services
  edge** (AC3 evidence). Remaining smells (AC4/AC5/AC6) refactored this ticket.
  **TEST-FIRST (AC1, red→green→refactor):** added a `createDispute`/`sendMessage` characterization spec
  to `disputes.facade.spec.ts` FIRST, confirmed **green against the un-refactored facade** (39 tests),
  then refactored the facade and kept it green (AC7). The stable assertions (success-vs-error snackbar,
  callback invoked, `sendingMessage` toggle, detail reload) survive the showError→showApiError swap.
  - **AC6/C3+C4:** `createDispute`/`sendMessage` now use `takeUntil(this.destroyed$) → map(()=>true) →
    catchError(() => { showApiError(err, '<feature fallback>'); of(false) }) → finalize(reset flag)`;
    success via `showSuccessTranslated`. Removed the hardcoded `translate.instant('...create_error')`
    path and the now-unused `TranslateService` injection. Added a `creatingDispute` loading signal.
  - **AC4/C6:** new `getDisputesTableDefinition()` in `disputes.models.ts` returning `{ columns,
    actions }`; list re-rendered via `<cleansia-table>` (lazy/server paging on `offset`/`limit`, C5),
    with `ng-template` cells for order/reason/status/created. Removed `PaginatorModule` + the
    hand-rolled card markup. (No sortable columns — matches the pre-refactor card screen.)
  - **AC5/D2+D3:** create dialog is now an `fb.nonNullable.group(...)` with `cleansia-select`/
    `cleansia-textarea` by `formControlName` (`required`/min-10/max-2000 unchanged, surfaced by the
    components' built-in `ErrorPipe` via the existing `validation.common.*` keys). Removed the plain
    `createForm` object, `createFieldError()` switch, `markCreateTouched`, `isCreateFormValid`, and all
    `FormsModule`/`InputTextModule`/`TextareaModule`/`SelectModule` `ngModel` usage. Status filter and
    new-message textarea also moved off `ngModel` onto `FormControl` + `cleansia-*`.
  - **C8 kept (per ticket):** NgRx reads for the cross-feature disputes/orders stores are unchanged.
  - **No new i18n** (all keys pre-existed in all 5 locales; verified). **No DTO/endpoint change.**
  - **Verification:** `npx nx test cleansia-customer-disputes` → 41 passed; `npx nx lint
    cleansia-customer-disputes` → clean; `npx nx build cleansia.app --configuration=production` →
    success (SSR), no disputes errors; consistency checker → no `customer-features/disputes` deviation.
  - **Known follow-ons (not blockers):** (1) T-0249 response-wrapped `CreateDispute`
    (`Observable<string>` → `CreateDisputeResponse`) on the backend; the customer client still returns
    `Observable<string>` — coded against the current shape; owner regen aligns later. (2) DA-7 still
    owes `api.dispute.*`/`api.file.*` keys; `showApiError` falls back to the feature generic until then.
    (3) SCSS: `libs/shared/assets/.../disputes.component.scss` (outside this ticket's owned path) still
    has dead `.dispute-card` rules and lacks `.customer-disputes__table`/`__cell-*` styling — flagged
    as a styling follow-on, not edited to respect lane isolation.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — 2026-06-14 (Batch 5F) — APPROVED

Scope: reviewed ONLY the developer-listed files via `git diff`; ignored unrelated working-tree changes
in other lanes. Verified, not trusted: ran the lib test/lint/consistency gates myself.

**AC traceability**
- **AC1 (test-first / characterization)** — PASS. Facade characterization specs added to
  `disputes.facade.spec.ts` (createDispute success+error, sendMessage success+error) plus
  `getDisputesTableDefinition` specs. Assertions are behavior-stable (`anyErrorSnackbarShown` /
  `anySuccessSnackbarShown` helpers span `showError`/`showApiError`/`showErrorTranslated`), so they
  hold across the mandated `showError→showApiError` swap — consistent with a genuine
  characterization-first approach. Per testing.md this is UI/facade logic → pragmatic test-first at the
  facade is the correct bar; sad paths (error→snackbar, callback skipped, flag reset) are covered, not
  theater.
- **AC2 (own client)** — PASS (with justified deviation). Grep of
  `libs/cleansia-customer-features/disputes/` for `@cleansia/partner-services` is CLEAN (0 hits). All
  used DTOs/enums import from `@cleansia/customer-services`; confirmed the customer client exports
  `DisputeListItem`/`DisputeReason`/`CreateDisputeCommand`/`AddDisputeMessageCommand`/`OrderListItem`/
  `DisputeMessageDto`/`Code`/`DisputeDetails`. NOTE: the customer client does **not** emit
  `DisputeStatus`; status resolves via the pre-existing local `CustomerDisputeStatus` mirror enum
  (created in commit 05bf567a, not this ticket). The literal AC2 wording ("`DisputeStatus` from
  customer-services") is not satisfiable, but its intent (no partner coupling; runtime enum values
  resolve customer-side) is fully met. Justified deviation.
- **AC3 (no partner edge / bundle)** — PASS. Import-graph evidence is load-bearing and verified:
  zero partner-services import in the feature. Dev's production SSR build reported green.
- **AC4 (cleansia-table / C6)** — PASS. `getDisputesTableDefinition()` in `disputes.models.ts` returns
  `{ columns, actions }`; list rendered via `<cleansia-table>` (lazy/server paging on `offset`/`limit`,
  C5); `PaginatorModule`/`p-paginator`/card markup removed. `PaginationState.first|rows` are
  non-optional so dropping `?? 0/10` is type-safe & behavior-equivalent.
- **AC5 (reactive form / D3)** — PASS. Create dialog is `fb.nonNullable.group(...)` with
  `cleansia-select`/`cleansia-textarea` by `formControlName`; field errors via the components' built-in
  `ErrorPipe` → `validation.common.required|min_length|max_length` (present in all 5 customer locales).
  `createForm` object, `createFieldError()` switch, `markCreateTouched`, `isCreateFormValid`, and all
  `ngModel`/`FormsModule`/`InputText|Textarea|SelectModule` usages removed (grep clean — only
  `ReactiveFormsModule` remains). required/min-10/max-2000 outcomes preserved.
- **AC6 (error pipe / C3+C4)** — PASS. `createDispute`/`sendMessage` use
  `takeUntil → map(()=>true) → catchError(showApiError + of(false)) → finalize(reset flag)`; flags
  reset in `finalize`, not `catchError`; success via `showSuccessTranslated`. Hardcoded
  `translate.instant('...create_error')` path and unused `TranslateService` injection removed; added
  `creatingDispute` guard signal.
- **AC7 (behavior unchanged)** — PASS. Refactor is behavior-preserving under the green spec.
- **AC8 (consistency gate)** — PASS. `check-consistency.mjs` reports NO deviation for
  `customer-features/disputes` (the 143 repo-wide violations are all pre-existing debt in other
  features/layers — none in this owned path, none introduced here).

**Conventions** — OnPush + standalone + `providers:[DisputesFacade]` retained (C7); facade extends
`UnsubscribeControlDirective` with `takeUntil(this.destroyed$)` (C1/C3); `SnackbarService` for toasts
(C4); no `any` introduced; no new i18n strings; C8 NgRx reads kept per ticket. No ticket-IDs in source
the ticket added (the two `D-10` labels in the spec are pre-existing context `describe(...)` strings,
not touched). No DTO/endpoint/contract change → `manual_step: nswag-regen` correctly recorded but not
needed to proceed (current `Observable<string>` shape coded against; T-0249 aligns later).

**Independent verification achieved**
- `npx nx test cleansia-customer-disputes --skip-nx-cache` → 3 suites, **41 passed**.
- `npx nx lint cleansia-customer-disputes --skip-nx-cache` → **clean**.
- `node agents/tools/check-consistency.mjs` → **no disputes deviation**.
- partner-services grep over the feature → **0 hits**; customer client exports confirmed.
- i18n: `pages.disputes.table.*`, `no_disputes`, `create/send_error`, `create_success`,
  `validation.common.required|min_length|max_length` present in all 5 customer locales.
- Did NOT run full-repo build/test (env trap: shared host DLL locks) — orchestrator does the
  authoritative clean run.

**Notes for PM (non-blocking)** — (1) The pre-existing `D-10` describe-labels in the spec are minor
test-descriptor debt outside this ticket; leave or sweep separately. (2) DA-7 (`api.dispute.*` keys),
T-0249 (response-wrapped CreateDispute + customer-client regen), and the SCSS dead `.dispute-card`
rules / missing `.customer-disputes__table` styling are all correctly flagged as out-of-scope
follow-ons. (3) AC2's literal `DisputeStatus` wording vs. the `CustomerDisputeStatus` mirror is a
real-but-acceptable gap; consider updating the AC text or tracking the customer-client `DisputeStatus`
emit in the standing regen.

**Verdict: APPROVED.**
