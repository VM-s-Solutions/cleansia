# Challenge — ADR-NNNN (the favourite cleaner is a reservation the cleaner must confirm)

**Challenger, single lane: `mechanism + promise`.** Reviewed against **HEAD**, not against the draft's
description of HEAD. Every `file:line` below is from a file I opened in this session. No number is
asserted that I did not derive from a constant I read. The draft's `## Defense` and `## Verdict`
sections are **empty** — the "prior round" it carries is the owner-answer round (`Q-ASSIGN-01…04`),
not a challenge round — so nothing below re-files a defended point; the four owner rulings are treated
as settled and are not reopened.

**Headline.** The load-bearing decision (§D2, no `OrderEmployee` row) survives: **all three of its legs
hold at HEAD, verbatim**, and I re-derived each rather than inheriting it (§What I checked). The
two-round derivation also survives my arithmetic — it is conservative in the **safe** direction (true
worst case **19%**, not the 20% the test bounds). What does not survive is (1) the section the draft
calls its biggest win, which describes work that **already shipped**; (2) a **standing owner ruling**
the draft never cites and directly contradicts; (3) a **re-grant invariant that is vacuous by
monotonicity**, which turns two of the draft's guarantees into no-ops; and (4) a **neutrality property
the draft pays for in copy and gives away in the next section**.

Nine findings. **CH-F1, CH-F2, CH-F3, CH-F4 and CH-F5 I consider blocking.** CH-F1 is the strongest.

---

### CH-F1 — BLOCKING. **§D10.2 is already shipped.** `order.cleaner_assigned`, its category arm, its shared notifier, both producers, its FCM arg map, its copy on both clients and its tests all exist at HEAD. R0 and R10 re-do finished work, and **verify #14 fails a correct codebase**

**The hole.** §D10.2 rules *"one new key, and it is NOT scoped to this perk"*, tables it in D10.3 as
**"yes"** (new), files it as tickets **R0** ("ships FIRST") and **R10**, and the Consequences promote it
to *"**a strictly bigger win than the perk**"*. The draft's front-matter warrants the census: *"Context
— **every citation below was re-verified by reading, 2026-08-08**"*, and the closing section says *"The
instruction to check rather than assume is what found it."*

**It was not re-verified. Every element of the ruling is in the tree today:**

| §D10.2 ruling | State at HEAD |
|---|---|
| Mint `NotificationEventCatalog.OrderCleanerAssigned` | `NotificationEventCatalog.cs:37` — **exists**, with a doc-comment that is near-verbatim the draft's proposed one (`:26-36`) |
| Add its `GetCategoryFor` arm → `OrderUpdates` | `NotificationEventCatalog.cs:89` — **exists** |
| **#1** `TakeOrder.Handler` produces it, `statusChanged` guard dropped **on the push only** | `TakeOrder.cs:275-276` — `OrderCleanerAssignedNotifier.NotifyCustomerOfAssignmentAsync`, called **outside** the guard; the guard survives on the status-track append (`:269-273`) and on the email (`:278`) — **exactly as ruled, already done** |
| `AdminReassignOrder.Handler` produces it too | `AdminReassignOrder.cs:102-103` — **exists** |
| One shared producer so the two cannot disagree | `src/Cleansia.Core.AppServices/Features/Orders/OrderCleanerAssignedNotifier.cs:17-41` — **exists**, and its doc-comment (`:12-15`) states the card-path argument the draft presents as a discovery |
| **#2** `order.confirmed` loses the cleaner claim | `cleansia_android/customer-app/…/values/strings.xml:1211` is **`"Booking confirmed ✅"`** — **already corrected** |
| The FCM arg map for the new key | `FcmMessageFactory.cs:33` — **exists** |
| Verify **#13** — `NotificationEventCatalog.OrderConfirmed` no longer returned by `TakeOrder.cs` | **already true**: the only production producers are `HandlePaymentNotification.cs:279` and `ConfirmRecurringOrder.cs:126` |
| Verify **#15** — the card-path test | `Cleansia.Tests/Features/Orders/OrderCleanerAssignedNotificationTests.cs`, incl. `Taking_An_Order_Never_Claims_A_Cleaner_Through_OrderConfirmed` (`:93-100`) and the category assertion (`:152`). Sibling: `OrderConfirmedHonestProducerTests.cs:66,92` |

**Verify #14 is worse than redundant — it is destructive.** It says *"`grep -rn "Cleaner found" src/` returns
nothing after R0. It currently returns three files… every one of them is reachable with no cleaner on
the job."* At HEAD it returns:

