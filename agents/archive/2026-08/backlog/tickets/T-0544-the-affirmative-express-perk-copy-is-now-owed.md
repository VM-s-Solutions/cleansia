---
id: T-0544
title: Plus advertises four perks instead of five — the affirmative express copy is now owed and belongs to nobody
status: done
size: S
owner: android
created: 2026-08-04
updated: 2026-08-06
depends_on: [T-0493, T-0513]
blocks: []
stories: []
adrs: [0035]
layers: [analyst, frontend, android, ios]
security_touching: false
manual_steps: []
sprint: 15
source: PM sprint-15 reconciliation. `0c665c08` deferred the affirmative sentence to T-0493; T-0493's
  mechanism shipped in `3092abc1` with **no copy AC**, so nothing owns it.
---

## Context

`0c665c08` removed the express perk from all three clients because the claim was **false against the
customer**: book at 09:00 for a 12:00 clean and you are inside the express window, are charged +20%, and
were told it was free. `MembershipPlan.AllowsExpressUpgrade` was read by zero pricing code. The perk was
**removed, not reworded** — there was no true present-tense sentence available.

That commit was explicit about the debt it was leaving:

> The affirmative copy — two free express bookings per calendar month, Plus-only, per ADR-0035 — is
> deliberately **NOT** written here. It ships with the mechanism in T-0493. Nobody is harmed by being
> told they get less than they do.
>
> Consequence the owner should know: **Plus advertises FOUR perks instead of five on every client until
> T-0493 lands.** That is the honest count.

**T-0493 has landed** (`3092abc1`) — the waiver is resolved, metered and consumed server-side, and all
four owner rulings are enforced. But T-0493's thirteen ACs are all mechanism; **not one of them is a copy
AC**. So the sentence fell between two closed tickets.

**Verified at HEAD, 2026-08-04.** A walk of all 15 web i18n bundles plus both mobile catalogs for
`express|expres|експрес|экспресс` finds only: the mechanic's own labels
(`pages.order.slot_express`, `pages.order.express_surcharge_label`), the new refusal key
(`api.membership.express_waiver.no_longer_available`), and the **admin** quota-configuration fields.
Android's customer catalog even carries a standing comment at `values/strings.xml:844` — *"No express
perk anywhere"*. **No client advertises the perk that now exists.**

So today a paying Plus member gets two free express bookings a month and **is never told**, while the
member who would have upgraded for that perk is not offered it.

## Acceptance criteria

- [ ] **AC1 — the analyst writes ONE sentence and it is true in every case the mechanism produces.**
      Given the resolver's actual behaviour, When the sentence is written, Then it is true for: a Plus
      member with quota left, a Plus member with none, a member **in the 14-day trial** (the owner ruled
      **no express waivers during trial**), and a **PastDue** member (the owner ruled PastDue keeps **no**
      benefits). A sentence that is true only in the happy case is the defect `0c665c08` removed.
- [ ] **AC2 — no number is hardcoded in the copy.** Given `ExpressUpgradesPerMonth` is **per-plan
      configurable** (the admin UI now edits it — `e4dd27f5`), When the string is written, Then it does
      not say "two". `8ff9dfb4` made exactly this call for the refusal message and it applies here with
      more force, because this string is a **promise**.
- [ ] **AC3 — "same-day" does not reappear, in any locale.** Given `BookingPolicy` implements a **2–4 h
      lead**, not same-day, When the copy is written, Then no locale says same-day. A 09:00 booking for
      18:00 is same-day and already surcharge-free for everyone.
- [ ] **AC4 — the three guard tests still pass, unmodified.** Given the per-platform guards `0c665c08`
      added (web Jest / Android JUnit / iOS XCTest), which scan **VALUES not key names** across all five
      locales for `express|expres|експрес|экспресс`, When the affirmative copy lands, Then those guards
      are **updated deliberately and their mutation proof re-run** — they exist to stop a false express
      claim returning, and this ticket is the one legitimate reason to touch them. **Narrow them to the
      false claim; do not delete them.** Say in the status log exactly what was narrowed and why.
