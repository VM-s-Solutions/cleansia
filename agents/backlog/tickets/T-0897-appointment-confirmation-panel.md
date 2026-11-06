---
id: T-0897
title: Apply the Appointment order confirmation layout
size: M
owner: Michael Chaban
created: 2026-10-30
updated: 2026-10-30
depends_on: []
blocks: []
stories: [US-mobile-20261030]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Mike selected the second order-confirmation concept, Appointment, on 30 October 2026. The Customer source inspected at startup stacked the large check, separate code/summary cards and vertical four-step timeline. The selected preview is `/Users/michael/.codex/visualizations/2026/10/29/01a186ab-892c-7343-aa31-44e6d72f03f5/order-confirmation-concepts.html`. This selection concerns the confirmation panel; rejected cleaning-status-bar concepts remain rejected.

## Acceptance criteria

- [x] **AC1 (owner correction)** — Only the immediate post-booking confirmation panel uses the selected Appointment composition: open sky-blue/dark hero, month/day calendar tile beside weekday/date/time, separate street and city address lines, and one full-width rounded-top receipt panel containing amounts and actions. Polish the selected concept rather than replace it with two outlined cards.
- [x] **AC2** — Preserve actual server totals/currency and credit/card breakdown, public order reference when supplied, selectable create-response code and existing navigation. Loading, failed reads and missing/invalid values do not fabricate appointment or payment facts.
- [x] **AC3** — Show a fixed Booking received acknowledgement of creation, with saved-details copy and the shared thin pulse renderer at received index0. Do not bind this panel to later order lifecycle status, elapsed/overrun, completion or cancellation variants. Order-detail pages, their mappings and shared Core component stay untouched.
- [x] **AC4** — Keep the existing animated cleaning mascot to the right of calendar/date when space permits. Active/motion policy controls looping with a matching still fallback; narrow/enlarged layouts omit decoration. Light/dark, locales and large text remain readable and scrollable.

- [x] **AC5 (compact follow-up)** — The success confirmation uses its natural content height rather than a forced large sheet, while the booking form keeps its existing large presentation and enlarged/overflow content remains scrollable. Address icon centers beside the full address block, unnecessary gaps shrink, and an active native animated preview shows the mascot and pulse.

## Out of scope

Backend or payment behavior, Partner UI, modifying the selected cleaning tracker, owner-checkout writes, PRO access, deployment, upload, push and merge.

## Work record

- 2026-10-30 — Ground-truthed native source and selected concept. Existing uncommitted status restoration and Xcode catalog edits were snapshotted before work. Evidence: `/Users/michael/cleansia-work.noindex/appointment-gallery-20261030/`.
- 2026-10-30 — Appointment hierarchy and truthful phase presentation are implemented locally on both Customer platforms. The confirmation code still comes from the create response; existing totals, credit/card shares and navigation remain. Native light/dark, large-text and missing-detail acceptance remains unchecked pending current-source validation.

## Current local verification — 30 October 2026

Root executed the Customer Android unit suite: **1,817 tests, zero failures/errors**, including all 14 new appointment/rotation checks. The expected old-latch RED failures and exact prior-source iOS indicator/status/overnight failures are retained separately. The iOS app-hosted focused suite passed **95 tests, zero failures** on the dedicated iOS 16.4 simulator; the built host's loopback API was verified before test launch. Formatting, strict lint, symbol, localization-preservation, docs-reference, catalog-claims and backlog checks passed.

Root's isolated Android native run retained 30 captures and passed real forward, late backward and motion-off swipe assertions: the selected offer stays through the fresh dwell, its visible capsule fills, and automatic paging resumes. Six final Appointment captures additionally verify the paired sky palette and same-row code/total at ordinary size. The iOS native pager test observes a real changed selection, a stable midpoint and a later automatic selection; captures show the selected fill advancing. Eight iOS and eight Android compact-detail captures preserve market/price, discount amounts and full wrapping addresses across both themes and large text. Independent review approved the current visual/source scope with no blocking issues.

