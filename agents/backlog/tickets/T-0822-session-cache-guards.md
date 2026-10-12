---
id: T-0822
title: Stop a signed-out session's late reads reaching the next account (S11)
size: M
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [ios, android]
security_touching: true
---

## Context

F12 guarded the membership read; the same shape remains elsewhere [IOS-01, IOS-02, IOS-03, AND-09], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). On iOS a read in flight at sign-out can refill a cache after `clear()`, and iOS has no sign-in wipe, so the next person on the phone can see the previous one's data.

## Doing

- iOS `PendingOffersStore` gets F12's session generation inline [IOS-01].
- iOS wipes the session caches at sign-in, before persisting the new tokens, in Core's `AuthApiClient` [IOS-02].
- Every other iOS session cache captures its generation before each await, publishes only if it still matches, and resets its loading latches in `clear()` [IOS-03].
- The Android partner app wipes its session caches at sign-in, as the customer app does [AND-09].
- The Android customer caches get no change: no path to the next account was found [AND-08].

## NOT doing

- No shared session-cache type (CLAUDE.md §4).
- No change to sign-out ordering beyond the wipe.

## Acceptance criteria (done looks like)

- [x] **AC1** — For each iOS cache, a test that holds a read across `clear()` fails before the change and passes after it. *(Amended 2026-10-12: AC1 covers iOS only, matching IOS-01 and IOS-03. The Android partner caches are covered by AC2's sign-in wipe (AND-09). The Android customer caches are unchanged (AND-08).)*
- [x] **AC2** — A sign-in-wipe test on each changed app.
- [x] **AC3** — Security review passes (S11).
- [x] **AC4** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented. iOS lane: IOS-01 to IOS-03. Android lane: AND-08 and AND-09. The review fix pass added `PushTokenRegistrar` and two held-read tests.
  - iOS:
    - Core's `AuthApiClient.resolveEmailGate` awaits the session wipe before it stores tokens. It covers a login and a confirmed e-mail in both apps, Google and Apple sign-in in the customer app, and an unconfirmed login that is given a token.
    - Every cache that reads across an await captures a session generation, and publishes only while it still matches. `clear()` advances the generation and resets the latches. The guard sits inline in each cache, with no shared type.
    - The caches are `PendingOffersStore`, customer loyalty, referrals, orders, disputes, recurring, saved addresses, profile and badge, the partner badge, Core's `RemoteImageCache`, and `PushTokenRegistrar`.
  - Android: the partner app's `login()` and `confirmEmail()` wipe the session caches before persisting the tokens. The customer caches are unchanged (AND-08).
  - RED:
    - iOS: `SignInWipeTests` 5 failed. `PendingOffersStoreTests` also failed on 16.4. All 19 `SessionCacheGuardTests` failed. The two fix-pass held reads failed with their guards removed.
    - `PushTokenRegistrarTests`: a late register rewrote the marker after `clear()`.
    - Android: 3 of 3 failed, including the previous cleaner's employee id reaching the new session.
  - GREEN: all of the above. On Android, the partner `data.auth` tests pass 29/29.
- 2026-10-12 — Committed as `ac5e774`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
- 2026-10-12 — Shipped in [#323](https://github.com/VM-s-Solutions/cleansia/pull/323). Exact-head CI on `cf63083` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `de4e6cb`. No deploy ran. Evidence: `raw/ci/p2-pr323-ci-merge-closure-001.json` (sha256 `504b3841…`) in the off-tree Wave F folder.

## Review

- **Security (S11): AC3 passes on the re-review.**
  - AC1 is iOS-scoped, as amended. On Android, AC2's sign-in wipe covers it: partner `AuthRepository.kt:136-137` and `:198-199`, pinned by `AuthRepositorySignInWipeTest`.
  - The latent republish in partner `OrdersRepository.kt:264-270`, after `clear()`, stays as reported.
- **Reported, not fixed:**
  - The server keeps the previous account's device row on a shared phone: T-0834.
  - `SessionManager.clearSessionScopedCaches` and `registerSessionScopedCache` have no callers.
  - Three places still say three wipe triggers where S11 now says four: `consistency.md:272`, `patterns-mobile.md:322-326` and `.claude/agents/security.md:52`.
  - `features.md:203` names only sign-out and forced sign-out.
- **Gate 7:** a Security entry in CHANGELOG ("signing in starts from a clean slate").
