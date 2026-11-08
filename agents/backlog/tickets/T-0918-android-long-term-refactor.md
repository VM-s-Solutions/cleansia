---
id: T-0918
title: Android long-term refactor — minified staging builds, lag fixes, shared pieces in :core, dead code and docs drift
size: L
owner: Michael Chaban
created: 2026-11-08
updated: 2026-11-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android, docs]
security_touching: false
---

## Context

Mike, 2026-11-07, on the verified mobile audit, verbatim:

> "Don't do the quick fixes, I want you to do the fixes that actually matter and win in long term. Refactor the code,
> polish it, review it from technical side as well if it's optimized"

He agreed the scope below the same day, and later wrote:

> "Fix all of the bugs that you found and also what you found"

which adds the pattern-catalog drift (AND-PO-04) and the unused version-catalog entries.

This is the Android part of three pull requests; the crash fix (T-0914, #366) and the iOS refactor (T-0917, #368) are
the other two. Every item comes from the verified audit, `audit-20261107/report/verified-flat.txt`, the Android rows
(lines 181–388), which describe master `3e720a7e7` on 2026-11-07. A row's note column carries the corrections made when
the audit was verified, and they win over its laziest fix. Where the agreed scope goes further than a laziest fix (a
move to `:core` that the audit deferred to "when next touched"), the agreed scope wins: Mike chose the long-term
version. Each item is checked against this branch before it changes; one that no longer exists is recorded and skipped.

## Scope

- **Minified staging builds.** and-perf-01: a minified staging build for each app. Every lag number below is measured on
  these builds.
- **Lag.** and-perf-04 (the customer Home carousel reads its progress in the draw phase), and-perf-02 (the partner job
  detail builds its page once), and-perf-03 (a far bottom-tab jump does not build the tabs in between, both apps),
  and-perf-06 (one shared navigation transition definition in `:core` for both apps), and-perf-10 (the customer Home
  starts loading during the splash).
- **Shared pieces in `:core`, first part.** AND-CM-01 (`slideSemantics` in `:core`; the customer booking slide gets its
  TalkBack action), AND-CM-05 (one `ActionState`, in `:core`), AND-CM-06 (`PasswordRuleList` used everywhere, and one
  `IconHalo` in `:core` for the seven copies), AND-CM-02 (the customer theme sets its tertiary roles: the one intended
  visual change).
- **Shared pieces in `:core`, second part.** AND-CM-04 (the notifications-feed and devices data layers), AND-CM-07 (the
  floating island bottom bar), AND-CM-09 (the customer's four private status pills become `:core` `OrderStatusPill`),
  AND-PO-03 (one money formatter in `:core` for the partner app's copies).
- **Cleanup.** and-perf-05 (the mascots as 720 px WebP), AND-PO-01 (strings no code reads), AND-PO-02 (dead
  declarations), AND-PO-06 (the nine `Log.d` calls), and the unused Room, biometric and Lottie entries in
  `gradle/libs.versions.toml` (from AND-PO-04).
- **Docs.** AND-PO-04 (the mobile pattern catalog describes the apps as they are), the Android charter's drift, and
  every catalog line the moves above made wrong.

## Acceptance criteria

- [ ] Each lag fix has a before and an after number, measured on the minified staging build.
- [ ] Each moved piece exists once, in `:core`, and both apps use it.
- [ ] The unit tests of `:core`, `:partner-app` and `:customer-app` pass, and both staging builds assemble and start.
- [ ] The screenshots of every touched screen are unchanged, except the one intended visual change: the customer's
  tertiary colours (AND-CM-02).
- [ ] There is one plain-prose commit per item.

## Out of scope

Not done, even where a problem is visible; it is reported instead.

- Visual design changes: one look for the error views, card radii, the partner app's three money looks (the one money
  formatter keeps every call site's current output exactly), the address search UI.
- The audit's "not worth doing" list; baseline profiles (and-perf-08); the contract WebView fonts (and-perf-09).
- The test-only functions `bookingSuccessSteps` and `heroRightText`: Mike decides whether they are unwired features.
- iOS, backend and web; dependency version upgrades.
- `docs/` pages, except to fix a path or a name that a move made wrong.

## Work record

- 2026-11-08 — Filed.

### Minified staging builds

- 2026-11-08 — and-perf-01 done (`06ab04a94`). The partner staging build type now falls back to release for `:core`
  and uses the debug package, which has a Firebase client in `google-services.json`; the customer gained a staging
  build type initialised from debug, not debuggable, R8-minified with release's proguard files. R8 broke the partner
  start with "Serializer for class 'o' is not found": the route objects `NavRoute.Invoices` and `NavRoute.Profile` are
  registered but never navigated to, so the kotlinx.serialization 1.7.3 object rule, which the partner's
  `proguard-rules.pro` copies, let R8 drop their `INSTANCE` and `serializer()`; that rule is now conditioned on the
  class alone. Partner `assembleStaging` 1 min 41 s (R8 56 s), APK 81.6 MB, dex 8.0 MB in one file; customer
  1 min 39 s (R8 65 s), APK 88.8 MB, dex 12.3 MB in two files. Both install and start on `cleansia35` from cleared
  data and over a debug install, and show the server's "Incorrect password." for a failed sign-in against DEV.
  Unit tests: core 390/0, partner 1277/0, customer 1871/0. `check-consistency.mjs` reports what origin/master
  reports (one existing E1 violation). Evidence in `android-longterm-20261108/` (`logs/`, `start/`).

### Lag

- 2026-11-08 — and-perf-04 (`cd3a38396`), and-perf-02 (`fcdf39891`), and-perf-03 (`60c1c6656`), and-perf-06
  (`ae70fdebe`) and and-perf-10 (`350d49346`) done. Measured on the minified staging builds on `cleansia35`, signed in
  against a local stand-in for the two mobile APIs (a DEV sign-in needs a confirmed e-mail), its delays taken from
  DEV's anonymous endpoints, with uncommitted probes. The Home carousel recomposed 52 times a second while autoplaying
  and its slide 52-104, now 0 and 0; the frame's animation phase p50 0.35 ms, now 0.04-0.05 ms. A job opened from the
  board built 2 MapViews, 2 sheet states and 2 figure decodes, now 1, 1 and 1, from one `JobDetailFrame` call. Each
  far bottom-tab jump built one tab in between in both apps, now none (`:core` `PagerState.showTab`); the customer's
  orders were read 6 times over nine taps, now 2. The partner's push and pop took 742-793 ms (the 700 ms default
  cross-fade), now 322-364 ms, through the push and pop transitions now in `:core` (`pushEnter` and its three
  siblings), which the customer uses unchanged (335-373 ms both ways). The customer splash starts the orders and the
  membership reads: they land at 0.65-0.78 s after the process starts instead of 2.55-2.65 s, but Home's first
  content stays at about 3.45 s because the catalog, read after the hold, is slower; with a 2.5 s orders read, first
  content moves from 3.64-3.83 s to 3.42-3.54 s. The partner builds measured and captured carry a placeholder Mapbox
  token, since an empty one makes the MapView constructor throw. Captures of the touched screens (the carousel and
  the splash, the 107 job detail frames; en, light and dark) are byte-identical in the apps' content; the first dark
  360x800 frame of a run differs in 23 pixels of the system gesture handle, which two captures of one unchanged build
  also do. Unit tests: core 391/0, partner 1278/0, customer 1872/0. Both staging builds assemble and start;
  `check-consistency.mjs` reports what origin/master reports. Evidence in `android-longterm-20261108/` (`lag/`,
  `captures/lag/`).

### Shared pieces in :core, first part

### Shared pieces in :core, second part

### Cleanup

### Docs
