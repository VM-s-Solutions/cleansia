---
id: T-0899
title: Add example email placeholders to mobile authentication
size: S
owner: Michael Chaban
created: 2026-10-30
updated: 2026-10-30
depends_on: []
blocks: []
stories: [US-mobile-20261030]
adrs: []
layers: [android, ios, docs]
security_touching: false
---

## Context

Mike requested example email placeholders across login, registration, forgotten-password and related authentication pages in both Customer and Partner apps. Grounding found three editable email fields in each app on each platform. Verification and reset follow-ups carry an existing email rather than asking for another one.

## Acceptance criteria

- [x] **AC1** — The three editable authentication email fields in each Customer app display `customer@example.com` as an example when focused and empty; the equivalent Partner fields display `partner@example.com`. The field label remains readable in both themes.
- [x] **AC2** — Examples are hints, never input values or submission defaults. Existing email keyboards, credential AutoFill, validation and submit behaviour remain intact. Entered text replaces the hint.
- [x] **AC3** — All five supported locales resolve the example. Other text fields retain their existing appearance and behaviour when no placeholder is supplied. Current-source builds and scoped static/resource checks pass.

## Out of scope

Authentication or password rules, seeded accounts, read-only profile email, identity re-entry for deletion, backend changes, deployment and uploads.

## Implementation and verification

Evidence: `/Users/michael/cleansia-work.noindex/auth-placeholders-20261030/`. Source and existing catalogue bytes were snapshotted before this follow-up. Both Android debug apps and both iOS app-hosted builds pass. Final iOS focused suites pass 83 tests with zero failures: Core text-field traits/caret 11, Customer confirmation/auth binding 29, Partner authentication/appearance 43. Independent source review passes 70 checks with zero findings; all five locales resolve, existing catalogue entries/metadata are preserved and the Core catalogue is byte-identical. Native credential-provider interaction is not claimed. Temporary generated output cleanup preserves evidence, dependency downloads, signing inputs and prior releases.
