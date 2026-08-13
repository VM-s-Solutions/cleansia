---
id: T-0230
title: Wave-0 PR review fixes — envelope dual-read, S8 tenant scope, GoogleAuth guard, money/perf
status: done
size: L
owner: —
created: 2026-06-05
depends_on: []
blocks: []
stories: []
adrs: [ADR-0001, ADR-0002]
layers: [backend, db]
security_touching: true
manual_steps:
  - ef-migration   # (#6) User unique index Email -> (TenantId, Email); (#11) Orders (PaymentStatus, CreatedOn) recon index
sprint: 0
source: multi-agent PR review of feature/wave-0-prod-readiness (35 confirmed findings)
---

## Context
Independent multi-agent review of the Wave-0 PR surfaced 35 adversarially-verified findings. This
ticket tracks fixing them in priority order (all in `feature/wave-0-prod-readiness`).

## MUST-FIX (blocking)
- [x] **#1** `SendPushNotificationHandler` — ADR-0002 D2.1a envelope DUAL-READ (was dropping every
  enveloped push silently). + enveloped/bare tests.
- [x] **#2** `CalculateOrderPayHandler` — envelope DUAL-READ (no pay row was created for completed
  orders). + tests.
- [x] **#3** `SendSitewidePromoFanoutHandler` — scope recipient query to `campaign.TenantId` (S8
  cross-tenant push leak). + real-DbContext tenant-isolation test.
- [x] **#4** `GoogleAuth` — move the AuthenticationType guard into the handler against the verified
  `claims.Email`; trim the validator to shape rules. + handler/validator tests.

## Strongly recommended (same PR)
- [x] **#6** `User` unique index `Email` → `(TenantId, Email)` (S8). **MANUAL_STEP: ef-migration.**
- [x] **#7** `PromoCodeService` — global redemption counter leaks a slot on per-user reservation failure.
- [x] **#8** `GenerateInvoiceHandler` — envelope dual-read now (latent; stub today).

## Medium
- [x] **#9/#10** promo fan-out: keyset (seek) paging instead of Skip(offset); resumability risk
  documented (mid-campaign retry re-notifies — accepted for best-effort marketing, follow-up for a
  persisted cursor / push dedup).
- [x] **#11** receipt-reconciliation sweep: add `Orders (PaymentStatus, CreatedOn)` index.
  **MANUAL_STEP: ef-migration.**
- [x] **#12** `DeadLetter` — align ITenantEntity intent vs docs.

## Low / Nit
- [x] **#15** GoogleAuth validator trimmed (folded into #4).
- [x] **#18** recon re-enqueue preserves locale — `ThenInclude(r => r.Language)` added.
- [x] **#21** `PoisonHandlerBase` — guard `RecordAsync` so a DB fault alerts + acks (no poison loop).
- [x] **#22** last-active-admin guard — atomic conditional `ExecuteUpdateAsync` (S7a), 0 rows ⇒
  CannotDeactivateLastAdmin; validator stays as the fast-path UX message.
- [x] **#23** recon per-item country-config N+1 — memoized per sweep (Dictionary cache).
- [x] **#25** unused `EmployeeDocumentsMissing` const — RETAINED with a comment (referenced by
  negative-assert tests; its locale key is still used by the frontend registration flow). Review's
  "delete OR comment why retained" alternative taken.
- [~] **#26** dashboard handler visibility — REJECTED. The handlers are `public` because their tests
  construct them directly; unifying to `internal` (as the nit proposed) breaks those tests. Kept
  `public` with an explanatory comment. The review nit overlooked the test dependency.
- [ ] **#16** receipt PDF/blob-failure artifact recovery in None/lenient modes — DEFERRED (follow-up).
- [ ] **#19** GDPR admin guard double round-trip — DEFERRED: cold admin path; folding the two
  distinct-message rules into one query would change edge-case error precedence + churn the validator
  tests for a marginal cold-path gain. Non-blocking per the review.
- [ ] **#20** receipt claim/realize re-resolve of companyInfo/mode — DEFERRED (bounded background reads).
- [ ] **#24** `CountryConfigurationRepository` enforcement-mode projection — DEFERRED (paired with #20).

## MANUAL_STEP (owner) — DONE
The owner regenerated the migration (`20260605165935_Initial`) carrying the `(TenantId, Email)` unique
index and the `(PaymentStatus, CreatedOn)` recon index. Merged in PR #72.

## Deferred follow-ups (Wave 2, non-blocking)
#16 / #19 / #20 / #24 are intentionally deferred cold-path micro-improvements (see the strike-through
rationale above). They do NOT block Wave 1; track them as a small Wave-2 polish ticket if/when those
paths are touched.

## Status log
- 2026-06-05 — created from the PR review; #1–#4, #6, #9/#10, #15 done test-first.
- 2026-06-05 — all blocking + recommended + medium + low/nit items shipped in PR #72 (+ comment cleanup
  in #73); owner regenerated the migration. CLOSED as **done**; only the four non-blocking deferred
  follow-ups (#16/#19/#20/#24) remain, moved to Wave-2 polish.
