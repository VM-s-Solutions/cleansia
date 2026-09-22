---
id: T-0787
title: Admin list pages — the 28 lists on one pattern (header, toolbar order, right-aligned numbers, width budgets, one row-action style, `h1`), plus the page-local defects the captures show
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-22
depends_on: [T-0785, T-0786, T-0794]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"polish alignments in admin app a bit on all of the overview/detail
pages"*. With the shell (T-0785), the primitives (T-0786) and the drawer (T-0794) shared, what is
left on the 28 admin lists is per-page: the toolbar as its own row, `Vytvořit` left of `Filtry` on one
page, `align` used 0× so every rating / count / price is left-aligned, three column budgets over 90 %
that clip the action column, per-page row-action colour overrides, and the page-specific defects
the 1440 captures show (a 300 px empty card on Countries, a one-tab tab strip on Templates, `Filtry`
80 px above the tab strip on Reports, two raw `<table>`s and raw inputs on Service area, a raw
`Date.toString()` on Deletion requests, …). Canonical pattern: VF 6.3 — header → optional info
banner *after* the header → optional tab strip, left, `gap: 0.5rem` → `cleansia-section` holding
`cleansia-table` → paginator; numbers, money, dates, ratings `align: 'right'`; badges and actions
centred; rows-per-page 20. Reference capture `admin-package-management-1440.png` with the 6.2 header.

**Pages (28):** employees, orders, pay periods, disputes, invoices, services, packages, extras,
admins, languages, countries, currencies, legal documents, company info, company settings, global
rates, templates, audit log, reports, notifications, company lifecycle — and the nine R1 outliers:
service area, employee documents, deletion requests, fiscal failures, promo codes, tiers, referrals,
membership plans, data protection. Paths are relative to `src/Cleansia.App/`.

## Doing — shared across the 19 canonical lists

- Toolbar into the header's right column (`filter-header` `pages/cleansia-admin/filter-drawer.scss:353-359`
  stops being a row of its own); active chips on a second row, left. Fixed **button order: `Filtry`
  (secondary) then `Vytvořit` (primary)** — pay periods flips it (`pay-period-management.component.html`).
- `align: 'right'` on every numeric column: `Hodnocení`, `Stížnosti`, `Celkem`, `Přiřazeno`, `Vratka`,
  `Celková částka`, `Objednávky`, `Délka trvání`, `Pořadí`, `Sleva`, `Limit`, every `Cena` (all admin
  `*.models.ts`).
- Column-width budgets ≤ 90 % with an actions column: `invoice-management.models.ts:42-96` (89 % +
  actions), `company-settings.models.ts:74-120` (100 %), `membership-plan-list.models.ts:60-139`
  (100 %); `Klíč` at 20 % on company settings forces 7-line wraps (`company-settings.models.ts:74`).
- Delete the per-page row-action colour overrides (`employee-management.component.scss:60-110`
  hard-coded `#3b82f6` / `#10b981` / `#ef4444`, and the same block on services / packages / extras).
- `[level]="1"` on the page `cleansia-title` (CS §9: 0 of 30 admin pages have an `h1`).

## Doing — page-specific (capture · defect · fix site)

| Page | Capture | Defect | Fix site |
|---|---|---|---|
| Disputes | `admin-dispute-management-1440.png` | no toolbar row; third badge style; `Vratka 1250.00` no currency | `disputes-management.component.html:1-40`, `.scss:56-75` (delete), the refund cell via T-0786 |
| Pay periods | `admin-pay-periods-1440.png` | status plain text; `Vytvořit` left of `Filtry` | `pay-period-management.component.html:256-262`, `.scss:225-260` |
| Countries | `admin-country-management-1440.png` | no paginator/filter, 300 px empty card; `Výchozí trh` blank when false | `country-management.component.html`, `country-management.models.ts` (render `—`) |
| Company settings | `admin-company-settings-1440.png` | subtitle wraps two centred lines; `Klíč` 20 % | `company-settings.component.scss:20-28`, `company-settings.models.ts:74` |
| Global rates | `admin-pay-config-management-1440.png` | info banner between subtitle and toolbar; `Popis` truncates | `pay-config-management.component.html:14-17` (`__info-banner` under the header) |
| Templates | `admin-template-management__email-templates-1440.png` | one-tab tab strip; table is a card inside the card | `template-management.component.html:14-18` (no strip while `tabs.length === 1`), `pages/cleansia-admin/template-management.component.scss:36-60` |
| Audit log | `admin-audit-log-1440.png` | tab icons glued to labels; tabs centred here, left on Reports | `audit-log-segment.component.html:4-8`, `audit-log.component.scss:163-167` |
| Reports | `admin-reports-1440.png` | `Filtry` 80 px above the tab strip; KPI baselines differ; 560 px paragraph under a centred header | `reports.component.html:11-31`, `reports.component.scss:109-200` |
| Notifications | `admin-notifications-1440.png` | `max-width: 1100px` inset; its header is the reference | `notifications.component.scss:14` (T-0785 width class) |
| Company lifecycle | `admin-company-lifecycle-1440.png` | four filled colours in one action row; ragged bullets; `Obnovit` floating | `company-lifecycle.component.scss:120-132`, `company-lifecycle.models.ts:216-240` (outlined, one primary) |
| Service area | `admin-service-area-management-1440.png` | flush card; plain `<table>` ×2; raw `<button>` ×2, `<input>` ×2, `p-select`, `p-toggleSwitch` ×2 | `service-area-management.component.html:20-27,41,57,79,102,128,137,174,185,198` → `cleansia-table` + `cleansia-*` |
| Employee documents | `admin-employee-documents-1440.png` | no card; heading clipped; dropdown to x=1440; `Země` h2 dark | `document-requirements.component.html:1-8` (T-0785 classes; `cleansia-section`) |
| Deletion requests | `admin-employee-documents__deletion-requests-1440.png` | raw `Date.toString()`; plain-text status | `deletion-requests.component.ts:111-114` (T-0786 helper + badge) |
| Fiscal failures | `admin-fiscal-failures-1440.png` | subtitle in body colour; seconds in the timestamp | `fiscal-failures-list.models.ts:52,94` (T-0786 `dateTime`) |
| Promo codes | `admin-loyalty__promos-1440.png` | `Nový kód` under the title; two full-width stacked filters; status plain | `promo-codes-list.component.html` (header actions; T-0794 drawer or one inline row) |
| Tiers | `admin-loyalty__tiers-1440.png` | outlined button glued to the table; paginator 10 | `tier-configs.component.html` |
| Referrals | `admin-loyalty__referrals-1440.png` | status select full width; `Od` / `Do` stacked, icon outside | `referrals-list.component.html` (one filter row; T-0785 calendar) |
| Membership plans | `admin-membership-plan-management-1440.png` | `Nový plán` full-width bar; table clipped | header actions; widths (above); the `Zkušební dny` column is T-0793's |
| Data protection | `admin-data-protection-1440.png` | three full-width stacked buttons, 0 gap, red `Vymazat účet` bar; `Stav` select full width | `data-protection.component.html` (one action row, outlined, `auto-width`; danger = red outline) |

