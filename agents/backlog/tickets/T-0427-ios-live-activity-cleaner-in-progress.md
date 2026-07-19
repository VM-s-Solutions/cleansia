---
id: T-0427
title: "iOS — Live Activity for an in-progress clean (Wolt/Uber-style lock-screen + Dynamic Island live status)"
status: proposed
size: L
owner: architect
created: 2026-07-16
updated: 2026-07-19
depends_on: [T-0403, T-0404]
blocks: []
stories: []
adrs: [0029]
layers: [architect, ios, backend]
security_touching: false
priority: medium
manual_steps: [ef-migration-fold-initial, customer-mobile-spec-regen, apns-config-binding, customer-project-yml-widget-target, widget-extension-provisioning]
sprint: 12
source: owner remark ("real-time background notification while the cleaner is cleaning, like Wolt/Uber") + remarks-sweep (wf_064232d3)
---

> Owner wants a live, background status while the cleaner works — lock screen + Dynamic Island — like
> Wolt/Uber delivery tracking. **This is a real feature: a second APNs push channel + a new extension target,
> not a toggle.** Needs an ADR before build.

## Scope (ActivityKit)
An order going `InProgress` (event exists) starts a **Live Activity** showing live status (assigned → on the
way → in progress → done), updated in real time and ended on completion.

**Phase A — client:**
- Lift `LiveProgressLogic.swift` into `CleansiaCore` as the shared `ContentState` model.
- Add a **Widget Extension** target to `CleansiaCustomer/project.yml` with `ActivityAttributes` + lock-screen
  and Dynamic Island views; add `NSSupportsLiveActivities`; bump the deployment target to **16.1**.
- Start the Activity on `order.in_progress` (or on `on_the_way`), register its **ActivityKit push token**,
  and end it on completion.

**Phase B — backend (the hard part):**
- A **separate APNs Live-Activity push channel** — ActivityKit updates are APNs pushes to the *activity*
  push token, a distinct payload/topic (`<bundle>.push-type.liveactivity`) from the FCM alert path landed in
  T-0403/T-0404. Decide: send directly via APNs (new APNs client on the Functions host) vs. through FCM's
  APNs bridge (FCM does not natively target Live Activity tokens — likely a direct-APNs path is required).
- Persist the per-order activity push token (new registration endpoint, analogous to Device/Register).
- Drive updates from the same order-status transitions that fan out the alert pushes.

## Decisions for the ADR
- Direct-APNs vs FCM for the live-activity channel (feasibility of FCM targeting activity tokens).
- Which events drive start/update/end; the ContentState shape and the ≤ frequency/battery budget.
- Token lifecycle (per-order, ended/cleaned up on completion; no-PII on the wire).
- Android parity path (Android has no Live Activity equivalent — foreground service / ongoing notification is
  the analogue; scope separately).

## Acceptance criteria
- [ ] **AC0 (ADR)** — architect records the push-channel choice + the ContentState/event contract.
- [ ] **AC1** — starting a clean shows a Live Activity on the lock screen + Dynamic Island; it updates on each
  status change and ends on completion.
- [ ] **AC2** — backend sends the ActivityKit updates reliably from the order-status transitions; token
  registered + cleaned up; no PII on the wire.
- [ ] **AC3** — `dotnet test` + iOS build green; deployment-target bump doesn't break the 16.0 floor policy
  (16.1 for the activity, guarded).

---

# Implementation plan (2026-07-19 — slices LA-1…LA-5, per ADR-0029 `proposed (ratification-ready)`)

> **Gate:** implementation starts only when the owner ratifies ADR-0029 (Q2 timing defaults + Q3 v1
> exclusions — Q1/.p8 custody is resolved by conduct: the key is in backend secret custody and
> confirmed working for ordinary push on live dev). Every slice codes against the ADR's pinned
> contracts; the ADR's "How a reviewer verifies compliance" section is the review checklist.
> Recommended order: **LA-1 → (LA-2 ∥ LA-3) → LA-4**, with **LA-5** in parallel once LA-1's spec
> regen lands. Backend ships **inert** (`APNS:Enabled=false`) ahead of the iOS train.

