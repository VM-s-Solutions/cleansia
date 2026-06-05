---
id: T-0125
title: Pay-calculation pure-function tests (test-first; clamp/override/bonus/rounding)
status: done
size: S
owner: backend
created: 2026-06-01
updated: 2026-06-05
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 0
source: testing.md must-cover #1
---

## Context
The pay formula is real money paid to cleaners and is currently **untested** (`Cleansia.Tests`
coverage is minimal — see the audit). `agents/knowledge/testing.md` must-cover #1 makes pay
calculation non-negotiable before PROD and mandates **strict red-green-refactor** for it (pure
logic, the highest-value place to test-first).

The pay math lives across four pure, I/O-free surfaces that this ticket pins with unit tests:
- `Cleansia.Core.Domain/EmployeePayroll/Services/PayCalculator.cs` — `CalculateTotalPay`
  (`base + extras + expenses + bonus − deduction`, floored at 0), `CalculateExtrasPay`,
  `CalculateDistancePay`, `ProratePay`, `SplitPayForMultipleEmployees`, `ConvertCurrency`,
  and the `Aggregate*`/`*Total` roll-ups.
- `Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs` — `CalculatePay(config, order)` and
  `CalculateAggregatedPay(configs, order)`, both of which apply `ApplyMinMaxClamp`.
- `Cleansia.Core.Domain/EmployeePayroll/EmployeePayConfig.cs` — `CalculatePay(rooms, bathrooms,
  distance)` with its own inline min/max clamp.
- `Cleansia.Core.AppServices/Features/EmployeePayroll/CalculateOrderPay.cs` —
  `Handler.SelectPreferredConfigs` (the IMP-3 per-employee override precedence: a config with
  `EmployeeId != null` wins over the per-service/per-package default).

This is a **test ticket** under the TDD invariant: it is written test-first (no production code
expected). Where a test exposes a defect in the math, it is logged and split into a separate fix
ticket — this ticket does not change behavior.

## Acceptance criteria
- [ ] **AC1 — clamp at min** — Given a config with `MinimumPay > 0` and a raw total below it, When
  `EmployeePayConfig.CalculatePay` / `PayCalculatorExtensions.CalculatePay` runs, Then the result
  equals `MinimumPay`. (`EmployeePayConfig.cs:141-143`, `PayCalculatorExtensions.cs:75-78`)
- [ ] **AC2 — clamp at max** — Given a config with `MaximumPay > 0` and a raw total above it, When
  the same paths run, Then the result equals `MaximumPay`. (`EmployeePayConfig.cs:146-148`,
  `PayCalculatorExtensions.cs:79-82`)
- [ ] **AC3 — no clamp when unset** — Given `MinimumPay == 0` and `MaximumPay == 0`, Then the raw
  total passes through unchanged (the `> 0` guards are respected, not treated as floor/ceiling of 0).
- [ ] **AC4 — inconsistent config throws** — Given `MinimumPay > MaximumPay` (both > 0), When
  `ApplyMinMaxClamp` runs, Then it throws `InvalidOperationException`. (`PayCalculatorExtensions.cs:68-73`)
- [ ] **AC5 — override precedence (IMP-3)** — Given for one `ServiceId` both a default config
  (`EmployeeId == null`) and a per-employee override (`EmployeeId != null`) exist, When
  `SelectPreferredConfigs` runs, Then the override is selected; And Given only the default exists,
  Then it falls back to the default. (`CalculateOrderPay.cs:153-161`)
- [ ] **AC6 — bonus/deduction** — Given base/extras/expenses with a bonus and a deduction, When
  `PayCalculator.CalculateTotalPay` runs, Then the result equals
  `base + extras + expenses + bonus − deduction`; And given a deduction large enough to drive the
  total negative, Then the result is floored at `0`. (`PayCalculator.cs:34-43`)
- [ ] **AC7 — extras count** — Given an order whose `Extras` has some `true` and some `false`
  values, When `CalculateExtrasPay` runs, Then only the `true` extras are multiplied by the rate.
  (`PayCalculator.cs:23-32`)
