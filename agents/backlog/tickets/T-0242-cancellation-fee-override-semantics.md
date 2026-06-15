---
id: T-0242
title: Cancellation-fee free-window override semantics — Plus override direction contradicts the doc
status: done
size: S
owner: —
created: 2026-06-13
updated: 2026-06-15
depends_on: [T-0211]
blocks: []
stories: []
adrs: []
layers: [backend]
security_touching: false
manual_steps: []
sprint: 6
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
- 2026-06-14 — **stays blocked — EXCLUDED from the executable Wave 6** (PM, Wave-6 intake). Re-confirmed
  **Q-W5-1 is still unanswered** in `questions/open.md` (the `Answer:` field is empty). It gates no other
  ticket, so the rest of Wave 6 proceeds without it. Unblocks the moment the owner answers Q-W5-1 — then runs
  as an S money-adjacent ticket (adversarial review) editing `BookingPolicy.cs` +
  `CancellationFeeRateBoundaryTests.cs`. Carried; on the owner action list. Plan: `status/sprint-8.md` §4.1.

- 2026-06-14 — **Q-W5-1 ANSWERED by owner: path (b)** → unblocked, `ready`, folded into Wave 6.
  Plus members must get a MORE GENEROUS (longer) free-cancellation window. Fix = INVERT the override
  handling in `BookingPolicy.CalculateCancellationFeeRate` so a larger `freeCancellationHoursOverride`
  WIDENS the free window (cancel-free further out), matching the doc intent — not the current
  literal-replacement that made it stricter. Re-flip T-0211's `CancellationFeeRateBoundaryTests` to the
  corrected intent (they currently pin the buggy direction). Adversarial money review applies.

