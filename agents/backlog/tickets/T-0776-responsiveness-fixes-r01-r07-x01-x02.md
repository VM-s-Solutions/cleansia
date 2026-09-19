---
id: T-0776
title: Responsiveness fixes from the 2026-09-16 audit — R01–R07, X01, X02 across the three web apps
status: done
size: M
owner: —
created: 2026-09-19
updated: 2026-09-19
depends_on: [T-0767]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

# T-0776 — Responsiveness fixes from the 2026-09-16 audit

Owner ruling 2026-09-19 (D6, *"go with recommended"*) on `agents/analysts/web-responsiveness-2026-09-16.md`. All nine groups were built — the audit's layout groups are partner/admin surfaces, not customer ones, so the "customer layout groups" in the recommendation had no referent and the P1s (R01, X01) went first.

## Doing

- X01 — the admin package edit route rendered nothing (its own load effect re-armed itself); it renders, with a spec.
- R01 — the partner and admin shells had no navigation at exactly 768 px (the sidebar CSS collapsed at ≤768 while the shells switched to mobile at <768); one mode at 768, spec on the shell's mode selection.
- X02 — a shared input inside a nested form group bound to the wrong control and logged console errors on the admin catalogue forms; it binds its own control.
- R02–R06 — thirteen action labels truncated at 400/768 px on partner /gdpr, /my-pay and admin employee documents, e-mail translations, audit entry; standalone actions size to their label (the 2026-08-27 single-line-ellipsis ruling kept, no wrapping), and a template guard fails any `<cleansia-button>` that pairs a label with a small-width slot.
- R07 — the 44 px touch floor as a hit ring (`::after`) around the painted control, not a bigger control: shared button, text field, password eye, mobile menu button, sidebar close, table action and pagination buttons; the visual pill unchanged.

## NOT

- No new breakpoints, no layout redesign; the audit's runner-provenance limits stand.

## Done looks like

- Each group fixed at its root with a spec that goes red on revert; lint, typecheck and both app builds green.

## Status log

- 2026-09-19 — done in 55661dea (X01), d7a54a59 (R01), f2562d67 (X02), 24299a99 (R02–R06), 0299e127 + 355ee133 (R07; the review found the first cut had grown the pill to 44 px and left the audit's own controls untouched — the fix moved the floor into a hit ring and covered the menu button, sidebar close and table buttons). Web workspace 74 projects / 2 893 specs, lint 77, typecheck 3/3, both partner and admin builds green.
