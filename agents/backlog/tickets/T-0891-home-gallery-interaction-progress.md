---
id: T-0891
title: Resume Home gallery autoplay and live progress after swiping
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-30
depends_on: []
blocks: []
stories: [US-mobile-20261029, US-mobile-20261030]
adrs: []
layers: [android, ios]
security_touching: false
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1 (revised 30 October)** — Manual paging pauses automatic movement through the gesture and settling, then starts a fresh six-second dwell on the settled page. Its selected indicator stays visible and fills from the same live clock that advances the page; automatic paging resumes after that dwell, including after backward or cancelled drags.
- [x] **AC2** — Use smoother paging and restrained small borders; preserve lifecycle pause/resume and reduced motion.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS retains a visible selected indicator after native manual paging, preserves documented pause-for-visit behavior, uses thin card/indicator borders and shared 450 ms page transitions with a 530 ms sentinel delay. Actual Home header/scroll context visibly settles last→first and shows readable CTA/corners. The isolated y16 under-status-bar crop was a fixture artifact; product safe-area workaround experiments were removed. Matching previous/final native captures and measured bounds are retained. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.
- 2026-10-30 — Mike reports that autoplay and live dots/chip stop after scrolling. Source confirms the persistent `userInteracted` latch on both platforms. His new request supersedes the previous permanent manual-pause behavior. Preserve background/offscreen and accessibility pauses while resuming after settled manual input. Fresh evidence is isolated in `/Users/michael/cleansia-work.noindex/appointment-gallery-20261030/`.
- 2026-10-30 — The current source replaces that permanent latch with interaction/settling pauses and a fresh six-second dwell. Android's extracted old-latch behavior executed 14 focused tests with three intended manual-resume failures before the correction; exact baseline, command log and RED receipt remain in the fresh evidence folder. Current-source native resume/fill acceptance remains unchecked pending root validation.

## Local verification review — 29 October 2026

Full native suites and captures verify the persistent fully filled manual indicator and existing permanent manual autoplay pause. iOS actual-Home wrap settles after the450ms transition with530ms configured reanchor delay; fixture-only safe-area workaround was removed. Android isolated gallery captures verify manual pause/indicator behavior, while actual Home supplies system insets/address header.

Ready for source review; no push, merge, deployment or upload was performed for this batch.

## Current local verification — 30 October 2026

Root executed the Customer Android unit suite: **1,817 tests, zero failures/errors**, including all 14 new appointment/rotation checks. The expected old-latch RED failures and exact prior-source iOS indicator/status/overnight failures are retained separately. The iOS app-hosted focused suite passed **95 tests, zero failures** on the dedicated iOS 16.4 simulator; the built host's loopback API was verified before test launch. Formatting, strict lint, symbol, localization-preservation, docs-reference, catalog-claims and backlog checks passed.

Root's isolated Android native run retained 30 captures and passed real forward, late backward and motion-off swipe assertions: the selected offer stays through the fresh dwell, its visible capsule fills, and automatic paging resumes. Six final Appointment captures additionally verify the paired sky palette and same-row code/total at ordinary size. The iOS native pager test observes a real changed selection, a stable midpoint and a later automatic selection; captures show the selected fill advancing. Eight iOS and eight Android compact-detail captures preserve market/price, discount amounts and full wrapping addresses across both themes and large text. Independent review approved the current visual/source scope with no blocking issues.

Evidence: `/Users/michael/cleansia-work.noindex/appointment-gallery-20261030/native-final-visual-review.json`, `android-all-receipt.json`, `native-retry1/android-appointment-gallery-receipt.json`, `native-retry2/android-appointment-gallery-receipt.json` and `native-ios-retry2/ios-customer-focused-receipt.json`. Android's final changes after its full unit pass are presentation-only palette/breakpoint adjustments and were rebuilt and recaptured. Actual iOS finger-contact/physical-device smoothness is not claimed by the programmatic native pager check. The existing extreme-large-text Android discount-chip edge clipping is recorded separately and was not changed. No commit or rollout is part of this follow-up.
