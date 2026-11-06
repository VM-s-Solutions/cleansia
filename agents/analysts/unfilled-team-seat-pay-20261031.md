# Pay when a team job's seat never fills — analysis, 31 October 2026

Ticket: T-0905

**Status: analysis only. Nothing is built, and today's pay rules are unchanged.** Mike's ruling of
2026-10-31 (ruling 2): a team job whose second place is never taken should have its pay recalculated
per cleaner. This report says what happens today, the options, the one we recommend and the nine
questions only Mike can answer; each question carries a default. Sources: the Partner Orders build's
ground truth — `gt/pay.md` (verified lines), `gt/contract.md` §7 and `gt/board.md` §8 R2 — in
`/Users/michael/cleansia-work.noindex/partner-orders-build-20261031/`. Evidence paths below are relative
to the worktree. The business rules page is cited by section anchor, because its line numbers move
with every edit.

## Summary

- **Today:** when a two-place job's second place is never taken, the one cleaner does the whole job
  and is paid **one place — half the job**. The customer pays the full price and is told nothing, the
  company keeps the other half, and no administrator is alerted. The contract texts say exactly this:
  the reward is per place, divided by the places the job needs.
- **Recommended (Option A):** at completion, divide the job's pay by the cleaners who are actually on
  it, between one and the places booked. A two-place job done alone pays the whole job; a three-place
  job done by two pays half each. Show the extra as its own amount, *for the empty place* (variant A2),
  so the invoice still shows the contract's reward as it was agreed.
- **What it does not change:** the customer's price, and the most the company ever pays — never more
  than a full crew. A cleaner who joins late still gets a full place (ruling 3), which takes the solo
  cleaner back to one place.
- **What it costs:** pay calculation, the pay row, both apps, the web, the invoice PDF, three
  notifications and the legal texts in five languages, with legal review. Roughly 4–5 days of build
  plus the legal work. It is not a tweak.
- **Nothing waits on the answers.** The current build ships only the take-sheet team line (question 8),
  which is true today and promises nothing about an empty place.

## A.1 How a seat is paid today (verified)

- A pay rate describes the **whole job**. The job's figures — base, extras, minimum and maximum — are
  taken at the paid cleaner's own rates (their personal override, else the platform row) —
  `src/Cleansia.Core.Domain/Extensions/PayCalculatorExtensions.cs:79-91`,
  `src/Cleansia.Core.AppServices/Features/Orders/OrderPayEstimator.cs:136-161`.
- Each figure is split equally over `Order.RequiredEmployees`, with the leftover cents going to the
  lowest `SeatOrdinal`; the place's share is clamped to its bounds; dirtiness (the clamped job pay ×
  `DirtinessRate`, split the same way) is added after the clamp —
  `PayCalculatorExtensions.cs:109-134,144-148`; business rules `docs/product/business-rules.md#cleaner-pay`,
  the formula.
- `RequiredEmployees = ceil(EstimatedTime / 120)`, set once at booking — `OrderDuration.cs:53-54`,
  `Order.cs:1078-1085`. The take freezes the four job figures at the taker's rates
  (`WorkContractAcceptor.cs:37-43`, `OrderEmployee.cs:76-85,122-140`). Completion queues one pay message
  per cleaner on the job (`CompleteOrder.cs:373`, `CalculateOrderPay.cs:35-46`), and each is paid with
  `RequiredEmployees` as the divisor (`CalculateOrderPay.cs:260,287-298`). There is one pay row per
  cleaner per order, held by a unique `(OrderId, EmployeeId)` —
  `OrderEmployeePayEntityConfiguration.cs:132-133`, `CalculateOrderPay.cs:146-148`.
- Pay has **no time term and no distance term**. The divisor is `RequiredEmployees` for the job pay,
  the lockout pay, the late-cancellation fee share, the contract's reward and the board's estimate —
  `CalculateOrderPay.cs:268,289,297`, `WorkContractFactsBuilder.cs:71`, `OrderPayEstimator.cs:97-99,123-129`.

## A.2 What happens today when the second seat never fills (verified)

