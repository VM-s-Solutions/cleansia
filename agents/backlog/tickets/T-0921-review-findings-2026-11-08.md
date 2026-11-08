---
id: T-0921
title: "The 2026-11-08 review findings for iOS, server, web and docs"
size: M
owner: Michael Chaban
created: 2026-11-08
updated: 2026-11-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [backend, frontend, ios, docs]
security_touching: false
---

## Context

Mike, 2026-11-08, on the review findings of that day:

> "Fix the findings as well. But don't focus too much"

So each fix is small, with no extra polish. They ride on T-0920's PR A (#369, `feature/customer-onboarding-d`).

## Findings

- **F1 (server).** `UpdateCurrentUser` accepts a phone number in any format. Its validator checks only that no other
  account holds it (`UpdateCurrentUser.cs:62-66`), and `[PhoneNumber]` on `User.PhoneNumber` (`User.cs:44`) is never
  enforced on save. Validate the number only when it differs from the stored one, so a replayed stored number still
  saves, with the clients' rule: a possible number for its country. Reuse an existing error key if one fits. A new key
  needs its `api.*` text in all five locales of every web app that reaches the endpoint, and a mobile mapping only
  where an app maps this endpoint's errors.
- **F2 (web).** The private `detailsOf` in the customer profile facade (`profile.facade.ts:241`) has no caller.
  Delete it.
- **F3 (iOS test).** `handOverMascots` in `AppointmentConfirmationVisualTests+Arrival.swift` watches a fixed 5 s
  window (`:345`), so `testTheBusyCardsCleanerStandsAsTallAsTheWelcomingFigure` fails with "no welcoming figure"
  (`:165`) on a slow runner. Wait for the landing with a generous bound instead.
- **F4 (iOS).** The customer `CreateDisputeViewModel.addEvidence` prepares the evidence image on the main actor
  (`CreateDisputeViewModel.swift:80-88`). Move the encode off the main actor, as #368 did for the avatar.
- **F5 (iOS test).** `PartnerLegalCaptureTests.documentLoaded` (`:112-116`) accepts a web view that is not loading and
  has a content height over 1 pt, which a blank page also has. Wait on the real condition, as `e60def088` did for the
  contract in `PartnerOrdersVisualTests`.
- **F6 (iOS build).** fastlane's `export_method: "app-store"` (`fastlane/Fastfile:218`) is deprecated. Use
  `"app-store-connect"`.
- **F7 (iOS).** `JobBandMapLayout.pinAnchor` (partner app, `JobDetailFrame.swift:339`) has no cap, so in a window
  shorter than the pin line the door leaves the screen. Mirror the Android rule that T-0919 shipped in #366 (`pinTip`
  in `JobMap.kt:128`: a map no taller than the pin line puts the door at its centre), test-first.
- **F12 (docs).** CLAUDE.md's quick reference was reported to name Angular 19 and Nx 21. State the versions that
  `src/Cleansia.App/package.json` pins.
- **F13 (docs).** `agents/knowledge/patterns-mobile.md` still prescribes app-local address-picker and devices views
  that #368 moved into CleansiaCore (`:1046`, `:2041`), and its `BirthDateField` row (`:1016`) describes the sheet
  that T-0920's iOS half replaces. Correct both to the tree. A rule change routes per ADR-0033.

## Done

Each finding is fixed in PR A, or reported in the work record with the reason it was not.

## Out of scope

Any finding not listed above, and everything T-0920's Out of scope names.

## Work record

- 2026-11-08 — Filed on `feature/customer-onboarding-d` after it merged master at `b5c9823c2`. Every finding is in
  the tree except F12: `CLAUDE.md:101` already names Angular 20.3 and Nx 23.2, as `package.json` pins
  (`@angular/core` `~20.3.34`, `nx` `23.2.1`), since `8c2e8b461`. F7's `JobBandMapLayout` is in the partner app, not
  in Core. T-0922 is held for T-0920's Android half (PR B), which files it.
- 2026-11-08 — F13: `agents/knowledge/patterns-mobile.md`'s address-picker row and devices bullet now describe the
  Core screens #368 shipped, in T-0920's records commit, with the routing test's sweep and both readings in T-0920's
  `## Review`. The `BirthDateField` row (`:1016`) is not edited: rewriting it replaces the sentence that governs the
  sheet and carves the onboarding's own birth date row out of "the ONE tappable birth-date control", so it routes
  to the Architect (ADR-0033, test 2); the draft row is in T-0920's `## Review`.
- 2026-11-08 — F3: `handOverMascots` records until the welcoming figure has stood for a second, and gives a slow
  runner 30 s to land it, instead of a fixed 5 s (`5786e8bd9`). The assertions are unchanged. On the iOS 26.3
  simulator the test passes in 10.3 s instead of 15.3 s; on all three phones the welcoming figure measures as
  before, and the busy card's cleaner within 0.06 pt.
- 2026-11-08 — F4: `EvidencePreparer.prepare` runs its work in a detached task, as `AvatarEdit.pick` does, and
  `addEvidence` awaits it (`51b21cf19`). The dispute detail's upload calls the same `prepare`, so it awaits it too
  and no longer freezes either. A photo whose encode ends after a submit has started is not added, and its temp file
  is removed. Both new tests failed before the change on the iOS 26.3 simulator and pass with it.
- 2026-11-08 — F5: `documentLoaded` asks the document how tall it has laid its text out, once its fonts are loaded
  and its body has content, and waits until the scroll view's content is at least that tall (`e2dd92ded`). "At
  least", not equal as in `e60def088`: this web view scrolls itself, and a throwaway probe found its content 20 pt
  taller than the body, the first heading's top margin, in all fifteen captures on iOS 16.4 and 26.3.
- 2026-11-08 — F6: not changed. fastlane 2.237.0, the only fastlane on this Mac, refuses the new name in
  `build_app`'s own option check, also when it comes from `export_options`: "Unsupported export_method
  'app-store-connect'". `fastlane customer` and `fastlane partner` would stop before they archive. The deprecation
  is xcodebuild's warning ("Command line name "app-store" is deprecated"), and the export still works; the name can
  change once a fastlane release accepts it.
- 2026-11-08 — F7: `JobBandMapLayout.pinAnchor` puts the door mid-map on a map no taller than the pin line, the
  rule of Android's `pinTip` (`5930074e5`). Test-first in the partner app's `JobDetailPageTests`, where the type
  lives: the new test failed before the change (the door at 1.0 and 1.026 of the map's height) and passes with it.