```
src/cleansia_android/customer-app/src/main/res/values/strings.xml:1213
    <string name="notification_cleaner_assigned_title">Cleaner found! 🎉</string>
src/cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings:24131  "value" : "Cleaner found! 🎉"
```

The string was **renamed and rebound** — `notification_cleaner_assigned_title`/`_body`
(`strings.xml:1213-1214`, body: *"A cleaner is assigned to your booking #%1$s"*), fired only by
`order.cleaner_assigned`, which only fires when an assignment row is created. **It is now the one
honest sentence in the set.** A reviewer executing #14 as written deletes correct customer copy for a
shipped event, in five locales, on two clients.

**How I know the census was read against a stale file, rather than mis-transcribed.** The draft's
§Context partner-event table cites `NotificationEventCatalog.cs:30 / :44 / :52 / :60`. HEAD has those
four constants at `:43 / :57 / :65 / :73`. **Every one is off by exactly 13** — the size of the
`OrderCleanerAssigned` block (`:26-37`) plus its blank line. So the whole notification census, including
the sentence *"⇒ This ADR adds ZERO partner-targeted notification events"*, was derived from a file
state that predates the key. (The same drift is visible, at 1 line, on `TakeOrder.cs`: the draft cites
the validator chain at `:46-71`, HEAD `:45-70`; the existence gate `:83-91`, HEAD `:82-90`;
`OrderEmployee.Create` at `:265`, HEAD `:264`. Harmless on its own; corroborating here.)

**Why it matters.** Three things, in increasing order of cost.
1. **R0 and R10 are wasted runs**, and R0 is scheduled *first* with *"depends on nothing"* — so the
   first thing a developer does on this feature is edit two strings that already say the right thing.
2. **The Consequences over-claim.** *"A live customer-facing false statement is retired for every
   customer… **This is a strictly bigger win than the perk**"* is a claim to have found and fixed
   something already fixed. An immutable ADR that takes credit for shipped work misleads every later
   reader about what this decision bought.
3. **It is contagious.** The draft's authority is its citation density. A reader who checks the one
   section the draft calls its strongest find, and finds it already shipped, has no basis for trusting
   the other ~120 citations. That is the actual damage.

**What I want.** §D10.2 is rewritten as a **precondition already met**, not a ruling: the key, the
notifier, both producers and the corrective copy are cited as shipped, and the section's remaining
content is the **one** thing that genuinely is not done — `NotificationFeedEventKeys.Customer`
(`:32-45`) still does **not** list `OrderCleanerAssigned`, while both clients now render it
(`partner`/`customer` template maps: `customer-app/…/NotificationTemplates.kt`,
`CleansiaCustomer/Tests/PushLocKeyCatalogTests.swift`). Under the file's own sequencing rule
(`NotificationFeedEventKeys.cs:26-28`) the client wave has been earned, so the feed listing is now
**owed** — that is a real, small, correctly-derived ticket, and it is the only survivor of R0+R10.
Verify #13/#14/#15 are deleted or restated as regression guards on shipped behaviour. And the
Consequences bullet goes.

---

### CH-F2 — BLOCKING. The reservation contradicts a **standing owner ruling** the draft never cites. `Q-PROMISE-01` (answered 2026-08-07): *"a cleaner is assigned within 1 hour, in PROD"* — **and the copy that says so is live and unconditional**. A single granted reservation breaks it for every lead over **10 hours**

**The hole.** The draft's whole customer-facing argument (§D5.2, §D10.3, verify #16) is about not
letting a **sentence outrun the mechanism**, and it derives its origin from `Q-PROMISE-02`. It never
mentions `Q-PROMISE-02`'s sibling, raised in the same challenger round on the same ADR-0036, answered by
the same owner:

```
agents/backlog/questions/open.md:1192
  Answer (owner, 2026-08-07): The promise is TRUE and must be kept: a cleaner is assigned
  within 1 hour, in PROD. The copy stays.
```

The copy is shipped, unconditional, on the booking-success screen of **both** mobile customer clients:

```
cleansia_android/customer-app/…/values/strings.xml:758   "Cleaner being assigned"
cleansia_android/customer-app/…/values/strings.xml:759   "Within 1 hour"
cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings:4986 / :4951   (same pair)
```

**The arithmetic, from the constants only.** `BookingPolicy.ComputePreferredHold` (`:171-180`) returns
`min(leadHours × PreferredHoldFraction, PreferredHoldCeilingHours)`, zero below `2 × StandardLeadTimeHours`;
`PreferredHoldFraction = 0.10m` (`:159`), `PreferredHoldCeilingHours = 12` (`:160`),
`StandardLeadTimeHours = 4` (`:20`).

