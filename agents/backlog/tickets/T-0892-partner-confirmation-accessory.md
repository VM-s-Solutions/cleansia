---
id: T-0892
title: Remove Partner confirmation code accessory icon
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

- [x] **AC1** — The confirmation code form has no separate copy/paste accessory icon; native text selection and paste remain available.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS ConfirmEmailView disables the separate CodeInput Paste accessory while retaining native edit-menu Paste and one-time-code AutoFill. Final English/Ukrainian native 16.4 captures pass and were inspected; shortened Ukrainian heading fits. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Full native suites compile both callsites with the accessory disabled. iOS EN/UK code-form captures fit; Android native component evidence has no accessory and its positive-control capture retains Paste. Native OS paste/code suggestions remain available.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