| When | What happens |
|---|---|
| Before the start | No administrator alert. `no_cleaner_yet` fires only when nobody is on the order, and `crew_short` / `crew_lost` fire only on a drop or a rejection (`SendCleanerJobReminders.cs:265-277`; callers `DropOrder.cs:185`, `RejectEmployee.cs:189`; business rules `#admin-notifications`, the `admin.order.no_cleaner_yet` row, *"a partly filled crew is not reported"*). |
| Start + 30 min | `cleaner_not_started` fires only if the solo cleaner has not started (`SendCleanerJobReminders.cs:229-239`). The unfilled-order sweep skips any order with somebody on it (`CancelUnfilledOrders.cs:80,86-89,117-118`). |
| During the job | The empty place stays takeable until the start plus `EstimatedTime` (`OrderAvailability.cs:41`; ADR-0076). |
| Completion | The solo cleaner completes alone — the gate counts the cleaners actually on the job (`Order.cs:198`; `docs/flows/execution-and-completion.md:103`) — and is paid **one place** (`CalculateOrderPay.cs:289`; business rules `#cleaner-pay`, *"an empty seat's share is nobody's"*; ADR-0069 `:160-161`; `docs/flows/pay-and-payouts.md:31-34`). |
| The customer | Charged the full price; no message and no refund. The company keeps the empty place's share (inferred). |
| What an administrator can do | Nothing per order: `OrderEmployeePay.AddBonus` has no production caller (`OrderEmployeePay.cs:205`). The only lever is a bonus or deduction on the whole period's invoice (`UpdateInvoiceAmounts`, Accountant and above, before payment; `AdminPayrollController.cs:16-25`, `EmployeeInvoice.cs:374-398`). |

## A.3 What binds it legally (verified)

The framework agreement, §9: the reward is *"divided equally among the places the job needs"*
(`Seed/Legal/employee/framework-contract/any/2026-10-27/en.md:88`); *"if it differs … tell the company and
it will correct it"* (`:90`); the fee share is divided *"among the places on the job"* (`:92`); *"The
company may add a bonus"* (`:94`). The contract for work, *Price*: *"the reward for your place on the job
shown to you at acceptance"* (`Seed/Legal/employee/work-contract/any/2026-10-27/en.md:25`), in the
version in force when the customer booked (`:17`). Every `2026-10-27` text is still our draft, in cs, en,
ru, sk and uk.

So today's rule is not an oversight in code: **the contracts promise one place's reward**, and paying
more needs either a new contract wording or a bonus under §9.

## A.4 Worked example (arithmetic from `PayCalculatorExtensions.cs:109-134`, inferred)

Assumes no minimum or maximum bound bites, and ignores the leftover cents.

| Job, at the cleaner's own rates | Who does it | Today | Option A |
|---|---|---|---|
| 1 600 Kč, normal dirt, two places | one cleaner | 800; the company keeps 800 | 1 600 |
| the same | one cleaner, and a second joins at 12:30 on a 09:00–13:00 job | 800 + 800 | 800 + 800 |
| 1 600 Kč, heavy dirt (+30 %), two places | one cleaner | 1 040 | 2 080 |
| 2 400 Kč, normal dirt, three places | two cleaners | 800 + 800; the company keeps 800 | 1 200 + 1 200 |

The customer's price is the same in every case. Under A the company never pays more than a full crew
would have cost.

## A.5 Options

- **A (recommended) — pay the job as if it was booked for the cleaners who did it.** At completion,
  divide the frozen job figures by `clamp(AssignedEmployees.Count, 1, RequiredEmployees)`; use the same
  divisor for a confirmed lockout's pay and, if agreed, for the late-cancellation fee share. A late
  joiner still takes a full place (ruling 3). The crew cannot change after the order is completed or
  cancelled (`AdminReassignOrderValidatorTests.cs:150-151`), and a drop is refused once the job is under
  way (`DropOrder.cs:119-122`), so every pay message for the job sees the same crew. Two ways to build it:
  - **A1 — change the divisor only.** No schema change, but the pay row then disagrees with the reward
    the contract states.
  - **A2 — keep the place's reward, and store the extra as its own amount on the same pay row**, *for
    the empty place*: a new column, the `Initial` migration regenerated and the DEV database dropped, a
    DTO field, NSwag and the mobile specs. A2 keeps *"price = the reward shown at acceptance"* true on
    the self-billed invoice.
- **B — pro-rata by hours.** Rejected: it contradicts ruling 3, no per-place start or end time is
  recorded, and it reads as an hourly wage.
- **C — an administrator decides each case.** Rejected: only a whole-period invoice bonus exists, no
  alert tells anyone the case happened, and nothing could be promised to the cleaner.
- **D — keep today's rule.** Rejected by ruling 2.
- **E — the solo cleaner also takes the second place.** Rejected: one place and one pay row per cleaner
  per order.

## A.6 What Option A touches (estimates)

- **Pay:** the divisor in `CalculateOrderPay.cs:268,289,297`, and `OrderPayEstimator`'s read paths for a
  completed job that has no pay row yet. For A2, one pay-row column, the DTO and the clients.
