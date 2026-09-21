---
id: T-0799
title: Docs and living pages for the UI-polish batch, last — availability out of the docs, the removed routes off the API reference and the two permission tables, the Cart off the model, the design-language and pattern pages, MS-2 / A1 to the new `Initial` id, the backlog rows flipped, the Q-UI rulings recorded
status: todo
size: S
owner: —
created: 2026-09-20
updated: 2026-09-20
depends_on: [T-0785, T-0786, T-0787, T-0788, T-0789, T-0790, T-0791, T-0792, T-0793, T-0794, T-0795, T-0796, T-0797, T-0798]
blocks: []
stories: []
adrs: [ADR-0034, ADR-0006, ADR-0066, ADR-0001]
layers: [docs]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Docs are written at the end of a feature, once it is green (memory: docs after implementation).
The discovery found the docs already contradicting the tree on availability
(`docs/getting-started.md:68`, `docs/api/authentication.md:322`, `docs/partner-app/onboarding.md:224`)
and on the Stripe forward (`docs/deployment/environment-config.md:320`); after T-0791 and T-0792 the
gap widens to two routes, three tables, two columns, eight permission rows and a `Cart` the model
page still draws. Every sentence is ground-truthed against the batch's head commit, not against the
plan.

## Doing

- **Availability:** `docs/admin-app/user-management.md:42,306-315` (row + section go);
  `docs/partner-app/onboarding.md:87-91` (the info box becomes "there is no schedule") and `:224`
  (three lock categories, not four); `docs/getting-started.md:68`; `docs/partner-app/overview.md:19,83`;
  `docs/api/authentication.md:322`; `docs/domain/model.md` (Employee attributes, verify; the `Cart`
  at `:102-104,162-164` and `EmailTranslation` off the diagram and the entity count — Q-UI-02
  fold); `docs/architecture/database.md` (the `Employees` and `MembershipPlans` column lists, the
  table count, the new `Initial` id); the API reference (two routes gone).
- **Endpoints removed in T-0792:** the API reference pages for the partner host (`PayConfig`,
  `Currency`, `Package`, `Service`, `PayPeriod/{id}`, the two `EmployeePayroll` actions, `Dispute`)
  and the admin host (`get-paged`, `GET {userId}`, `get-current`);
  `docs/decisions/adr-0066.md:354,356,363` and `adr-0001.md:235,251,264` permission tables lose the
  eight rows (a one-line dated note each — the ADR is the record of *why*, the row is history);
  `docs/deployment/environment-config.md:320` (the Stripe forward → the customer host, whichever way
  Q-UI-03 is answered); `CountryController.cs:26` comment reworded (*"legacy/admin paths"* is wrong;
  partner web + both mobile profile pickers call it — `profile-bank.facade.ts:70-71`,
  `IdentificationSectionViewModel.kt:101`, `PartnerProfileClient.swift:164`) — a comment edit, the
  docs lane's to make.
- **Free trial:** `docs/product/features.md` / `business-rules.md` membership section no longer
  mentions a trial field on the admin form or a `TrialPeriodDays` column (verify what T-0690's docs
  already say; only the admin-UI sentence and the column change).
- **The design language and the how-we-build pages:** `agents/knowledge/design-language.md` records
  the Q-UI-06 / 07 / 10 defaults as written (back-office headings are Nunito — the doc is amended,
  not the apps; neutrals stay Tailwind gray; no spacing tokens) or *"unchanged, ruled on <date>"*;
  `agents/knowledge/patterns-frontend.md` names `cleansia-status-badge`, `cleansia-filter-drawer`,
  `cleansia-mobile-toolbar`, `cleansia-breadcrumb`, `formatDate`, the `[section-actions]` slot, the
  page-shell classes and the F-rules; `agents/knowledge/consistency.md` gets the F1–F15 rows;
  `libs/shared/assets/src/styles/README.md` completed (T-0793 leaves it truthful); `docs/admin-app/*`
  screenshots re-captured only where a page embeds one (check for `![`; none expected).
- **ADR notes:** ADR-0034 (`PayoutDetailsStatus.NeedsReconfirmation`, D5) and ADR-0006 (the refund
  seam; `RefundStatus.Failed`) each get a dated *what shipped* line that the state has no producer
  (Q-UI-05), whichever way it is answered.
- **The record:** `agents/backlog/INDEX.md` rows T-0785–T-0799 flipped by the lanes as they ship
  (this ticket verifies each row names its commits); `agents/backlog/questions/open.md` Q-UI-01…13
  updated with any ruling that came (Q-UI-01 / 02 deleted once T-0791 ships — the record is the
  ticket); `agents/OWNER-PLATE.md` the Q-UI rows and A1's runbook id; `agents/cleanup/MANUAL_STEPS.md`
  MS-2 to the new `Initial` id; `CHANGELOG.md` `[Unreleased]` — *Removed* (the availability
  schedule, the four schema things, the routes) and *Changed* (the admin and partner web alignment)
  in the reader's vocabulary.

## NOT

- Any docs page for a feature this batch did not change. ADRs — none of this is a decision (it is
  alignment and deletion); if Q-UI-08 lands as "breadcrumb", the PM decides whether a one-page ADR
  is owed for the navigation idiom. No docs build run by the lane if it has no shell — the
  orchestrator runs `check-docs-refs.mjs`, `check-catalog-claims.mjs` and the VitePress build and
  the row closes on those results.

## Done looks like

`docs-ci` green (docs-refs 0 unresolved, catalog-claims 0 violations, VitePress build);
`rg -i "availability" docs --glob '!docs/decisions/**'` returns only offerability and
slot-availability mentions; `docs/domain/model.md` draws no `Cart`; MS-2 and A1 name the batch's
`Initial` id.

## Acceptance criteria

- [ ] **AC1** — Given `docs/` outside `decisions/`, When `rg -i "availability"` runs, Then every hit
      is offerability (ADR-0037) or the serving-cleaner slot check (ADR-0039).
- [ ] **AC2** — Given the API reference, When each route removed by T-0791 and T-0792 is searched,
      Then none is documented as live, and the two permission tables carry a dated note in place of
      the eight rows.
- [ ] **AC3** — Given `docs/domain/model.md` and `docs/architecture/database.md`, When read, Then
      neither names `Cart`, `EmailTranslation`, `Employee.Availability`, `PreferredCurrencyCode` or
      `TrialPeriodDays`, and the migration id and table count match the tree.
- [ ] **AC4** — Given `agents/knowledge/design-language.md`, When read, Then it states the
      back-office heading face as shipped (Nunito) with the Q-UI-06 date, and the Q-UI-07 / 10
      defaults.
- [ ] **AC5** — Given the orchestrator's run, When `check-docs-refs.mjs`, `check-catalog-claims.mjs`
      and the VitePress build execute, Then all three are green and the row closes on their results.
- [ ] **AC6** — Given `CHANGELOG.md`, When read, Then `[Unreleased]` carries the *Removed* and
      *Changed* entries in the reader's vocabulary with no attribution line.

## Implementation notes

Runs last, once everything is green. The `→ /path#anchor` pointers the code lanes left (T-0791's
reworded comments) are verified by `check-docs-refs.mjs` — a page or heading renamed here fails
Docs CI, so rename nothing the code points at without moving the pointer.

## Status log

- 2026-09-20 — filed 2026-09-20 from the UI-polish discovery; branch chore/ui-polish-and-dead-code.
  Phase 4, docs lane, last.
