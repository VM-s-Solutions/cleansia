---
id: T-0451
title: iOS avatar initials fail WCAG in dark mode — 2.14:1 against a hardcoded white circle
status: done
size: S
owner: ios
created: 2026-07-30
updated: 2026-08-01
depends_on: []
blocks: [T-0450, T-0449]
stories: []
adrs: []
layers: [ios]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Both iOS apps draw the profile-hero avatar initials in a **dynamic** colour on a **static** circle.
The circle is `Color.white` in both themes; the text is `CleansiaColors.primary`, which is
`Color.dynamic(light: Palette.sky600, dark: Palette.sky400)`
(`CleansiaCore/.../DesignSystem/CleansiaColors.swift:4`). In dark mode the text resolves to sky400
while the circle stays white.

**PM measurement, 2026-07-30** (WCAG 2.x relative-luminance formula):

| Pair | Ratio | 3:1 floor (large text) |
|---|---|---|
| sky400 `#38BDF8` on `#FFFFFF` — **iOS dark mode, live today** | **2.14:1** | **FAIL** |
| sky600 `#0284C7` on `#FFFFFF` — iOS light mode / Android both modes | **4.10:1** | PASS |

Call sites, both with the identical shape (`Circle().fill(Color.white)` + `.foregroundColor(CleansiaColors.primary)`):
- `CleansiaCustomer/Sources/Features/Profile/ProfileTab.swift:276-282`
- `CleansiaPartner/Sources/Features/Profile/ProfileHubContent.swift:156-162`

**Android already deviated deliberately, and wrote down why.** T-0442 shipped
`customer-app/.../features/profile/ProfileTab.kt:279-284`:

```
// The circle is fixed white in both themes, so the initials pin the light-mode
// brand blue; the theme-adaptive primary drops to 2.1:1 against it in dark.
Text(initials.uppercase(), style = …headlineSmall, color = Sky600)
```

So the direction of convergence is already settled by a shipped, commented, reviewed decision on the
other platform, and the owner has already ruled (T-0442, T-0443, T-0444) that the two phones converge.
iOS is simply the unfixed side.

**Not affected, do not "fix":** the Android partner avatars
(`partner-app/.../ProfileScreen.kt:304-320`, `PersonalSectionScreen.kt:168-190`) pair
`colorScheme.primary` with `colorScheme.primaryContainer.copy(alpha = 0.4f)` — **both** adaptive, so
the pair tracks the theme and is not the defect described here. Verify before touching.

## Acceptance criteria

- [ ] **AC1** — Given iOS **dark** mode, When the customer profile hero renders the initials circle,
      Then the initials measure **≥ 3:1** against the circle fill. Evidence: the computed ratio for the
      shipped pair, recorded in `## Review` alongside a dark-mode screenshot.
- [ ] **AC2** — Given iOS **dark** mode, When the **partner** profile hub renders its initials circle,
      Then the same holds. Evidence: same.
- [ ] **AC3** — Given iOS **light** mode, When either hero renders, Then the initials are unchanged
      from today (4.10:1, sky600). This is a dark-mode-only regression fix; a light-mode diff is a
      fail. Evidence: before/after light-mode screenshots.
