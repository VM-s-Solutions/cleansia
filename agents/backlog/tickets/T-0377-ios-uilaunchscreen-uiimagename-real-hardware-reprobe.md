---
id: T-0377
title: "iOS launch screen — re-probe UILaunchScreen UIImageName on REAL hardware (known-broken on the iOS 16.4 SIMULATOR; color-only shipped by T-0372)"
status: proposed
size: S
owner: ios
created: 2026-07-03
updated: 2026-07-03
depends_on: []
blocks: []
stories: []
adrs: [ADR-0014, ADR-0018]
layers: [ios]
security_touching: false
priority: low
manual_steps: [owner-device-probe (a real iPhone is required — the finding is exactly that the simulator evidence is untrustworthy here)]
sprint: 12
source: phase/ios-fix1 T-0372 AC5 recorded deviation (2026-07-03)
---

> **The T-0372 AC5 deviation, kept alive as its own ticket so it isn't forgotten.** T-0372 shipped the
> launch screen COLOR-ONLY (`UIColorName: SplashBackground`, `#0284C7` + dark variant) because
> `UIImageName: mascot_waving` was empirically broken on the iOS 16.4 SIMULATOR — it rendered the raw
> imageset scaled-to-fill (giant mascot) or silently BLANK for every padded variant tried. The
> `patterns-mobile.md` launcher/splash row records exactly this: "known-broken on the iOS 16.4 SIMULATOR —
> re-probe on real hardware; color-only until then". The branded mascot splash currently lands one frame
> later in the restyled customer `SplashGateView`, so the user-visible gap is a single launch frame.

## Context
`UILaunchScreen` is plist-declared (no storyboard), so its rendering is entirely OS-side and the 16.4
simulator evidence may not represent real devices. Whether `UIImageName` + `UIImageRespectsSafeAreaInsets`
behaves correctly on real iOS 16 hardware is UNKNOWN — the probe was deliberately deferred rather than
shipping a possibly-broken first frame.

## Acceptance criteria
- [ ] **AC1 (probe)** — A probe build with `UIImageName: mascot_waving` (+ a reasonably-padded variant if
  the raw one scales wrong) is run on the owner's REAL iPhone (iOS 16.x) — cold launch, screenshot
  evidence of the first frame. Also observed once on a modern-iOS real device if available.
- [ ] **AC2 (decision + fold)** — Based on the probe: EITHER adopt `UIImageName` in both apps'
  `project.yml` (+ xcodegen re-run; keep `UIColorName`) OR keep color-only permanently; the
  `patterns-mobile.md` launcher/splash row is updated from "re-probe" to the verdict, and this ticket
  records the screenshot evidence.
- [ ] **AC3 (non-regression)** — If adopted: both apps build, launch clean on the 16.4 sim (color-only
  fallback acceptable THERE if the sim stays broken — record it), suites green, lint clean.

## Out of scope
- The in-app splash (`SplashGateView`) — already branded for the customer (T-0372); the partner's is
  T-0378.
- Any asset redesign; the probe uses the existing `mascot_waving` imageset.

## Implementation notes
- No-decision note: composes ADR-0018/T-0372's recorded deviation; the AC2 "decision" is an empirical
  probe verdict, not an architecture decision — no panel.
- The probe build can ride any owner device-pass session (e.g. the phase/ios-fix1 device pass) to avoid a
  dedicated round-trip.

## Status log
- 2026-07-03 — filed `proposed` by pm at the phase/ios-fix1 close, from the T-0372 AC5 recorded
  deviation. Low priority (the gap is one launch frame; the in-app splash is branded). Not dispatched;
  requires the owner's device for the probe.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
