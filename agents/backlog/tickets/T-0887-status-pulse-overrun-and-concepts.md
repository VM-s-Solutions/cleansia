---
id: T-0887
title: Restore active status pulse and align overrun feedback
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — Current native active status pulses gently and respects reduced motion/background state; Partner elapsed progress and overrun use the same estimate semantics as Customer.
- [x] **AC2** — Provide three meaningfully different status-component concepts with normal/overrun states for Mike to select; do not silently select a new layout.
- [x] **AC3** — Following Mike's rejection of the concepts, restore the original thin, borderless five-phase tracker with its moving highlight. Keep live estimated cleaning progress in phase four, an explicit overtime warning, and phase five incomplete until server completion in both apps and themes. Reduced motion and background state pause the highlight.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and implementing the rejected concepts.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS restores a gentle pulse only on the current live phase/marker, paused for Reduce Motion/inactive scene. Partner maps the estimated duration, shows live fraction/remaining time and amber overtime using the shared tracker surface. Native light/dark overtime captures and floor timing boundaries pass. Final replacement layout remains Mike’s concept choice. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.
- 2026-10-29 — Mike rejected the preview designs and requested the previous bar plus cleaning progress in its fourth segment. Source triage confirms the previews were never native implementations. The earlier native renderer at `77de66282` used 4-point borderless segments and a left-to-right highlight; the later `701d6a7ce` renderer introduced thicker borders. Restore only the earlier appearance while retaining the existing timing bindings. New evidence is isolated in `/Users/michael/cleansia-work.noindex/status-bar-restore-20261029/`; previous receipts remain historical.

## Local verification review — 29 October 2026

The original pulse/overrun batch was covered by full native suites, iOS16.4 focus and native captures. The full-component and subsequent bar-only previews remain historical; Mike rejected the designs and requested the earlier native appearance instead.

The restoration passes both Android app debug builds and 12 focused Core/Customer tests, with 28 actual native captures covering both apps, normal/missing/overrun/completed states, both themes and system animations disabled. Current shared iOS source compiles and passes two scoped fixture tests, with eight native SwiftUI ImageRenderer exports covering the same four states in both themes and active/inactive scenes. Root inspected the exports; active samples differ and inactive samples are identical. Existing app timing bindings remain unchanged and source-reviewed. This follow-up did not rerun the full iOS app suites. iOS Reduce Motion is guarded in source; physical-device smoothness and a system Reduce Motion toggle remain unverified. The first iOS fixture failed on a read-only environment key; a second produced unusable black hostless-window captures. Both attempts are retained separately from the valid ImageRenderer evidence. See `/Users/michael/cleansia-work.noindex/status-bar-restore-20261029/report.md`.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