## LA-1 — `LiveActivityToken` entity + customer-mobile registration surface (backend, **S**)

Deps: ADR-0029 accepted. Blocks: LA-4, LA-5 (spec regen).

- New `Cleansia.Core.Domain/LiveActivities/LiveActivityToken.cs` — `Auditable`, **`ITenantEntity`**;
  `UserId`, `DeviceId` (correlation with `Device.DeviceId`, **not** an FK), `OrderId` (**nullable**:
  null = per-install push-to-start token, non-null = per-activity update token), `Token` (APNs hex),
  `LastUpdatedAt`. EF config in `Cleansia.Infra.Database` with unique `(UserId, DeviceId, OrderId)`.
- `ILiveActivityTokenRepository` + implementation (by-order, by-device, upsert, delete helpers).
- Commands (CQRS, happy-path handlers only): `RegisterLiveActivityToken` (**upsert** — ActivityKit
  rotates tokens; last write wins) and `UnregisterLiveActivity` (order-scoped delete for the
  caller's device — the user-dismissed path).
- `LiveActivityController` on **`Cleansia.Web.Mobile.Customer` only** (per-audience host seam):
  `POST /api/LiveActivity/Register` `{ deviceId, token, orderId? }`,
  `DELETE /api/LiveActivity/{orderId}?deviceId=` — `[Permission(Policy.Authenticated)]` +
  `[EnableRateLimiting("auth")]`, the `DeviceController` archetype verbatim.
- Validators (FluentValidation, `Cascade.Stop`, **never** the handler): deviceId + token non-empty;
  when `orderId` present → order **belongs to the caller** and
  `CurrentStatus ∈ {Confirmed, OnTheWay, InProgress}`.
- Cleanup cascade: `RevokeDevice` / logout delete the device's `LiveActivityToken` rows
  (push-to-start included) — ADR-0026 revocation intent, S11 analog.

**AC**
- [ ] Entity + EF config + unique index as pinned; `ITenantEntity` covered by the global filter (S8).
- [ ] Register upserts on token rotation (same `(user, device, orderId)` → one row, new token).
- [ ] Foreign order → rejected in the **validator**; terminal-status order → rejected (S1/S3).
- [ ] `UserId` is taken from the session, never from the request body (S1).
- [ ] RevokeDevice/logout cascades the device's rows; `Device`/`RegisterDevice` byte-unchanged.
- [ ] `dotnet test` green.

**Security (S1–S11):** S1 caller-truth UserId; S2 `Policy.Authenticated` on both endpoints; S3
ownership check in validator; S5 `auth` rate-limit partition; S8 tenant filter (no hand-rolled
scoping); S10 note — rows are hours-lived *operational* addressing data, **hard-delete** (not the
ADR-0007 soft-delete domain surface; recorded deliberately); S11 session-end cascade.

**Tests (TC-LA-5):** upsert-on-rotation; foreign-order reject; terminal-status reject; revoke/logout
cascade; tenant-filter coverage test.

**MANUAL_STEP (owner):** EF migration — pre-prod rule: fold into the single Initial migration
(startup host `Cleansia.Web.Partner`); regenerate the customer-mobile OpenAPI spec + iOS client.

## LA-2 — `live-activity-dispatch` queue + producer seam (backend, **S**)

Deps: ADR-0029 accepted (schema consumed by LA-4; can land before or with LA-3).

- `QueueNames.LiveActivityDispatch = "live-activity-dispatch"`.
- `SendLiveActivityUpdateMessage` (`Cleansia.Core.Queue.Abstractions/Messages/`): `UserId`,
  `OrderId`, `EventKey`, `OrderNumber`, `ScheduledStart`, `ScheduledEnd`, `TransitionAtUtc`
  (→ the APNs `timestamp`), `TenantId`. **No names, no addresses, no free text** (S6 starts at the
  message, not the payload builder).
- `MessageKeys.LiveActivity(orderId, eventKey, sequence)` → `liveactivity:{orderId}:{eventKey}:{sequence}`
  — an **added** frozen D2.1 formula (pure function; `sequence` = `OrderStatusTrack.Sequence`,
  defensive per the ADR's corrected rationale).
- `ILiveActivityProducer` + `LiveActivityProducer` (`Cleansia.Core.AppServices/Services/`,
  holds `IPendingDispatch`) — the `NotificationProducer` archetype; the **only** construction site
  of the message (raw-file tripwire test, `SendPushNotificationSeamTripwireTests` pattern).
- Producer calls in **six** handlers: `NotifyOnTheWay` (start), `StartOrder` (update),
  `CompleteOrder` (end), `CancelOrder` + `AdminCancelOrder` (end — **outside** the refund
  conditional, beside `AddOrderStatus`), `AdminOverrideOrderStatus` (event mapped from
  `TargetStatus`; its **first** producer call — forward moves only, Cancelled unreachable there).

**AC**
- [ ] Exactly the six sites call the producer; no handler branches on country/platform/token
  existence; cancel-path calls are unconditional.
- [ ] Tripwire test: `new SendLiveActivityUpdateMessage(` only in `LiveActivityProducer.cs` +
  the record's own file (+ test projects).
- [ ] `INotificationProducer`/`NotificationProducer` + its tripwire byte-unchanged.
- [ ] Key formula pure + pinned by test (same inputs ⇒ same key; distinct sequence ⇒ distinct key).
- [ ] `dotnet test` green.

**Security:** S6 message-field allowlist (test-pinned); S7 deterministic key (no Guid/timestamp).

**Tests:** key purity (MessageKeyTests addition); per-handler enqueue characterization (envelope on
the right queue with the right key per transition); tripwire.

## LA-3 — direct-APNs client behind `ILiveActivityPushClient` (backend, **M**)

Deps: ADR-0029 accepted. Independent of LA-1/LA-2.

- `IApnsConfig` (`Cleansia.Infra.Common/Configuration/Interfaces/`) + `ApnsConfig :
  AutoBindConfig(configuration, "APNS")` + registration in
  `Cleansia.Config/Configurations/ConfigurationExtensions.AddConfigurationBindings` — keys exactly
  as pinned in ADR-0029 D1: `APNS:Enabled` (default false), `APNS:KeyId`, `APNS:TeamId`,
  `APNS:PrivateKeyPem` (raw or base64, the `FCM:ServiceAccountJson` dual-accept), 
  `APNS:CustomerBundleId`, `APNS:UseSandbox`.
- Port `Cleansia.Core.Clients.Abstractions/Apns/ILiveActivityPushClient.cs` + a result type
  mirroring `PushDispatchResult` (`Skipped` signal + permanent-invalid-token signal).
- `ApnsLiveActivityClient` (`Cleansia.Infra.Clients/Apns/`): named `IHttpClientFactory` client,
  pooled `SocketsHttpHandler`, `HttpVersion.Version20`; ES256 JWT minted from `PrivateKeyPem`,
  cached ~50 min; `POST /3/device/{token}` with `apns-push-type: liveactivity`,
  `apns-topic: {CustomerBundleId}.push-type.liveactivity`; host by `UseSandbox`. Failure taxonomy
  (ADR-0005 vocabulary): `410 Unregistered`/`400 BadDeviceToken` → permanent (prune signal);
  `403 Expired/InvalidProviderToken` → re-mint once, then transient; 429/5xx/network → transient
  (throw); `Enabled=false` or empty key material → **Skipped** (+ one startup `LogWarning` when
  enabled-but-keyless). Library vs hand-rolled ES256/HTTP2 = implementation choice inside this
  contract (ADR CH-D1-2).

**AC**
- [ ] Port + config + DI exactly at the pinned placements; no other site constructs an APNs call.
- [ ] Taxonomy behavior test-pinned (TC-LA-3 client half); JWT re-mint happens at most once per send.
- [ ] `.p8`/JWT never logged, never in exception messages (S6).
- [ ] `FcmPushDispatcher`/`IPushDispatcher` byte-unchanged.
- [ ] `dotnet test` green.

**Security:** S6 secret-logging hygiene (test with a fake key asserts log output); key material only
via config (no file paths, no checked-in fixtures with real keys).

**Tests:** classification matrix (410/400/403-once/429/5xx); skipped-when-disabled;
skipped-when-keyless+enabled logs warning once; JWT cache expiry re-mint; topic/host derivation.

## LA-4 — `SendLiveActivityUpdateHandler` consumer + poison pair + janitor (backend, **M**)

Deps: **LA-1, LA-2, LA-3.**

- `SendLiveActivityUpdateHandler` (`Cleansia.Functions.Core/Handlers/`) + thin
  `SendLiveActivityUpdateFunction` `[QueueTrigger("live-activity-dispatch")]` shell +
  `LiveActivityDispatchPoisonHandler` + `[QueueTrigger("live-activity-dispatch-poison")]` shell
  (DeadLetter row + LogError + ack — `NotificationsDispatchPoisonHandler` archetype); DI in
  `Functions/Program.cs`.
- **Mode A claim-first** on `MessageKeys.LiveActivity(...)`; envelope-only read (new queue — no
  pre-envelope traffic exists, so **no D2.1a dual-read**; recorded deliberately). Malformed body →
  Warning + ack (permanent), body size only, never content (S6).
- Tenant override via `ITenantProvider` (queue trigger has no JWT) — the
  `SendPushNotificationHandler` pattern (S8).
- Payload builder per ADR D2/D4: `content-state {v:1, status, orderNumber, scheduledStart,
  scheduledEnd}`; `event` start/update/end from `EventKey`; `timestamp = TransitionAtUtc`;
  `stale-date = max(now + 4h, scheduledEnd + 1h)`; `dismissal-date` = +30 min (completed) / now
  (cancelled); start events target the push-to-start rows (`OrderId is null`), update/end events
  the order rows.
- Zero token rows → info-log no-op ack; permanent rejections prune the row; **successful terminal
  send deletes the order's rows**; `Skipped` → ack, rows untouched; transient → throw (redelivery).
- Janitor: `LiveActivityTokenJanitorHandler` on the existing Functions **timer** host (daily) —
  deletes **order-scoped** rows older than 24 h (the lost-terminal-update backstop).

**AC**
- [ ] TC-LA-0/1/2/3(consumer)/4/6 red-first and green (payload-per-event, stale-date boundaries,
  claim idempotency, taxonomy handling, terminal cleanup + janitor scope, S6 allowlist pin over the
  builder).
- [ ] Shared JSON content-state fixture committed + asserted backend-side (iOS asserts it in LA-5).
- [ ] `SendPushNotificationHandler` byte-unchanged; separate claim keyspace/poison queue verified.
- [ ] `dotnet test` green.

**Security:** S6 allowlist test (union of builder output fields ⊆ `{v, status, orderNumber,
scheduledStart, scheduledEnd}`) + no-raw-body logging; S7 claim-first idempotency; S8 tenant
override, no cross-tenant leak in token resolution (rows are tenant-filtered).

## LA-5 — iOS: Widget Extension + ActivityKit client (ios, **L**)

Deps: ADR-0029 accepted; LA-1 (endpoint in the regenerated `CleansiaCustomerApi`);
owner MANUAL_STEPs below. Backend LA-2..LA-4 NOT required to start (activity runs locally until the
channel is enabled).

- **MANUAL_STEP (owner, verbatim diff):** `CleansiaCustomer/project.yml` is owner-modified working
  tree (carries the owner's Stripe key; agents never checkout/stage it). The implementing agent
  writes the exact diff in the PR description / a `.patch` alongside; **the owner applies it**, runs
  `xcodegen generate`, and signs the new target (automatic signing, paid team). The diff adds:
  1. App target `info.properties`: `NSSupportsLiveActivities: true`.
  2. New target `CleansiaCustomerWidgets`: `type: app-extension`, `platform: iOS`,
     `deploymentTarget: "16.1"` (an embedded extension may floor above its 16.0 host; it simply
     never loads on 16.0), `PRODUCT_BUNDLE_IDENTIFIER: cz.cleansia.customer.widgets`,
     `info.properties.NSExtension.NSExtensionPointIdentifier: com.apple.widgetkit-extension`,
     sources `WidgetSources/` + the shared `Sources/LiveActivity/` folder, `SWIFT_EMIT_LOC_STRINGS: NO`.
  3. App target `dependencies`: `- target: CleansiaCustomerWidgets` (embedded, `codeSign: true`).
- Shared `Sources/LiveActivity/CleanOrderAttributes.swift` — `ActivityAttributes` +
  `ContentState: Codable` decoding `status` as **string** with a generic in-service fallback for
  unknown values (additive-evolution pin). Compiled into **both** targets via project.yml source
  sharing — deliberately NOT into `CleansiaCore` (partner shares that package; customer-only v1).
- Widget bundle: lock-screen card + Dynamic Island (compact/minimal/expanded), `isStale` → "status
  may be outdated — open the app" presentation; **5-locale strings in the extension's own catalog**.
- App side `LiveActivityCoordinator` (all call sites availability-gated, floor stays 16.0):
  `Activity.pushToStartTokenUpdates` (17.2+, observed at launch) → register `orderId = null`;
  `activityUpdates` + per-activity `pushTokenUpdates` → register with `orderId`; local-start
  fallback on foreground while status ∈ {OnTheWay, InProgress} (16.1–17.1); **one activity per
  order** checked via `Activity<CleanOrderAttributes>.activities`; user-dismissed → `DELETE
  /api/LiveActivity/{orderId}`; sign-out ends all activities + registrations (S11).

**AC**
- [ ] TC-LA-7: the shared JSON fixture decodes into `ContentState`; unknown `status` renders the
  fallback presentation (unit-tested).
- [ ] All ActivityKit call sites `#available(iOS 16.1/16.2/17.2)`-gated; deployment target of the
  app still 16.0; both schemes build; customer tests green at the CI pins.
- [ ] One activity per order enforced; dismissal calls the DELETE endpoint; sign-out cleans up.
- [ ] QA device matrix (manual, per ADR §15): 17.2+ terminated-app remote start on OnTheWay;
  update on InProgress; completed lingers ~30 min; cancelled dismisses immediately; Island renders;
  16.x foreground fallback.
- [ ] Widget strings ×5 locales in the extension catalog; no raw user-visible strings.

**Security:** S6 — the card renders only the allowlisted fields (no names/addresses ever reach the
widget); S11 — session end tears down activities, token registrations, and local state.

**MANUAL_STEPs (owner, consolidated):** (1) apply the project.yml diff + xcodegen + extension
signing; (2) bind `APNS:KeyId/TeamId/PrivateKeyPem/CustomerBundleId/UseSandbox` from the existing
backend-custody `.p8` and flip `APNS:Enabled=true` when LA-4 + LA-5 are both deployed; (3) EF
migration fold + customer-mobile spec regen (from LA-1).

## Status log
- 2026-07-16 — filed from the remarks-sweep; large, ADR-gated. Depends on the FCM/APNs push foundation
  (T-0403/T-0404).
- 2026-07-19 — **architecture hardened + implementation plan ready.** ADR-0029 re-verified against
  the shipped code (producer-seam consolidation `INotificationProducer` + tripwire;
  `AdminOverrideOrderStatus` forward-only/no-Cancelled/no-notify reality; refund-conditional cancel
  alerts) and corrected; the `.p8` gate resolved by conduct (key uploaded, backend secret custody,
  ordinary push confirmed working on live dev — same key signs `liveactivity`; residual =
  `APNS:*` binding). ADR status → `proposed (ratification-ready)`; owner ratifies Q2 (timing
  defaults) + Q3 (v1 exclusions). Slices LA-1…LA-5 specced above with AC/size/deps/security/tests +
  MANUAL_STEPs (project.yml diff is owner-applied — customer project.yml is owner-modified working
  tree). Implementation starts on ratification.
