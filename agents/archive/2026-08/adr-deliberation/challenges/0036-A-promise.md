# Challenge A — ADR-0036, the customer promise

- **Role:** `analyst`, **challenger** mode (Challenger A, the lane the author named in §Verdict).
- **Artifact:** `docs/decisions/adr-0036.md` (`proposed`, author-only).
- **Method:** every claim below is a **read** of the working tree, 2026-08-02. Nothing was run. Every
  copy claim was read in **all five locales** of every client that carries the string, not just `en`.
- **Bottom line:** **six blocking findings**, one ruling (CH-2 → **8 hours**), and a list of what held up.
  The mechanism (D1/D2/D5) is not what I am attacking — I think the stored-deadline hold is right. What
  I am attacking is that **the ADR designs a silent mechanism into a product that has already made the
  customer a loud, numeric, contradicting promise**, and then declares the copy "not blocking
  acceptance." That is the ADR-0035 failure re-run with the roles reversed: last time we shipped copy
  without a mechanism; this time we would ship a mechanism without the copy.

---

### CH-P1 — Both clients that have the picker promise the customer **"Cleaner being assigned · Within 1 hour"** on the screen immediately after booking. D3 grants holds of up to **12 hours**, and D6 shows the customer nothing. **[BLOCKING]**

**The hole.** The ADR's entire latency argument (D3 Invariant H, D6 "no countdown", the CH-5 defense
*"a silent 30-minute delay is worse than the perk is better — but only if the customer is watching
it"*) rests on the premise that the customer has **no expectation** about time-to-assignment. That
premise is false. The platform states an expectation, as a number, unconditionally, in five languages,
on both clients that have the picker:

- Android: `src/cleansia_android/customer-app/src/main/res/values/strings.xml:741-742`
  ```xml
  <string name="booking_success_t2_title">Cleaner being assigned</string>
  <string name="booking_success_t2_desc">Within 1 hour</string>
  ```
- iOS: `src/cleansia_ios/CleansiaCustomer/Resources/Localizable.xcstrings:4799-4812` (`booking_success_t2_desc` = *"Within 1 hour"*, cs *"Do 1 hodiny"*) and `:4834-4847` (`booking_success_t2_title`).
- It is **unconditional**. `src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/BookingSuccessTimeline.swift:10-14` is `CaseIterable` over `received → assigning → confirmed → cleaningDay`; `:44-46` makes `assigning` `.active` whenever no order status has loaded — i.e. exactly the moment after submit. `:28` binds the subtitle. Android is the parity source (`BookingSuccessScreen.kt`, referenced at `BookingSuccessTimeline.swift:41`).

Now put D3's table (ADR `:171-180`) next to it:

| Lead at creation | Hold granted | vs. the promised "Within 1 hour" |
|---|---|---|
| 4 h | 24 min | **40% of the promised hour spent on one person** |
| 24 h (next day) | 2 h 24 | **2.4× the promise, before the board has seen the order** |
| 72 h | 7 h 12 | **7.2× the promise** |
| ≥ 120 h (every recurring occurrence) | **12 h** | **12× the promise** |

**Why it matters.** This is not "the customer might be mildly surprised." A Plus member books three days
out, picks Anna, taps confirm, and reads *"Cleaner being assigned — Within 1 hour."* The system has at
that instant guaranteed the order is invisible to the entire board for **7 hours 12 minutes**. The
customer who opens the app at hour two sees the assigning step still active and no cleaner. D6 has
decided they get no explanation, because D6 assumed there was nothing to explain. **The correction this
platform just went through was "express" being described as same-day; the shape is identical — a
customer-facing time claim that the mechanic contradicts — and here we would be *creating* it, not
inheriting it.**

The author's own defense of CH-5 says the perk is acceptable-because-invisible: *"a silent 30-minute
delay is worse than the perk is better — but only if the customer is watching it."* `booking_success_t2`
is the platform **telling the customer to watch it**, with a stopwatch, on the next screen.

**What I want changed (any one of these; the ADR must pick one, not defer it):**
1. **Lower `PreferredHoldCeilingHours` to a number that does not exceed the platform's own stated
   assignment window** — i.e. ≤ 1 h — and keep the "Within 1 hour" copy; **or**
2. **Delete the "Within 1 hour" claim from both clients × five locales in the same wave as C2a/C2b**,
   under ADR-0035's own corrective-ships-first rule (see CH-P7), replacing it with a non-numeric
   phrase; **or**
