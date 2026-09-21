---
id: T-0790
title: Admin dialogs — the 12 `p-dialog` templates on one footer shape, the two confirmations that cannot open fixed, translated accept / reject labels on the 11 bare `.confirm({...})` sites; the "Create pay period" stub's layout only (Q-UI-04)
status: todo
size: S
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785, T-0796]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Owner ruling 2026-09-20: *"polish alignments in admin app"*. The visual scout did not drive dialog
states (VF §0), so the 12 admin `p-dialog` templates (CS §3.2) were read, not captured: approve /
reject / wind-down / issue-credit / refund / pay-period create / pay-period ops / payroll ops /
weekly-limit / document requirement / email-type translation / work-contract, plus the row-level
dialogs on the customer detail. Two findings are functional, not polish: `membership-plan-list.component.ts:53,147`
and `data-protection.component.ts:64,132` provide a scoped `ConfirmationService` but render no
`<p-confirmDialog>` — by reading, *Deactivate plan* and *Erase user* have an unreachable accept
callback (CS §11.1). Eleven `.confirm({...})` sites omit `acceptLabel` / `rejectLabel`, so a Czech
session reads the hard-coded English `Yes` / `No` (`admin app.config.ts:69-70`). Canonical pattern:
`components/cleansia-dialog.component.scss` + `approve-dialog.component.html` (reactive form,
`cleansia-textarea`, `cleansia-button`): body on the `form-grid`, footer = `Zrušit` (outlined) then
one primary, right-aligned, `auto-width`; danger primary red **outline** only when destructive; no
`success` / `warn` / `info` fills.

Paths are relative to `src/Cleansia.App/`.

## Doing

- **Capture first.** Each dialog open (runner: click the trigger, 6 s idle) *before* changing it;
  the captures go beside the discovery's under `shots/dialogs/` and the before/after pair is named in
  the status log.
- Every dialog footer on the one shape; every `severity="success|warn|info"` in a dialog →
  primary / outlined (`approve-dialog.component.html`, `wind-down-dialog.component.html`).
- **The two dialogs that cannot open** — verified in the browser first; fixed by moving to the root
  `DialogService` (T-0796's idiom — applied here because they are broken now, not merely
  inconsistent).
- Confirm dialogs: `acceptLabel` / `rejectLabel` on the 11 admin `.confirm({...})` sites that omit
  them (`company-info-list.component.ts:203-210`, `employee-detail:379`, `admin-user-management`,
  `country-management`, `service-area-management`, `document-requirements`, `extra-management`,
  `language-management`, `package-management`, `pay-config-management`, `service-management`,
  `email-type-detail`) → `DialogService.confirmTranslated`, so the order of this ticket and T-0796
  does not matter.
- `*ngIf` → `@if` in the five admin dialog templates if T-0789 has not (whichever lands second
  removes the rest).
- The "Create pay period" dialog: its **layout** aligned to the shape; its behaviour untouched.

## NOT

- Wiring the "Create pay period" stub — its button ends in `console.warn`
  (`pay-period-management.component.ts:313-320`) while `adminPayPeriodClient.create` exists
  (`admin-client.ts:13206`); wiring or removing it is a product decision (**Q-UI-04: leave**).
- New dialog content. The partner dialogs (T-0797).

## Done looks like

Every admin dialog capture shows `Zrušit` + one primary right-aligned at 44 px; *Deactivate plan*
and *Erase user* open a confirmation; no English `Yes` / `No` in the Czech UI.

## Acceptance criteria

- [ ] **AC1** — Given the membership-plan list and the data-protection page, When *Deactivate plan*
      / *Erase user* is clicked, Then a confirmation opens and its accept callback runs (the
      component provides no scoped `ConfirmationService`).
- [ ] **AC2** — Given each of the 12 dialogs captured open, When compared to `approve-dialog`, Then
      the footer is `Zrušit` (outlined) then one primary, right-aligned, both 44 px `auto-width`;
      the only red is a destructive primary's outline.
- [ ] **AC3** — Given a Czech session, When any of the 11 confirm sites opens, Then the two buttons
      read the bundle's words (through `confirmTranslated`), never `Yes` / `No`.
- [ ] **AC4** — Given `libs/cleansia-admin-features`, When `rg 'severity="(success|warn|info)"'`
      runs over dialog templates, Then it returns nothing.
- [ ] **AC5** — Given the "Create pay period" dialog, When opened, Then its layout matches the shape
      and its primary still ends in the existing stub (no new behaviour — Q-UI-04).

## Implementation notes

Depends on T-0785 and T-0796 (the root dialog). **Regen:** none. **Guard:** F3 / F4 / F5 in T-0798
(`<p-confirmDialog` outside the shell, `providers: [ConfirmationService]`, direct `.confirm(`
outside `dialog.service.ts`).

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 3, admin web lane, last of four. Q-UI-04 default (leave the stub) in force.