- [ ] **AC8 — extras component math** — Given `Rooms`/`Bathrooms`/`TravelDistance` and per-unit
  rates, When `PayCalculatorExtensions.CalculatePay` runs, Then `extrasPay` uses `max(0, Rooms − 1)`
  rooms (first room is in base) and `expensesPay == TravelDistance × DistanceRatePerKm`, including
  the `null` travel-distance → `0` case. (`PayCalculatorExtensions.cs:13-16`)
- [ ] **AC9 — rounding is exact** — Given rates and quantities that produce a non-round decimal
  (e.g. fractional km × rate), Then the asserted result is the **exact `decimal`** value with no
  floating-point drift (assert on `decimal` literals, not doubles).
- [ ] **AC10 — guarded helpers** — `CalculateDistancePay` and `ConvertCurrency` reject negative
  amount/rate and non-positive exchange rate (`ArgumentException`); `ProratePay` rejects a
  completion percentage outside `0..100`; `SplitPayForMultipleEmployees` rejects a non-positive
  count. (`PayCalculator.cs:111-133`, `217-250`)
- [ ] **AC11 — red→green visible** — Each test was committed failing-first (or with) the assertion
  it pins, per `testing.md` TDD; the status log records the red→green note and each AC maps to a
  named test case. No assertion is theater (no "method exists / non-null" without a value check).

## Out of scope
- Any change to production pay logic. If a test reveals a real defect (e.g. the IMP-3 override
  precedence or a clamp edge), file a separate fix ticket and reference it here — do not fix in
  this ticket.
- Handler/route integration tests for `CalculateOrderPay` (mocked-repo handler tests and the
  endpoint integration test belong to the broader payroll test tickets, not this pure-function set).
- Pay-period/invoice numbering, fiscal, refund, or pricing math (must-cover #3/#4/#7 — other tickets).
- `GeneratePeriodSummary`/`GeneratePayBreakdown` string formatting beyond what AC requires (coupling
  to exact log/format strings is an anti-pattern per testing.md).

## Implementation notes
- **Test project:** `Cleansia.Tests` (xUnit). Use builders from `Cleansia.TestUtilities` for
  `EmployeePayConfig` / `Order` graphs — do not hand-roll entity graphs inline (testing.md
  "How to write them"). `EmployeePayConfig` has private setters and factory methods
  (`CreateForService`/`CreateForPackage`, `SetPayLimits`, `UpdatePayRates`); construct via those.
- **TEST-FIRST (mandatory):** strict red→green→refactor per `agents/knowledge/testing.md` — pure
  pricing/pay logic is the strict-TDD category; the Reviewer (Gate 6) expects the test to predate
  the implementation, visible in commit order and this ticket's status log. As this is a pure-test
  ticket on existing code, treat untested units as **characterization-first**: pin current behavior,
  and if current behavior is wrong, log it (see Out of scope) rather than silently "correcting" it.
- **Serialization cluster:** NOT in any shared-file cluster in `agents/backlog/TICKET-MAP.md`.
  TC-PAY adds only test files under `Cleansia.Tests`; the `CreateOrder.cs` and `LoyaltyService.cs`
  clusters are unrelated. No concurrency hazard — safe to run in parallel with other Wave-0 work.
- **ADR governing:** none. ADR-0001/0002/0003 cover authorization, outbox-dispatch, and
  rate-limiting respectively; pay calculation is pure domain logic outside their scope (`adrs: []`).
- **Override precedence (AC5):** `SelectPreferredConfigs` groups by target id and picks
  `g.FirstOrDefault(c => c.EmployeeId != null) ?? g.First()` — assert both the override-wins and the
  default-fallback branch (`CalculateOrderPay.cs:153-161`). This is the IMP-3 contract.
- `security_touching: false`, `manual_steps: []` — no migration/NSwag; no DTO or endpoint changes.

## Status log
- 2026-06-01 00:00 — draft (created by pm)
- 2026-06-05 — in_progress (backend) — read all 4 pure surfaces, pinned signatures + clamp
  fields (`MinimumPay`/`MaximumPay`, the `> 0` guards), the two DISTINCT extras formulas
  (`EmployeePayConfig.CalculatePay` multiplies `rooms`/`bathrooms` directly; the
  `PayCalculatorExtensions.CalculatePay` folds the first room into base via `max(0, Rooms−1)`),
  the pure-decimal split/proration math, and the IMP-3 `g.FirstOrDefault(EmployeeId != null) ?? g.First()`
  precedence. Construction via factories (`CreateForService`/`CreateForPackage`, `SetPayLimits`) +
  `CurrencyMockFactory`, reusing the existing `Cleansia.Tests` idiom.
- 2026-06-05 — done (backend) — added 4 test files under
  `src/Cleansia.Tests/Features/EmployeePayroll/` (70 new tests, all green). Each AC maps to ≥1 named
  test (AC1–AC10 covered with exact `decimal` assertions; AC11 red→green note recorded here). TEST-ONLY:
  no production code changed, no ef, no nswag, not committed. Full suite 387 → 457 (0 failures); full
  solution builds 0/0.
  - RED→GREEN note (per testing.md AC11): the pay math was previously UNPINNED (zero PayCalculator
    tests = the "red" for a characterization ticket). One characterization assertion was authored on a
    WRONG assumption (expected the 100/3 three-way split to under-sum) and FAILED on first run — proving
    the assertion was live, not theater — then corrected to the verified exact-decimal behavior (50/3
    over-sums by a 27th-dp epsilon; 100/3 round-trips exactly). All assertions check concrete values.
  - DEFECT SCAN: NO money-math defect found in the formula, the floor-at-0, the min/max clamp (all 3
    directions + the unset-`0` guards + the AC4 `Min>Max` throw), the bonus/deduction edges, extras,
    distance/expenses, proration, currency conversion, or the IMP-3 override precedence — current math is
    correct. The split helper does a pure `decimal` divide with NO cents-rounding / remainder
    reconciliation; this is a documented design GAP, not a calculator bug (the caller owns reconciliation),
    flagged below for a separate follow-up — NOT fixed here.
  - PROPOSED FOLLOW-UP (non-blocking, NOT a fix in this ticket): `SplitPayForMultipleEmployees` rounding
    & remainder policy. `totalPay / employeeCount` returns decimal's full-precision quotient (not
    rounded to currency minor units), so a real 3-way split of e.g. 50.00 produces shares that, summed,
    drift a sub-cent from the total. If/when multi-employee payouts go live, a fix ticket should define a
    cents-rounded split with explicit last-share remainder reconciliation so `sum(shares) == total`
    exactly. Pinned by `SplitPayForMultipleEmployees_Uneven_Split_Is_A_Pure_Decimal_Divide_No_Remainder_Reconciliation`.

