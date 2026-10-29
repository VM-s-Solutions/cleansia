---
id: T-0878
title: Use public order references and compact dispute forms
size: S
owner: Michael Chaban
created: 2026-10-28
updated: 2026-10-29
depends_on: []
blocks: []
stories: [US-mobile-20261028]
adrs: []
layers: [android, ios, docs]
security_touching: true
---

## Context

Mike approved the bounded customer and partner Android/iOS scope on 2026-10-28 with “OK”. The finalized [mobile presentation story](../../analysts/mobile-presentation.md) carries the challenge, defense and verdict, source grounding and full criteria D1–D2. Source triage confirmed the reported presentation paths before implementation.

## Acceptance criteria

- [x] **AC1** — Given dispute creation or an equivalent customer-facing order context, show the public order number instead of a technical ID; use neutral context until known.
- [x] **AC2** — Given compact screen, keyboard or large text, retain all form inputs, feedback and submit reachability while reducing unused space.

## Out of scope

PRO access, owner-checkout writes, unrelated fixes, pricing/payment/cancellation eligibility changes, database operations, deployment, app upload and merge. Functional saved-card, invoice and Live Activity repairs have separate tickets.

## Implementation notes

Match existing shared components, native state/network patterns and all five supported locales. Logic uses focused behavioral tests; layout uses native visual evidence. Keep Android and iOS behavior aligned. Exact commands and local evidence: `/Users/michael/cleansia-work.noindex/mobile-polish-20261028/`. Status lives only in INDEX.md.

## Work record

- 2026-10-28 — Source-grounded triage, explicit scope approval and independent challenge passes completed; native implementation and concurrent review started.

- 2026-10-29 — Implementation and independent combined-tree native verification recorded below; prepared for PR review. Merge and rollout remain separately gated.

## Review

Source/conventions and relevant security/optimizer rechecks accepted the bounded implementation. Independent evidence and resolved findings are retained in concurrent-review.md, final-independent-qa.md and the stack-specific security reports under the evidence directory named above.

AC1: CreateDisputeViewModelTests on both platforms cover the public order number and neutral unavailable context; internal IDs remain route/submission inputs only. AC2: native compact and larger-text form fixtures retain inputs and submit access. No shared dispute was submitted; physical keyboard behavior is not inferred from screenshots.

### Combined-tree verification

Root independently executed compileDebugKotlin/testDebugUnitTest for core, customer and partner with forced actual tests: 343 + 1,787 + 1,004 = 3,134 passed; 0 failures/errors/skips. Evidence: android-root-final-results.json and logs/android-root-final.log.

Root independently executed gate-ios-final.sh cleansia on the final combined source: Core 861 passed; Partner 1,261 passed each Debug/optimized; Customer 1,897 total / 1,896 passed / 1 existing VoiceOver-server skip / 0 failed each Debug/optimized. Both optimized legs use -O/whole-module and MallocScribble. Evidence: ios-final-full-results.json and its exact logs/xcresults.

Root executed gate-ios-floor.sh on iPhone 14 / iOS 16.4: customer: 109 tests / 0 failures / 0 skips, partner: 46 tests / 0 failures / 0 skips, core: 29 tests / 0 failures / 0 skips. Evidence: ios-floor-results.json and exact per-leg logs.

Layout acceptance uses actual native screenshots and optical measurements where appropriate; a capture-test exit 0 is not a behavioral RED. Existing behavioral tests and meaningful mutations are identified separately in the evidence.

Exact commands, cwd, exits and logs are in /Users/michael/cleansia-work.noindex/mobile-polish-20261028/commands-index.md. Repo consistency, documentation build, committed catalog validation and hosted PR checks are also verified at publication. No source approval authorizes merge or rollout; the INDEX remains the sole status record.
