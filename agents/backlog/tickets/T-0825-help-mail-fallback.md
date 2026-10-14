---
id: T-0825
title: When no mail app answers, Help copies the support address and says so
size: S
owner: pm
created: 2026-10-11
updated: 2026-10-14
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
- 2026-10-13 — The no-mail check, run in T-0839's device pass (`render-p6/raw/receipt-003.json`, off-tree in the Wave F folder). iOS: PASS for Help. With no Mail app on the simulator, one Help tap copied `support@cleansia.cz`, the "Address copied" notice showed, and no composer opened. The registration lock's support row was not reached on either app. Android: NOT OBSERVED; Help was found but not tapped, because Gmail is the owned emulator's only `mailto` handler and disabling it waits for Mike's word or an image with no mail handler (T-0839 AC5).

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "you can do some kind of local development switch" and "Go with defaults (recommended)"; `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. This completes the device follow-up carried by T-0839 AC5; #323 and the dated #328-era partial observation remain history.
- Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. Android ids use `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/records-android-preparation-002/evidence-hash-manifest.json` (SHA256 `6b433bd7c839ac2a2e12a777a6070914ac23f785c746ec8a3ee9e467e12eb21f`), whose `evidence_ids` maps each Android id below to its exact path and SHA256. `A-DEVICE` is the corrected terminal `android-device-001/device-acceptance-002.json`, SHA256 `5a3e95308df098e5c2b758c92413b5e0402c30a30f915f00d424745198236bb9`; it supersedes 001's notice attribution. Producer and installed baseline/final APK bytes are qualified. The final APK SHA256 `39c59d720be888d88afde5dccab9770645a6293951093b4b952165343b64d6d3` is the actual combined product.
- Android Help and rejected-registration Approval **PASS** through the approved debug extra `cleansia.debug.force_no_mail_handler=true`: exact `support@cleansia.cz` copy and existing notice. Baseline with the extra still opened Gmail; final normal cold launch without the extra opened Gmail and produced no copy notice. Gmail stayed enabled, no mail was sent, and no call was placed. Help's notice is proven by exact native UI XML **only**; its early PNG shows Dashboard before notice pixels. Approval's notice and copied-address paste into the native blank login field are retained, with no support-address sign-in submitted (`A-MAIL`, `A-DEVICE-REFUTATION`).
- `runtime-preparation/ios-runtime-acceptance-final-001.json` (SHA256 `40501b1b8af9dc2ef62bba9c33b57d8a358b8f99e801e01abe4befcb557c7628`) seals the final two-floor runtime receipts and their product/source bindings. It records PASS within the stated device scope, not a full ticket or whole-wave approval. On iOS **16.4 and 18.6**, actual Dashboard Help and the rejected Approval support action each copied the exact support address and showed "Address copied, in case your mail app isn't set up." All four notices have AX and clipboard evidence; all four PNGs were visually inspected and show actual notice pixels. Receipts: `runtime-preparation/ios-final-help-001-164.json`, `ios-final-approval-004-164.json`, `ios-final-help-009-186.json`, `ios-final-approval-012-186.json`. No composer/provider Send was exercised. The rejected route was reached by actual native login against the SQL-rejected synthetic account.
- Cross-recorded on T-0839. Full source-bound suite receipts are recorded there. These observations complete the prior device follow-up; the new Android switch and current native publication remain unmerged.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.

## Review

- **Correctness:** approve, with AC1 amended. The tests read the source, so they show the fallback is wired, not that it runs.
- **Open, for QA:** the runtime check on a device with no mail app. The render pass excludes Help and mail actions, so it did not run it.
- **Reported, not fixed:**
  - The support address is a private constant in three Android files. iOS keeps it once, in Core.
  - The two platforms' notice wording differs on purpose (`business-rules.md`).
- **Gate 7:** a Fixed entry ("Help works on a phone with no mail app").

### Native follow-up assessment — 2026-10-14

- Dated native device follow-up: **PASS within the Help/rejected Approval copy-and-notice scope**, with Android Help XML-only notice attribution and all four iOS notice PNGs qualified. Historical source-binding approval remains unchanged. This observation is not provider delivery, a new auth-flow approval or whole S11 closure.
- Final independent `ios/final-review-001/verdict.json` (SHA256 `0b26a7a469e2478c76ffdcd4665f7d6ab593225ec63e12abbbe4713d4b0f2769`): **APPROVED_SCOPED_NATIVE_IOS27**. Allocated native source/SDK/runtime scope and the own 18/18 copied-product SDK run (no own compile) are qualified in T-0839's dated Work follow-up. This does not close records, hosted CI, merge/deploy or whole S11.
