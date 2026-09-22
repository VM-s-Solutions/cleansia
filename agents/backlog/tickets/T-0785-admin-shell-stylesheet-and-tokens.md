---
id: T-0785
title: Admin shell — one page wrapper, one header, one button width, one field height, one section with an action slot, one label/value grid, the tokens that are referenced and undefined, the z-index scale, one focus ring
status: done
size: M
owner: —
created: 2026-09-20
updated: 2026-09-22
depends_on: [T-0793]
blocks: [T-0786, T-0787, T-0788, T-0789, T-0790, T-0794, T-0795, T-0796, T-0797]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"I want to polish alignments in admin app a bit on all of the
overview/detail pages"*. The visual scout captured the 70 admin routes and 13 partner routes at
1440×1024 and traced every misalignment to eight root causes (VF §1, R1–R8); the consistency scout
found each of them re-declared per page (CS §1–§2). This ticket fixes the root causes **once**, in
the shared stylesheet and the four shared primitives the page tickets then rely on — a page ticket
that finds itself re-implementing a shared rule is out of its lane. Ground truth at `069b72ae`:
`common/page-wrapper.scss:6` is 8 px and 21 admin pages re-declare 12 px and `max-width: 1400px`;
`cleansia-button.component.scss:6` is `min-width: 100%`; `cleansia-section.component.html:1-8` has
no action slot; 18 CSS custom properties are referenced with no declaration (an invalid
`cleansia-select` has no red border in any app).

Paths are relative to `src/Cleansia.App/`; stylesheet paths under `libs/shared/assets/src/styles/`.
Screenshot names are the discovery's captures (`admin-<route>-1440.png`, `crops/*.png`).

## Doing

- **Page wrapper (R1).** `common/page-wrapper.scss:2-16` becomes the only card: `background:
  var(--cleansia-white); border-radius: 12px; padding: 2rem; max-width: 1400px; margin: 0 auto;
  display: flex; flex-direction: column; gap: 1.5rem;` + one shadow + one border; the outer gutter
  `padding: 1rem` on the page root via one shared `.cleansia-page` rule. Delete the 21 per-page
  copies (`grep -n "max-width: 1400px" pages/cleansia-admin/*.scss`; e.g.
  `employee-management.component.scss:1-22`) and the 37 `&__container { border-radius: 12px;
  background: … }` re-declarations (CS §1.3). Width **list 1400 / detail-form 1200** by a second
  class (`.page-wrapper--narrow`), never per page; fix the two outliers
  `notifications.component.scss:14` (1100) and `invoice-detail.component.scss:6` (1400 on a detail).
  Drop the duplicate `@use` in `apps/cleansia-admin.app/src/styles.scss:13` (already in
  `common/index.scss:42`). The nine unstyled features (`data-protection`, `fiscal-failures`,
  `loyalty-promo-codes`, `loyalty-referrals`, `loyalty-tier-configs`, `loyalty-user-detail`,
  `membership-plan-management`, `employee-document-config` — `document-requirements.component.html:1-8`
  uses an unruled `page-container`; `service-area-management.component.scss:1-12`) and
  `admin-profile.component.scss:14` (800) get the card by carrying the two classes — no page
  stylesheet needed for the shell.
- **Page header (VF 6.2).** One `.cleansia-page-header` rule: `display: flex; align-items:
  flex-start; justify-content: space-between; gap: 1rem`; left = `cleansia-title` + muted one-line
  subtitle (`max-width: 60ch`); right = actions `display: flex; gap: 0.75rem; align-items: center`,
  **secondary before primary**. Delete the 20 `&__header { text-align: center }` lines (CS §2.1:
  `admin-user-management.component.scss:21`, `audit-log:23`, `company-management:21`,
  `country-management:21`, `currency-management:21`, `disputes-management:21`,
  `employee-management:21`, `extra-management:20`, `invoice-management:21`, `language-management:21`,
  `legal-documents:21`, `order-management:21`, `package-management:21`, `pay-config-management:20`,
  `pay-period-management:21`, `reports:21`, `service-management:20`, `template-management:21`,
  `company-lifecycle:21`, `company-settings:21`) and the header rhythm drift
  `service-management.component.scss:19-27` vs `package-management.component.scss:19-27`. The
  reference is `notifications.component.scss:20-30` + the partner lists
  (`pages/cleansia-partner/orders.component.scss:18-23`).
