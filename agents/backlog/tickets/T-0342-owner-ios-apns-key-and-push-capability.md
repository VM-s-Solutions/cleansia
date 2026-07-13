---
id: T-0342
title: "Owner: iOS APNs auth key (.p8) + Push Notifications capability/provisioning"
status: proposed
size: S
owner: owner
created: 2026-06-28
updated: 2026-06-28
depends_on: []
blocks: []
stories: []
adrs: [0016]
layers: [ios]
security_touching: false
manual_steps: [ios-apns-key, ios-push-capability]
sprint: 12
source: T-0311 §3B (the APNs end-to-end-delivery gate)
---

> **OWNER TASK — gates end-to-end push DELIVERY, not the T-0311 code.** T-0311 ships code-complete (the
> registration + token plumbing + lifecycle + the `aps-environment` entitlement are all in-app); registration
> even returns a token in dev. But pushes won't actually be DELIVERED to devices until the two Apple/backend
> provisioning items below are in place. Same pattern as T-0325 (the location plist) gating T-0335.

## What this is

The iOS app registers its APNs device token to `/api/Device/Register` (T-0311). For the backend's push
dispatcher (FCM → APNs bridge, see `docs/architecture/push-notifications.md`) to actually deliver a
notification to an iOS device, Apple requires:

1. **An APNs auth key (`.p8`)** — generated in the Apple Developer account (Keys → Apple Push Notifications
   service) and uploaded to the backend's push provider (the FCM project's iOS app config / the APNs key
   slot). This is what lets the server authenticate to APNs. **Owner-only** (Apple Developer account access).
2. **The Push Notifications capability on the App ID + a push-capable provisioning profile** — enable "Push
   Notifications" on the `cz.cleansia.partner` App ID in the Developer portal, and regenerate the provisioning
   profile so the signed build carries the entitlement. **Owner-only** (provisioning/signing).

The `aps-environment` entitlement declaration in `project.yml` is added by T-0311 (agent) — it only takes
effect once (2) is done.

## Who does what
| Part | Who | Status |
|---|---|---|
| `aps-environment` entitlement + Push capability in `project.yml` | agent (T-0311) | done in T-0311 |
| `PushRegistrar`/`PushSessionObserver`/AppDelegate/register+unregister code | agent (T-0311) | done in T-0311 |
| `Device/Register` POST with `Platform="ios"` | agent (T-0311) | done in T-0311 |
| **APNs auth key (`.p8`) in the backend push provider** | **owner** | this ticket |
| **Push capability on the App ID + push-capable provisioning profile** | **owner** | this ticket |

## Done when
- [ ] The APNs `.p8` key is uploaded to the backend's push provider (FCM iOS config).
- [ ] The `cz.cleansia.partner` App ID has Push Notifications enabled + the provisioning profile regenerated.
- [ ] A test push sent from the backend is delivered to a real iOS device registered via T-0311.

## Notes
- Until this lands, the backend dispatcher safely no-ops for iOS devices (`result.Skipped`, ACKs without
  sending) — no errors, just no delivery (verified in the T-0311 security gate, `security/ios-push.md`).
- No `NSUserNotificationsUsageDescription` is needed (APNs uses the system-provided authorization alert) —
  unlike the camera/location purpose strings.

## Status log
- 2026-06-28 — filed from the T-0311 push gate (§3B). T-0311 lands the registration code + entitlement;
  this is the owner Apple-provisioning gate for end-to-end delivery. Numbered T-0342 (T-0341 is the backend
  status-history flaky-test ticket).
- 2026-07-13 — **owner decided to enroll in the paid Apple Developer Program** (partner device pass: empty devices page + no cleaning-update notifications, both the personal-team APNs wall). ACTIVE. Order: enroll -> create the APNs .p8 key (Keys, +Apple Push Notifications service; note Key ID + Team ID) -> upload it to Firebase Cloud Messaging (APNs Authentication Key) for BOTH iOS bundle IDs -> `git checkout` the two project.yml strips (re-adds aps-environment + SIWA) + regenerate + select the PAID team in Xcode -> rebuild on device. Backend FCM->APNs dispatcher already live (order-status pushes wired). PARTNER works once this lands (already wired); CUSTOMER also needs T-0398.
