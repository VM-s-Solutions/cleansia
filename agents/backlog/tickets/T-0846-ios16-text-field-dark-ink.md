---
id: T-0846
title: Find out whether text fields draw light-mode ink and caret in dark mode on iOS 16, and fix them if they do
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-14
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
---

## Context

This comes from open question 2 of the catalog item in the 2026-10-12 native design ruling. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`. **This is unverified: a render check decides.** Lines are as of `207d2f319`.

- **The cause, from T-0829.** On the iOS 16 floor, `UIColor(Color)` keeps only a dynamic colour's light value. That holds under `resolvedColor(with:)`, under `performAsCurrent` and in dark appearance alike. From iOS 17 on, it keeps both values.
- **Where it may bite.**
  - `CleansiaTextField.swift:225-226` and `CleansiaPhoneInput.swift:159-160`, both CleansiaCore components, hand UIKit `UIColor(textColor)` and `UIColor(tintColor)`.
  - Those colours come from `CleansiaColors.onSurface` and `CleansiaColors.primary` (`CleansiaColors.swift:38`, `:4`; passed at `CleansiaTextField.swift:165-166`), and both are dynamic.
  - So in dark mode on iOS 16, every text field's typed text and caret may be drawn in light-mode colours: slate-900 ink on a slate-800 field.
- **Why nobody has seen it.**
  - Nobody has looked yet.
  - CI's newest runtime cannot show it.
  - T-0839's dark-read rule leaves out what UIKit draws from a `UIColor(Color)`. That is a separate question, and this ticket is that question.

## Doing (proposed, for Mike)

1. **The check.** On the iOS 16.4 simulator in dark mode, open a screen with a `CleansiaTextField` and a `CleansiaPhoneInput`, with text typed and the caret showing. Do the same on 18.6. Screenshots or sampled colours go here.
2. **If confirmed:**
   - hand UIKit a colour that keeps both appearances on iOS 16. That is either a dynamic `UIColor` built from the same light and dark values, or colours set from the current trait collection whenever the view updates;
   - add a test on the 16.4 floor run that fails today;
   - report any other `UIColor(Color)` hand-off the fix finds.
3. **If not confirmed:** close the ticket with the evidence.

## NOT doing

- Colour reads on the test side, which T-0839's rule covers.

## Acceptance criteria (done looks like)

- [x] **AC1** — The render check is recorded for 16.4 and 18.6, in light and dark mode.
- [x] **AC2** — If the defect is confirmed: typed text and caret use the dark-mode colours on 16.4, a floor-run test goes RED then GREEN, and the iOS suites pass on 18.6 and 16.4. If it is not confirmed, the ticket closes on AC1's evidence.

## Work record

- 2026-10-12 — Filed from the native design ruling's open question on the iOS 16 floor. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

### Native follow-up — 2026-10-14

- Authority, dated 2026-10-13: Mike, "Analyze and Fix those" and "Go with defaults (recommended)"; `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/next-wave/decisions-2026-10-13.md`. The native plan requests the 16.4/18.6 render probe; fixing production color code is conditional on a reproduced defect.
- Evidence root: `/Users/michael/.codex/scratchpads/cleansia-next-wave-2026-10-13/native/`. **The suspected production ink/caret defect was not reproduced.** Final mounted `CleansiaTextField` and `CleansiaPhoneInput` probes retain the same focused UIKit field identity through light → dark → light on **both 16.4 and 18.6**, with actual nonempty text, trait/readback, rendered text/caret RGBA and screenshots. Twelve mounted samples and twelve PNGs are retained; all PNGs were visually inspected. The phone probe explicitly configures nondefault **`GroupedPhoneDisplayFormatter`**. It proves that stated formatter configuration, not a production-default formatting fix.
- The original 16.4 default-phone attempt already had correct text/caret RGBA; its assertion incorrectly expected grouped text while the actual default returned raw `+420728089247`. That original failed attempt is retained and excluded as a color-defect RED. The final harness sets the nondefault formatter explicitly, retaining identity/focus/appearance assertions. No production text-field/phone-field color or default-format code changed.
- Mounted receipts: `ios/developer-final-partner-focused164-001-mounted.json`, `ios/developer-final-partner-focused186-001-mounted.json`; visual qualification: `ios/developer-final-focused164-visual-001.json`, `ios/developer-final-focused186-visual-001.json`. `ios/developer-final-packet-001.json` (SHA256 `470115f5e483f4af7f835d3b66221bbae638315771d4296f791eab51b448c031`) binds all 27 iOS overlays to `ios/developer-final-source-001-source-manifest.json` (SHA256 `1cc89799ea912d3d650ce8fc33c2a2a96aca8b308d813d261c6610ece2ba09cb`). On **each** of 16.4 and 18.6: Core 776 passed; Partner 1133 passed; Customer 1780 reported, 1779 passed and one unchanged VoiceOver guard skipped; no failures. Focused Partner 133/133 passed on each floor. All nine final attempts are retained, including the second Customer 16.4 run: the accessibility preference was already 1, reapplying 1 still left `UIAccessibility.isVoiceOverRunning` false, and readback restored the same 1. There is no spoken VoiceOver proof. The actual Dashboard source split is 24 main-file methods plus 10 barrier-file methods, all 34 passing on both floors; the older 25+9 preparation split is stale. The final RED/GREEN matrix has 23 entries with the same method ids passing on both floors. Its 19 original failed methods include an invalid assumed default-phone-grouping assertion; the epoch attempt's three failures include the still-unlabelled Menu and a wrong radius fixture expectation, both explicitly qualified. Harness errors are not relabelled as behavior RED.
- AC1 is met on two-floor light/dark evidence; AC2's not-confirmed branch is met without a production fix. This is mounted-field/render evidence and actual SDK execution, not an assertion that an on-device keyboard flow or every application screen was exercised. CHANGELOG earns no entry for this refuted defect/test-only probe. Final native iOS approval is sealed below; publication checks remain pending; INDEX is in review until the native phase merges.

- Final combined-source cross-stack gates PASS is recorded centrally in T-0839's dated native Work follow-up (`cross-stack-001/final.json` and `count-receipt-001.json`); final docs/parity/symbol and publication obligations remain separate.

## Review

### Native follow-up assessment — 2026-10-14

- Actual mounted color/caret/field-identity evidence and two-floor source-bound test results are recorded; **suspected defect not confirmed**, production color/default-format files unchanged. The prior scoped iOS security approval is not a visual approval. The final native iOS assessment is sealed below, including its independent mounted-field checks.
- Final independent `ios/final-review-001/verdict.json` (SHA256 `0b26a7a469e2478c76ffdcd4665f7d6ab593225ec63e12abbbe4713d4b0f2769`): **APPROVED_SCOPED_NATIVE_IOS27**. Allocated native source/SDK/runtime scope and the own 18/18 copied-product SDK run (no own compile) are qualified in T-0839's dated Work follow-up. This does not close records, hosted CI, merge/deploy or whole S11.
