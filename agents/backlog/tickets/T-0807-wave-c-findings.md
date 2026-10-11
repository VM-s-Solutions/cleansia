---
id: T-0807
title: Fix the reported Wave C findings and the open Dependabot alerts
size: L
owner: pm
created: 2026-10-10
updated: 2026-10-11
depends_on: [T-0806]
blocks: []
stories: []
adrs: []
layers: [backend, android, ios, frontend, docs]
security_touching: true
---

## Context

Wave C ([T-0806](T-0806-request-work-and-native-startup.md), PR #318) closed with a list of observations "reported, not fixed" in its report (`agents/WAVE-C-2026-10-09.md`, *Observations outside Wave C scope*), plus 16 open Dependabot alerts on master.

On 2026-10-10 Mike asked to fix all of them ("Fix all of the finding that you found"), as a separate, explicit track ahead of Wave D, not absorbed into D.

A read-only ground truth checked every finding against the current tree before any code change. For each it established the root cause (file:line), any written design rationale, the fix options and the test plan.

Mike was away when the choices were made. Each choice is the ground truth's recommended option under his standing authorization for recommended routine choices; each is reversible and is listed below. Two findings turned out to be deliberate, documented designs: the Plus back arrow scrolling away (F10) and the launch-time notification prompt (F11). Their behaviour is kept, and what is decided for each is stated plainly.

Evidence, the ground truth and the decisions live off-tree under `/Users/michael/.codex/scratchpads/cleansia-wave-c-2026-10-09/raw/findings/`.

## Doing

- **F1 — iOS Profile hero truncation.** Restore Android's exact gaps (avatar 14, column-to-chip 12) so the name and email no longer truncate. Today SwiftUI applies its stack spacing on both sides of a `Spacer`, leaving a dead 40pt gap.
- **F2 — Plus purchase bar parity.** Android mounts the trial/subscribe bar only when a Stripe publishable key is configured, as iOS already does. Without a key, Android used to create server-side Stripe objects for a sheet that could not open.
- **F3 — Partner hero parity.** iOS reads the employee's upcoming orders in the dashboard's single update. It now shows Android's *Next job* hero (status label, localized when-line, name and address; a tap opens the order) and the today line. Its pending-offers card now loads from the dashboard; a SwiftUI lifecycle bug had kept it from ever loading. Android's when-line moves off hard-coded English onto its existing localized keys.
- **F4 — Dashboard money.** Both platforms print the pay-period and last-month figures with grouping and currency. iOS resolves the currency symbol by Android's `CurrencySymbols` rule (CZK → Kč on every device locale).
- **F5 — Post-trial price.** The *then X / month* line is no longer struck through on either platform. It is the price that will be charged.
- **F6 — Status-bar overlap.** The existing scroll-driven status-bar fade moves into Core on both platforms and is applied to both Partner dashboards.
- **F7 — Help shortcut.** It opens an e-mail to support@cleansia.cz on both platforms, instead of doing nothing.
- **F8 — Android bottom navigation.** Both bars report the selected tab to accessibility (`selectable` with `Role.Tab`).
- **F9 — iOS text links.** They get a 44pt minimum touch target at the component, covering every call site.
- **F10 — Plus back control.** The bar-less hero is kept; the arrow scrolling away is documented design at parity with Android. Its accessibility is fixed on both platforms: an accessible name, and a 44pt target on iOS.
- **F11 — Launch-time notification prompt.** Deliberate and documented; it is now written into the mobile overview. Asking only after sign-in is recorded as an option for Mike, not done.
- **F12 — Membership `getMine` session guard.** On both platforms, an answer that arrives after `clear()` can no longer restore the previous session's membership or mark it fresh.
- **F13 — The Wave C lens follow-ups:**
  - (a) The request-logging middleware stops building strings for bodies it will only suppress. The bounded pre-authentication read and Kestrel's 413 are unchanged.
  - (b) Android's dashboard starts stats together with the other two reads; no performance claim.
  - (c) iOS no longer strands `plansState` in `.loading`.
  - (d) Android answers a superseded plans read with the silent error, not a `CancellationException`.
- **Dependabot.** All 16 open alerts are resolved in `src/Cleansia.App` and `docs`, through the supported paths:
  - version bumps;
  - `nx migrate`, whose migrations made no changes;
  - scoped overrides where an upstream pins a vulnerable version, or where no patched release exists (`sprintf-js` leaves the tree through `js-yaml@3 → argparse 2`).

## Acceptance criteria

- [x] **AC1** — Each finding above lands as decided, with a test that fails before and passes after, or with the stated documentation for F11. The F10 behaviour is kept and documented.
- [x] **AC2** — The Dependabot alerts are resolved in the lockfiles. A clean CI-parity install validates locally: Node 22, npm 10, `npm ci`, unit tests for every project, lint, the three production builds, the three e2e smokes, and the docs build.
- [x] **AC3** — Before/after simulator and emulator captures, through ordinary navigation, show the visible fixes: Profile hero, Plus hero and bar, Partner hero, money, status bar, text links. Accessibility dumps show the semantics fixes.
- [x] **AC4** — Full affected gates pass:
  - backend Release build plus unit and host suites;
  - Android core/partner/customer unit suites;
  - iOS CleansiaCore/Partner/Customer schemes plus pinned SwiftFormat/SwiftLint;
  - touched-class iOS 16.4 floor tests;
  - repository checkers and the docs build.
- [x] **AC5** — Independent review per platform, plus a security review of the security-touching items (F2, F12, F13a), passes.
- [x] **AC6** — Exact-head CI passes on every applicable workflow, and a normal SHA-guarded merge follows.

## Out of scope

- Wave D (B005/B006) and everything later.
- B009 compression.
- The unranked audit candidates.
- **Findings the ground truth newly found, reported and not fixed here:**
  - Android booking still offers card payment without a Stripe key.
  - An Android release build does not refuse a blank key.
  - Discount-perk copy differs between platforms.
  - The partner Orders segmented control has no selected state for accessibility.
  - The other customer session caches lack a session guard, and iOS has no sign-in wipe.
  - `infrastructure.md` misstates DEV request logging.
  - The partner dashboard KDoc points at the web dashboard page.
  - iOS reloads the dashboard on every tab return.
  - Three Android next-job edge cases.
  - The iOS dashboard mascots are announced to VoiceOver.
  - The partner Profile hub has the same status-bar overlap.
  - Earnings/Invoices print "CZK" on English phones.
  - A null currency code is handled differently on the two platforms.
- **Found by the T-0807 reviews, reported and not fixed here:**
  - **iOS `PendingOffersStore.refresh()` has no session guard** (S11, latent). It is the same shape F12 fixes for membership. A forced sign-out while a pending-offers read is in flight can refill the store after `clear()`; iOS has no sign-in wipe, so the next cleaner on that phone could see the previous cleaner's held-offer card. F3 adds the dashboard as a caller; order detail already reached it. The fix is F12's pattern. This is the first follow-up to file.
  - Android `MembershipViewModel.startSubscribe` has no key check of its own; the gated bar is its only caller today.
  - Android order detail's card confirm for a recurring occurrence has no key check either (`OrderDetailViewModel.confirmRecurring`). This is the same family as "booking still offers card payment without a Stripe key".
  - The F3 port carries Android's three next-job edge cases to iOS as well.
  - The iOS dashboard spinner now waits for the upcoming read: about 1.4 s against about 0.2 s on the render fixture, not measured on DEV. The progressive option stays open.
  - iOS formats the hero's day with its own localized template ("Mon, Nov 30"), where Android prints "Mon 30 Nov". `DashboardFormat` builds a new `DateFormatter` per call.
  - On a phone with no mail handler, Help still does nothing.
  - `DashboardScreen.kt` keeps dead private helpers.
  - The moved `StatusBarFade.kt` keeps tracker ids in its comments.
  - Source-binding tests re-implement comment stripping and block extraction; a shared helper is an Architect question.
  - `CalculateOrderPay` writes `PayBreakdown` in the machine culture ("300,00" on a Czech-region Mac). Three `CollectedFeeSharePayTests` fail locally for that reason and pass under `en_US.UTF-8` and on CI.
  - iOS 16.4 floor debt in two touched classes, byte-identical on the base commit: three `ContentSafeAreaBindingTests` dark-mode colour checks (customer) and two `TextInkTests` (partner).
- **Options recorded for Mike, not done:** a pinned Plus back arrow (F10) and asking for notification permission at sign-in (F11).

## Work record

- 2026-10-10 — Filed from Mike's instruction; ground truth read-only. Dependabot refresh validated in an isolated worktree and committed (`545ae30`). Native and backend fixes are implemented in parallel per platform, with independent review.
- 2026-10-11 — Implemented RED→GREEN per platform and committed one cause per platform:
  - `aae6fca` backend F13a;
  - `196448e` Android;
  - `c456fc1` iOS;
  - `279007b` the cross-platform docs.

  The coordinator applied the reviews' notes:
  - the docs text each lane returned;
  - the stale `PendingOffersCard` row in the partner safe-area audit;
  - a pin for iOS F12's conditional `loading` reset;
  - one KDoc sentence on Android's `plansLock` rule.

  Items the reviews found outside the decisions are listed under *Out of scope*.
- 2026-10-11 — PR #319.
  - **First head, `96919e3`.** It failed only Android CI. `DashboardWireTest` still asserted the sequential stats-then-preview request order that F13b deliberately made concurrent; the hosted runner received the preview first.
  - **Fix, `e494ba4`.** The test now compares the refresh's two requests as a set, in a thread-safe list. Locally it passed 5/5 reruns, and partner ran 815 tests with 0 failures.
  - **Merge.** Exact-head CI passed on all workflows. Merged normally with the SHA guard as `64dfed7`.

## Review

Evidence lives off-tree in `/Users/michael/.codex/scratchpads/cleansia-wave-c-2026-10-09/raw/findings/` (`t0807-001/MANIFEST.sha256`; `web-deps-validate-001/`).

- **AC1.** Each lane wrote its tests first and recorded RED before GREEN (`t0807-001/findings-impl-result.json`). The coordinator's added iOS pin, `testAnOldSessionReadAnsweringFirstLeavesTheNewSessionsReadLoading`, fails with the `loading` reset made unconditional: `loading` is false and a third read is made (3 ≠ 2). It passes as committed. F10's behaviour and F11's launch-time ask are documented in `docs/mobile-app/patterns.md#native-ios` and `docs/mobile-app/overview.md` (*Permissions*).
- **AC2.** Node 22 and npm 10.9.9, with `npm ci` on the new lockfiles. All twelve steps exited 0:
  - typecheck;
  - every project's unit tests;
  - lint;
  - the three production builds;
  - the three e2e smokes;
  - the docs `npm ci` and build.
- **AC4.**
  - **Backend.** The Release build has 0 errors. Unit: 8,589 passed. Host: 488 passed. Integration: 842 passed and 3 failed. The 3 failures are `CollectedFeeSharePayTests` on this Mac's Czech-region culture; they pass 5/5 under `LC_ALL=en_US.UTF-8` (listed under *Out of scope*).
  - **Android.** Core 291, partner 815 and customer 1,671 passed, with no failures or skips.
  - **iOS.** CleansiaCore 765, Partner 1,091 and Customer 1,735 passed, with 1 skip that predates this change (VoiceOver needs the accessibility server). SwiftFormat 0.60.1 and SwiftLint 0.65.0 report 0 violations.
  - **iOS 16.4 floor.** The touched classes pass, except the existing floor debt that is byte-identical on `545ae30`. After the new pin, MembershipViewModelTests passed 49 of 49.
  - **Repository checkers.** All eleven pass, and the docs build passes.
- **AC3.** Wave C's six captures of master are the "before". The "after" captures were taken at `279007b` on the same owned simulator and emulator, with products built from a fresh export under Wave C's safe profile, using ordinary navigation only. Receipt: `/Users/michael/.codex/scratchpads/cleansia-t0807-2026-10-11/raw/render/receipt.json` (`35189d7c…`); everything started was stopped, with proofs.
  - **PASS:**
    - F1: name and e-mail untruncated; 14pt after the avatar.
    - F2: no bar or disclosure on a keyless build; the last perk clears the navigation bar by 63px.
    - F4: `3 609 Kč` and `6 870 Kč` on both platforms, plus `2 034 Kč` on iOS.
    - F5: no strike-through.
    - F6: the strip under the status bar is page-coloured once scrolled. Dark text pixels went from 11.8 % to 0 % on Android and from 4.8 % to 0 % on iOS.
    - F8: the active slot is `selected="true"`, the rest `false`.
    - F9: the links went from 19.3 to 44pt.
    - F10: iOS *Back* at 44×44; Android content-desc *Back*.
  - **F3 passes for the iOS Next-job hero and today line.** Two things were not observed:
    - The localized when-line ("In 35m") needs a job within a day; the fixture's is a month out, and tests pin it.
    - The hero tap was not exercised.
  - **Visible side effect, as the iOS review predicted:** the 44pt back button moves the Plus hero down about 27pt.
  - **Accessibility note:** Android's Plus back control now dumps as a clickable view over a named icon. That is the same `IconButton { Icon(contentDescription = …) }` pattern the apps' other back buttons use.
- **AC5.** Each platform had a reviewer and a security reviewer. All six returned approve-with-notes, none blocking. The security reviews covered F2, F12 and F13a.
- **AC6.** On exact head `e494ba4` every job passed:
  - Android, Backend, Frontend and iOS `build`;
  - Frontend e2e-smoke;
  - Docs;
  - Secret scan;
  - iOS Symbols.

  The first head's Android failure and its fix are in the work record. The SHA-guarded merge was `64dfed7` on 2026-10-11 at 00:21:49 UTC. Its parents are `5f94129` and `e494ba4`, and its tree equals the head tree. No deploy ran. Evidence: `t0807-001/t0807-ci-merge-closure-001.json`.
- **AC2, after the merge.** GitHub's Dependabot API reports all 16 alerts fixed on 2026-10-11 (13 in `src/Cleansia.App/package-lock.json`, 3 in `docs/package-lock.json`), with 0 open on master.

