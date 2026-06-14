---
id: T-0241
title: Admin-app eslint selector-prefix alignment (kill the recurring baseline noise)
status: ready
size: S
owner: pm
created: 2026-06-12
updated: 2026-06-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 6
source: recurring Wave-3 baseline noise (flagged since Batch 3A; T-0191 frontend fixed one instance ad hoc)
---

## Context
Reviewers flagged the same lint baseline noise on every Wave-3 admin-frontend ticket since Batch 3A:
admin feature libs carry inconsistent Angular eslint **selector-prefix** config — some lib
`eslint.config.mjs` files use the scaffold default `lib`/`app` prefix while the catalog mandates the
`cleansia` prefix (the T-0191 frontend slice fixed `package-management`'s `lib`→`cleansia` ad hoc;
several Wave-3-scaffolded libs also shipped with missing jest/eslint/tsconfig infra that had to be
patched mid-review). Each new lib regenerates the noise and each reviewer re-flags it.
**No-decision note:** mechanical config alignment to the existing `cleansia` convention — skips the
panel.

## Acceptance criteria
- [ ] **AC1** — Every lib under `libs/cleansia-admin-features/**` (and the admin app) enforces
  `@angular-eslint/component-selector` + `directive-selector` with prefix `cleansia` in its eslint
  config; no lib retains the scaffold `lib`/`app` prefix.
- [ ] **AC2** — `npx nx lint` across the admin projects is green — any selector renamed to comply is
  updated at all usage sites (templates included) in the same change; admin production build green.
- [ ] **AC3** — The Nx generator defaults (`nx.json` / generators config) set the `cleansia` prefix
  so newly scaffolded libs are born compliant — the noise stops recurring, not just today's instances.
- [ ] **AC4** — Convention noted in `agents/knowledge/patterns-frontend.md` if not already explicit.

## Out of scope
- Customer/partner feature libs (sweep separately if the same drift exists — note findings to PM).
- Any non-selector eslint rule changes.

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; recurring reviewer baseline noise since 3A)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6C**). No-decision mechanical config alignment to the
  `cleansia` selector-prefix convention → skips the panel. **Lane FE-config:** edits admin eslint configs +
  the Nx generator default (`nx.json` generators block) + the admin app. **Parallel to the T-0259→T-0239
  chain** UNLESS it needs the same `nx.json` generators block they touch — if so, serialize after T-0259.
  Plan: `status/sprint-8.md` §3 Batch 6C.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
