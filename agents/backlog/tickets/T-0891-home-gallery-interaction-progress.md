---
id: T-0891
title: Keep Home gallery progress visible after swiping
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

- [x] **AC1** — Manual paging keeps the active indicator visible and fully filled, preserves the existing pause of automatic paging after interaction and does not fight an active gesture.
- [x] **AC2** — Use smoother paging and restrained small borders; preserve lifecycle pause/resume and reduced motion.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS retains a visible selected indicator after native manual paging, preserves documented pause-for-visit behavior, uses thin card/indicator borders and shared 450 ms page transitions with a 530 ms sentinel delay. Actual Home header/scroll context visibly settles last→first and shows readable CTA/corners. The isolated y16 under-status-bar crop was a fixture artifact; product safe-area workaround experiments were removed. Matching previous/final native captures and measured bounds are retained. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Full native suites and captures verify the persistent fully filled manual indicator and existing permanent manual autoplay pause. iOS actual-Home wrap settles after the450ms transition with530ms configured reanchor delay; fixture-only safe-area workaround was removed. Android isolated gallery captures verify manual pause/indicator behavior, while actual Home supplies system insets/address header.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
