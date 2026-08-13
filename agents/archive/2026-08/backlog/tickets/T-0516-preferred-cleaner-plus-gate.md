---
id: T-0516
title: Preferred cleaner is advertised as a Plus perk but any customer can use it — decide and gate
status: done
size: S
owner: backend
created: 2026-08-02
updated: 2026-08-04
depends_on: [T-0495]
blocks: []
stories: []
adrs: [0036, 0039]
layers: [backend]
security_touching: true
manual_steps: []
sprint: 15
---

## Context

**PM-verified 2026-08-02.** The preferred-cleaner feature is advertised as a Cleansia Plus perk on
every client:

- iOS `booking_preferred_cleaner_subtitle`: *"**Plus benefit** · choose someone who's cleaned for you before"*
- Android `values/strings.xml:721`: `<!-- **Plus**: pre-request a favorite cleaner -->`, and
  `membership_perk_favorite_cleaner_title` = *"Your favorite cleaner"*
- Web `en.json:1096-1097`: listed under the membership benefits block

**The server does not check membership.** `CreateOrder`'s validator gates it on exactly one thing —
that the customer has **completed an order with that cleaner** (`CreateOrder.cs:140-154` →
`UserHasCompletedOrderWithEmployeeAsync`). **No membership rule of any kind.** A non-subscriber who has
used a cleaner once can request them.

**This is the same shape as T-0494** (the recurring perk gated client-side only): a capability
advertised as paid, obtainable without paying. It is filed separately from that ticket because the
remedy is a *product decision first* — the recurring gate was unambiguously meant to be paid; this one
may reasonably be universal.

### Why it is `blocked`

**`Q-PLUS-03`, `blocking: yes`.** Two defensible answers with opposite code:

- **Universal** → the copy is wrong on three clients × five locales, and this becomes a **copy**
  ticket, not a backend one.
- **Plus-only** → a server-side membership check in `CreateOrder`'s validator, and **existing
  non-member customers who have used the feature lose it**, which is a customer-communication question
  the owner also owns.

**Guessing either way ships a wrong thing:** gate it and you break a working feature for
non-subscribers; leave it and you keep selling something everyone already has. **The PM has not taken
a default.**

## Acceptance criteria

- [ ] **AC0 — the owner answers `Q-PLUS-03`: universal, or Plus-only?** The ticket does not move until
      then. Evidence: the answer in `questions/answered.md`.
- [ ] **AC1 — if PLUS-ONLY: the check is server-side, in the validator, and refuses a non-member.**
      Not a client filter — **T-0494 exists because that mistake was already made once.** Evidence: the
      rule at file:line plus an integration/host test posting `PreferredEmployeeId` without an active
      membership and being refused.
- [ ] **AC2 — if PLUS-ONLY: a lapsed/cancelled membership is treated as a non-member.** Read
      `UserMembership.MembershipStatus`, not the existence of a row. Evidence: the test case.
- [ ] **AC3 — if PLUS-ONLY: reuse the predicate T-0494 already shipped.** **T-0494 landed in PR #189:**
      `IUserMembershipRepository.GetActiveForUserNoTrackingAsync(userId, ct)`, used at
      `CreateRecurringBooking.cs:84-92`, with the error key
      `BusinessErrorMessage.RecurringTemplateMembershipRequired` as the shape to mirror. Three tickets,
      one predicate. Evidence: the call site, cross-noted.
- [ ] **AC4 — if PLUS-ONLY: existing non-member customers are addressed.** How many have used it? Are
      in-flight orders honoured? **This is a fact the owner needs, not a technical detail.** Evidence:
      the count plus the ruling.
- [ ] **AC5 — if UNIVERSAL: the perk is removed from the Plus copy on all three clients × five
      locales, and the ticket is re-owned to the client charters.** **Selling a universal feature as a
      paid perk is the misrepresentation, and it is the thing this ticket exists to stop.** Evidence:
      the re-file, or the copy diff.
- [ ] **AC6 — the completed-order eligibility rule is left alone unless T-0495 ADR AC5 ruled on it.**
      Evidence: the citation.
- [ ] **AC7 — the SECURITY gate runs.** `security_touching: true` — this is an authorization decision
      about a paid capability. Evidence: the security verdict.
