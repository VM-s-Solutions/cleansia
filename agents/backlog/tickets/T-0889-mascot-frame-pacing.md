---
id: T-0889
title: Smooth cleaning mascot playback
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [android, ios]
security_touching: false
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [ ] **AC1** — Remove measured decode/recomposition/presentation stalls without increasing unnecessary decoding or persistent CPU work.
- [x] **AC2** — Preserve original timing, reduced-motion behavior, foreground/background lifecycle and all required frames.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS uses the upcoming display deadline and skips slightly overdue intermediate frames while retaining partial-buffer holds, long-stall rebase, lifecycle and original segmented assets. A ticker-factory-injected probe called production MascotAnimationView.step(targetTimestamp) on 387 actual CADisplayLink callbacks: 387/387 matched the upcoming frame versus 230/387 for legacy on the same callbacks. This is simulator frame-assignment evidence, not full unmodified ticker or physical display FPS; production DisplayLinkProxy wiring is separately source/hash verified. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Production iOS step(targetTimestamp) selected the intended upcoming frame for387/387 actual simulator callbacks in an injected-ticker probe; legacy policy selected230/387 on the same callbacks. Android retains125 original41/42ms frames and reuses its request. Full native suites pass. AC1 remains open for physical-device stall/smoothness acceptance; this probe is not display FPS.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
