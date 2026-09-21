---
id: T-0796
title: One confirmation, one toast, one loading, one empty state — the root `DialogService`, PrimeNG locale from the bundle, no double toasts, in-place skeletons, `not-found-state` and `empty-state`, one Cancel key
status: todo
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785]
blocks: [T-0788, T-0790]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"make overall check on both apps to make them consistent"*. CS §6: the
admin app carries 17 local `<p-confirmDialog>`s and 18 `providers: [ConfirmationService]` beside the
root one the shared `DialogService` already uses; the PrimeNG locale is a hard-coded English block
(`admin app.config.ts:59-73` — `Yes` / `No` / `Today` in every Czech session); 37 facade sites toast
**twice** per failure (the interceptor and a `catchError` with a page-local `*_ERROR_KEY_MAP` whose
keys already exist under `api.*`); admin detail pages use `cleansia-loader` — a `position: fixed`
100vh overlay covering the sidebar — where the partner uses in-place skeletons; two admin details
render a blank card for a missing entity; six ad-hoc `*__empty` classes; five Cancel keys.

Paths are relative to `src/Cleansia.App/`.

## Doing

- **Confirmation:** `DialogService.confirmTranslated` / `confirmDelete`
  (`libs/core/services/src/lib/services/dialog.service.ts:23-71`) is the only idiom. Delete the 17
  admin `<p-confirmDialog>` (`admin-user-management:135`, `company-info-list:135`,
  `country-management:48`, `service-area-management:223`, `currency-management:48`,
  `document-requirements:109`, `employee-detail:1029`, `extra-management:142`, `invoice-detail:327`,
  `language-management:131`, `promo-code-detail:118`, `promo-codes-list:57`, `package-management:148`,
  `pay-config-management:55`, `service-management:148`, `email-type-detail:253`) + partner `gdpr:70`,
  and every `providers: [ConfirmationService]` (18); the root one (`admin app.config.ts:92`, `partner
  app.config.ts:79`) is what the service needs. The 18 files / 33 direct `.confirm(` calls become
  `dialogService.confirm*`.
- **PrimeNG locale:** one `providePrimeNgTranslation()` in `libs/core/services` reading `primeng.*`
  keys from the bundle and re-applying on `onLangChange`; delete the hard-coded English block
  `admin app.config.ts:59-73`; the partner gets it too (sets none today).
- **Toasts:** retire the 16 admin `*_ERROR_KEY_MAP` + `resolveXxxErrorKey()` pairs whose keys already
  exist under `api.*` (the parity spec proves which) — e.g. `country-management.facade.ts:59-63`;
  `showSuccess(translate.instant(key))` (107 admin) → `showSuccessTranslated(key)`;
  `SNACKBAR_ERROR_MAPPINGS` / `DEFAULT_SNACKBAR_ERROR_MAPPINGS` (`snackbar.service.ts:23,32`,
  referenced nowhere) go with them.
- **Loading:** admin detail / form pages stop using `cleansia-loader`
  (`components/cleansia-loader.component.scss:1-12`, 65 uses) and use the in-place
  `cleansia-detail-skeleton` / `cleansia-table-skeleton` the partner uses (23 uses);
  `cleansia-loader` stays for app boot.
- **Not found / empty:** `not-found-state` (`common/not-found-state.scss`, shipped in the admin
  bundle unused) on every admin detail whose `@if (loading) … @else if (entity())` has no else
  (`order-detail:38-40`, `employee-detail:24-26`; `dispute-detail:25-27` has it); one `empty-state`
  class in `common/` replacing the six ad-hoc classes (`report-empty`, `refund-empty`,
  `order-photos__empty`, `cleansia-dispute-detail__empty`, `currency-price-block__empty`,
  `cleansia-audit-entry__empty-value`); tables keep `emptyMessage` (T-0786 draws it).
- **Cancel key:** `global.actions.cancel` everywhere (`common.cancel`,
  `pages.promo_codes.form.cancel`, `pages.loyalty_tiers.form.cancel`, five `*_dialog.cancel_button`
  → one key); admin `common.*` deleted once the last reader moves (T-0793 deletes the partner's).
- **Guard:** `dialog.service.spec.ts` exists — extend with *"labels are translated"*; F3 / F4 / F5 /
  F15 in T-0798.

## NOT

- The two dialogs that cannot open (T-0790 fixes them first, as defects).
- The partner dialogs' widgets (T-0797). Any toast copy.

## Done looks like

`rg -l "<p-confirmDialog" apps libs` → the two shell templates only; `rg -c "_ERROR_KEY_MAP" libs` = 0
(or the frozen back-compat count the pattern catalogue names, with the list in this ticket); a
failing save on the admin country form shows **one** toast; a Czech session's delete confirmation
reads the bundle's words (`Ano` / `Ne`); `/order-management/does-not-exist` renders the not-found
block, not a blank card.

## Acceptance criteria

- [ ] **AC1** — Given the two apps, When `rg -l "<p-confirmDialog" apps libs` runs, Then only the two
      shell templates match and no feature lib provides its own `ConfirmationService`.
- [ ] **AC2** — Given a Czech session, When a delete is confirmed on any admin list, Then the
      dialog's buttons read the bundle's `primeng.*` words, not `Yes` / `No`; switching language
      re-applies them without reload.
- [ ] **AC3** — Given the admin country form, When a save fails with a backend key, Then exactly one
      toast renders, from the interceptor's `api.*` resolution.
- [ ] **AC4** — Given `/order-management/does-not-exist` and `/employee-management/does-not-exist`,
      When loaded, Then the not-found block renders inside the card, no blank page.
- [ ] **AC5** — Given an admin detail page while its entity loads, When rendered, Then an in-place
      skeleton shows inside the card and the sidebar stays uncovered.
- [ ] **AC6** — Given every Cancel button in both apps, When its key is read, Then it is
      `global.actions.cancel`; `common.*` is absent from both apps' locales.

## Implementation notes

Depends on T-0785. T-0790 applies the root-dialog idiom to the two broken dialogs regardless of
order; whichever lands second removes the remainder. **Regen:** none.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 2, web-shared lane, fourth of the serial four.