## Review

REVIEW — reviewer (2026-06-05) — T-0125

Re-verified in the MAIN tree (`src/Cleansia.Api.sln`). Read all 4 pure surfaces + the 4 new test
files + the support types (`OrderEmployeePay.Create`, `Order.Create`/`SetTravelDistance`,
`CurrencyMockFactory`/`TestHelper.Merge`, `Currency.Code` private-set).

1. COVERAGE — all 4 pure surfaces pinned:
   - `PayCalculator`: `CalculateTotalPay` (formula + floor-at-0 incl. exact-zero boundary &
     negative-driving deduction), `CalculateExtrasPay` (true/false/empty/default/null),
     `CalculateDistancePay` (math + negative guards), `ProratePay` (math + 0..100 guards),
     `SplitPayForMultipleEmployees` (even/odd/single + non-positive-count guards),
     `ConvertCurrency` (same/cross + negative/non-positive-rate guards), and the
     `CalculatePeriodTotal`/`CalculateDailyTotal`/`AggregatePeriodBreakdown`/`CountPeriodOrders`
     roll-ups (+ null guards). 35 tests.
   - `PayCalculatorExtensions.CalculatePay` + `CalculateAggregatedPay`: the `max(0,Rooms−1)` extras
     formula, null-distance→0 expenses, and the 3-direction `ApplyMinMaxClamp` (up/down/pass-through,
     Min>Max throw, aggregate max-of-mins floor / min-of-maxes ceiling). 14 tests.
   - `EmployeePayConfig.CalculatePay`: the DISTINCT direct rooms/bathrooms formula + inline clamp
     (min, max, both-unset, only-min, only-max, at-exactly-min/max boundaries). 15 tests.
   - `CalculateOrderPay.Handler.SelectPreferredConfigs` (IMP-3, AC5): override-wins,
     order-independence, default-fallback, per-service independence, override-only, empty. 6 tests
     (private static, reached by reflection — pins selection LOGIC, not accessibility).