- [ ] **AC8 — a test that goes red against the pre-change code (Gate 0.5 leg 1)** for whichever branch
      lands, re-run un-cached by the verifier.
- [ ] **AC9 (Gate 0.5)** — `Cleansia.Tests` / `Cleansia.IntegrationTests` / `Cleansia.HostTests`
      **locally**, baselines **2295 / 108 / 75**.

## Out of scope

- **The dispatch mechanism** — **T-0495** (design) and **T-0515** (build). **This ticket is only about
  WHO may use the feature, not what the feature does.**
- **The recurring-booking gate** — **T-0494**, same defect shape, already filed, no dependency.
- **The express perk's gate** — **T-0493**.

## Implementation notes

**No panel of its own — T-0495 is the panel**, and its AC8 designs both branches so that whichever the
owner picks is already specified.

**AC1's branch is genuinely small** (one `MustAsync` alongside the existing eligibility rule) — the
ticket is `S` for a reason. **AC5's branch is not backend work at all** and re-files.

**Read first:** `CreateOrder.cs:140-155`, `UserMembership.cs` + `MembershipStatus.cs`, T-0494's
predicate if it has landed, and the three copy strings quoted in the Context.

## Status log
- 2026-08-02 — **draft → `blocked` immediately (created by pm from the owner's 2026-08-02
  favourite-cleaner answer).** The owner ruled the perk must **work**; they did not rule **who gets
  it**. Filed `blocked` on **`Q-PLUS-03`** rather than defaulted, because the two answers have opposite
  diffs and one of them is not a backend ticket at all. **PM-verified:** `CreateOrder.cs:140-154`
  contains the completed-order eligibility rule and **no membership rule of any kind**, while all three
  clients label the feature a Plus benefit.
- 2026-08-04 — **done** (PM sprint-15 reconciliation). `Q-PLUS-03` was answered by the owner
  (*favourite cleaner is Plus-only*, `2caa5f82`, carried by ADR-0036 D7), which unblocked this ticket; the
  gate shipped in `b6f1c2a2` *"fix(security): rate-limit, Plus-gate and active-filter the favourite-cleaner
  feed"*. **Verified at HEAD:** `GetMyServingCleaners.cs:45-46` —
  `ResolveSlotAvailability(bool hasActiveMembership, bool? evaluatedAvailability) => hasActiveMembership ?
  evaluatedAvailability : null;` — the gate lands **on the flag, not the list**, and the handler reads the
  membership at `:90`. `OrderController.cs:187` carries `[EnableRateLimiting("auth")]`.
- 2026-08-04 — **the ruling that made this a SERVER ticket is worth preserving:** ADR-0039 ruled that the
  three disclosure gaps block the server ticket, **not the picker UI** — the exploit is `curl`, and what a
  client renders is irrelevant. Gating the UI ticket was the natural call and would have shipped the oracle
  behind a greyed row. Gating the LIST (rather than the flag) would have changed a shipped contract and
  emptied it, which both clients render as *"no picker at all"*.
- 2026-08-04 — **two further gaps closed in the same change, both beyond this ticket's original scope:** a
  departed cleaner was still offered and would compute as FREE once the flag shipped (both
  `Employee.IsActive` and `User.IsActive` are now checked, because `Deactivated()` on one leaves the other
  untouched), and the query pulled **127 columns to use 4, tracked, including IBAN and PassportId into a
  customer-facing handler** — now four columns, `AsNoTracking`, with `Take(20)` bounding at the source.

## Review

**MANUAL-GATE (PM reconciliation, 2026-08-04).** Read at HEAD: `GetMyServingCleaners.cs:1-95` and
`Web.Customer/Controllers/OrderController.cs:180-196`. `b6f1c2a2` records every gate mutation-proved with
negative controls holding — removing `User.IsActive` fails **only** the user-deactivated case (exactly the
gap an Employee-only check would miss), restoring the old projection fails 5 including the IBAN/PassportId
guard, and the 429 proof floods the real limiter middleware showing a second subject on the same IP still
served, proving the window is per-subject. **`manual_steps` discharged** — the response's nullable bool
regen landed at `53f887b6` and the mobile re-dump at `37440bbc`.