Evidence: `/Users/michael/cleansia-work.noindex/appointment-gallery-20261030/native-final-visual-review.json`, `android-all-receipt.json`, `native-retry1/android-appointment-gallery-receipt.json`, `native-retry2/android-appointment-gallery-receipt.json` and `native-ios-retry2/ios-customer-focused-receipt.json`. Android's final changes after its full unit pass are presentation-only palette/breakpoint adjustments and were rebuilt and recaptured. Actual iOS finger-contact/physical-device smoothness is not claimed by the programmatic native pager check. The existing extreme-large-text Android discount-chip edge clipping is recorded separately and was not changed. No commit or rollout is part of this follow-up.

## Historical polish revision — superseded by owner correction

After viewing the native panel, Mike requested stronger alignment with the app design and suggested a mascot in the unused right space. Ground truth is the current Appointment source and supplied screenshot, not an older concept selection. The broad blue canvas, oversized duplicated calendar and full-width white slab are being replaced with the app’s standard inset cards, neutral ink and compact date hierarchy. Existing truth/data/payment/navigation logic, gallery, compact order details and the accepted cleaning tracker remain unchanged. Fresh evidence: `/Users/michael/cleansia-work.noindex/confirmation-polish-20261030/`.

### Current polish validation

Root rebuilt both actual Customer apps with loopback pinned and inspected the compiled API host before launch. Android retained eleven current-source native captures across EN/CS light/dark, loading/error/missing/cancelled states, large text and ordinary 320dp width. iOS passed 22 focused booking tests with zero failures and retained eighteen app-hosted captures including UK accessibility text and terminal/missing states. Both source seals held; the temporary Android emulator stopped and the dedicated iOS simulator shut down. The user’s active iPhone 17 was not a test target.

The two app-native cards, compact date hierarchy, right-side static waving art and neutral canvas render clearly in normal light/dark layouts. Narrow/enlarged layouts omit the decoration and keep scrollable data/actions. SwiftFormat, strict SwiftLint, iOS symbols, docs references, catalog claims and backlog checks passed. All three pre-existing iOS catalog byte sequences are unchanged by this polish follow-up. Only the two confirmation views and four related documentation files differ from the task-start snapshot. Temporary build output cleanup retained captures/results, dependencies and prior release archives. Evidence: `/Users/michael/cleansia-work.noindex/confirmation-polish-20261030/`.

### Historical shared tracker and animation follow-up — superseded by owner correction

Mike requested the animated mascot and the same status-bar design as order detail. Current source had a static waving image and a custom four-phase booking rail. Replace them with the existing cleaning-in-progress animation and the app’s exact order-detail tracker wrapper. That wrapper supplies the five actual lifecycle phases, pulse, phase-four estimate/overrun, completed and cancelled presentation. Loading or unknown status does not receive a guessed lifecycle bar. Existing descriptive booking copy, two-card styling, data, prices and actions remain. No Core component or business-state mapping is being changed. Fresh evidence: `/Users/michael/cleansia-work.noindex/confirmation-motion-tracker-20261030/`.

- Mascot identity clarified by Mike: use the animated cleaning mascot from order detail beside the calendar. Preserve the current calendar/address styling; welcoming was a provisional candidate and is superseded by this choice.

### Final motion/tracker validation

Both Customer apps reuse the actual order-detail wrapper and the selected cleaning animation. Android final cleaning has twelve native captures with isolated actual mascot/tracker movement and exact static equality with system motion off; four later status3/4 captures verify current captions after removing the stale future reminder. iOS current source passed 29 focused Customer tests and retained 48 app-hosted captures; current status3/4 and half-estimate/overrun images are readable in both themes. Actual confirmation mascot frames and tracker crops change while active; an inactive scene uses matching static art. iOS Reduce Motion is source-reviewed only, not a runtime claim. The separate companion tracker probe remains component evidence. Calendar/date/time/address/card/action code remains unchanged from the accepted polish.

All runtime builds were pinned to loopback before launch. Owned emulators stopped and the dedicated floor simulator shut down; the user's active iPhone17 was not touched. The provisional welcoming candidate, test-only iOS compile failure and Android caption-fixture collision are retained as historical evidence; successful final cleaning/caption runs supersede them. No production fix was needed for the fixture collision. Evidence: `/Users/michael/cleansia-work.noindex/confirmation-motion-tracker-20261030/` and the final caption/current Customer receipts in `/Users/michael/cleansia-work.noindex/auth-placeholders-20261030/`. No rollout or publication is claimed.

