---
id: T-0793
title: Web dead code — admin, partner and shared libs; unrouted components, the two user NgRx slices, declaration-only exports, dead i18n sections, orphan stylesheets; the free-trial UI remnants T-0690 left; the stale pre-ADR-0057 help copy
status: in_progress
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0791]
blocks: [T-0785, T-0797, T-0799]
stories: []
adrs: [ADR-0057]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"remove redundant and deprecated things that are no longer used"*. The
discovery (DC §3, CS §3.4 / §4.2) walked the two back-office web apps and the shared libs for
components with zero consumers, components no route reaches, store slices nothing reads, exports
nothing imports, i18n sections no template resolves and stylesheets no `@use` includes. Only the
zero-reader verdicts are here. It runs **before** the polish tickets so they do not restyle dead
CSS (12+ dead `.p-datatable` blocks, two orphan partner stylesheets, `CompanyInfoComponent`), and
after T-0791 because both touch `employee-detail.*`, the code stores and the locale files.

Paths are relative to `src/Cleansia.App/` unless they start with `src/`, `docs/` or `agents/`.
Line numbers are the discovery's (verified at `069b72ae`) — re-grep before each deletion.

## Doing — DELETE only (DC §3, CS §3.4 / §4.2)

- **Shared components with zero consumers:** `libs/shared/components/src/lib/cleansia-menu/`
  (selector `cleansia-cleansia-menu`, `lib/index.ts:21`),
  `cleansia-skeleton/cleansia-table-skeleton.component.ts` (`lib/index.ts:25`); pipes
  `payment-status-label.pipe.ts`, `payment-status-severity.pipe.ts`, `replace/replace.pipe.ts:7`.
  **`cleansia-multiselect` is KEPT** — T-0789 adopts it for `package-form.component.html:97`.
- **Unrouted admin components:** `company-management/src/lib/company-info/` (`CompanyInfoComponent`
  + facade, 4 files, 615 lines — the routes go to `CompanyInfoListComponent` / `CompanyInfoFormComponent`),
  `template-management/src/lib/email-template-form/` (4 files, 579 lines — not in `lib.routes.ts`).
- **NgRx:** admin `libs/data-access/admin-stores/src/lib/user/*` (7 files; `loadCurrent$` emits an
  empty item, `selectCurrentUser` has 0 readers; `store.config.ts` rows; the no-op dispatch at
  `admin-login.facade.ts:66`); partner `libs/data-access/partner-stores/src/lib/user/*` (`state.user`
  read by nothing; one wasted `GET /User/GetCurrent` per login at `login.facade.ts:48`,
  `confirm-email.facade.ts:47`); partner selectors `dashboard.selectors.ts:26,31,36,98,103,108,113`,
  `employee.selectors.ts:12,15`, `order.selectors.ts:16,24`; action `clearDashboard`
  (`dashboard.reducer.ts:192`).
- **Declaration-only exports (DC §3.5):** `filter.models.ts` everything but `BaseFilter` /
  `UserFilter` / `OrderFilter` (the Stroytorg/material/supplier/unit vocabulary, `:3-352`;
  `UserFilter` goes with the user slices); `page.models.ts:4-7,9,17,21,106-245,257` (`Page.create()`
  stays); `sort.models.ts:4,61-111,124,147,162,181,236`; `jwt-token.models.ts`;
  `state-adapter.models.ts:12,26`; `booking-window.models.ts:13`; `cookie.utils.ts:1,13,27`;
  `object.utils.ts:1,25`; `storage.utils.ts:26,47,54`; `error.models.ts:5,20`;
  `cleansia-base-form-input-controls.ts:4`, `cleansia-base-form.models.ts:6`;
  `cleansia-cookie-consent.component.ts:21,191`; `cleansia-file.component.ts:9`;
  `cleansia-table.models.ts:58-91` + `cleansia-table.component.ts:322`;
  `cleansia-telephone.component.ts:141`; `admin-order-refund.models.ts:20`,
  `order-detail.facade.ts:344`; component methods `audit-log.component.ts:165`,
  `extra-management.component.ts:158`, `service-management.component.ts:160`,
  `user-loyalty-detail.component.ts:513`, `reports.component.ts:332`, `email-type-detail.facade.ts:62`;
  partner `order-details.helpers.ts:159,250`, `dashboard.component.ts:53` + `dashboard.facade.ts:199`,
  `order-photos.component.ts:177`, `profile-documents.component.ts:229`, `profile.facade.ts:169`,
  `partner-auth.service.ts:194`; core `file-validation-error.service.ts:57`,
  `page-title.service.ts:62`; `mapbox-autocomplete.service.ts:17,53` (`@deprecated`, zero references).
