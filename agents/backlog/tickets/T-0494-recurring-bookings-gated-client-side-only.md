---
id: T-0494
title: SECURITY — recurring bookings are a paid Plus perk; Create is gated, Update re-authors a whole schedule ungated
status: in_review
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-05
depends_on: []
blocks: []
stories: [T-0485]
adrs: [ADR-0036]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**Source: the Cleansia Plus audit (2026-08-02).** *"Recurring bookings are gated client-side only —
a direct API call succeeds."*

### ⚠️ THE HEADLINE CLAIM IS REFUTED. Read this before anything below it.

**`CreateRecurringBooking` has carried a server-side entitlement check since `2012b014` (2026-08-02,
PR #189) — one day after the verification point this ticket was written against.** The claim
*"a direct API call succeeds"* was true at `0e4ede1b` and is **false at HEAD**. There is nothing to
exploit on the create path, and **a second gate must not be added there**.

The check is `CreateRecurringBooking.cs:113-125`: resolve `userId` from `IUserSessionProvider`, call
`IUserMembershipRepository.GetActiveForUserNoTrackingAsync`, and fail with
`BusinessErrorMessage.RecurringTemplateMembershipRequired` when it returns null. The corollary this
ticket asserted — *"a `grep` for `Membership`/`Plus` across `CreateRecurringBooking.cs` returns
nothing"* — is stale with it: the file now names the repository, the gate and the error key.

*The Context block that stood here listed `CreateRecurringBooking.Validator`'s nine rules and
concluded "there is no membership check anywhere in the file". That reading was accurate on
2026-08-01 and has been replaced rather than annotated, because a backlog that asserts a live hole
which is in fact closed teaches the next reader to distrust every other row in it. The original
ground-truth table is recoverable from git history at `0e4ede1b` if anyone needs it.*

### What was actually open, and is what this ticket now closes

**`UpdateRecurringBooking` was `CreateRecurringBooking` wearing an old id.** Its validator chain ran
`TemplateId` → `NotEmpty` → `ExistsAsync` → `BeOwnedByCallerAsync` and stopped; the word "membership"
did not appear in the file. Its handler calls `RecurringBookingTemplate.UpdateSchedule`, which
rewrites **every** schedule field — frequency, day, time, rooms, bathrooms, address, both id lists,
payment type, start and end — and clears `LastMaterializedFor`.

So the exploit was not "recurring bookings are free"; it was **"one paid month buys a permanently
re-specifiable scheduling engine"**: subscribe, create a template, cancel, then POST to `Update` with
a completely different frequency, service set and address, forever, upgradable Monthly → Weekly.
Ownership passed and the entitlement gate was never consulted.

The owner's ruling for a lapsed subscriber (`Q-PLUS-04`, ADR-0036 AM-B) — *keep generating, at full
non-member price, and notify them* — **preserves a schedule; it does not license authoring a new
one.** That distinction is the whole finding.

### The gate the customer sees is also in the clients — that is UX, not the control

`Policy.CanManageRecurringBookings` maps to `PhysicalPolicy.CustomerOnly`, i.e. **every signed-in
customer holds it**, which is exactly why the entitlement decision cannot live in the policy and
correctly lives in the handler/validator. iOS ships `recurring_plus_gate_{title,subtitle,cta}` in
`CleansiaCustomer/Resources/Localizable.xcstrings`; **Android still carries no client-side gate at
all** (`recurring_plus_gate_*` has no Android equivalent), so on Android the server's refusal is the
entire user experience. Both clients *do* now carry the error string — see the backend review below.

### Why this is filed separately from every other Plus ticket, with no dependency on T-0491

**Because it is true whichever way the product questions are answered.** T-0491 decides what Plus
*promises*; this ticket is about a paid capability being obtainable **without paying**, which is an
authorization defect under any ruling. Making it wait on a product panel would be filing a security
hole behind a design discussion.

**One sibling to check in the same pass:** `SetRecurringBookingActive.cs`, `UpdateRecurringBooking.cs`
and `DeleteRecurringBooking.cs` sit in the same folder. A gate on create that is absent on
*re-activate* is not a gate. (Answered in the four-row table below: `Update` is gated; pause/resume
and delete stay deliberately open.)

## Acceptance criteria

- [ ] **AC1 — an authenticated customer with NO active membership is refused by the server.** Given a
      valid customer JWT and no active `UserMembership`, When `CreateRecurringBooking` is called
      directly, Then the request is rejected with a business error, and the template is **not**
      persisted. Evidence: an **integration or host test** that calls the route (not a unit test on
      the validator alone) — the whole finding is that the client is not the enforcement point, so
      the proof must go through the route.
- [ ] **AC2 — the gate covers the WHOLE lifecycle, not just create.** `SetRecurringBookingActive`,
      `UpdateRecurringBooking` and `DeleteRecurringBooking` are each examined and each given the
      correct answer with a reason. **Delete and pause should almost certainly stay open** — a lapsed
      subscriber must be able to stop a template that is still generating orders, and locking them out
      of that is a worse defect than the one being fixed. State the ruling per endpoint. Evidence:
      the four-row table plus a test per gated endpoint.
- [ ] **AC3 — an EXISTING template belonging to a lapsed subscriber is handled deliberately.** What
      happens to templates that are already materializing orders when a membership lapses? The
      materializer is `Features/Bookings/MaterializeRecurringBookings.cs` (a Function). **Silently
      continuing to generate paid cleanings for a non-subscriber, or silently stopping and leaving the
      customer with no cleaning and no notice, are both bad — pick one and say so.** Evidence: the
      ruling plus the behaviour at file:line. **If the answer is "notify the customer", that is a
      separate ticket and it is named, not built here.**
- [ ] **AC4 — status, not existence.** The check reads `MembershipStatus`
      (`Core.Domain/Memberships/MembershipStatus.cs`), not the presence of a `UserMembership` row.
      Evidence: the predicate at file:line plus a test with a cancelled membership.
- [ ] **AC5 — the error is in the contract on every client.** The new `BusinessErrorMessage` key has
      an `errors.*` translation in all three web apps' five locales, and the mobile clients map it —
      **note that different clients use different key namespaces** and NSwag throws ProblemDetails
      bare, so reading `.result` alone resolves nothing. Evidence: the parity check
      (`error-contract-parity.spec.ts` for customer web) plus the mobile mapping.
- [ ] **AC6 — a test that goes red against the pre-fix code (Gate 0.5 leg 1).** AC1's route test,
      proved to **succeed** (i.e. the booking is created) against the current code and to fail after.
      The verifier re-runs it **un-cached**. Evidence: the before/after runs.
- [ ] **AC7 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests` run
      **locally**, baselines **2295 / 108 / 75**.
- [ ] **AC8 — the SECURITY gate runs.** `security_touching: true`. The security reviewer states
      whether the same class exists on other paid capabilities — this is a *class*, and the audit
      already found a second instance (express, T-0493).

## Out of scope

- **What Plus promises** — T-0491. Deliberately no dependency in either direction.
- **The discount math** — T-0492.
- **Notifying customers whose templates stop** — named by AC3, not built here.
- **Any client change.** The clients already show a gate (on iOS at least); this ticket makes the
  server the one that decides. If Android's client-side gate is found missing entirely, **record it**
  and file it separately.

## Implementation notes

**No panel — one-line "no-decision" note on the security half:** enforcing on the server what the
client already claims to enforce introduces no new behaviour and no new product decision. **AC2 and
AC3 do carry decisions** (which lifecycle endpoints are gated, and what happens to live templates) —
they are written as forced rulings inside the ticket rather than as a panel, because the wrong answers
are both obviously bad and the right answer is bounded. **If AC3's answer turns out to need customer
notification, stop and escalate.**

**Gate 6.5 applies** — this is an authorization decision, one of the classes `routing.md` rule 7
enumerates. The reviewer gates on a behavioural non-stub plus an end-to-end test driving the real
route.

**Share the predicate with T-0493.** Whichever lands first writes "does this user have an active Plus
membership" somewhere the other can reuse. Two copies of a membership check is how they drift.

**Read first:** `agents/knowledge/security-rules.md` (S1–S11), `Features/Bookings/*.cs`,
`Core.Domain/Memberships/*`.

## Status log
- 2026-08-02 — **draft (created by pm from the Cleansia Plus audit).** **PM-verified first-hand:**
  `CreateRecurringBooking.cs`'s validator was read in full and contains **no membership rule of any
  kind**; the customer-facing gate is three iOS string keys (`recurring_plus_gate_*`) that Android
  does not even carry. **Filed with NO dependency on T-0491, deliberately** — a paid capability
  obtainable without paying is a defect under every possible product ruling, and queuing it behind a
  design panel would be filing a security hole behind a discussion.
- 2026-08-05 — **security review: headline claim REFUTED at HEAD** (create gate landed `2012b014`,
  2026-08-02). One real residual found: `UpdateRecurringBooking` re-authors a whole schedule with no
  entitlement check.
- 2026-08-05 — **backend: residual closed.** Context block rewritten to record the refutation.
  Entitlement check added as the **fourth link** of `UpdateRecurringBooking`'s existing `Cascade.Stop`
  chain, reusing `GetActiveForUserNoTrackingAsync`. Test-first: the validator unit test was written
  against the intended 3-arg `Validator` contract and confirmed **red** (compile) before the body; the
  ten-case host route suite is new (there was none). Six mutations run — every one of the 15 new cases
  is killed by at least one, and the L2 control leg stays green under both guard-deleting mutations.
  Pause / resume / delete deliberately left open. Full detail in `## Review` → BACKEND.

## Review

### SECURITY — 2026-08-05, reviewer: security. Verdict: **the headline claim is REFUTED at HEAD. No live breach. Ticket does not FAIL; it is partly landed and needs three residual items closed.**

#### 0. Headline first — this is already enforced server-side

`CreateRecurringBooking.Handler`, `src/Cleansia.Core.AppServices/Features/Bookings/CreateRecurringBooking.cs:113-125`:

```csharp
var userId = userSessionProvider.GetUserId()!;
// Recurring schedules are a paid Cleansia Plus perk, and CanManageRecurringBookings is
// held by every signed-in customer — without this the perk is free to anyone who calls
// the endpoint directly. The client-side gates are UX, not the control.
var membership = await userMembershipRepository
    .GetActiveForUserNoTrackingAsync(userId, cancellationToken);
if (membership is null)
    return BusinessResult.Failure<RecurringBookingTemplateDto>(new Error(
        nameof(userId), BusinessErrorMessage.RecurringTemplateMembershipRequired));
```

**The PM's ground truth was correct when written and is now stale.** The PM verified at `0e4ede1b`
(2026-08-01). The gate landed in `2012b014` (2026-08-02, PR #189, *"the owner's remark list — … Plus
enforcement, …"*), which is a descendant of `0e4ede1b`. HEAD is `f3b21dc5`; the working tree is clean
for `Features/Bookings/`. **Nothing to exploit today: a customer with no active membership gets a
business error and no row is written.**

Corollary the ticket assumed and which is also stale: *"a `grep` for `Membership`/`Plus` across
`CreateRecurringBooking.cs` returns nothing"* — it now returns `:108` (the injected
`IUserMembershipRepository`), `:115-125` (the gate) and the error key.

#### 1. S1–S10 walk

| Rule | Verdict | Evidence |
|---|---|---|
| **S1** userId from JWT | **PASS** | `CreateRecurringBooking.cs:113` and `:97` both resolve from `IUserSessionProvider`. The `Command` (`:14-26`) carries **no** `UserId` wire field at all — nothing to spoof. Same in `Update`/`SetActive`/`Delete`/`GetMine`. |
| **S2** authorization | **PASS** | All 5 actions on both hosts carry `[Permission(Policy.CanManageRecurringBookings)]` — `Cleansia.Web.Customer/Controllers/RecurringBookingController.cs:17,26,39,52,65` and `Cleansia.Web.Mobile.Customer/Controllers/RecurringBookingController.cs:17,26,39,52,65`. The policy maps to `PhysicalPolicy.CustomerOnly` (`Authentication/PolicyBuilder.cs:48`, frozen by `FrozenPermissionMapTests.cs:49`) — i.e. **every signed-in customer holds it**, which is exactly why the entitlement check cannot live in the policy and correctly lives in the handler. |
| **S3** ownership on resource-by-id | **PASS** | `Update` `:40-47`, `SetActive` `:25-32`, `Delete` `:25-32` — each one ordered `Cascade.Stop` chain: `NotEmpty` → exists → `BeOwnedByCallerAsync` comparing `template.UserId` to the session user. `GetMyRecurringBookings.cs:20` is user-scoped by query. |
| **S4** DTO leak | **PASS** | `RecurringBookingTemplateDto` carries no `UserId`, no `TenantId`, no Stripe id. `PreferredEmployeeId` (`GetMyRecurringBookings.cs:54`, `CreateRecurringBooking.cs:171`) is on a **customer**-facing DTO returning the customer's own choice — the ADR-0036 rule is that it is never on a *partner*-facing DTO. |
| **S5** rate limiting | **PASS** | `[EnableRateLimiting("auth")]` on all four mutations, both hosts (`:27,40,53,66`). |
| **S6** logging | **PASS** | The only logs on this path are `MaterializeRecurringBookings.cs:102-106,118-123,129-131` (template ids, counts) and `MaterializeRecurringBookingTemplate.cs:93-95,124-127,133-137` (template / address / saved-address ids). No name, email, phone or address line. |
| **S7** idempotency | **PASS** | `Create` is not a doublable financial side effect; the money is made by the materializer, which is idempotent via `LastMaterializedFor` (`MaterializeRecurringBookingTemplate.cs:36-43,195`) and commits per template (`:204`). |
| **S8** tenancy | **PASS** | `ActiveForUserQuery` uses tenant-scoped `GetDbSet()` (`Infra.Database/Repositories/UserMembershipRepository.cs:20-31`) — correct, it is a request path. The sweep is tenant-ignoring on **both** sides (`MaterializeRecurringBookings.cs:70` select, `MaterializeRecurringBookingTemplate.cs:85-87` write-back) with `SetTenantOverride` at `:102-106` and the commit **inside** the per-template scope at `:204`. That is the shape S8's mirror-case rule demands; pinned by `MaterializeRecurringBookingsTenantStampingTests`. |
| **S9** migration/DTO | **PASS** | No schema change. `RecurringTemplateMembershipRequired` is a new *error key*, not a DTO field — no `nswag-regen` implied by the guard itself. |
| **S10** `IsActive` | **PASS** | The documented collision holds correctly: `RecurringBookingTemplate.IsActive` (`Core.Domain/Bookings/RecurringBookingTemplate.cs:72`) is pause/resume, `GetMine` deliberately returns paused rows so the user can resume, and the materializer filters it (`MaterializeRecurringBookings.cs:71`). |

**AC4 (status, not existence) — already satisfied.** The predicate is
`Infra.Database/Repositories/UserMembershipRepository.cs:27-29`:
`m.UserId == userId && m.Status == MembershipStatus.Active && m.CurrentPeriodEnd > DateTime.UtcNow`.
`PastDue` / `Cancelled` / `Paused` and expired-period rows are all excluded, matching the owner ruling
recorded at `Core.Domain/Memberships/MembershipStatus.cs:18-30` (*"cut every benefit on the first
payment failure"*). **A trialing member IS entitled to recurring bookings** — Stripe's `trialing`
collapses to `Active` (`UserMembership.cs:157`) and only the *metered* express waiver is withheld
(`ExpressWaiverResolver.cs:63-66`). This is the split the task flagged, and the code gets it right.

**The "share the predicate with T-0493" note is already discharged.** `GetActiveForUserNoTrackingAsync`
is the single predicate used by `CreateRecurringBooking.cs:118`, `ExpressWaiverResolver.cs:40`,
`PreferredCleanerHoldResolver.cs:42`, `CancellationPolicyResolver.cs:33`, `GetMyServingCleaners.cs:90`
and `GetMyMembership.cs:60`. There is no second copy to drift.

**AC8 — is the class open on other paid capabilities? No.** Swept every Plus perk: express waiver
server-enforced at `ExpressWaiverResolver.cs:40-49` (T-0493); preferred-cleaner hold at
`PreferredCleanerHoldResolver.cs:42-47` (T-0516); discount per-occurrence via the same predicate;
cancellation window at `CancellationPolicyResolver.cs:33`. ADR-0039 CH-D1's *"the Plus gate is
client-side only"* remark about `GetMyServingCleaners` is also stale — membership gates the
availability oracle at `GetMyServingCleaners.cs:90-103`, and the endpoint now carries
`[EnableRateLimiting("auth")]` (`Web.Customer/Controllers/OrderController.cs:205`, mobile `:205`).

#### 2. The one real residual finding — `Update` is `Create` wearing an old id

**FINDING (entitlement bypass, requires one paid month — not a free-for-all).**
`UpdateRecurringBooking.Validator` (`Features/Bookings/UpdateRecurringBooking.cs:40-81`) has **no**
membership rule, and `UpdateRecurringBooking.Handler:118-129` calls
`RecurringBookingTemplate.UpdateSchedule` (`Core.Domain/Bookings/RecurringBookingTemplate.cs:123-149`),
which **rewrites every schedule field** — `Frequency`, `DayOfWeek`, `TimeOfDay`, `Rooms`, `Bathrooms`,
`SavedAddressId`, both id lists, `PaymentType`, `StartsOn`, `EndsOn` — and clears `LastMaterializedFor`
at `:147`.

*Exploit trace, complete:* subscribe for one month → `POST /api/RecurringBooking/Create` (passes the
gate) → cancel the subscription → `POST /api/RecurringBooking/Update` with that template id and a
completely different frequency, service set, address and start date. Every rule in the chain passes:
the row exists (`:44`) and the caller owns it (`:46`). **The surviving row is a new schedule wearing an
old id, and the create gate at `CreateRecurringBooking.cs:118-125` was never consulted.** Concretely:
199 CZK once buys a re-specifiable scheduling engine forever, upgradable Monthly → Weekly.

*Why this is not covered by the owner's ruling.* `Q-PLUS-04`
(`adr/0039-…-never-earns-a-hold-when-it-fails.md:1407-1408`, verbatim owner text at
`adr/0036-preferred-cleaner-first-refusal-hold.md:1988-1990`) rules that *"a lapsed membership does NOT
stop a recurring schedule. Occurrences keep being generated, at full non-member price, and the customer
is notified of the price change."* That **preserves a schedule**; it does not license **authoring a new
one**. The gap is the difference between the two, and it needs an explicit ruling under AC2.

*Not a FAIL against S1–S10 as a live breach:* no data leaks, no tenant crossing, no privilege
escalation beyond a perk the caller once legitimately held, and it is not reachable by an unsubscribed
caller (`BeOwnedByCallerAsync` `:84-90` requires a template they own, which only the gated `Create` can
mint — `RecurringBookingTemplate.Create` has exactly one production call site,
`CreateRecurringBooking.cs:136`; there is **no** admin creation path). Classify it as an
**entitlement/revenue defect of the same class, one degree weaker**, and gate the ticket on the ruling
rather than on a hotfix.

**Where the check belongs, precisely.** Append a **fourth link to the EXISTING `Cascade.Stop` chain**
at `UpdateRecurringBooking.cs:40-47`, **after** `BeOwnedByCallerAsync`:

```
RuleFor(x => x.TemplateId).Cascade(CascadeMode.Stop)
    .NotEmpty() → ExistsAsync → BeOwnedByCallerAsync → HasActiveMembershipAsync   // new, LAST
```

Do **not** open a second `RuleFor`. FluentValidation's class-level default is `Continue`, so a second
chain runs regardless of this one's verdict and would answer *"you need Plus"* for a template id that
does not exist or belongs to someone else — leaking entitlement state onto a path S3 requires to
resolve as not-found. Ordering matters here in the same way `TakeOrder.Validator` (`TakeOrder.cs:46-71`)
documents.

#### 3. AC2 — the four-row ruling table (reviewer's recommendation; AC2 requires the owner/PM to ratify)

| Endpoint | Gate? | Reason |
|---|---|---|
| `CreateRecurringBooking.cs:118-125` | **GATED — shipped** | Authoring a schedule is the paid capability. |
| `UpdateRecurringBooking.cs:40-81` | **GATE IT — open** | `UpdateSchedule` rewrites every field; update-with-all-fields-changed *is* create. §2 above. |
| `SetRecurringBookingActive.cs` | **LEAVE OPEN** | Resume restores a schedule `Q-PLUS-04` already says keeps running. Gating resume makes *pause a one-way door* for a lapsed subscriber — strictly worse than the defect, and it inverts the ticket's own AC2 warning: a lapsed customer must be able to stop and restart generation. Wants a **characterization** test, not a gate. |
| `DeleteRecurringBooking.cs` | **LEAVE OPEN** | Ticket's own ruling, and correct — a lapsed subscriber must be able to stop a template that is still materializing paid cleanings. |

Note in favour of the current shape: `UpdateRecurringBooking.Command` (`:14-26`) deliberately carries
**no** `PreferredEmployeeId`, so the ADR-0036 hold cannot be set or retargeted through update. That
closes the sharper escalation (withholding a seat from the board) independently. Keep it off.

#### 4. AC3 — already answered in code, and the answer matches the owner ruling

`MaterializeRecurringBookingTemplate.cs:143-157` prices every occurrence as a guest —
`userId: null`, `cleaningDateUtc: null` — with the comment *"A lapsed membership must not stop a
schedule, and a live one must not have this background job spend the member's monthly express
waivers…"*, and `:184` passes `ReservedExpressWaiver: null` explicitly. `MaterializeRecurringBookings.cs:70-75`
selects on `IsActive`/`StartsOn`/`EndsOn` with **no** membership term. So: **keep generating, at full
price** — `Q-PLUS-04` exactly. Pinned by
`Cleansia.Tests/Features/Memberships/RecurringMaterializationIsMembershipIndependentTests.cs`.

**The notification half is NOT built and must not be built here.** The owner ruling includes *"and the
customer is notified of the price change"*; `MaterializeRecurringBookings.Handler` takes no
`INotificationProducer`, and ADR-0039 records it as *"filed as its own ticket off ADR-0036 AM-B and is
out of scope"*. AC3 says name it, don't build it — **confirm the ADR-0036 AM-B ticket exists and link
it; if it does not, file it.** Do not let this ticket close with the ruling half-shipped.

#### 5. AC1 / AC6 — the real gap: there is no route-level test

`grep -r RecurringBooking` over `Cleansia.HostTests/` and `Cleansia.IntegrationTests/` returns
**nothing**. The only coverage is
`Cleansia.Tests/Features/Bookings/CreateRecurringBookingMembershipGuardTests.cs` — a unit test that
constructs `CreateRecurringBooking.Handler` directly (`:40-45`). AC1 is explicit that this is
insufficient: *"the whole finding is that the client is not the enforcement point, so the proof must go
through the route."* **AC1, AC6 and AC5's mobile half are the only things standing between this ticket
and done.**

The existing unit suite *is* non-vacuous at handler level and should be kept:
`Handle_ActiveMembership_CreatesTheTemplate` (`:118-127`) is the positive leg, and
`Handle_NoActiveMembership_RejectsBeforeTouchingTheAddress` (`:110-116`) pins the *ordering* — refuse
before reading the address, so the refusal cannot double as a saved-address-id oracle. Good test; wrong
level for this AC.

**Test design — `Cleansia.HostTests`, `POST /api/RecurringBooking/Create`.** Anti-vacuity is carried by
L2 and L4, not by L1.

| Leg | Arrange | Assert | Dies if the guard is deleted? |
|---|---|---|---|
| **L1** negative | customer JWT, **no** `UserMembership` row at all | 400 + `errors` bag first value `recurring_booking.membership_required`; and `SELECT COUNT(*) FROM "RecurringBookingTemplates" WHERE "UserId" = @u` is **0** | **YES — this is AC6's red-before-green leg** |
| **L2** positive — **the anti-vacuity leg** | *byte-identical request, same user*, plus one `UserMembership` `Status = Active`, `CurrentPeriodEnd = now + 30d` | **200** + exactly one template row persisted | **no, by design** — this is what proves L1's 400 came from the entitlement guard and not from a malformed body, a missing saved address, a bad JWT, a 404 route or a rate-limit 429 |
| **L3** status-not-existence (**AC4**) | a `UserMembership` row **exists** — run three cases: `Cancelled`; `PastDue`; `Active` with `CurrentPeriodEnd = now - 1d` | 400 + same key, nothing persisted | **YES** — and specifically it dies if anyone rewrites the predicate as `AnyAsync(m => m.UserId == userId)` |
| **L4** trialing (**AC4, the other direction**) | `Status = Active`, `TrialEndsAtUtc = now + 7d` | **200** — a trialing member is entitled (`UserMembership.cs:106,114`; `MembershipStatus.cs:9`) | **YES, inverted** — dies if someone copies `ExpressWaiverResolver.cs:63-66`'s `IsInTrial` conjunct into this gate. Without L4, a future "harmonize the Plus predicate" refactor silently strips recurring bookings from every trialing subscriber and nothing notices — `ExpressWaiverResolver.cs:57-62` and `UserMembership.cs:88-92` both warn about exactly that. |
| **L5** escape hatch (**AC2**) | lapsed user who owns a template | `POST /Delete` → 200 and `POST /SetActive {IsActive:false}` → 200 | pins §3 rows 3–4; dies if someone over-gates the lifecycle |
| **L6** (after §2 lands) | lapsed user who owns a template | `POST /Update` → 400 + membership key, and the persisted row's `Frequency`/`SelectedServiceIds` are **unchanged** | **YES** — assert the row, not just the status code; a 400 with the write already applied is the failure mode |

Run both hosts (`Web.Customer` **and** `Web.Mobile.Customer`) — the two controllers are
byte-identical siblings and the whole point of the finding is that enforcement must not depend on which
host is called. It does not today (the check is in the shared handler, per S3), and the test should keep
it that way.

**AC6 procedure.** "Pre-fix code" is `2012b014^`. Either run L1/L3/L4-negative against that parent
commit, or — cheaper and equivalent — delete `CreateRecurringBooking.cs:118-125` and confirm **L1, L3
and L4's negative cases go red while L2 stays green**. A mutation that reddens L1 but also reddens L2
proves nothing; that combination means the test broke, not that the guard fired.

#### 6. AC5 — web is done, **both mobile clients are missing the key**

- **Web customer: PASS.** `api.recurring_booking.membership_required` present in all five locales at
  `src/Cleansia.App/apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json:1584`, inside the `api`
  block opened at `:1577`. Backend key: `Core.AppServices/Common/BusinessErrorMessage.cs:137` =
  `"recurring_booking.membership_required"`.
- **iOS: FAIL.** `src/cleansia_ios/CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings`
  carries six `error.recurring_booking.*` siblings — `ends_on_before_start` `:4412`,
  `no_services_or_packages` `:4447`, `not_found` `:4482`, `not_owned_by_user` `:4517`,
  `saved_address_not_found` `:4552`, `starts_on_in_past` `:4587` — and **`error.recurring_booking.membership_required`
  is absent.** A non-member hitting the endpoint gets the generic fallback, not the upsell.
- **Android: FAIL.** Convention is `error_recurring_booking_*` string resources
  (e.g. `customer-app/src/main/res/values-cs/strings.xml:1261`); a repo-wide grep for
  `membership_required` under `src/cleansia_android/` returns **nothing**, in any of the five locales.
  Related and worth recording separately per the ticket's Out-of-scope note: Android carries **no**
  `recurring_plus_gate_*` client-side gate at all, so on Android the server error is the *only* thing
  the user will ever see — which makes the missing string the whole UX.

#### 7. "Were existing rows created without entitlement?" — partially answerable, and the blast radius is small

Answerable from the schema:
- `RecurringBookingTemplate : Auditable` carries `CreatedOn` (`Core.Domain/Common/Auditable.cs:9`).
- **Definitive:** templates whose owner has **never** held any `UserMembership` row —
  `RecurringBookingTemplates t LEFT JOIN "UserMemberships" m ON m."UserId" = t."UserId" WHERE m."Id" IS NULL`.
  Those were unambiguously created without entitlement.
- **Strongly indicative:** `t."CreatedOn" < (SELECT MIN(m."CreatedOn") …)` — template predates the
  user's first-ever enrolment.

**Not** answerable from the schema: whether a user who holds a membership row *today* was `Active` at
`t.CreatedOn`. `UserMembership` is mutated in place — `UpdateFromStripeWebhook` (`UserMembership.cs:149-179`)
and `ApplyPlanSwap` (`:219-236`) overwrite `Status`, `CurrentPeriodStart` and `CurrentPeriodEnd` on every
renewal — and there is **no membership status-history table** (the only migration is
`Infra.Database/Migrations/20260723182623_Initial.cs`). Stripe's subscription event log is the only
source that can reconstruct it.

**Blast radius:** the gate landed 2026-08-02 and the platform is pre-production (one `Initial`
migration; DEV is the only live environment). Any affected rows are DEV/seed data, not paying
customers. **Reviewer's position: no data fix is warranted — run the LEFT JOIN once on DEV to confirm
the count, and record the number. Do not build a backfill.**

#### 8. Non-blocking observations (not S-rule failures, do not gate this ticket)

- `UpdateRecurringBooking.Validator` omits the `StartsOn`-not-in-the-past rule that
  `CreateRecurringBooking.cs:65-67` has. **Not exploitable** — `MaterializeRecurringBookingTemplate.cs:231`
  clamps `if (searchStart < now) searchStart = now;`, so no backdated orders. Consistency nit only.
- `RecurringBookingTemplate.IsActive` (`Core.Domain/Bookings/RecurringBookingTemplate.cs:72`) shadows
  `BaseEntity.IsActive` with no `new` keyword, and its XML doc calls itself *"Soft-delete flag"* while
  S10 and CLAUDE.md both say it is the pause/resume flag. Behaviour is correct everywhere I traced it;
  the **comment** is wrong and the shadowing is a trap for the next reader. Worth a one-line doc fix in
  whatever ticket next touches the file.

#### 9. Verdict and required actions

**Not a FAIL. Not approvable as-written either — the ticket cannot close on its own ACs yet.**

1. **Rewrite the ticket's Context/ground-truth block** to record that the create gate landed in
   `2012b014` and that the finding as filed is refuted at HEAD. Leaving a ticket asserting a live hole
   that is closed teaches the next reader to distrust the backlog — the same defect class
   `security-rules.md` S8 calls out for the catalog itself.
2. **AC1 + AC6 — build the L1–L4 host test.** This is the only thing making the shipped gate a
   *regression-proof* gate rather than a line of code someone can delete.
3. **AC5 — add `error.recurring_booking.membership_required` (iOS, `CleansiaCore` xcstrings, 5 locales)
   and `error_recurring_booking_membership_required` (Android, `customer-app` strings.xml, 5 locales).**
4. **AC2 — get the §3 table ratified, then gate `Update`** as the fourth link of the existing
   `Cascade.Stop` chain at `UpdateRecurringBooking.cs:40-47`, with L6 as its test.
5. **AC3 — confirm the ADR-0036 AM-B notification ticket exists and link it.** Do not build it here.
6. Record separately (ticket's own Out-of-scope instruction): **Android has no client-side recurring
   Plus gate at all** — no `recurring_plus_gate_*` equivalent.

I will re-verify items 2–4 when they land. Re-run the route test **un-cached** with
`CreateRecurringBooking.cs:118-125` deleted; L1/L3/L4-negative must go red and L2 must stay green.

---

### BACKEND — 2026-08-05, dev: backend. Residual closed. Items 1, 2, 4 done; 3, 5, 6 answered (two of them already-done-and-stale, one still open for the PM).

#### B0a. AC roll-up

| AC | Verdict |
|---|---|
| **AC1** — server refuses a non-member, proved through the route | **MET** — L1 (+L2 as its control), `RecurringBookingMembershipGateRouteTests` |
| **AC2** — whole lifecycle examined, ruling per endpoint | **MET** — the four-row table in B2; `Update` gated, pause/resume/delete left open **and pinned by L5** |
| **AC3** — existing templates of a lapsed subscriber handled deliberately | **MET on the two shipped legs** (keep generating, at full price — B6). **The third leg, the price-change notification, has no ticket.** AC3 says *name it*; it cannot be *linked* until the PM files it → **open, PM** |
| **AC4** — status, not existence | **MET** — the shared predicate is `Status == Active && CurrentPeriodEnd > UtcNow`; L3's three cases + L4's inverse pin both directions |
| **AC5** — error in the contract on every client | **MET** — web ×5, iOS ×5, Android ×5 all present today (B4). The review's §6 FAIL is stale; **no client change was needed or made** |
| **AC6** — a test that goes red against the pre-fix code | **MET** — mutation table in B3; M2 is §5's procedure verbatim (L1 + L3×3 red, **L2 green**) |
| **AC7** — three suites run locally | **MET** — B8. The AC's baselines are stale; exact counts + exit codes recorded there |
| **AC8** — the SECURITY gate runs, and states whether the class is open elsewhere | **MET by the security review above** (swept every Plus perk; none open) |

#### B0. What changed, in three files

| File | Change |
|---|---|
| `src/Cleansia.Core.AppServices/Features/Bookings/UpdateRecurringBooking.cs` | `:55-56` — the **fourth link** on the existing `Cascade.Stop` chain; `:113-119` — `CallerHasActiveMembershipAsync`, which delegates to `GetActiveForUserNoTrackingAsync`; `:31/:36/:40` — the injected `IUserMembershipRepository` |
| `src/Cleansia.Tests/Features/Bookings/UpdateRecurringBookingMembershipGuardTests.cs` | **new** — 5 validator cases |
| `src/Cleansia.HostTests/Tests/RecurringBookingMembershipGateRouteTests.cs` | **new** — 10 route cases on the real Customer host |

**`CreateRecurringBooking.cs` is untouched — deliberately.** The gate at `:113-125` is correct and a
second one would be a second source of truth for one fact. **No new `BusinessErrorMessage` key**: the
refusal reuses `RecurringTemplateMembershipRequired` (`BusinessErrorMessage.cs:137`), so there is no new
i18n obligation on any client and **no `nswag-regen`** (no DTO or endpoint changed). **No schema change,
so no `ef-migration`.** `manual_steps` stays empty.

#### B1. The check, and why it is the fourth link rather than a second `RuleFor`

```
RuleFor(x => x.TemplateId).Cascade(CascadeMode.Stop)
    .NotEmpty() → ExistsAsync → BeOwnedByCallerAsync → CallerHasActiveMembershipAsync   // NEW, last
```

Exactly as §2 prescribed. `Cascade.Stop` is **rule-level, not class-level**, and the class-level default
is `Continue` — a parallel chain would run regardless of this one's verdict and answer *"you need Plus"*
for a template id that does not exist or belongs to someone else. That is not hypothetical: mutation
**M4** below built precisely that shape and the response body came back
`…not_owned_by_user; recurring_booking.membership_required`, i.e. the refusal for a stranger's template
id becomes an oracle for whether the caller is a subscriber. Same failure `TakeOrder.Validator`
(`TakeOrder.cs:46-71`) is shaped to avoid, and the same one `consistency.md:337-338` already records as a
deviating form for that validator.

**One predicate, not a second expression.** `CallerHasActiveMembershipAsync` calls
`IUserMembershipRepository.GetActiveForUserNoTrackingAsync` — the same method
`CreateRecurringBooking.cs:118`, `ExpressWaiverResolver.cs:41`, `PreferredCleanerHoldResolver.cs:43`,
`CancellationPolicyResolver.cs:33`, `GetMyServingCleaners.cs:90` and `GetMyMembership.cs:60` call. Its
rule (`UserMembershipRepository.cs:27-29`) is `Status == Active && CurrentPeriodEnd > UtcNow`, tenant-scoped.
Nothing new was written, so **AC4 holds by construction** and there is nothing to drift.

**A trialing member keeps this perk.** Stripe's `trialing` collapses to `Active`
(`UserMembership.cs:152`), and only the *metered* express waiver is withheld during a trial
(`ExpressWaiverResolver.cs:63-66`). L4 below pins that, and mutation **M3** proves L4 dies the moment
anyone copies the express resolver's trial conjunct into this gate.

#### B2. AC2 — the four-row table, as implemented

| Endpoint | Gate? | Reason |
|---|---|---|
| `CreateRecurringBooking.cs:113-125` | **GATED — was already shipped** | Authoring a schedule is the paid capability. Untouched by this ticket. |
| `UpdateRecurringBooking.cs:55-56` | **GATED — this ticket** | `UpdateSchedule` (`RecurringBookingTemplate.cs:123-149`) rewrites every schedule field and clears `LastMaterializedFor` at `:147`. Update-with-all-fields-changed *is* create. |
| `SetRecurringBookingActive.cs` | **LEFT OPEN — deliberately** | Resume restores a schedule `Q-PLUS-04` already says keeps running. Gating resume makes **pause a one-way door** for a lapsed subscriber: they pause to stop the bills, then cannot restart. That is strictly worse than the defect being fixed. Pinned by L5, not by a gate. |
| `DeleteRecurringBooking.cs` | **LEFT OPEN — deliberately** | A lapsed subscriber must always be able to stop a template that is still materialising paid cleanings. Pinned by L5. |

The two "left open" rows are **tested, not merely asserted**: L5 drives `SetActive{false}` and `Delete`
as a `Cancelled`-membership owner and requires 200 from both, and mutation **M5** (which over-gates
`SetActive`) turns L5 red. So an over-gating regression is caught, in the direction that matters.

`UpdateRecurringBooking.Command` still carries **no** `PreferredEmployeeId` — the ADR-0036 hold cannot be
set or retargeted through update. Kept off.

#### B3. AC1 / AC6 — the route suite, and the mutation table

`Cleansia.HostTests/Tests/RecurringBookingMembershipGateRouteTests.cs`, real `Web.Customer` host, real
JWT, real `[Permission]` gate, real Postgres. **Every leg posts the same request bytes** — `CreateBodyJson`
is a `static readonly` built once per process from fixed user / saved-address ids, so L1's refused request
and L2's accepted request are *literally the same JSON*. The only thing that varies is the membership row.

| Leg | Test | Arrange | Asserts |
|---|---|---|---|
| L1 | `A_customer_with_no_membership_is_refused_and_nothing_is_persisted` | no `UserMembership` row | **400** (asserted explicitly, so a 404/403/429 cannot pass) + `recurring_booking.membership_required` + `COUNT(*) == 0` |
| L2 | `An_active_member_posting_the_same_bytes_is_served` | + one `Active` row, period +27d | **200** + exactly one row |
| L3 ×3 | `A_membership_row_that_is_not_providing_benefits_is_refused` | `Cancelled` / `PastDue` / `Active`-but-period-expired | 400 + same key + 0 rows |
| L4 | `A_trialing_member_is_served` | `Active` + `TrialEndsAtUtc = +7d` | **200** + one row |
| L5 | `A_lapsed_subscriber_can_still_pause_and_delete_a_template_that_is_generating` | `Cancelled` owner, owns a template | `SetActive{false}` → 200 **and the row is paused**; `Delete` → 200 **and the row is gone** |
| L6 | `A_lapsed_subscriber_cannot_re_author_a_template_and_the_row_is_untouched` | `Cancelled` owner, owns a template | 400 + key, **and** `Frequency` still `Monthly`, `SelectedServiceIds` still the seeded one, `LastMaterializedFor` still non-null |
| L6b | `An_active_member_re_authoring_the_same_template_is_served` | `Active` owner, same template, **same Update bytes** | 200 **and** `Frequency == Weekly`, service list replaced, `LastMaterializedFor` cleared |
| L7 | `A_lapsed_subscriber_updating_someone_elses_template_is_told_not_owned_not_membership` | `Cancelled` caller, **stranger's** template | 400 + `not_owned_by_user`, and the body **does not contain** `membership_required` |

L6 asserts the persisted row, not just the status code, because *"400 with the write already applied"* is
the failure mode worth catching.

**Mutation table. Six mutations, each applied to source, rebuilt, and the ten-case class re-run un-cached.**

| # | Mutation | RED | GREEN |
|---|---|---|---|
| **M1** | delete the **new** `Update` chain link (`UpdateRecurringBooking.cs:55-56`) | **L6**; unit `A_Lapsed_Subscriber_Cannot_Re_Author_Their_Schedule`, `The_Entitlement_Question_Is_Scoped_To_The_Session_User` | L1, L2, L3×3, L4, L5, L6b, L7 |
| **M2** | delete the shipped `Create` gate (`CreateRecurringBooking.cs:115-125`) — **this is §5's AC6 procedure verbatim** | **L1, L3×3** | **L2**, L4, L5, L6, L6b, L7 |
| **M3** | `membership is null` → `membership is null \|\| membership.IsInTrial` (the express-resolver conjunct) | **L4** | L1, L2, L3×3, L5, L6, L6b, L7 |
| **M4** | move the entitlement link into a **second `RuleFor`** | **L7** | L1, L2, L3×3, L4, L5, L6, L6b |
| **M5** | invert the `Create` gate **and** gate `SetActive` | **L2, L5**, and L1/L3×3/L4 | L6, L6b, L7 |
| **M6** | `CallerHasActiveMembershipAsync` → always `false` | **L6b** | all nine others |

Raw counts: M1 `2 failed / 8 passed`; M2 `4 / 6`; M3+M4 (applied together — their kills are disjoint and
attributable by test name) `2 / 8`; M5 `7 / 3`; M6 `1 / 9`.

**The two claims the review asked for, both discharged:**
- **AC6's exact procedure** is M2: deleting `CreateRecurringBooking.cs:115-125` reddens **L1 and all
  three L3 cases while L2 stays green**. L2 green under the same mutation is the whole point — it rules
  out "the 400 came from a malformed body, a missing saved address, a bad JWT, a renamed route or a
  429" as the reason L1 passes.
- **No test survives every mutation.** Each of the 10 route cases and each of the 5 unit cases is killed
  by at least one row above. L2 is killed by M5 and L6b by M6 — the two "control" legs are themselves
  sensitive, so neither is a free pass.

**Honesty note on one run.** M1's first full-class execution reported L7 red as well. That was **not** a
kill: L7 re-run in isolation under the same mutation **passed**, and a later full-class run failed all
ten in 3 s at the fixture (`HostTestPostgresFixture.InitializeAsync` → Testcontainers `ResourceReaper`
cancelled). The harness is sensitive to Docker contention while sibling lanes are also using
Testcontainers in this shared checkout; a `[1 ms]` failure in this project means the container/reset
failed, not that an assertion did. Every number in the table above comes from a run where the fixture
came up.

**Host coverage is `Web.Customer` only, and that is a decision, not an omission.** §5 asked for both
customer hosts. The enforcement point is the shared `Cleansia.Core.AppServices` validator/handler, which
`Web.Mobile.Customer/Controllers/RecurringBookingController.cs` reaches through the identical
`Mediator.Send`; the two controllers cannot diverge on entitlement without the shared code diverging
first, which M1–M6 already cover. Booting a fifth host costs a `ProjectReference` in
`Cleansia.HostTests.csproj` plus an audience/device-claim mapping, and `Web.Mobile.Customer` currently
overrides `RequestLoggingMiddlewareType` — a file another live lane is editing. **Flagged for the
reviewer to overrule if they disagree**; it is one `ProjectReference` and a locally-constructed factory
whenever the logging lane lands.

#### B4. AC5 — both mobile clients now carry the key. §6's FAIL is stale as of today.

The review's §6 was written against an earlier tree. Re-verified 2026-08-05:

| Client | Key | Status |
|---|---|---|
| Web customer | `api.recurring_booking.membership_required` | **PRESENT** ×5 locales, `apps/cleansia.app/src/assets/i18n/{en,cs,sk,uk,ru}.json` |
| iOS | `error.recurring_booking.membership_required` | **PRESENT** ×5, `CleansiaCore/Sources/CleansiaCore/Resources/Localizable.xcstrings:4447-4479`, all `"state": "translated"` — it is now the 2nd of **seven** `error.recurring_booking.*` siblings, not six |
| Android | `error_recurring_booking_membership_required` | **PRESENT** ×5, `customer-app/src/main/res/values{,-cs,-sk,-uk,-ru}/strings.xml`, and pinned by `customer-app/src/test/java/cz/cleansia/customer/core/auth/BackendKeyStringsTest.kt:26` |

**No client work is required by this ticket, and none was done** (mobile lanes are live). Two things the
mobile lanes should know, recorded so they need not re-derive them:

1. **The `Update` refusal arrives in a different ProblemDetails slot than the `Create` one, and the
   clients already handle it.** `Create`'s gate is a handler failure → `type`/`detail` carry the key
   directly. `Update`'s is a **validation** failure → `BusinessResult.Error` is the
   `IValidationResult.ValidationError` sentinel and the real key rides the `errors` extension bag under
   FluentValidation's error code (`CleansiaApiController.CreateProblemDetails`). Every client reads the
   first `errors` value (web `HttpErrorInterceptorFn`, iOS/Android `firstErrorKey`), and
   `recurring_booking.not_found` / `not_owned_by_user` already reach users through that exact path from
   this same chain — so the new link needs **no** client change to render.
2. **Android still has no client-side Plus gate at all** — `recurring_plus_gate_{title,subtitle,cta}`
   exist only on iOS (`CleansiaCustomer/Resources/Localizable.xcstrings:26324/26359/26394`, consumed by
   `CleansiaCustomer/Sources/L10n+Recurring.swift:90-98`); a repo-wide grep for `recurring_plus_gate`
   under `src/cleansia_android/` returns nothing. **On Android the server refusal is the entire UX**,
   which is why the string above matters more there than anywhere else. Per this ticket's own
   Out-of-scope instruction this is **recorded, not fixed** — it wants its own Android ticket
   (upsell sheet + CTA into the Plus purchase flow, mirroring iOS).

#### B5. §7 — the existing-rows query was NOT run: no database is reachable from this environment

The SQL, ready to paste, restricted to the answerable half:

```sql
SELECT COUNT(*)
FROM   "RecurringBookingTemplates" t
LEFT   JOIN "UserMemberships" m ON m."UserId" = t."UserId"
WHERE  m."Id" IS NULL;
```

**Count: not obtained.** There is no local Postgres, no `psql` on PATH, no `az` CLI, and no user-secrets
connection string in `~/.microsoft/usersecrets/` for `58a2f81b-2ff8-4dd7-afbf-0999d43a5775` — the DEV
connection string simply is not present in this checkout. Running it against the Testcontainers database
would return 0 and mean nothing. **Owner/PM action: run the statement once on DEV and paste the number
here.** Reviewer's position stands and this ticket does not change it: **no backfill.** The blast radius
is DEV/seed rows (one `Initial` migration, pre-production), the create gate has been in place since
2026-08-02, and whether a user who holds a membership row *today* was `Active` at `t."CreatedOn"` is
**unanswerable from this schema** — `UserMembership.UpdateFromStripeWebhook` (`UserMembership.cs:149-179`)
overwrites `Status` in place on every renewal and there is no status-history table. Stripe's event log is
the only reconstruction.

#### B6. §9 item 5 — the ADR-0036 AM-B notification ticket **does not exist**. Open for the PM.

Searched `agents/backlog/tickets/` for `AM-B`, for the ruling's own words (*"notified of the price
change"*, *"non-member price"*) and for every filename matching `recur`/`notif`. The only file in
`agents/backlog/tickets/` that matches any of them is **T-0494 itself**. ADR-0036 AM-B
(`agents/backlog/adr/0036-preferred-cleaner-first-refusal-hold.md:2026-2075`) states the third leg of the
owner ruling — *"Notify the customer of the price change: **NO. This does not exist.** … A new
notification is required. **Filed as a ticket (P-3)**, not assumed"* — and enumerates its scope: a new
`NotificationEventCatalog` key, a `NotificationCategory` + `UserNotificationPreferences` mapping,
`NotificationFeedEventKeys` registration, the ADR-0025 loc-key display contract on both mobile clients,
and five-locale copy on three clients.

**It was never filed.** Per AC3 it is *named here, not built here*; per §9 item 5 it must be **linked**,
so this ticket cannot claim AC3 complete until the PM files it and adds it to `blocks:`/`depends_on:`.
The other two legs of the ruling **are** shipped and correct — `MaterializeRecurringBookings.Handler`
takes no `IUserMembershipRepository` and its query carries no membership term, and
`MaterializeRecurringBookingTemplate.cs:143-157` prices every occurrence as a guest
(`userId: null`, `ReservedExpressWaiver: null` at `:184`), pinned by
`RecurringMaterializationIsMembershipIndependentTests`.

#### B7. Catalog-edit routing — **routed to the Architect, not edited inline**

There is a reusable entry here: *"an entitlement / paid-capability check on a mutate-by-id command is
the LAST link of the existing ownership `Cascade.Stop` chain, never a second `RuleFor`."* Running
`conventions.md` §"Who ratifies a catalog edit":

- **Test 1 — does it put shipped code in violation? No.** *Sweep run:*
  `grep -rn "GetActiveForUserNoTrackingAsync\|GetActiveForUserAsync" src/Cleansia.Core.AppServices --include="*.cs"`
  → 19 call sites. Classified: the only **mutate-by-id command carrying an ownership chain** among them
  is `UpdateRecurringBooking`, which conforms. The rest are creates (`CreateRecurringBooking`,
  `CreateMembership*`), self-scoped commands/queries with no resource id (`CancelMembershipSubscription`,
  `SwapMembershipPlan`, `GetMyMembership`, `GetMyServingCleaners`), pricing/policy resolvers
  (`ExpressWaiverResolver`, `CancellationPolicyResolver`, `PreferredCleanerHoldResolver`, `OrderFactory`,
  `QuoteOrder`), or background/webhook paths (`GdprDeletionService`, `StripeSubscriptionWebhookHandler`).
  Zero become deviations. Baseline is zero by construction.
- **Test 2 — does it narrow latitude? Contested, therefore routed.** *Search run:* `consistency.md`,
  `patterns-backend.md`, `security-rules.md` for `entitle` / `membership` / `Cascade`. The candidate
  governing sentence is **`consistency.md` B4** (`:56-62`): *"Validator validates the shape and existence
  of inputs; the Handler enforces business rules and ownership … Do **not** put ownership/session checks
  in the validator … ownership belongs in the handler, S3."*
  **Reading A (fires):** an entitlement check is a *business rule about the caller*, so B4 governs the
  subject at a general level and rules "handler"; putting it in the validator carves an exception out of
  B4 and is a law.
  **Reading B (floor):** B4's operative clause names *ownership/session* checks and the *fetch-and-guard
  of the entity being mutated*; an entitlement check reads a different aggregate, never loads the mutated
  row, and is nowhere named — nothing governs the subject, so the floor applies and it is inline.
  Per `conventions.md`'s ⚠️ note (*"quote the candidate sentence and record both readings … rather than
  settling it by whoever quotes first"*, pending T-0553), **both are recorded and the edit is routed to
  the Architect.** No `agents/knowledge/*.md` file was touched by this ticket.
- **Test 3 — cross-stack prescription? No.** B4's mobile/web notes are descriptive only.

**Related deviation to record while the Architect looks at it:** `UpdateRecurringBooking`'s
`BeOwnedByCallerAsync` was **already** in the validator before this ticket — the same B4 deviation
`consistency.md:61-62` names for `UpdateSavedAddress`/`DeleteSavedAddress`. The new link follows the
file's existing shape rather than inventing a third placement, and §2 of the security review required
that placement for the ordering reason in B1. If the Architect canonicalises entitlement-in-handler
instead, `Update`'s ownership check has to move with it or the leak in M4 comes back.

#### B8. Gate 0.5 — local runs, exact numbers

Solution built from `src/`: `dotnet build Cleansia.Api.sln` → **0 Error(s)**, 33 warnings (all
pre-existing `CS8618`-class nullable warnings in unrelated projects).

| Suite | Result | Exit |
|---|---|---|
| `Cleansia.Tests` | **3129 passed, 0 failed, 0 skipped** (3 m 09 s) | 0 |
| `Cleansia.IntegrationTests` | **144 passed, 0 failed, 0 skipped** (1 m 27 s) | 0 |
| `Cleansia.HostTests` | **135 passed, 0 failed, 0 skipped** (1 m 52 s) | 0 |

**The AC7 baselines (2295 / 108 / 75) and the task's (3072 / 144 / 120) are both stale, and the reason
is worth recording:** this is a **shared working tree** — `git status` shows three other live backend
lanes' edits (`SaveOrderPhotos`, `OrderFactory`/`QuoteOrder`/`OrderPricingCalculator`, the five
`RequestLoggingMiddleware` copies + `CleansiaStartupBase`) plus frontend work in the same checkout. The
totals above therefore include those lanes' new tests, not only this ticket's 15. This ticket contributes
**+5 unit and +10 host**; nothing was removed, disabled or skipped.