- `0.10 × leadHours > 1 h ⟺ leadHours > 10`. **Above a ten-hour lead, round 1 alone withholds the order
  from the entire board for longer than the promise the owner has affirmed** — before a second round
  exists.
- The modal next-day booking (24 h lead): **2 h 24** — 2.4× the promise. That is ADR-0036's own
  table (`0036-…:290`).
- The draft's stated worst case: **24 h** (§D5.3) — 24× the promise.
- The band where it does *not* break: `leadHours ∈ [8, 10]`, holds of 48–60 min. That is the only band
  ADR-0036's own D3 table showcases (`:289`).

**Why the draft owns this even though ADR-0036 created it.**
1. It **doubles the exposure**: one round → two, Invariant H 90% → 80%, max withheld 12 h → up to 24 h.
2. It **makes the contradiction visible on one screen**. §D7.2 puts `respondByUtc` — *"the deadline; an
   instant, not a countdown"* — on the customer's order detail. The platform will now display
   "awaiting confirmation until 22:00" on an order whose booking-success screen said "Within 1 hour",
   minutes earlier, in the same app. ADR-0036 could plead invisibility; this design explicitly ends it.
3. **Invariant H cannot enforce the ruling, by type.** Invariant H is a *share* of the fill window; the
   owner's ruling is an *absolute duration*. 20% of a 168-hour fill window is 33.6 hours. §D5.3 pins
   `MaxPreferredOfferRounds × PreferredHoldFraction ≤ 1 − MinimumOpenBoardShare` (verify #8) and calls
   that *"the whole of Invariant H's enforcement"* — and it is structurally incapable of bounding the
   one number the owner has committed to.
4. It is **exactly the defect class the ADR was raised to fix**, aimed at the Plus customer: the perk
   they pay for is what makes the platform's affirmed promise false for them specifically, and §AC4
   forbids ever telling them why.

**What I want.** One of three, decided in the ADR, not left to a reader:
- **(a)** A named **absolute** ceiling that composes with the share ceiling — e.g. the granted hold is
  `min(lead × fraction, ceiling, PromisedAssignmentWindow)` — pinned by the same test idiom as
  `cap <= floor`, so the promise and the perk cannot move independently; or
- **(b)** the booking-success copy is scoped so it is not shown on a reserved order (a client change
  the draft does not budget, and a five-locale × two-client wave); or
- **(c)** an **escalation** naming the collision precisely — *"Q-PROMISE-01 says 1 hour; a reservation
  is up to 12 h per round × 2. Which gives?"* — since only the owner can retire either.

Silence is not an option here: the two rulings are both on file, both dated 2026-08-07/08, and this
ADR is the first document that has to hold both.

---

### CH-F3 — BLOCKING. `GrantPreferredHold`'s re-grant invariant *"`untilUtc >` the current value"* is **vacuous** — it is satisfied for every re-grant, monotonically. So the eviction it names is exactly what it permits, and it lets a **zero-length** reservation burn the customer's last round

**The hole.** §D5.1 widens `GrantPreferredHold` and lists four structural invariants, of which this one
carries a stated safety purpose:

> `untilUtc > the current value` *(a re-grant may never SHORTEN a live reservation, which would be a way
> to evict a beneficiary silently)*

**Derivation.** Let `L` = lead hours at creation, `t` = hours elapsed when the re-grant runs. The new
deadline is `untilUtc(t) = t + min(0.10·(L − t), 12)` (`ComputePreferredHold`, `BookingPolicy.cs:171-180`);
the stored value is `untilUtc(0)`. Differentiate:

- below the ceiling: `d/dt [t + 0.10(L − t)] = 0.9 > 0`
- at the ceiling: `d/dt [t + 12] = 1 > 0`
- below the 8-hour floor the hold is `0`, so `untilUtc(t) = t`, and `t > 0.10L ⟺ t > 0.10L` — which
  holds for every `t > L − 8` whenever `L > 8.89`

**`untilUtc` is strictly increasing in `t`. For any `t > 0` the guard passes.** It refuses nothing it
was written to refuse. Two consequences the draft states the opposite of:

1. **Silent eviction of a live beneficiary is permitted, not prevented.** §D5.1's only listed refusals
   are *the same `EmployeeId`* and *any cleaner assigned*. Neither requires the current reservation to
   have **ended**. So: customer books, cleaner A is pushed *"A customer asked for you"*
   (`OrderFactory.cs:192-205`), three minutes later the customer picks B, the guard passes (per above),
   `PreferredEmployeeId` is overwritten, and A's offer evaporates. Under ADR-0036 that was invisible
   because A was never told anything (D4: *"exclusivity is invisible to the excluded"*). **Under this
   ADR A was told the job was theirs to confirm.**
2. **A zero-length reservation is grantable and burns the round.** Once `L − t < 8`, `ComputePreferredHold`
   returns `TimeSpan.Zero` and `PreferredCleanerHoldResolver.cs:95-98` returns
   `NotifyOnly(ShortLeadTime, …)`. If `ChoosePreferredCleaner` grants on that outcome, the guard still
   passes (shown above), `PreferredOfferRound` reaches the cap, the state is `None` (§D7.1 ruling), the
   sweep fires `order.preferred_offer_closed` on the next 5-minute tick — and the customer has spent
   their final round on nothing.

**What I want.** The invariant is replaced by one that can fail: **a re-grant requires
`PreferredHoldUntilUtc <= nowUtc` OR `PreferredEmployeeId == the same beneficiary`** (i.e. the current
reservation has ended, or you are extending the same person's). And §D5.1 rules explicitly what happens
on `NotifyOnly` — see CH-F4, which is the same branch from the customer's side. Both are cheap; both
are currently unwritten and the draft asserts the opposite property.

---

### CH-F4 — BLOCKING. `canChooseAnother` is specified without the only term that decides whether a second round can exist. The 8-hour floor makes the customer's exit unreachable over a large, computable share of exactly the window in which they are being asked to use it

**The hole.** §D7.2 specifies the flag the whole customer-facing exit hangs on:

```
canChooseAnother: bool   // PreferredOfferRound < MaxPreferredOfferRounds && no assignment
```

**Two terms. Neither is the lead time.** But `ComputePreferredHold` returns `TimeSpan.Zero` below
`2 × StandardLeadTimeHours = 8 h` (`BookingPolicy.cs:174`), and the resolver turns that into
`NotifyOnly` rather than `Granted` (`PreferredCleanerHoldResolver.cs:94-100`). So a second **reservation**
is impossible whenever the remaining lead is under 8 hours — and the flag says it is available.

**How large is "large".** Round 1 lapses at `creation + min(0.10L, 12)`. The customer's remaining window
is then `L − min(0.10L, 12)`, of which the **last 8 hours are dead**:

| Lead at creation `L` | Round-1 hold | Window left after lapse | Dead tail | Share of the window in which the exit is unreachable |
|---|---|---|---|---|
| 8 h | 48 min | 7 h 12 | 7 h 12 | **100%** |
| 12 h | 1 h 12 | 10 h 48 | 8 h | **74%** |
| 24 h | 2 h 24 | 21 h 36 | 8 h | **37%** |
| 120 h | 12 h (ceiling) | 108 h | 8 h | 7% |

The lapse notification is the trigger; the whole reason it exists (§D6) is that the customer *was not
watching*. So the customer most likely to tap "choose another" is the one who reads the push hours
later — which is the population the dead tail is made of.

**And a second, disjoint instance the draft states as correct.** §D7.1 rules that
`PreferredOfferState.None` covers *"**the entire 2–8 h notify-only band**, which is correct: in that
band nothing is withheld."* Correct — but in that band `GrantPreferredHold` never ran, so
`PreferredOfferRound == 0`, so `canChooseAnother == 0 < 2 == true`. **The customer of a same-day
booking is offered a second choice for a perk that never applied and provably cannot.**

**Why it matters.** §D3.d calls the customer-facing exit one of the four things the reservation gains,
and §AC1 makes it the third clause of the ruling. A flag that is `true` when the server will refuse is
not an exit — it is a button that fails, on the screen where the customer is already waiting, in the
one flow this ADR exists to build. And it is not caught by any of the 16 verification steps.

**What I want.**
1. `canChooseAnother` gains the lead-time term, expressed through the **existing** function, not a
   re-implementation: `BookingPolicy.ComputePreferredHold(order.CleaningDateTime, nowUtc) > TimeSpan.Zero`.
2. §D5.1 rules the `NotifyOnly` outcome explicitly — **refuse** (with `order.preferred_offer_closed`,
   the key it already mints) rather than grant-and-burn. A `NotifyOnly` re-offer would push a named
   cleaner about an order that is open to everyone, which is a different product from the one §AC1
   describes.
3. §D7.1's *"`None` covers the notify-only band, which is correct"* is amended: correct for the
   **state**, wrong for the **affordance**.

---

### CH-F5 — BLOCKING. The neutrality §D7.3 buys with copy is given away by §D6.4 and §D7.2 in the same document. The **arrival time** of `order.preferred_offer_closed` discloses decline-vs-silence, and the ADR hands the customer the reference clock

**The hole.** §AC4 and §D7.3 rule that one sentence covers both endings and that the customer is *"never
told that a specific person refused, and never told that a specific person did not answer."* Ground 1
is a **legal** argument — *"`Q-AVAIL-04` — which lawful basis covers that — is open… Shipping the
strongest form of the disclosure while the weakest form's basis is unresolved is the wrong order."*

Two other sections defeat it:

- **§D6.4**: *"`DeclinePreferredOffer` must tell the customer **immediately**, not in ≤5 minutes."*
- **§D7.2**: `respondByUtc` — the exact deadline — is on the customer's order detail, deliberately, as
  *"a fact, and it is the fact the customer needs because §D6's prompt arrives at roughly that time."*

So the customer holds `respondByUtc`, and the message arrives either **at** it (a lapse, ±5 min) or
**far before** it (a decline). On a 24-hour booking the gap is up to 2 h 24; on a 5-day booking, up to
12 h. **The bit the copy withholds is fully recoverable from a timestamp the ADR chose to disclose and
a delivery time the ADR chose to make immediate.** Nothing about *"they're not available"* has to be
said; the phone buzzing four minutes after booking says it.

**Why it matters.** This is not a copy nit — it is the ADR asserting a property it does not have, and
resting an open legal question (`Q-AVAIL-04`, re-scoped from notice to **basis** by the ADR-0039 panel)
on that assertion. If the inference is available anyway, the ADR's posture on `Q-AVAIL-04` is not
"we withheld it pending the basis"; it is "we withheld the wording."

There is also a real trade-off underneath that the draft never names: **the immediate decline is the
only thing the decline buys.** Delay it to the deadline and §D3.b's *"a decline — one tap"* stops
helping the customer at all and becomes purely a cleaner-side affordance. That is a genuine choice and
it deserves a row, not silence.

**What I want.** §D7.3 either (a) concedes the timing channel in writing and argues why it is
acceptable (a plausible answer: the customer already knows *when* they booked and *what* the deadline
is, so this discloses no more than the deadline itself does — but it must be **argued**, since the
legal ground is built on the opposite claim), or (b) closes it (announce both outcomes at the deadline,
or on a bounded random delay), or (c) drops `respondByUtc` from the customer DTO. As written, §D7.3
claims a guarantee that §D6.4 removes on the next page.

---

### CH-F6 — Non-blocking, but it invalidates the ticket bill. The announcement predicate is **wider** than the reservation predicate, and the draft renames the announcement without narrowing it, without changing its copy, and without a wire path for the deadline

Three separate gaps in one place. The draft's D10.1 table records the cleaner-side event as *"`order.preferred_offer` — **no** — shipped"* and the Consequences bill the partner side as *"a pending-offers surface + a decline"*. All three of the following fall outside that bill.

**(a) The push fires in states where there is no reservation.** `OrderFactory.cs:184` grants on
`preferredCleaner.HoldUntilUtc`; `OrderFactory.cs:192` pushes on `preferredCleaner.Recipient` — and
`NotifyOnly` carries a `Recipient` with no `HoldUntilUtc` (`PreferredCleanerHoldResolver.cs:97`). ADR-0036
D4.1 made that width a **feature** for a silent hold. Under a named assignment it means the cleaner is
told they were chosen for an order the whole board can take from them, and `GetMyPendingOffers` (§D9,
`PreferredHoldUntilUtc > nowUtc`) **will not list it**. Push, then an empty surface.

**(b) The push fires before the order is confirmable.** A card order sits at `New` + `Card` +
`PaymentStatus.Pending` until the webhook (`OrderAvailability.cs:19-25` documents both retractors), and
`IsOfferable` requires `Paid` for card (`:60-63`). §D9 correctly conjoins `IsOfferableSql` into
`GetMyPendingOffers` — so again: push lands, surface is empty, and if the cleaner follows the deep link
they reach the detail anyway (`OrderAccessService.cs:88-91` omits the offerability conjunct — the draft's
own ⚠️, filed as F1) and `TakeOrder` refuses at `:55-56` with `order.not_takeable`. §D9 names the browse
half and files it; it does not name that the **push** is what walks the cleaner into it.

**(c) The shipped partner copy is priority language, and the deadline has no wire path.** At HEAD, in
five locales on the partner Android app (and the mirror on iOS):

```
partner-app/src/main/res/values/strings.xml:1244  "A customer asked for you"
partner-app/src/main/res/values/strings.xml:1245  "Order %1$s — someone you've cleaned for before
                                                   requested you. Open it to take the job."
```

*"asked for you… open it to take the job"* is **exactly the priority framing this ADR exists to
replace**. And the deadline the design makes load-bearing cannot appear in it: the event's args are
`orderId` + `orderNumber` only (`OrderFactory.cs:197-201`), and the FCM template map is per-event
(`FcmMessageFactory.cs:32-33`), so a `respondByUtc` arg is a backend change **plus** both partner
clients. Not in R5/R7, not in the Consequences bill.

**What I want.** Either the push is narrowed to `Granted ∧ IsOfferable` (and D4.1's wider-notify rule is
explicitly superseded for the *assignment* framing, keeping it for the notify-only one), or the copy is
split into two keys and the ADR budgets the partner-side five-locale × two-client copy wave and the
deadline arg. Right now the ADR says the partner notification side costs nothing, and it costs a wave.

---

### CH-F7 — Non-blocking. The two-round derivation is **sound**, but §D5.3's stated worst case is arithmetically wrong: 24 h needs a **132**-hour fill window, not 120. At 120 h the total is 22 h 48

I re-derived the invariant rather than accepting it, and it holds — in the safe direction.

Let `L` = lead hours at creation, `h₁ = min(0.10L, 12)`, and the re-offer at `t ≥ h₁` giving
`h₂ = min(0.10(L − t), 12)` (zero below the 8 h floor). Withheld intervals are `[0, h₁]` and `[t, t+h₂]`,
so total withheld `= h₁ + h₂`, maximised at `t = h₁`:

- `L ≤ 120`: `h₁ = 0.10L`, `h₂ = 0.10(0.9L) = 0.09L` → total `0.19L` → **19%**
- `L > 120`: `h₁ = 12`, `h₂ = min(0.10L − 1.2, 12)` → share `(12 + h₂)/L`, which is 19% just above 120
  and **falls** thereafter (2.4% at `L = 1000`)

**Maximum share over all `L`: 19%.** The test `MaxPreferredOfferRounds × PreferredHoldFraction ≤ 1 −
MinimumOpenBoardShare` (`2 × 0.10 ≤ 0.20`, verify #8) is therefore a **conservative over-estimate** — it
bounds a quantity strictly larger than the real one, which is the right direction and worth saying out
loud in the ADR, because a later reader will otherwise think the invariant is tight at equality and be
afraid to touch either number for the wrong reason.

**The error.** §D5.3: *"the absolute worst case is 24 hours of a fill window that is at least 120 hours
long."* Two rounds both at the 12 h ceiling requires `L − 12 ≥ 120`, i.e. `L ≥ 132`. At exactly `L = 120`
the total is `12 + 10.8 = 22.8 h`. The pairing "24 h / 120 h" is 20% and appears to sit exactly on the
bound, which is what makes it read as a proof; the true pair is either 22.8/120 (19%) or 24/132 (18.2%).
Conclusion unaffected; the sentence that carries it is wrong.

**Also worth stating so the round counter is not re-derived:** `MaxPreferredOfferRounds = 2` with a
`PreferredOfferRound < 2` guard and creation counting as round 1 is **correct** — creation `0→1`,
re-offer `1→2`, third refused; and `canChooseAnother`'s `Round < Max` is `true` after creation and
`false` after the re-offer. I checked this because it is the kind of off-by-one that survives review.

---

### CH-F8 — Non-blocking. `ChoosePreferredCleaner` on a recurring occurrence **works, is per-occurrence, and silently reverts next week**. §D6.2 suppresses the prompt but not the action

`MaterializeRecurringBookingTemplate.cs:240` carries `template.PreferredEmployeeId` into every
occurrence. §D6.2 rules the *lapse prompt* off for `RecurringTemplateId != null` — correctly, and for
the right stated reason. But §D5.1's structural refusals are only *same `EmployeeId`* and *already
assigned*; nothing refuses a recurring occurrence. So a customer who opens next Tuesday's occurrence
and picks a different cleaner changes **that row only**; the template is untouched (`F3` is filed as
*not built*), and the following week the materializer re-grants to the original favourite.

Three inconsistencies follow, none fatal, all cheap to rule: the customer's choice appears to persist
and does not; `PreferredOfferRound` is per-occurrence so the cap resets weekly (an unbounded number of
lifetime rounds on a recurring booking, which Invariant H does bound per-seat but the ADR's *"exactly
one re-offer"* framing does not describe); and the re-offer's own push to the newly chosen cleaner is
**not** suppressed by D6.2, which only names the customer prompt — so the interruption D6.2 exists to
prevent reappears on the supply side.

**What I want.** One sentence in §D6.2: whether `ChoosePreferredCleaner` is refused on
`RecurringTemplateId != null` (consistent with the prompt suppression, and honest — the customer cannot
usefully act per-occurrence) or permitted with the reversion stated. Either is defensible; neither is
written.

---

### CH-F9 — Non-blocking. The front-matter's *"composes with ADR-0035 — §D8 is where the two perks collide over a cancellation"* is false by construction: express and the reservation are **disjoint**

`RequiresExpressSurcharge` is `leadHours >= ExpressLeadTimeHours && leadHours < StandardLeadTimeHours`
— lead ∈ [2, 4) (`BookingPolicy.cs:130-140`, `:20`, `:26`). `ComputePreferredHold` returns zero below
`2 × StandardLeadTimeHours` = 8 h (`:174`). **An express booking can never carry a reservation**, so the
two perks cannot collide over a cancellation or anything else. §D8's express-waiver row (*"still
released on that cancel, because it keys on the same boolean"*) is true and vacuous for every order
this ADR is about.

Small, but it is in the composition list a later reader uses to decide which ADRs to open, and a false
composition claim sends them to the wrong document.

---

## What I checked and found sound

Silence is not assent. I opened each of these and they hold exactly as the draft states.

- **§D2's three legs, all at HEAD, re-derived rather than inherited — the load-bearing decision stands.**
  1. `OrderRepository.GetEmployeeOrderCountThisWeekAsync` (`:245-257`) counts
     `o.AssignedEmployees.Any(e => e.EmployeeId == employeeId)` over the UTC week with **no status term
     and no confirmation term**, feeding the 3/6/10 rating tiers at `TakeOrder.cs:201-206`. A pending row
     would spend the cap. ✔
  2. `LiveCommitmentsInWindow` (`OrderRepository.cs:318-333`) + `:282-284` is the one overlap predicate,
     read by both `HasOverlappingOrderAsync` (the `TakeOrder` conflict gate, `:222-226`) and
     `GetBusyEmployeeIdsInWindowAsync` (`:302-307`, ADR-0039's picker). A pending row would block the
     calendar. ✔
  3. `CancellationAssessor.cs:55` is verbatim `var hasBeenAccepted = order.AssignedEmployees.Count > 0;`,
     driving `BookingPolicy.ClassifyCancellation`'s `FreeNotAccepted` arm (`:252-255`) **and**
     `CancelOrder.cs:143-146`'s express-waiver release. A row at creation really would make every
     favourite booking fee-bearing and waiver-consuming. ✔
  **All three hold. §D2 is the right call and A1's rejection is correctly grounded.**
- **`OrderVisibility.cs` and `OrderAvailability.cs` are exactly as described**, and verify #2/#3
  ("byte-unchanged") are meaningful checks. Term 5 (`AssignedEmployees.Any()`, `OrderVisibility.cs:41`,
  `:52`) really does release the remaining seats on first assignment, so §AC1's "the order's seats" and
  `CLAUDE.md`'s "first seat" describe the same rule.
- **§D6.1's regression argument is real.** `NewJobsDigestService.cs:262-265` and `:272-275` both read the
  `(PreferredEmployeeId, PreferredHoldUntilUtc)` pair as a bounded freshness window. A sweep that nulled
  the pair would erase it. The receipt-column ruling is correct and verify #9 is the right guard.
- **§D9's ⚠️ is accurate.** `OrderAccessService.cs:88-91` conjoins `HasAvailableSpots` and
  `NotHeldFrom` and **not** `IsOfferableSql`. F1 is correctly scoped.
- **§D3/A13's "the confirm is `TakeOrder`, unchanged" survives gate-by-gate.** I walked the single
  ordered chain (`TakeOrder.cs:45-70`) asking whether each gate still makes sense for a caller who was
  *reserved* rather than *browsing*: existence-with-hold passes via `NotHeldFrom` term 4; cancelled /
  completed / seats / caller-is-employee / profile / approval / already-assigned / weekly cap / conflict
  are all still the right questions for a confirmation. **Only `IsOfferableAsync` behaves differently in
  kind** — it is the one gate that can refuse a *validly reserved* beneficiary for a reason that is not
  about them — and that is CH-F6(b), not an argument against reusing the command. A13's rejection of a
  dedicated `ConfirmPreferredOffer` holds.
- **`CreateRecurringBooking` has no Plus gate** (`:76-82` runs only `PreferredEmployeeIsEligibleAsync`,
  unlike `CreateOrder.cs:162-171`) — but this is **not** a hole in the ADR's reliance on ADR-0036 D7: the
  resolver re-checks membership at materialization (`PreferredCleanerHoldResolver.cs:42-47`), so no hold
  is granted for a lapsed member. Reject-vs-degrade, matching `MaterializeRecurringBookingTemplate.cs:238-239`.
- **The living doc really is as stale as the draft says**, and worse: `preferred-cleaner-dispatch.md:30`
  *"Nothing is shipped yet"*, `:50` *"**Consumption** | **None.** No query, no ordering, no notification,
  no assignment reads `PreferredEmployeeId`"*, and `:48` *"No membership check"*. All three false at HEAD.
  The draft's commitment to fix it at acceptance is right and should be treated as part of the decision,
  not a chore.
- **`Order.PreferredOfferRound`, `Order.PreferredOfferLapseNotifiedAt` and
  `BookingPolicy.MaxPreferredOfferRounds` do not exist** — grep over `src/` returns nothing. The two new
  columns really are new; `Order.RecurringReminderSentAt` (`Order.cs:275-281`) really is the precedent
  the draft cites.

---

## Verdict requested of the lead

**Blocking (5).**

| # | Finding | Why it blocks |
|---|---|---|
| **CH-F1** | §D10.2 / R0 / R10 / verify #13–15 describe already-shipped work; verify #14 would delete correct copy | An immutable ADR cannot record shipped work as its own deliverable, and one of its verification steps must not fail a correct codebase. The stale-by-13-lines citation pattern means the census, not just the conclusion, needs redoing |
| **CH-F2** | Contradicts `Q-PROMISE-01` (owner, 2026-08-07 — *"a cleaner is assigned within 1 hour, in PROD"*, copy live at `strings.xml:758-759`); any lead > 10 h breaks it on round 1 alone | Two live owner rulings collide and this is the first document that must hold both. Invariant H is a *share* and cannot bound an *absolute* SLA. §D7.2 makes the contradiction visible on one screen |
| **CH-F3** | The re-grant invariant `untilUtc > current` is vacuous by monotonicity | The ADR states a safety property it does not have; the silent eviction it names is permitted, and a zero-length reservation can burn the customer's final round |
| **CH-F4** | `canChooseAnother` omits the lead-time term; the 8 h floor makes the exit unreachable over 37–100% of the post-lapse window, and it is `true` for the entire notify-only band | The customer-facing exit is one of §AC1's three clauses and one of §D3's four gains; as specified it is a button the server refuses |
| **CH-F5** | §D7.3's neutrality is defeated by §D6.4 (immediate decline) + §D7.2 (disclosed `respondByUtc`) | The ADR rests an open legal question (`Q-AVAIL-04`) on a property the same document removes two sections later |

**Non-blocking (4).** CH-F6 (announcement predicate wider than the reservation; partner copy + deadline
arg missing from the bill — **affects the ticket list, so worth folding in before the PM files**);
CH-F7 (D5.3's 24 h / 120 h pairing is wrong — 132 h; the derivation itself is sound at a true 19% max);
CH-F8 (`ChoosePreferredCleaner` on a recurring occurrence is unruled); CH-F9 (the ADR-0035 composition
claim is false by construction).

**Explicitly not challenged, and not to be re-litigated:** the four owner rulings (`Q-ASSIGN-01…04`),
the 8-hour floor (owner-ruled on ADR-0036 CH-2), the 12-hour ceiling, `MaxPreferredOfferRounds = 2`,
the choice of a reservation over a true assignment, and §D2's no-`OrderEmployee`-row decision — which I
attacked hardest and which won.

**One question I believe is genuinely open despite what is on file.** Not `Q-ASSIGN-01…04` — those are
answered and CH-F2 does not reopen any of them. It is the **collision between two already-answered
questions**: `Q-PROMISE-01` (the 1-hour assignment promise is true and must be kept) and `Q-ASSIGN-02`
(two rounds of up to 12 h each stand). Both were answered without the other in view — `Q-PROMISE-01` on
2026-08-07 against the *silent* ADR-0036 hold, `Q-ASSIGN-02` on 2026-08-08 against a *disclosed*
reservation whose escalation text framed the cost as a **share** of the fill window and never named the
hour. Neither answer is wrong; they are jointly unsatisfiable above a ten-hour lead, and no agent may
pick which promise the platform breaks. That is the escalation this ADR owes, and it is a different
question from the one §Open already offers the PM (*"is 'we're still looking' the final state?"*),
which is about the **terminal** state — CH-F2 is about the **first** state, where the platform has
already made a number-bearing promise on a screen that ships today.

---

**File:** `agents/backlog/adr/challenges/NNNN-favourite-cleaner-reservation.md`
**Draft under challenge:** `agents/backlog/adr/drafts/NNNN-favourite-cleaner-is-a-reservation-the-cleaner-must-confirm.md` — **not edited by me.**
