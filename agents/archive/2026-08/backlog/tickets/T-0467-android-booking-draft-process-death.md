---
id: T-0467
title: Android booking draft is lost on process death — including a possible key-box/alarm code
status: draft
size: M
owner: analyst
created: 2026-07-30
updated: 2026-08-01
depends_on: [T-0441]
blocks: []
stories: [US-customer-access-instructions]
adrs: []
layers: [analyst, android]
security_touching: true
manual_steps: []
sprint: 14
---

## Context

Filed from the **T-0441 review** (approved: 321/321, 53/53 tasks executed, no new consistency
violations, both review findings closed and independently re-proved).

The Android booking draft lives in a **plain in-memory `StateFlow` with no `SavedStateHandle`
anywhere**. **PM-verified:** every `SavedStateHandle` occurrence under `customer-app` is in
`build/generated/**` (Hilt/KSP factories) — **there is no source usage at all**.

So if the OS reclaims the app mid-booking, the user loses **address, promo code, chosen slot,
services, and both instruction fields**.

**The reviewer rated this Medium and argued against inflating it. The PM agrees, and the reason is
the important part:** `BookingBottomSheet.kt:301` already calls `bookingVm.reset()` on every fresh
non-rebook open (three `reset()` call sites in that file: `:241`, `:301`, `:583`). So the app
**deliberately discards the draft today**. Persisting it across process death is therefore a
**behaviour change requiring product input** — not a bug fix — and the ticket is owned by `analyst`
first for exactly that reason.

## 🔒 The constraint that MUST be settled before anyone writes code

**This is in the ticket up front, deliberately, so it is not discovered in review.**

**`accessInstructions` can hold a key-box code or an alarm code — a physical security credential for
the customer's home.** T-0441 is what makes this material; before T-0441 it was theoretical, because
the field was not captured on Android at all.

Any persistence design **must**:

- [ ] **Never write the draft to unencrypted DataStore, SharedPreferences, or disk.** If persistence
      is chosen, it is encrypted at rest, or `accessInstructions` is **excluded from what is
      persisted** while the rest of the draft is kept.
- [ ] **Clear on sign-out via the `SessionScopedCache` set (S11).** A draft holding a home-entry code
      surviving to the next account on a shared handset is an **S11 violation**, not a UX nit. Join
      the Hilt multibinding (`@Binds @IntoSet … : SessionScopedCache`); do not hand-maintain a
      clear-list.
- [ ] **`SavedStateHandle` is not automatically safe.** Its bundle is written to disk by the system
      in `onSaveInstanceState`. Whether that is acceptable for a key-box code is a **decision to
      make explicitly**, not an assumption to inherit because `SavedStateHandle` is the idiomatic
      answer to process death.

**The cheapest compliant design may well be "persist everything except `accessInstructions`."** Say
so if that is the answer — a partial restore that keeps the user's address and slot while asking again
for the door code is a legitimate and arguably *better* product outcome than restoring a credential
from disk.

## Deliberation required — NOT `ready`

**Analyst panel** (author + 2–3 challengers + lead) per `agents/process/deliberation.md`. This is a
**product behaviour change**, and the current behaviour is deliberate. Questions the panel must settle:

- Should the draft survive process death **at all**, given `reset()` is called on every fresh open?
  What is the actual user story — someone who takes a phone call mid-booking, or someone who
  abandoned it?
- If yes: **what is restored, and what is deliberately not** (see the credential constraint above).
- Is there a **visible** restore affordance ("resume your booking?") or a silent restore? A silent
  restore of a half-built booking can be worse than a clean start.
- How does it interact with the **rebook** flow, which is the one path that does *not* reset?

## Acceptance criteria

_(PM floor; the panel finalizes)_

- [ ] **AC1** — The analyst panel reaches consensus and the story is finalized, **explicitly ruling on
      whether `accessInstructions` is persisted**. Evidence: the story doc with its deliberation trail.
- [ ] **AC2** — Given a booking in progress, When the OS kills and restores the process, Then the
      behaviour matches AC1's ruling exactly. Evidence: an executed process-death test (`adb shell am
      kill` or the "Don't keep activities" developer option) — **not** a unit test of a
      `SavedStateHandle` wrapper.
- [ ] **AC3 (S11)** — Given a persisted draft, When the user signs out, Then it is gone. Evidence: a
      test, plus the holder's membership in the `SessionScopedCache` roster.
- [ ] **AC4 (security)** — If anything is persisted, the security reviewer confirms the at-rest
      treatment of `accessInstructions` specifically, **naming** the risk cleared. `security_touching:
      true` is set for this reason.
- [ ] **AC5** — The existing `reset()` behaviour on fresh non-rebook open is either preserved or
      **deliberately changed with the panel's reasoning recorded**. Do not let it change as a
      side effect.

## Out of scope

- iOS parity. If the panel rules the draft should persist, **file the iOS sibling separately** — do
  not widen this ticket. (Check the iOS booking flow's current behaviour before assuming it differs.)
- The rebook flow's own state handling, beyond AC5's interaction question.
- Any change to what `accessInstructions` *is* or how it reaches the backend — that is T-0441, which
  is **approved**.

## Implementation notes

- **Archetype:** `agents/knowledge/patterns-mobile.md` (including T-0441's harvest) and the
  `SessionScopedCache` mechanism documented in **S11** (`docs/architecture/security-rules.md:209-247`).
- **Shared-file lane:** `BookingBottomSheet.kt` + the booking view-model — **no other sprint-14 ticket
  writes these**, so no serialization is needed today. Re-check at dispatch.
- `depends_on: [T-0441]` — not for code, but because T-0441 is what puts a credential in the draft.
  The constraint above is meaningless before it lands.

## Status log
- 2026-07-30 — draft (created by pm from the T-0441 review). **PM-verified:** no `SavedStateHandle` in any `customer-app` source file (all hits are `build/generated/**`), and `reset()` at `BookingBottomSheet.kt:241`/`:301`/`:583`.
- 2026-07-30 — **not `ready`**: awaiting the analyst panel (DoR item 2 — this is a product behaviour change, and the current behaviour is deliberate).
- 2026-08-01 — **T-0441's code is on `master`** (`1d85b35f`, PR #178), so the field this ticket's
  security constraint is about — `accessInstructions`, which can hold a **key-box or alarm code** — is
  now shipped and being typed into by real users on DEV. **`depends_on: [T-0441]` is formally
  unsatisfied** (T-0441 is `qa` on an owed screenshot) **but discharged in substance**: a screenshot
  does not change what the ViewModel persists. **Stays `draft` on the analyst panel**, which is DoR
  item 2 and not a dependency; it can be dispatched now.
  **The S11 constraint written into this ticket up front is now live, not anticipatory** — if the panel
  rules that drafts should survive process death, the persisted blob contains a door code, so
  "never unencrypted at rest, cleared on sign-out via `SessionScopedCache`" is a hard requirement of
  the ruling and not a nice-to-have bolted on afterwards.

## Review
<!-- analyst / reviewer / security verdicts here -->