- [ ] **AC5 — all seven render sites are covered, in all five locales.** `0c665c08` found **seven**, not
      the four its ticket listed: web subscribe / management card / welcome (4 keys), Android subscribe /
      success (2 keys), iOS subscribe / success (2 keys). Re-derive the list; do not trust this one.
- [ ] **AC6 — Plus advertises five perks again, and the count is verified by rendering, not by grepping
      keys.**
- [ ] **AC7 — the copy is consistent with what T-0514 will render.** T-0514 shows the waived surcharge and
      the remaining quota **in the booking flow**. The perk sentence sells it; T-0514 reports it. They must
      not describe two different products. Hand T-0514 the agreed vocabulary.

## Out of scope

- The booking-flow disclosure itself — **T-0514**.
- The other four perks' copy — **T-0491** owns the full copy table. **If T-0491's panel is running, hand
  it AC1's sentence rather than deciding twice** (this is T-0513's own instruction, still in force).
- `Q-PROMISE-02` — cs/sk/ru promise the favourite cleaner *"will be preferentially assigned"* where en/uk
  promise only priority. Different perk, open owner question, no copy ticket until the promise is chosen.

## Implementation notes

**`analyst` owns AC1–AC3** (the sentence); `frontend` / `android` / `ios` instances apply it, each with a
reviewer in parallel. **Serialize on the i18n bundles** per `process/shared-file-lanes.md`.

**Read first:** ADR-0035 (the waiver's actual semantics, including AM-17/18/19, the owner's PastDue,
trial and plan-swap rulings) and `0c665c08`'s reasoning for the removal — the sentence must survive the
same test that killed the last one.

## Status log
- 2026-08-04 — created `ready` by pm during the sprint-15 reconciliation. Both dependencies are `done`.
  This is a gap **created by closing T-0493**, not a pre-existing one: the mechanism shipped and the
  promise did not follow it. Passes DoR: AC observable, `S`, deps satisfied, no owner-only steps.
- 2026-08-04 — **web leg shipped** (frontend). Copy applied to the three web render sites, ×5 locales.
  The sentence is grounded on `ExpressWaiverResolver` rather than on the ADR anchor: it says *"while
  your paid membership is active"* (which is the only clause that is simultaneously true for a trial
  member — the resolver returns `noWaiver` on `IsInTrial` — and for `PastDue`, whom
  `GetActiveForUserNoTrackingAsync` excludes so the whole perk list disappears), *"2 to 4 hours after
  you book"* (`BookingPolicy.ExpressLeadTimeHours`→`StandardLeadTimeHours`, never "same-day"), *"each
  calendar month"* (D2), *"unused ones don't carry over"* (AM-16), and **no count** — AC2 over the
  ADR's §Copy constraint 3, which predates `e4dd27f5` making the quota admin-editable.
  **Guard narrowed, not deleted** (AC4): `membership-express-claim.spec.ts` kept its shape and its
  five-locale walk over VALUES, and swapped "no express string exists" for four content properties —
  (a) the perk keys are present and non-empty in all five locales, (b) **no locale** matches a
  same-day regex, (c) **no express string carries a digit** once `{{placeholders}}`, the `20 %` rate
  and the `2…4` window are erased **by shape** (a plain allow-list of the digits 2/4/20 was tried
  first and passed *"2 free express bookings a month"* — mutation M3 — so it was replaced), and
  (d) every screen gates the claim on a server field. Mutation-proven: **10/10 mutations red,
  baseline restored green** (M1/M2 same-day incl. a single non-English locale, M3/M3b/M4 a hardcoded
  quota as numeral/inside the window string/as a word, M5 a locale dropping the perk, M6 the window
  dropped from the selling copy, M7 the gate removed, M7b Plus back to four perks, M8 the client
  adjusting the server count).
  **Android/iOS have NOT been touched** — the shipped English strings are listed for verbatim port in
  the handover; both mobile guards and both source comments (`values/strings.xml:844`,
  `MembershipPerks.swift:6-9`) are still owed by their own legs.

