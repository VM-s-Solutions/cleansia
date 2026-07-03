---
id: T-0372
title: "iOS brand/asset parity — 6 mascot imagesets + Core Mascot enum + AnimatedMascotView (ImageIO webp), mascots across auth/splash/success/hero/empty-states/membership (+ BusyMascotOverlay), app icons BOTH apps, branded launch screens BOTH apps, category icon meaning + tints"
status: in_progress
size: M
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: [T-0373]
stories: []
adrs: [ADR-0014, ADR-0018]
layers: [ios]
security_touching: false
priority: medium-high
manual_steps: []
sprint: 12
source: phase/ios-fix1 on-device shakeout diagnosis (2026-07-02, cluster brand-assets)
---

> **Dev DISPATCHED (in_progress).** Not iOS-16-specific — the assets are absent on EVERY version; the
> owner's device test was the first time a human looked at the app (sim suites never assert branding). These
> are Gate-DP **misses, not gate gaps**: ADR-0018 puts "mascot" and "logo" inside non-negotiable branding
> parity (`ios-app-architecture.md:56-93`), but everything living in the Android `res/` tree and app-level
> packaging (AppIcon, UILaunchScreen) slipped because the gate's citation unit is the `.kt` screen file —
> the checklist hardening is **T-0374**. The Partner iOS app already proves the whole pipeline (4 imagesets +
> `Mascot.swift` + `MascotEmptyState`) — most of this is mechanical replication; the genuinely new code is
> the animated-WebP player.

## Context (brand-assets cluster: findings 1–3 + design deltas 2/3/4-6)
- **No mascots anywhere in the customer app.** `CleansiaCustomer/Resources/Assets.xcassets` contains ONLY
  `google_g.imageset`; `AuthHeaderImage.swift:8` is an SF-symbol "sparkles" placeholder. Android inventory:
  6 static 1024×1024 PNGs in `res/drawable-nodpi` (waving/ready/idea/mopping/cleaning/leaning) + 2 animated
  WebPs in `res/raw` (`mascot_cleaning_in_progress.webp`, `mascot_welcoming.webp`) played via Coil
  (`MascotAnimation.kt:37`). Full usage map in the diagnosis (SignIn/SignUp/EmailVerify/Forgot/Onboarding/
  Splash/SubscribePlus hero → mascot_waving; Home slides; MembershipManagementCard → mascot_ready; Orders +
  Disputes empty states → mascot_idea; BusyMascotOverlay/LiveProgressHero → animated cleaning; success
  screens → animated welcoming).
- **No app icon — BOTH apps.** Both `project.yml`s set `ASSETCATALOG_COMPILER_APPICON_NAME: ""` and neither
  catalog has an `AppIcon.appiconset` → blank grid placeholder on install. Android source of truth: white "C"
  vector foreground on solid `#0284C7` (customer + a bolder partner variant); the `android:pathData` is
  verbatim SVG path syntax → a 1024px master is trivially derivable.
- **No launch branding.** iOS has `UILaunchScreen: {}` (blank white) and `SplashGateView.swift:13-33` renders
  a plain "sparkles" gate — vs Android's two-stage splash (system splash `#0284C7` + icon; in-app
  Sky600→Sky400 gradient, mascot_waving 180dp, Poppins Bold "Cleansia" wordmark, tagline, spinner, 600ms fade).
- **Category icon meaning wrong + no tints.** iOS maps home→"sparkles" (`ServicesStepComponents.swift:
  247-256`) vs Android home→CleaningServices (broom) `#0284C7`, deep→Spa `#7C3AED`, laundry→
  LocalLaundryService `#0891B2`, pet→Pets `#EA580C` (`ServicesStep.kt:74-82,739`); AR-DP-2 requires the SF
  Symbol to map the Android icon's MEANING.
- **Submit feedback + success/hero/empty-state deltas.** No `BusyMascotOverlay` during booking/subscribe
  submit (`BusyMascotOverlay.kt:80-120` vs a footer spinner only); success screens show glyph circles vs the
  played-once welcoming mascot; `LiveProgressHero.swift:36-38` shows a 44pt SF symbol vs the 140dp animated
  mascot; Orders/Disputes empty states use SF glyphs despite CleansiaCore ALREADY shipping `MascotEmptyState`
  (used by the partner app — `OrdersListComponents.swift:160`).

## Acceptance criteria
- [ ] **AC1 (imagesets)** — The 6 mascot PNGs land in `CleansiaCustomer/Resources/Assets.xcassets` as
  universal single-scale imagesets (the exact Contents.json shape of the partner's `mascot_waving.imageset`,
  template-rendering-intent: original); optionally optimized (~8 MB raw).
