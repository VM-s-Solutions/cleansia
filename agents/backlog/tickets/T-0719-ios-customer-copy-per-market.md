---
id: T-0719
title: iOS customer — copy per market
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0718]
blocks: [T-0720]
stories: []
adrs: [ADR-0060]
layers: [ios]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

The T-0717 mirror on iOS, plus the partner catalogue's copy of the no-show push body (the same
sentence, not read by the checker).

## Doing

- `InsuranceCopy.swift` (new): `trustBadge` / `faqAnswer` via `OrdersFormat.price(amount, currencyCode:)`
  into `%1$@`, else the `_no_figure` key; `TrustBadges(insurance:)`, `HelpSupportView(insurance:)`,
  `BookingViewModel.insurance` for the booking's country.
- `Localizable.xcstrings` (customer): `booking_trust_insured` / `help_faq_a3` → `%1$@` ×5, the two
  `_no_figure` keys ×5, `push.order.no_cleaner_refunded.body` ×5 without a figure, `home_seasonal_*`
  deleted — the customer catalogue now contains no `CZK|Kč|EUR|€` anywhere. Partner catalogue: the
  same five push bodies.
- `SeasonalCard` and its two L10n members deleted from `HomeSecondarySections` / `HomeTab` / `L10n+Home`.

## NOT

Loc-arg allowlist untouched; `PushLocKeyCatalogTests` expectations only where the copy changed; no
workflow path change; the checker flip was the orchestrator's.

## Acceptance criteria

AC1–AC6 of the programme plan (the T-0717 criteria on iOS, plus the partner catalogue's push body
carrying no integer and no currency word).

## Review

- Commit `c3540d8b` (15 files). `check-ios-symbols` clean (2 → 0); `MarketCopyTests` (6, new — the
  renderers, the no-figure variants, both push catalogues, a catalogue-wide no-currency sweep, the
  seasonal deletion) and `BookingCurrencyBindingTests` (+2) to run on the Mac. Sabotages 6–10 of the
  lane's list caught by the static checks.