- **Button width (R2).** `components/cleansia-button.component.scss:5-6` `min-width: 100%` → `auto`;
  the two opt-outs at `:195-197` and `:265-267` become no-ops and go; a new `[block]` input (or
  `.cleansia-button--block`) for the auth screens and mobile footers that *want* full width — audit
  first (`rg "cleansia-button" apps/*/src libs/cleansia-*-features --glob '*.html' -l` and the
  customer wizard footers: the customer app inherits this rule, so the flip ships with `block`
  applied wherever a capture shows a full-width button today).
- **Field heights (R6).** `components/cleansia-select.component.scss:7-15` and
  `cleansia-calendar.component.scss:1-5` get the text input's `min-height: 44px`
  (`cleansia-text-input.component.scss:9-13`); rewrite the calendar rule for the PrimeNG 20
  `p-datepicker` DOM (`cleansia-calendar.component.ts:37-39`, `iconDisplay: 'input'`) so the icon
  sits inside the box and the field fills its column.
- **Section action slot (R8).** `cleansia-section.component.html:1-8` gets a header row
  (`<div class="cleansia-section__header"><h2 …>{{ title() }}</h2><ng-content select="[section-actions]"></ng-content></div>`);
  `components/cleansia-section.component.scss:1-5` puts the rule under the header row, `gap: 2rem`
  → `1.25rem`; `cleansia-section__title` colour from `--cleansia-text-primary` (`:10`) to the Sky700
  the page title uses (`cleansia-title.component.scss:25`).
- **Label/value grid (R7).** One `common/detail-grid.scss`: `.detail-grid { display: grid;
  grid-template-columns: repeat(4, 1fr); gap: 1.25rem 2rem }` (2 at ≥ 768, 1 below),
  `.detail-grid__label` 12 px Slate500 sentence-case **no trailing colon**, `.detail-grid__value`
  15 px, `.detail-grid__item--wide { grid-column: span 2 }`. Replaces
  `employee-detail.component.scss:112-115`, `order-detail.component.scss:100-103`,
  `pay-period-detail.component.scss:98-106`.
- **Tokens referenced and undefined (CS §5a).** Define in `common/variables.scss` the 18 names in
  use with no declaration — `--cleansia-error-{50,100,400,500,600,700,800,rgb}`,
  `--cleansia-success-{100,800}`, `--cleansia-warning-{100,300,700,800}`, `--cleansia-primary-25`,
  `--cleansia-text-muted`, `--cleansia-text-placeholder`, `--cleansia-border` — from the DL ramps;
  plus `--cleansia-radius-{sm,md,lg,xl}` = 6/12/16/24 and `--cleansia-shadow-{1,2}`. This fixes the
  nine no-fallback sites: `components/cleansia-select.component.scss:49,70-71` (**no red border on an
  invalid select in any app**), `pages/cleansia-partner/profile.component.scss:549-558`,
  `order-details.component.scss:1288-1294`, `dashboard.component.scss:194,403`. Replace
  `var(--border-radius)` (PrimeNG v17, undefined in the v20 preset) at
  `pages/cleansia-admin/invoice-detail.component.scss:138,180` and
  `components/cleansia-file.component.scss:19,147,154`.
- **z-index (CS §1.1).** Drawer `filter-drawer.scss:113,128` (1000/1001) and sidebar
  `cleansia-sidebar-menu.component.scss:80,115` (998/999) move under the documented scale in
  `common/z-index.scss:5-11` so toasts (900) render above an open drawer.
- **Focus.** The ten `outline: none` sites without a `:focus-visible` replacement (CS §9:
  `filter-drawer.scss:300`, `notifications.component.scss:99`, `cleansia-table.component.scss:538`,
  `cleansia-sidebar-menu:294`, `cleansia-select:45,114`, `cleansia-file:26`, partner
  `invoices.component.scss:400`, `orders.component.scss:459`) get one shared `%focus-ring`.
- **Guard:** `apps/cleansia-admin.app/src/app/theme/page-shell.spec.ts` — reads
  `libs/shared/assets/src/styles/pages/cleansia-admin/*.scss` and fails on
  `max-width:\s*1[0-9]{3}px`, `border-radius` inside a `&__container`, or `text-align:\s*center`
  inside a `&__header`; asserts `cleansia-button.component.scss` has no `min-width: 100%`. Same
  shape as `font-stack.spec.ts`; runs in `frontend-ci` (jest).

