---
id: T-0242
title: Cancellation-fee free-window override semantics — Plus override direction contradicts the doc
status: blocked
size: S
owner: —
created: 2026-06-13
updated: 2026-06-14
depends_on: [T-0211]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 5
source: T-0211 (TC-7) carried production finding — characterization tests pinned the contradiction
---

## Context
Surfaced (not fixed) by **T-0211** (TC-7 refund/dispute money-math gap-fill, test-only wave).
`BookingPolicy.CalculateCancellationFeeRate` treats `freeCancellationHoursOverride` as a **literal
replacement** of `FreeCancellationHours` in the `h >= freeWindow` comparison. As a consequence a
**larger** override value makes the free-cancellation window **stricter** — the customer must cancel
even **earlier** to qualify for a free cancellation. The Plus-membership path (via
`CancellationPolicyResolver`) passes a larger override (e.g. `48`), which by this code path makes Plus
members' free window *less* generous, directly contradicting the code/doc comment that Plus members
get a **more generous** window. A genuinely more-generous window needs a **smaller** override under
the current literal-replacement semantics.

This is a **product-semantics decision**, not a clear-cut bug: the math is internally consistent, but
the intent (Plus = more generous) and the wiring (Plus passes a larger value) point in opposite
directions. T-0211's `CancellationFeeRateBoundaryTests` currently **pin the existing
literal-replacement behavior** (smaller override = free closer in), so any correction must update
those pinning tests to match the corrected intent.

This is money-adjacent (it changes which cancellations are free vs charged a fee) — route the standard
adversarial money review even though it is not authz/secret-touching (`security_touching: false`).

## Acceptance criteria
- [ ] **AC1 (confirm intent with the owner)** — Confirm with the owner the intended direction of the
  Plus-member free-cancellation window (Plus should be **more generous** = a *wider* free window than
  the standard tier). Record the answer (questions/answered) before changing behavior.
- [ ] **AC2 (correct the direction — one of two paths)** — Implement the owner-confirmed fix as
  **either** (a) change the Plus path (`CancellationPolicyResolver`) to pass a **smaller** override so
  the existing literal-replacement semantics widen the free window, **or** (b) **invert the override
  semantics** inside `BookingPolicy.CalculateCancellationFeeRate` so a larger override widens (not
  narrows) the free window. Pick the path that is least surprising to readers and least likely to be
  re-broken; document the choice in the handler/policy comment.
- [ ] **AC3 (tests follow the corrected intent)** — Update `CancellationFeeRateBoundaryTests` (which
  today PIN the old literal-replacement behavior) so they assert the **corrected** behavior: an
  accepted Plus order cancelled inside the *widened* free window is `0m` fee; the standard tier is
  unaffected; partial/last-minute thresholds and rates are unchanged. Red→green per
  `agents/knowledge/testing.md`.
- [ ] **AC4 (no collateral change)** — Non-Plus cancellation behavior, the partial/last-minute fee
  tiers, the oops-window short-circuits, and the `refundAmount = TotalPrice * (1m - feeRate)` formula
  are unchanged. No new error constant, no DTO/endpoint change.

## Out of scope
- The refund-amount formula and tier rates themselves (covered/pinned by T-0211 — not re-litigated).
- Admin refund UX, chargeback linkage, loyalty clawback.
- Any change to the oops-window or acceptance-aware short-circuits.

## Implementation notes
- Symbol under change: `BookingPolicy.CalculateCancellationFeeRate`
  (`src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs`). The override is the
  `freeCancellationHoursOverride` parameter compared in the `h >= freeWindow` test.
- The Plus path that supplies the override is `CancellationPolicyResolver` — verify the exact value it
  passes (T-0211 notes `48`) before deciding between path (a) and path (b).
- Existing pinning test file to update: `src/Cleansia.Tests/Features/Orders/CancellationFeeRateBoundaryTests.cs`.
- Re-read T-0211's status-log "Production observation (NOT fixed)" block for the exact pinned-direction
  evidence so the test inversion is correct.
- Adversarial money review applies — never accept on the dev's own green suite.

## Status log
- 2026-06-13 — draft (created by pm; Wave-4 Batch 4A/T-0211 carried finding made a ticket — Wave-5 candidate).
- 2026-06-13 — **blocked** (PM, Wave-5 intake). Dep T-0211✓ is `done`, but **AC1 requires an owner
  product decision** (Plus free-cancellation-window direction) before behavior can change — opened as
  **Q-W5-1 (blocking: yes)** in `questions/open.md`. The fix is otherwise small (S, money-adjacent →
  adversarial review). It edits `BookingPolicy.cs` + `CancellationFeeRateBoundaryTests.cs`; once Q-W5-1
  is answered it unblocks into **Batch 5B** and **serializes against any other ticket touching
  `BookingPolicy.cs`** (none in this wave). Held so the rest of Wave 5 proceeds; gated only on Q-W5-1.)
- 2026-06-14 — **stays blocked — CARRIED past Wave-5 close** (PM, Wave-5 close-out). Wave 5 closed with
  every other ticket `done`/deferred; this one remains **blocked on Q-W5-1** (owner product decision on the
  Plus free-cancellation-window direction — still unanswered in `questions/open.md`). No behavior or
  test-pin change has landed. It carries forward to whenever the owner answers Q-W5-1; at that point it
  unblocks as an S, money-adjacent (adversarial money review) ticket editing `BookingPolicy.cs` +
  `CancellationFeeRateBoundaryTests.cs`, serializing against any other `BookingPolicy.cs` writer. On the
  owner action list (see `status/sprint-7.md` §close-out).

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
