---
id: T-0788
title: Admin detail pages — the 11 details on one pattern (breadcrumb above the title, audit link on the title row's right, `[section-actions]`, `.detail-grid`, one outlined `Akce` row, no green/orange/blue fills), plus the page-local defects the captures show
status: done
size: L
owner: —
created: 2026-09-20
updated: 2026-09-22
depends_on: [T-0785, T-0786, T-0791, T-0796, T-0797]
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
pages"*. The 11 admin details show three header shapes (back/title/audit left-clustered on
employee, two secondaries clustered on order, a full-width `Zpět` bar then a *centred* title on the
audit entries), three label/value grids on one page (3 / 2 / 4 columns, colons inconsistent),
five `Upravit` buttons floating 40 px under their section rule, four stacked full-width action bars
with 0 gap on the order page, orange / green / blue fills (`Zobrazit úplné údaje`, `Schválit`), 41
inline `style=""` on the employee detail, English rate strings (`1 + 1/room + 1/bath CZK`), and two
details that render nothing for a missing entity. Canonical pattern: VF 6.4 — back control **above**
the title as a breadcrumb (**Q-UI-08 default: breadcrumb**, built as `cleansia-breadcrumb` by T-0797
and adopted here) → header: entity title left, `Zobrazit historii auditu` + other secondaries right
(`dispute-detail.component.scss:20-30` is the rule) → identity strip as one tinted card →
`cleansia-section`s with `[section-actions]` → `.detail-grid` (T-0785) → one `Akce` section:
left-aligned wrapped row of *outlined* `auto-width` buttons, `gap: 0.75rem`, at most one filled
primary, danger = red outline, **no `success` / `warn` / `info` fills**. Reference captures:
`admin-dispute-management__disputeId-1440.png` (header), `admin-order-management__orderId-1440.png`
(sections), `partner-my-pay-1440.png` (ledger).

**Pages (11):** employee, order, pay period, dispute, invoice, promo, customer (`/customers/:userId`),
audit entry / customer audit entry / resource history, email template (`…/translations`), company
lifecycle (its detail half). Paths are relative to `src/Cleansia.App/`.

## Doing (capture · defects · fix site)

