---
id: T-0717
title: Android customer — copy per market
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0716]
blocks: [T-0720]
stories: []
adrs: [ADR-0060]
layers: [android]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

The confirm-step trust badge and the FAQ stated "1M CZK" with nothing behind it, the no-show push
stated "250 Kč", and the home tab carried a seasonal card no promotion backed.

## Doing

- `booking_trust_insured` / `help_faq_a3` → `%1$s` + `_no_figure` twins ×5;
  `MarketListItem.insuranceCoverage(amount, currencyCode)`; `ConfirmStepViewModel` formats the
  booking country's market figure when listed, else the chosen market's, else the no-figure key;
  `HelpSupportViewModel` (new, stateful/stateless split) likewise for the FAQ.
- `notification_order_no_cleaner_refunded_body` ×5 without a figure.
- `SeasonalCard` + `home_seasonal_*` ×5 deleted.

## NOT

No loc-arg change (`FcmMessageFactory`, `NotificationTemplates` arg set untouched); no partner strings;
no workflow path change; the checker flip was the orchestrator's.

## Acceptance criteria

AC1–AC5 of the programme plan: badge with "1 000 000 Kč" from `formatOrderPrice`, no-figure variants,
push without an integer or currency word, seasonal card gone, checker green with the pins flipped.

## Review

- Commit `12144ffe` (14 files). `MarketCopyStringsTest` (4), `ConfirmStepViewModelTest` (5),
  `HelpSupportViewModelTest` (2) new; 6/6 sabotages caught.