## NOT

- Re-colouring the neutral palette to DL slate (Q-UI-07: leave); loading Poppins (Q-UI-06: amend the
  design language instead — T-0799); spacing tokens / the 8-pt sweep (Q-UI-10: leave).
- The radius sweep of the ~200 off-scale values beyond the ones the page tickets touch (T-0798
  ratchets it).
- Any per-page stylesheet beyond deleting its shell copy (T-0787–T-0790 own the pages).
- The customer app, beyond applying `[block]` where a capture shows a full-width button today.

## Done looks like

`grep -c "max-width: 1400px\|text-align: center" pages/cleansia-admin/*.scss` = 0; every admin
capture shows the card at x = 14 px from the sidebar with the title at top-left;
`admin-employee-documents-1440.png` re-captured has a card; an invalid `cleansia-select` shows a red
border in the admin-user form; a toast renders above an open filter drawer; `page-shell.spec.ts`
green in `frontend-ci`.

## Acceptance criteria

- [ ] **AC1** — Given the admin page stylesheets, When `page-shell.spec.ts` runs, Then it finds no
      `max-width: 1NNNpx`, no `border-radius` under a `&__container` and no centred `&__header`, and
      `cleansia-button.component.scss` has no `min-width: 100%`.
- [ ] **AC2** — Given the nine formerly unstyled admin routes, When each is re-captured at 1440, Then
      it renders inside the same card (same x-offset, radius, padding) as the employee list.
- [ ] **AC3** — Given the admin-user create form with an empty required select, When the form is
      submitted, Then the `cleansia-select` shows a red border (the `--cleansia-error-*` tokens
      resolve in all three apps).
- [ ] **AC4** — Given a `<cleansia-button>` outside a form footer in any app, When rendered, Then it
      sizes to its label; a button carrying `[block]` fills its container; every customer-wizard
      footer button that was full-width at `069b72ae` still is.
- [ ] **AC5** — Given the `cleansia-calendar` in the admin-user form, When rendered, Then the field
      is 44 px, fills its column and the icon sits inside the box.
- [ ] **AC6** — Given a `<cleansia-section>` with `[section-actions]` content, When rendered, Then
      the projected actions sit on the title row's right edge and the title is Sky700.
- [ ] **AC7** — Given an open filter drawer and a toast, When both render, Then the toast is above
      the drawer (z-index from `common/z-index.scss`).
- [ ] **AC8** — Given each of the ten former `outline: none` sites, When focused by keyboard, Then a
      visible focus ring renders (`%focus-ring`).

## Implementation notes

Depends on T-0793 so the dead `.p-datatable` blocks and orphan stylesheets are gone before this
restyles anything. **Regen:** none. Reference captures: `admin-notifications-1440.png` (header),
`admin-package-management-1440.png` (list), `crops/customer-1.png` (the seven full-width bars the
`[block]` audit must keep), `crops/emp-detail-top.png`, `crops/order-detail-3.png`.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 2, web-shared lane, first of the serial four (T-0785 → T-0786 → T-0794 → T-0796).
- 2026-09-22 — **done**; shipped 2026-09-21 as `eb01bba24` on chore/ui-polish-and-dead-code (PR #260).
  `common/page-wrapper.scss` (`.cleansia-page` gutter + `.page-wrapper` card, `--narrow` for detail
  and form pages), `common/page-header.scss`, `common/detail-grid.scss`, `common/focus.scss`
  (`%focus-ring`), the referenced-but-undefined tokens plus `--cleansia-radius-*` and
  `--cleansia-shadow-*` in `variables.scss`, the z-index scale, the `[section-actions]` slot on
  `cleansia-section` with its title in `--cleansia-primary-700`, `cleansia-button` sizing to its label
  with `[block]` the one full-width variant, `cleansia-select` and `cleansia-calendar` at the text
  input's 44 px. Guard: `apps/cleansia-admin.app/src/app/theme/page-shell.spec.ts` (frontend-ci).
  Findings reported, not absorbed, on `agents/OWNER-PLATE.md` (the 2026-09-22 block): the
  `size="'full-width'"` sites still mapped rather than on `[block]`, `appearance="brand"` with zero
  consumers, the sidebar style budget. The neutral palette, Poppins and spacing tokens stayed as
  Q-UI-07 / 06 / 10 defaults (`design-language.md`).
