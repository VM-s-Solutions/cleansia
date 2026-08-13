---
id: T-0513
title: The three clients advertise three different express perks, and none matches the mechanic
status: done
size: M
owner: analyst
created: 2026-08-02
updated: 2026-08-04
depends_on: []
blocks: [T-0514]
stories: []
adrs: [0035]
layers: [analyst, frontend, android, ios]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Found by the PM while grounding the owner's *"You can upgrade"* answer, 2026-08-02.** This was not in
the Plus audit and is not on any existing ticket.

**The same paid perk is advertised three different ways, and a fourth thing is what the code does.**

| Surface | String | What it promises |
|---|---|---|
| Android `values/strings.xml:843-844` | *"Free express upgrade" / "One free same-day booking per month, no surcharge."* | a **metered** waiver — 1/month |
| iOS `Localizable.xcstrings` `membership_perk_express_desc` | **identical** to Android | 1/month |
| Web `apps/cleansia.app/.../en.json:1094-1095` | *"Express upgrade" / "Pay less for last-minute bookings inside the express window."* | an **unmetered discount** — not free, no cap |
| Web `en.json:1102`, `:1140` | *"Express upgrade benefits"* | unspecified |
| **The code** (`BookingPolicy.cs:18-30`) | express = **2–4 h lead time**, +20% | not "same-day" |

**Two distinct defects, and the second is the interesting one:**

1. **Web promises a different product from mobile.** "Pay less, inside the window" has no monthly cap
   at all. A customer who reads the web card and then hits a cap on their second express booking was
   told something untrue by us.
2. **"Same-day" is not what express means here.** A booking made at 09:00 for 18:00 the same day has a
   **9-hour** lead time — that is a *standard* booking, no surcharge, for everybody. The perk as
   worded promises to waive a surcharge that would never have applied. Meanwhile a booking made at
   09:00 for 12:00 **is** express and **is** same-day. The word does real work in the wrong direction.

**Why this ticket exists separately from T-0493:** it needs **no backend, no API and no dependency**.
It is dispatchable today, and it is the half that reduces a live misrepresentation on a paid
subscription. Shipping the enforcement (T-0493) against copy that says three things would make the
mismatch *more* visible, not less.

## Acceptance criteria

- [ ] **AC1 — ONE canonical sentence for the perk, in English, agreed before any locale is touched.**
      It must be checkable against the code: it names the lead-time window (not "same-day") and states
      the cap in the terms `Q-PLUS-02` settles. Evidence: the sentence, plus the `BookingPolicy`
      constant it maps to.
- [ ] **AC2 — the word "same-day" is either justified or removed.** If the owner wants the customer-
      facing word to stay "same-day", then **the mechanic is wrong, not the copy** — say so in one
      sentence and file it, because that is a `BookingPolicy` change and a different ticket. Evidence:
      the ruling, one way or the other.
- [ ] **AC3 — all three clients carry the same promise across all five locales** (en, cs, sk, uk, ru).
      Web `en.json` + 4, Android `values*/strings.xml` ×5, iOS `Localizable.xcstrings` ×5. Evidence:
      a key-by-key diff showing parity.
- [ ] **AC4 — the copy is honest about the state it ships into.** Until T-0493 lands, **nothing waives
      the surcharge.** Either this ticket ships with T-0493 in the same wave, or the copy must not
      claim a benefit that is not yet enforced. **State which, and why.** Evidence: the sequencing
      statement.
- [ ] **AC5 — the Android in-code comment at `values/strings.xml:846-847` is updated or removed.** It
      currently reads *"No express pill: nothing in pricing reads AllowsExpressUpgrade, so a member
      pays the standard express surcharge."* **That comment is a correct description of a defect and it
      must not silently become false.** Evidence: the updated comment.
- [ ] **AC6 — no new keys, or new keys are named.** The three clients use different key names for the
      same idea; consolidating them is **out of scope** unless AC1 forces a new string. Evidence: the
      key list.
- [ ] **AC7 — the shared-file lanes are respected.** `Localizable.xcstrings` (customer) and the five
      Android `values*/strings.xml` are serialized lanes — see `sprint-15.md` §3. **This ticket must be
      scheduled into those lanes, not run against them concurrently.** Evidence: the lane check at
      dispatch.
- [ ] **AC8 (Gate 0.5 leg 3)** — the AC evidence here is textual/visual, not executable; **do not
      write a Gate 0.5 leg-1 mutation proof for it.** State what was verified by reading vs by running.

## Out of scope

- **Enforcing the perk** — **T-0493**.
- **Showing a remaining-quota count in the UI** — **T-0514**, which needs the API field.
- **The other four perks' copy.** T-0491 owns the full copy table; this ticket touches express only.
  **If T-0491's panel is running, hand it AC1's sentence rather than deciding twice.**
- **Changing `BookingPolicy`'s lead-time constants.** AC2 may *name* that as the consequence; it does
  not make the change.

## Implementation notes

**`analyst` owns AC1/AC2** (the sentence and the same-day ruling); `frontend` / `android` / `ios`
instances apply it, each with a reviewer in parallel. **Serialize on the i18n bundles** per
`process/shared-file-lanes.md`.

**Read first:** the table in the Context above (every file:line is PM-verified), `BookingPolicy.cs:14-30`,
and T-0491's copy inventory if that panel has run.

## Status log
- 2026-08-02 — **draft (created by pm).** **New finding, not in the Plus audit:** while grounding the
  owner's express answer the PM found the web client advertises an *unmetered discount* where the two
  mobile clients advertise a *1-per-month waiver*, and that **"same-day" does not describe the 2–4 h
  express window the code implements**. Filed independently of T-0493 because it needs no backend and
  it reduces a live misrepresentation on its own. **No dependency deliberately** — AC4 owns the
  sequencing question rather than a `depends_on` hiding it.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). Shipped in `0c665c08` *"fix(i18n): stop advertising a
  free express upgrade that nobody gets"*, merged at `0da90eeb`. **Verified at HEAD** by walking every
  `*.json` under `apps/*/src/assets/i18n/` and both mobile catalogs for `express|expres|експрес|экспресс`:
  the only survivors are the **mechanic's own** labels (`pages.order.slot_express`,
  `pages.order.express_surcharge_label`), the new refusal key
  (`api.membership.express_waiver.no_longer_available`) and the **admin** quota-configuration fields. No
  client advertises an express perk. Android's catalog carries an explicit comment at
  `customer-app/.../values/strings.xml:844` — *"No express perk anywhere"* — so the deletion is
  self-documenting.
- 2026-08-04 — **the consequence this ticket named has now come due.** `0c665c08` deliberately left the
  AFFIRMATIVE copy to T-0493 (*"Nobody is harmed by being told they get less than they do"*), and Plus has
  advertised **four perks instead of five** ever since. T-0493's mechanism shipped in `3092abc1` **with no
  copy AC**, so the affirmative sentence is now owed and belongs to nobody → filed as **T-0544**.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Method: a scripted walk of all 15 web i18n bundles
plus `values*/strings.xml` and both `Localizable.xcstrings`, matching VALUES not key names across five
locales — the same check the ticket's three guard tests make, run independently. Commit `0c665c08` records
3 web production builds green, web Jest 12/12, iOS 16/16 on an iOS 26.3 simulator, Android 17/17, and all
three guards mutation-proven. Seven render sites were found, not the four this ticket listed. **No
`manual_steps`.**

