---
id: T-0678
title: One button and one title component across every page
status: ready
size: M
owner: frontend
created: 2026-08-30
updated: 2026-08-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 16
---

## Context
- <cleansia-button> is already dominant - 24 customer files, 73 partner and admin - but 8 customer files still use raw pButton and 9 carry .cl-btn-primary / .cl-btn-outline.
- Headings are split: 5 files use <cleansia-title>, 8 use .cl-title. The component renders var(--cleansia-primary) at fixed sizes; .cl-title renders Sky700 fluid.
- Owner decision 2026-08-30: the shared component adopts the fluid Sky700 scale and .cl-title is deleted. Partner and admin headings shift - same hue family, fluid rather than fixed.

## Acceptance criteria
- [ ] **AC1** - cleansia-title carries the clamp() scale and Sky700; .cl-title no longer exists.
- [ ] **AC2** - No page defines its own button styling; .cl-btn-primary and .cl-btn-outline are gone.
- [ ] **AC3** - Raw pButton usage in the customer app is replaced by <cleansia-button>.
- [ ] **AC4** - All three apps build and their suites pass; partner and admin are checked visually for the heading shift.

## Out of scope
- Adding variants to the components beyond what the pages already need.
- The PrimeNG theme preset.

## Status log
- 2026-08-30 - ready

## Review
<!-- reviewer / security / optimizer write verdicts here -->
