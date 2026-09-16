---
id: T-0767
title: Responsiveness audit of the three web apps at phone width — a report of what breaks, no changes without the owner's pick
status: done
size: S
owner: —
created: 2026-09-16
updated: 2026-09-16
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: —
---

## Context

Item 2 of the owner's 2026-09-13 improvement list ("responsiveness"). The owner will pick what to fix; this ticket produces the report only: every route of the customer, partner and admin apps at ~400 px and ~768 px (Playwright or the existing e2e harness), screenshots or a table of what overflows, wraps badly, hides an action or scrolls horizontally, grouped by severity, with the design-language rules it violates (`agents/knowledge/design-language.md`).

**Doing** — the report under `agents/analysts/` (or the folder the analysts use), one row per finding with route, width, symptom, suggested fix, size.
**NOT** — no code change.
**Done looks like** — the report exists and the owner has a list to pick from.

## Status log

- 2026-09-16 — Complete: [report and evidence](../../analysts/web-responsiveness-2026-09-16.md)
  cover all 109 rendered bindings and 12 redirects at both requested widths: 242 attempted pairs,
  240 DOM measurements, two package-edit timeouts. Ten measured admin form captures have textarea
  errors. Seven layout/control-sizing groups and two runtime groups are documented with severity,
  source, proposed repair and size. Independent review approved the report; the runner-hash drift
  is disclosed while captured-result, source and screenshot hashes match. Coverage chiefly describes
  initial states under isolated synthetic fixtures. No application repair is included; selection
  remains with the owner.
- 2026-09-16 — Inventoried 108 terminal routes, one wrapper and 12 redirects. Browser audit started
  at 400 × 844 and 768 × 1024 with synthetic sessions and generated-contract fixtures. Static
  bundles are served from disk; API bases are replaced in the served copies with loopback origins.
  Every browser request is fulfilled locally or blocked, with an unreachable proxy as a second
  barrier. Exact public font/icon dependencies were cached separately. Route coverage and findings
  are being collected; no existing-screen responsiveness fix is included in this ticket.