| Page | Capture | Defects | Fix site |
|---|---|---|---|
| Employee | `admin-employee-management__employeeId-1440.png`; `crops/emp-detail-{top,mid,bottom}.png` | (a) back/title/audit left-clustered `gap: 2rem`; (b) `Nastavit týdenní limit` full-width; (c) `Týdenní limit zakázek:20`, `Služby:1 / 1` no space after colon, `Stav smlouvy` raw `Approved`; (d) five `Upravit` floating under the rule; (e) `Použít šablonu hodnosti` block indented 14 px, checkbox above its label, `Použít na vše` orphaned; (f) rate strings in English; (g) orange `Zobrazit úplné údaje`, green `Schválit`; (h) 350 px pending-document tile; **41 inline `style=""`** | (a) `employee-detail.component.scss:13-18` → copy `dispute-detail:20-30`; (b) `employee-detail.component.html:153-161`; (c) `:114-121`, `:86`; (d) `:245,355,462,553,605` → `[section-actions]`, save/cancel rows `:231,341,448,539,590`; (e) `:660-700`, checkbox `:691-695` (`p-checkbox` → `cleansia-checkbox`, baseline-aligned); (f) `:727` → a translated template `pages.employee_detail.rate_format` with `{{ base }}`, `{{ room }}`, `{{ bath }}`; (g) `employee-payout-section.component.html:64`, `employee-detail.component.html:170`, `employee-documents-section.component.html:120`; (h) `employee-detail.component.scss:294`; the 41 `style=` → classes |
| Order | `admin-order-management__orderId-1440.png`; `crops/order-detail-1..4.png` | (a) header left-clustered; (b) `NOVÁ` translated, `PAID` not; (c) three grids on one page; (d) `Celková cena` a tinted tile inside a plain grid; (e) four stacked full-width outlined bars, 0 gap; (f) timeline connector above the first node; (g) `1250.00 Kč` | (a) `order-detail.component.scss:20-29`; (b) T-0786 badge; (c) `order-detail.component.scss:100-103` + `order-detail.component.html:84-88` vs `:125-132` → `.detail-grid`; (d) `order-detail.component.scss:135` (`&.total-price` → a ledger row); (e) `order-detail/components/admin-order-ops.component.html:2-30` (`order-ops__actions` has **no rule anywhere** → the `Akce` row); (f) `order-detail.component.scss:250-266` (`top: 1rem`); (g) T-0786. The crew acceptance line T-0784 added is restyled only. |
| Pay period | `admin-pay-periods__id-1440.png` | UPPERCASE labels; status line indented 23 px; 3-col grid | `pay-period-detail.component.scss:98-106` (delete `text-transform`), `:16-21` header |
| Dispute | `admin-dispute-management__disputeId-1440.png` | headings without the rule; three action alignments; disabled buttons pastel-filled; `Vratka 1250` no currency | `dispute-detail.component.scss:50` (`.section-title` → `cleansia-section`), the action rows in `dispute-detail.component.html`; keep `:20-30` |
| Invoice | `admin-invoice-management__invoiceId-1440.png` | two action sections, seven buttons, three colours; status strip `space-between` on three unrelated facts; `Finanční přehled` indented 15 px, `Poznámky administrátora` 4 px; seven numeric columns left-aligned | `invoice-detail.component.html:20-60`, `invoice-detail/components/admin-payroll-ops.component.html`, `invoice-detail.component.scss:10-19,60-110`, `invoice-detail.models.ts` (`align`) |
| Promo | `admin-loyalty__promos__id-1440.png` | full-width `Zpět` above the title; label/value run together (`Typ Procentuální`) | `loyalty-promo-codes/src/lib/promo-code-detail/promo-code-detail.component.html` (no stylesheet → `.detail-grid`) |
| Customer | `admin-customers__userId-1440.png`; `crops/customer-1.png`, `-2.png` | seven full-width bars incl. two green fills; `Objednávka` floating label overprints its placeholder; tier metrics as prose; black glyph star; `Zůstatek 1250 CZK` loose between bars | `user-loyalty-detail.component.html:4-30,55-61,100-130` (no stylesheet → `.detail-grid` + `Akce` row); the select overlap `cleansia-select.component.scss:7-27` (float-label when both `label` and `placeholder` are set — a shared fix, landing here because this is its only visible site) |
| Audit entry / customer entry / resource history | `admin-audit-log__entry__auditId-1440.png` | full-width `Zpět` bar then a *centred* title | `audit-log/src/lib/audit-entry/audit-entry.component.html:3-8`, `audit-log.component.scss`; the diff `<table>`s at `audit-entry.component.html:62`, `customer-audit-entry:130,157` stay (not data grids) |
| Email template | `admin-template-management__email-templates__emailType__translations-1440.png` | `Přidat překlad` orphaned right with a resting focus ring; row `Uložit` 44 px beside a 40 px delete circle; footer two secondaries, no primary | `email-type-detail.component.html:27-31,54` (raw `<button>`), `email-type-detail.component.scss:20-25`; tab label = language *name* (`email-type-detail.component.ts:99`) |
| Company lifecycle | `admin-company-lifecycle-1440.png` | dates `21. 9. 2026` here vs `21/09/2026` elsewhere — this page is *right* | — (T-0786 makes it the rule) |
| Not-found | (not captured — no fixture) | order / employee detail render nothing when the entity is missing (`order-detail.component.html:38-40`, `employee-detail:24-26`; `dispute-detail:25-27` has the branch) | the `not-found-state` block (T-0796 owns the idiom; this ticket applies it to these two) |

## NOT

