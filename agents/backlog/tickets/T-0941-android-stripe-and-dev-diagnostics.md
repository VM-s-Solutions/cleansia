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
