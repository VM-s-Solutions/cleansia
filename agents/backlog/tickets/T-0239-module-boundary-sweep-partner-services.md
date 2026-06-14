---
id: T-0239
title: Module-boundary sweep — customer features off @cleansia/partner-services + eslint boundary rule
status: ready
size: M
owner: pm
created: 2026-06-12
updated: 2026-06-14
depends_on: [T-0259]
blocks: []
stories: []
adrs: []
layers: [frontend]
security_touching: false
manual_steps: []
sprint: 6
source: Wave-3 review finding (3 files fixed in-wave; grep 2026-06-12 shows 14 remaining)
---

## Context
Customer feature libs import API clients from **`@cleansia/partner-services`** instead of the
per-app `@cleansia/customer-services` client. Three call sites were fixed opportunistically during
Wave 3; a verification grep (2026-06-12) shows **14 files still violating** across
`libs/cleansia-customer-features/`: forgot-password.facade.ts, gdpr.component.ts + gdpr.facade.ts,
home/components/services/services.component.ts, login.facade.ts, order-wizard (4 files:
order-wizard.component/facade/models + wizard-summary-step.component), profile.component.ts +
profile.facade.ts, recurring-bookings.facade.ts, register.facade.ts,
services-catalog.component.ts. Risk: the customer SSR app compiles against the **partner** OpenAPI
contract — a partner-only regen (or partner-endpoint removal, cf. T-0173a) silently breaks or skews
customer flows, and the NSwag hold-point discipline per app is undermined.

**No-decision note:** mechanical canonicalization to the existing per-app-client convention + a
lint guard; no new behavior or contract — skips the deliberation panel.

## Acceptance criteria
- [ ] **AC1** — Zero imports of `@cleansia/partner-services` remain under
  `libs/cleansia-customer-features/**` and `apps/cleansia.app/**`; each call site uses the
  equivalent `@cleansia/customer-services` client/DTO. If any consumed endpoint has **no** customer
  client equivalent, STOP and flag it to the PM (that is a backend/host gap, not a swap).
- [ ] **AC2** — An eslint module-boundary rule (Nx `@nx/enforce-module-boundaries` tags or an
  explicit `no-restricted-imports` per feature scope) makes the violation a lint **error**: customer
  features may not import partner/admin services, partner features may not import customer/admin
  services, etc. CI lint proves it (rule trips on a deliberate fixture, then passes clean).
- [ ] **AC3** — `npx nx build cleansia-app` (production SSR) + affected unit suites green; no
  behavior change (same endpoints, same DTO shapes — the clients are generated from per-host specs,
  so verify DTO parity per swapped call site and flag any drift).
- [ ] **AC4** — The boundary rule + tag scheme documented in
  `agents/knowledge/patterns-frontend.md`.

## Out of scope
- Regenerating any NSwag client (owner-only; the swap targets the existing generated customer
  client). Admin/partner-side boundary violations beyond adding the rule (file separately if the
  lint sweep surfaces them).

## Implementation notes
Verify per file that the customer client exposes the same operation (the hosts share AppServices but
not necessarily controller surface). The order-wizard cluster is the riskiest (4 files, money path)
— do it last, with its existing specs as the harness. Serialize with any concurrent order-wizard
work (AUD-07 is a future wave; none in flight now).

## Status log
- 2026-06-12 — draft (created by pm at Wave-3 close; review-finding sweep, 14 files enumerated)
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6C**). No-decision mechanical canonicalization + a
  lint guard → skips the panel. **Added `depends_on: [T-0259]`** — the `@nx/enforce-module-boundaries` rule
  (AC2) needs the workspace **tags** T-0259 lays down to be effective; **runs AFTER T-0259** in Lane
  FE-config. Re-derive the violating-file list against current master (the body's 14-file grep was
  2026-06-12). The **order-wizard cluster (4 files, money path) is done LAST** with its existing Jest specs
  as the harness; no AUD-07 work is in flight (shipped Wave 5) → no contention. Per-call-site DTO-parity
  check; flag any drift to the PM. Plan: `status/sprint-8.md` §3 Batch 6C.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
