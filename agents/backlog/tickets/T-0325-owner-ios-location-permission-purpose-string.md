---
id: T-0325
title: "Owner: iOS location-permission purpose string (NSLocationWhenInUseUsageDescription) ×5"
status: done
size: S
owner: owner
created: 2026-06-27
updated: 2026-07-19
depends_on: []
blocks: [T-0335]
stories: []
adrs: [0014, 0018]
layers: [ios]
security_touching: false
manual_steps: [ios-location-purpose-string]
sprint: 12
source: T-0306 §7.6 D2 + T-0310 §7.7 Scope A (the deferred current-location FAB's plist gate)
---

> **OWNER TASK — do this whenever you want the map's "my-location" button built (T-0335).**
> Nothing else is blocked by it; the address picker already works fully (pan + search) without it.

## What this is (plain English)

`NSLocationWhenInUseUsageDescription` is the **message iOS shows in the location-permission popup**
("Allow CleansiaPartner to use your location? — <this text>"). Apple **requires** it before the app is
allowed to ask for location at all — if it's missing, iOS silently denies and the popup never appears.

It's needed by exactly one feature: the **"my-location" button** on the address-picker map (tap → the map
jumps to your current GPS position). That feature is **T-0335** and is not built yet, which is why this
string isn't in the app yet — there was no point adding a permission prompt for a button that doesn't exist.

## Where it goes (it's just text, not signing/provisioning)

It's a string in `info.properties` of **both** apps' `project.yml`, right next to `API_BASE_URL` and the
fonts that are already there:
- `src/cleansia_ios/CleansiaPartner/project.yml` → `targets.CleansiaPartner.info.properties`
- `src/cleansia_ios/CleansiaCustomer/project.yml` → (same, when the customer picker lands)

The **mechanical add is trivial and an agent can do it** — the only owner-input part is **approving the
user-facing wording** (it appears in a system privacy prompt and in the App Store privacy disclosure, so you
may want to control the exact copy + its 5 translations).

## Proposed wording (×5 — review / edit, then it gets added)

| Locale | Text (proposed — yours to approve/edit) |
|---|---|
| en | Cleansia uses your location to center the map on your current position when you set your address. |
| cs | Cleansia používá vaši polohu k vycentrování mapy na vaši aktuální pozici při zadávání adresy. |
| sk | Cleansia používa vašu polohu na vycentrovanie mapy na vašu aktuálnu pozíciu pri zadávaní adresy. |
| uk | Cleansia використовує ваше місцезнаходження, щоб відцентрувати карту на вашій поточній позиції під час введення адреси. |
| ru | Cleansia использует ваше местоположение, чтобы центрировать карту на вашей текущей позиции при вводе адреса. |

(Localized Info.plist strings on iOS live in per-language `InfoPlist.strings`; the agent wires the mechanism
when building this — you only need to approve the copy.)

## Done when
- [x] Owner approves the wording ×5 (or edits it). *(Approved verbatim 2026-07-19.)*
- [x] `NSLocationWhenInUseUsageDescription` is added to the partner (and later customer) `project.yml`
      `info.properties`, localized ×5. (Agent can do the mechanical add once the copy is signed off.)
- [x] Unblocks **T-0335** (the my-location FAB + auto-center).

## Notes
- **When-in-use only** — no `NSLocationAlwaysUsageDescription` (no background location; matches Android's
  FusedLocation when-in-use usage).
- The privacy-manifest location entry (Gate-AR / check #16) is carried in **T-0335** alongside the feature,
  not here — this ticket is just the purpose string.

## Status log
- 2026-06-27 — created as the explicit owner backlog item (per owner request). Previously referenced only as a
  "proposed owner manual_step" inside T-0306/§7.6 D2, T-0310/§7.7 Scope A, and T-0335 `depends_on` — but no
  ticket file existed. This file makes it a real, do-it-later owner task with proposed copy. Blocks T-0335.
- 2026-07-19 — **done.** Owner approved the proposed copy ×5 verbatim (2026-07-19); mechanical add landed by the
  ios agent: `NSLocationWhenInUseUsageDescription` in **both** apps' `project.yml` `info.properties` (en base,
  next to the camera/photo strings) + the ×5 localized values appended to both apps'
  `Resources/{en,cs,sk,uk,ru}.lproj/InfoPlist.strings` (the existing localized-Info.plist mechanism —
  `NSCameraUsageDescription` precedent). `xcodegen generate` re-run in both app dirs (Info.plist carries the key).
  When-in-use only, no Always variant. Unblocks T-0335 (implemented in the same batch).
