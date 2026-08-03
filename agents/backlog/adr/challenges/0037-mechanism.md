# ADR-0037 — Challenger report, MECHANISM lane

Role: challenger (attack). Gate 0: REFUTED-by-default — every claim below is from source I opened in
this session, cited `file:line`. Nothing inherited from the ADR's own citations without re-reading.

Ten findings. **CH-M2, CH-M3, CH-M4 and CH-M7 I consider blocking** — each one falsifies a sentence
the ADR uses as load-bearing justification, not a detail.

---

### CH-M1 — One opaque `order.not_takeable` is the wrong refusal contract: two precise keys already exist, are already localized on both mobile clients, and are already used by a sibling command for exactly these statuses

**The hole.** D6's "why a new key and not an existing one" table considers exactly three candidates —
`OrderNotConfirmed`, `NoAvailableSpots`, `OrderNotFound`. It never considers the two keys that name the
two most likely refusals:

```
BusinessErrorMessage.cs:49   public const string OrderAlreadyCancelled = "order.already_cancelled";
BusinessErrorMessage.cs:50   public const string OrderAlreadyCompleted = "order.already_completed";
```

These are not hypothetical. `AdminOverrideOrderStatus.Handler` refuses **exactly these two statuses
with exactly these two keys**, ten lines above the lifecycle walk this ADR proposes to edit:

```
AdminOverrideOrderStatus.cs:83-88   currentStatus == Completed  -> OrderAlreadyCompleted
AdminOverrideOrderStatus.cs:89-94   currentStatus == Cancelled  -> OrderAlreadyCancelled
```

**Why it matters.** D6's first and strongest argument for shipping the gate is *family consistency*
("it is the only unguarded command in its own family"). The same argument applied one level down says
`TakeOrder` should refuse `Cancelled`/`Completed` the way its family already does. Inventing a third
vocabulary for the same two states is the divergence this whole ADR exists to kill — a fourth entry in
the "surfaces that disagree about order status" table, authored by the document closing that table.

And the refusals are not one thing to the cleaner. The gate produces three behaviourally different
outcomes and one key collapses them:

| Refused because | What the cleaner should do |
|---|---|
| `Cancelled` | gone forever — refresh, stop looking |
| `Completed` | someone finished it — refresh |
| `New` + `Card` | **not payable yet — try again in a minute** |

"This job is no longer available" is a lie for the third row, and it is the row the ADR spends D1
constructing. It is also the row a cleaner will hit repeatedly and complain about, with a support
ticket that carries no distinguishing information.

