---
id: T-0794
title: "`cleansia-filter-drawer` — one drawer, one chip row, one list scaffold, replacing the 60-line block copied into 13 admin and 2 partner components"
status: todo
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785]
blocks: [T-0787]
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"make overall check on both apps to make them consistent"*. The filter
drawer is the largest copy in the two back-office apps: the same ~60-line block of markup, the same
drawer SCSS (`pages/cleansia-admin/filter-drawer.scss:106-…` = partner `orders.component.scss:265-…`
= `invoices.component.scss:254-…`) and the same eleven methods re-declared in 13 admin and 2 partner
components — with the partner copy the only one carrying dialog semantics
(`role="dialog" aria-modal="true"`, a typed and labelled close button). Ground truth at `069b72ae`:
compare `company-info-list.component.html:14-54,82-133` with `orders.component.html:55-89,91-230`.
Lands before T-0787 so the list pages adopt the shared drawer rather than fix 13 copies.

Paths are relative to `src/Cleansia.App/`.

## Doing

- Extract `libs/shared/components/src/lib/cleansia-filter-drawer/` carrying the **partner a11y
  variant** (`role="dialog" aria-modal="true" [attr.aria-label]`, `orders:104-110`; typed +
  labelled close `:117-124`) and the drawer SCSS as `components/cleansia-filter-drawer.component.scss`.
  Chip-remove buttons get `type="button"` + `aria-label` = chip label (both apps lack it:
  `company-info-list:21-26`, `orders:62-67`). Inputs `[open]`, `[chips]`, `[count]`; outputs
  `(apply)`, `(reset)`, `(close)`; content projected.
- A `FilterDrawerState` helper (signals) replacing the eleven methods re-declared per component
  (`openFilterDrawer` / `closeFilterDrawer` / `resetFilters` / `clearAllFilters` / `removeFilterChip` /
  `hasActiveFilters` / `activeFilterCount` / `activeFilterChips` / `applyFilters` / `onPageChange` /
  `onSortChange` — 13× each) and moving the 24× `filterForm.valueChanges` + 31×
  `translate.onLangChange` subscriptions out of the components into the facades (the frontend
  pattern: subscriptions live in the facade). The 15 components shrink by ~120 lines each.
- Drawer placement unified (inside the page card, the partner shape); search inputs with float
  labels (partner shape, `orders:135-153`); footer `Reset` (one key).
- The rows-per-page `inputId="rows-per-page-select"` duplicate on two-table pages
  (`cleansia-table.component.html:108-110`; partner orders) → per-instance suffix.
- The 39 admin + 6 partner raw drawer `<button>`s vanish with the component; the raw buttons that
  are *not* drawer scaffold are listed for T-0787 (`service-area-management:128,137`,
  `pay-period-management:216`, `email-type-detail:54`) and T-0797 (partner) in this ticket's status
  log when it ships.
- **Guard:** `cleansia-filter-drawer.component.spec.ts` (open / close / reset / chip-remove;
  `role="dialog"` present); F1 in T-0798 for the raw buttons.

## NOT

- The list header (T-0785), the table (T-0786), the promo / referrals ad-hoc filters (T-0787 decides
  drawer vs inline row per page), any new filter that does not exist today.

## Done looks like

`rg -l 'class="filter-drawer"' libs` → only the shared component; `rg -c "openFilterDrawer" libs`
= 1; the admin employee list and the partner orders list render the same drawer DOM (same `role`,
same close button); axe on either drawer: 0 violations for dialog semantics.

## Acceptance criteria

- [ ] **AC1** — Given the 15 list components, When `rg -l 'class="filter-drawer"' libs` and
      `rg -c "openFilterDrawer" libs` run, Then only the shared component matches.
- [ ] **AC2** — Given the admin employee list and the partner orders list, When each drawer is
      opened, Then both render `role="dialog" aria-modal="true"` with a translated `aria-label`, a
      typed labelled close button, and chip-remove buttons with `type="button"` and an `aria-label`
      equal to the chip's text.
- [ ] **AC3** — Given a page with two `cleansia-table`s, When rendered, Then the two rows-per-page
      selects have distinct `inputId`s.
- [ ] **AC4** — Given any of the 15 components, When its source is read, Then it holds no
      `filterForm.valueChanges` or `translate.onLangChange` subscription (they live in the facade)
      and none of the eleven drawer methods.
- [ ] **AC5** — Given `cleansia-filter-drawer.component.spec.ts`, When it runs, Then open / close /
      reset / chip-remove each emit the right output and the dialog role is asserted.

## Implementation notes

Depends on T-0785 for the z-index scale (the drawer moves under it). **Regen:** none. The partner
copy is the source of truth for semantics; the admin copy for placement is *not* — the drawer sits
inside the page card (partner shape).

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 2, web-shared lane, third of the serial four; lands before T-0787.
