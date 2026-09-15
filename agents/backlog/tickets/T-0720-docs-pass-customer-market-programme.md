---
id: T-0720
title: Docs pass — three ADRs accepted, living docs, roles, business rules
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0710, T-0711, T-0712, T-0713, T-0714, T-0715, T-0716, T-0717, T-0718, T-0719]
blocks: []
stories: []
adrs: [ADR-0058, ADR-0059, ADR-0060]
layers: [docs]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

Docs are written at the end, once the feature is green. Six implementation lanes shipped the
customer-market programme; the three ADR drafts described intent and the lanes' reports and the tree
are the truth. Two docs sentences ("no currency picker … there will not be one", "the market is a
property of the booking") and every "250 CZK" / `NoShowCreditCzk` / `MonthlyPriceCzk` citation were
stale, and three code pointers (`/decisions/adr-0058`, `/decisions/adr-0059`) resolved to nothing.

## Doing

- `docs/decisions/adr-0058.md`, `adr-0059.md`, `adr-0060.md` as `accepted` (2026-09-13), the owner
  rulings of 2026-09-12 quoted, the Challenge / Defense / Verdict trail closed, every lane deviation
  recorded in §Consequences (the alpha-2 column, the mobile chip hidden with one market, the Android
  Plus sheet's `countryCode`, the dead transfer cache, `StripeRefusals`, the no-clause lock copy, the
  empty list for an unserviced country, no admin price sort, a block per every currency, the annual
  switch gated on the membership's currency, `terms_page.section3_text_no_market`, `%1$s` / `%1$@`);
  `index.md` rows and count; the VitePress sidebar (ADR-0056–0060, which was missing 0056/0057 too).
- Living docs: `business-rules.md` (the no-show sentence, a Plus-per-market block, `#money-constants`
  rewritten — no-show per currency, Plus prices, insurance ceiling, promotion moves the default market
  — the order-currency paragraph, a new `#market` section); `features.md`; `domain/model.md`
  (`MembershipPlanPrice`, `UserMembership.CurrencyId`, `Currency.NoShowCredit`,
  `CountryConfiguration.InsuranceCoverageAmount`, `Country.IsoAlpha2`); roles
  `market-directory.md` + `membership-plan-price.md` + index; flows `booking-and-pricing` (market
  before address; a stale "currency conversion" clause; the recurring-form asymmetry),
  `loyalty-and-memberships` (Plus per market end to end, edge cases), `cancellation-refund-dispute`;
  `api/markets-and-memberships.md` (new) + `api/orders.md` cross-links; `admin-app/overview.md`,
  `customer-app/overview.md`; `platform-expandability.md` (§0 banner, §2 order/customer surfaces/
  default-bound numbers, §3 item 8, §3b row, §5 table, §7c as shipped, §8 with both gates and the
  optional steps + `{#expansion-path}`, §10).
- `agents/knowledge/design-language.md` §6.2 (no "Kč" literals as examples; no trial),
  `patterns-frontend.md` (the placeholder rule as a descriptive note citing `rules.component.ts`).
- `CHANGELOG.md` Added / Changed / Removed entries.
- Backlog: T-0712–T-0720 rows and tickets; `questions/open.md` gains Q-MARKET-01, -02, -03, -04, -05.

## NOT

No code; no edits to accepted ADRs other than the three new ones (ADR-0035:659 still cites the deleted
`NoShowCreditCzk` — a historical record, reported); no `npm run build` (no shell in the lane — flagged
for the orchestrator); `agents/architecture/decisions/*.md` living notes not updated (reported).

## Acceptance criteria

AC1–AC6 of the programme plan by inspection; AC7 (`npm run build`, `check-docs-*`) is the
orchestrator's to run.

## Review

- Every claim checked against the tree: entities, EF configurations and the regenerated `Initial`
  (`20260913080510`), the DTOs under `Cleansia.Core.AppServices`, the controllers on all three hosts,
  `StripeRefusals`, the seed, the checker's pins, the web/Android/iOS membership facades and view
  models, the locale files.
