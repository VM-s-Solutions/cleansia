---
id: T-0825
title: When no mail app answers, Help copies the support address and says so
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-12
depends_on: [T-0807]
blocks: []
stories: []
adrs: []
layers: [android, ios]
security_touching: false
---

## Context

F7's Help shortcut (and the registration lock's support row) does nothing on a phone with no mail app [AND-18, IOS-14], per the read-only post-Wave E ground truth of 2026-10-11 (off-tree: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/raw/post-e-ground-truth-results.json`, finding ids in brackets). The customer app already falls back to copying the address.

## Doing

- Both partner apps copy the support address and show a snackbar when no app accepts the `mailto:` (variant A2).
- Android's `openOrCopy` moves to `:core` for its third caller.
- A partner `help_email_unavailable` string in all five locales.

## NOT doing

- No in-app mail composer.

## Acceptance criteria (done looks like)

- [x] **AC1** — Source-binding tests show each Help entry is wired to the copy-and-say fallback, RED then GREEN. On Android they pin the refused-intent branch, `if (!context.openOrCopy(…))`. On iOS they pin its parts (the `openURL` completion, the pasteboard write, the notice and the `canSendMail()` check), but not the `!accepted` condition. *(Amended 2026-10-12: the tests read the source, so they show the fallback is wired, not that it runs.)*
- [x] **AC2** — Five locales are present (parity guards).
- [x] **AC3** — The full suites pass.

## Work record

- 2026-10-11 — Filed from the post-Wave E ground truth and Mike's rulings.
- 2026-10-11 — Implemented.
  - Android:
    - `openOrCopy` moves to `:core` for its third caller.
    - The partner dashboard's Help and the registration lock's support row run `if (!context.openOrCopy(...)) …Unavailable()`.
    - Both view models show `help_email_unavailable`, which is in all five locales.
  - iOS:
    - Both partner sites copy first when `canSendMail()` is false, because Mail still takes the link on its setup screen.
    - Otherwise they use `openURL(url) { accepted in … }`, which copies and says so when no app takes the link.
    - The new key is in all five locales.
  - RED: the binding tests and the new-API tests failed (Android: compile-RED, then assertion-RED). The iOS binding tests failed on all four assertions.
  - GREEN: Android core `OpenOrCopyTest` 2/2, partner parity, dashboard and registration-lock suites, and customer `HelpSupportViewModelTest`. iOS partner suites, and `StringCatalogCompletenessTests`.
- 2026-10-12 — Committed as `278582f`. Its Android half compiles on its own (main and unit-test sources, per commit); its iOS half was built only as part of the final tree. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.
- 2026-10-12 — Shipped in [#323](https://github.com/VM-s-Solutions/cleansia/pull/323). Exact-head CI on `cf63083` passed all 9 check runs with no re-run: Backend, Frontend and e2e-smoke, Android, iOS, Docs, iOS Symbols, booking-policy parity and Secret scan. Merged normally with the SHA guard as `de4e6cb`. No deploy ran. Evidence: `raw/ci/p2-pr323-ci-merge-closure-001.json` (sha256 `504b3841…`) in the off-tree Wave F folder.

## Review

- **Correctness:** approve, with AC1 amended. The tests read the source, so they show the fallback is wired, not that it runs.
- **Open, for QA:** the runtime check on a device with no mail app. The render pass excludes Help and mail actions, so it did not run it.
- **Reported, not fixed:**
  - The support address is a private constant in three Android files. iOS keeps it once, in Core.
  - The two platforms' notice wording differs on purpose (`business-rules.md`).
- **Gate 7:** a Fixed entry ("Help works on a phone with no mail app").
