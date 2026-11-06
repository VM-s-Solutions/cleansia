---
id: T-0893
title: Validate duplicate phones during Partner onboarding
size: M
owner: Michael Chaban
created: 2026-10-29
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261029]
adrs: []
layers: [backend, android, ios]
security_touching: true
---

## Context

Mike requested this behavior on 29 October 2026 after testing build18. Current source was inspected before implementation. This batch follows the completed rollout and does not authorize another deployment, reset or upload.

## Acceptance criteria

- [x] **AC1** — Onboarding personal save applies the same existing duplicate-phone refusal as profile save, excluding the authenticated user.
- [x] **AC2** — Render the refusal under the phone field and clear it when edited; do not claim new global normalization or race-proof database uniqueness.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.
- 2026-10-29 — `UpdatePersonalInfo.Validator` now reuses the existing repository phone lookup after required/format validation. It returns `user.existing_phone_number` under `Phone`, excluding the JWT caller's account. The client-supplied EmployeeId remains inert; a spoofed id cannot change self-exclusion or the record written. The existing exact-string/scoped lookup and nonunique phone index remain: no anonymous availability endpoint, global phone normalization or race-proof uniqueness is claimed.

## Backend evidence and remaining checks

Evidence directory: `/Users/michael/cleansia-work.noindex/mobile-refinements-20261029/`. INDEX.md remains the only ticket-status record.

- [x] All **11** `UpdatePersonalInfoValidatorTests` passed within the full **9,142/9,142** unit run. Coverage includes another account's number refused under Phone, the JWT caller's own number accepted despite another EmployeeId, and required/invalid phone input stopping before the availability lookup. Exact results: `test-results/final-backend-unit-full.trx`.
- [x] Existing named `Another_Accounts_Phone_Is_Refused_Under_Phone` test produced **one expected failure** when the duplicate-phone decision was bypassed, then **one pass** after byte-exact source restoration. `backend-final-gates.json` records the mutation, original/restored hash and restored-source full build/unit result.
- [x] Final PostgreSQL1038 and Host518 successors pass after fixture-only corrections; validator/unit source bytes remain unchanged. The original21/7 failures remain retained.
- [x] Current Android Partner1004 and iOS Partner1269 optimized tests pass, including inline Phone refusal/clearing-on-edit cases. No physical-device save is claimed.

- 2026-10-29 — iOS source/native evidence: iOS Personal maps existing user.existing_phone_number under Phone on onboarding’s bottom Next/Save as on profile Save. Local/duplicate-phone failure stops before any staged photo write and keeps the phone error inline. Focused tests and 16.4 floor pass. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

All11 backend phone validator cases plus the named RED/exact-restore/GREEN proof pass. Android Partner1004 and iOS Partner1269 optimized tests pass, including inline duplicate Phone refusal and clearing on edit. Validation uses the existing tenant-scoped exact-phone seam with authenticated self exclusion.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