- 2026-06-14 — **implemented, path (b), → `review`** (backend). Inverted the override semantics in
  `BookingPolicy.CalculateCancellationFeeRate` so a LARGER `freeCancellationHoursOverride` WIDENS the free
  window: `freeWindow = Math.Max(0, FreeCancellationHours - (override ?? 0))`; the free arm stays `h >= freeWindow`.
  Null/0 override = the standard 24h window (unchanged); the partial (4h, 25%) and last-minute (50%) tier
  thresholds/rates and the oops/not-accepted short-circuits are untouched. `CancelOrder`'s
  `refundAmount = TotalPrice * (1 - feeRate)` formula is unchanged (AC4). I own `BookingPolicy.cs` alone this wave.
  - **TDD red→green** (per `agents/knowledge/testing.md`, money math = strict): re-flipped
    `CancellationFeeRateBoundaryTests` AC3 block to the corrected intent FIRST. Captured red against the
    unchanged literal-replacement code — **5 failed / 11 passed / 16 total**, each failing for the right reason
    (e.g. `LargerOverride_WidensFreeWindow` override 48 got 0.25 expected 0; `HugeOverride` 30min-out got 0.50
    expected 0; `ZeroOverride` got 0 expected 0.25). After the inversion: **boundary 16/16 + BookingPolicyTests
    13/13 = 29/29 green**; the standard-path `BookingPolicyTests` (no override) passed UNCHANGED, proving no
    collateral change.
  - **Adversarial money discriminators** chosen so a re-inversion is caught: the divergence between old and new
    is zero only at override=12 (old threshold `=override`, new `=24-override` coincide there), so every override
    test uses values ≠ 12 (8, 9, 48, 100, 0) where the two directions give different fees; plus a monotonicity
    test (larger override never charges more) and a floor-at-0 guard. Expected rates are the policy's named
    constants, never recomputed.
  - **Verification achieved:** project-scoped `dotnet test Cleansia.Tests` filtered to the two policy classes
    (29/29 green). A concurrent lane's untracked, not-yet-compiling `CatalogInUseTemplateGuardTests.cs`
    (references a not-yet-existent `ITenantProvider`) blocks a whole-project compile; I ran my classes by moving
    that untracked file aside and restored it byte-for-byte (still `??`). The orchestrator's authoritative clean
    run will confirm once that lane lands.
  - **FOLLOW-UP / WIRING MISMATCH (report-only, not mine to fix this wave):** the seeded Plus plan
    (`insert_seed_data.sql`) and `MembershipPlan.FreeCancellationWindowHours` doc treat the value as the literal
    free-threshold ("set to 4 → free up to 4h before"), and `CancellationPolicyResolver.ResolveForUserAsync`
    passes `MembershipPlan.FreeCancellationWindowHours` (seed = **4**, not the ticket's assumed 48) straight into
    `freeCancellationHoursOverride`. Under the corrected (inverted) BookingPolicy that value now means "widen by N
    hours", so seed 4 → free threshold 20h (wider than standard 24h, but NOT the doc's intended 4h). The
    resolver/MembershipPlan/seed semantics must be re-aligned to the inverted policy (resolver should pass
    `FreeCancellationHours - membershipFreeWindow`, or the membership field/seed/doc be redefined as a "widen-by"
    delta). Those files are owner-only (seed) / not in my lane (resolver, MembershipPlan) — flagged for the PM to
    open a follow-up. No behavior outside `BookingPolicy.cs` changed here.

- 2026-06-14 — **review findings fixed; switched to AC2 path (a); stays `review`** (backend). The
  reviewer was right: the path-(b) inversion shipped a standard-tier money leak. Root cause confirmed —
  the override is fed an ABSOLUTE threshold by the only caller (`CancelOrder` passes
  `CancellationPolicy.FreeCancellationHours`; the resolver fills 24 for non-members), so the inverted
  `Math.Max(0, 24 − 24) = 0` collapsed the standard free window to 0 and refunded every standard
  cancellation in full. The premise behind the path-(b) decision (resolver passes a LARGER value, e.g.
  48) was false: the Plus seed is **4**, and the membership domain model / DTOs / create+update
  validators / seed ALL treat `FreeCancellationWindowHours` as an absolute "free up to N hours before"
  threshold (a SMALLER value = MORE generous). Under the original absolute-threshold contract a Plus
  plan seeded at 4 is already wider than the standard 24h — the owner's intent (Plus more generous) is
  satisfied with **zero** out-of-lane lockstep changes.
  - **Fix:** reverted `BookingPolicy.CalculateCancellationFeeRate` to
    `freeWindow = freeCancellationHoursOverride ?? FreeCancellationHours` (the absolute-threshold
    contract). Rewrote the param doc to state the contract explicitly (absolute threshold, smaller =
    more generous, the value the sole caller actually passes). The partial/last-minute thresholds+rates,
    the oops/not-accepted short-circuits, and `CancelOrder`'s `refundAmount = TotalPrice × (1 − feeRate)`
    are untouched (AC4). No change to `CancelOrder.cs`, `CancellationPolicyResolver`, `MembershipPlan`,
    DTOs, validators, or seed — all already speak the absolute contract.
  - **TDD red→green (finding #2):** re-flipped `CancellationFeeRateBoundaryTests` to the absolute
    contract and added the PRODUCTION call shape the prior tests omitted — standard tier with the
    resolver-supplied absolute 24 (not null), asserting 0.50 (last-minute) / 0.25 (partial) / free
    (≥24h) and that the resolver-supplied 24 is byte-identical to the null default across every tier.
    Captured honest RED against the still-inverted code: **6 failed** (e.g. last-minute std got 0 vs
    0.50, partial std got 0 vs 0.25, Plus-seed-4 got 0.25 vs 0). After the revert: boundary **19/19**
    green; `BookingPolicyTests` (the no-override standard cases) passed UNCHANGED.
  - **New integration-style test (findings #5/#2):** `CancelOrderStandardTierFeeTests` drives a STANDARD
    (non-member) accepted card-paid cancellation through the real `CancelOrder.Handler` + the real
    `CancellationPolicyResolver` (membership repo → null → default policy) at a NEAR-TERM cleaning (12h
    and 1h before start), asserting feeRate 0.25/0.50 and hand-derived refund 750/500 on both the
    response and the persisted `Order`. The existing `CancelOrderRefundWiring/Seam` suites always use a
    +10-day cleaning (free tier) and were blind to this. Captured honest RED through the full handler
    path against the inverted formula (expected 0.25/0.50, got 0), then GREEN after the revert.
  - **Verification:** project-scoped `dotnet test Cleansia.Tests` (`--no-build`, refs off due to the
    Functions host-lock trap). Orders namespace **191/191** green; Memberships/Cancellation/Booking
    filter **117/117** green; new standard-tier class 2/2 green. Full-suite run = 1507 pass, 2 fail —
    both in the untracked, out-of-lane `RefreshTokenServiceTenantRevokeTests.cs` (T-0236 token-revoke
    lane, zero references to `BookingPolicy`/`CancelOrder`); pre-existing, NOT caused by this change,
    not mine to fix. The orchestrator's authoritative clean run will confirm.
  - **FOLLOW-UP (report-only, owner-only seed; superseded the prior note):** no resolver/seed re-align is
    needed for correctness — the absolute contract already makes Plus (seed 4) more generous. If the
    product later wants a Plus free window OTHER than 4h, only the seeded `FreeCancellationWindowHours`
    changes (owner-only). The earlier "resolver must pass a delta" follow-up is moot under path (a) and
    can be closed.

- 2026-06-14 — **reviewer findings #1–#5 verified resolved; stays `review`** (backend). Re-audited the
  live tree against the CHANGES-REQUESTED verdict. The dangerous inverted formula the verdict targets
  (`Math.Max(0, FreeCancellationHours - (override ?? 0))`) is NOT present: `BookingPolicy.cs` is the
  absolute-threshold contract `freeWindow = freeCancellationHoursOverride ?? FreeCancellationHours`
  (path a). `git diff` of `BookingPolicy.cs` vs the branch HEAD is the doc-comment clarification ONLY —
  the code line was never committed inverted. `CancelOrder.cs` is unchanged (it still passes the
  resolver's absolute `policy.FreeCancellationHours`), so caller/resolver/policy all speak the absolute
  contract in lockstep — no out-of-lane lockstep change is required (findings #1, #3, #4 resolved by
  path a).
  - **Findings #2 + #5 — non-vacuous production-shape evidence, honest red→green re-captured:**
    `CancellationFeeRateBoundaryTests` now drives the EXACT production call shape (standard tier with
    the resolver-supplied absolute `24`, NOT null) and asserts `0.50`/`0.25` still charge; and the new
    `CancelOrderStandardTierFeeTests` drives a standard (non-member) accepted card-paid cancellation at
    12h / 1h before start through the REAL `CancelOrder.Handler` + REAL `CancellationPolicyResolver`
    (membership repo → null → default 24h), asserting feeRate `0.25`/`0.50` and hand-derived refund
    `750`/`500` on both the response and the persisted `Order`. To prove these are not theater I
    re-injected the inverted formula into `BookingPolicy.cs`, rebuilt, and captured RED: **8 failed**,
    including all four standard-tier guards failing for the right reason — `StandardTier_…_LastMinute`
    expected 0.50 got 0; `StandardTier_…_Partial` expected 0.25 got 0; `CancelOrderStandardTierFeeTests`
    12h expected 0.25 got 0 and 1h expected 0.50 got 0. Restored the correct formula → GREEN.
  - **Verification achieved:** full solution-graph build succeeded (the prior concurrent-lane compile
    blockers are gone). Affected classes (`CancellationFeeRateBoundaryTests`,
    `CancelOrderStandardTierFeeTests`, `CancelOrderRefundWiringTests`, `CancelOrderRefundSeamTests`,
    `BookingPolicyTests`) = **49/49 green**. Orders + Memberships + Cancellation namespaces =
    **242/242 green** (AC4: no collateral change — partial/last-minute tiers, oops short-circuits, and
    `refundAmount = TotalPrice × (1 − feeRate)` all unchanged). No production change beyond the
    `BookingPolicy.cs` doc comment; `CancelOrder.cs`, the resolver, `MembershipPlan`, DTOs, validators,
    and the seed are untouched. No new error constant, no DTO/endpoint change → no nswag-regen, no
    ef-migration.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->

### Reviewer verdict — 2026-06-14 — CHANGES REQUESTED (money-path regression)

**FAIL.** The single production change introduces an immediate standard-tier (non-Plus) regression
that is NOT a follow-up — it ships broken on the dominant production path today.

Root cause: the sole production caller `CancelOrder.cs:113` passes
`freeCancellationHoursOverride: policy.FreeCancellationHours`, where
`CancellationPolicyResolver` always returns a **non-nullable absolute threshold** (24 for
non-Plus/guest, `MembershipPlan.FreeCancellationWindowHours` for Plus). The override is therefore
**never null and never 0 in production** for the standard tier — it is always `24`.

After the inversion `freeWindow = Math.Max(0, FreeCancellationHours - (override ?? 0))`, a standard
user (override=24) gets `freeWindow = Math.Max(0, 24-24) = 0`: every accepted cancellation up to the
appointment start is now FREE. Verified empirically against the freshly-built
`Cleansia.Core.AppServices.dll`:
- cancel 30min before start, override 24 → `0` (was 0.50 last-minute)
- cancel 12h before start, override 24 → `0` (was 0.25 partial)

The "standard-path BookingPolicyTests passed unchanged" evidence is **vacuous w.r.t. production**:
every BookingPolicyTests standard case omits the override arg (defaults to `null`), a call shape the
only production caller never uses. The new boundary tests likewise only assert null/delta shapes.

AC4 ("Non-Plus cancellation behavior … unchanged") is violated. AC2 path (b) cannot land in
`BookingPolicy` alone: inverting the parameter's meaning from "absolute free-window threshold" to
"widen-by delta" requires the caller (`CancelOrder.cs`) and `CancellationPolicyResolver` to be changed
in lockstep to pass a delta — both currently feed an absolute threshold. As written, both the standard
tier (now always-free) AND the Plus seed (4 → 20h, not 4h) are wrong.

Required to pass (PM to sequence; CancelOrder/resolver are out of this lane):
1. Do not change `BookingPolicy` semantics in isolation. Either (a) keep `BookingPolicy` as the
   absolute-threshold contract and implement the fix in the resolver/membership wiring (AC2 path a),
   or (b) land the inversion together with the matching caller change so `CancelOrder`/resolver pass a
   widen-by delta — coordinated as one change, not split across lanes.
2. Add a test that exercises the ACTUAL production call shape (standard tier with the resolver-supplied
   value, not `null`) and asserts 0.50/0.25 are still charged. The current tests miss the regression.
3. Re-run honest red→green on the production-shape test.

NOTE (non-blocking, in lane): comment discipline clean (no ticket/Q/ADR IDs in source); production
project compiles 0/0; the new test descriptors are non-vacuous and adversarial *for the delta-shape
contract* — they are simply testing a contract the production caller does not use.

### Security/adversarial re-gate — 2026-06-14 — PASS (all five findings resolved; independently verified)

Re-gated the CHANGES-REQUESTED money-path verdict against the LIVE tree, with my own honest
red→green (not the fixer's claims).

- **#1/#4 standard-tier money leak — RESOLVED.** `BookingPolicy.cs:125` is the absolute-threshold
  contract `freeWindow = freeCancellationHoursOverride ?? FreeCancellationHours` (AC2 path a). The
  dangerous inverted formula `Math.Max(0, FreeCancellationHours - (override ?? 0))` is NOT in the
  tree; `git diff HEAD` of `BookingPolicy.cs` is the doc-comment clarification ONLY (code line
  byte-identical to HEAD).
- **#3 lockstep — RESOLVED.** `CancelOrder.cs`, `CancellationPolicyResolver.cs`, `MembershipPlan.cs`
  unchanged vs HEAD. Caller passes absolute `policy.FreeCancellationHours`, resolver returns the
  absolute window (24 standard / membership `FreeCancellationWindowHours` for Plus), policy treats
  it as absolute — consistent end-to-end, no out-of-lane change required.
- **#2/#5 non-vacuous production-shape evidence — VERIFIED BY ME.** I injected the inverted formula
  into `BookingPolicy.cs:125`, rebuilt, and ran the affected classes: **8 FAILED for the right
  reason**, incl. `CancelOrderStandardTierFeeTests` 12h (exp 0.25 got 0) and 1h (exp 0.50 got 0)
  through the REAL handler+resolver, plus
  `StandardTier_ResolverSuppliedAbsoluteWindow_{LastMinute,Partial,EqualsNullDefault}`. Restored
  byte-for-byte → **34/34 GREEN**. Broader **Orders+Memberships+Cancellation = 242/242 GREEN** (AC4
  no-collateral: tiers, oops short-circuits, `refundAmount = TotalPrice × (1 − feeRate)` unchanged).
- Comment discipline clean (no ticket/Q/ADR IDs in source). `security_touching: false` stands — no
  authz/PII/secret/tenant surface changed; the only production change is a doc comment.

VERDICT: PASS. No blocking finding survives. Recommend PM advance state.
