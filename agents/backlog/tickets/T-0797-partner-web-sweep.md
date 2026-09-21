---
id: T-0797
title: Partner web sweep onto the shared primitives — the `cleansia-button` API unified, partner dialogs on the admin shape, raw icon buttons wrapped, `Nová` in the same pill family as `Zaplaceno`, headers on the shell rule, `cleansia-breadcrumb` (Q-UI-08), `!important` 61 → ≤ 1
status: todo
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785, T-0786, T-0793]
blocks: [T-0788]
stories: []
adrs: [ADR-0057]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"make overall check on both apps to make them consistent"* — both = the
admin and partner web. VF §5, CS §3, §4.4, §5a: the partner binds `cleansia-button` through a legacy
API (`(clickFn)` 33×, `[title]`, `[buttonType]="'button'"` 26×, `[style]="'raised-button'"` 17×);
its five dialogs use `p-button` / `pButton`, raw `<textarea pTextarea [(ngModel)]>`, `p-inputNumber`
and `*ngIf`; eleven raw icon `<button>`s (one `title="Download"` untranslated); the order status
`NOVÁ` is unstyled text beside a `ZAPLACENO` pill (`orders.component.scss:558-600` has no
`status-new` — the resting state of every paid order after ADR-0057; `crops/p-order-1.png`); the
dashboard and profile centre their titles where the lists are left; `!important` 61× and `::ng-deep`
19× in `pages/cleansia-partner/*` (styles compile globally, so `::ng-deep` is a no-op and each
`!important` is a specificity fight inside one bundle — `order-details.component.scss` carries 36).

Paths are relative to `src/Cleansia.App/`.

## Doing

- **`cleansia-button` API:** `(clickFn)` → `(onClick)`, `[title]` → `[label]`, drop the redundant
  `[buttonType]="'button'"` and `[style]="'raised-button'"` — then delete the legacy `clickFn` /
  `title` from the component (`cleansia-button.component.ts:79,118`) once the three remaining
  binders move (`confirm-email.component.html:48,59`, customer `gdpr.component.html:18` — the one
  customer-app edit in this batch, two tokens, named here so it is not a surprise).
- **Partner dialogs to the admin shape:** `p-button` / `pButton` (`add-note-dialog:20,26`,
  `complete-order-dialog:102,110`, `mark-cash-collected-dialog:22,28`, `report-issue-dialog:20,26`,
  `date-range-selector:4,10,16,22`) → `cleansia-button`; raw `<textarea pTextarea [(ngModel)]>`
  (`add-note-dialog:10`, `complete-order-dialog:77`, `report-issue-dialog:10`) → `cleansia-textarea`
  + reactive form; `p-inputNumber` (`complete-order-dialog:56`) → `cleansia-text-input
  dataType="number"`; the five `*ngIf` in `complete-order-dialog` → `@if`.
- **Raw icon buttons** → `cleansia-button [text]` with a translated `aria-label`:
  `invoice-detail:5,73,78`, `order-header:29,34`, `order-photos:147,187,243,297`,
  `profile-documents:55,123,131,143`. Hidden file inputs stay.
- **Badges:** `cleansia-status-badge` (T-0786) on list, detail and dashboard; delete the six partner
  ramps (`dashboard`, `invoice-detail`, `invoices`, `order-details`, `orders`, `period-pay` .scss)
  and the help panels' sixth badge style.
- **Header:** partner dashboard (`dashboard.component.scss:27-32`) and profile on the T-0785 header
  rule (title left); the `my-pay` shell gets the outer gutter (flush outlier).
- **`cleansia-breadcrumb`** (Q-UI-08 default: breadcrumb) with a real `routerLink`, replacing the two
  hand-rolled partner breadcrumbs (`order-details.component.html:3-26`,
  `invoice-detail.component.html:3-24`); T-0788 adopts it on the admin details.
- `orders.component.html:260` `<hr class="section-divider">` with no rule (renders as a stray `·`)
  → the section gap.
