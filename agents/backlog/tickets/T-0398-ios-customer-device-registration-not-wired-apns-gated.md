---
id: T-0398
title: "iOS customer — device registration is not wired at all (partner has the full push stack; customer constructs none of it), and even wired it is APNs-gated (paid Apple account required)"
status: proposed
size: M
owner: ios
created: 2026-07-10
updated: 2026-07-10
depends_on: [T-0342]
blocks: []
stories: []
adrs: []
layers: [ios]
security_touching: false
priority: medium
manual_steps:
  - "owner: paid Apple Developer Program + APNs key (same blocker as T-0342/T-0344) before push-token registration can work on any team"
sprint: 12
source: phase/ios-fix2 fix-round-8 slice E investigation (owner remark: the Devices page is empty despite 2 registered devices)
---

> **Root cause of the owner's empty "Your devices" page (fix-round 8, evidence-verified).** The page itself is
> CORRECT — parse, auth, and error-vs-empty handling are all sound (a regression test now pins that an empty
> backend maps to the empty state, not an error). The backend genuinely has zero devices for this user because
> **the iOS customer install never registers its own device**:
>
> 1. **The customer app wires NONE of the Core Push stack.** `PartnerAppContainer` constructs
>    `PushTokenRegistrar` + `PushSessionObserver` + `PartnerDeviceRegistrationClient` + `startPush()` + a
>    `PartnerAppDelegate`; `CustomerAppContainer`/`CleansiaCustomerApp` construct none of these — no
>    `POST /api/Device/Register` ever fires. This is also an Android-parity gap (Android registers the FCM
>    token on sign-in via `CleansiaFirebaseMessagingService` + `PushTokenRepository`).
> 2. **Even wired, registration is APNs-gated.** `PushSessionObserver` fires only when an APNs token arrives,
>    which needs the Push capability — impossible on the owner's personal Apple team (the same paid-account
>    blocker as T-0342/T-0344; the customer entitlements never had `aps-environment` and the local strips
>    remove `CODE_SIGN_ENTITLEMENTS` entirely).
> 3. The owner's "2 registered devices" were most likely Android/web installs whose rows were wiped by the
>    dev-DB resets during this phase.

## Acceptance criteria
- [ ] **AC1 (wire the stack)** — the customer app constructs the push/device-registration chain on login/session
  exactly like the partner app (registrar + session observer + a customer `DeviceRegistrationClient` +
  `startPush()` + app delegate), so once APNs is provisionable the install self-registers and appears in
  "Your devices". Mirror the partner wiring; keep the fail-open behavior when no APNs token ever arrives
  (no crash, no error toast — silent, like today).
- [ ] **AC2 (decision)** — decide whether device registration should ALSO happen WITHOUT an APNs token (a
  device row without a push token still has value for the security "revoke sessions" story — check what the
  backend `Device/Register` contract requires and what Android sends when FCM is unavailable). If the contract
  allows tokenless registration, register on sign-in regardless of APNs so the Devices page works even before
  the paid account; record the decision either way.
- [ ] **AC3 (verify)** — with the paid account + APNs key (owner manual step), a real device registers on
  sign-in and renders on the Devices page; revoke works. Until then, the sim/dev behavior is documented.

## Out of scope
- The partner push stack (already wired).
- Buying the Apple Developer membership (owner; T-0342/T-0344).

## Status log
- 2026-07-10 — filed `proposed` by pm from the fix-round-8 slice-E investigation. The Devices page code was
  verified correct end-to-end (parse sound, auth stamped, error-vs-empty separated, regression test added);
  the gap is the missing customer-side registration wiring + the APNs personal-team limitation.

## Review
<!-- reviewer / qa write verdicts here; PM reconciles before advancing state -->
- 2026-07-13 — owner enrolling in the paid Apple Program (T-0342), unblocking the APNs gate. The CODE half here (wire the customer push/device stack like partner: PushTokenRegistrar + PushSessionObserver + a CustomerDeviceRegistrationClient + startPush() + a CustomerAppDelegate) is now dispatch-ready; it registers-on-login regardless of APNs and starts delivering the customer device row + notifications the moment T-0342's key is in place. Ready to implement on request.
- 2026-07-13 — FOLDED into T-0403 (bb30cd1f): the customer push/device stack is now wired (CustomerAppDelegate + CustomerDeviceRegistrationClient + PushSessionObserver + PushTokenRegistrar + startPush + updatePushSession + pre-logout unregister) and registers an FCM token on session. This ticket's code is DONE via T-0403; close once the owner verifies on-device push.