- [ ] **AC2 (Core enum + animator)** — Partner's `Mascot.swift` enum is promoted to `CleansiaCore` (+
  `.idea`/`.mopping` cases; partner repoints); a Core `AnimatedMascotView` (UIViewRepresentable wrapping
  `CGAnimateImageDataWithBlock` — ImageIO decodes animated WebP since iOS 14, safe at the 16 floor) plays the
  2 WebPs bundled as data assets, with a static-still fallback mode.
- [ ] **AC3 (usage parity)** — mascot_waving in AuthHeaderImage (SignIn/SignUp/EmailVerify/Forgot/
  Onboarding), SplashGateView, and the SubscribePlus hero; mascot_ready in MembershipManagementCard
  (`:94/:152`, replacing crown/checkmark); mascot_idea via the EXISTING Core `MascotEmptyState` in
  OrdersEmptyView + the disputes empty state (exactly as the partner app does); animated welcoming
  (loop:false, freeze on last frame) on BookingSuccess + MembershipSuccess; the 140pt top-trailing animated
  mascot overlay + trailing content padding on LiveProgressHero; a `BusyMascotOverlay` (scrim +
  RoundedRectangle(24) card + 140pt mascot + message, spring scale-in transition) attached to
  BookingSheetView + SubscribePlusScreen keyed to isSubmitting (static mascot_cleaning acceptable until the
  animator lands; button spinner kept).
- [ ] **AC4 (app icons, BOTH apps)** — A 1024×1024 full-bleed master per app (white C path on `#0284C7`
  baked in, no transparency; partner slightly differentiated, e.g. the bolder C), `AppIcon.appiconset` with
  the modern single-size Contents.json, `ASSETCATALOG_COMPILER_APPICON_NAME: AppIcon` in both `project.yml`s
  + xcodegen re-run; both apps install with a real icon.
- [ ] **AC5 (launch screens, BOTH apps)** — `UILaunchScreen` populated in both `project.yml`s
  (`UIColorName: SplashBackground` — a new color asset `#0284C7` with a dark-appearance variant mirroring
  values-night — + `UIImageName: mascot_waving`, `UIImageRespectsSafeAreaInsets: true`); the in-app
  `SplashGateView` restyled to mirror `SplashScreen.kt`: Sky600→Sky400 LinearGradient full-bleed, 180pt
  mascot_waving, white Poppins "Cleansia" wordmark (font already bundled via UIAppFonts), white tagline +
  white-tinted ProgressView, a 600ms opacity fade on appear.
- [ ] **AC6 (category icons + tints)** — home→`bubbles.and.sparkles` (SF Symbols 4, iOS 16; reads as
  cleaning — or `paintbrush.fill`); `CategoryPalette` extended to return the Android tint hexes
  (`0284C7`/`7C3AED`/`0891B2`/`EA580C`) so card icon chips match; `"washer"` verified rendering on a real
  iOS 16.0 device/16.4 sim (an unavailable symbol renders EMPTY, silently); the no-broom substitution
  recorded as the one-line Gate-DP divergence note.
- [ ] **AC7 (verification)** — Both apps build; suites green; swiftformat/swiftlint --strict clean; iOS
  16.4-simulator visual smoke of splash/auth/home/success/empty states (T-0374 leg).

## Out of scope
- The Home upsell carousel (the structural pager) — **T-0373** (depends on this ticket's imagesets + T-0368's
  shell).
- Any Android-side asset change.
- The Gate-DP checklist hardening itself — **T-0374**.

## Implementation notes
- Pipeline exists in the partner target — copy the shapes, don't invent. Icon master: an SVG with a
  `#0284C7` 1024px rect + the white C pathData scaled 108→1024 (×9.481), rendered via
  rsvg-convert/qlmanage.
- If the animator proves fiddly, ship static stills first and keep the animated pass inside this ticket's
  review cycle (the diagnosis sanctions the split).
- project.yml edits require an xcodegen re-run — the dev runs it locally (not an owner step; recorded here
  for the reviewer).

## Status log
- 2026-07-03 — filed `in_progress` by pm from the phase/ios-fix1 diagnosis (brand-assets cluster; findings
  1–3 + deltas 2/3/4-6; delta 1 — the upsell carousel — split to T-0373). Dev dispatched. medium-high
  priority (no crash, but the single largest visual-credibility gap; App-Store-blocking via the icon).