3. **Make the assigning-step subtitle conditional on the hold**, which requires the order DTO to carry
   *something* — which D6 forbids and D2 makes cheap (`PreferredHoldUntilUtc` already exists; the
   customer's own order DTO may carry it per §verify #5).

Option 2 is the cheapest and I recommend it. But **"defer the copy to T-0491 and accept the ADR" is not
available**: the ADR would then be accepting a decision whose known consequence is a false statement to
a paying customer, with no ticket that must land before the mechanism does.

---

### CH-P2 — D6's *"told once, at booking"* has **no surface to live on**: both pickers overwrite the only explanatory string with the cleaner's name at the exact moment the customer chooses. **[BLOCKING]**

**The hole.** D6 (`:354`, `:368`) rests the whole customer promise on *"one sentence at the moment of
choosing."* I read the two components that render that moment. In both, the explanatory sentence is the
**`?:` fallback** for the selected cleaner's name — so it exists only while the customer has chosen
*nobody*, and is destroyed by the act the ADR says it explains:

- Android `src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/booking/PreferredCleanerPicker.kt:131-135`
  ```kotlin
  Text(
      text = selected?.fullName ?: stringResource(R.string.booking_preferred_cleaner_subtitle),
  ```
- iOS `src/cleansia_ios/CleansiaCustomer/Sources/Features/Booking/Confirm/ConfirmExtrasComponents.swift:71-73`
  ```swift
  Text(selected?.fullName ?? L10n.Booking.preferredCleanerSubtitle)
  ```

There is **no other text** in either component. The row is: icon · title · one line · chevron. After
selection the customer sees a name in the primary accent colour and nothing else — which reads as
confirmation that *this person is coming*, not as an explanation that they get a head start.

**Second half of the hole: the sentence cannot be written truthfully as a static string anyway.** The
resolver (D5.1) can decline the hold for **seven** reasons (`HoldDeclineReason`, ADR `:295-296`). Four of
them — express lead time, cleaner unapproved, wrong `WorkCountryId`, muted category — are decided
*after* the customer taps, and three of those four are facts about the cleaner that D4's privacy line
(`:254-256`) forbids showing. So a single static string is **false in every decline case** and the
customer has no way to tell which case they are in. The ADR's §Copy anchor sentence (`:695-696`)
— *"they get first chance at your booking"* — is exactly this false-in-the-decline-case sentence.

**Why it matters.** The ADR's answer to "is this perk observable?" (Defense to CH-5) is *"the customer is
told what happens before it does not work."* Today there is no place to tell them, and no string that is
true in all cases. Ship as specified and the customer is told **nothing** — which is the state the ADR
believes it is fixing.

**What I want changed.**
1. **The resolver must be called at quote time and its `Granted` boolean must reach the client.** The
   ADR already specifies the resolver as `PURE READ. Never writes. Safe to call from the quote path and
   from the factory` (`:286`). It has the mechanism and does not use it. Expose **one boolean and no
   reason** (`firstChanceApplies`) — that leaks nothing, because a customer cannot distinguish "too
   soon" from "Anna can't be reached", and both resolve to the same sentence.
2. **The picker must render a persistent second line that survives selection**, in both clients. This is
   a customer-client change on iOS and Android — and **D10 budgets none.** C1 is sized `S` with "a
   client-side picker gate"; C2b's client work is "loc-keys on **both partner clients**". The customer
   clients get zero. **The ADR under-sizes its own copy requirement.**
3. **Here are the two sentences.** I was asked to write them; both are true against the code as
   specified and neither is alarming:

   **Hold granted:**
   > *"Anna gets first chance at this booking. If she can't take it, we open it to every cleaner right
   > away — your cleaning time doesn't change."*

   **Hold not granted (true for all seven decline reasons, leaks nothing):**
   > *"We can't hold this one for Anna — it's going straight to every cleaner so someone can get to you."*

   I could **not** write a truthful comforting variant of the second that mentions the stored preference
   (*"we'll still note your request"*). It is stored (`OrderFactory.cs:124`) and read by nothing —
   saying it would be the ADR-0035 failure in miniature. **That clause must not appear.** If T-0491
   writes it, this challenge stands.

---

### CH-P3 — The label the customer taps says **"Request"**, and three of the five web locales promise **assignment**. First refusal is neither, and the ADR read only the English string. **[BLOCKING]**

**The hole, part 1 — the picker title.** The ADR's §Copy constraint 1 (`:680-682`) targets the *perk
description* (`membership_perk_favorite_cleaner_desc` — *"Request the same cleaner you trust on every
booking"*). It never touches the **title of the control the customer actually taps**, which is a request
verb in all five locales on both mobile clients:

| Locale | Picker title | Literal |
|---|---|---|
| en | `values/strings.xml:722` — *"Request your favorite cleaner"* | request |
| cs | `values-cs/strings.xml:712` — *"Vyberte si oblíbeného uklízeče"* | choose |
| sk | `values-sk/strings.xml:709` — *"Vyberte si obľúbenú upratovačku"* | choose |
| ru | `values-ru/strings.xml:709` — *"Запросить любимого клинера"* | **request** |
| uk | `values-uk/strings.xml:709` — *"Запросити улюбленого клінера"* | **request** |

iOS carries the identical set (`Localizable.xcstrings:2874-2907`). "Request X" means *I am asking for
that person*. It does not mean *that person gets a 24-minute head start*. The ADR's D1 says the honest
name of the product is "first chance" — **then the control must say so**, and the constraint list does
not require it.

**The hole, part 2 — the ADR read one locale of the web string and cited the wrong line.** ADR `:677-679`
says *"only the web string promises prioritisation (`cleansia.app en.json:1097`)"*. Two errors:

- The line is **`en.json:1095`**, not `:1097` (`:1097` is `cancelled_until`). A copy ticket implemented
  from that citation edits the wrong string.
- **Three of the five web locales are stronger than the English and promise *assignment*, which AC3
  guarantees will never happen:**

| File:line | String | Literal |
|---|---|---|
| `apps/cleansia.app/src/assets/i18n/en.json:1095` | *"they'll be prioritized when matching"* | priority |
| `apps/cleansia.app/src/assets/i18n/uk.json:1095` | *"матиме пріоритет при підборі"* | priority |
| `apps/cleansia.app/src/assets/i18n/cs.json:1095` | *"bude **přednostně přiřazen**"* | **will be preferentially ASSIGNED** |
| `apps/cleansia.app/src/assets/i18n/sk.json:1095` | *"bude **prednostne priradený**"* | **will be preferentially ASSIGNED** |
| `apps/cleansia.app/src/assets/i18n/ru.json:1095` | *"он **будет назначен в первую очередь**"* | **will be ASSIGNED first** |

Rendered by `libs/cleansia-customer-features/profile/src/lib/membership/membership-subscribe.component.html:102-103`
— i.e. **on the page where the customer pays for Plus.** A Czech or Russian customer is told, at the
point of sale, that their chosen cleaner **will be assigned**. ADR-0036's AC3 (`:33-38`) states in
its own first sentence that *"a preferred cleaner who does nothing is never assigned anything."*

**And the web cannot deliver any of it.** `order-wizard.facade.ts:576-580` sends `preferredEmployeeId:
undefined` unconditionally; there is no picker. `apps/cleansia.app/src/assets/i18n/en.json:1084`
(`inactive_subtitle`, rendered by `membership-management.component.html:70`) also sells it. So the web
sells, on the web, a perk unreachable from the web. The ADR files this under **"Not blocking
acceptance"** (`:917-918`).

**Why it matters.** ADR-0035's panel established that a false statement to a paying customer is
corrective work that ships **immediately** and does not wait for the mechanism
(`0035-metered-membership-benefit-usage.md:659-671`). The cs/sk/ru web strings are a *stronger* false
statement than the one that triggered that ruling, on the *checkout page*, and this ADR has not seen
them.

**What I want changed.**
1. **A sixth copy constraint: no locale of any client may use an assignment verb** (`přiřazen` /
   `priradený` / `назначен` / "assigned"), because AC3 forbids the outcome. Constraint 1 as written
   ("never *the same cleaner every time*") does not catch these.
2. **The picker title is in scope for T-0491**, all five locales × iOS + Android — not just the perk
   description.
3. **Fix the citation** (`en.json:1095`) and the garbled constraint 5 (ADR `:691-692` reads *"must stop
   promising a picker the web has"* — the web does **not** have one; a copy ticket cannot act on that
   sentence as written).
4. **Constraint 5's scoping is wrong in the other direction too:** the ADR says *"iOS's 'Plus benefit ·…'
   is already correct; the other two surfaces must match."* That exact string is **also on Android**
   (`values/strings.xml:723`, identical text). Two of the three clients are already correct on that one
   string; the constraint mis-assigns the work.

---

## CH-2 — RULING: **8 hours.** Raise the hold floor from `StandardLeadTimeHours` to `2 × StandardLeadTimeHours`.

The author asked a challenger to make this call and named it *"the single decision I most want a
challenger to make for me"* (ADR `:903-906`). **My ruling is 8 hours**, expressed as
`2 * BookingPolicy.StandardLeadTimeHours`, not as a literal `8`.

**Reason 1 — the ADR's own justification for zero-hold does not stop at 4h01m; it decays.** The
Consequences section (`:576-579`) says: *"at 2–4 hours' notice the customer's real want is 'someone
comes at all', and spending any of a two-hour window on exclusivity risks the booking itself."* That is
a statement about **customer want under time pressure**. Customer want does not step-function at the
express boundary — the express boundary is a **pricing** line (`BookingPolicy.cs:22-30`: it exists to
decide who pays +20%). The ADR imports a pricing boundary into a dispatch decision because the constant
was to hand. At a 5-hour lead the customer's want is still overwhelmingly "someone comes at all."

**Reason 2 — at a 5-hour lead the named cleaner is the *least* likely person on the board to be free,
and the ADR deliberately declines to check.** A slot five hours from now is a slot on a cleaner's
already-planned day. D5.1 explicitly does **not** check the weekly cap (`TakeOrder.cs:125-143`: 3/6/10 by
rating tier) or the time conflict (`:145-161`) at creation — A6 (`:546`) accepts being "wrong in both
directions" because the cost is bounded by Invariant H. That reasoning is sound at 7 days out, where the
cleaner's calendar is empty and the hold is 7% of the window. **At 5 hours out it inverts:** the
conflicted case is not the tail, it is the modal case, and the hold we grant is the one most likely to
expire unused. To answer the question as posed — *at a 5-hour lead, what is the realistic probability
the favourite cleaner is free and checking their phone?* — the answer is **low on both conjuncts
simultaneously**, and low precisely *because* the lead is short. A hold that expires unused just delays
everyone, exactly as the framing says.

**Reason 3 — a 24-minute hold fails the ADR's own test of what a hold must be to be worth granting.**
`:204-205`: *"12 h is chosen because it is the smallest window that **always intersects a normal waking
period** … which is the actual thing a hold needs to be worth granting."* The author has already stated
the criterion: a hold is worth granting when it intersects a period in which the cleaner will look. A
**24-minute** window intersects nothing; it requires the cleaner to be holding their phone in that exact
slice. The ADR applies the criterion at the ceiling and abandons it at the floor. Applying it
consistently kills the 4h band.

**Reason 4 — the signal at the short end is the weakest, and CH-P4 below shows it can be zero.** The
targeted push is the *only* thing that makes a 24-minute hold actionable (D4, `:225-229`). The resolver
does not check whether the cleaner can receive a push at all (CH-P4). At 12 hours a dead push is
recoverable — the cleaner opens the board. At 24 minutes a dead push means the hold is 100% pure loss,
with certainty.

**What 8 hours costs, honestly:** every 4–8h-lead booking gets no hold. I cannot size that band (CH-10
stands — nothing was measured). But the perk's own audience is not the 5-hours-from-now booker: D8
(`:451-458`) argues that **recurring is the strongest case** (168 h lead, 12 h hold, 7% of the window),
and the picker's own framing is *"the cleaner you already trust"* — a planner's perk. The 4–8h band is
where the perk is least wanted and most expensive. **It costs the perk almost nothing that anybody
notices, and it removes the entire short-lead risk class.**

**On CH-7's objection (the one-number-two-uses property), which the author says is what stopped them:**
it survives, and I want the lead to see why the author's own hesitation was misplaced. Write it as
`2 * BookingPolicy.StandardLeadTimeHours` and there is still **one** number in the codebase, expressed
once; the express/hold relationship stays **derivational**, not duplicated; and if express moves to 6 h,
the hold floor moves to 12 h automatically with no drift. What is lost is only the *aesthetic* of the
two policies sharing a literal value. CH-7's defense (`:846-852`) argues that a second **constant** is
the drift — correct, and a multiple of the first constant is not a second constant. **The property the
author values is preserved; only the coincidence is given up.** §verify #7 should then read: *grep
`ComputePreferredHold` — the floor is `2 * BookingPolicy.StandardLeadTimeHours`; a literal `8` (or `4`)
is a finding.*

**Verdict: 8 h. This is a blocking amendment to D3 unless the lead overrules it.**

---

### CH-P4 — A hold can be granted to a cleaner the platform **already knows it cannot reach**, and D4's own rule says it must not be. **[BLOCKING — D4 contradicts D5.1]**

**The hole.** D4 states the rule as an absolute: *"no notification ⇒ no hold. … A hold exists only to
give someone a chance to act on a signal; with no signal there is no chance, and the latency is pure
loss"* (`:233-235`). D5.1's resolver enforces it against **one** of the platform's **three** ways of not
receiving a push. It checks `UserNotificationPreferences` for a muted category (`HoldDeclineReason
.CleanerMutedNewJobs`). It does not check:

1. **`Device.NotificationsEnabled`** — `src/Cleansia.Core.Domain/Devices/Device.cs:14-20`:
   > *"System-level kill switch driven by the OS notification permission. **When false, the push
   > dispatcher skips this row entirely — even if the user's per-category preferences allow the
   > event.**"*
   This is a server-stored, statically-knowable, per-device fact. It is exactly the shape D5.1's other
   six checks have.
2. **Having any `Device` row at all.** A cleaner who works from the **partner web app** has none — I
   grepped `src/Cleansia.App/apps/cleansia-partner.app` for `registerDevice|deviceToken` and got **zero
   files**. The partner web SPA registers no push devices. For that cleaner a hold is **always** dead
   time: up to 12 hours of board-invisibility bought for a signal that structurally cannot arrive.

**Why it matters for the promise.** The customer is about to be told (CH-P2's sentence) *"Anna gets first
chance."* If Anna denied iOS notifications or works from the web board, Anna gets no chance at all — she
gets an order silently withheld from everyone including, in practice, herself. The sentence is false, the
order is delayed, and nobody is better off. This is the purest form of the failure class the ADR was
written to end.

**What I want changed.** Two entries added to `HoldDeclineReason` and two rows to D5.1's
checked-at-creation table:

| Condition | Source | Reason |
|---|---|---|
| the cleaner has ≥1 active `Device` with `NotificationsEnabled == true` | `Device.cs:20`, `IsActive` | `CleanerUnreachableForPush = 8` |
| (folds in) no device row at all | — | same |

This is a **read the resolver already has the shape for**, and it makes D4's stated rule actually true.
`TC-PREF-INELIGIBLE-4` should pin it: a cleaner with `NotificationsEnabled = false` → order created,
preference stored, **no hold**, on the open board immediately.

---

### CH-P5 — Recurring: the perk is sold as *"on **every** booking"*, delivered on **zero** occurrences, and **"Make this recurring" silently destroys a preference the customer stated**. **[BLOCKING on copy; C3's sequencing is acceptable]**

**Is it acceptable to ship without C3? Yes — but only with a copy correction in the same wave, and the
ADR does not require one.** My finding is not "build C3 first"; it is "do not ship the mechanism while
the copy claims the mechanism covers schedules."

**Fact, confirmed.** `RecurringBookingTemplate.Create(...)` takes userId, frequency, dayOfWeek, timeOfDay,
rooms, bathrooms, savedAddressId, service/package ids, paymentType, startsOn, endsOn — and nothing else
(`src/Cleansia.Core.AppServices/Features/Bookings/CreateRecurringBooking.cs:102-114`).
`MaterializeRecurringBookings.cs:138` passes `PreferredEmployeeId: null`. The author's read is correct.

**What the customer is told, on the same screen, in the same list:**

| String | Text |
|---|---|
| `values/strings.xml:836` (+ `values-cs/:824`, `values-sk/:821`, `values-ru/:821`, `values-uk/:821`, `Localizable.xcstrings:14109-14143`) | *"Request the same cleaner you trust on **every booking**."* |
| `values/strings.xml:838` | *"Set it once — every other Tuesday at 10am, automatically."* |

These two perks sit adjacent on the Plus subscribe screen. A customer reads them **together** and
concludes their automatic Tuesday cleanings come from their trusted cleaner. The system delivers that on
**zero** occurrences and has no field in which to store the intent. Same for the web (`en.json:1094-1095`
+ `:1129-1130`).

**And a second, sharper case the ADR does not name at all — the conversion moment.**
`en.json:1680` `order_detail_make_recurring` = *"Make this recurring"*. The web prefill
(`libs/cleansia-customer-features/recurring-bookings/src/lib/recurring-bookings.facade.ts:167-180`)
prefills the wizard **from a past Completed order** — *"keeps everything else (rooms, bathrooms, payment
type, time slot from the source order)"*. That source order is, by definition, an order the customer
completed **with a cleaner** — i.e. precisely a customer eligible for the perk, quite possibly an order
that carried `PreferredEmployeeId`. The conversion drops it, silently.

The platform **already has the disclosure surface for exactly this**: `en.json:1679`
`prefill_dropped_items` = *"We removed these from your prefill — they're no longer available: {{items}}"*.
The preference is dropped **outside** that message. So the one place the platform is honest about
dropping things is the one place the preference goes missing without a word.

**What the customer sees today, and must see instead.**
- **Today:** nothing. No field, no note, no line in the recurring wizard on any of the three clients.
- **Required, in the same release as C2 (not C3):** the *"every booking"* claim comes off all five
  locales × Android + iOS + web, replaced by a one-off-scoped sentence, **and** the recurring create
  wizard carries one line:
  > *"Favourite-cleaner requests apply to one-off bookings. Schedules go to whichever cleaner is free."*
  When C3 lands, both change back together.
- **Add to D8, as a named case:** `prefillFromOrder` must either carry the preference (once C3 exists)
  or list it in the existing dropped-items disclosure. Right now it is a silent destruction of a stated
  customer preference at the exact moment the customer is doing the thing the perk is for.

---

### CH-P6 — The lapse case. The freeze ruling is **sound**; the `PastDue` grace window the platform documents **does not exist**; and D7's error message, as specified, upsells the customer at the moment their booking fails. **[BLOCKING on the error copy + the PastDue ruling]**

**What holds up, and I want it on the record as defended:** D7's *"a member who lapses — orders already
created: the hold stands"* is right, and its "practically moot" justification checks out — the hold is
computed at creation and capped at 12 h (`ComputePreferredHold`, ADR `:160-166`), so no already-created
order can carry a hold more than 12 h past its creation regardless of how far ahead the cleaning is.
`TC-PREF-GATE-2` pins it. **No challenge.** Likewise the D8 principle *"reject where a human can react,
degrade where nobody can"* is the right rule and I am not contesting it.

**Hole 1 — `PastDue` is documented as a grace window and implemented as a lapse.**

- `src/Cleansia.Core.Domain/Memberships/MembershipStatus.cs:18-19`:
  > *"PastDue = 2 — Latest invoice failed; Stripe is retrying. **Benefits still apply during the grace
  > window.**"*
- `src/Cleansia.Core.Domain/Memberships/UserMembership.cs:84-85`:
  ```csharp
  public bool IsActive => Status == MembershipStatus.Active && DateTime.UtcNow < CurrentPeriodEnd;
  ```
- `src/Cleansia.Infra.Database/Repositories/UserMembershipRepository.cs:27-29` — the "one live-membership
  predicate" D5.1/D7 both build on:
  ```csharp
  .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active && m.CurrentPeriodEnd > DateTime.UtcNow)
  ```

**PastDue is excluded from both.** The grace window the enum documents is not implemented anywhere. D7
does not merely inherit this — **D7 makes it load-bearing for a hard rejection of a booking.** A Plus
member whose card fails one retry cycle (Stripe `past_due`, mapped at `UserMembership.cs:125`) has their
**entire order rejected** with a new error, for a perk, while Stripe is still retrying and the customer
believes — because we told them — that their benefits continue.

**Hole 2 — the "nearly unreachable in practice" defense depends on client caches that do not refresh.**
D7 (`:439`) argues the error is nearly unreachable because the client gates the picker on membership.
`GetMyMembership.cs:35` does use the same predicate — good. But the pickers cache for the session:

- iOS `PreferredCleanerViewModel.swift:27-29` — `func load() { if loaded { return }; loaded = true; … }`.
  One fetch per view-model lifetime, full stop.
- Android `PreferredCleanerPicker.kt:78-82` — refreshes **only** `if (membershipState == null)`, against a
  singleton repo *"shared with the Profile tab, so it might be stale"* (its own comment, `:75-77`).

A webhook landing mid-session leaves the picker on screen and the submit rejected. Narrow, but real, and
it is the *whole* of D7's reachability defense.

**Hole 3 — the error message D7 specifies is a sales pitch at the moment of failure.** D7 says the new
`PreferredEmployeeMembershipRequired` mirrors `RecurringTemplateMembershipRequired`. That one's shipped
customer-facing text is `apps/cleansia.app/src/assets/i18n/en.json:1569`:
> *"Recurring cleanings are a Cleansia Plus benefit — subscribe to set one up."*

That is a fine sentence when the customer is *starting* something optional. Mirrored onto a **booking
the customer has already filled in and paid attention to**, it becomes: your cleaning was refused, here
is an upsell, and no statement of what to do next. **The ADR's own justification for rejecting rather
than ignoring is "a human is present and can fix it in one tap" (`:472-473`) — that is only true if the
message names the tap.**

**What I want changed.**
1. **Rule on `PastDue` explicitly in D7's table.** Either (a) treat `PastDue` as active for perk
   *gating* (matching the documented grace window), or (b) correct `MembershipStatus.cs:18-19` in the
   same wave so the enum stops documenting a window that does not exist. **Do not leave a rejection path
   whose predicate contradicts the platform's own comment.** If neither is chosen, escalate to the owner
   via `questions/open.md` — "does a failed card payment revoke perks immediately or after Stripe's
   retries?" is a business decision, not an architect's.
2. **The error's five translations must name the action, not the rule.** Constraint for T-0491:
   > *"Cleansia Plus is needed to request a specific cleaner. Remove the cleaner request to book now."*
   Not *"subscribe to set one up."*
3. **Add a `TC-PREF-GATE-3`:** a `PastDue` member setting a preference gets whatever (1) rules — the test
   must exist either way, because today the behaviour is decided by an unstated `WHERE` clause.

**One more thing the principle statement should say out loud.** `MaterializeRecurringBookings.Handler`'s
constructor (`:39-47`) takes **no** membership repository — the sweep checks membership **nowhere**
today. So a lapsed member's schedule keeps materializing (and charging) indefinitely, while
`en.json:1102` tells them at cancellation *"Your benefits stay active until the end of your current paid
period."* D8.3 would add a membership re-check **for the preference only** — meaning the ADR ships a rule
where the *smaller* perk is revoked on lapse and the *larger* one (the schedule itself, a Plus-gated
feature per `CreateRecurringBooking.cs:81-91`) is not. That is defensible under "never drop a cleaning",
but it is a **new asymmetry the ADR creates and does not name**, and it should be one line in D8:
*"materialization itself is deliberately not membership-gated; only the preference is. Whether a lapsed
member's schedule should continue is `Q-PLUS-04`, not this ADR."*

---

### CH-P7 — The ADR abandons **ADR-0035's own corrective-ships-first ruling** and files every false statement under "not blocking acceptance." **[BLOCKING — process]**

ADR-0036 declares it *"composes with ADR-0035"* (`:9`). ADR-0035's copy section
(`0035-metered-membership-benefit-usage.md:659-671`) made a ruling in bold:

> **The corrective half ships immediately and does NOT wait for the implementation. The affirmative half
> ships only with T-0493.**
> *"Waiting for the mechanism to ship is choosing to keep a false statement live for the length of a
> build."*

ADR-0036's §Copy has **no sequencing ruling at all**. It hands the whole thing to T-0491 and its §Verdict
says *"Not blocking acceptance: the exact copy (T-0491 owns it) and the web wizard's missing picker"*
(`:917-918`). Meanwhile the live false statements this panel has now catalogued are:

| Statement | Where | Status |
|---|---|---|
| *"will be **assigned** first/preferentially"* | web `cs/sk/ru.json:1095`, on the **checkout page** | contradicts AC3 |
| *"they'll be prioritized when matching"* | web `en/uk.json:1095` | false today (nothing reads the field) |
| *"Request the same cleaner … on **every booking**"* | Android ×5, iOS ×5 | false for every recurring occurrence, forever |
| *"Cleaner being assigned · **Within 1 hour**"* | Android `:742`, iOS `:4799` | **made false by this ADR**, up to 12× |
| a Plus perk sold on a surface with no picker | web `en.json:1084`, `:1094-1095` | unreachable where it is sold |

**The distinction the ADR needs to make and does not:** rows 1–3 and 5 are **corrective** (false today,
independent of the build) and must ship on the ADR-0035 schedule — **now, ahead of C1/C2**. Row 4 is
**created by this decision** and must ship **with** C2, never after. Only the *affirmative* first-chance
sentence waits for the mechanism.

**What I want changed.** A §Copy sequencing paragraph mirroring ADR-0035's, and the §Verdict's
"not blocking acceptance" line amended: **the corrective wave is a condition of acceptance**, in the same
way CH-10's measurement ticket is.

---

### One non-blocking observation (recorded, not a challenge)

**The false "matching algorithm" claim lives in three files, and AC12 corrects one.** The ADR's §Naming
replacement covers `Order.cs:217-224`. The same myth is written in the clients:

- `src/cleansia_android/customer-app/src/main/java/cz/cleansia/customer/features/booking/PreferredCleanerPicker.kt:52-54` —
  *"Selection writes to [BookingState.preferredEmployeeId]; the booking submit picks it up and **the matching algorithm boosts that cleaner's score**."*
- `src/Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard/order-wizard.facade.ts:576-578` —
  *"send undefined so **the backend skips the matching boost**."*

Correct all three in the same wave or the next reader re-learns the myth from the client. One line in
AC12.

---

## What I checked and found sound

Named explicitly, because silence is not assent.

1. **D1's mechanism choice.** I tried to argue A1 (board boost) from the customer's side and could not.
   A boost is unfalsifiable as a promise — there is no observable difference between "the boost worked"
   and "they were first", so no support answer exists to *"did I get what I paid for?"*. For a **paid**
   perk that is disqualifying. The author's rebuttal to CH-1 stands.
2. **AC1/AC3 — the hold delays assignment, never the appointment.** I checked that nothing in D2/D3/D5
   touches `Order.CleaningDateTime`, and that `ComputePreferredHold` (ADR `:160-166`) reads the cleaning
   time but writes only a deadline. The customer's cleaning time genuinely never moves. **This is the
   single most important true sentence in the ADR and the copy should lead with it.**
3. **D5.2 — the take-time refusal returns `OrderNotFound`.** The rule *"the error a caller gets must
   agree with what that same caller's GET would return"* is right, and it is a **cleaner**-side
   question, not a customer-promise one. No challenge from this lane.
4. **D7's freeze ruling on already-created orders** — verified moot as claimed (see CH-P6). Sound.
5. **D7 vs D8's asymmetry (the author's CH-8).** *Reject where a human can react, degrade where nobody
   can* is the right rule and I tested it against the real flows: `CreateRecurringBooking.cs:84-91`
   rejects with a user present; `MaterializeRecurringBookings` runs system-level with a per-template
   tenant override and no session (`:54-74`). The cases genuinely differ in whether an error has an
   audience. **The principle survives**; my CH-P6 attacks its *inputs* (PastDue) and its *message*, not
   the principle.
6. **D9 — keeping `UserHasCompletedOrderWithEmployeeAsync`.** From the customer's side, "the cleaner you
   already trust doesn't exist before your first clean" is honest, and the picker is fed by the same
   predicate (`GetMyServingCleaners`), so the picker never offers a cleaner the server refuses.
   Confirmed the picker only renders when the list is non-empty (`PreferredCleanerPicker.kt:94`,
   `PreferredCleanerViewModel.swift:23-25`), so a first-time customer never sees a dead control. Sound.
7. **D6's "no push on expiry".** I looked for a reason to want one and agree with the ADR: a push whose
   entire content is *"the person you asked for couldn't come"* manufactures a disappointment out of a
   normal outcome. **This half of D6 is right.** It is the *"told once"* half that has no surface
   (CH-P2).
8. **`PreferredEmployeeId` reaches no DTO anywhere.** Whole-`src` grep: entity, setter, EF config,
   anonymizer, factory, command plumbing, validator, migrations, two tests, one materializer null. The
   author's "written and read by nothing" is exactly right. **Consequence worth noting for T-0491:**
   because it reaches no customer DTO either, the customer's order detail never echoes *who they asked
   for* — so the success case is observable only against the customer's memory of a name they typed
   days earlier. §verify #5's parenthetical (*"it may reach the customer's own order DTO"*) is
   permission, not a requirement; I think it should become a requirement, but I am not blocking on it.
9. **The express-band interaction (D3, TC-PREF-EXPRESS-0).** Verified the boundary is real:
   `BookingPolicy.cs:68-72` `RequiresExpressSurcharge` is `[2h, 4h)`, and `booking_slot_express`
   (`values/strings.xml:560`, *"Express +20%"*) marks those slots to the customer. A Plus member in that
   band getting the waiver and no hold is coherent, and constraint 4 (*must not claim the preference
   applies to express*) is correctly stated. **My CH-2 ruling moves this boundary to 8 h**, which makes
   the copy easier, not harder: with an 8 h floor there is no *"express but also held"* edge to explain
   at all.
10. **What I did not examine:** partner-side push plumbing (Challenger C's lane), index-servability of
    the predicate, the admin surfaces, and the Stripe `Pending`-order interaction. The three clients'
    **strings** I read in full, in all five locales, for every key named in §Copy plus the picker titles,
    the booking-success timeline and the two membership perk blocks.