## Owner correction — 30 October 2026

Mike rejected the two-card native result and the expansion into lifecycle status presentations. His supplied screenshots2/3 and selected Appointment HTML are authoritative: restore the month/day tile, weekday/date/time, two-line address and original open-hero/single-receipt composition, adding only the requested polish and animated cleaning mascot. Earlier local review approvals record technical checks, not owner acceptance of that redesign. The correction removes the order-detail wrapper binding and alternate status captions from this confirmation panel only. The receipt acknowledges successful creation and uses index0 of the existing thin pulse renderer decoratively; no later status is inferred. Evidence: `/Users/michael/cleansia-work.noindex/confirmation-appointment-correction-20261030/`.

### Correction verification

Both actual Customer apps build with explicit loopback runtime hosts. The iOS 16.4 focused run passes 24 tests with zero failures, including byte-equal confirmation rendering when only later lifecycle status changes. Android passes 10 focused presentation/resource tests with zero failures/errors/skips. The obsolete Android assertion requiring assignment copy in this confirmation file was removed; the three existing resource/translation/deadline checks remain.

Fresh native evidence retains 34 iOS and 21 Android captures: selected calendar/date/two-line address/open hero/single receipt composition, light/dark, EN/CS and narrow/large fallbacks. Actual confirmation mascot and received-phase bar pixels change while active; Android system-motion-off and iOS inactive-scene equality pass. iOS Reduce Motion remains source-reviewed only. The actual iOS production bar crop is distinguished from the separate companion fixture. The unchanged shared iOS primary button may ellipsize the long Ukrainian label at extreme accessibility size; its full accessibility value remains, and no Core component was changed in this correction.

Only two confirmation views, their two affected test files and four related docs differ from this request's initial source snapshot. All prior auth/gallery/order-detail/Core/Partner/resource paths in that snapshot are unchanged. The dedicated floor simulator shut down and owned emulator stopped; active user iPhone17 was not touched. Native previews and exact receipts are retained under `/Users/michael/cleansia-work.noindex/confirmation-appointment-correction-20261030/`. No deployment, upload or publication is part of this correction.

## Compact height, alignment and moving preview — 30 October 2026

Mike reported excess unused space, top-aligned address icon and a still mascot in the linked preview. Grounding found the success route still inherited the booking form’s fixed large detent, while the confirmation stack had a viewport minimum. The old PNG was deliberately captured inactive and cannot demonstrate playback. Correct only this panel: natural-height success detent, tighter spacing, centered pin and a real active-sheet moving preview. Keep the selected composition, fixed receipt semantics and motion policy. The Android confirmation is an existing full-screen route; compact its content without migrating navigation. Evidence: `/Users/michael/cleansia-work.noindex/confirmation-compact-motion-20261030/`.

### Compact follow-up verification

The current iOS 16.4 app-hosted run passes 24 tests with zero failures. The real presented success sheet fits below 85% of the window, while the booking form and accessibility layout retain the large detent. Stable native captures verify the centered pin and reachable bottom actions after scrolling. Both light/dark recordings contain 80 actual native window samples, with measured sample delays and changing mascot frames. The shell now forwards its real scene phase into the iOS 16 booking sheet; the test-created hosting window bridges its separately verified UIKit scene state. Neither path forces an active phase.

Android retains 23 native PNG captures and a 4.38-second native MP4. Mascot/bar pixels advance while active and remain identical with system motion off. iOS inactive-scene equality passes; Reduce Motion runtime and physical-device smoothness remain unclaimed. The existing shared iOS primary button can truncate its long Ukrainian label at extreme accessibility size; its full accessibility value remains.

The three unsuccessful iOS attempts remain preserved. A native startup notification alert kept the test host inactive; a pinned, isolated AppleSimulatorUtils build resolved permission only on the dedicated simulator, with no global installation. The final retry also waits for modal transition completion before accessibility captures. Current-source seals and loopback hosts pass, and owned simulators stop after validation. No user iPhone17, prior auth/gallery/detail/Core/Partner/resource source, deployment or upload was changed by this follow-up. Useful build caches remain for subsequent UI review.
