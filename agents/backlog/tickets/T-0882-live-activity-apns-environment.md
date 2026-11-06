---
id: T-0882
title: Route Live Activity tokens by their signing environment
size: M
owner: Michael Chaban
created: 2026-10-28
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-functional-20261028]
adrs: [0078]
layers: [backend, db, ios, docs]
security_touching: true
---

## Context

Mike approved the bounded customer/partner mobile scope on 2026-10-28 with “OK”. The finalized [functional repair story](../../analysts/mobile-functional-repairs.md) contains the full LA1–LA3 criteria and challenge/defense/verdict. The design review selected nullable per-token APNs environment metadata; approval of implementation does not authorize database operations or rollout.

## Acceptance criteria

- [x] **AC1** — Registration accepts optional per-token UseSandbox metadata and upserts it without changing ownership, token identity, tenant rules or retirement joins; legacy null values use configured routing.
- [x] **AC2** — APNs sends to the selected fixed Apple endpoint once, preserving inert behavior, priority and existing retry taxonomy; no alternate-host BadDeviceToken retry.
- [x] **AC3** — Regenerate the single Initial migration normally, verify model/schema locally and regenerate the API contracts. Record that future DEV deployment requires separately approved reset/reseed; run no shared database operation.

## Out of scope

PRO, local az, unrelated fixes, pricing/payment/cancellation rules, arbitrary endpoint probing, shared timer tuning without evidence, database reset/refresh/reseed, invoice backfill, deployment, app upload and merge. Preserve all identity, ownership, tenancy and session-wipe protections.

## Implementation notes

Use existing backend and native architecture. Lock generated contract before dependent native environment wiring. Keep protected ignored iOS inputs unread. Focused behavioral tests precede changed logic; schema verification uses local real Postgres. Exact commands and evidence: `/Users/michael/cleansia-work.noindex/mobile-polish-20261028/`. Status lives only in INDEX.md.

## Work record

- 2026-10-28 — Source triage, scope approval and two independent challenge/recheck passes completed. Source implementation and concurrent review started; no shared DEV or PRO operation ran.

- 2026-10-29 — Implementation and independent combined-tree native verification recorded below; prepared for PR review. Merge and rollout remain separately gated.

## Review

Source/conventions and relevant security/optimizer rechecks accepted the bounded implementation. Independent evidence and resolved findings are retained in concurrent-review.md, final-independent-qa.md and the stack-specific security reports under the evidence directory named above.

AC1–AC3: canonical contract audit preserves 95 paths with only optional nullable useSandbox; structural Initial audit proves only nullable UseSandbox and migration ID differ. Real PostgreSQL integration suite passes 1,031 tests. Fixed-host APNs selection, legacy null fallback, priority 10 and terminal BadDeviceToken/410 behavior pass focused and full tests. Five artifact-guard Python tests pass; no current signed archive/IPA was produced. Future DEV rollout requires a separately approved reset/deployment/reseed; no shared schema or mutation operation was run.

### Combined-tree verification

Root independently executed gate-ios-final.sh cleansia on the final combined source: Core 861 passed; Partner 1,261 passed each Debug/optimized; Customer 1,897 total / 1,896 passed / 1 existing VoiceOver-server skip / 0 failed each Debug/optimized. Both optimized legs use -O/whole-module and MallocScribble. Evidence: ios-final-full-results.json and its exact logs/xcresults.

Root executed gate-ios-floor.sh on iPhone 14 / iOS 16.4: customer: 109 tests / 0 failures / 0 skips, partner: 46 tests / 0 failures / 0 skips, core: 29 tests / 0 failures / 0 skips. Evidence: ios-floor-results.json and exact per-leg logs.

Root independently executed the final Release build and full backend suites: 9,114 unit + 1,031 real PostgreSQL integration + 518 host tests; 0 failures/skips. Evidence: backend-final-full-results.json and logs/backend-final-full.log.

Exact commands, cwd, exits and logs are in /Users/michael/cleansia-work.noindex/mobile-polish-20261028/commands-index.md. Repo consistency, documentation build, committed catalog validation and hosted PR checks are also verified at publication. No source approval authorizes merge or rollout; the INDEX remains the sole status record.

### Merge closure — 29 October 2026

Mike approved the merge. [PR #363](https://github.com/VM-s-Solutions/cleansia/pull/363) merged as `1acb9c721847c46da185a12ea93df7083cc053fa`; its tree equals approved head `eba38eec9b2db0960bb34645d8687dcf87efdef8`. All eight premerge hosted workflows succeeded (nine successful checks; optional Xcode readiness skipped), with existing skips, warnings and physical/runtime limits retained in the final report. INDEX.md records completion. This closure remains local and unpushed for the next branch; merge approval did not authorize deployment, app upload, database reset/reseed or invoice backfill. Evidence: `/Users/michael/cleansia-work.noindex/mobile-polish-20261028/merge-363-verification.json` and `report.md`.
