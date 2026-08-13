---
id: T-0404
title: "iOS — order-status pushes never surface: backend sends data-only FCM (no APNs alert) and iOS has no receive-side handler"
status: done
size: M
owner: architect
created: 2026-07-14
updated: 2026-07-19
depends_on: [T-0403]
blocks: []
stories: []
adrs: []
layers: [architect, backend, ios]
security_touching: true
priority: high
manual_steps: []
sprint: 12
source: phase/ios-fix3 push-chain adversarial audit (wf_a1afcd54-252) — confirmed end-to-end against source
---

> **Found by the push-chain audit (T-0403 follow-up).** Once a device registers (provisioning fixed +
> re-registration collision fixed in the T-0403 batch), iOS still shows **nothing** for real order-status
> pushes. The backend deliberately sends **data-only** FCM messages and lets each client render the
> notification locally ("no PII shipped to FCM"). Android does that in `CleansiaFirebaseMessagingService.
> onMessageReceived`. **iOS has no equivalent receive-side at all**, and a data-only FCM message does not
> wake or display on iOS. So iOS registers a token, is included in the fan-out, and receives a payload it
> cannot surface — foreground OR background.

## Evidence (verified against source)
- **Backend payload is data-only.** `FcmPushDispatcher.cs:64-75` builds `MulticastMessage { Tokens, Data =
  payload, Android = new AndroidConfig { Priority = High } }` — **no `Notification`, no `Apns`/`Aps`, no
  `content-available`**. `SendAsync(tokens, eventKey, data, ct)` has no per-platform config parameter, and
  it is the only registered `IPushDispatcher`.
- **iOS is a live target, not filtered out.** `DeviceRepository.GetByUserIdAsync` filters only `UserId &&
  IsActive`; `SendPushNotificationHandler` filters only `NotificationsEnabled && token != empty`.
  `Device.Platform` is never used to branch. iOS FCM tokens go into the multicast array.
- **iOS has zero receive-side.** grep across all iOS Swift: **no** `didReceiveRemoteNotification`, **no**
  `UNMutableNotificationContent`/`UNNotificationRequest`, **no** Notification Service Extension, **no**
  `UIBackgroundModes` in Info.plist. `CustomerAppDelegate` implements only `willPresent` (fires only for
  *alert*-type notifications, which these data-only messages are not) and token registration.
- **Net runtime symptom:** Android delivery works (once `FCM:ServiceAccountJson` is set); iOS delivery is a
  silent no-op even with a healthy token pipeline. This is a code gap, **not** the provisioning gap.

> **Note on the Firebase Console test push:** a console test sends a `notification` payload (alert), so it
> *may* display on iOS in the foreground via `willPresent`. That does **not** validate the real path — the
> five order-status events are data-only and will not surface. Do not treat a green console test as "iOS
> push works".

## The decision to make (architect)
The Android design is "data-only + client renders from an eventKey template, no user text on the wire".
iOS cannot mirror that for background/terminated delivery without either an APNs alert or a Notification
Service Extension (and even an NSE requires an alert payload with `mutable-content` to run). Pick one:

- **Option A — per-platform APNs alert block (recommended).** In the dispatcher, when the fan-out includes
  iOS tokens, attach an `ApnsConfig` carrying a localized `aps.alert` (title/body resolved server-side from
  the same eventKey template Android uses) plus `sound`. Keeps Android data-only; adds an alert only for
  Apple. Simplest path to parity, no iOS app code required for display. **Trade-off:** a short localized
  status string ("Your cleaner is on the way") now crosses FCM for iOS — decide whether that violates the
  no-PII stance (these strings carry no customer PII; likely acceptable — record in an ADR).
- **Option B — data-only + iOS Notification Service Extension.** Send `content-available` + `mutable-content`
  and a minimal alert; an NSE localizes/renders client-side to preserve the no-text-on-wire stance. More
  moving parts (new target, entitlement, app-group), and still needs a visible alert to launch the NSE.

Whichever is chosen, iOS also needs the **tap → deep-link** path wired to the existing `PushNavigationModel`
/ `appDelegate.onTap` (partner already has the `onTap` seam; confirm both apps route
`didReceive response` → destination).

## Acceptance criteria
- [ ] **AC1 (ADR)** — architect records the chosen option and the no-PII trade-off in a short ADR; the
  per-platform payload contract (what Apple vs Android receive) is written down.
- [ ] **AC2 (backend)** — an installed iOS customer receives an order-status push that **displays** on the
  lock screen / banner in background AND terminated states for all five events (on_the_way, in_progress,
  completed, confirmed, refunded). Android behavior is unchanged (regression-checked).
- [ ] **AC3 (backend)** — the title/body are correctly localized to the device/user locale via the existing
  template mechanism; no raw eventKey leaks to the UI.
- [ ] **AC4 (iOS)** — tapping the notification deep-links to the relevant order (routes through
  `PushNavigationModel`); foreground receipt is handled gracefully (no duplicate/omitted display).
- [ ] **AC5** — `dotnet test` green; iOS builds on the 16.4 floor; no new secret committed.

## Status log
- 2026-07-14 — filed `proposed` from the push-chain adversarial audit (wf_a1afcd54-252). Blocker sibling
  (logout→login re-registration collision) fixed in the same T-0403 push batch; this ticket covers the
  remaining iOS *display* gap.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-19 — **frontmatter reconciled to reality (proposed → done)** — code shipped `e956529e`+`d937de0f` (ADR-0025 accepted, loc-key APNs alert path); live push remains gated on T-0342 (.p8, owner).
