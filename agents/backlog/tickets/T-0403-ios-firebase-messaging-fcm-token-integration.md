---
id: T-0403
title: "iOS — integrate Firebase Messaging so the apps register an FCM token (backend dispatches via FCM; iOS currently sends a raw APNs token FCM can't target)"
status: proposed
size: M
owner: ios
created: 2026-07-13
updated: 2026-07-13
depends_on: [T-0342]
blocks: []
stories: []
adrs: [0016]
layers: [ios]
security_touching: false
priority: high
manual_steps:
  - "owner: add iOS apps (cz.cleansia.customer, cz.cleansia.partner) to the Firebase project + download each GoogleService-Info.plist"
sprint: 12
source: phase/ios-fix3 push investigation — the FCM-token/raw-APNs mismatch surfaced while navigating the owner through the APNs key + Firebase upload
---

> **Architecture gap found while enabling push (owner enrolled in the paid Apple Program).** The backend
> dispatches via `FcmPushDispatcher.SendEachForMulticastAsync` with `MulticastMessage.Tokens` — these are
> **FCM registration tokens** (Android provides them via the Firebase SDK). But the iOS apps have **no
> Firebase SDK**; `UNUserNotificationPushRegistrar`/`ApnsToken` produce a **raw APNs device token** (hex) and
> `PushTokenRegistrar` registers THAT to `/api/Device/Register`. FCM will reject a raw APNs token as an
> invalid/dead token, so iOS push cannot deliver even after the APNs .p8 is uploaded to Firebase (T-0342) and
> the push capability is provisioned. The APNs key upload is still required — it's what lets FCM's APNs bridge
> reach the device — but the token the app registers must be an FCM token.

## Acceptance criteria
- [ ] **AC1** — add the FirebaseCore + FirebaseMessaging SPM packages to BOTH iOS apps; add each
  `GoogleService-Info.plist` (owner downloads from the Firebase iOS apps); configure Firebase at launch.
- [ ] **AC2** — the push registrar obtains the **FCM registration token** (`Messaging.messaging().token` /
  the `messaging(_:didReceiveRegistrationToken:)` delegate, set the APNs token on Messaging) and registers
  THAT to `/api/Device/Register` (replacing the raw-APNs-hex path); token refresh re-registers. Keep the
  existing DeviceId + platform "ios".
- [ ] **AC3 (customer wiring — folds T-0398)** — the customer app, which wires none of the push stack, gets
  the full chain (registrar + session observer + registration client + startPush + app delegate) so it also
  registers an FCM token on login; partner keeps its wiring but switches to the FCM token.
- [ ] **AC4** — with the paid team + the APNs key in Firebase, a real device registers an FCM token, a Console
  test push (doc §6) arrives, and an order-status change (NotifyOnTheWay/Start/Complete) delivers to the
  device. Both apps build on the iOS-16 floor.

## Out of scope
- The APNs .p8 creation + Firebase upload (owner, T-0342).
- Buying the membership (done).

## Status log
- 2026-07-13 — filed `high` from the push-enablement investigation. Corrects the earlier assumption that iOS
  push was code-complete pending only provisioning — the FCM-token integration is the missing code layer;
  absorbs the T-0398 customer-wiring code.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
