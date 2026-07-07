# Testing Strategy — What Must Be Tested, and Where

Tests are evidence, not theater. This catalog fixes **what** gets tested, at **which layer**, and the
**must-cover** list for a platform that handles real orders, real pay, and real money. QA owns
execution; every developer writes the tests for the logic they add; the Reviewer enforces Gate 6
against this doc. The bar: "would I let this run unattended in production handling real customers and
real money."

Backend test projects that exist: `Cleansia.Tests` (xUnit unit), `Cleansia.IntegrationTests`
(WebApplicationFactory-style), `Cleansia.TestUtilities` (fixtures/builders). Frontend: Jest specs.
Coverage today is **minimal** — expanding it is part of going to PROD.

---

## TDD — write the test first (the default development approach)

We develop **test-first**. A story defines *what correct looks like*; the test encodes it
**executably**; the code makes it pass. Writing the test first forces you to nail the contract before
the implementation, catches the failure branches you'd otherwise forget, and gives you a regression
net the moment the feature exists. For money and lifecycle code this isn't optional polish — it's how
we get accuracy.

### The loop (red → green → refactor)
1. **Red** — write a failing test that states the desired behavior from the story's acceptance
   criteria. Run it; confirm it fails *for the right reason* (the behavior is missing, not a typo).
2. **Green** — write the **minimum** code to make it pass. No extra scope, no "while I'm here".
3. **Refactor** — clean up with the test green: extract helpers, remove duplication, apply the
   canonical pattern. The test stays green throughout.
4. Repeat per acceptance criterion / per failure branch until the story's AC are all covered.

