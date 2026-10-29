---
id: T-0873
title: Separate cleaning photo phases without losing evidence
size: M
owner: Michael Chaban
created: 2026-10-28
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261028]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Mike approved the bounded customer and partner Android/iOS scope on 2026-10-28 with “OK”. The finalized [mobile presentation story](../../analysts/mobile-presentation.md) carries the challenge, defense and verdict, source grounding and full criteria C6. Source triage confirmed the reported presentation paths before implementation.

## Acceptance criteria

- [x] **AC1** — Given mixed photo phases, show exact Before, After, Entrance and Other collections without inferring phase.
- [x] **AC2** — Given a selected preview, open the correct image and collection; retain all photos and native back navigation.

## Out of scope

PRO access, owner-checkout writes, unrelated fixes, pricing/payment/cancellation eligibility changes, database operations, deployment, app upload and merge. Functional saved-card, invoice and Live Activity repairs have separate tickets.

## Implementation notes

Match existing shared components, native state/network patterns and all five supported locales. Logic uses focused behavioral tests; layout uses native visual evidence. Keep Android and iOS behavior aligned. Exact commands and local evidence: `/Users/michael/cleansia-work.noindex/mobile-polish-20261028/`. Status lives only in INDEX.md.

## Work record

- 2026-10-28 — Source-grounded triage, explicit scope approval and independent challenge passes completed; native implementation and concurrent review started.

- 2026-10-29 — Implementation and independent combined-tree native verification recorded below; prepared for PR review. Merge and rollout remain separately gated.

## Review

Source/conventions and relevant security/optimizer rechecks accepted the bounded implementation. Independent evidence and resolved findings are retained in concurrent-review.md, final-independent-qa.md and the stack-specific security reports under the evidence directory named above.

AC1–AC2: exact phase grouping and stable selected-photo identity are covered by OrderPhotosGridTests / Android OrderReadingTest and final native gallery fixtures. iOS retains representable missing/null phases as Other; its generated closed enum still rejects invalid wire integers such as 99 before UI classification. No custom decoding contract or network-photo success is claimed.

### Combined-tree verification

Root independently executed compileDebugKotlin/testDebugUnitTest for core, customer and partner with forced actual tests: 343 + 1,787 + 1,004 = 3,134 passed; 0 failures/errors/skips. Evidence: android-root-final-results.json and logs/android-root-final.log.

Root independently executed gate-ios-final.sh cleansia on the final combined source: Core 861 passed; Partner 1,261 passed each Debug/optimized; Customer 1,897 total / 1,896 passed / 1 existing VoiceOver-server skip / 0 failed each Debug/optimized. Both optimized legs use -O/whole-module and MallocScribble. Evidence: ios-final-full-results.json and its exact logs/xcresults.

Root executed gate-ios-floor.sh on iPhone 14 / iOS 16.4: customer: 109 tests / 0 failures / 0 skips, partner: 46 tests / 0 failures / 0 skips, core: 29 tests / 0 failures / 0 skips. Evidence: ios-floor-results.json and exact per-leg logs.

Layout acceptance uses actual native screenshots and optical measurements where appropriate; a capture-test exit 0 is not a behavioral RED. Existing behavioral tests and meaningful mutations are identified separately in the evidence.

Exact commands, cwd, exits and logs are in /Users/michael/cleansia-work.noindex/mobile-polish-20261028/commands-index.md. Repo consistency, documentation build, committed catalog validation and hosted PR checks are also verified at publication. No source approval authorizes merge or rollout; the INDEX remains the sole status record.

### Merge closure — 29 October 2026

Mike approved the merge. [PR #363](https://github.com/VM-s-Solutions/cleansia/pull/363) merged as `1acb9c721847c46da185a12ea93df7083cc053fa`; its tree equals approved head `eba38eec9b2db0960bb34645d8687dcf87efdef8`. All eight premerge hosted workflows succeeded (nine successful checks; optional Xcode readiness skipped), with existing skips, warnings and physical/runtime limits retained in the final report. INDEX.md records completion. This closure remains local and unpushed for the next branch; merge approval did not authorize deployment, app upload, database reset/reseed or invoice backfill. Evidence: `/Users/michael/cleansia-work.noindex/mobile-polish-20261028/merge-363-verification.json` and `report.md`.
