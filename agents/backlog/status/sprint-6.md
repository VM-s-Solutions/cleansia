# Sprint 6 — Wave 4 plan (tests + accessibility)

- **Date:** 2026-06-12
- **Goal:** Sequence and execute **Wave 4 — the test + a11y wave**: T-0210…T-0218 (TC-2/3, TC-7, TC-4,
  TC-6, TC-8, TC-9, TC-10, EP-1/EP-2/DA-7, A11Y-1) plus two carried tickets — **T-0179** (LG-07, not
  built in Wave 3) and **T-0235** (the T-0194 AC6 runtime-429 deviation made a ticket).
- **Status:** **READY — promoted 2026-06-12.** All 11 tickets pass the Definition of Ready; batches
  below. Owner gave the go signal ("Continue with Wave 4") after the Wave-3 merge.
- **Branch:** all Wave-4 work goes on **one feature branch `feature/wave-4-tests-a11y`** cut from
  current `master` (**`05bf567a`**, PR #76), committed **batch-by-batch**.
- **Scope note:** the owner scoped this wave to **tests + a11y**. The consistency/quality sweep
  (T-0196…T-0206) that sprint-5 §8.5 mentioned alongside it is **NOT in this wave** — it becomes the
  Wave-5 candidate. The Wave-3 follow-ups T-0233/T-0234/T-0236…T-0241 also stay `draft` (not in scope
  this wave; **T-0236 must land before any multi-tenant onboarding** — flagged again in §4).

---

## 0. Pre-flight reconciliation (verified against the repo, 2026-06-12)

- **Wave 3 merged:** `master` tip = **`05bf567a`** ("Feature/wave 3a admin order dispute ops (#76)").
  All Wave-0…3 dependencies of every Wave-4 ticket are **`done`**: T-0100✓, T-0111✓, T-0114✓, T-0118✓,
  T-0121✓, T-0126✓, T-0140✓, T-0143✓(epic children), T-0171✓, T-0172✓, T-0180✓, T-0194✓. **Zero open
  dependencies; no intra-wave edges** — batching is purely shared-file lanes + verification gates.
- **The §8.2 ef-migration blocker is cleared in-repo:** `20260612134125_Initial.cs` (+Designer +
  snapshot) contains the 4 Users lockout columns (`FailedLoginAttempts`, `LockoutEndsAt`, …). The
  Testcontainers fixture builds schema from the repo migrations, so the integration batch is **not**
  blocked. Residual owner confirm (§4): a green `Cleansia.IntegrationTests` run on master to formally
  close **T-0193 AC4** (a Wave-3 close-out item, not a Wave-4 gate).
- **The test harnesses the tickets reference now EXIST** (several tickets' text predates them):
  - `src/Cleansia.IntegrationTests/` — `PostgresContainerFixture` / `BaseIntegrationTest` /
    `PostgresCollection` + suites (Auth lockout/code-cap, Gdpr, Outbox claim, FiscalCounter,
    **GenerateInvoiceQueueConsumeTests**, catalog overviews).
  - `src/Cleansia.HostTests/` — the T-0115/T-0126 WebApplicationFactory harness incl.
    **Ac9CrossTenantRejectionTests** and `RateLimitCoverageGuardTests`.
- **Existing test inventory forces two resizes (dedup evidence — Waves 0–3 shipped TDD suites that
  cover much of the original TC-6/TC-7/TC-8 text):**
  - `Cleansia.Tests/Functions/` — **33 files**: smoke suites for every timer/sweep, push
    idempotency/classify/disabled-ack, fanout resume/tenant-scope, receipt idempotency (+fiscal),
    poison handlers, timer schedules, FiscalReconciliation, DeadLetter/IdempotencyGuard/CampaignProgress
    persistence, `GenerateInvoiceHandlerTests`, `SendEmailHandlerTests`,
    `CalculateOrderPayHandlerEnvelopeTests`. → **T-0214 resized L→M** (coverage audit + gap-fill net,
    not a from-scratch 16-Function suite — and the inventory is now **26 Function classes**, not 16).
  - Payroll: `AdminInvoiceAdjustmentHandlerTests`, `PayPeriodSettlementHandlerTests`,
    `GenerateInvoiceHandlerTests` (+ the integration queue-consume suite). → **T-0213 resized L→M**
    (entity-lifecycle/validator gap-fill, dedup mandated).
  - Refund/dispute: `BookingPolicyTests`, `CancelOrderRefundSeamTests`, `ResolveDisputeRefundSeamTests`,
    `DisputeTransitionTests`, `RefundPolicyTests`/`RefundAllocatorTests`/`RefundServiceTests`/
    `IssuePartialRefundHandlerTests`/`ChargebackRefundableCeilingTests`, `AdminCancelOrderHandlerTests`,
    `AdminRefundOrderHandlerTests`, `PartialRefundLoyaltyClawbackTests`. → **T-0211 stays M** but is a
    **gap-fill** (tier boundaries, formula wiring, illegal-state, validator paths), not a green-field suite.
  - **No CreateOrder tests exist** → T-0212 is genuinely green-field (full M).
  - **No webhook integration tests exist** → T-0210 is genuinely green-field (full M).
- **`sprint:` frontmatter** re-tagged to `6` on promotion (the Wave-3 convention).

---

## 1. Scope — the Wave-4 tickets (11)

| ID | Title (short) | Size | Batch | Layers | depends_on (all ✓ done) | sec gate | manual_step |
|----|---------------|------|-------|--------|--------------------------|----------|-------------|
| **T-0212** | TC-4: CreateOrder characterization tests (pre-decomposition net) | M | 4A | backend | — | no | — |
| **T-0211** | TC-7: refund/dispute money-math gap-fill tests | M | 4A | backend | T-0172✓, T-0140✓ | no (**adversarial review**) | — |
| **T-0213** | TC-6: invoice/numbering/pay-period-close tests (**resized L→M**, dedup) | M | 4A | backend | T-0171✓, T-0180✓ | no | — |
| **T-0214** | TC-8: per-Function coverage audit + gap-fill (**resized L→M**, dedup; 26 fns) | M | 4A | backend | T-0121✓, T-0143✓ | no | — |
| **T-0216** | TC-10: fiscal-mode selection characterization | M | 4A | backend | — | no | — |
| **T-0179** | LG-07 (carried): unify membership subscribe path — doc + B5 rename + lock test | S | 4A | backend, frontend | T-0111✓ | no | nswag-regen* (likely none) |
| **T-0218** | A11Y-1: a11y pass — cleansia-* primitives + order wizard | M | 4B | frontend | — | no | — |
| **T-0217** | EP-1/EP-2/DA-7: error-contract parity, customer `api.*` ×5 locales + guard test | M | 4B | frontend | — | no | — |
| **T-0210** | TC-2/3: Stripe webhook integration tests + signature-stays-on lock | M | 4C | backend | T-0114✓, T-0118✓ | no (**security advisory**) | — |
| **T-0215** | TC-9: authz / cross-tenant write-path integration tests | M | 4C | backend | T-0100✓, T-0126✓ | no (**security advisory**) | — |
| **T-0235** | Runtime 429 flood harness (T-0194 AC6 deviation) | S | 4C | backend | T-0194✓ | no | — |

\* T-0179's `nswag-regen` fires only if the handler doc/rename alters the customer OpenAPI schema
(expected comment-only → no regen; dev confirms at review).

**No ticket in this wave is `security_touching`** (all are tests / i18n / a11y / doc against existing
behavior). Per the standing money/authz lesson (refund money-math needs adversarial review — never
accept on the dev's own green suite), the PM **additionally** routes a `security`-flavored adversarial
pass on **T-0211** (money), **T-0210** (signature lock), **T-0215** (tenant boundary) — advisory, not
a frontmatter gate. **Reviewer-per-developer invariant holds on every ticket.** QA gate per ticket =
suite-green-in-CI + AC↔test-name mapping (plus a keyboard/screen-reader walkthrough on T-0218).

---

## 2. Batches, parallelism, lanes

Three batches. **4A, 4B, 4C have no file overlap and no dependency edges between them** — they may run
concurrently if instance budget allows; the recommended dispatch is 4A ∥ 4B first, 4C immediately
after the owner confirm in §4.1 (or concurrently — the migration is already in-repo).

### Batch 4A — backend unit-test nets (`Cleansia.Tests`) + the carried T-0179. 6 tickets, fan-out.
All five test tickets add new test files; collisions are only on **shared helpers**:

- **Lane U1 — `Cleansia.TestUtilities` builders:** additive new builder files are free; **an edit to
  the same existing builder file serializes** (notably Order builders shared by T-0211/T-0212 —
  whichever needs the edit first owns it, the other rebases).
- **Lane U2 — `Cleansia.Tests.csproj`:** already references `Functions.Core` + `Functions` (T-0121 /
  Wave-3) — **no csproj edit expected**; if one becomes necessary it is a single serialized edit.
- **T-0179** touches `CreateMembershipSubscription.cs` + `membership.facade.ts` + one new unit test —
  no other Wave-4 ticket touches these files; fully parallel rider.

| Ticket | Dev | Reviewer | Extra gates | Parallel with |
|---|---|---|---|---|
| T-0212 | backend | yes (concurrent) | qa-light | all of 4A |
| T-0211 | backend | yes | **adversarial money review** + qa-light | all of 4A (Lane U1 vs T-0212) |
| T-0213 | backend | yes | qa-light | all of 4A |
| T-0214 | backend | yes | qa-light | all of 4A |
| T-0216 | backend | yes | qa-light | all of 4A |
| T-0179 | backend (incl. the facade doc-note) | yes | qa-light; confirm no-regen | all of 4A |

### Batch 4B — frontend (customer app). 2 tickets, **STRICTLY SERIAL** — runs parallel to 4A.
Both edit `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` (T-0218 adds `aria.*` keys,
T-0217 adds `api.*` keys — disjoint subtrees, same 5 files). **Serialize: T-0218 → T-0217.**

| Order | Ticket | Dev | Reviewer | Extra gates | Lane |
|---|---|---|---|---|---|
| 1 | T-0218 | frontend | yes | **qa: keyboard/SR walkthrough** | sole editor of `libs/shared/components/**` + `order-wizard/**` this wave |
| 2 | T-0217 | frontend | yes | qa-light (parity guard red→green) | customer locale JSONs (after T-0218); also edits `agents/knowledge/patterns-frontend.md` (AC5) |

### Batch 4C — integration + host runtime tests. 3 tickets.
- **Lane I1 — IntegrationTests shared fixtures** (`PostgresContainerFixture` / `BaseIntegrationTest` /
  `PostgresCollection` / shared constants): T-0210 and T-0215 add new suites in parallel, but **any
  edit to a shared fixture file serializes** — first-needing ticket owns the edit, the other rebases.
- **T-0235** lives in `Cleansia.HostTests` (separate project) — parallel with both. It must **not**
  touch `RateLimitCoverageGuardTests.cs`, attributes, policies, or `CleansiaStartupBase.cs` (its own
  out-of-scope list).

| Ticket | Dev | Reviewer | Extra gates | Parallel with |
|---|---|---|---|---|
| T-0210 | backend | yes | **security advisory** (signature lock) + qa-light | T-0215 (Lane I1), T-0235 |
| T-0215 | backend | yes | **security advisory** (tenant boundary) + qa-light | T-0210 (Lane I1), T-0235 |
| T-0235 | backend | yes | qa-light | both |

### Commit cadence
One commit per batch on `feature/wave-4-tests-a11y` (4A may land as two commits if the fan-out
converges unevenly: unit nets, then T-0179). PM never merges; the PR to `master` is the owner's call.

---

## 3. Stale-ticket-text deltas (merged Wave-2/3 reality the implementing agents MUST be told)

The ticket bodies were written 2026-06-01, before Waves 2–3 merged. The PM corrections, per ticket:

- **T-0210:** (1) `HandlePaymentNotification.cs` was changed by **T-0174** (chargeback `LinkStripeDispute`
  wiring) — all cited line numbers are stale; re-derive. (2) The dispatch substrate is now the **durable
  outbox** (T-0157/T-0158) and **ADR-0010** (durable consumer idempotency) is in force alongside
  ADR-0002 — AC1's "assert at the `IQueueClient.SendAsync` seam" must target the **current** post-commit
  dispatch seam (verify whether effects are realized via outbox rows; assert effect-count at the real
  seam). (3) Webhook paths are **anonymous → no tenant claim**: repo reads on those paths use
  `IgnoreQueryFilters` + tenant override before write (Wave-3 fix) — multi-tenant fixtures must
  account for this. (4) `Cleansia.IntegrationTests` **exists** — consume `PostgresContainerFixture`/
  `BaseIntegrationTest`, do not stand up a new harness. (5) citext columns require
  `EnableUnmappedTypes` — already fixed in `DbContextBindingExtensions`; container tests exercise it.
- **T-0211:** the refund path was **re-architected** after the ticket was written: CancelOrder/
  ResolveDispute now go through the **`IRefundService` seam** (T-0164), generalized cancel +
  `CancelledBy` enum landed (T-0170a), the **dispute transition guard** (T-0172) constrains
  `Dispute.Resolve`, and RefundPolicy/allocator/per-country Stripe-fee config exist (T-0167). Cited
  line numbers and the bare `TotalPrice * (1m - feeRate)` handler wiring are stale. **Dedup hard**
  against the existing suites (`BookingPolicyTests`, `CancelOrderRefundSeamTests`,
  `ResolveDisputeRefundSeamTests`, `RefundPolicyTests`, `RefundAllocatorTests`, etc.) — this ticket
  fills the **gaps**: fee-tier boundary cases (exact `FreeCancellationHours`/`PartialCancellationHours`
  edges, oops windows, Plus override), refund-amount formula + response wiring, illegal-state
  rejections, `ResolveDispute` validator paths. Extend, never duplicate. Adversarial review applies.
- **T-0212:** AUD-06 (the CreateOrder decomposition) has **NOT run** (it sits in the deferred
  consistency wave) — the characterization-before-refactor rationale holds fully. Line numbers may
  have drifted (Wave-1/2 touches); the Cash-path enqueue (AC9) is realized through the **post-commit
  dispatch** mechanism (`IPendingDispatch`/outbox), not necessarily a direct `IQueueClient.SendAsync`
  in the handler — assert at the seam the handler actually uses today.
- **T-0213:** `GenerateInvoice.cs` and the payroll lifecycle were changed by **T-0171** (adjust/
  dispute/reject, MarkPaid/Reopen) and **T-0180** (the function now really runs the command) — line
  refs stale; pin **post-T-0171** behavior. **Dedup** against `GenerateInvoiceHandlerTests`,
  `AdminInvoiceAdjustmentHandlerTests`, `PayPeriodSettlementHandlerTests`, and the integration
  `GenerateInvoiceQueueConsumeTests`. Remaining gaps ≈ the pure-entity nets: `EmployeeInvoice`
  numbering/variable-symbol/transition guards, clamp-to-zero, `AssignToInvoice` completeness,
  `PayPeriod` lifecycle guards, `ClosePayPeriod` validator paths. PDF-failure DTO fields are
  **T-0238**, not here.
- **T-0214:** **AC6 is wrong now** — `GenerateInvoiceFunction` is **implemented** (T-0180), not a stub;
  replace the stub characterization with tests of the real delegation (validation-failure → ack vs
  infra → throw, per actual classification). The inventory is **26 Function classes** (incl.
  `FiscalReconciliationFunction`, `OutboxDrainerFunction`, `SendEmailFunction`,
  `ExpireStaleReferralsFunction`, 6 poison consumers), not 16. T-0181 added the fanout **resume
  cursor** (AC7's "no inbound guard" framing is stale — assert the new resume/idempotent-enqueue
  behavior, already partially covered by `SendSitewidePromoFanoutResumeTests`), T-0182 made push
  dispatch durable-idempotent per **ADR-0010**, T-0183 fixed cron cadences (see
  `TimerScheduleConfigTests`), T-0184 rewrote `FiscalRetryService`. The deliverable is a **coverage
  audit table (existing suite ↔ Function ↔ branch) + gap-fill tests only.**
- **T-0215:** the ticket's "`Cleansia.IntegrationTests` does not yet exist" is **stale** — it exists;
  consume it. `Cleansia.HostTests` already has **`Ac9CrossTenantRejectionTests`** — extend the
  coverage to the four hosts and the cross-**user** (same-tenant) cases + the named write paths
  (Order lifecycle, Dispute, UserMembership, SavedAddress); do not re-prove what T-0126 already locks.
  Mind the single-tenant escape branch (`currentTenantId == null`) — both fixtures must carry
  non-null `tenant_id` claims.
- **T-0216:** `FiscalRetryService.cs` was **rewritten by T-0184** (per-receipt durability) — AC7's
  hold-release path and all line refs must be re-derived; `IFiscalService.RegisterReceiptAsync` now
  takes an **idempotency key** (T-0221) — mock signatures updated. Dedup against
  `FiscalRetryServicePerReceiptDurabilityTests`, `ReceiptServiceFiscalIdempotencyTokenTests`,
  `GenerateReceiptHandlerFiscalIdempotencyTests` — the **mode-selection matrix**
  (None/AsyncBackground/BlockingOnline × hold/send/release) is the gap this ticket owns.
- **T-0217:** `BusinessErrorMessage.cs` grew substantially in Waves 2–3 (refund.*, chargeback,
  payroll, catalog `in_use`, lockout codes…) — the EP-2 "customer subset" enumeration is stale;
  **re-derive from current source + customer-host controllers.** T-0192 (customer dispute/refund UI)
  may have already added some keys — dedup before backfilling. The AC4 parity-guard test is the
  mechanism that makes the re-derivation durable.
- **T-0218:** wizard HTML line refs may have drifted slightly — verify against current files;
  otherwise current. Sole editor of `libs/shared/components/**` + `order-wizard/**` this wave.
- **T-0179:** text verified current at Wave-3 close (file untouched since Wave 1). Two notes: B8 was
  closed by T-0147 (already out-of-scope'd), and T-0194 already rate-limited the Subscribe endpoints —
  the doc comment may mention both. Expected comment-only → no nswag-regen (confirm at review).
- **T-0235:** current (filed 2026-06-12). One interaction to design around: **account lockout
  (T-0193) now trips on repeated failed logins** — flooding a credentialed auth endpoint can hit the
  lockout before the rate limit. Use distinct users per request or pick representatives whose 429 is
  not confounded by lockout, per policy class.

---

## 4. Owner items

### 4.1 Confirms (none blocks 4A/4B; one soft-gates 4C)
1. **Confirm `Cleansia.IntegrationTests` runs green on current master.** The Users-lockout migration
   is verified **in-repo** (`20260612134125_Initial`), so the Testcontainers schema has the columns —
   this confirm formally closes **T-0193 AC4** (Wave-3 deviation #3) and de-risks Batch 4C. If the
   owner has not run the suite, the 4C developers will surface any failure on first run anyway.
2. **Customer nswag-regen (sprint-5 §8.2 #2)** — `DisputeReason.Chargeback` + device endpoints. **No
   Wave-4 ticket consumes the customer generated client**, so it does not gate this wave — but it is
   still outstanding and the generated client is stale until done.
3. **Wave numbering:** this wave = tests + a11y (owner's scope). The consistency sweep T-0196…T-0206
   moves to **Wave 5** — confirm or reorder.

### 4.2 Standing carry-forwards (unchanged, owner-tracked)
**T-0159 rotate-mapbox-token (live exposure — still outstanding)** · IMP-1 Google OAuth ClientId ·
CZ Stripe-fee figures · DE/AT/ES fiscal go-live gates + corrective document (Q-REFUND-01/ADR-0009 D7)
· Q-REFUND-03 per-bundle weights · Q-W3-4 dispute-resolve-on-refund-failure UX confirm ·
T-0236 (multi-tenant token-revoke asymmetry) **must land before any multi-tenant onboarding.**

---

## 5. Definition of "Wave 4 done"

All 11 tickets `done`: every AC has a named test (or doc/markup artifact) as evidence; reviewer
approved each (adversarial pass reconciled on T-0211/T-0210/T-0215); QA recorded suite-green + the
T-0218 keyboard walkthrough; `dotnet test` (Cleansia.Tests, Cleansia.IntegrationTests,
Cleansia.HostTests) and `npx nx test` for the touched frontend projects run green on
`feature/wave-4-tests-a11y`; T-0179's no-regen confirmation (or the flagged regen) recorded; the i18n
key sets are identical across all 5 customer locales (parity guard green); INDEX.md + this doc match
reality. PR to `master` is the owner's call.

---

## 6. Explicitly NOT in Wave 4

- **T-0196…T-0206** — consistency/quality sweep (Wave-5 candidate, §4.1 #3).
- **T-0233/T-0234** (lockout-DoS, ChangeOwnPassword bound), **T-0236…T-0241** — Wave-3 follow-ups,
  stay `draft`; sequence at Wave-5 intake (T-0236 before multi-tenant onboarding).
- **TC-11** (broad Jest interceptor/error-pipe suite) — T-0217 lands only the AC4 parity guard.
- **axe-core CI integration** — T-0218 proposes it as follow-up only.
- Fixing any defect a test surfaces — findings route to new/owning fix tickets; test tickets do not
  patch production code.

---

## Status log
- 2026-06-12 — Wave-4 plan drafted + promoted (PM). Verified master `05bf567a` (PR #76); all 11
  tickets' dependencies `done`; Users-lockout migration present in-repo; IntegrationTests/HostTests
  harnesses exist. **Resized T-0213 and T-0214 L→M** on verified dedup evidence (Wave-0…3 TDD suites
  already cover the bulk; both become audit+gap-fill nets — if either grows back past M mid-flight,
  the dev stops and the PM splits per the L rule). Grouped 3 batches: 4A unit nets + T-0179 (6 ∥,
  lanes U1/U2), 4B frontend **T-0218 → T-0217 serialized** on the 5 customer locale JSONs, 4C
  integration/host (T-0210 ∥ T-0215 on Lane I1 + T-0235). All 11 promoted `ready`; stale-text deltas
  recorded in §3 for the implementing agents. Owner confirms filed in §4.1 (none blocks 4A/4B).
