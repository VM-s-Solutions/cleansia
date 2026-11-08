---
id: T-0922
title: "The 2026-11-08 Android review findings"
size: M
owner: Michael Chaban
created: 2026-11-08
updated: 2026-11-08
depends_on: []
blocks: []
stories: []
adrs: []
layers: [android]
security_touching: false
---

## Context

Mike, 2026-11-08, on the review findings of that day:

> "Fix the findings as well. But don't focus too much"

So each fix is small, with no extra polish. They ride on T-0920's PR B (`feature/customer-onboarding-d-android`), the
Android half of concept D. The iOS, server, web and docs findings are T-0921, on PR A (#369).

## Findings

- **F8 (CI).** `android-ci.yml` compiles and runs the unit tests only (`.github/workflows/android-ci.yml:87-91`), so
  an R8 break passes CI. #370 hit two: a removed serializer, and a `VerifyError` from a return inside `synchronized`.
  Add `assembleStaging` for both apps to android-ci, and run `monitor_check.py` (ART's monitor catch-all rule, written
  to check #370's staging builds) over each staging APK's `dexdump` output. Commit it under the repo's tools with a
  one-line self-test, as the other checkers have. Keep CI time modest.
- **F9 (partner).** An empty `MAPBOX_ACCESS_TOKEN` crashes the partner app on any job with a door: the job map
  composes `MapboxMap` (`JobMap.kt:306`), and `MapView`'s constructor throws `MapboxConfigurationException`. Show the
  no-map fallback instead of crashing.
- **F10 (customer).** A disputes response that breaks the wire contract crashes the customer app instead of showing
  an error: `refresh`, `loadNextPage` and `getById` (`DisputeRepository.kt:69`, `:94`, `:116`) are not wrapped in
  `wireResult`. Map it to the error state the way the other repositories do.
- **F11 (partner).** The invoice list takes the day from the timestamp's own offset, UTC (`InvoicesListScreen.kt:297`),
  while the period pay lines take the phone's day (`PeriodPayScreen.kt:293`), so a payment between 22:00 and 24:00
  UTC shows different days. Use the phone's day for both (`formatShortDate`'s `inDeviceZone`).

## Done

Each finding is fixed in PR B.

## Out of scope

Any finding not listed above. The rest of #370's out-of-scope list stays reported.

## Work record

- 2026-11-08 — Filed on `feature/customer-onboarding-d-android`, made from master at `5b57d65b2` (#370's merge).
  Every finding is in the tree there.
