---
id: T-0890
title: Refine order creation and completion success marks
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

- [x] **AC1** — Inspect current marks and remove undesirable motion/effects while retaining a clear accessible success state.
- [x] **AC2** — Do not invent an animated defect where the current platform mark is static; preserve successful booking/completion actions.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS removed the actual .cleansiaBounceOnAppear() from BookingSuccessView’s success checkmark. Completed status marks remain still; no replacement decorative bounce was introduced. Native full Debug/floor compilation and existing completion/layout coverage pass. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Optimized Customer suite passes after removal of the booking-success bounce. Existing quiet completion mark and Android static check are retained; no absent animated Android defect is claimed.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
