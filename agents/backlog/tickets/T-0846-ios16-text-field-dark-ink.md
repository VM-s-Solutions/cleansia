---
id: T-0846
title: Find out whether text fields draw light-mode ink and caret in dark mode on iOS 16, and fix them if they do
size: S
owner: pm
created: 2026-10-12
updated: 2026-10-12
depends_on: []
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
---

## Context

This comes from open question 2 of the catalog item in the 2026-10-12 native design ruling. The ruling is off-tree, in the Wave F folder: `/Users/michael/.codex/scratchpads/cleansia-wave-f-2026-10-11/p5/phase0/architect_native-lead.json`. **This is unverified: a render check decides.** Lines are as of `76e1638a3`.

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

- [ ] **AC1** — The render check is recorded for 16.4 and 18.6, in light and dark mode.
- [ ] **AC2** — If the defect is confirmed: typed text and caret use the dark-mode colours on 16.4, a floor-run test goes RED then GREEN, and the iOS suites pass on 18.6 and 16.4. If it is not confirmed, the ticket closes on AC1's evidence.

## Work record

- 2026-10-12 — Filed from the native design ruling's open question on the iOS 16 floor. Reported, not absorbed (CLAUDE.md §2): Mike decides whether it becomes work.

## Review

Pending.
