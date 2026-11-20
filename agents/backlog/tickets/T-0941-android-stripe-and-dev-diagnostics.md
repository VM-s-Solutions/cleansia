---
id: T-0941
title: "The Android Stripe SDK upgraded with Link off, invoices that name the country in their language, the Android eye icon's label, the push docs, the DEV SQL job's firewall rule, and a read-only DEV metrics workflow"
size: L
owner: Michael Chaban
created: 2026-11-20
updated: 2026-11-20
depends_on: [T-0940]
blocks: []
stories: []
adrs: []
layers: [android, backend, ci, docs]
security_touching: false
---

## Context

Mike, 20 November 2026, on wave 4 and the F-110 investigation (AskUserQuestion): upgrade the Android Stripe SDK to
the latest so its payment sheets turn Stripe Link off in code like iOS, with every card flow re-tested and shown on
DEV before the merge; fix F-111, F-113 and F-117 from the fix-proof run; give execute-sql.yml's DEV job a per-run
firewall rule for its own runner; and add a read-only DEV metrics workflow, run once over the six API-stall windows.
This is wave 5 of the phase-2 findings programme.

Every row was ground-truthed against master 8f564b942 before filing.

## Doing

1. F-109 on Android: the pinned stripe-android 21.2.0 has no Link setting, so the Android sheets still offer *Pay
   with Link* (the Stripe account runs Link as a card brand, which card-only intents do not hide). The SDK goes to
   23.26.0, every Android sheet turns Link off in code, and every card flow is shown on DEV.
2. F-111: the Czech payout invoice prints the country in English (`FileExtensions.cs:134` reads `Country.Name`). The
   country is printed in the invoice's language.
3. F-113: the Android password field's show/hide icon has an English content description in every language
   (`CleansiaTextField.kt:83`). It takes string resources in the five locales.
4. F-117: `docs/architecture/push-notifications.md` and `docs/mobile-app/overview.md` say no simulator push shows the
   in-app banner; on 20 November a real DEV push did. The two sentences say what holds.
5. F-110, the SQL timeouts: some GitHub runners are on AWS, whose IPs DEV Postgres's firewall drops.
   `execute-sql.yml`'s DEV job opens a firewall rule for its own runner IP for the length of the job and always
   removes it, as `deploy-azure.yml` does; the PRO job is untouched.
6. F-110, the API stalls: a read-only, DEV-only metrics workflow reads the plan's, Postgres's and the sites' metrics,
   Resource Health and the Activity Log for given windows; after the merge it is run once over the six stall windows,
   and the cause and a costed remedy go to Mike.

## Not doing

- PRO (the execute-sql PRO job and every prod resource stay untouched); Apple Pay (waits for Mike's Merchant ID);
  the API-stall remedy itself (a plan or SKU change is Mike's call after the metrics).

## Done

Merged with CI green, each fix with a test where the platform allows one, the Android card flows shown on DEV, DEV
deployed with the wave, and the metrics run's reading with Mike.

## Work record

- 2026-11-20 — Filed after Mike's rulings; started on `fix/t0941-wave5`.
- 2026-11-20 — Built on `fix/t0941-wave5`, one review and its in-scope rows fixed. stripe-android went from 21.2.0 to
  23.26.0 (compileSdk 36, targetSdk still 35), and every Android PaymentSheet turns Link off (LinkConfiguration
  display Never, pinned by `PaymentSheetParamsTest`); shown on DEV: bookings with the save tick on and off, the Plus
  sheet and the add-card sheet, none offering Link; the layout workaround is gone. F-111: the payout invoice prints
  the country in its language. F-113: the Android eye icon reads its label in the user's language. F-117: the push
  docs say what holds. F-110: `execute-sql.yml`'s DEV job opens a per-run firewall rule for its runner, and
  `dev-metrics.yml` reads DEV's metrics, read-only.
- 2026-11-20 — Shipped in #393 (merge `b9c508ca4`) and #394 (merge `a142d0269`, the site metrics Azure offers), CI
  green; DEV deploy dispatched after #393 (run 44778651539). Proven after the merge: an Execute on DEV job on an AWS
  runner (us-east-1) opened its rule, ran its script and removed the rule (run 44782049010). The metrics runs
  44778249469 and 44781532777 settled the stalls: the single B2 instance runs out of memory and CPU and its apps
  restart, while Postgres never limits; Mike keeps B2, since DEV is only for testing. The recurring-occurrence sheet
  was not shown on DEV (it uses the same builder the test pins). Mike picked the four bugs the wave found as T-0942.
  Not picked, reported: Resource Health needs the `Microsoft.ResourceHealth` provider registered;
  `docs/deployment/azure-setup.md` says DEV's Functions run on Consumption, but they share the B2 plan.