- 2026-08-05 — **`ready` → `in_progress` (PM reconciliation pass 4). PARTLY SHIPPED — web only.** The
  status-log entry above already said *"Android/iOS have NOT been touched"*; this pass confirms it at HEAD
  and moves the row out of the `ready` queue so nobody rebuilds the web leg.
  - **SHIPPED (web, `4984c2eb`) — do not rebuild.** `benefit_express_title` / `benefit_express_body` /
    `perk_express` / `perk_express_used` / `perk_express_trial` / `welcome_perk_express` in all five
    customer-app locales, and the guard was **narrowed rather than deleted**. The guard is the part worth
    preserving: `apps/cleansia.app/src/app/i18n/membership-express-claim.spec.ts:53-67` erases the two
    numbers the copy MAY name **by shape** (`SURCHARGE_RATE = /\b20\s?%/g`, `LEAD_TIME_WINDOW = /\b2\D{1,6}4\b/g`)
    and then rejects any surviving digit — because a bare allow-list of the digits 2/4/20 also permits
    *"2 free express bookings a month"*, which is precisely the hardcoded quota the rule exists to stop
    (recorded as mutation M3). It also walks **values, not key names**, across all five locales, and asserts
    every screen gates the claim on a server field.
  - **OWED (android + ios), and both still carry a comment that is now false.**
    `cleansia_android/customer-app/src/main/res/values/strings.xml:852-855` still says *"No express perk
    anywhere — not on the subscribe screen, the success screen or the management pills… Restore this perk
    only together with the code that waives the surcharge."* **The code that waives the surcharge shipped in
    T-0493.** `cleansia_ios/CleansiaCustomer/Sources/Features/Membership/MembershipPerks.swift:6-9` carries
    the same claim (*"Express upgrade is deliberately absent"*) and the enum still has only
    `discount` / `freeCancellation` / `recurring`, so **Plus advertises four perks on mobile and five on
    web** — AC6 fails on two clients. Both comments must be corrected in the same change that adds the perk;
    leaving a stale comment that forbids the thing you just did is how this ticket was needed twice.
  - AC5's seven render sites: **three are done (web), four are owed** (Android subscribe + success, iOS
    subscribe + success). Re-derive rather than trusting that count.
  - `owner` moves `analyst` → `android`; the copy decision is made and attributed, so what remains is a
    verbatim port plus the two per-platform guards. iOS runs in parallel.

