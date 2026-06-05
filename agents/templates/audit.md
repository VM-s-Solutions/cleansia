# Audit — <subsystem / area>

- **Auditor:** <analyst | architect | reviewer | security | optimizer>
- **Date:** YYYY-MM-DD
- **Scope:** which projects/folders/features were examined
- **Method:** what was compared against what (docs, stories, lifecycle in CLAUDE.md, code)

## Summary
One paragraph: overall health of the area, the biggest risk, the highest-value fix.

## Findings
Ranked by impact. Each finding is directly convertible to a ticket.

### F1 — <title>   [severity: critical | major | minor]   [type: gap | bug | spaghetti | hardcoded | perf | security]
- **Where:** file:line / area
- **What:** the concrete problem
- **Why it matters:** user/business/security/cost impact
- **Proposed fix:** the long-term-correct resolution (not a workaround)
- **Proposed ticket:** `<imperative ticket title>`  size: S/M/L  layers: [...]

### F2 — ...

## Not-issues considered
Things that looked wrong but are intentional (cite the reason) — so they aren't re-flagged later.