- **What cleaners and staff see:** My Pay — Android `PeriodPayScreen.kt` and `PeriodPayRepository.kt`,
  iOS `PeriodPayContent.swift` and `L10n+Earnings.swift`, partner web `period-pay.models.ts` — and the
  admin `invoice-detail.models.ts`; the invoice PDF's wording (`InvoiceLabels.cs:47-50`,
  `DefaultInvoiceLayoutBuilder.cs:222-231`, cs and en); the archive export (`CompanyArchiveService.cs:400`).
- **Three notifications in five languages:** the solo cleaner about two hours before the start; the
  crew when someone joins; an administrator *"job still short"* inside 24 hours of the start.
- **The apps:** a line on the partner job detail, *"If nobody joins: X"*; a take-sheet line only after
  the legal change.
- **The legal texts in five languages** — framework agreement §9 (`:88`, `:90`, `:92`) and the contract
  for work's *Price* — with legal review. A job booked under the old version gets the extra as a §9
  bonus.
- **Docs:** business rules — cleaner pay (`#cleaner-pay`, the empty-seat paragraph), crew size
  (`#crew-size`, the `SpareSeatsPerOrder` paragraph), the no-show's *"crews are rare"*
  (`#when-the-cleaner-cancels-or-no-shows`), the fee share and the lockout pay; `pay-and-payouts.md:31-34`; `features.md`; and a new ADR replacing ADR-0069's
  *"an empty seat's share is nobody's"*.
- **Tests:** `PayCalculatorExtensionsTests`, `FrozenSeatPayTests`, `LockoutRewardPayTests` and
  `CancellationFeeSharePayTests`, plus cases for one cleaner on two places, two of three, a late joiner
  and mixed grades; and an integration test of a two-place job completed by one cleaner.
- **Rough size:** backend 1–1.5 days, about a day per mobile app, about a day for the web and admin, half
  a day of docs, plus the legal texts and their review.

**Risks.**

- A late joiner at 12:30 (ruling 3 and ADR-0076) takes the solo cleaner back to one place after most of
  the work is done. Tell both of them.
- Framework §9 `:90` (*"tell the company and it will correct it"*) must change alongside.
- One person doing a job staffed for two invites quality complaints; the administrator alert matters
  more than the money rule.
- The company loses today's windfall, but never pays more than a full crew.
- Mixed grades still pay differently: each cleaner at their own rates, divided by the cleaners present.
- The legal texts are unreviewed drafts.
- Eight areas are touched. This is not a tweak.
- The four summary lines on the take sheet (built 2026-10-31, not merged) stay true under A only if the extra is
  paid as a §9 bonus; a new contract version hides them automatically until someone re-checks them.

## A.7 Questions for the owner (with defaults)

1. **Which option?** Default: **A** — divide by the cleaners on the job, between 1 and the places booked
   — with the extra shown as its own amount, *for the empty place* (A2).
2. **A late joiner takes a full place, and so cancels the solo cleaner's extra.** Accept that, or stop
   offering a started team job's free place (which reverses ADR-0076 for teams)? Default: accept, and
   tell both cleaners when someone joins.
3. **The same rule for a confirmed lockout and for the late-cancellation fee share on a short crew?**
   Default: yes to both.
4. **Change the legal texts, or pay the extra as a discretionary §9 bonus and promise nothing in the
   app?** Default: change the texts; until they are live, pay it as a bonus and add no take-sheet line.
5. **Apply it to every job completed after the deploy, whatever its contract version?** Default: yes —
   paying more than the contract says is always allowed.
6. **Tell or refund the customer when a team job goes ahead short?** Default: no; a dispute works as
   today.
7. **Add an administrator alert, *"job still short"*, inside 24 hours of the start?** Default: yes.
8. **The team line on the take sheet** — shipped in this build as `take_team_line`: *"2 cleaners needed.
   The app doesn't divide the work between you, and you can't take another job that overlaps this job's
   booked time."* Approve, or reword? Default: approve as shipped.
9. **"The same place reward" for a late joiner means the same as their own on-time take** — their own
   grade and rates — **not the same as the crew-mate's.** Confirm? Default: confirm; that is what the
   code does today.

## Out-of-scope findings with it

- **F1.** *"Crews are rare"* (business rules `#when-the-cleaner-cancels-or-no-shows`) is false by formula: every job over 120 booked
  minutes is a crew.
- **F2.** No alert fires when a crew goes ahead short without a release.
- **F3.** A cleaner who joins a job already in progress cannot record a start (`StartOrder.cs:106`
  accepts only `Confirmed` or `OnTheWay`), although the contract asks every
  contractor to (`work-contract … en.md:31`).
- **F4.** Crew-mates are not told when someone joins (`TakeOrder.cs:470-490`).
- **F5.** `OrderEmployeePay.AddBonus` and `AddDeduction` have no production caller
  (`OrderEmployeePay.cs:205-235`).