- **Stray guard** `libs/core/admin-services/src/lib/guards/auth.guard.ts` (redirects to the
  **partner** login, no consumer).
- **Aliases:** `tsconfig.base.json` `@cleansia.app/template-management` (zero imports);
  `@cleansia.app/pay-periods` → `@cleansia/admin-features/pay-periods` (one import, `app.routes.ts:36`).
- **i18n whole dead sections, five locales each (DC §3.6):** admin `pages.login`, `pages.service_edit`,
  `employees.*`, `invoices.*`, `orders.*`, `common.loading`, the six
  `page_titles.admin.{invoice,receipt}_template*`, `validation.auth.admin_access_required`,
  `validation.file.size_too_large`, root `gdpr.*`, `order.review.already_exists`,
  `admin_user.cannot_target_admin_via_gdpr_tool` (the `errors.*`-outside-`api.*` landmine); partner
  `pages.payslips` (56), `pages.tax_benefits` (25), `pages.disputes` (20), `pages.dispute_details`
  (28), `enums.dispute_status/reason`, `enums.payment_type`, `pages.login`, `pages.register`,
  `sidebar.payroll/disputes`, `help.hide_help`, `global.status.*`, `global.time.*`, `common.*`
  (partner), `common.validation.*`, `validation.common.invalid_quantity`,
  `validation.file.size_too_large`, `registration_lock.{missing_requirements,profile_completion,document_upload}`,
  root `gdpr.*`, `order.review.already_exists`. The "needs a hand check" sections (admin
  `pages.template_management`, `pages.company_lifecycle`, …) are **not** here — F11 in T-0798
  reports them and a later sweep decides.
- **SCSS orphans (DC §3.7, CS §4.2):** `libs/shared/assets/src/shared-styles.scss`,
  `styles/index.scss`, the three empty lib stubs (`country-form.component.scss`,
  `language-form.component.scss`, `language-management.component.scss`),
  `pages/cleansia-partner/disputes.component.scss` (340) + `dispute-details.component.scss` (409) +
  `pages/cleansia-partner/index.scss:101-102`, the `.cleansia-invoice-template-form` /
  `.cleansia-receipt-template-form` blocks in `pages/cleansia-admin/template-form.component.scss`
  (+ `index.scss:88` if the file empties); the 12+ dead `.p-datatable` override blocks under
  `&__table` in every admin list stylesheet that has no `*__table` element
  (`company-management.component.scss:30-80`, `order-management:30-80`, `employee-management:30-82`,
  … — only `reports.component.html` uses the class; its one live copy moves to
  `components/cleansia-table.component.scss` if wanted); `pages/cleansia-partner/orders.component.scss:533,560`
  (`status-pending`, the dead status); `styles/README.md` rewritten to what is true.
- **Free-trial UI remnants (T-0690 left them; the member leaves the wire in T-0791):**
  `membership-plan-list.models.ts:111-114` (`Zkušební dny` column),
  `membership-plan-form.component.html:185-197` + `.component.ts:112,201,213,267` +
  `.facade.ts:37,114,150` + specs (`.facade.spec.ts:43,57,67`, `.component.spec.ts:208`), the
  `pages.membership_plans.description` subtitle ("zkušební období"), `columns.trial_days`,
  `form.field.trial_days`, `form.validation.trial_negative` in five locales.