### Where TDD is strict vs. pragmatic
- **Strict red-green-refactor (mandatory)** for **pure logic** — pricing, the pay formula + override
  precedence, validators, order/pay state transitions, fiscal-mode selection, numbering, refund math,
  any algorithm. This is where TDD pays off most and is easiest; there is no excuse to write these
  after. The Reviewer expects the test to predate the implementation (visible in commit order /
  the ticket's status log).
- **Test-first at the contract** for **command/query handlers** — write the handler's unit test
  (mock repos; assert `IsSuccess` and each `Error.Code`) against the intended `Command`/`Response`
  shape before the handler body. Write the route integration test (incl. the auth/ownership rejection)
  against the controller signature before wiring it.
- **Pragmatic test-alongside** for **UI** (Angular components, Compose/SwiftUI screens) — pure TDD on
  pixels is low-value. Here: write the **facade/ViewModel** test first (state transitions, error→
  snackbar mapping, the three data states — these are logic), then build the view to that tested
  state. The view itself is verified by QA against the AC, not by a unit test of markup.

### How the ticket shows TDD happened
A ticket implemented test-first shows it: the **test appears before (or with) the implementation** in
the diff/commits, the status log notes "red: <test> failing → green", and each AC item maps to a test
case. A PR where the implementation lands first and tests are bolted on at the end (or not at all) for
pure logic **fails Gate 6** — the Reviewer asks for it to be redone test-first, because after-the-fact
tests systematically miss the branches the author didn't think to handle.

### When you're changing existing untested code
Much of the codebase has no tests yet (see the audit). When you touch an untested unit to change it:
write a **characterization test first** that pins the *current* behavior, confirm it passes, then
TDD the change on top. This stops you silently breaking behavior you didn't know existed.

---

## Which layer tests what

| Layer | Test type | What it proves | Where |
|---|---|---|---|
| Pure domain/app logic | **Unit** | a calculation, validator, or state transition is correct in isolation | `Cleansia.Tests` |
| A handler with mocked repos | **Unit** | the handler's happy path + each `BusinessResult.Failure` branch | `Cleansia.Tests` (mock `IXxxRepository`) |
| A route end-to-end | **Integration** | controller → MediatR → validator → handler → DB behaves, incl. auth | `Cleansia.IntegrationTests` |
| A facade/component | **Jest** | state transitions, error mapping, the three UI states | frontend specs |
| A ViewModel | **(when harness exists)** | sealed-state transitions, action states | android test source |

**Rule:** new **pure logic** (no I/O) → a unit test is mandatory. A new **endpoint** → at least one
integration test covering the happy path and the most important failure (auth/ownership). A new
**validator rule** → a unit test asserting the rule's `BusinessErrorMessage` code fires.

## The must-cover list (non-negotiable before PROD)

These are the areas where a bug costs money, breaks the law, or leaks data. Each needs explicit tests:

1. **Pay calculation** — the full formula
   `clamp(basePay + extras + expenses, min, max) + bonus − deduction`, **and** the per-employee
   `EmployeePayConfig` override precedence over per-service config (IMP-3). Cover: override wins,
   fallback to service config, clamp at min and at max, bonus/deduction. Rounding is exact.
2. **Order lifecycle** — every transition in `New → Pending → Confirmed → InProgress → Completed` and
   `→ Cancelled`, including the illegal transitions that must be **rejected** (cancel a completed
   order, start an unconfirmed one, etc.).
3. **Pricing & money** — order total, surcharges/extras, promo-code discount, membership pricing, VAT,
   refunds on cancellation (the fee-rate × refund-amount math), currency handling. No floating-point
   surprises.
4. **Fiscal enforcement modes** — `None` / `AsyncBackground` / `BlockingOnline` route correctly per
   `CountryConfiguration`; **customer completion is never blocked** by fiscal registration; failed
   registrations are retried.
5. **Authorization & ownership boundaries (S2/S3)** — for resource-by-id endpoints, a **cross-user**
   and **cross-tenant** access attempt is rejected (returns NotFound, not the resource). This is a
   test, not just a code review.
6. **Idempotency (S7)** — side-effecting commands (Stripe charge, email, loyalty grant, referral
   reward, invoice/receipt/payout) are safe to run twice: the second call does **not** double the
   effect. Simulate the webhook re-delivery.
7. **Pay periods & invoices** — period open/close, invoice generation per employee per period,
   approve/mark-paid/cancel transitions, gap-free numbering where required.
8. **Every `BusinessErrorMessage` path** — a validator/handler that can return a given error code has
   a test that triggers it (so the error contract the frontend i18n depends on is real).

## How to write them (match the codebase)

- **Handler unit test:** construct the handler with mocked `IXxxRepository`/services, call `Handle`,
  assert `result.IsSuccess` or `result.Error!.Message == BusinessErrorMessage.X`. Use the builders in
  `Cleansia.TestUtilities`; don't hand-roll entity graphs inline.
- **Validator unit test:** `new Validator(mockRepo).TestValidate(command)` → assert the expected
  failure code for each rule, and a clean pass for valid input.
- **Integration test:** spin the Web host, authenticate as the relevant role, hit the route, assert
  the HTTP status + body. Cover the auth/ownership rejection explicitly.
- **Frontend:** test the **facade** (signal transitions, error→snackbar mapping) over the component
  where possible; assert the three states (empty/loading/error) render.
- **Process-global meters** (e.g. `IntegrationFailureMetrics`): a `MeterListener` hears every
  parallel test in the process. Two hermeticity strategies, chosen by what you assert on: unique
  synthetic tag values + listener-side filtering (see `IntegrationFailureMetricsTests`), or — when
  the assertion is on a REAL provider/tag value that a foreign test could also emit — membership in
  the serial `IntegrationFailureMeter` collection (filtering cannot help there; the foreign
  measurement carries the same tag). Either way the sink must be a `ConcurrentQueue`, never a plain
  `List` (callbacks arrive on foreign threads mid-assertion).

## Anti-patterns (Reviewer rejects)

- A test that asserts a method exists or returns non-null but checks no behavior (theater).
- All-happy-path with no failure/edge cases — money and state machines especially must test the sad
  paths.
- Tests coupled to incidental detail (exact log strings, private fields) that break on any refactor.
- Asserting on a hardcoded expected string instead of the `BusinessErrorMessage` constant.
- Skipping the cross-user/cross-tenant authorization test "because the code looks right".
