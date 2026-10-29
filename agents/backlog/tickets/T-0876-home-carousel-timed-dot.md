---
id: T-0876
title: Show home carousel rotation time in the active dot
size: S
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

Mike approved the bounded customer and partner Android/iOS scope on 2026-10-28 with “OK”. The finalized [mobile presentation story](../../analysts/mobile-presentation.md) carries the challenge, defense and verdict, source grounding and full criteria H1. Source triage confirmed the reported presentation paths before implementation.

## Acceptance criteria

- [x] **AC1** — Given autoplay, active-dot fill and rotation share the existing six-second clock.
- [x] **AC2** — Given interaction, accessibility motion settings, offscreen or inactive lifecycle, pause correctly and restart honestly on return.

## Out of scope

PRO access, owner-checkout writes, unrelated fixes, pricing/payment/cancellation eligibility changes, database operations, deployment, app upload and merge. Functional saved-card, invoice and Live Activity repairs have separate tickets.

## Implementation notes

Match existing shared components, native state/network patterns and all five supported locales. Logic uses focused behavioral tests; layout uses native visual evidence. Keep Android and iOS behavior aligned. Exact commands and local evidence: `/Users/michael/cleansia-work.noindex/mobile-polish-20261028/`. Status lives only in INDEX.md.

## Work record

- 2026-10-28 — Source-grounded triage, explicit scope approval and independent challenge passes completed; native implementation and concurrent review started.

- 2026-10-29 — Implementation and independent combined-tree native verification recorded below; prepared for PR review. Merge and rollout remain separately gated.

## Review

Source/conventions and relevant security/optimizer rechecks accepted the bounded implementation. Independent evidence and resolved findings are retained in concurrent-review.md, final-independent-qa.md and the stack-specific security reports under the evidence directory named above.

AC1: Android actual 0/3/6-second captures and iOS actual natural-clock start / 2.129-second partial / 6.884-second next-slide captures prove visible progress and advance. The source uses one six-second clock. AC2: Android actual background/return and drag-stop observations pass; iOS hosted scenePhase holds 6.651 seconds inactive then resets/fills on resume. iOS actual OS background, offscreen return, VoiceOver and touch are source-policy checks here, not claimed device events. Direct frame delivery preserves the existing visibility predicate; temporary diagnostic instrumentation was restored byte-exact.

### Combined-tree verification

Root independently executed compileDebugKotlin/testDebugUnitTest for core, customer and partner with forced actual tests: 343 + 1,787 + 1,004 = 3,134 passed; 0 failures/errors/skips. Evidence: android-root-final-results.json and logs/android-root-final.log.

Root independently executed gate-ios-final.sh cleansia on the final combined source: Core 861 passed; Partner 1,261 passed each Debug/optimized; Customer 1,897 total / 1,896 passed / 1 existing VoiceOver-server skip / 0 failed each Debug/optimized. Both optimized legs use -O/whole-module and MallocScribble. Evidence: ios-final-full-results.json and its exact logs/xcresults.

Root executed gate-ios-floor.sh on iPhone 14 / iOS 16.4: customer: 109 tests / 0 failures / 0 skips, partner: 46 tests / 0 failures / 0 skips, core: 29 tests / 0 failures / 0 skips. Evidence: ios-floor-results.json and exact per-leg logs.

Layout acceptance uses actual native screenshots and optical measurements where appropriate; a capture-test exit 0 is not a behavioral RED. Existing behavioral tests and meaningful mutations are identified separately in the evidence.

Exact commands, cwd, exits and logs are in /Users/michael/cleansia-work.noindex/mobile-polish-20261028/commands-index.md. Repo consistency, documentation build, committed catalog validation and hosted PR checks are also verified at publication. No source approval authorizes merge or rollout; the INDEX remains the sole status record.