Localization state, checked (this is *cheaper*, not more expensive, than the ADR's plan):
- Android partner already ships both: `partner-app/src/main/res/values/strings.xml:1092` and `:1093`.
- iOS already ships both: `CleansiaCore/.../Localizable.xcstrings:2802` and `:2837`.
- Web partner ships neither — see CH-M6, where it also ships neither of the keys the ADR *did* budget.

**What I want.** D6 rules the refusal **taxonomy**, not just "a key": `order.already_cancelled` for
`Cancelled`, `order.already_completed` for `Completed`, and at most one new key for the residue
(`New`+`Card`, `OnTheWay`, `InProgress`) — and that new key should name the residue honestly
(`order.not_yet_payable` / `order.no_longer_offered`), not "not takeable". If the author wants one
opaque key he must defend why the two existing keys are correct in `AdminOverrideOrderStatus` and wrong
here, given the caller is a partner in both cases and the state is identical.

---

### CH-M2 — BLOCKING. "Rule ordering under `Cascade.Stop`" does not give the property D6 claims. `TakeOrder.Validator` has **two** rule chains, both always run, and the combined response is a semicolon-joined composite that no client can resolve

**The hole.** D6 asserts:

> Under `Cascade.Stop` on `RuleFor(x => x.OrderId)`, the required order is:
> `NotEmpty → ExistsAsync (incl. ADR-0036 hold) → IsOfferable → HasAvailableSpots`
> …so a held order **never** returns `order.not_takeable`.

That is false three times over, and I verified each link.

**1. `Cascade.Stop` is rule-level, and the validator has two chains.**

```
TakeOrder.cs:38-45   RuleFor(x => x.OrderId).Cascade(CascadeMode.Stop)   // NotEmpty, Exists, HasAvailableSpots
TakeOrder.cs:47-60   RuleFor(x => x)        .Cascade(CascadeMode.Stop)   // caller, profile, approval,
                                                                          // already-assigned, weekly cap, overlap
```

FluentValidation **12.1.1** (`src/Directory.Packages.props`) defaults `ClassLevelCascadeMode` to
`Continue`, and `.Cascade(...)` on a `RuleFor` sets only the *rule-level* mode. Nothing in this repo
sets the global (`rg ValidatorOptions|ClassLevelCascadeMode src/` → zero hits). **Chain 2 runs even
when chain 1 has already failed.** Putting `IsOfferable` "after `ExistsAsync`" orders it relative to
three rules and leaves it unordered relative to six.

**2. The pipeline returns every failure, not the first.**

```
ValidationPipelineBehavior.cs:38-48
    validationResult.Errors ... .Select(failure => new Error(failure.ErrorCode, failure.ErrorMessage))
    .SelectMany(...).Distinct().ToArray();
```

**3. The transport collapses them into an unresolvable composite.**

```
CleansiaApiController.cs:93-99
    errors.GroupBy(e => e.Code!).ToDictionary(g => g.Key, g => string.Join("; ", g.Select(e => e.Message)))
```

The group key is FluentValidation's `ErrorCode`, which for all six `MustAsync` rules in this validator
is the same default (`AsyncPredicateValidator`) — **not** the property name. So two async failures
produce one dictionary entry valued `"order.not_found; order.time_conflict"`. Both clients read the
first value verbatim:

- web `http-error.interceptor.ts:45-48` → `getObjectValues(errors)[0]` → `resolveApiError:14-20` →
  no translation for the joined string → shows the generic `api.common.error_occurred`.
- Android `ApiErrorTranslator.kt:60-79` → `lookupKey(key) ?: key` → shows the **raw joined string** to
  the cleaner.

**This is not theoretical — it fires today.** `NotHaveTimeConflictAsync` returns `false` when the order
is missing (`TakeOrder.cs:150-154`), so an unknown order id already yields
`{order.not_found, order.time_conflict}`.

**Why it matters most: it breaks ADR-0036's inference protection, which D6 says this ordering
preserves.** Chain 2 queries the *real* order regardless of chain 1's verdict. So:

| Scenario | Chain 1 | Chain 2 (runs anyway) | Response |
|---|---|---|---|
| id does not exist | `order.not_found` | `order.time_conflict` (order is null → `false`, `:154`) | `not_found; time_conflict` |
| order exists but is **held** (ADR-0036), no overlap | `order.not_found` | passes | `not_found` alone |

**A bare `order.not_found` proves the order exists and does not overlap.** ADR-0036's whole reason for
folding the hold into `ExistsAsync` is defeated by a chain the ADR did not look at. ADR-0037 doesn't
create this, but D6 *asserts the property holds* and then relies on that assertion to justify adding a
fourth key into the same response — which strictly increases the number of multi-error responses.

**What I want.**
1. Delete the claim that rule ordering delivers the property.
2. Make it a **binding implementation constraint**: `TakeOrder.Validator` sets
   `ClassLevelCascadeMode = CascadeMode.Stop` (or folds every rule into one chain). This is a two-line
   change and it is the only thing that makes the ordering mean anything.
3. Verification step 5 stops being "confirm `IsOfferable` sits after `ExistsAsync`" — a code-reading
   instruction, i.e. exactly the comment-as-enforcement D7 forbids — and becomes a test that asserts
   `result.Errors` contains **exactly one** entry, per refusal scenario, including the ADR-0036 hold.
   The ADR already demands ordering be pinned by a test; it just points that demand at the wrong thing.

---

### CH-M3 — BLOCKING. `New + Cash` is **not** retraction-free. A live hourly sweep cancels unconfirmed recurring cash orders one hour before the cleaning, and this ADR would canonize offering them for up to seven days

**The hole.** The ADR's headline invariant, stated in the pull-quote at the top and repeated in D1:

> *an order is offerable when nothing that is still in flight can retract it. **For cash nothing is in
> flight** — the take is the confirmation.*

A recurring cash order has the customer's **confirmation** in flight, and there is a scheduled sweep
that retracts it. Chain, all read:

| Step | Evidence |
|---|---|
| Materializer runs daily, 7 days ahead | `MaterializeRecurringBookings.cs:27` (`HorizonDays = 7`); `MaterializeRecurringBookingsFunction.cs` (daily 02:00 UTC) |
| It creates a normal order with the template's payment type | `MaterializeRecurringBookings.cs:131` `PaymentType: template.PaymentType` |
| …at `New` + `PaymentStatus.Pending` | `OrderFactory.cs:116`, `OrderFactory.cs:166` |
| A cash order stays `PaymentStatus.Pending` until collection | confirmed — only `MarkCashCollected.cs:178` / `ConfirmRecurringOrder.cs:112` move it |
| Hourly sweep cancels it 1h before the slot if unconfirmed | `AutoCancelStaleRecurringOrders.cs:37` (grace 1h), `:57-58` (cutoff), `:63-69` (predicate); `AutoCancelStaleRecurringOrdersFunction.cs` cron `0 0 * * * *` |

The sweep's predicate is `RecurringTemplateId != null && PaymentStatus == Pending && CleaningDateTime
<= now+1h && UserId != null` — **there is no `PaymentType` term.** Its own doc-comment says what it is
for:

```
AutoCancelStaleRecurringOrders.cs:25-27
    this is the safety net that frees the cleaner's slot when the customer
    either ignored the reminder or never opened it.
```

The system already knows cleaners are standing on these orders. Under ADR-0037 that becomes the ruled,
correct behaviour: the order is `New` + `Cash`, therefore offerable, for up to seven days, and it is
retracted **at T−1h** — a strictly worse window for the cleaner than the card case the ADR refuses
(which evaporates ~1h15m after creation, days before the slot).

**Collateral: Fact 2's categorical is wrong.** ADR line 116-117 states *"The only writer that can move a
cash order off `New` is `TakeOrder.cs:192-194`."* `ConfirmRecurringOrder.cs:111-112` also does — it
writes `Confirmed` + `PaymentStatus.Paid` for a recurring cash order with **no cleaner assigned**. In a
document whose authority rests on "Gate 0: every row read, not inherited", a wrong categorical in the
fact table is corrosive, and this one has a second consequence (CH-M8b).

**Why it matters.** The ADR does not merely leave this open — it *rules it correct* and writes a false
invariant into the immutable record as the reason. The next engineer who notices cleaners losing
recurring cash jobs at T−1h will find ADR-0037 saying "for cash nothing is in flight" and stop.

**What I want.** The ADR's own method — *read the discriminator the system already wrote down* — hands
the fix. The sweep's own predicate is the term:

```
Offerable(o) ⟺ o.CurrentStatus == Confirmed
             ∨ (o.CurrentStatus == New ∧ o.PaymentType == Cash ∧ o.RecurringTemplateId == null)
```

D1's per-status table gains a `New + Cash + recurring` = **NO** row with `AutoCancelStaleRecurringOrders`
as the citation; the pull-quote invariant is rewritten; Fact 2's categorical is corrected. If the author
prefers to keep them offerable, that is an owner escalation with a named consequence (a cleaner loses a
booked slot an hour before it starts), not a silent yes.

---

### CH-M4 — BLOCKING. The predicate is asymmetric: it payment-qualifies `New` but *trusts* `Confirmed` to imply paid. It doesn't, and the same 15-minute sweep kills a `Confirmed`-but-unpaid card order

**The hole.** D1's `Confirmed` row: *"Card: money settled (`HandlePaymentNotification.cs:260-261`
writes `Paid` + `Confirmed` together)."* True of **that** writer. It is not true of the status axis.

```
CleanupStalePendingOrders.cs:50-53
    .Where(o => o.PaymentStatus == PaymentStatus.Pending
        && o.PaymentType == PaymentType.Card
        && o.CreatedOn < cutoff)
```

**No `OrderStatus` term at all**, every 15 minutes (`CleanupStalePendingOrdersFunction.cs`,
`0 */15 * * * *`). And `Confirmed ∧ Card ∧ PaymentStatus.Pending` is reachable:

- `AdminOverrideOrderStatus.Handler` is the generic status writer and has **no payment guard** — its
  only checks are terminal-state (`:83-94`) and forward-rank (`:96-106`). "Customer says they paid, the
  webhook never landed, push it to Confirmed" is precisely what an override exists for.
- A **declined** card is deliberately left at `PaymentStatus.Pending` so the client can retry —
  `HandlePaymentNotification.cs:230-235` (doc) and `:236-242` (`HandlePaymentIntentFailed`). So the
  unpaid-card population is larger than "abandoned checkouts".

Under D1 that order is offerable **and** takeable, and then it is cancelled out from under the cleaner
— the exact harm the card row of D1 exists to prevent, reached through the one term the rule doesn't
qualify.

**Why the ADR missed it.** A4/A5 reject `PaymentStatus != Failed` and `PaymentStatus == Paid` as
*whole rules*, and both rejections are correct (I re-checked them — see "found sound"). But **neither
row addresses the conjunction**, which is a different proposition:

```
Offerable(o) ⟺ (o.CurrentStatus == Confirmed ∧ (o.PaymentType == Cash ∨ o.PaymentStatus == Paid))
             ∨ (o.CurrentStatus == New       ∧  o.PaymentType == Cash ∧ …)
```

This is equivalent to D1's rule on every normal path (the webhook writes both together) and strictly
safer on the override path. A5's "excludes every cash order ever" does not apply — the `Cash` disjunct
carries them.

**Adjacent finding this evidence forces out, which the ADR should file (it owns the sweep's
semantics now).** The same over-broad sweep appears to kill **every recurring card order**: the
materializer creates it at 02:00 with `PaymentStatus.Pending` + `Card` for a slot up to 7 days out
(`MaterializeRecurringBookings.cs:27,131`; `OrderFactory.cs:116`), and `CleanupStalePendingOrders`
cancels anything matching `Pending + Card + CreatedOn < now−1h` — so it dies around 03:15, before the
02:30-next-day reminder (`SendRecurringOrderRemindersFunction`) and before `ConfirmRecurringOrder` can
ever be called. ADR-0037 is the document that promoted this sweep to "the sweep that actually runs";
it should carry the finding to `§Escalations` rather than leave the next reader to re-derive it.

**What I want.** Either the money term becomes symmetric (above), or D1 states explicitly that the rule
*trusts* `Confirmed` and the ADR requires the missing payment guard on `AdminOverrideOrderStatus`.
Silently trusting it is the option that must not survive. A4/A5's why-not rows are re-argued against
the conjunction, since as written they answer a question nobody asked.

---

### CH-M5 — Removing `Pending` from `AdminOverrideOrderStatus.Lifecycle` does **not** strand a legacy row. It does something worse: it unlocks a backwards move

**The hole.** D5 action 2 says remove `Pending` from the array and adds: *"the array is a forward-only
ordered walk and `Pending` sits between `New` and `Confirmed`, so removing it leaves every other
transition forward — the implementer confirms the index semantics before landing."* I confirmed the
index semantics. They are not what the ADR assumes.

```
AdminOverrideOrderStatus.cs:96    var currentRank = Array.IndexOf(Lifecycle, currentStatus ?? OrderStatus.New);
AdminOverrideOrderStatus.cs:97    var targetRank  = Array.IndexOf(Lifecycle, command.TargetStatus);
AdminOverrideOrderStatus.cs:101   if (targetRank < 0 || targetRank <= currentRank) -> InvalidOrderStatusTransition
```

With `Pending` removed from `Lifecycle` (`:56-64`), a row whose `CurrentStatus == Pending` yields
`currentRank = -1`. Every legal target then has `targetRank >= 0 > -1`, so the guard **passes for all
of them** — including `New` at index 0. The forward-only invariant, which the array exists to enforce,
is silently inverted for exactly the rows the change is about.

Concrete chain: a legacy `Pending` **cash** order gets overridden backwards to `New`, and under D1 it is
now offerable and takeable. The ADR reached the right conclusion ("dead, not deleted") partly *because*
"historical rows may exist (DEV in particular)" — those are the rows this makes mutable in the wrong
direction.

**Why it matters.** This is a shipped admin capability being edited by a one-line array delete whose
entire safety argument is a sentence telling the implementer to think about it. That is the same
species of artifact — "an assertion of safety that does not exist" — that D5 action 5 deletes
`StaleOrderCleanupService` for.

**What I want.** D5 action 2 names the fix, not the homework: an off-lifecycle `currentStatus` must be
handled explicitly (refuse with `InvalidOrderStatusTransition`, or pin it to the `New` rank), and it is
covered by a test seeded from a `Pending` row asserting `Pending → New` is refused. Also note the
knock-on: `TakeOrder.cs:192`'s `currentStatus is OrderStatus.New or OrderStatus.Pending` becomes
unreachable in its `Pending` arm once the take gate ships, so the ADR should say whether that arm is
kept (tolerating legacy) or deleted — right now it says nothing and the implementer will guess.

---

### CH-M6 — The "15 strings in 11 files" bill was traced against a key that is absent from 5 of those 11 files, the namespace is wrong, and the stated failure mode ("shows the raw key") is false on web

**The hole.** ADR line 377-378: *"traced against the existing `order.weekly_limit_reached` key, a
partner-facing take error needs: web partner — `.../assets/i18n/{en,cs,sk,uk,ru}.json` (5 files)…"*

I dumped `api.order.*` from all five partner-web locale files. The key sets are identical across
locales and **`weekly_limit_reached` is not in any of them.** It exists only on Android
(`values/strings.xml`, `error_order_weekly_limit_reached`) and iOS (`Localizable.xcstrings:4062`). The
reference exemplar the bill was derived from is itself missing from 5 of the 11 files it is used to
count — i.e. the partner web app has **no translation today** for the take error a cleaner most
commonly hits, and nobody noticed. That is a live defect *and* it invalidates the traceability claim.

Two further mechanical errors in the same passage:

1. **Namespace.** Partner web resolves under `api.*`, not `errors.*` —
   `http-error.interceptor.ts:15` (`` const candidateKey = `api.${String(errorKey)}` ``), confirmed by
   the shape of the locale files (`api.order.no_available_spots`, etc.). The root `CLAUDE.md` tells a
   developer *"Every backend error key … must have a corresponding frontend translation under
   `errors.*`"*. A dev following the project guide puts it in the wrong namespace and it never resolves.
   The ADR names files but not the namespace, so it does not correct the misdirection.
2. **Verification step 6 is wrong for web.** *"a missing one shows the raw key"* is true on Android
   (`ApiErrorTranslator.kt:70` — `lookupKey(key) ?: key`) and **false on web**:

```
http-error.interceptor.ts:14-20
    // ngx-translate echoes the key back when it has no translation — never let a
    // raw machine key reach the snackbar; fall back to the generic message.
    return message === candidateKey ? translate.instant(GENERIC_ERROR_KEY) : message;
```

A missing web key shows "an error occurred". It is **silent**. That is exactly how
`weekly_limit_reached` has stayed missing.

**Why it matters.** The ADR's only proposed detection for the 15 strings is a human running
verification step 6, and on the one client where the failure is invisible, step 6 says it is visible.

**What I want.** The bill states the namespace per client (`api.order.*` web / `error_order_*` Android /
`error.order.*` iOS). Step 6 is replaced by a machine check — which is CH-M7's problem, so the two must
be fixed together. And `order.weekly_limit_reached` missing from partner web is filed as a defect found
by this panel.

---

### CH-M7 — BLOCKING. The cross-stack parity test is not enforcement. **No CI job in this repo runs it on the edits it exists to catch**, and Nx will serve a cached green even when it is selected

D7 calls this *"the only layer that would have caught this drift"*. I traced whether it would run.

**1. It is Nx-`affected`-gated, and the drift lives outside Nx.**

```
frontend-ci.yml:86   run: npx nx affected -t test --base="$NX_BASE" --head=HEAD --parallel=3 --ci
```

A diff touching only `OrdersListViewModel.kt`, only `OrdersListLogic.swift`, or only
`OrderAvailability.cs` marks **zero** Nx projects affected → zero tests selected. The PR trigger has no
`paths` filter so the *job* starts; the *spec* does not run.

**2. On master, the workflow doesn't even start for those trees.**

```
frontend-ci.yml:12-17   push: branches: [master]  paths: ['src/Cleansia.App/**', '.github/workflows/frontend-ci.yml']
```

**3. Worse than not selected — Nx will return a cached PASS if it *is* selected.**

```
nx.json  namedInputs.default   = ["{projectRoot}/**/*", "sharedGlobals"]
nx.json  namedInputs.sharedGlobals = []
nx.json  targetDefaults["@nx/jest:jest"].inputs = ["default", "^production", "{workspaceRoot}/jest.preset.js"]
                                        .options = { passWithNoTests: true }
```

`{workspaceRoot}` here is `src/Cleansia.App`. Nx inputs cannot reference paths above the workspace
root, so `OrderAvailability.cs`, `OrdersListViewModel.kt` and `OrdersListLogic.swift` are **not
declared inputs of the spec's target**. Change a Kotlin literal, change one unrelated TS file in the
same project so the target is selected — Nx replays the cached result. The guard is green while the
thing it guards has drifted. This is not a hypothetical property of the design; it is a property of
the existing `error-contract-parity.spec.ts` too, which is why nobody caught CH-M6.

**4. Relocating it does not fix it.**

```
backend-ci.yml:15-17, 25-27   paths: ['src/**', '!src/Cleansia.App/**', '!src/cleansia_android/**', '!src/cleansia_ios/**']
```

Backend CI explicitly excludes both mobile trees. `android-ci` / `ios-ci` are Gradle/Xcode and cannot
run a Jest spec. **There is no workflow in this repo that fires on a mobile-only change and can read
C# source.** So D7 layer 2, as specified, would have caught none of the three mobile drifts D0 lists.

D7 layer 3 (`check-consistency.mjs`) does exist — `agents/tools/check-consistency.mjs` — but I found no
CI wiring for it either (`rg check-consistency .github/workflows/` → nothing).

**Why it matters.** D7's own thesis is "a comment is not enforcement". A test with no trigger is a
comment with a `.spec.ts` extension, and it is more dangerous than a comment because the ADR records it
as the thing that makes the ruling durable.

**What I want.** The ADR names the trigger, or D7 layer 2 is downgraded from "enforcement" to
"advisory". The precedent for the fix is eleven lines above the broken one in the same file:

```
frontend-ci.yml:79-81
      - name: Regen-drift guard self-test
        run: npm run typecheck:test           # unconditional, outside `affected`
```

Minimum acceptable: (a) the parity spec runs as an **unconditional non-Nx step** (plain `jest` /
`node`, not `nx affected`), and (b) `frontend-ci`'s push trigger adds `src/cleansia_android/**`,
`src/cleansia_ios/**` and `src/Cleansia.Core.Domain/**`, or the check moves to a repo-root workflow
triggered on all four trees. Without (a) *and* (b) it is scaffolding.

---

### CH-M8 — Three passages still assume the spare seat the owner has since deleted — and one of them is the severity argument for the take gate

Shipped state (commit `305ec194`), all verified: `BookingPolicy.cs:76` `SpareSeatsPerOrder = 0`;
`Order.cs:514-521` `MaxEmployees = RequiredEmployees + spareSeats`; `OrderFactory.cs:148` is the only
production caller; `IsFullyAssigned` is **deleted** (`rg IsFullyAssigned src/` → zero hits; `Order.cs:118`
is now `HasAvailableSpots`).

**(a) D0's severity claim is now false, and D6 leans on it.** ADR lines 87-88: *"per `Order.cs:519`
(`MaxEmployees = RequiredEmployees + 1`) … admits every `Cancelled` and `Completed` order that has a
free seat — which is **nearly all of them**."* Two problems: `Order.cs:519` is now `? 1` inside
`CalculateRequiredEmployees` (the formula moved to `:521`), and the claim itself is false. Neither
cancel nor complete unassigns — `Order.UnassignEmployee` (`:497`) has exactly **one** production caller,
`AdminReassignOrder.cs:86` — so a fulfilled `Completed` order has `Count == MaxEmployees` and **no free
seat**, and is therefore already invisible to the `RestrictToEmployeeId` floor. What survives is
`Cancelled`-before-anyone-took-it (common) and under-crewed multi-seat orders. **Fact 3 survives;
"nearly all of them" does not** — and D6's justification #2 ("Fact 3 is a live capacity bug") is quoted
at that magnitude.

**(b) D1's `Confirmed` row cites a seat that no longer exists.** *"Cash: already crewed, **spare seat
open**."* For the modal 1-seat order (`EstimatedTime ≤ 120`, `OrderSeatCapacityTests.cs:29`), a
`Confirmed` cash order is confirmed *because* a cleaner took it, so it has zero open seats. The actual
generator of an offerable `Confirmed` cash order with no cleaner on it is
`ConfirmRecurringOrder.cs:111-112` — recurring cash → `Confirmed` + `Paid`, no assignment — which the
ADR never mentions anywhere. The row's conclusion is right; its stated reason is now wrong and its real
reason is missing.

**(c) D9.2(b)'s justification is falsified, though its requirement survives.** *"a mobile cleaner's
Available tab would list jobs they are already on — **every one of which carries a spare seat**."* Not
any more: if you are on a 1-seat order it is full, so `hasAvailableSpots: true` filters it out. The
regression is now confined to `RequiredEmployees ≥ 2` orders that are not yet fully crewed.
`excludeEmployeeId` is still required and verification step 10's "hard reject" is still right — but a
reviewer who checks the stated reason will find it false and may wave the diff through. Restate it.

**(d) D9.4's interim is stale.** *"Interim, so nothing is blocked: `MaxEmployees` stands, unchanged."*
It was changed. And D9.5's *"does not introduce a covered-but-not-full state; `IsFullyAssigned` names it
and is unused"* is now vacuous — covered ⟺ full, and `IsFullyAssigned` is gone.

**What I want.** D0, D1's `Confirmed` row, D9.2(b) and D9.4's interim restated against
`SpareSeatsPerOrder = 0`. No conclusion flips. Three severity claims do, and an implementer who catches
one falsified premise stops trusting the other forty citations — which is the real cost.

---

### CH-M9 — `PaymentType` as the discriminator fails safe but is closed to extension, and the ADR gives no rule for the next member

**The hole.** `PaymentType.cs:6-9` is `{ Cash = 1, Card = 2 }`. The rule `Confirmed ∨ (New ∧ Cash)`
means a future `BankTransfer`/`Invoice` order at `New` is not offerable — the *safe* direction, so I am
not calling this broken. But it is silently wrong for a pay-on-site type like `Invoice` (B2B, settles
after the job — semantically `Cash`), and **nothing fails** when it is added. The ADR is the document
that will be cited as "the rule was decided", and it records no extension obligation.

I am **not** asking for an abstraction: switching on `PaymentType` is idiomatic here
(`OrderPaymentDispatcher.cs:33-73`, `ConfirmRecurringOrder.cs:96-102`, `CleanupStalePendingOrders.cs:52`)
and both of the first two already carry `default:` arms (`OrderPaymentDispatcher.cs:71-72`,
`ConfirmRecurringOrder.cs:100-101`) — the codebase already knows this enum grows.

**What I want.** One line in D3 and one test: `OrderAvailability` carries an exhaustiveness test over
`Enum.GetValues<PaymentType>()` (natural home: alongside `TC-AVAIL-EQUIV`) that goes **red on a new
member until `OrderAvailability` explicitly classifies it**. Cheap, mechanical, and it is the same
"a comment is not enforcement" standard D7 applies to everything else.

---

### CH-M10 — D3's NRE claim is overstated, and it is the evidence for a non-obvious asymmetry

**The hole.** D3: *"`OrderMappers.cs:14-17` `GetCurrentOrderStatus()` is `order.CurrentStatus!.Value`,
and `TakeOrder.cs:191` dereferences it on the request path — **a NULL row is a 500 today**."*

`Order.CurrentStatus` already falls back to the loaded history:

```
Order.cs:264-269
    public OrderStatus? CurrentStatus =>
        _currentStatus
        ?? _orderStatusHistory.OrderByDescending(s => s.CreatedOn).ThenByDescending(s => s.Sequence)
            .FirstOrDefault()?.Status;
```

and `TakeOrder.Handler` includes the history (`TakeOrder.cs:179`). Every order gets a `New` track at
creation (`OrderFactory.cs:166`). So the 500 requires a NULL column **and** zero history rows — not
"a NULL row".

**Why it matters.** D3 uses that claim to justify a genuinely non-obvious asymmetry (reads fail closed
on NULL at `OrderSpecification.cs:115-116`; the write gate must **not**). The design is right and I am
not attacking it — the *evidence* is wrong, and D3 is the section an implementer reads hardest.

**What I want.** Correct the sentence to "a row with a NULL column and no loaded history rows", and
keep the design.

---

## What I checked and found sound

Silence is not assent, so, explicitly — I read each of these and they hold as the ADR states:

- **Fact 1 (`OrderStatus.Pending` has no production writer).** Re-derived independently rather than
  taking the table: `rg AddOrderStatus src/ --type cs` gives 13 production call sites; none emits
  `Pending`; the only generic writer is `AdminOverrideOrderStatus.cs:109` with `command.TargetStatus`,
  and `Pending` is a legal target at `:59`. `{Pending, Confirmed}` ≡ `{Confirmed}`. Confirmed.
- **Fact 3's mechanism, in full.** `TakeOrder.cs:42-45` (order-side gates are only `ExistsAsync` +
  `HasAvailableSpotsAsync`); `Order.cs:116-117`; `Order.cs:481-490` (`AddAssignedEmployee` throws only
  on no-spots, status-blind); `TakeOrder.cs:190-196` (conditional status write);
  `OrderRepository.cs:247-259` (weekly count, **no status filter**);
  `OrderRepository.cs:262-269` (`SlotBlockingStatuses` excludes `Cancelled`/`Completed`). Both halves
  of "counts against the cap but does not block the calendar" confirmed. Only the *magnitude* is stale
  (CH-M8a).
- **The `StaleOrderCleanupService` refutation.** Both halves true: `StaleOrderCleanupService.cs:34`
  requires an `OrderStatusHistory` row with `Status == Pending`, unsatisfiable per Fact 1; and it has
  no DI registration, no Function and no caller anywhere in `src/`. Deleting it is right.
- **Surface #4 is the correct seam, and A13's why-not holds.** `GetPagedOrders.cs:71-91` pins
  `restrictToEmployeeId` for every non-admin; `OrderSpecification.cs:134-139` is `assigned-to-me OR
  free-seat` with no status term. And a blanket floor in `GetPagedOrders` really would break both the
  Admin list (`Cleansia.Web.Admin/Controllers/AdminOrderController.cs` uses the same handler, and
  `isAdmin` sets `restrictToEmployeeId: null`) and My-Completed (`orderStatuses` is client-supplied at
  `:87`).
- **The client status list genuinely is not a boundary today.** `orders.facade.ts:142` is
  `additionalFilters?.orderStatuses || [...]` — the caller can override it, and only surface #4 stops
  the result. D4's "display refinement, not a security boundary" is correctly argued.
- **D9.2(a) — the `-2h` floor really does fire once mobile switches.** Mobile sends no
  `cleaningDateFrom` on Available (`OrdersListViewModel.kt:263-265` → `null to null`;
  `OrdersListLogic.swift:81` → `cleaningDateFrom: nil`), web pins its own
  (`orders.facade.ts:149` → `?? new Date()`), and `GetPagedOrders.cs:57-61` applies the default only
  when `HasAvailableSpots == true`. Sound, and the absorbed-ticket call is right.
- **D9.2(b)'s core claim.** `RestrictToEmployeeId` (`OrderSpecification.cs:134-139`) does **not**
  exclude the caller's own orders — `ExcludeEmployeeId` is a separate, opposite-polarity block at
  `:129-132`. `excludeEmployeeId` is genuinely required. Only its severity is stale (CH-M8c).
- **Client literals, all three, exactly as tabulated.** web `{New, Pending, Confirmed}`
  (`orders.facade.ts:142-146`); Android `{_0, _2}` (`OrdersListViewModel.kt:248`); iOS `{._0, ._2}`
  (`OrdersListLogic.swift:78`). Two clients right, majority wrong. Confirmed.
- **A4's rejection is *stronger* than stated.** Not just abandonment: a **declined** card is
  deliberately left at `PaymentStatus.Pending` so the client can retry
  (`HandlePaymentNotification.cs:230-242`). `!= Failed` would admit declines too.
- **A5's rejection.** `OrderFactory.cs:116` creates every order `PaymentStatus.Pending`; cash leaves it
  only at `MarkCashCollected.cs:178` / `ConfirmRecurringOrder.cs:112`. `== Paid` really would empty the
  cash board. Confirmed.
- **A10 / two evaluation forms.** No `.Compile()` on any request path; the SQL/C# NULL divergence is
  real (`OrderSpecification.cs:115-116` fails closed vs `OrderRepository.cs:283-291` falling back to
  latest history). Two forms + an equivalence test is the right call and correctly follows ADR-0036.
- **A11 / dead-not-deleted.** `OrderStatus.cs:5` is `[SwaggerEnumAsInt]` and `Pending = 1` is on the
  wire; `OrderRepository.cs:266` and `GdprDeletionService.cs:92` both tolerate it in the conservative
  direction. Keeping the member is right — CH-M5 makes it *more* right.
- **The parity-spec technique works, even though the trigger doesn't.** `Cleansia.Api.sln` lives at
  `src/`, and `error-contract-parity.spec.ts:9-25` walks up to it and joins
  `Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs` correctly. Reading C# from Jest is proven.
  `check-consistency.mjs` exists at `agents/tools/check-consistency.mjs`, so D7 layer 3 is buildable.
- **The `Q-AVAIL-03` implementation matches what the escalation promised.** `SpareSeatsPerOrder = 0` as
  a named constant (`BookingPolicy.cs:67-76`), `MaxEmployees` retained as a wire field,
  `IsFullyAssigned` deleted, one writer of the cap (`Order.cs:514-521`, called only from
  `OrderFactory.cs:148`), and the behaviour pinned by `OrderSeatCapacityTests.cs`. D9.4's
  property-not-formula ruling held up under the flip — that part of the ADR earned its keep.