- [ ] **AC4** — Given the fix, When it is described in `## Review`, Then it states whether iOS **pins
      the static colour** (matching Android's shipped deviation) or **makes the circle adaptive**, and
      why. If the circle is made adaptive instead, Android must be re-checked for parity and the delta
      recorded — do not create a new cross-platform divergence while closing this one.
- [ ] **AC5** — Gate 0.5: the iOS build/test is executed and its command + exit code recorded, or the
      inability to run it is declared under leg 3 (naming the agent or CI job that can). Leg 1: the
      AC evidence here is a computed contrast ratio, which **is** assertable — prefer a unit test over
      a screenshot and mutation-prove it; if that is not practical, say so under leg 3 rather than
      claiming a mutation that was not run.

## Out of scope

- The **avatar image** itself — T-0449. This ticket keeps the initials placeholder and must leave the
  circle's shape/size untouched so T-0449 can drop an image in without re-laying-out the hero.
- Auditing every other `CleansiaColors.primary`-on-static-background pair in the two iOS apps. If the
  dev spots more while in the file, **report them in `## Review`**; do not fix them here — a
  contrast sweep is its own ticket.
- Android. It is already correct on this surface.

## Implementation notes

- The Android comment at `ProfileTab.kt:279-280` is the reference rationale; iOS should end up
  saying the same thing in the same place, so the next reader of either file finds the reason.
- `Palette.sky600` is `0x0284C7` (`CleansiaCore/.../DesignSystem/Palette.swift:10`), `sky400` is
  `0x38BDF8` (`:8`).
- **Shared-file lane:** `CleansiaCustomer/.../Profile/ProfileTab.swift` is serialized —
  **T-0451 → T-0450 → T-0449**. This ticket is first because it is the only one of the three that is
  unblocked today.

**No-decision note (skips the deliberation panel):** no new behaviour and no open decision — the 3:1
floor is a platform accessibility requirement, and the direction of convergence was settled by the
owner's iOS↔Android ruling plus Android's already-shipped, commented deviation. AC4 exists so the
implementation choice is *recorded*, not *litigated*. Sizing/AC/deps/layers set → DoR met.

## Status log
- 2026-07-30 — draft (created by pm; wave-1 finding with no home, from T-0442's reviewer)
- 2026-07-30 — ready (no deps; DoR met; no-decision note recorded)
- 2026-08-01 — red→green (ios; recorded here as the branch carries one squashed commit).
  Test-first: `FixedWhiteContrastTests` written against a token that did not exist → RED
  ("Type 'CleansiaColors' has no member 'onFixedWhiteHex'"); added `CleansiaColors.onFixedWhite`
  + both call sites → GREEN.
- 2026-08-01 — reviewer F1/F2 addressed: `AvatarDiscBindingTests` (the `ConsentCatalogTests`
  `#filePath` idiom) binds both heroes' disc fill AND ink to the Core token; dropped the
  tautological `testTheFixedWhiteSurfaceIsWhiteInBothSchemes` — `fixedWhiteHex` now observes the
  disc rather than modelling it. The disc block is found by brace-matching outward from
  `Text(initials)`, not by grepping the file: `ProfileTab.swift:163` carries a legitimate
  `CleansiaColors.primary` on a row icon over an adaptive surface, which a file-wide check
  would have false-failed.
- 2026-08-01 — Gate 0.5 leg 1, three mutations, each restored byte-exact (sha256 re-verified):
  M1 token 0x0284C7→0x38BDF8 → `testInkClearsTheLargeTextFloorAgainstTheFixedWhiteSurface` RED
  (2.142277591669845 < 3.0), 1→0. M2 customer ink→`primary` → both `AvatarDiscBindingTests` RED,
  2→0 (the three arithmetic tests stayed green — exactly the gap F1 named). M3 partner
  fill→`primaryContainer` (1.4489:1) → both RED, 2→0. M2 and M3 hit different files, so both
  heroes are proven covered.
- 2026-08-01 — Gate 8 on the iOS 16.4 floor: Core 519/0 (clean build test), Customer 677/0,
  Partner 527/0, all exit 0; SwiftFormat 0.60.1 + SwiftLint 0.65.0 --strict clean. Gate 8.5: both
  apps installed and launched on 16.4 in dark, no crash.
- 2026-08-01 — DECLARED, unresolved: the first Core `clean build test` after the guard landed
  reported 519 tests / **1 failure**; the log was not retained, so the test cannot be named. Six
  subsequent runs (4 incremental, 1 `test`, 1 identical `clean build test`) were 519/0. This is
  **not** claimed to be the known flake — it is an unidentified, non-reproducing failure in a suite
  with flake history. Naming it needs a re-run loop that keeps the full log.
- 2026-08-01 — **`ready` → `done`. MERGED as `1c8fdd00` (PR #180)**, "fix(ios): pin the avatar
  initials to a colour that survives dark mode [T-0451]". Reviewer **APPROVED** (relayed by the
  orchestrator at close-out). AC evidence lives in the status log above and in the merge-commit body,
  not in `## Review` — see the PM reconciliation note below, which says so rather than papering over it.
- 2026-08-01 — **carried forward, NOT closed and deliberately NOT ticketed:** the declared
  unreproduced **519 tests / 1 failure** on the first Core `clean build test` (entry above). It stays
  a declared unknown rather than becoming a ticket because it cannot satisfy **Gate 0**: no named
  test, no file:line, no reproducible trigger, and six subsequent runs (including an identical
  `clean build test`) were 519/0. Filing it would manufacture a finding. It is surfaced to the owner
  in `status/sprint-14.md` instead, and the next agent to see a Core red should keep the full log.

## Review

### PM reconciliation, 2026-08-01 — what this section does and does NOT contain

**This is not a verdict and the PM does not write one.** It records the state of the artifact, because
a later reader will otherwise assume a missing verdict means a missing review.

**The reviewer's verdict text was never committed into this file.** The in-artifact trace of the
review is the status-log line *"2026-08-01 — reviewer F1/F2 addressed"* (`:107-113`) — two named
findings raised, both closed with the reasoning recorded, including the reviewer's specific objection
that a file-wide `CleansiaColors.primary` grep would have false-failed on the legitimate row icon at
`ProfileTab.swift:163`. That is a real review lane that ran; only its write-up is absent.
**APPROVED is relayed by the orchestrator, and is labelled as relayed.**

**Where each AC's evidence actually is** (AC1/AC2 asked for it here; it is one section up):

| AC | Evidence | Where |
|---|---|---|
| AC1 / AC2 | computed ratio `2.142277591669845 < 3.0` going RED under mutation M1; `AvatarDiscBindingTests` binding **both** heroes' disc fill and ink, RED under M2 (customer) and M3 (partner) — different files, so both heroes are proven covered | status log `:114-119` |
| AC3 | light mode unchanged — "Light mode is bit-for-bit identical before and after" | merge-commit body, `1c8fdd00` |
| AC4 | **pins the static colour** (Android's shipped deviation), and does it in the **token** rather than at the call sites, because `Palette` and `Color(hex:)` are internal to `CleansiaCore` so an app target cannot name `sky600`; `CleansiaColors.primary` untouched (correct in its other 293 usages) | merge-commit body, `1c8fdd00` |
| AC5 | Core 519/0, Customer 677/0, Partner 527/0, exit 0; SwiftFormat 0.60.1 + SwiftLint 0.65.0 `--strict` clean; **run on the 16.4 floor**; Gate 8.5 both apps installed and launched on 16.4 in dark | status log `:120-122` |

**Gate 8 consistency leg: correctly UNVERIFIED, not PASS.** `check-consistency.mjs` has no Swift
coverage, and as of `d6969fef` (#177) it now says so out loud — `--paths=src/cleansia_ios` prints
`NOT RUN` and exits **1** (PM-re-ran it on `1c8fdd00`). This ticket never claimed a green there.
Swift enforcement is **ADR-0032's** call (SwiftLint `custom_rules` or an XCTest guard, never the
walker) — do not file a "add Swift to the walker" ticket.

**This ticket is the origin of ADR-0032 and ADR-0033.** Its `patterns-mobile.md` hunk ("Ink on a
theme-INVARIANT surface — the ONE way") is what the reviewer refused to ratify inline, which produced
ADR-0032 (accepted, amended) and the split-off ADR-0033 (`proposed`). ADR-0032's **FT-3** (the iOS
theme-invariant contrast sweep) is this ticket's deferred `## Out of scope` item and is sequenced
after this merge — it is not owed by this ticket.
