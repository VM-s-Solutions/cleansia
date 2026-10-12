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

- [x] **AC1** — Source-binding tests prove that each Help entry routes a refused intent or URL to the copy-and-say fallback, RED then GREEN. A device with no mail app is the runtime check. *(Amended 2026-10-12: the tests read the source, so they show the fallback is wired, not that it runs.)*
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
- 2026-10-12 — Committed as `887d10a`, which compiles on its own. Full suites on the final tree, from the review fix pass: Android core 294, partner 834 and customer 1679, all passing. iOS on 18.6 and on the 16.4 floor: Core 775/0, Partner 1109/0, and Customer 1770 with 1 skipped (the VoiceOver-only chip test) and 0 failures. After that run only two test files changed (T-0829's source pin, T-0831's time-zone pin), each run green at class level, plus comment-only edits. Every Android-touching commit compiles on its own.

## Review

- **Correctness:** approve, with AC1 amended. The tests read the source, so they show the fallback is wired, not that it runs. The runtime check is a device with no mail app. It joins the render pass if the pass can do it without a mail action, and otherwise stays QA.
- **Reported, not fixed:**
  - The support address is a private constant in three Android files. iOS keeps it once, in Core.
  - The two platforms' notice wording differs on purpose (`business-rules.md`).
- **Gate 7:** a Fixed entry ("Help works on a phone with no mail app").
