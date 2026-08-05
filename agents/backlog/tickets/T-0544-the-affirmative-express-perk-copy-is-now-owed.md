---
id: T-0544
title: Plus advertises four perks instead of five — the affirmative express copy is now owed and belongs to nobody
status: in_progress
size: S
owner: android
created: 2026-08-04
updated: 2026-08-05
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

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