The raw buttons T-0794 lists for this ticket (`service-area-management:128,137`,
`pay-period-management:216`, `email-type-detail:54`) → `cleansia-button`.

## NOT

- The filter-drawer extraction (T-0794 — it lands *before* this ticket), the badge / date / money
  helpers (T-0786), the shell rule (T-0785), the audit-log entry pages (detail — T-0788), any new
  column or filter that does not exist today.

## Done looks like

Each of the 28 routes re-captured at 1440 shows: title top-left with the subtitle under it,
`Filtry` then `Vytvořit` on the title row, the table's right border visible with the action icon
inside it, numeric columns right-aligned; the disputes and countries pages start their table at the
same y as the employee list.

## Acceptance criteria

- [ ] **AC1** — Given the 28 list routes re-captured at 1440, When each is compared to
      `admin-package-management-1440.png`, Then the title is top-left, the subtitle under it,
      `Filtry` then `Vytvořit` on the title row, and the first table row starts at the same y on
      disputes, countries and employees.
- [ ] **AC2** — Given every admin `TableColumn[]`, When a jest snapshot asserts `align: 'right'` on
      every column whose `id` matches `/price|amount|total|count|rating|limit|discount|order|duration/`,
      Then it is green.
- [ ] **AC3** — Given the invoice, company-settings and membership-plan lists, When rendered, Then
      the column budget with the actions column is ≤ 100 % and the action icon renders inside the
      table's right border.
- [ ] **AC4** — Given the service-area page, When rendered, Then it holds no raw `<table>`, `<button>`,
      `<input>`, `p-select` or `p-toggleSwitch` (`cleansia-*` wrappers only) and sits in the card.
- [ ] **AC5** — Given every list page, When the DOM is read, Then exactly one `h1` renders (the
      `cleansia-title` with `[level]="1"`).
- [ ] **AC6** — Given the pay-period, promo-code and deletion-request lists, When a status renders,
      Then it is a `cleansia-status-badge` pill with a translated label.
- [ ] **AC7** — Given `page-shell.spec.ts` extended (or F10 in T-0798), When it runs, Then no
      `pages/cleansia-admin/*-management.component.scss` carries a `.p-button` colour override.

## Implementation notes

Depends on T-0785, T-0786, T-0794. **Regen:** none. The Notifications header is the reference and
keeps its shape; the Company-lifecycle page's date format is already right (T-0786 makes it the rule).

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 3, admin web lane, first of four (T-0787 → T-0788 → T-0789 → T-0790).
- 2026-09-22 — **done**; shipped 2026-09-21 as `bd00ff694` + the review fix `bb4a7b74f`, and the
  second-round fix `ea70c8c7e` on 2026-09-22, on chore/ui-polish-and-dead-code (PR #260). The 28
  lists on the one shape (the shared header with an `h1`, the drawer before create, figures and stamps
  right-aligned through `numeric`, badges centred, the shared error and empty states, a page
  stylesheet that carries only what is page-specific); the reports KPI cards subgrid their rows;
  `pages/cleansia-admin/tab-strip.scss` draws the tab icon gap once (in the admin pages folder, not
  `common/` — the shared tree was frozen; a follow-up); `list-filter.scss` bounds a lone select
  above a list; marker columns read `Ano` / `—` (a boolean fact keeps `Ano` / `Ne`; the currencies'
  `isActive` keeps its deliberate `Ano` / `Zatím ne`); the promo minimum order prints as money.
  Guard: admin `theme/list-pages.spec.ts` with the numeric-column guard. Findings reported, not
  absorbed, on the plate: the disputes `Vratka` currency (backend DTO), the badge kinds the shared
  catalogue lacks (audit outcome, fiscal error, GDPR request — page-local tone maps for now), the
  table's nowrap headers on wide tables, the three page-local percent formatters, the membership
  plans' `formatPlanPrice`.