- 2026-08-05 — **android leg shipped.**
  - **The stale comment is gone.** `values/strings.xml:855-858` did read *"No express perk anywhere …
    Restore this perk only together with the code that waives the surcharge."* — verified verbatim before
    editing, and false since T-0493. Replaced with the neutral section header the other four locales
    already carried (`<!-- Perk pills on the active management card. -->`). The second copy of the same
    claim lived in `MembershipPerks.kt:9-12` (*"Express upgrade is deliberately absent…"*) and in
    `SubscribePlusScreen.kt:82` (*"Express upgrade is absent on purpose"*) and in `MembershipPerksTest`'s
    doc — **all four rewritten**, plus `MembershipExpressClaimTest`'s class doc, which asserted the
    opposite of what it now guards. A grep for `absent|deliberate|no pricing code|Restore this` over
    `customer-app/src/main/java` now returns only the new, true comments.
  - **AC5 re-derived, not trusted: Android has THREE render sites, not two.** Subscribe
    (`SubscribePlusScreen.kt` — a 5th `PerkTile`), success (`MembershipSuccessScreen.kt` — a 4th
    `PerkRow`), **and the management card's pill row** (`MembershipManagementCard.kt`), which the ticket's
    count omitted. The pill row is the container-equivalent of web's `<li>` list in
    `membership-management.component.html`. **iOS has the same three** (`MembershipPerks.swift`'s enum
    feeds a `ChipFlow` row per `patterns-mobile.md`), so the iOS leg owes three, not two.
  - **AC6 — Plus advertises five perks again** on the subscribe screen (discount → cancellation →
    favorite cleaner → recurring → **express waiver**), four on the success screen, and four pills on the
    active card. Verified by rendering order in source, not by key grep.
  - **Copy taken verbatim from `apps/cleansia.app/src/assets/i18n/*.json` in all five locales:**
    `benefit_express_title` → `membership_perk_express_title`; `benefit_express_body` →
    `membership_perk_express_desc`; `welcome_perk_express` → `membership_success_perk_express`.
  - ⚠️ **One deliberate copy divergence the iOS leg should match, not re-derive.** Web's
    `perk_express` / `_used` / `_trial` are full sentences in a `<li>` list ("Express surcharge waived —
    {{count}} left this month"). Android's counterpart is a **chip in a `FlowRow`** whose siblings are
    "%1$d%% off" / "%1$dh free cancel" / "Recurring" — a 50-character sentence wraps inside the pill and
    reads as broken. So the three pill strings are terse Android-native variants carrying the identical
    three claims (`membership_perk_pill_express` = "Express waived · %1$d left",
    `_used` = "… · none left", `_trial` = "… · after trial"). The **promise** copy on the prose surfaces
    is verbatim web. **iOS uses a `ChipFlow` for the same row, so it should take the Android pill copy.**
  - **AC1 holds in all four cases** because the claim is a *status*, not a promise: the pill only renders
    when the server reports a quota, and it renders `Trial` / `Exhausted` / `Available(n)` distinctly.
    PastDue never reaches it — `GetActiveForUserNoTrackingAsync` excludes it, so the whole perk list
    disappears. **AC2** — no locale names a count; the count is always `%1$d` from the server.
    **AC3** — no locale says same-day.
  - **AC4 — the three guards were narrowed, not deleted, and re-mutation-proven.**
    `MembershipExpressClaimTest` kept its five-locale walk over **VALUES** and swapped "no express string
    exists" for: (a) all 11 express keys present + non-empty in all five locales, (b) no locale matches a
    multilingual same-day regex, (c) **no express string carries a digit** once `%…` placeholders, the
    rate (`\b20\s?%`) and the window (`\b2\D{1,6}4\b`) are erased **by shape** — web's M3 lesson (a plain
    2/4/20 allow-list also passes *"2 free express bookings a month"*) applied directly, (d) every screen
    renders the claim and none branches on the bare `allowsExpressUpgrade` flag. A floor assertion fails
    the walk if it ever stops finding `11 keys × 5 locales` strings.
  - **Bonus safety net found, not added by me:** `NotificationsScreenTogglesTest :: all five locales
    declare the same keys` already enforces whole-catalog locale parity and went RED on mutation M7 (a
    key deleted from `values-uk`). So the five-locale requirement is double-covered.
  - **Gate 0.5 — 18/18 mutations RED**, each attributed to a named test, all files restored byte-exact
    (sha256 verified after each batch). Copy-guard mutations specifically: **M7** a locale drops the perk
    title → *every express string is present…*; **M8** a cs locale claims same-day → *no locale describes
    the express window as same-day*; **M9** en body says "2 free express bookings a month" → *no express
    string hardcodes the monthly quota*; **M15** a sk locale loses `%1$d` → *every counted express string
    keeps its placeholder*; **M13** the trial pill points at the "used" string → *each express pill state
    maps to its own label*; **M16** the subscribe tile deleted → *every membership screen gates the
    express claim on a server field*.
  - customer-app **51 classes / 455 tests → 54 / 492, 0 failures**; `:core` and `:partner-app` unchanged.
  - 🚩 **Found, NOT fixed (pre-existing, out of scope):** `booking_slot_express` is the untranslated
    literal *"Express +20%"* in **all five** locales including uk/ru, where every neighbouring string uses
    "Експрес"/"Экспресс". The new `booking_slot_express_waived` IS translated (web verbatim), so the chip
    flips between an English and a localized label for the same user. Fixing it would change a
    non-member's screen, which T-0514 AC3 forbids — needs its own row.

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
