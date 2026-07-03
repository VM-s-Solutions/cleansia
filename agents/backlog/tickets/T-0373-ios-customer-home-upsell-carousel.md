---
id: T-0373
title: "iOS customer Home upsell carousel — port the Android HorizontalPager of gradient mascot upsell slides (TabView(.page) + BrandGradients)"
status: proposed
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: [T-0368, T-0372]
blocks: []
stories: []
adrs: [ADR-0014, ADR-0018]
layers: [ios]
security_touching: false
priority: medium
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster brand-assets — design delta #1)
---

> **The largest single visual gap on the most-seen screen.** Android's HomeTab builds a `HorizontalPager` of
> gradient `UpsellSlide` cards (Plus upsell / setup-recurring / welcome / referral / book-hero), each with a
> dedicated mascot and per-slide gradient + CTA (`HomeTab.kt:426-500` — `buildList` of `UpsellSlide` with
> `mascotRes` + gradients + `rememberPagerState`). iOS `HomeTab` is a flat static stack — GreetingHeader,
> ProfileNudgeCard, a plain sparkles `BookCard`, MembershipManagementCard, RecurringEntryRow
> (`HomeTab.swift:36-71`, `:151-172`, `:162`). An AR-DP-1 (layout-region) miss. Not iOS-16-specific.

## Context
Deliberately split from T-0372 because it is **structural, not just assets**: it needs the mascot imagesets
(T-0372) AND mounts on the restructured shell/pager surface (T-0368) — building it against the pre-T-0368
HomeTab would mean immediate rework, hence `proposed` until both land.

## Acceptance criteria
- [ ] **AC1 (pager)** — HomeTab's `BookCard` is replaced by a SwiftUI `TabView(.page)` pager (the ADR-0018
  D3-style mapping for HorizontalPager) of gradient upsell cards; `MembershipManagementCard` +
  `RecurringEntryRow` stay below, as on Android.
- [ ] **AC2 (slide model parity)** — Same slide predicate logic (`isPlus` / `showSetupRecurring` /
  `hasAnyOrders`), same slide order, same gradients — a `BrandGradients` set defined in `CleansiaCore`
  mirroring the Android values — each slide with its dedicated mascot (`mascot_ready`/`mascot_idea`/
  `mascot_mopping`/`mascot_cleaning` ×2) trailing-aligned, plus the per-slide CTA wiring (Plus →
  SubscribePlus, book-hero → booking sheet, referral → Rewards, setup-recurring → recurring flow).
- [ ] **AC3 (Gate-DP)** — Review cites `HomeTab.kt:426-500` as the reference; divergences (if any) recorded
  as one-line notes; the pager renders correctly on the iOS 16.4 simulator (T-0374 leg) with no clipping
  against the T-0368 pill-bar inset contract.
- [ ] **AC4 (non-regression)** — Customer suite green; swiftformat/swiftlint --strict clean.

## Out of scope
- The mascot assets/animator (T-0372) and the shell/pager restructure (T-0368) — hard dependencies.
- Any new upsell slide not present on Android (parity port only).

## Implementation notes
- Nested pagers caution: the Home tab root itself lives inside the T-0368 shell content pager — the slide
  carousel is a SECOND, inner `TabView(.page)`; verify the gesture coexistence on iOS 16 (inner pager should
  win horizontal drags within its frame; this is the same composition Android ships).
- Slide card = gradient RoundedRectangle + text/CTA column + trailing mascot Image; height fixed to the
  Android card ratio.

## Status log
- 2026-07-03 — filed `proposed` by pm from the phase/ios-fix1 diagnosis (brand-assets cluster, design delta
  #1). Held until T-0368 (shell) + T-0372 (imagesets) are done — building against the pre-restructure HomeTab
  would be immediate rework.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
