---
id: T-0786
title: Shared primitives — `cleansia-status-badge`, `formatDate`, every money figure through `formatMoney`, table alignment honoured, the paginator hidden on zero rows, one row-action style
status: todo
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785]
blocks: [T-0787, T-0788, T-0797]
stories: []
adrs: [ADR-0057]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Root causes R3, R4, R5 of VF §1 and the table defects of VF 6.3: twelve hand-rolled status badges
with eleven page-local colour ramps (two `.contract-status-badge` rules collide across
`employee-management` and `employee-detail`), dates rendered five ways (`toLocaleDateString('en-GB')`
19×, `toLocaleString('en-GB')` 14×, bare `toLocaleDateString()` 10×, `'dd.MM.yyyy HH:mm'` 4×,
`'dd/MM/yyyy'` 1×, and one raw `Date.toString()` on the deletion-requests page), money bypassing
`formatMoney` at six sites (`1250.00 Kč`, `CZK 1,250.00` beside Reports' correct `1 250,00 Kč`),
`TableColumn.align` used 0× in admin, a paginator that reads `1 – 0 z 0` with an active "next"
arrow (`cleansia-table.component.ts:138-142`), and an actions column clipped on three lists. Fixed
once, here; the page tickets then only adopt.

Paths are relative to `src/Cleansia.App/`.

## Doing

- **`cleansia-status-badge` (R3 / CS §6a).** One component
  `<cleansia-status-badge [kind]="'order'|'payment'|'contract'|'invoice'|'dispute'|'payPeriod'|'company'|'referral'|'plan'|'document'" [value]="…">`
  rendering `.status-badge.status-badge--{neutral|info|success|warning|danger}` with a per-kind map
  to the five tones and the translated label (`enums.*` keys exist per kind). Style: promote
  `common/status-badge.scss:10-25` `%status-badge-base` to the real class — sentence-case, radius
  16, min-width 80, **no hover lift** (`:25-28` lifts a non-interactive element); the
  `employee-detail.component.scss:182-187` look is the one to keep. Replace the 12 hand-rolled
  `#statusTemplate` + `getXStatusClass()` + `.x-status-badge` triples
  (`employee-management.component.html:184`, `order-management.component.html:212,220`,
  `disputes-management.component.html:41`, `invoice-management.component.html:149,157`,
  `pay-period-management.component.html:256`, `company-info-list.component.html:138`,
  `admin-user-management.component.html:138`, `referrals-list.component.html:61`,
  `membership-plan-list.component.html:60`, `company-lifecycle.component.html:117`,
  `order-detail.component.html:53-61`); delete the 11 page-local ramps (CS §6.1, incl. the
  `.contract-status-badge` collision). Also the three pages with **no** badge today (pay periods
  `pay-period-management.component.html:256-262`, promos, deletion requests), the raw enum on the
  employee summary (`employee-detail.component.html:86` `{{ employee.contractStatus }}` —
  `Approved` untranslated) and the untranslated `PAID` on the order banner
  (`order-detail.component.html` payment template). The order kind renders `New` as a pill — the
  resting state of every paid order after ADR-0057.
- **Dates (R4).** One `formatDate(value, lang, 'date' | 'dateTime')` in `libs/shared/utils`
  wrapping `toLocaleString(localeFor(lang), { dateStyle: 'medium', timeStyle: 'short' })` — the call
  `company-lifecycle.facade.ts:268` already makes. Replace the five shapes above
  (`order-management.models.ts:53-55`, `employee-management.models.ts:86`,
  `invoice-management.models.ts:103`, `fiscal-failures-list.models.ts:52,94`, partner
  `order-details.helpers.ts:12-17`, …) and the raw `Date` interpolation in
  `employee-document-config/src/lib/deletion-requests/deletion-requests.component.ts:111-114`.
- **Money (R5).** Route the six bypasses through `formatMoney(value, code, localeFor(lang),
  { fractionDigits: 2 })` (`libs/shared/utils/src/money-formatters.utils.ts:44-53,63`):
  `order-management.models.ts:70` (`toFixed(2)`), `service-management.facade.ts:111-113`,
  `package-management.facade.ts:111-113`, `extra-management.facade.ts:111-113` (all pin `'en-GB'`),
  `pay-config-management.facade.ts:91-97` (own `Intl.NumberFormat('en-GB')`), partner
  `orders/src/lib/order-details/order-details.helpers.ts:12-17`; add the missing currency to the
  dispute refund cells (`disputes-management.models.ts`, `dispute-detail.component.html` —
  `Vratka 1250.00`). The reference output is what Reports renders (`1 250,00 Kč`).
- **Table (VF 6.3).** `cleansia-table`: `align` honoured for header *and* cell (verify —
  `TableColumn.align` at `cleansia-table.models.ts:9` is used 15× and only in partner); a
  `numeric: true` column shorthand = `align: 'right'` + tabular figures; default `rows` 20,
  `rowsPerPageOptions` `[5, 10, 20, 50]` (drop the `1`, `cleansia-table.component.ts:50-51,68,78-79`);
  the paginator hidden when `totalRecords() === 0` and the "next" guard
  (`cleansia-table.component.html:159`) compares to `Math.max(totalPages(), 1)`; the empty state
  renders `cleansia-not-found-state` (`common/not-found-state.scss` exists) instead of the bare
  sentence (`cleansia-table.component.html:45-53`); one row-action style — the grey circle the table
  already draws (`cleansia-table.component.scss:317-340`) — and the actions column counted in the
  width budget so percent widths ≤ 90 % never clip (`:131` nowrap, `:317-318` 120 px).
- **Guards:** `cleansia-status-badge.component.spec.ts` (every `kind` × every enum value maps to one
  of five tones and a translated label — a missing map entry fails);
  `cleansia-table.component.spec.ts` case *"0 records: paginator hidden"*; `formatDate.spec.ts` for
  `cs` / `en` / `uk` outputs.

## NOT

- The partner `my-pay` ledger (already right; it is the reference).
- Merging `formatPayAmount` / `formatPlanPrice` / `roundToCents` into the shared util (DC §3.9 —
  consolidation, not a defect).
- The rows-per-page `inputId` collision on two-table pages (T-0794 owns the table scaffold a11y).
- Any page's column list (T-0787) or the partner adoption (T-0797).

## Done looks like

`rg "toLocale(Date)?String\('en-GB'\)" libs apps/cleansia-admin.app apps/cleansia-partner.app` = 0;
`rg "getValue.*toFixed\(2\)" libs` = 0; the admin employee list re-captured shows `Schváleno` in the
same pill as the detail's `Stav smlouvy`; `1 – 0 z 0` never appears with an active arrow; the three
guard specs green.

## Acceptance criteria

- [ ] **AC1** — Given every `kind` of `cleansia-status-badge`, When the spec enumerates each enum
      value of that kind, Then each maps to exactly one of the five tones and to a key present in
      all five locales; an unmapped value fails the spec.
- [ ] **AC2** — Given a Czech session, When the admin employee list, the employee detail, the order
      detail's payment banner and the pay-period list render a status, Then each is a pill of the
      same family reading a translated label (`Schváleno`, `Zaplaceno`, never `Approved` or `PAID`).
- [ ] **AC3** — Given a date on any admin or partner list or detail, When rendered in `cs`, Then it
      reads `21. 9. 2026` (or `21. 9. 2026 11:00` for `dateTime`) and the deletion-requests page
      shows no `GMT+0200` string.
- [ ] **AC4** — Given a money figure on the order list, the service / package / extra lists, the
      global-rates page, the dispute refund cells and the partner order detail, When rendered in
      `cs`, Then it reads `1 250,00 Kč` with its currency.
- [ ] **AC5** — Given a `cleansia-table` bound to zero records, When rendered, Then the paginator is
      hidden and the not-found state shows; given one page of records, the "next" arrow is disabled.
- [ ] **AC6** — Given a column with `numeric: true` or `align: 'right'`, When rendered, Then both the
      header and the cells are right-aligned with tabular figures.
- [ ] **AC7** — Given the invoice, company-settings and membership-plan lists, When re-captured at
      1440, Then the table's right border is visible and the action icon sits inside it.

## Implementation notes

Depends on T-0785 for the five tone tokens. **Regen:** none. Reference captures:
`admin-employee-management-1440.png`, `admin-admin-user-management-1440.png` (the `1 – 0 z 0`),
`admin-invoice-management-1440.png`, `crops/emp-detail-top.png`, `crops/order-detail-1.png`,
`partner-my-pay-1440.png` (the ledger — the money reference).

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 2, web-shared lane, second of the serial four.
