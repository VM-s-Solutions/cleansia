---
id: T-0885
title: Keep registration AutoFill isolated and readable
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [ios, android]
security_touching: true
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — Registration uses account-username and new-password semantics without treating another field’s programmatic text as a user edit.
- [ ] **AC2** — Preserve password-manager support and dark-mode legibility; record physical strong-password-overlay verification limits honestly.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS uses username + email keyboard and two newPassword fields, with correction/smart punctuation disabled for passwords and native presentation colours preserved through unrelated binding redraw. Synthetic native field values/traits and simulated black/yellow preservation pass; real iCloud Keychain/QuickType yellow-overlay selection was not reproduced. Mounted focused light→dark→light ink/caret tests pass on 16.4 and 26.3. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

Core864, Partner1269 and Customer1916 optimized tests pass (one existing Customer VoiceOver test skipped); iOS16.4 focused tests pass. Credential traits/bindings and mounted light/dark ink checks pass. AC2 stays open for physical iCloud Keychain/strong-password highlight acceptance; simulator evidence does not reproduce that state.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