- Decomposing `employee-detail.component.html` (1 029 lines) into child components (CS §2.2) — this
  ticket restyles it. The availability section (gone in T-0791 before this starts). The `Použít
  šablonu hodnosti` business logic (layout only). The customer detail's dialogs' content (T-0790).
  The partner detail vocabulary (Q-UI-09: keep the partner's shape).

## Done looks like

Every detail capture shows the breadcrumb above the title, the audit button on the title row's
right edge, `Upravit` on the section title's row, four-column label/value grids with no trailing
colon, the actions in one outlined row, no green / orange / blue fill anywhere on the page;
`rg 'style="' libs/cleansia-admin-features/employee-management` = 0;
`rg 'severity="(success|warn|info)"' libs/cleansia-admin-features` = 0.

## Acceptance criteria

- [ ] **AC1** — Given the 11 detail routes re-captured at 1440, When each is compared to
      `admin-dispute-management__disputeId-1440.png`, Then the breadcrumb sits above the title, the
      title is left, and every secondary (audit history, incident file, …) is on the title row's
      right edge.
- [ ] **AC2** — Given `libs/cleansia-admin-features/employee-management`, When
      `rg 'style="'` runs, Then it returns nothing; a jest spec on `employee-detail.component.html`
      pins it.
- [ ] **AC3** — Given `libs/cleansia-admin-features`, When `rg 'severity="(success|warn|info)"'`
      runs, Then it returns nothing; every `Akce` row is outlined `auto-width` buttons with at most
      one filled primary and danger as a red outline.
- [ ] **AC4** — Given a Czech session on the employee detail, When the pay-config line renders, Then
      it reads a translated sentence from `pages.employee_detail.rate_format` (no `1 + 1/room +
      1/bath CZK`), the contract status is a badge, and `Týdenní limit zakázek` has a space after
      the label (no colon in the grid).
- [ ] **AC5** — Given the order detail, When rendered, Then one `.detail-grid` shape serves the
      three fact groups, `Celková cena` is a ledger row, the four operations sit in one wrapped
      outlined row with `gap: 0.75rem`, and the timeline connector starts at the first node.
- [ ] **AC6** — Given `/order-management/does-not-exist` and `/employee-management/does-not-exist`,
      When loaded, Then the not-found block renders (T-0796's idiom applied here).
- [ ] **AC7** — Given the customer detail's `Objednávka` select with both `label` and `placeholder`,
      When rendered, Then the floating label does not overprint the placeholder (the shared
      `cleansia-select` fix).
- [ ] **AC8** — Given the invoice detail's order table, When rendered, Then its seven numeric
      columns are right-aligned and the two action sections are one `Akce` row.

## Implementation notes

Depends on T-0785, T-0786, T-0791 (the employee detail shrinks first), T-0796 (the not-found
idiom), T-0797 (`cleansia-breadcrumb` with a real `routerLink`; if Q-UI-08 is overruled to "header
row", the two partner breadcrumbs become the header row and this ticket keeps the back button on the
title row instead). **Regen:** none. **Guard:** the F6 / F7-shaped rules in T-0798 (`style="` and
`severity="success|warn|info"` in feature templates).

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 3, admin web lane, second of four. Q-UI-08 default (breadcrumb) in force.
- 2026-09-22 — **done**; shipped as `9010033e3` + the fixes `d87842299`, `d56e48364`, `d7f33ca2c` on
  chore/ui-polish-and-dead-code (PR #260). The eleven details on the one shape:
  `pages/cleansia-admin/_detail-page.scss` (`.cleansia-detail-title` — the back control **beside**
  the `h1` with the audit link on the right, `.detail-identity`, `.detail-actions` + `__panel`,
  `.detail-ledger`), sections with their edit action in `[section-actions]`, `.detail-grid` with no
  trailing colon, one row of content-sized actions per entity, the inline `style=""` gone, the
  customer credit ledger and employee rates through `formatMoney`, the promo detail on the identity
  strip. Guard: admin `theme/detail-pages.spec.ts`. **Q-UI-08 as shipped: no breadcrumb** — T-0797
  built no `cleansia-breadcrumb` (shared tree frozen), so the title row carries the back control; the
  partial's own header records it. Findings reported, not absorbed, on the plate: every
  `cleansia-button` autofocuses (`[pAutoFocus]` undefined → PrimeNG sets the attribute; one line in
  the shared template), `.detail-ledger` duplicating the partner's `.amount-breakdown`, no `@else`
  on the promo, customer and email-type details, `entityType === 2` twice in `employee-detail`, the
  unused `pay_periods.detail.*` keys, the order detail's raw `Paid` / `New` cells, the pastel
  disabled primaries, the audit-entry identity on a bare grid.
