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

## 7. Batch 4A + 4B close-out (2026-06-13) — 8 of 11 done

**Status:** Batches **4A** (6 backend unit-net tickets) and **4B** (2 frontend tickets) are complete,
orchestrator-verified green, committed **`6706d8d1`** and pushed on `feature/wave-4-tests-a11y`.
**Wave-4 progress: 8 of 11 done. Remaining: Batch 4C** (T-0210 webhook integration, T-0215 cross-tenant/
cross-user write-path integration, T-0235 runtime 429 flood harness).

### 7.1 What landed
- **4A (backend, `Cleansia.Tests`):**
  - **T-0212** — green-field CreateOrder characterization suite (20 cases, validator + handler + shared
    fixture; AUD-06 has not run, so the before-refactor net is intact).
  - **T-0211** — refund/dispute money-math **gap-fill** (50 new tests, 5 files) — fee-tier boundaries,
    Plus free-window override, pure-decimal refund formula, CancelOrder wiring + refund-branch guards,
    illegal-state rejection, ResolveDispute validator, adversarial fee-rounding. Adversarial money
    review honoured; 4 production mutations applied + reverted to prove RED; zero production edits.
  - **T-0213** — invoice/numbering/pay-period **gap-fill** (54 tests, 5 files + 1 additive builder) —
    money aggregation/AssignToInvoice/clamp, validator paths, numbering shape + variable symbol,
    entity transition guards, ClosePayPeriod + PayPeriod lifecycle.
  - **T-0214** — per-Function **coverage audit + gap-fill** (audit table over 26 Function shells → 18
    Core handlers + 1 poison base mapped to the 33 existing suites; 6 new files / 18 tests for the
    uncovered branches: OutboxDrainer entry point, 3 GenerateReceipt branches, push muted-category,
    CalculateOrderPay classification, the 6 mediator sweeps' failure-else, SendEmailPoison).
  - **T-0216** — fiscal-mode selection characterization **matrix** (29 theory cases, 3 files;
    None/AsyncBackground/BlockingOnline × hold/send/release + fallback-to-None).
  - **T-0179** — membership subscribe-path doc + B5 `nameof(Command)`→`nameof(userId)` rename + contract-
    lock test (3 tests, red-first). **No nswag-regen needed** (comment + runtime-error-payload only).
- **4B (frontend, customer app):**
  - **T-0218** — a11y pass on `cleansia-button` + order wizard (16 Jest cases; div→native-button
    conversions, ARIA state, label/error association, icon-button names; 11 `aria.*` i18n keys ×5
    locales). Reviewer APPROVED (PASS-WITH-NOTES).
  - **T-0217** — error-contract parity: customer `api.*` keys backfilled across all 5 locales (identical
    key sets), parity-guard + interceptor-fallback specs, `patterns-frontend.md` AC5 doc. Serialized
    correctly behind T-0218 on the locale JSONs (disjoint `aria.*`/`api.*` subtrees).

### 7.2 Verified test counts (orchestrator)
- **`Cleansia.Tests` = 1311 / 1311 passed**, 0 failed, 0 skipped.
- **Frontend Jest green** for the touched projects (`components` 4/4, `cleansia-customer-order-wizard`
  12/12, `services` 27/27, `cleansia.app` 5/5).
- **Customer production build clean** (`nx build cleansia.app --configuration=production`; only the
  pre-existing unrelated NG8102 order-wizard warning).

### 7.3 T-0216 transient-FAIL explanation (resolved)
The T-0216 workflow returned a transient FAIL that was **two distinct, now-resolved** items:
1. **Cross-lane mid-write flicker** — the concurrent T-0212/T-0214 lanes were mid-write on
   `Features/Orders/CreateOrderHandlerCharacterizationTests.cs` (and the T-0216 lane's own
   `Functions/GenerateReceiptHandlerFiscalModeMatrixTests.cs` briefly overlapped T-0214's AC2
   BlockingOnline-hold case) while T-0216's full-project run executed, so the project transiently did
   not compile / showed 2 unrelated Orders-lane failures. These were **not** caused by T-0216 and
   cleared once the lanes converged.
2. **Comment-token must-fix** — the reviewer's single CHANGES-REQUESTED item: the three new fiscal-mode
   test files carried `TC-10` (and one `post-T-0184`) ticket-ID tokens in their class docblocks,
   violating `conventions.md` (no `// T-NNNN` in source). The dev stripped the tokens (behavioural
   prose + load-bearing ADR refs kept); comment-only, zero IL change.
**Both resolved.** T-0216's 29 fiscal-mode cases pass and the orchestrator confirmed the new test files
across all 4A lanes carry no TC-/T-NNNN tokens. T-0216 marked `done`.

### 7.4 The 3 follow-ups filed (carried production findings — Wave-5 candidates, all `draft`)
The characterization tests uncovered three real production findings that the test-only wave (correctly)
did **not** fix. Each is now a proper ticket:
- **T-0242** (from T-0211, backend, S, money-semantics) — `BookingPolicy.CalculateCancellationFeeRate`
  treats `freeCancellationHoursOverride` as a literal replacement, so the **larger** override the Plus
  path passes makes the free-cancellation window **stricter**, contradicting the "Plus = more generous"
  doc. AC: confirm intended direction with the owner, then either pass a smaller override on the Plus
  path or invert the override semantics in `BookingPolicy`, and update T-0211's
  `CancellationFeeRateBoundaryTests` (which currently PIN the literal-replacement behavior) to the
  corrected intent.
- **T-0243** (from T-0179, backend, XS) — `CreateMembershipCheckoutSession.cs` (~line 45) builds its
  `UserNotFound` failure with `nameof(Command)` instead of `nameof(userId)` — the same B5 smell T-0179
  fixed in the sibling, explicitly scoped out there. Mechanical rename; consistency debt, not a runtime
  defect.
- **T-0244** (from T-0213, backend, S) — `EmployeeInvoice.GenerateVariableSymbol` relies on
  per-process-randomized `string.GetHashCode()`; no live bug today (computed once, not recomputed
  cross-process), but a persist-then-recompute-in-another-process path would silently mismatch a
  fiscal/payment reference. AC: replace with a deterministic stable hash (or persist-and-never-recompute)
  + a cross-invocation determinism test (T-0213 pins only within-run determinism today).

### 7.5 Carried production findings list (the §6 "fixing any defect a test surfaces" rule in action)
| Finding | Surfaced by | Severity | Filed as |
|---|---|---|---|
| Cancellation-fee Plus free-window override direction contradicts doc | T-0211 (TC-7) | money-semantics (product decision) | **T-0242** |
| `CreateMembershipCheckoutSession` `nameof(Command)` B5 smell | T-0179 (LG-07) | consistency debt (no runtime defect) | **T-0243** |
| `GenerateVariableSymbol` per-process `GetHashCode` cross-process trap | T-0213 (TC-6) | latent fiscal-reference correctness | **T-0244** |

All three are `draft`, `sprint: 5` (Wave-5 candidates), `owner: —`, and depend on their done source
ticket. None blocks Batch 4C.

---

## 8. Batch 4C close-out (2026-06-13) — 11 of 11 done · WAVE 4 COMPLETE

**Status:** Batch **4C** (the integration + host-runtime test slice) is complete and
orchestrator-verified green against **real Postgres** (Testcontainers). With 4A+4B (§7) already landed,
**Wave 4 is COMPLETE: 11 of 11 tickets `done`.**

### 8.1 What landed
- **T-0210** (TC-2/3 — Stripe order + subscription webhook integration + signature-stays-on lock,
  backend) — 15 integration tests in 3 new files under
  `src/Cleansia.IntegrationTests/Features/Payments/Webhooks/` (order-webhook idempotency at the durable-
  outbox seam, subscription active-membership + filtered-unique-index backstop, missing/forged-signature
  rejection on both routes, the no-env-bypass structural + behavioral lock, happy paths). NO production
  code edits. Reviewer CHANGES-REQUESTED (comment-discipline must-fix — strip `T-NNNN` from sources)
  resolved; Security PASS-WITH-NOTES (two test-strength notes, non-blocking).
- **T-0215** (TC-9 — authz / cross-tenant + cross-user write-path integration, backend) — 13 tests
  across 4 new files extending the `Cleansia.HostTests` `Ac*` family (SavedAddress cross-user/cross-tenant,
  Dispute add-message cross-tenant, Membership cancel cross-tenant, Order Take/Start on the 4th — Mobile
  partner — host). Additive `HttpAssert`/`DomainSeed` infra only; **zero production source edits**.
  Reviewer + Security both PASS-WITH-NOTES (coverage-attribution / degenerate-case observations,
  non-blocking; isolation locked both directions, non-vacuous).
- **T-0235** (runtime 429 flood harness — the T-0194 AC6 deviation, backend) — 3 cases over the existing
  runtime harness `src/Cleansia.Tests/RateLimiting/Harness/RateLimiterHostHarness.cs` (auth-anonymous +
  remediation path, auth-authenticated per-sub, webhook per-source-IP), each flooding past its window to
  runtime 429 + `Retry-After` with an under-window control. RED (right-reason) proven by temporarily
  dropping the remediation route's `RequireRateLimiting("auth")`. Additive non-breaking `extraEndpoints`
  harness hook; no change to policies/attributes/`CleansiaStartupBase.cs`.

### 8.2 Verified test counts (orchestrator, clean runs vs real Postgres)
- **`Cleansia.HostTests` = 51 / 51 passed.**
- **`Cleansia.IntegrationTests` = 60 / 60 passed.**
- **`Cleansia.Tests` RateLimiting = 65 / 65 passed** (62 prior + 3 new).

### 8.3 T-0235 AC3 home divergence (accepted)
AC3 named `Cleansia.HostTests` as the test home, but that project does not exercise the **runtime**
rate-limiter; the runtime limiter is exercisable in `Cleansia.Tests/RateLimiting` via the existing
TestServer harness (`RateLimiterHostHarness`, already the home of `WebhookRateLimitTests` /
`RateLimiterHostBehaviorTests`). The tests correctly live there; the AC3 **intent** (runtime proof, not
`BaseIntegrationTest`, green in CI, per-policy-class mapping) is fully satisfied. Deviation D1 stands.

### 8.4 The 2 confirmed production bugs filed (4C carried findings — test-only wave, correctly NOT fixed)
| Bug | Surfaced/verified by | Severity | Filed as |
|---|---|---|---|
| Multi-tenant Stripe webhook validator/handler tenant-scope mismatch — order-exists VALIDATOR (`BaseRepository.ExistsAsync`) tenant-scoped vs handler read (`GetByIdIgnoringTenantAsync`) tenant-ignoring → a non-null-tenant paid `checkout.session.completed` fails validation and the order is never confirmed/paid (silent money/lifecycle failure; masked today because web Checkout is single-tenant) | T-0210 (TC-2/3) review + Security; the suite seeds single-tenant to dodge it and documents the gap | ⚠️ **MULTI-TENANT GO-LIVE BLOCKER** (M, `security_touching`) — sibling of T-0236 | **T-0245** |
| StartOrder handler NRE→500 — `StartOrder.cs:137` `order!.StartOrder()` derefs an unguarded Include-shaped load while the validator (`:45`) gated existence via a different query path (`ExistsAsync`); divergence → 500 instead of clean business not-found | T-0215 (TC-9) Ac14, reproduced live on the Mobile partner host with tenant-consistent seed data | latent 500-vs-not-found robustness (S) | **T-0246** |

Both are `draft`, `sprint: 5` (Wave-5 candidates), `owner: —`, depend on their done source ticket.
**T-0245 must land before any multi-tenant onboarding** (alongside T-0236). Cross-linked: T-0245 ↔
memory `tenant-ignoring-read-on-webhook-paths.md` + T-0236; T-0246 ↔ T-0215 Ac14.

### 8.5 Wave-4 overall summary
- **11 tickets `done`** across 3 batches: 4A (T-0212/T-0211/T-0213/T-0214/T-0216/T-0179),
  4B (T-0218/T-0217), 4C (T-0210/T-0215/T-0235). Zero production-source edits in the wave (tests /
  i18n / a11y / doc / comment-only renames against existing behavior).
- **Orchestrator-verified green:** `Cleansia.Tests` 1311/1311 (4A) + frontend Jest + customer prod
  build (4B); `Cleansia.HostTests` 51/51 + `Cleansia.IntegrationTests` 60/60 + RateLimiting 65/65 (4C,
  real Postgres).
- **5 carried production findings filed** as new `draft` tickets — **T-0242** (cancellation-fee override
  direction, from T-0211), **T-0243** (CheckoutSession `nameof` B5, from T-0179), **T-0244** (variable-
  symbol stable hash, from T-0213), **T-0245** (multi-tenant webhook tenant-scope mismatch — go-live
  blocker, from T-0210), **T-0246** (StartOrder NRE→500, from T-0215). None blocked Wave 4; all are
  Wave-5 candidates.
- **PR to `master` is the owner's call** (PM does not merge). Branch `feature/wave-4-tests-a11y`.

### 8.6 WAVE 4 CLOSED
**Wave 4 (tests + a11y) CLOSED 2026-06-13 — 11/11 done; follow-ups T-0242–T-0246 filed (`draft`,
Wave-5 candidates; T-0245 + T-0236 = multi-tenant onboarding blockers).** No code/commits/branch ops
by the PM in this close-out (backlog bookkeeping only).

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
- 2026-06-13 — **Batch 4A+4B closed (PM bookkeeping)**. 8 tickets set `done`
  (T-0212/T-0211/T-0213/T-0214/T-0216/T-0179 + T-0218/T-0217) — orchestrator-verified green
  (`Cleansia.Tests` 1311/1311, frontend Jest green, customer prod build clean), committed `6706d8d1`
  + pushed. Each ticket carries an `updated: 2026-06-13` bump and a final `done` status-log line. The
  T-0216 transient FAIL (cross-lane mid-write flicker + comment-token must-fix) explained + recorded
  as resolved (§7.3). Three carried production findings filed as new `draft` tickets: **T-0242**
  (cancellation-fee override direction, from T-0211), **T-0243** (CheckoutSession `nameof` B5, from
  T-0179), **T-0244** (variable-symbol stable hash, from T-0213) — §7.4/§7.5. INDEX.md updated (8
  `done`, 3 new drafts, Wave-4 progress 8/11; 4C = T-0210/T-0215/T-0235 remaining). Wave-4 close-out
  subsection added (§7). No code/commits/branch ops by the PM (bookkeeping only).
- 2026-06-13 — **Batch 4C closed → WAVE 4 COMPLETE (PM bookkeeping)**. 3 tickets set `done`
  (T-0210/T-0215/T-0235), each with `updated: 2026-06-13` + a final `done` status-log line —
  orchestrator-verified green vs real Postgres (`Cleansia.HostTests` 51/51, `Cleansia.IntegrationTests`
  60/60, `Cleansia.Tests` RateLimiting 65/65). T-0235's AC3 home divergence (named HostTests; tests live
  in `Cleansia.Tests/RateLimiting` where the runtime limiter is exercisable) noted + accepted (§8.3).
  **2 confirmed production bugs filed** as new `draft` tickets — **T-0245** (multi-tenant Stripe webhook
  validator/handler tenant-scope mismatch — **MULTI-TENANT GO-LIVE BLOCKER**, `security_touching`, from
  T-0210) + **T-0246** (StartOrder handler NRE→500 on validator/handler load divergence, from T-0215).
  §8 close-out added (4C + Wave-4 summary + WAVE 4 CLOSED line). INDEX.md: 4C roster rows → `done ✅`,
  Wave-4 banner → "✅ WAVE 4 COMPLETE — 11 of 11 done", T-0245/T-0246 added to the Wave-4 follow-up table
  (T-0245 flagged go-live blocker). **Wave 4 = 11/11 done; follow-ups T-0242–T-0246 filed.** No
  code/commits/branch ops by the PM (bookkeeping only).
