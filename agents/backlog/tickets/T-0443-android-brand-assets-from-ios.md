---
id: T-0443
title: Android brand assets — adopt the iOS mark for both apps (launcher icon, splash, notification icon)
status: done
size: M
owner: android
created: 2026-07-30
updated: 2026-07-30
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
manual_steps: []
sprint: 14
---

## Context

Owner, verbatim: *"Use the icons from the ios app for both customer and partner apps. And splash
screen can be the same as in ios app. Replicate the same in android app. Make it on your own to match
ios version. Logos you can basically take from ios resources that are present now."*

**iOS is the source of truth for the brand mark.** The iOS assets that exist today (verified by the
PM, 2026-07-30):

| Asset | Path | Size | sha1 (12) |
|---|---|---|---|
| Customer app icon | `src/cleansia_ios/CleansiaCustomer/Resources/Assets.xcassets/AppIcon.appiconset/AppIcon1024.png` | 1024×1024 | `a39ee5488041` |
| Partner app icon | `src/cleansia_ios/CleansiaPartner/Resources/Assets.xcassets/AppIcon.appiconset/AppIcon1024.png` | 1024×1024 | `f65d6c98356c` |
| Customer wordmark | `src/cleansia_ios/CleansiaCustomer/Resources/Assets.xcassets/LaunchWordmark.imageset/wordmark.png` | 1400×360 | `8239890d1e62` |
| Partner wordmark | `src/cleansia_ios/CleansiaPartner/Resources/Assets.xcassets/LaunchWordmark.imageset/wordmark.png` | 1400×480 | `c98a4ad8f081` |

The two app icons have **different** sha1s — customer and partner are deliberately distinct marks on
iOS, and that per-app distinction must be preserved on Android, not flattened to one icon.

**What Android has today (verified, and it confirms the prior diagnosis):**

- Both apps use hand-drawn **vector** foregrounds — `customer-app/src/main/res/drawable/ic_launcher_foreground.xml`
  (15 lines, `7e50e895c7fd`) and `partner-app/.../ic_launcher_foreground.xml` (24 lines, `cfc7b6256584`).
  Different files, different drawings — **the two Android apps do not share a mark, and neither matches iOS.**
- The adaptive-icon wrappers already differ structurally: customer's
  `mipmap-anydpi-v26/ic_launcher.xml` uses `<background android:drawable="@color/ic_launcher_background"/>`
  while partner's uses `<background android:drawable="@drawable/ic_launcher_background"/>` — and only
  the partner app **has** an `ic_launcher_background.xml`.
- **The system splash is already wired to the launcher foreground** on both apps:
  `values/themes.xml:7` → `windowSplashScreenAnimatedIcon = @drawable/ic_launcher_foreground`. So a
  correct launcher-foreground replacement fixes the system splash for free — verify this rather than
  duplicating the asset.
- The **partner app has no in-app splash composable at all** (`features/splash/SplashScreen.kt` exists
  only in the customer app) — the prior diagnosis's "bare spinner with no branding" reproduces. iOS
  closed the equivalent gap in T-0378 with a shared `SplashBrandingView`.
- `ic_notification.xml` exists per app and, per Android's notification-icon contract, **must be a flat
  monochrome silhouette with alpha** — it cannot simply be the colour mark. This is the one asset that
  is *not* a straight copy.

## Acceptance criteria

- [ ] **AC1** — Given each Android app, When installed, Then its launcher icon is visually the same
      mark as the corresponding iOS app icon (customer↔customer, partner↔partner), at every launcher
      shape (circle, squircle, rounded-square, teardrop). Evidence: 4 launcher screenshots per app,
      plus the iOS icon beside them.
- [ ] **AC2** — Given the adaptive-icon safe zone, When the icon is masked, Then no part of the mark
      is clipped: the mark sits inside the central **66dp of the 108dp** foreground with the outer
      18dp on every edge treated as bleed. Evidence: the foreground asset with its safe-zone geometry
      stated at file:line. *(This is why AC1 is not "copy the 1024 PNG in" — iOS icons are authored
      full-bleed for a fixed rounded-rect mask; a naive import will crop the mark on a circle mask.)*
- [ ] **AC3** — Given the two apps, When their adaptive-icon wrappers are compared, Then they are
      structurally identical (same background mechanism, same three layers), with the customer/partner
      difference living only in the mark itself. Evidence: the two `mipmap-anydpi-v26/ic_launcher.xml`
      diffed in the verdict.
