---
id: T-0451
title: iOS avatar initials fail WCAG in dark mode — 2.14:1 against a hardcoded white circle
status: ready
size: S
owner: ios
created: 2026-07-30
updated: 2026-07-30
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

## Review
<!-- reviewer writes verdict here; AC1/AC2 ratios and AC4's rationale go here -->
