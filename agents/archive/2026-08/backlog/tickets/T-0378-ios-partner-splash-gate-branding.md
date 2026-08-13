---
id: T-0378
title: "iOS partner in-app splash branding — SplashGateView is a bare ProgressView; bring it to brand parity (the customer got the full branded splash in T-0372)"
status: done
size: S
owner: ios
created: 2026-07-03
updated: 2026-07-19
depends_on: []
blocks: []
stories: []
adrs: [ADR-0018, ADR-0020]
layers: [ios]
security_touching: false
priority: low
manual_steps: []
sprint: 12
source: phase/ios-fix1 T-0372 review-fold note (2026-07-03) — the partner launch-screen comment correction
---

> Surfaced during the T-0372 review fold: the partner's `SplashGateView`
> (`CleansiaPartner/Sources/Features/Splash/SplashGateView.swift`) renders a plain `ProgressView` on the
> background color while it resolves the session/registration gate — no gradient, no mascot, no wordmark.
> The CUSTOMER app's gate was restyled in T-0372 AC5 to the full branded splash (Sky600→Sky400 gradient,
> 180pt mascot_waving, Poppins "Cleansia" wordmark, tagline, white-tinted spinner, 600ms fade). The
> partner already HAS the ingredients in-target (T-0372 review fold copied `mascot_idea`/`mascot_mopping`
> into the partner catalog; `mascot_waving` + the Core `Mascot` enum + fonts are shared), so this is a
> small styling port.

## Context
Brand consistency between the two apps is the recorded design intent (the Android partner file's own
comment: "the two apps share a visual home"). The partner gate is the first screen every partner sees on
cold start; today it reads as an unbranded loading page while the customer app opens branded.

## Acceptance criteria
- [ ] **AC1 (branded gate)** — `SplashGateView` mirrors the customer's T-0372 splash composition (gradient
  full-bleed, mascot_waving, wordmark, tagline, white-tinted `ProgressView`, the 600ms opacity fade) with
  any partner-appropriate copy differences recorded; the gate's RESOLUTION LOGIC (`SplashViewModel`,
  `onResolved`, the fail-closed RegistrationLock chain — T-0304/T-0310 gate #24) is byte-untouched.
- [ ] **AC2 (parity check)** — Gate-DP one-liner recorded against the Android partner splash surface (its
  system splash `splash_background.xml` + launcher styling); divergences (if the Android partner has no
  in-app splash screen of its own) recorded as the customer-composition adoption, brand-sanctioned.
- [ ] **AC3 (non-regression + floor)** — Partner + Core suites green; swiftformat/swiftlint --strict
  clean; the iOS 16.4 floor smoke shows the branded gate (T-0374 leg); no change to launch timing
  (the gate still resolves via `vm.resolve()` on `.task`).

## Out of scope
- The plist `UILaunchScreen` image question — T-0377.
- Any gate/routing behavior change; this is styling only.

## Implementation notes
- No-decision note: composes ADR-0018 branding parity + the T-0372-landed splash composition — no new
  decision, panel skipped.
- Reuse the customer splash view's composition rather than re-deriving values; if that means hoisting a
  small shared splash layout to `CleansiaCore`, apply the ≥2-call-sites rule and record the harvest in
  `patterns-mobile.md`.

## Status log
- 2026-07-03 — filed `proposed` by pm at the phase/ios-fix1 close, from the T-0372 review-fold note (the
  partner launch-screen comment correction made the gap explicit). Low priority: cosmetic, short-lived
  screen; all assets already in place. Not dispatched.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — shipped on feature/i18n-cluster-3 (merged PR #126): shared-look SplashBrandingView ported to partner.