- [ ] **AC4** — Given a cold start of either app, When the system splash shows, Then it shows the new
      mark on the brand background. Evidence: screenshot per app + confirmation that `themes.xml:7`
      still resolves to the updated foreground (do not add a second asset if the existing wiring
      suffices — say which you found).
- [ ] **AC5** — Given the **partner** app's in-app splash, When it shows, Then it is branded rather
      than a bare spinner, following the customer app's existing splash and the iOS `SplashBrandingView`
      shipped by T-0378. Evidence: screenshot. **The fail-closed auth/gate logic behind the splash must
      be byte-unchanged** — styling only (this is the constraint T-0378 held to; hold it here).
- [ ] **AC6** — Given `ic_notification.xml` in each app, When a notification is posted on API 26+,
      Then the icon renders as a recognisable monochrome silhouette of the new mark, not a grey blob
      and not a clipped square. Evidence: a posted-notification screenshot per app.
- [ ] **AC7** — Gate 8: `:core` + both apps `compileDebugKotlin` + `testDebugUnitTest` succeed, and
      the run is **not `UP-TO-DATE`** — an asset-only change is exactly the case a cached Gradle run
      silently skips (record task outcomes, and `--rerun-tasks` or a clean if needed).
- [ ] **AC8** — No raster asset is committed at a size that bloats the APK unnecessarily; if PNG
      densities are added, all of mdpi→xxxhdpi are present (a missing density scales badly). State
      which route (vector vs density set) was taken and why.

## Out of scope

- The **web** apps' logo/favicon — T-0444.
- Changing the iOS assets in any way. iOS is the source; this ticket only reads from it. **Never read
  or modify `src/cleansia_ios/**/Info.plist` or `**/project.yml`** — the owner's live Stripe key is in
  the working copies of those files.
- Any in-app logo/wordmark usage beyond the splash (headers, empty states) — separate work if wanted.
- Play Store listing graphics.

## Implementation notes

- The customer app currently lacks `ic_launcher_background.xml` while the partner has one; AC3 forces
  a decision — converge on one mechanism. State it.
- Prefer a **vector** foreground where the mark is reproducible as paths (keeps the APK small and
  scales cleanly); fall back to a density set of PNGs only if the mark has gradients/effects that
  vectorise badly. Whichever route, AC8 wants the reasoning in one line.
- Read `agents/knowledge/patterns-mobile.md` before starting.
- Gate-DP §G (folded by T-0374) applies: brand raster art is **never** substituted with an SF-symbol/
  Material-icon equivalent, and app chrome (AppIcon + launch + splash) is a per-app checklist item.

**No-decision note (skips the deliberation panel):** the owner supplied the decision in this batch —
iOS is the brand source, both Android apps adopt it, splash matches, and the owner explicitly
delegated execution judgment ("make it on your own to match iOS version"). No new behaviour or seam;
the adaptive-icon safe-zone and monochrome-notification constraints are platform requirements
recorded above, not open choices. Sizing/AC/deps/layers set → DoR met.

## Status log
- 2026-07-30 — draft (created by pm; owner batch item 4, Android half)
- 2026-07-30 — ready (no deps; DoR met; no-decision note recorded)

- 2026-07-30 — dispatched by the orchestrator, android + paired reviewer.
- 2026-07-30 — **done** — merged to `master` as `10d03f14` (PR #173), 22 files.
  **PM re-verification:** both apps' `ic_launcher_foreground.xml` are re-cut to the same 10-line /
  108dp / `viewport 108` structure (they were 15 and 24 lines with different structures before), and
  `ic_notification.xml` is now **byte-identical across the two apps** (sha1 `981999053b21`). The
  partner gained the shared `WordmarkSplash.kt` composable in `:core` and a
  `BrandIconCatalogTest.kt` guard. **Not verified by the PM:** whether the two foreground vectors
  *should* still differ in path data (sha1 `a836259a` vs `61021817`) — that may be the deliberate
  partner-lockup distinction the owner later ruled for the web in T-0444, or a miss. Recorded as an
  open question in `status/sprint-14.md` rather than asserted either way. No Android build was run by
  the PM; Gate 8 evidence is as reported in the PR.

## Review
<!-- reviewer writes verdict here -->
