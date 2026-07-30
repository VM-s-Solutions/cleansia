---
id: T-0442
title: Android customer profile hero — match the iOS layout (edit chip vertically centred in the same row)
status: ready
size: S
owner: android
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: [T-0448]
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Owner report, verbatim: *"Now the name and profile photo is on top and 'edit' button is in the bottom
while I want both of them being aligned in the middle of the screen vertically."*

A previous diagnosis enumerated ~11 header differences and concluded the direction was mixed. **The
owner has RESOLVED that: iOS is the reference, Android matches it.** That ruling is the decision;
what remains is a port with no open design question.

**The structural difference, read from both files (PM, 2026-07-30):**

| | iOS — `CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift:261-311` (`HeroGradient`) | Android — `customer-app/.../features/profile/ProfileTab.kt:244-336` (`ProfileHero`) |
|---|---|---|
| Root | `HStack(alignment: .top, spacing: 14)` — **one row** | `Column { Row { … }; Spacer(16.dp); Row(End) { … } }` — **two stacked rows** |
| Edit control | `EditProfileChip` is the **third item of that row**, `.frame(maxHeight: .infinity, alignment: .center)` — the comment at `:299-301` says explicitly it centres the chip vertically while avatar/name stay top-anchored | a right-aligned pill on its **own row below**, after `Spacer(Modifier.height(16.dp))` |

That is precisely the owner's complaint, and iOS already has the wanted behaviour with a comment
explaining it. Everything else in the two heroes is close but not identical; the deltas I confirmed
by reading both files:

| # | iOS | Android | 
|---|---|---|
| 1 | horizontal padding `Spacing.ml` | `20.dp` start/end |
| 2 | top padding `48 + topInset` | `16.dp` |
| 3 | bottom padding `40` | `36.dp` |
| 4 | name `CleansiaTypography.headlineSmall` | `titleLarge` + Poppins + Bold |
| 5 | email `bodyMedium` | `bodySmall` |
| 6 | avatar initials `CleansiaColors.primary` | `Sky600` |
| 7 | email rendered only when non-empty (`if let email … !email.isEmpty`) | always rendered |
| 8 | `TierBadge` has a `crown.fill` glyph + `labelSmall` | `TierBadge` — confirm glyph/typography parity in the port |
| 9 | avatar 72pt, white fill, 3pt white-35% stroke | 72.dp, white fill, 3.dp white-35% border — **already matches** |
| 10 | gradient `BrandGradient.blue` top→bottom | `BrandGradients.blue()` verticalGradient — **already matches** |
| 11 | name/email/badge column spacing 2 + `Spacing.xxs` before badge | 2.dp + 6.dp before badge |

The dev must re-derive this table against the current files rather than trust it — it is the PM's
read, not a reviewed spec. #1-#3 are token mappings, not literal pt→dp copies: use the Android
design-system token that corresponds to the iOS one, and say which mapping you used.

## Acceptance criteria

- [ ] **AC1** — Given the Android customer profile tab, When it renders, Then the avatar, the
      name/email/tier column, and the edit control are in a **single row**, with the edit control
      **vertically centred** against the full row height while the avatar and text column stay
      top-anchored — matching `ProfileTab.swift:296-303`. Evidence: side-by-side screenshots
      (iOS simulator / Android emulator) attached to the ticket.
- [ ] **AC2** — Given the delta table above, When the port is complete, Then each row is either
      **matched** or **explicitly deviated with a one-line reason** recorded in `## Review`. A silent
      skip fails the gate. Evidence: the annotated table in the verdict.
- [ ] **AC3** — Given a user with a long name and a long email, When the hero renders at the narrowest
      supported width, Then nothing clips or wraps into the edit control (both stay `maxLines = 1`
      with ellipsis, and the edit control keeps its intrinsic width). Evidence: a screenshot at the
      narrow width with a long-name fixture.
- [ ] **AC4** — Given a user with **no** email on the profile, When the hero renders, Then no empty
      line is reserved — matching iOS `:287`. Evidence: a preview/screenshot with an empty-email
      fixture.
- [ ] **AC5** — Gate 8: `:core` + `:customer-app` `compileDebugKotlin` + `testDebugUnitTest` succeed
      and the run is **not `UP-TO-DATE`** (record task outcomes). Kotlin diff byte-clean (no BOM/
      mojibake).

## Out of scope

- The **partner** Android profile screen — a different surface, not what the owner reported.
- Rendering a real avatar image — that is T-0448 (needs the read path, T-0446). This ticket keeps the
  initials circle and must leave the avatar `Box` shaped so T-0448 can drop an image into it without
  re-laying-out the hero.
- The stats card below the hero, and the row list under it.
- Any change to `ProfileViewModel.kt` — this is layout only.

## Implementation notes

- Compose equivalent of the iOS trick: a `Row(verticalAlignment = Alignment.Top)` whose edit-chip
  child carries `Modifier.align(Alignment.CenterVertically)`, or `Modifier.fillMaxHeight()` +
  `wrapContentHeight(Alignment.CenterVertically)` if `IntrinsicSize` is needed. Pick the one that
  survives a long name without forcing an intrinsic-measure pass on the whole hero, and say which.
- **Shared-file lane:** `ProfileTab.kt` is serialized — T-0448 edits the same file later and must
  wait (recorded in `blocks:`).
- No new string resources expected: `R.string.profile_row_edit` already backs the pill
  (`ProfileTab.kt:329`). Verify before adding anything.

**No-decision note (skips the deliberation panel):** the only open question was the direction of
convergence, and the owner resolved it in this batch (iOS is the reference). No new behaviour, no new
seam — a layout port against a shipped reference. Sizing/AC/deps/layers set → DoR met.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 3)
- 2026-07-30 — ready (no deps; DoR met; no-decision note recorded — owner ruling supplies the decision)

## Review
<!-- reviewer writes verdict here; AC2's annotated delta table goes here -->