- 2026-07-03 — dev: slice landed in `62d9495b` on phase/ios-fix1. AC1/AC2/AC4/AC6 delivered; AC3 delivered
  except the two BusyMascotOverlay attachments (see the fold entry below); AC5 delivered with a recorded
  deviation (below). Verified per the commit: Core 252 / Customer 378 / Partner 369 green on iPhone 17 +
  the asset/palette/splash suites re-run on the iOS 16.4 destination; swiftformat 0.60.1 + swiftlint
  --strict clean; 16.4 visual smoke (mascot sign-in, branded splash, launch color, both springboard icons).
- 2026-07-03 — **AC5 deviation (recorded):** `UILaunchScreen` ships **color-only** (`UIColorName:
  SplashBackground`, `#0284C7` + dark variant) instead of the AC's `UIImageName: mascot_waving` —
  empirically on the iOS 16.4 simulator `UIImageName` renders the raw imageset scaled-to-fill (giant
  mascot) or silently blank for every padded variant tried. The branded mascot splash lands one frame later
  in the restyled customer `SplashGateView`. Re-probe `UIImageName` on real hardware before adopting
  (`patterns-mobile.md` launcher/splash row updated to say exactly that).
- 2026-07-03 — **AC6 Gate-DP divergence (one-line):** no SF Symbols broom exists, so Android
  home→CleaningServices (broom) maps to iOS home→`bubbles.and.sparkles` — same "cleaning" meaning, native
  symbol (AR-DP-2).
- 2026-07-03 — dev: review fold (reviewer requested CHANGES). (1) `AnimatedMascotView` stale-animation bug
  fixed: empty `updateUIView` froze the OLD animation when SwiftUI reused the `UIImageView` across a mascot
  switch (e.g. LiveProgressHero `.welcoming` → `.cleaningInProgress` as an order advances under refresh) —
  now a `Coordinator` holds the active `(data, loop)` + a generation token; `updateUIView` restarts on
  change and a superseded run stops itself via the block's stop flag; the failure fallback now respects the
  view's `bundle` (`UIImage(named:in:with:)`). **Red→green:** `shouldRestart`/`isSuperseded` tests added to
  `AnimatedMascotPlaybackTests` first (6 tests, confirmed failing-to-compile red), then the Coordinator
  implementation turned the suite green (Core 259). (2) The accidental Xcode string-catalog sync rewrite of
  BOTH `Localizable.xcstrings` in `62d9495b` (33.6k lines of `extractionState: stale` churn + junk
  auto-extracted keys; verified zero value-level localization changes) reverted to the pre-commit content;
  `SWIFT_EMIT_LOC_STRINGS: NO` pinned in BOTH `project.yml`s so no build ever sync-rewrites the
  hand-managed catalogs (guard proven: a clean `xcodebuild` of CleansiaCustomer leaves both catalogs
  byte-identical). (3) AC3's `BusyMascotOverlay` built (customer-local `Components/BusyMascotOverlay.swift`,
  matching Android's customer-app home) and attached to SubscribePlusScreen keyed to
  `submitState.isSubmitting` (+ `busy_subscribe_plus` ×5 locales). Also: partner launch-screen comment
  corrected (its gate is a plain ProgressView, no mascot splash), `mascot_idea`/`mascot_mopping` imagesets
  copied into the partner catalog (Core `Mascot` has the cases; they resolved to a silently-empty `Image`),
  and the `patterns-mobile.md` `UIImageName` row softened to "known-broken on the iOS 16.4 SIMULATOR —
  re-probe on real hardware".
- 2026-07-03 — **AC3 deviation (recorded):** the `BusyMascotOverlay` attachment to **BookingSheetView is
  DEFERRED to T-0371** (Slice D owns that file in this parallel batch; attaching here would collide).
  T-0371 is the carrier: attach `BusyMascotOverlay` keyed to the booking submit state (+ `busy_booking` /
  `busy_payment` strings). SubscribePlusScreen is attached in this ticket.

## Review
<!-- reviewer / security / optimizer write verdicts here; PM reconciles before advancing state -->
- 2026-07-03 — dev (harvest note): folded two clarifications into `patterns-mobile.md` with the review fold —
  the `AnimatedMascotView` row now records the Coordinator `(data, loop)` + generation-token restart idiom
  (empty `updateUIView` freezes the old animation on view reuse; `CGAnimateImageDataWithBlock` has no cancel
  handle), and the launcher/splash row's `UIImageName` ban is softened to "known-broken on the iOS 16.4
  SIMULATOR — re-probe on real hardware; color-only until then".
