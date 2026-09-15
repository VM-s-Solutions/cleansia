---
id: T-0713
title: Admin web — per-currency plan prices, the no-show credit, the insurance ceiling
status: done
size: M
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0710, T-0711]
blocks: [T-0720]
stories: []
adrs: [ADR-0059, ADR-0060, ADR-0058]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

T-0710 replaced the plan's two CZK scalars with a per-currency dictionary and T-0711 added the
no-show credit, the insurance ceiling, the alpha-2 and the servicing gate; the admin app still bound
`monthlyPriceCzk` / `stripePriceId` and knew none of the new fields or keys.

## Doing

- `membership-plan-management`: a `prices` FormGroup with **one block per currency the platform
  knows** (Active / Optional badge), each block optional, sent only when both price and Stripe id are
  filled, half-filled refused client-side (`priceBlockHalfFilled`), blank ≠ 0 on populate; the list
  prints the default-currency price with its code and "—" when null, a Currency column, no price sort;
  `BILLING_INTERVAL_WIRE` re-pointed at the regenerated numeric enum.
- `currency-management`: `noShowCredit` beside the divisor (min 0.01, blank → absent on the wire).
- `country-management`: `isoAlpha2` (required on create, pattern-only on edit, upper-cased), a Market
  section with `insuranceCoverageAmount` disabled with a hint until `hasConfiguration`, saved through
  its own PUT after the country update; `service-area-management` snaps the serviced toggle back off
  on `country.market_not_ready`.
- `api.*` ×5: `country.market_not_ready`, `country.configuration_missing`, `country.iso_alpha2_invalid`,
  `membership.plan.stripe_price_already_used`; 25 keys per locale in all; parity spec roster +4.

## NOT

No customer app; no new permission; no `CountryConfiguration` editor beyond the one field; no Stripe
Price creation UI; no "all active currencies required" rule.

## Acceptance criteria

AC1–AC8 of the programme plan: per-currency blocks, partial prices legal, half-filled refused, blank
populate, "—" rendering, errors translated ×5, currency form null/250, country form order of the two
PUTs.

## Review

- Commits `a2d65191` (feature, 34 files) and `bae82513` (partner invoices lint, split out).
- Jest: 5 + 4 + 2 suites green across the three libs; parity spec green; lint 0 problems on 5
  projects; typecheck OK. 14 sabotages, each caught by a named test.
- Absorbed: `iso_code_placeholder` corrected to alpha-3 examples; two `TemplateRef<any>`; a
  pre-existing `label-has-associated-control` lint error in the city dialog.
- Findings (not fixed): `cleansia-text-input` crashes under a nested `formGroupName` (the package /
  service / extra price blocks bind that way — very likely throw at render); the plan form has no
  stylesheet under `shared/assets`; the membership feature toasts twice on a refused save; the city
  dialog uses raw `<input pInputText>`.