2. EXACTNESS — floor-at-0, the min/max clamp (all 3 directions), the multi-employee split, and
   rounding are all asserted with EXACT `decimal` literals (e.g. `23.5200m`, `1107.2785m`,
   `16.666666666666666666666666667m`); no float tolerance. The same-vs-cross currency distinction is
   real (`Merge` writes the private `Code` setter via `BindingFlags.NonPublic`, so EUR/USD codes are
   applied and the cross-branch genuinely multiplies). No theater assertions — every test checks a
   concrete value.

3. TEST-ONLY — confirmed by `git diff --stat`: ZERO change on all 4 target surfaces (and
   `OrderEmployeePay`); ZERO modifications anywhere under the EmployeePayroll production dirs or
   `PayCalculatorExtensions.cs`. The only T-0125 writes are the 4 new test files +
   this ticket. (The large unrelated tree modifications are pre-existing in-flight work in the
   starting snapshot — none touch the pay-calc surfaces.)

4. DEFECT HANDLING — no money-math defect introduced/hidden. The reported design gap
   (`SplitPayForMultipleEmployees` does a pure full-precision `decimal` divide with no
   currency-minor-unit rounding / last-share remainder reconciliation, so an uneven split can drift a
   sub-cent on round-trip — value-dependent: 50/3 over-sums by a 27th-dp epsilon, 100/3 happens to
   round-trip) is correctly classified as a CALLER-owned contract concern, NOT a calculator bug, NOT
   patched in production, and logged as a PROPOSED FOLLOW-UP in the Status log + pinned by
   `SplitPayForMultipleEmployees_Uneven_Split_Is_A_Pure_Decimal_Divide_No_Remainder_Reconciliation`.
   Reviewer concurs — for a test-only/characterization ticket this is the correct disposition.
   ACTION FOR PM: open a separate non-blocking follow-up ticket for a cents-rounded split with
   explicit remainder reconciliation BEFORE multi-employee payouts go live.

5. BUILD + TEST (re-run by reviewer):
   - `dotnet build src/Cleansia.Api.sln -c Debug`: 0 warnings, 0 errors.
   - `dotnet test src/Cleansia.Tests`: Failed 0, Passed 457, Skipped 0 (= 387 prior + 70 new).
   - Filtered `FullyQualifiedName~Features.EmployeePayroll`: Passed 70, Failed 0.
   - No ef, no nswag run. Not committed, not pushed.

Verdict: APPROVED — src/Cleansia.Tests/Features/EmployeePayroll/PayCalculatorTests.cs:1

### Verification (orchestrator, independent) — 2026-06-05
4 pay-calc test files present; `PayCalculator.cs` git-clean (no production change — confirmed via
`git status`, empty). `dotnet build Cleansia.Api.sln` = 0 errors; `dotnet test Cleansia.Tests` =
**457 passed / 0 failed** (387 + 70 new). The "flaky pay test" the concurrent T-0127/T-0128 audit saw
was T-0125's own RED→GREEN cycle in progress (a characterization assertion authored on a wrong
assumption failed live, then corrected) — not a genuine flake; resolved. Pay math is now pinned (was
zero coverage): real money to cleaners is verified. **No money-math defect** found.

⚠️ **Follow-up logged (separate ticket T-0222):** `SplitPayForMultipleEmployees` does a pure
full-precision `decimal` divide with no currency-minor-unit rounding / last-share remainder
reconciliation, so `sum(shares)` can drift a sub-cent epsilon from the total on uneven splits
(value-dependent). Correct for the calculator's contract today (caller owns reconciliation) and NOT a
blocker, but must be addressed before multi-employee payouts go live. Pinned by
`SplitPayForMultipleEmployees_Uneven_Split_Is_A_Pure_Decimal_Divide_No_Remainder_Reconciliation`.

- 2026-06-05 — done (reviewer APPROVED; build 0 errors, 457 tests, 70 new; pay math pinned; no prod
  change; split-rounding follow-up → T-0222). NOT committed.
