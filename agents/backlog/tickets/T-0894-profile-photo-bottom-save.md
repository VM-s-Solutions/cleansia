---
id: T-0894
title: Save Partner profile photo through the bottom action
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

- [x] **AC1** — Picked or removed profile photos remain staged until the same bottom Save used by the form succeeds.
- [x] **AC2** — Remove separate photo-save actions where they exist and preserve working Android behavior.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS staged profile photo uses the same bottom Save/Next as personal data; separate PendingAvatarBar actions are removed. Personal validation/server save precedes photo save, completion waits for both, failed photo preserves draft/page, Back preserves the existing draft, and photo save rereads current user fields. Native one-bottom-Save capture and ordering/refusal tests pass. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

iOS Partner1269 optimized and64 focused iOS16.4 tests pass, including staged photo/personal-save failure paths. Native capture shows one bottom Save and no separate photo Save. Android existing staged bottom action is retained and its1004 Partner tests pass.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
