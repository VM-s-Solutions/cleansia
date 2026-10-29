---
id: T-0896
title: Use concise mobile translations
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

- [x] **AC1** — Remove redundant visible tap/click-to-view instructions from the touched flows in all five locales.
- [x] **AC2** — Keep meaningful validation, status, safety information and accessibility action labels; retain locale key parity.

## Out of scope

PRO access, unrelated fixes, public release, new automatic charges, and changing a status layout before Mike selects a concept.

## Work record

- 2026-10-29 — Ground-truthed the current implementation and started the requested change. Evidence and exact command receipts are retained in the mobile-refinements-20261029 work report.

- 2026-10-29 — iOS source/native evidence: iOS scoped copy is concise in cs/en/ru/sk/uk while validation, consent, payment, safety and accessibility meaning are retained. Redundant tap/click-to-view notification suffixes are removed; meaningful actions remain. Existing saved-card consent text/version are unchanged; final English/Ukrainian code-form captures are readable. Full iOS formatter/linter pass and source seal are linked from [the iOS evidence record](../../analysts/mobile-refinements-ios-20261029.md). Root owns final combined-gate/acceptance status.

## Local verification review — 29 October 2026

All five native locale catalogues compile. Web Customer16/Partner21 API-error parity cases pass; Android resource/unit gates and iOS optimized suites pass. Redundant view hints are removed from touched flows while validation, payment-confirmation/rating actions and accessibility labels remain. Native UK confirmation text fits.

Ready for source review; no push, merge, deployment or upload was performed for this batch.
