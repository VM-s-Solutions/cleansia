---
id: T-0895
title: Confirm cleaning evidence photo deletion
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

- [x] **AC1** — Before/after photo deletion asks for confirmation; cancelling preserves the photo and confirming deletes only the selected photo.
- [x] **AC2** — Reuse native dialog conventions and translated destructive-action wording.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS cleaning-photo deletion now requires a native destructive confirmation; Cancel performs no delete and confirmation rechecks current read-only/deleting flags. Existing photo view-model coverage and the 16.4 floor pass. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Native Android before/after Cancel and Confirm interactions preserve/remove the selected synthetic photo as expected. iOS alert and photo state coverage pass in Partner1269 optimized tests. Confirmation rechecks editability and current photo/mutation state; dialogs use translated destructive wording.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
