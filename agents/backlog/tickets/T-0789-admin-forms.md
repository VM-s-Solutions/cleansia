---
id: T-0789
title: Admin create / edit forms — the 14 forms on the pay-config shape (12-column `form-grid` in `common/`, spans not pixels, 44 px fields, row hints, a right-aligned `Zrušit` + primary footer), `cleansia-multiselect` adopted, the last five `*ngIf` gone
status: todo
size: M
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"polish alignments in admin app"*. The admin forms share a `form-grid` /
`form-field` vocabulary on 20+ forms (CS §2.3) but each page stylesheet declares its own tracks —
two 267 px fields beside 600 px of air on the service form, sections in 2/1/4/3/3/4 columns on the
company-info form, titles in a 4-col grid over texts in a 3-col grid on the marketing push, and two
forms with **no stylesheet at all** so `form-grid` has no rule (membership plan, promo code: fields
stacked with 0 gap, floating labels overprinting hints, a checkbox overlapping the next label).
Hints under one field break the row's bottom edge; a 36 px `p-multiSelect` sits beside 44 px inputs.
Canonical pattern: VF 6.5 = `admin-pay-config-management__create-1440.png`
(`pay-config-form.component.html` / `.scss`): back → title (no actions right) → sections (h3 +
rule) → rows on a **12-column `form-grid`** (`repeat(12, 1fr); gap: 1rem 1.5rem`; fields span
12/6/4/3, never a fixed px) → footer rule → right-aligned `Zrušit` (outlined) then primary, both
`auto-width`, equal heights. Hints under the **row**; checkboxes on the row's field baseline;
translation tabs labelled with the language *name*, content spanning the full grid.

**Forms (14):** service, package, extra, admin user, country, currency, language, company info,
marketing push, membership plan (new/edit), promo code (new/edit), pay config (reference), profile,
legal document. Paths are relative to `src/Cleansia.App/`.

## Doing (capture · defect · fix site)

- One `common/form-grid.scss` carrying the 12-column rule and the span classes; every
  `pages/cleansia-admin/*-form.component.scss` loses its own tracks.

| Form | Capture | Defect | Fix site |
|---|---|---|---|
| Service | `admin-service-management__create-1440.png` | `Kategorie` 36 px beside 44 px `Název` (T-0785); first row two 267 px fields + 600 px air; translation tab content 600 px, indented 16 px | `service-form.component.scss:27-60` → spans |
| Package | `admin-package-management__create-1440.png` | `Zahrnuté služby` external label over a 36 px `p-multiSelect`, label repeats the section title | `package-form.component.html:97` → `cleansia-multiselect` (exists, 0 consumers; T-0793 keeps it *because* this ticket adopts it) with a float label |
| Admin user | `admin-admin-user-management__create-1440.png` | `Datum narození` icon outside, field narrower than column, hint breaks the row; `Role` / `Preferovaný jazyk` 36 px | `admin-user-form.component.html` (row hint); heights from T-0785 |
| Country | `admin-country-management__create-1440.png` | hint under the middle field only → ragged row | `country-form.component.html` (row hint) |
| Company info | `admin-company-info__create-1440.png` | sections 2/1/4/3/3/4 columns; `Plátce DPH od` calendar hang + two-line hint; `Země` 36 px | `company-info.component.scss`, `company-info-form.component.html` → 12-col spans |
| Marketing push | `admin-marketing__sitewide-push-1440.png` | titles in a 4-col grid, texts in a 3-col grid; no back control | `sitewide-push.component.scss` → one grid; header per 6.2 |
| Membership plan | `admin-membership-plan-management__new-1440.png` | fields stacked with **0 gap**, floating labels overprint hints, checkbox overlaps the next label; full-width `Zpět` / `Zrušit` / `Vytvořit plán` | `membership-plan-form.component.html:1-24` (no stylesheet) → `common/form-grid.scss`; the trial field is T-0793's |
| Promo code | `admin-loyalty__promos__new-1440.png` | same as membership; two calendars at 210 px with hanging icons | `promo-code-form.component.html` (no stylesheet) |
| Profile | `admin-profile-1440.png` | `max-width: 800px` centred — a fourth width | `admin-profile.component.scss:14` → the narrow class (T-0785) |
| Approve / reject dialogs' error rows | — | hand-rolled `<small class="__error" *ngIf>` — the only `*ngIf` left in admin (5) | `approve-dialog.component.html:20-24`, `reject-dialog` → `@if` + `showErrors` on the control (or T-0790, whichever lands second removes the rest) |

## NOT

- Field semantics or validation rules. The `[floatVariant]="'on'"` redundancy (88 sites, cosmetic —
  CS 2g). The Cancel i18n key unification beyond the forms this ticket touches (T-0796 owns
  `global.actions.cancel`). The country form's `ISO kód` / `Dvoupísmenný kód` content (Q-UI-13:
  layout only, the fields stay).

## Done looks like

Every form capture shows fields sharing the 1 130 px content width on a 12-column rhythm, all fields
44 px, no calendar icon outside its box, a straight bottom edge on every row, footer `Zrušit` +
primary right-aligned at equal height; `rg "form-grid" pages/cleansia-admin/*.scss` = 0 (the rule
lives in `common/`).

## Acceptance criteria

- [ ] **AC1** — Given `pages/cleansia-admin/*-form.component.scss`, When `page-shell.spec.ts`'s new
      case runs, Then no file declares `grid-template-columns: repeat(N, ` or a `px` column track
      (spans only) and `rg "form-grid" pages/cleansia-admin/*.scss` = 0.
- [ ] **AC2** — Given the 14 forms re-captured at 1440, When each row is measured, Then every field
      is 44 px, the row's bottom edge is straight (hints under the row), and the fields share the
      content width on the 12-column rhythm.
- [ ] **AC3** — Given the package form, When rendered, Then `Zahrnuté služby` is a
      `cleansia-multiselect` with a float label at 44 px and no external label repeating the section
      title.
- [ ] **AC4** — Given every form footer, When rendered, Then `Zrušit` (outlined) then the primary
      sit right-aligned, both `auto-width`, at equal height; no full-width `Zpět` bar remains.
- [ ] **AC5** — Given `libs/cleansia-admin-features`, When `rg "\*ngIf"` runs, Then it returns
      nothing (the five dialog error rows are `@if`).
- [ ] **AC6** — Given the admin profile form, When rendered, Then it sits in the narrow card
      (1200) — no `max-width: 800px`.

## Implementation notes

Depends on T-0785 (field heights, calendar DOM, the narrow width class). **Regen:** none. The
membership-plan and promo-code forms have no stylesheet today and need none after — the rule is in
`common/form-grid.scss`.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 3, admin web lane, third of four.
