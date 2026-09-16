---
id: T-0767
title: Responsiveness audit of the three web apps at phone width — a report of what breaks, no changes without the owner's pick
status: todo
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