- **Dashboard:** stat icons in four fills (`dashboard.facade.ts:131,138,145,157`) and gradient
  quick-action squares (`dashboard.component.scss:347-356`) → one tint; `1,250 CZK` next to
  `1 250 CZK` (T-0786 helper); chart ticks `2 hrs, 2 hrs, 1 hrs …`
  (`time-analytics-chart.component.html:18,31,98`, `.ts:56,66`) → integer ticks + translated unit.
- **Profile:** the pending-document tile wraps one letter per line, the title truncates at
  `Ukázkov…`, `1 Bytes` (`crops/p-profile-1.png`, `-2.png`) → min-width + a byte formatter that
  pluralises.
- **Overrides:** `!important` 61 → ≤ 1, `::ng-deep` 19 → 0 in `pages/cleansia-partner/*`.
- **Undefined-token fallbacks** (T-0785 defines them): remove the now-redundant fallbacks at
  `order-details.component.scss:216-218,1252-1253`, `profile.component.scss:354-365,429-430,583-586,622-623`.
- `[level]="1"` on the page titles (5/11 today).

## NOT

- The partner detail's read-only-input vocabulary and per-section cards (**Q-UI-09: keep the
  partner's shape**). Row-click behaviour (partner rows are jobs — leave). The per-section save on
  the profile (follows the endpoint shape — leave). The partner ↔ mobile feature-parity gap
  (**Q-UI-11: no change**). `authenticateWithGoogle` (**Q-UI-12: keep**).

## Done looks like

`rg "\(clickFn\)|\[title\]=" libs apps` = 0 and the two legacy members are gone from the component;
`rg "p-button|pButton|pTextarea|p-inputNumber" libs/cleansia-partner-features` = 0;
`rg -c "!important" pages/cleansia-partner/*.scss` ≤ 1; the partner orders capture shows `Nová` in
the same pill family as `Zaplaceno`; the dashboard tick labels are distinct integers.

## Acceptance criteria

- [ ] **AC1** — Given `libs` and `apps`, When `rg "\(clickFn\)|\[title\]="` runs, Then it returns
      nothing and `cleansia-button.component.ts` has no `clickFn` / `title` member; every app builds.
- [ ] **AC2** — Given `libs/cleansia-partner-features`, When
      `rg "p-button|pButton|pTextarea|p-inputNumber|\*ngIf"` runs, Then it returns nothing and the
      five dialogs' footers match the admin shape.
- [ ] **AC3** — Given a paid `New` order on the partner list, detail and dashboard, When rendered in
      `cs`, Then `Nová` is a `cleansia-status-badge` of the same family as `Zaplaceno`.
- [ ] **AC4** — Given the eleven former raw icon buttons, When rendered, Then each is a
      `cleansia-button [text]` with a translated `aria-label`; `rg 'title="Download"' libs` = 0.
- [ ] **AC5** — Given `pages/cleansia-partner/*.scss`, When `rg -c "!important"` and
      `rg -c "::ng-deep"` run, Then the totals are ≤ 1 and 0.
- [ ] **AC6** — Given the partner order detail and invoice detail, When rendered, Then a
      `cleansia-breadcrumb` with a working `routerLink` sits above the title.
- [ ] **AC7** — Given the dashboard's time-analytics chart, When rendered, Then the tick labels are
      distinct integers with a translated unit, and the stat icons share one tint.
- [ ] **AC8** — Given the profile's pending-document tile, When a long title and a 1-byte file
      render, Then the title truncates on one line with a minimum width and the size reads a
      pluralised unit.

## Implementation notes

Depends on T-0785, T-0786, T-0793 (the dead partner SCSS is gone first). If Q-UI-08 is overruled
to "header row", the two partner breadcrumbs become the header row instead of `cleansia-breadcrumb`
and T-0788 keeps the back button on the title row. **Regen:** none. **Guard:** F2, F6, F7 in T-0798.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 3, partner web lane, runs beside the admin lane (disjoint feature libs). Q-UI-08 default
  (breadcrumb), Q-UI-09 / 11 / 12 defaults (leave) in force.
