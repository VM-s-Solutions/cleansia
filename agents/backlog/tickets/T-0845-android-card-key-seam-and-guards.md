---
id: T-0845
title: Android customer app — one injectable seam for the Stripe key, view-model guards on card-only actions, and a pin on the sign-in wipe
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: [T-0835]
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: true
---

## Context

This comes from the 2026-10-12 native design ruling: open questions 1 and 2 of its item on T-0835, and open question 1 of its catalog item. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`.

Keyless builds are debug-only: a release build refuses a blank key (`customer-app/build.gradle.kts:252-262`). Lines are as of `207d2f319`.

- **The key is read inline in six places, with no seam.**
  - `BuildConfig.STRIPE_PUBLISHABLE_KEY` is read at `CleansiaApp.kt:58`, `ConfirmStep.kt:115`, `OrderDetailScreen.kt:582`, `SubscribePlusScreen.kt:100`, `CreateRecurringViewModel.kt:731` and `CreateRecurringScreen.kt:1285`.
  - T-0835's ruling adds a seventh read: `cardAvailable` in `CreateRecurringViewModel`.
  - `BuildConfig` is fixed at compile time, and android-ci builds without a key. So CI never runs the keyed branch of any of these reads.
- **The card-only view models refuse nothing.** In a keyless build, only hidden buttons keep them unreachable:
  - `OrderDetailViewModel.confirmRecurring` (`:521-573`). Here the server spends the customer's credit before the payment sheet opens (`:556-560`).
  - `MembershipViewModel.startSubscribe` (`:150-174`).

  iOS refuses all three in its view models: `OrderDetailViewModel.swift:444`, `MembershipViewModel.swift:95`, and Add card after T-0835.
- **The customer sign-in wipe has no test.**
  - `core/auth/AuthRepository.kt:217-222` clears every session cache before it saves the new tokens.
  - `AuthRepositoryTest` pins only the logout wipe (`:551-622`).
  - The partner app pins its sign-in wipe (`AuthRepositorySignInWipeTest`), and T-0839's E9 line names the customer wipe as outside its roster.

**If not built:**
- CI keeps testing only the keyless branch;
- the unguarded calls stay reachable only through a hidden button, in a debug build;
- a regression of the customer sign-in wipe (S11) fails no test.

## Doing (proposed, for Mike)

1. **One injectable "card payment available" value.** It is read once from `BuildConfig`, provided through Hilt, and used by all seven reads, so a test can set it either way.
2. **Guards.** `confirmRecurring` and `startSubscribe` return before any server call when card is unavailable, as iOS does.
3. **A customer sign-in wipe test.** It checks that the caches are cleared before the tokens are saved, in that order. E9's roster gains it.

Each test goes RED then GREEN, and the full Android suites pass.

## NOT doing

- iOS, release builds, server behaviour, and T-0835's own changes.

## Acceptance criteria (done looks like)

- [ ] **AC1** — No production file reads `BuildConfig.STRIPE_PUBLISHABLE_KEY` outside the seam, and a test runs a keyed branch on CI.
- [ ] **AC2** — With card unavailable, `confirmRecurring` and `startSubscribe` make no server call. The tests go RED then GREEN.
- [ ] **AC3** — The customer sign-in wipe test fails when the wipe is removed, or moved after the token save.
- [ ] **AC4** — The full Android suites pass.

## Work record

- 2026-10-12 — Filed from the native design ruling's open questions on T-0835 and on the wipe-trigger wording. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
