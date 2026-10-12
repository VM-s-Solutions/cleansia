---
id: T-0844
title: Stop a cancel overwriting a payment or returning credit on an order it failed to end, and let each sweep item and timer command fail alone (BE-11 to BE-14)
size: L
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0814]
blocks: [T-0841, T-0843]
stories: []
adrs: [0038, 0067]
layers: [backend, db, docs]
security_touching: true
---

## Context

The post-Wave E ground truth found four defects in how the order sweeps and cancels fail (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, lane F-backend, ids BE-11 to BE-14). T-0814 listed them for Mike instead of fixing them. A design ruling of 2026-10-12 settled one long-term fix for all four, and a security review checked it (off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/`, files `architect_be-lead.json` and `security_be.json`). **Mike: "Go with recommended long-term fix" (2026-10-12).**

Lines are as of `76e1638a3`; none moved since `b405e9cc5`, where the ruling read them.

- **BE-11: a cancel overwrites a payment.** `CurrentStatus` is the order's only concurrency token (`OrderEntityConfiguration.cs:252-254`; snapshot `:4211-4212`). `PaymentStatus` is only indexed (`:245`). So a writer that decided on a `PaymentStatus` it read earlier overwrites a `Paid` committed since then:
  - the stale-checkout sweep: it reads at `CleanupStalePendingOrders.cs:89-99`, writes at `:137-149` and commits at `:167`;
  - the recurring auto-cancel: it reads at `AutoCancelStaleRecurringOrders.cs:74-81` and commits at `:146`;
  - the request-path cancels: `CustomerOrderCancellation.cs:45`, `:97-99`; `PlatformOrderCancellation.cs:46-53`, `:68-78`.

  The order ends cancelled and paid, with no refund. The webhook escalates only when it commits second (`HandlePaymentNotification.cs:365-375`). This is rare, because the payment has to land inside the writer's window between read and commit.
- **The same token would also refuse a refund settle.** If another settle of the same order commits inside a settle's window, that settle's commit is refused after Stripe has already paid (`RefundService.cs:345-352`, then `:374-381`). So the settle has to re-derive its status before the token can land. Today the two overlapping settles instead write a stale status.
- **BE-12: credit is handed back outside the commit that ends the order.** The sweep returns credit (`:151-156`) before its group commit (`:167`). `TryReturnAsync` commits on its own when no transaction is open (`CreditAccountRepository.cs:107-110`, `:154-155`). So a throw at `:167` leaves the credit returned on an order that is still Pending. Six more callers have the same shape.
- **BE-13: one auto-cancel failure rides the next commit.** The per-order catch (`AutoCancelStaleRecurringOrders.cs:149-154`) neither rolls back nor detaches. A failed `CommitAsync` leaves the tracked entries as they were (`CleansiaDbContext.cs:84-120`). So the failed order's leftovers ride the next order's commit, and a fault that repeats fails every later commit in the run. `MaterializeRecurringBookingTemplate.cs:26-40` calls this shape unsafe.
- **BE-14: "sent separately" is not true.** `CleanupStalePendingOrdersHandler.cs:15-16` says each of its three commands is sent separately "so a failure in one does not suppress the others". But the three `Send` calls (`:28`, `:46-47`, `:66`) share one scope and have no try. When the sweep throws, `CancelUnfilledOrders` (the only sweep that refunds paid orders nobody took) waits 15 minutes. `AutoCancelStaleRecurringOrdersHandler.cs:28`, `:45` has the same shape, and its second command, `RedrivePendingRefunds`, moves money.

## Doing

The steps land in the ruling's order, and each is green on its own. The token lands last. Before steps 2 and 4, a refused commit would hand back credit on an order that stays paid, and a refund settle would fail after Stripe has paid.

1. **Each timer command runs in its own scope (BE-14).**
   - Both timer handlers take `IServiceScopeFactory`, and each command is sent from its own `CreateAsyncScope()`.
   - An `OperationCanceledException` while the host stops is rethrown at once.
   - Any other exception is logged at Warning, naming the command, and the next command still runs.
   - After the last command, the handler rethrows: the exception itself, or an `AggregateException` if several commands threw. So the invocation still fails, and `FunctionInvocationErrorMiddleware` logs it at Error.
   - A failure *result* is logged as today and never thrown.
   - No shared type is added.
   - A source guard in the new `TimerCommandIsolationTests`: a handler under `src/Cleansia.Functions.Core/Handlers` with two or more `Send(` calls must use `CreateAsyncScope`.
2. **The credit returned when an order ends unpaid joins the commit that ends the order (BE-12).**
   - `CreditUnwind.ReturnUnpaidOrderCreditAsync` (`CreditUnwind.cs:128-152`) takes the customer's owner lock through `ICreditAccountRepository.LockForUserAsync` before it returns anything. It does so only for an order with credit applied and a `UserId`.
   - That opens the context's lock transaction (`CleansiaDbContext.cs:295-305`). `TryReturnAsync` joins it, and `CommitAsync` commits it or rolls it back whole (`:108-119`).
   - This one change covers all seven callers: the sweep (`:155`), the auto-cancel (`:119`), `CustomerOrderCancellation.cs:116`, `PlatformOrderCancellation.cs:73`, `CleanerNoShowCancellation.cs:78`, `HandlePaymentNotification.cs:459` and `AdminCancelOrderAsLockout.cs:114`.
   - `CreateOrder`'s compensation stays self-committing.
   - The sweep processes each company's orders by `UserId` (ordinal), then by `Id`. A unit that holds several owners' locks then takes them in one order.
   - The comments this change makes false are rewritten, and the sweep's commit gets the comment "No catch: a refused group ends the run and its scope."
3. **Each recurring occurrence is cancelled in its own scope (BE-13).**
   - New command `AutoCancelRecurringOccurrence`: `Command(string OrderId, DateTime CutoffUtc)`, `Response(bool Cancelled)`. Its validator requires the id and caps `CutoffUtc` at 24 h ahead.
   - Its handler re-reads the order without the tenant filter, and re-checks `AwaitsCustomerConfirmation`, the cutoff and a `UserId`. It takes the tenant from the order and runs today's per-order body.
   - The handler has no catch and calls no `CommitAsync`: the unit-of-work pipeline commits it in the same scope.
   - `AutoCancelStaleRecurringOrders` becomes a dispatcher, as `MaterializeRecurringBookings` is:
     - it reads only ids, with today's predicate plus a status term (not Cancelled, not Completed);
     - it orders them by `CleaningDateTime`, then `Id`, and gives each id its own scope;
     - it returns `Response(int Cancelled, int Considered, int Failed = 0)`. A failure is logged at Error and counted.
   - `AutoCancelRecurringOccurrence.cs` joins the push-subject allowlist, and the dispatcher leaves it.
4. **A refund settle re-derives its status on a conflict (BE-11).**
   - `RefundService.SettleAsync`'s single pass (`:367-381`) becomes at most three attempts.
   - Each attempt re-reads the succeeded-refund total and returns the credit share. The return is keyed, so the credit moves once. The attempt then sets the status from the fresh totals and commits.
   - It retries only on a `DbUpdateConcurrencyException` whose entries are this order alone, with only `PaymentStatus`, `UpdatedOn` and `UpdatedBy` modified. It reloads that entry before retrying. Anything else is rethrown.
   - `MarkSucceeded` stays before the loop.
5. **`PaymentStatus` becomes the second concurrency token (BE-11).**
   - Add `builder.Property(o => o.PaymentStatus).IsConcurrencyToken()` beside `CurrentStatus` (`OrderEntityConfiguration.cs:252-254`).
   - Regenerate `Initial` with the CLAUDE.md commands. Then check that:
     - the migration body is byte-identical;
     - the Designer and the snapshot each gain exactly one `IsConcurrencyToken` line;
     - `dotnet ef migrations has-pending-model-changes` reports nothing.
   - In MANUAL_STEPS, MS-2 moves the owed drop to the new id. `docs/architecture/database.md:492-503` names the new id too; it still says `20260920204705`, but the file is `20261005201452_Initial.cs`.
   - If T-0834 regenerates `Initial` in the same wave, the later branch rebases and regenerates again.
6. **The DEV reset** is prepared with step 5. It runs only on Mike's explicit approval, because approving this change does not approve a reset (CLAUDE.md, *DEV is shared with testers*). It renews the drop already owed for `20261005201452` (`MANUAL_STEPS.md:227-229`). Whatever runs is recorded here.

**Tests.** Each RED is written against today's code first. Race tests use two real connections, never `TestMethod(transactional: true)`.
- **The token:**
  - the model marks both axes as tokens;
  - a stale cancel against a committed `Paid` throws (SQLite);
  - a source guard fails on raw SQL that updates or deletes `"Orders"`, and on an `ExecuteUpdate` or `ExecuteDelete` on orders. It finds none today.
- **Races (PostgreSQL):**
  - the sweep, the auto-cancel and a request-path cancel each meet a `Paid` committed inside their window, and leave the order Paid + New with no ledger row;
  - two refunds of one order settle at once, both succeed, and the order ends Refunded.
- **Credit atomicity:**
  - a refused commit in the sweep, in the auto-cancel or on a request-path cancel leaves no ledger row;
  - a rerun returns the credit once (`Balance == SUM(ledger)`);
  - the sweep takes the owner locks in ordinal order.
- **Isolation:**
  - an occurrence whose notice throws stays Pending, while the next one is cancelled with only its own notice;
  - a failure that repeats holds back nobody;
  - an occurrence that is already cancelled is not dispatched.
- **Timers:**
  - a first command that throws no longer stops the others;
  - two commands that throw give an `AggregateException`;
  - when the host stops, the handler stops at once;
  - each command gets one scope.

The suites the ruling names stay green: among them the sweep, two-operator, credit-return, credit-expiry-lock, late-settlement, webhook, seat-race and guest-cancellation suites. The full unit, integration and host suites pass at every step.

**Docs.**
- **Three decision records**, numbered before the work began so that the parallel pull requests could not collide:
  - ADR-0071: the two tokens;
  - ADR-0072: job units. D1 covers the timers, and D2 one scope per item;
  - ADR-0073: the credit return and the lock order.

  Each ships `accepted` in the commit that brings its code, with its challenge, defence and verdict trail. T-0833's record is ADR-0074, and T-0832's is ADR-0075.
- **Two new living records**, `agents/architecture/decisions/order-concurrency.md` and `system-jobs.md`, join the decisions README.
- **The page edits each ruling lists:**
  - business-rules, order-lifecycle, payment-and-fiscal, cancellation-refund-dispute, platform-order-cancellation, cross-cutting, infrastructure (`:202`, `:205`) and order-availability (`:53-58`);
  - patterns-backend, and runtime-readiness, which gets a new item 8;
  - consistency.md: a judgment-call line, and a deviating-forms entry. The entry lists the shared-scope catches still in the tree, names T-0841, and retires when T-0841 lands.

## NOT doing

The ruling reported these items without absorbing them (CLAUDE.md §2). Each has its own ticket for Mike:
- the recurring reminder sweep's morning gap and its missing status term (T-0840);
- the other shared-scope catches, which consistency.md lists but this ticket does not change, and `CancelUnfilledOrders`' re-check comment (T-0841);
- a CI step that fails on pending model changes (T-0842);
- the Started and Completed e-mails sent before their commit, and `TryReturnAsync`'s remaining self-committing callers (T-0843).

The ruling also leaves these out:
- No Orders row lock, no `xmin` token, no conditional `ExecuteUpdate`, and no host-level 409 mapping.
- No code change to the webhook, `TakeOrder`, `MarkCashCollected` or the request-path cancels beyond the shared credit seam.
- The stale-checkout sweep keeps one commit per company, because it is the named reference shape.
- No attempt cap for an occurrence that fails every hour. The job-units ADR records this as a deviation.
- `CustomerConfirmedAt` does not become a token, and every other column stays last-writer-wins. Both are accepted residuals in the token ADR.

## Acceptance criteria (done looks like)

- [ ] **AC1** — Steps 1 to 4 land in order, before the token, and each is green on its own. Every RED above fails against today's code and passes after its step.
- [ ] **AC2** — With the token in, a payment that commits inside a sweep's or a cancel's window is refused at commit. The order stays Paid + New, with no Cancelled track, no notice and no ledger row, and a rerun cancels nothing (PostgreSQL, two connections).
- [ ] **AC3** — The regenerated `Initial` has a byte-identical migration body, and one more `IsConcurrencyToken` line in each of the Designer and the snapshot. It has no pending model changes. MS-2 and `database.md` name the new id.
- [ ] **AC4** — The unit, integration and host suites pass in full at every step.
- [ ] **AC5** — The three decision records, the two living records and the listed page edits ship with the code. consistency.md's deviating-forms entry names T-0841.
- [ ] **AC6** — The DEV reset runs on Mike's explicit approval and is recorded here. Otherwise it is recorded as owed, with the new migration id.

## Work record

- 2026-10-12 — Filed from the post-Wave E ground truth (BE-11 to BE-14) and the 2026-10-12 design ruling. Mike: "Go with recommended long-term fix" (2026-10-12).

## Review

Pending.
