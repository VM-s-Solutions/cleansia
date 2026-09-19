---
id: T-0715
title: Customer web — money figures in copy come from the market
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: [T-0714]
blocks: [T-0720]
stories: []
adrs: [ADR-0060]
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

The home rules card stated "250 CZK" in five locales and the terms page said "Prices are displayed
in CZK"; ADR-0060 D0/D1/D3 make both a placeholder formatted from the market.

## Doing

- `pages.home.rules.we_cancel_value` → `{{amount}}` ×5 + `we_cancel_value_refund_only` ×5;
  `RulesComponent.creditAmount` = `formatMoney(selectMarketNoShowCredit, selectMarketCurrencyCode, locale)`,
  null (→ refund-only key) when the credit is null, zero or no market resolved; comment rewritten.
- `terms_page.section3_text` → `{{currency}}` ×5 from `selectMarketCurrencyCode`, passed through the
  legal-document component's new `sectionParams` / `sectionTextKeys` inputs; **new key
  `terms_page.section3_text_no_market`** ×5 for the no-market state (the draft's reuse of
  `catalogue_changed_for_country` was wrong — that key opens with a trimmed-basket notice).
- Checker: the web pins flipped to `pinPlaceholderCopy` (placeholder present, no integer, no currency
  word); `section3_text` and `section3_text_no_market` pinned; drift case in the self-test.

## NOT

No mobile copy; no e-mail templates; `catalogue_changed_for_country`, `pages.disputes.none_item2`,
error strings untouched; the privacy-page phone number untouched (reported).

## Acceptance criteria

AC1–AC5 of the programme plan: "250 Kč" rendered from the market in `cs`, refund-only for a null
credit, terms per market and the no-market sentence, checker green + drift case red, no literal
figures under `pages.home.rules.*` / `terms_page.*`.

## Review

- Commit `2ba96d5d`. `rules.component.spec.ts`, `terms.component.spec.ts`,
  `legal-page-blankable-copy.spec.ts` green; checker self-test green after the flip. Sabotages: rules
  never states the credit / prints a bare number / never renders refund-only; terms never picks the
  no-market key; legal document drops params — all caught.
