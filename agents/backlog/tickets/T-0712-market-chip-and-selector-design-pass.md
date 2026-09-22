---
id: T-0712
title: Design pass — the market chip and the market selector, three clients
status: done
size: S
owner: —
created: 2026-09-13
updated: 2026-09-13
depends_on: []
blocks: [T-0714, T-0716, T-0718]
stories: []
adrs: [ADR-0058, ADR-0059, ADR-0060]
layers: [design]
security_touching: false
manual_steps: []
sprint: 16
---

## Context

ADR-0058 D6 asked for one shared chip and selector per client, drawn once, and carried the mobile
"chip with one market" question to a design pass that had to precede the three client lanes.

## Doing

- One-page spec (`scratchpad/market/design-spec.md`) + 14 canvas artboards: web quick-quote chip
  (`.cl-chip`, control with ≥ 2 markets, static label with one, absent with none), navbar/footer pill
  (the language pill's geometry with a map glyph, ≥ 2 markets only), Android/iOS home-header chip
  (`CleansiaChip` geometry, button semantics, ≥ 2 markets only) and Market preference screens (the
  Language screens' twins), the "Plus is not available in your market yet" state on three surfaces
  (hero kept, no price, no button), the no-market state (chip and selector absent, nothing else moves).
- Copy keys named by intent, no literal figure in any: `we_cancel_value` / `_refund_only`,
  `booking_trust_insured` / `_no_figure`, `help_faq_a3` / `_no_figure`, `plus.not_available_in_market`.
- Q-DESIGN-01 raised: the ticket wrote "CZ · CZK" and the wire carried alpha-3 only. Resolved by the
  orchestrator as "add `Country.IsoAlpha2`" (T-0711).

## NOT

No code; no partner/admin surfaces; no flag glyphs; no Czech copy; `design-language.md` untouched (the
chip does not differ from `CleansiaChip`).

## Acceptance criteria

AC1–AC5 of the programme plan: one-market chip as a label, multi-market chip with states and a ≥ 44 px
target, mobile parity with the Language screens, the empty and no-market states drawn, the copy
variants named without a figure.

## Review

- DL §7 pre-submission checklist run on the spec and artboards; two fixes (map glyph, iOS 44 pt hit
  area). Ten findings reported (F-1 iOS zero price with no plan, F-2 teaser CTA leak, F-3 dead
  `cl-btn--on-dark`, F-5 `CleansiaChip` checkbox semantics, …), each closed or carried by the client
  lanes.
