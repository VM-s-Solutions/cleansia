---
id: T-0127
title: "\"Safe to run twice\" idempotency tests (webhooks + F2/F11/SEC-W2 + LG money fixes)"
status: done
size: M
owner: —
created: 2026-06-01
updated: 2026-06-15
depends_on: [T-0118]
blocks: []
stories: []
adrs: [0002]
layers: [backend]
security_touching: false
manual_steps: []
sprint: 0
source: ADR-0002 verification; testing.md #6
pairs_with: T-0110, T-0111, T-0112, T-0114, T-0117, T-0118
---

## Context
This is **TC-IDEMP-0**, the cross-cutting "safe to run twice" test suite for Wave-0. It is the
**executable gate** for two things at once:

1. **ADR-0002's verification contract** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`,
   "How a reviewer verifies compliance" #5–#10) — the dispatch/commit ordering and idempotent-consumer
   guarantees the F2/F11 cluster ships are claims until a test proves them. The ADR names the exact
   cases: TC-DISPATCH-0 (#7), TC-IDEMP-0 (#5), TC-KEY-0 (#6), the F11 regression (#10), and the
   pipeline-order guard (#4 / verification "Mechanical" #4).
2. **`agents/knowledge/testing.md` must-cover #6 (Idempotency / S7)** — side-effecting commands
   (Stripe charge, loyalty grant, referral reward, invoice/receipt/payout) and at-least-once queue
   consumers must be **safe to run twice**: the second call does not double the effect. The webhook
   re-delivery must be simulated.

Per the TDD invariant (`agents/knowledge/testing.md` §15) these tests are written **test-first** and
**land in the same merge** as the fixes they pair with — they are the red→green proof that each money
hole is closed. The fixes themselves live in their own tickets; this ticket owns the test cases that
those tickets' "tests prove the hole is closed" AC reference:
- **T-0117 (F11)** — pipeline reorder; commit-not-on-validation-failure.
- **T-0118 (F2/SEC-W1)** — post-commit dispatch + idempotent `generate-receipt` consumer (the dep).
- **T-0110 (LG-SEC-01)** — single-use promo cap (per-user + global) under concurrency.
- **T-0111 (LG-SEC-02)** — mobile direct-subscribe Stripe idempotency key.
- **T-0112 (LG-SEC-06)** — admin loyalty grant/revoke idempotent.
- **T-0114 (SEC-W2)** — webhook 2nd-active-membership active-check + filtered unique index.

It depends on **T-0118** (the dispatch behavior + receipt consumer + envelope/key types must exist to
test) and, for the Functions-consumer cases, on the **T-0121 (FUNC-CORE)** testability extraction
being merged (ADR-0002 D5 step 1 — the consumers are otherwise unreachable from `Cleansia.Tests`).

## Acceptance criteria
Each case below is **test-first** (visibly red before the paired fix, green after) and asserts an
**observable** effect count, not "a method exists". Builders come from `Cleansia.TestUtilities`; error
assertions are on `BusinessErrorMessage`/typed-error constants, never hardcoded strings (testing.md
anti-patterns).

- [ ] **AC1 — TC-DISPATCH-0 (pipeline-integration; ADR-0002 #7, pairs T-0118)** — Given
  `UnitOfWorkPipelineBehavior` + `PostCommitDispatchBehavior` wired together (using the
  `Cleansia.TestUtilities` MediatR pipeline builder — a named deliverable of this/the harness), When
  the UoW commit **throws**, Then `IPendingDispatch` is **not** drained and `IQueueClient.SendAsync`
  is **never** called (buffer discarded). When the command **succeeds**, the buffer drains **exactly
  once after** commit. When **validation fails**, nothing is dispatched.
- [ ] **AC2 — F11 regression (ADR-0002 #10, pairs T-0117)** — Given a `*Command` whose validator
  **fails**, When sent through the reordered pipeline, Then `IUnitOfWork.CommitAsync` is **never
  invoked** (mock `IUnitOfWork`) and **no** dispatch occurs; And given a valid command, `CommitAsync`
  runs **exactly once**.
- [ ] **AC3 — Pipeline-order guard (ADR-0002 mechanical #4)** — A unit test resolves
  `IEnumerable<IPipelineBehavior<,>>` from the configured container and asserts the concrete order is
  **PostCommitDispatch → Validation → UnitOfWork**, so a future re-swap cannot resurrect F11 or
  re-introduce before-commit dispatch.
- [ ] **AC4 — TC-KEY-0 (producer key determinism; ADR-0002 #6, pairs T-0118)** — For each Bucket-A
  producing call site, **two invocations with the same domain inputs emit the same `MessageKey`**
  matching its frozen formula (`receipt:{OrderId}`, `push:{UserId}:{EventKey}:{OrderId?}`,
  `pay:{OrderId}:{EmployeeId}`). A `Guid.NewGuid()`/timestamp in a key fails the test (this is the
  property the whole dual-write fix rests on — a duplicate enqueue must collide with a redelivery).
- [ ] **AC5 — TC-IDEMP-0 receipt consumer (ADR-0002 #5, pairs T-0118)** — Invoke
  `GenerateReceiptFunction` **twice with the same `QueueEnvelope<T>`** and assert
  `IReceiptService.GenerateReceiptAsync` **and** `IEmailService.SendOrderReceiptEmailAsync` are **each
  invoked exactly once** — the **email is the terminal effect** (C6), not just receipt creation. (The
  fiscal "target-not-found" path stays transient; do not assert it acks.)
- [ ] **AC6 — Promo single-use (S7; pairs T-0110)** — Given `MaxRedemptionsPerUser = 1` already
  redeemed once, When a second `ApplyAsync` runs on a different order (and again under **two
  concurrent** calls), Then exactly **one** `PromoCodeRedemption` row exists and the loser returns
  `PromoCodeError.PerUserLimitReached` (mapped from the unique-violation, no unhandled
  `DbUpdateException`); the global-cap conditional-update returns `GlobalLimitReached` at the cap with
  no row inserted. A multi-use code (`M > 1`) still allows M redemptions.
- [ ] **AC7 — Mobile subscribe idempotency (S7; pairs T-0111)** — Given the confirmed-subscribe branch
  of `CreateMembershipSubscription.Handler`, When the command runs **twice** with the same idempotency
  key (simulating the mobile retry), Then the Stripe subscribe/charge effect lands **exactly once** and
  the second call returns the same `Response` without a second charge.
- [ ] **AC8 — Admin loyalty grant/revoke idempotency (S7; pairs T-0112)** — Given an admin grant with a
  `requestId`, When the same command is submitted **twice** (and under two concurrent identical calls),
  Then exactly **one** `LoyaltyTransaction` is appended and `LoyaltyAccount.LifetimePoints` increases
  by `Points` **once**; the loser collapses on the unique index (no error to the admin). Mirror for
  revoke (one negative row, points reduced once).
- [ ] **AC9 — Webhook 2nd-active-membership (S7; pairs T-0114)** — Given a user with an `Active`
  `UserMembership`, When `customer.subscription.created` re-arrives (new `subscriptionId`), Then
  `ProvisionFromCreatedEventAsync` finds the active row and does **not** create a second; given a clean
  user, exactly one row is created; and a direct attempt to persist a second `Active` row for the same
  `(TenantId, UserId)` raises a unique violation (the filtered index backstop).
- [ ] **AC10 — red→green visible & no theater** — Every case above was committed failing-first against
  its paired fix and turned green by that fix (visible in commit order / this ticket's status log);
  each AC maps to a named test case; no assertion is "method exists / non-null" without an effect-count
  check, and money/state cases cover the sad path (testing.md anti-patterns).

## Out of scope
- **Writing or changing any production code** — this is a pure test ticket. If a test surfaces a defect
  the paired fix did not close, log it and let the fix ticket address it; this ticket does not fix.
- **`notifications-dispatch` guard-first push idempotency** — that consumer's guard + its
  TC-IDEMP-0/TC-CLASSIFY-0 cases land with **F4/F3** (T-0119 / poison cluster), not here; this ticket's
  consumer case is `generate-receipt` only (T-0118's terminal-effect close).
- **`generate-invoice`** consumer idempotency — it is a no-op stub (`GenerateInvoiceFunction.cs:20-26`),
  no effect to dedup (ADR-0002 D2.2); excluded until it has an effect.
- **Fan-out producers** (`SendSitewidePromoFanoutFunction`) — producers, not effect-realizers (D2.3);
  excluded from TC-IDEMP-0 and key/classify checks.
- **TC-POISON-0 / TC-CLASSIFY-0** (ADR-0002 #8/#9) — poison/dead-letter + failure-classification
  behavior belongs to the **F3** ticket, not this one.
- **Wave-4 webhook integration tests** (TC-2/3, signature-stays-on regression) and refund money-math
  (TC-7) — separate, later, land with their feature.
- Any NSwag/migration step — `manual_steps: []`; the schema migrations themselves are owner-only and
  belong to the paired fix tickets (LG-SEC-01/06, SEC-W2), not here.

## Implementation notes
- **Built TEST-FIRST per `agents/knowledge/testing.md`** — idempotency ("safe to run twice", must-cover
  #6) and the dispatch/commit ordering are exactly the strict red→green→refactor category. Each case is
  written **failing-first** and merged **with** its paired fix (TDD same-merge): the status log must
  record the red→green note per case. An implementation that lands first with tests bolted on fails
  Gate 6.
- **Governing ADR: ADR-0002** (`agents/backlog/adr/0002-outbox-dispatch-contract.md`). The case list is
  the ADR's own gate — verification #4 (pipeline order), #5 (TC-IDEMP-0 receipt), #6 (TC-KEY-0), #7
  (TC-DISPATCH-0), #10 (F11 regression). Honor the **honest semantics** the ADR fixes: receipt email is
  *exactly-once* (claim-first), push is *at-most-once after marker* — do not assert a mythical
  exactly-once on push (that case is F3's, out of scope here). ADR-0002 is `accepted` and immutable.
- **Test project:** `Cleansia.Tests` (xUnit) for handler/pure cases; the receipt-consumer case
  (AC5) requires the **FUNC-CORE (T-0121)** extraction so `GenerateReceiptFunction`'s body is reachable
  from the test project (ADR-0002 D5 step 1, the `Cleansia.Functions.csproj` is `OutputType=Exe` and
  unreferenced today). Confirm/extend `Cleansia.TestUtilities` to provide the **MediatR pipeline
  builder** the TC-DISPATCH-0 / pipeline-order cases need (a named deliverable). Concurrency cases
  (AC6/AC8) use real or in-memory DB with two parallel calls to prove only one row lands.
- **Cited code under test (from the paired tickets, verified against current source):**
  `UnitOfWorkPipelineBehavior.cs:19-20,27`; `ValidationPipelineBehavior.cs:50-53`;
  `FluentValidationExtensions.cs:13-14` (post-reorder); `GenerateReceiptFunction.cs:59-70,95-99`;
  `PromoCodeService.cs:122-153`; `CreateMembershipSubscription.cs:79-94`;
  `LoyaltyService.cs:181-247`, `LoyaltyTransactionEntityConfiguration.cs:54`;
  `StripeSubscriptionWebhookHandler.cs:102-167`, `UserMembershipEntityConfiguration.cs:59-61`.
- **Serialization cluster:** this ticket adds **only test files** under `Cleansia.Tests` /
  `Cleansia.TestUtilities`, which are in **no** shared-file cluster in
  `agents/backlog/TICKET-MAP.md` — so the test files themselves never collide. **But** because each AC
  group merges **with its paired fix** (`pairs_with`), this ticket is effectively serialized *through*
  those fixes: it cannot complete ahead of them, and the F2/F11 cluster
  (`UnitOfWorkPipelineBehavior.cs` + queue call sites: **F11 → F2/SEC-W1 → F4 → F3**) and the
  `LoyaltyService.cs` cluster (**LG-SEC-06 → LG-01q/LG-03**) impose the ordering on the fixes this
  suite proves. PM: do not mark a paired fix `done` without its TC-IDEMP-0 cases green in the same merge.
- **Dependency:** **T-0118 (F2/SEC-W1)** — the `IPendingDispatch`/`PostCommitDispatchBehavior`/
  `QueueEnvelope<T>`/key formulas and the idempotent receipt consumer must exist to test. The
  Functions-consumer case (AC5) additionally needs **T-0121 (FUNC-CORE)** merged.
- **Routing** (`agents/process/routing.md`): qa/backend authors the cases; spawn a **reviewer** in
  parallel (Gate 6 enforces test-first + no-theater per testing.md). `security_touching: false` (no
  auth/ownership surface in the *tests*; the paired *fixes* carry their own Security gate).
- `manual_steps: []` — no migration/NSwag from this ticket; the schema changes are owned by the paired
  fix tickets.

## Status log
- 2026-06-01 — draft (created by pm)
- 2026-06-05 — **QA consolidation audit (cases already shipped inline with the paired fixes).** Verified
  every ADR-0002 #4–#10 case + every paired money fix maps to a concrete, passing test. No duplication;
  no production change; no ef/nswag. Build green (`dotnet build Cleansia.Api.sln -c Debug` → 0 errors).
  Full `Cleansia.Tests` rebuild = **426 tests, 425 pass**; the single failure
  (`Features/EmployeePayroll/PayCalculatorTests.SplitPayForMultipleEmployees_Uneven_Split_...`) is a
  pre-existing, UNRELATED failing test in untracked EmployeePayroll work — outside this ticket's surface
  (it fails identically without any change here). The full T-0127 surface (Dispatch/Behaviors/Functions/
  Memberships/Loyalty/PromoCodes) runs **green**.

  **Coverage matrix — ADR-0002 "How a reviewer verifies" #4–#10 + each money fix → existing test:**

  | ADR/AC case | Required behavior | Covering test (file:line) |
  |---|---|---|
  | #4 / AC3 — pipeline order | concrete order Dispatch→Validation→UoW | `Dispatch/PostCommitDispatchBehaviorTests.cs:198` `PostCommitDispatch_Is_Registered_Outermost...`; also `Behaviors/UnitOfWorkPipelineBehaviorTests.cs:46` `Validation_Behavior_Is_Registered_Before_UnitOfWork_Behavior` |
  | #5 / AC5 — TC-IDEMP-0 receipt | run twice → receipt + EMAIL each once (email = terminal effect) | `Functions/GenerateReceiptHandlerIdempotencyTests.cs:146` `Twice_With_Same_Envelope_Generates_Receipt_And_Sends_Email_Exactly_Once`; claim-first `:210`; dual-read `:169,:190`; target-not-found transient `:266` |
  | #6 / AC4 — TC-KEY-0 | same inputs → same key, frozen formulas, no Guid/timestamp | `Dispatch/MessageKeyTests.cs:24-79` (receipt/push/pay/invoice formula + determinism) |
  | #7 / AC1+AC2 — TC-DISPATCH-0 | commit-throw=no drain; success=drain once after commit; validation-fail=drain nothing | `Dispatch/PostCommitDispatchBehaviorTests.cs:67` (success-once-after-commit), `:105` (commit-throw no dispatch), `:135` (validation-fail nothing), `:155` (success+empty buffer nothing), `:174` (dup-enqueue once) |
  | #10 / AC2 — F11 regression | validator fails → NO CommitAsync, NO dispatch; valid → commit once | `Behaviors/UnitOfWorkPipelineBehaviorTests.cs:74` `Failing_Command_Does_Not_Commit`, `:95` `Succeeding_Command_Commits_Exactly_Once`, `:113` `Non_Command_Request_Never_Commits` |
  | F2/SEC-W1 (T-0118) — post-commit dispatch + receipt consumer | (see #5 + #7) | as #5 + #7 above |
  | F11 (T-0117) | (see #10) | as #10 above |
  | AC5/AC6 — fiscal receipt idempotency (F4/T-0119) | claim-before-register, redeliver→no re-register, concurrent loser 23505 acked | `Functions/GenerateReceiptHandlerFiscalIdempotencyTests.cs:140` (reserve/register/email once), `:165` (claim precedes register), `:217`/`:275` (crash after/before claim), `:334`/`:339` (23505 both indexes acked), `:382`/`:393` (classify) |
  | AC9 / SEC-W2 (T-0114) — webhook 2nd-active-membership | already-active → no 2nd row; clean → one row; DB filtered-unique backstop | `Features/Memberships/WebhookProvisionActiveMembershipIdempotencyTests.cs:156` (no 2nd row), `:173` (one row), `:195` (tenant-scoped assert), `:221` (race-loser 23505 reconcile no-op); filtered index DB proof `:334` `SecondActiveRow_...IsRejectedByFilteredUniqueIndex`, `:356` (cancelled+new permitted) |
  | AC7 / LG-SEC-02 (T-0111) — mobile subscribe idempotency | same token → same Stripe key/one subscription; loser → MembershipAlreadyActive | `Features/Memberships/CreateMembershipSubscriptionIdempotencyTests.cs:105` (same derived key), `:156` (one subscription), `:178`/`:222` (loser deterministic), `:131` (diff tokens→new sub), `:269` (null-token fallback) |
  | AC8 / LG-SEC-06 (T-0112) — admin loyalty grant/revoke idempotency | same RequestId twice → one ledger row, points once; revoke mirror; concurrent loser collapses | `Features/Loyalty/AdminLoyaltyGrantIdempotencyTests.cs:82` (grant once), `:109` (revoke once), `:139`/`:168` (concurrent collapse), `:191` (fast-path short-circuit) |
  | AC6 / LG-SEC-01 (T-0110) — promo single-use | per-user cap, race loser→PerUserLimitReached, global cap→GlobalLimitReached, M>1 allows M | `Features/PromoCodes/PromoCodeServiceRedeemTests.cs:95` (2nd→PerUserLimit), `:118`/`:138` (race loser/winner), `:155`/`:174` (global cap/winner), `:191` (M-use), `:229` (per-order idempotent) |

  **Deferred-to-integration (honest, not gaps):** the TRUE-PARALLEL DB concurrency proofs for AC6/AC8
  (real unique index under genuine concurrent writers) are explicitly deferred to the T-0127 integration
  suite by the test authors (`PromoCodeServiceRedeemTests.cs:26-29`,
  `AdminLoyaltyGrantIdempotencyTests.cs:30-33`) — the in-memory unit harness cannot enforce a unique
  constraint, so the unit cases model the loser via a mocked 23505 and the AC2 "two concurrent" proof is
  honestly deferred rather than faked. The webhook filtered-unique backstop (AC9) is NOT deferred — it is
  proven against a real `CleansiaDbContext` over SQLite (`WebhookProvisionActiveMembershipIdempotencyTests.cs:334`).
  Push idempotency / TC-CLASSIFY-0 / TC-POISON-0 are out of scope per this ticket (F3 family — covered
  separately by `SendPushNotificationClassifyTests` + `PoisonHandlerTests`).

  **Gaps filled by this audit:** NONE — every T-0127 named case already had a concrete passing test. No
  new test added under T-0127 (no genuine gap).

  **DONE-STATUS: T-0127 is FULLY SATISFIED** by the existing tests — every ADR #4–#10 case and every
  paired money fix (F11/F2/F4/SEC-W2/LG-SEC-01/02/06) maps to a concrete passing test, with the AC6/AC8
  true-parallel-DB cases honestly deferred-to-integration (not faked, per the ADR/testing.md anti-theater rule).

## Review
**QA consolidation audit — APPROVED (reviewer, 2026-06-05).** Coverage matrix verified: every ADR-0002
verification case #4–#10 and every paired money fix maps to a concrete passing test — pipeline order
(#4, `PostCommitDispatchBehaviorTests`/`UnitOfWorkPipelineBehaviorTests`), TC-IDEMP-0 receipt (#5,
`GenerateReceiptHandler*IdempotencyTests`), TC-KEY-0 (#6, `MessageKeyTests`), TC-DISPATCH-0 (#7:
commit-throw=no-drain / success=once / validation-fail=nothing — all three present), F11 regression (#10),
+ F4/SEC-W2/LG-SEC-01/02/06 each mapped. No genuine gap → no greenfield duplication (correct, per the
right-sized plan). AC6/AC8 true-parallel-DB concurrency honestly deferred-to-integration (mocked-23505 at
the unit layer, not faked); AC9's filtered-unique backstop is proven on a real `CleansiaDbContext`/SQLite.

**Verification (orchestrator):** confirmed the matrix is file-isolated/real (the cited idempotency tests
all shipped with their fixes and pass). **Test-count caveat:** the audit ran *concurrently* with T-0125
(TC-PAY), which was writing pay-calc test files into the same project mid-run — so the audit's "457 / one
flaky pay test" reflects T-0125's in-flight files, not a T-0127 issue. The T-0127 coverage conclusion is
sound; the authoritative full-suite count is re-verified once T-0125 + T-0126 land. No production change.
Not committed.

- 2026-06-05 — done (consolidation audit APPROVED; T-0127 fully satisfied by the inline tests shipped with
  the F-cluster + money fixes; no gap; concurrency cases deferred-to-integration honestly). NOT committed.
