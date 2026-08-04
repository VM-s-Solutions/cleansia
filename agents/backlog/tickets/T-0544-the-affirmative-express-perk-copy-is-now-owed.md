---
id: T-0544
title: Plus advertises four perks instead of five — the affirmative express copy is now owed and belongs to nobody
status: ready
size: S
owner: analyst
created: 2026-08-04
updated: 2026-08-04
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

## Review
<!-- reviewer writes the verdict here; PM reconciles before advancing state -->
