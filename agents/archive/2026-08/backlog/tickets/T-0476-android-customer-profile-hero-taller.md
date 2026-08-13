---
id: T-0476
title: Android customer profile hero reads short — the owner wants it taller
status: draft
size: S
owner: android
created: 2026-08-02
updated: 2026-08-02
depends_on: [T-0450, T-0448]
blocks: []
stories: []
adrs: [0018]
layers: [analyst, android]
security_touching: false
manual_steps: []
sprint: 15
---

## Context

**Owner remark #1 (2026-08-02):** *"Android profile header — make it taller."*

### Ground truth — PM-verified on `master` at `0e4ede1b`

`customer-app/.../features/profile/ProfileTab.kt:270-274` — `ProfileHero` is a `BoxWithConstraints`
with **no explicit height**. Its height is entirely derived from content plus
`.padding(start = Spacing.ML, end = Spacing.ML, top = Spacing.M, bottom = Spacing.XXL)`, wrapping a
single `Row(verticalAlignment = Alignment.Top)` with a **72.dp** avatar.

**This is the shape T-0442 deliberately created** (sprint-14, merged `ce2416a0`): Android previously
stacked a second row below a `Spacer(16.dp)`; T-0442 collapsed it to ONE `Row` *specifically to match
iOS*. iOS `ProfileTab.swift:290` is the sibling `HStack(alignment: .top, spacing: 14)`.

**So "make it taller" is a request to move AWAY from the parity T-0442 just established.** That is the
owner's call to make, and it is why this is not a one-line padding bump filed as mechanical:

- If only Android grows, the two platforms diverge on a screen ADR-0018 governs and that a sprint-14
  ticket just converged.
- `ProfileTab.kt:172` carries `Spacer(Modifier.height(56.dp))` described as *"Spacer absorbs the
  overlap height"* — the hero's height is coupled to an overlapping card below it. **Growing the hero
  without moving that spacer produces a gap or an overlap**, not a taller header.
- `ProfileTab.kt:143` adds `Spacer(Modifier.height(12.dp))` for *"breathing room between the status
  bar and the hero gradient"* — a hand-rolled substitute for a status-bar inset. **T-0453 (sprint-14,
  post-demo) exists to make this hero edge-to-edge**, which changes its effective height again.

### Lane warning — this is the most contested file in the backlog

`ProfileTab.kt` currently has **four** other claimants: **T-0450** (`ready`, the verb-only edit chip),
**T-0448** (avatar upload, `blocked` on T-0450), **T-0472** (Poppins call sites at `:437`), **T-0453**
(edge-to-edge hero). This ticket is appended to that lane, **after T-0448**, for the same reason
sprint-14 §9.3.1 put T-0472 last: a height restructure landing under an in-flight avatar picker is a
three-way conflict on one 60-line composable.

## Acceptance criteria

- [ ] **AC1 — the target height is a NUMBER, chosen against the iOS sibling.** Given the hero today,
      When the change lands, Then the ticket records the measured before/after height in dp **and**
      states whether iOS `HeroGradient` moves with it or deliberately does not. "Taller" is not an
      acceptance criterion; a dp figure is. Evidence: before/after screenshots with the measurement.
- [ ] **AC2 — the overlap spacer stays correct.** Given `ProfileTab.kt:172`'s
      `Spacer(Modifier.height(56.dp))`, When the hero height changes, Then the first card below the
      hero sits at the same visual relationship to the gradient's bottom edge as before — no new gap,
      no new overlap. Evidence: screenshot of the hero→first-card seam, before and after.
- [ ] **AC3 — the chip and name row survive.** Given `ru` (`Редактировать`, T-0450's label) at 320dp
      width, When the hero renders, Then the name column is not starved and the chip still truncates
      per T-0450 AC3/AC4. `EditChipMaxWidthFraction` is **re-measured**, not inherited. Evidence: the
      320dp `ru` screenshot.
- [ ] **AC4 — ADR-0018 parity is answered, not skipped.** The verdict states in one sentence whether
      this is now a **sanctioned divergence** from iOS or whether an iOS follow-up is wanted. If a
      follow-up is wanted, it is **named as a new ticket id**, not folded in here.
- [ ] **AC5 (Gate 0.5)** — Android `:customer-app` compile + `testDebugUnitTest` re-run
      **un-cached** (`--rerun-tasks --no-build-cache`), task outcomes recorded. **Leg 1:** the evidence
      here is *screenshots and a dp measurement* — say so under leg 3; do not invent a mutation for a
      padding literal.

## Out of scope

- **iOS.** Unless AC4's ruling says otherwise, and then as a separate ticket.
- **Edge-to-edge / status-bar inset** — that is **T-0453**, already filed, sequenced post-demo.
- **The Poppins fallback on the hero name** — **T-0472**.
- **The avatar picker** — **T-0448**.
- **The partner Android profile hero.** Different app, not named in the remark.

## Implementation notes

**Short analyst read first, not a full panel.** The design question ("how tall, and does iOS follow")
is real but small. One `analyst` author + one challenger is proportionate; the ADR-0018 parity
consequence in AC4 is what the challenger must attack.

**Read `agents/knowledge/patterns-mobile.md`** and the T-0442 ticket before touching the `Row` — the
current single-`Row` shape is a deliberate outcome, not an accident.

## Status log
- 2026-08-02 — **draft (created by pm from the owner's remark #1).** The hero's current geometry, the
  T-0442 provenance, the `:172` overlap spacer coupling and the four-way lane contention were all
  PM-verified against `ProfileTab.kt` at `0e4ede1b` before ticketing.

## Review