- **Stale copy (DC §6.3):** partner `help.orders.status.confirmed_desc` (*"Payment received and order
  is confirmed"*), `pending_desc`, `help.orders.payment.*` describe the pre-ADR-0057 `Confirmed` —
  rewritten to the `/domain/order-lifecycle` wording in five locales (content from the docs page,
  not invented).
- Hand-check `pages/cleansia-partner/profile.component.scss:60` (*"Legacy grid rows (used by other
  sections if needed)"*) — whether the selectors under it match anything after T-0791.

## NOT

- `cleansia-multiselect` (adopted by T-0789); `cleansia-file` / `cleansia-radio` /
  `cleansia-help-card` / `cleansia-label` (customer or one-app users, shared by design).
- `partner-auth.service.ts:105 authenticateWithGoogle` (Q-UI-12: keep).
- The `code` / `loading` NgRx slice consolidation, the photo-gallery and work-contract-dialog
  unification, merging the three page-local money helpers (DC §3.9, CS §4.3 — structural moves with
  no defect behind them).
- The `TODO(W6.2)` `any` typing on `cleansia-radio` / `cleansia-select` (DC §3.8).
- The hand-check i18n sections (T-0798 F11 reports them).
- The customer app's locale files beyond the `api.*` twins T-0792 removes.

## Done looks like

`npx nx affected -t lint,test,build` green for admin and partner;
`rg "cleansia-cleansia-menu|paymentStatusLabel|paymentStatusSeverity|\| replace" libs apps` = 0;
`rg "Stroytorg" libs` = 0; `rg "trialPeriodDays|trial_days" libs/cleansia-admin-features
apps/cleansia-admin.app` = 0; partner login issues no `GET /User/GetCurrent` (network tab); the admin
membership-plan list capture has no `Zkušební dny` column and the form no `Zkušební období (dny)`
field; `styles/README.md` names only files that exist.

## Acceptance criteria

- [ ] **AC1** — Given the shared component barrel, When `rg "cleansia-cleansia-menu|paymentStatusLabel|paymentStatusSeverity|\| replace" libs apps`
      runs, Then it returns nothing and both apps build.
- [ ] **AC2** — Given a partner login, When the session is set, Then the network tab shows no
      `GET /User/GetCurrent` and the dashboard still renders (the user slice is gone from
      `store.config.ts` on both apps).
- [ ] **AC3** — Given the admin membership-plan list and form at 1440, When re-captured, Then no
      `Zkušební dny` column and no `Zkušební období (dny)` field render, and
      `rg "trialPeriodDays|trial_days" libs/cleansia-admin-features apps/cleansia-admin.app` = 0.
- [ ] **AC4** — Given the five partner locales, When the help panel's order-status copy renders, Then
      `confirmed_desc` says a cleaner took the job (the `/domain/order-lifecycle` wording) and no
      sentence ties `Confirmed` to payment.
- [ ] **AC5** — Given the discovery's `unused-keys.mjs` re-run after the i18n deletions, When its
      output is attached to this ticket's status log, Then every deleted section is absent from all
      five locales of the app that carried it and no remaining key it lists as unused is in a
      template (a hand-check section is reported, not deleted).
- [ ] **AC6** — Given `libs/shared/assets/src/styles/README.md`, When each file it names is looked
      up, Then every one exists and every orphan listed under DC §3.7 is gone from the tree and from
      every `@use`.
- [ ] **AC7** — Given `tsconfig.base.json`, When `@cleansia.app/template-management` is removed and
      `@cleansia.app/pay-periods` is renamed, Then `app.routes.ts:36` imports the new alias and the
      admin app builds.

## Implementation notes

**Regen:** none (no DTO or endpoint changes here — the free-trial member leaves the wire in T-0791,
so this ticket edits against the already-regenerated client). Delete the `.p-datatable` blocks by
checking each stylesheet's template for a `*__table` element first; the one live copy is
`reports.component.html`. `styles/README.md` is rewritten here to what is true and completed by
T-0799. **Guard:** F11 (i18n namespaces + no `common.*`) and F12 (orphan stylesheet) in T-0798.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Opened the same night as phase 1's third lane, after T-0791's commit, beside T-0792.
- 2026-09-21 — shipped as 7da4c1665; the five-locale section removals for admin and partner rode
  along in the backend lane's f307e835 and cf4b1f98. Beyond the Doing line list, the same sweep also
  removed (every one at zero readers, `rg` across `src/Cleansia.App`): `BaseSortDefinition`
  `create`/`init`/`toggle`/`select`/`update`; `BaseFilter.resetFilter`/`equals`; `Page.createWith*`/
  `createDefaultWithSpecifiedSort`/`updateSort`; `FormState` beside `isFormStateEqual`;
  `StateAdapter.create`/`setState`; and in `template-form.component.scss` the
  `.template-info-badges`/`.variables-*`/`.variable-item` blocks plus the three `lib-*-template-form`
  host rules (all read only by the deleted `email-template-form`). Review fix commit retargets the
  three `patterns-frontend.md` citations the deletions left dangling (`check-catalog-claims` C3 2 → 0),
  the `docs/admin-app/overview.md` pay-periods alias, and the `styles/README.md` shipping rule (the
  two `common/` mixin partials are `@use`'d directly).
