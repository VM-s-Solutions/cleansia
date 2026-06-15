---
id: T-0239
title: Module-boundary sweep — customer features off @cleansia/partner-services + eslint boundary rule
status: done
size: M
owner: pm
created: 2026-06-12
updated: 2026-06-15
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
- 2026-06-14 — **review** (frontend). Re-derived violators against current master: **14 files** under
  `libs/cleansia-customer-features/**` (forgot-password/login from the 2026-06-12 body were already fixed;
  the 14 are the order-wizard cluster of 6 incl. 2 specs, gdpr ×2, profile ×2, register, recurring-bookings,
  services-catalog, home/services). `apps/cleansia.app/**` was already clean. **AC1** done — all swapped to
  `@cleansia/customer-services`; the consumed operations/DTOs all exist on the customer host (verified each
  swapped symbol is shape-identical between the two generated clients — **zero DTO drift**, AC3). Nine DTOs
  were present in the generated `customer-client.ts` but not surfaced by the hand-maintained
  `libs/core/customer-services/src/index.ts` barrel (`PackageServiceSummary`, `CategoryDto`, `CountryListItem`,
  `ConsentType`, `UserConsentDto`, `GrantConsentCommand`, `WithdrawConsentCommand`, `GdprExportDto`,
  `UpdateCurrentUserCommand`) → added to the barrel (not a nswag-regen — NSwag only emits `client/*-client.ts`).
  **AC2** done via T-0259's `scope:*` tags: added `scope:customer|partner|admin →
  [<same-scope>, scope:shared]` constraints to `eslint.config.mjs`. **TDD red→green captured:** with the new
  rule but unfixed imports, `nx lint cleansia-customer-gdpr` errored
  ("A project tagged with `scope:customer` can only depend on libs tagged with `scope:customer`, `scope:shared`")
  on both gdpr files; after the swap it (and all 8 customer feature projects) lint with **0 errors**. **AC3**:
  `nx build cleansia.app --configuration=production` (SSR) **green**; `cleansia-customer-order-wizard` **119/119**
  + `cleansia-customer-profile` **15/15** Jest green; no behavior change. **AC4** done — boundary rule + tag
  scheme documented in `patterns-frontend.md`.
  **Out-of-scope pre-existing issues found (NOT fixed — report only):** (1) `customer/partner/admin-services`
  (`type:util`) have **circular deps** with their `*-stores` (`type:data`) and `services`→`pipes` — these
  `*-services` libs already failed `nx lint` before this change (T-0259 fallout); my scope constraints add no new
  errors there. (2) `libs/cleansia-customer-features/{gdpr,home,services-catalog}/jest.config.ts` have a wrong
  preset depth (`../../../../` vs `../../../`) + `setupFilesAfterSetup` typo → `nx test` errors; these projects
  have no specs so it was latent. (3) `cleansia-partner-gdpr`/`cleansia-partner-forgot-password` aren't in the nx
  project graph ("Could not find project"). (4) Pre-existing lint warnings on touched files (`PackageServiceSummary`
  unused in services-catalog, `loadCustomerServices` unused in home/services, `any`/non-null) left as-is to keep
  the swap surgical.
- 2026-06-14 — **ready** (PM, Wave-6 intake / Batch **6C**). No-decision mechanical canonicalization + a
  lint guard → skips the panel. **Added `depends_on: [T-0259]`** — the `@nx/enforce-module-boundaries` rule
  (AC2) needs the workspace **tags** T-0259 lays down to be effective; **runs AFTER T-0259** in Lane
  FE-config. Re-derive the violating-file list against current master (the body's 14-file grep was
  2026-06-12). The **order-wizard cluster (4 files, money path) is done LAST** with its existing Jest specs
  as the harness; no AUD-07 work is in flight (shipped Wave 5) → no contention. Per-call-site DTO-parity
  check; flag any drift to the PM. Plan: `status/sprint-8.md` §3 Batch 6C.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
