---
id: T-0427
title: "iOS — Live Activity for an in-progress clean (Wolt/Uber-style lock-screen + Dynamic Island live status)"
status: in_progress
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
- [x] Entity + EF config + unique index as pinned; `ITenantEntity` covered by the global filter (S8).
- [x] Register upserts on token rotation (same `(user, device, orderId)` → one row, new token).
- [x] Foreign order → rejected in the **validator**; terminal-status order → rejected (S1/S3).
- [x] `UserId` is taken from the session, never from the request body (S1).
- [x] RevokeDevice/logout cascades the device's rows; `Device`/`RegisterDevice` byte-unchanged.
- [x] `dotnet test` green.

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
- [x] Exactly the six sites call the producer; no handler branches on country/platform/token
  existence; cancel-path calls are unconditional. (The token gate lives in the **producer**, not the
  handlers — see the deviation note in the Status log.)
- [x] Tripwire test: `new SendLiveActivityUpdateMessage(` only in `LiveActivityProducer.cs` +
  the record's own file (+ test projects).
- [x] `INotificationProducer`/`NotificationProducer` + its tripwire byte-unchanged.
- [x] Key formula pure + pinned by test (same inputs ⇒ same key; distinct sequence ⇒ distinct key).
- [x] `dotnet test` green.

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
- [x] Port + config + DI exactly at the pinned placements; no other site constructs an APNs call.
- [x] Taxonomy behavior test-pinned (TC-LA-3 client half); JWT re-mint happens at most once per send.
- [x] `.p8`/JWT never logged, never in exception messages (S6).
- [x] `FcmPushDispatcher`/`IPushDispatcher` byte-unchanged.
- [x] `dotnet test` green.

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
- [x] TC-LA-0/1/2/3(consumer)/4/6 red-first and green (payload-per-event, stale-date boundaries,
  claim idempotency, taxonomy handling, terminal cleanup + janitor scope, S6 allowlist pin over the
  builder).
- [x] Shared JSON content-state fixture committed + asserted backend-side (iOS asserts it in LA-5).
- [x] `SendPushNotificationHandler` byte-unchanged; separate claim keyspace/poison queue verified.
- [x] `dotnet test` green.

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
- 2026-07-19 — **owner RATIFIED ADR-0029 2026-07-19 — execution started.** Ratified defaults: OnTheWay=start / InProgress=update / terminal=end (+30-min Completed linger, immediate Cancelled dismiss), customer-app-only v1. Backend LA-1..LA-4 in flight (ships inert behind APNS:Enabled=false); iOS LA-5 widget gated on the owner's project.yml widget-target step + spec regen.
- 2026-07-19 — **LA-1 + LA-2 landed on `feature/payroll-invoice-paid-notify` (backend).** All ACs
  above ticked; `dotnet build src/Cleansia.Api.sln` clean; `dotnet test src/Cleansia.Tests` fully
  green (2058 passed / 0 failed, including the new suites). Not committed (owner commits).
  - **LA-1** — `Cleansia.Core.Domain/LiveActivities/LiveActivityToken.cs` (Auditable + ITenantEntity;
    `UserId`, `DeviceId` correlation-not-FK, nullable `OrderId`, `Token`, `LastUpdatedAt`; `Create` +
    `Refresh` upsert). EF config `LiveActivityTokenConfiguration` (table `LiveActivityTokens`, unique
    `(UserId, DeviceId, OrderId)` **NULLS NOT DISTINCT** so the push-to-start row is one-per-device,
    `(UserId, OrderId)` lookup index) + `DbSet` added to `CleansiaDbContext`.
    `ILiveActivityTokenRepository` + impl (auto-registered by the `IRepository<,>` scan). Commands
    `RegisterLiveActivityToken` (upsert; validator: deviceId+token required, order ownership + status ∈
    {Confirmed,OnTheWay,InProgress}, S3 foreign→OrderNotFound) and `UnregisterLiveActivity`
    (order-scoped delete). `LiveActivityController` on **Web.Mobile.Customer** only
    (`POST /api/LiveActivity/Register`, `DELETE /api/LiveActivity/{orderId}?deviceId=`;
    `[Permission(Policy.Authenticated)]` + `[EnableRateLimiting("auth")]`, DeviceController archetype).
    **Revoke cascade** hooked into BOTH device-deactivation sites (`RevokeDevice` + `UnregisterDevice`)
    — hard-deletes the device's token rows (push-to-start included). `Device`/`RegisterDevice`
    byte-unchanged. New error key `live_activity.order_not_active` (needs the 5-locale i18n keys —
    frontend/L10n).
  - **LA-2** — `QueueNames.LiveActivityDispatch`, `SendLiveActivityUpdateMessage`,
    `MessageKeys.LiveActivity(orderId, eventKey, sequence)`, `LiveActivityEventKeys` (start/update/end +
    `ForStatus`). `ILiveActivityProducer` + `LiveActivityProducer` (sibling to `INotificationProducer`,
    holds `IPendingDispatch` + the token repo; enqueues on the caller's UoW, no CommitAsync). Wired the
    **six** sites: `NotifyOnTheWay`(start), `StartOrder`(update), `CompleteOrder`(end), `CancelOrder` +
    `AdminCancelOrder`(end, **outside** the refund conditional), `AdminOverrideOrderStatus`(its first
    producer call, event mapped from `TargetStatus`, forward-only). Second raw-file tripwire pins the
    single construction site. `INotificationProducer`/`NotificationProducer` + their tripwire
    byte-unchanged.
  - **DEVIATION from ADR-0029 D2 (recorded for the reviewer):** the ADR says producers do NOT check
    token existence and the LA-4 consumer no-ops on zero rows. Per the execution instruction ("gate each
    on the order having a registered token so orders without an iOS activity produce nothing"), the
    **producer** performs a cheap `HasTokensForOrderAsync(userId, orderId)` existence check
    (`OrderId == orderId OR OrderId == null`) and skips the enqueue when none. The **handlers stay
    branch-free** (they call the producer unconditionally), so the ADR's reviewer-compliance item "no
    handler branches on token existence" holds; the gate simply lives one layer down in the producer
    seam. Net effect: orders with no iOS activity registration produce no queue traffic even before LA-4
    ships. If the Architect prefers the strict ADR shape (producer always enqueues, consumer gates),
    delete the `HasTokensForOrderAsync` guard — it is the only line that differs.
  - **MANUAL_STEP (owner) — EF migration:** `LiveActivityToken` needs a schema migration. Per the
    pre-prod rule, **fold `LiveActivityTokens` into the single Initial migration**
    (`src/Cleansia.Infra.Database/Migrations/…_Initial.cs`; startup host `Cleansia.Web.Partner`) — the
    entity + config compile and the model builds, but the table is not in the migration until the owner
    regenerates/folds it. Also flagged: **customer-mobile OpenAPI spec regen** (new `LiveActivity`
    endpoints) for the iOS client (LA-5).
  - **Left for LA-3/LA-4:** the direct-APNs client (`ILiveActivityPushClient` + `ApnsLiveActivityClient`
    + `IApnsConfig`/`ApnsConfig` + `APNS:*` binding) and the `SendLiveActivityUpdateHandler` consumer +
    poison pair + 24h janitor + the content-state payload builder (TC-LA-0/1/6 + the shared JSON
    fixture). LA-2 only enqueues; nothing consumes `live-activity-dispatch` yet.
- 2026-07-20 — **LA-3 + LA-4 landed on `feature/payroll-invoice-paid-notify` (backend). The pipeline
  now ships INERT end-to-end behind `APNS:Enabled=false`.** `dotnet build src/Cleansia.Api.sln` clean;
  `dotnet test src/Cleansia.Tests` fully green (**2105 passed / 0 failed** — baseline 2058 + 47 new);
  bicep compiles + dev byte-invariant (machine-checked). Not committed (owner commits).
  - **LA-3** — `IApnsConfig` + `ApnsConfig : AutoBindConfig("APNS")` (Infra.Common; six keys — `Enabled`
    default false, `KeyId`, `TeamId`, `PrivateKeyPem` raw-or-base64 dual-accept, `CustomerBundleId`,
    `UseSandbox`), registered in `ConfigurationExtensions.AddConfigurationBindings`. Port
    `Cleansia.Core.Clients.Abstractions/Apns/ILiveActivityPushClient.cs` + `LiveActivityPush` /
    `LiveActivityContentState` (the S6 allowlist enforced STRUCTURALLY — the record carries exactly
    `{v,status,orderNumber,scheduledStart,scheduledEnd}`) + `LiveActivityPushResult`. `ApnsJwtProvider`
    (BCL `ECDsa` ES256, no NuGet; cached ~50 min via injected `TimeProvider`; `Invalidate` on 403) +
    `ApnsLiveActivityClient` (`Cleansia.Infra.Clients/Apns/`): named pooled `IHttpClientFactory` client
    (`"Apns"`, HTTP/2, standard resilience handler), `POST /3/device/{token}` with
    `apns-push-type: liveactivity` + derived `{CustomerBundleId}.push-type.liveactivity` topic + sandbox
    host by `UseSandbox`. Taxonomy: `Enabled=false`/keyless → **Skipped without a socket** (never opens
    one); 410/`400 BadDeviceToken` → prune; 403 → re-mint once then transient; 429/5xx/network → throw.
    Wired via a new `AddApns()` beside `AddFcm()` in `AddCoreBindings`. `FcmPushDispatcher`/`IPushDispatcher`
    byte-unchanged.
  - **LA-4** — `SendLiveActivityUpdateHandler` (`Functions.Core/Handlers/`) — Mode-A claim-first on
    `MessageKeys.LiveActivity(...)`, **envelope-ONLY read** (new queue, no pre-envelope traffic AND the
    claim key carries the transition `Sequence` the bare payload cannot supply — so no D2.1a dual-read to
    synthesize; recorded deliberately), tenant override, S6 logging (bytes only, never the body). Start
    targets the push-to-start rows (`OrderId == null`), update/end the order rows; a terminal `end`
    hard-deletes the order's rows; dead tokens pruned; `Skipped`/no-token → info + ack. Pure
    `LiveActivityPayloadFactory` (content-state `{v:1,status,orderNumber,scheduledStart,scheduledEnd}`,
    stale-date `max(now+4h, scheduledEnd+1h)`, dismissal +30 min completed / now cancelled, start carries
    `attributes-type`+`attributes`). `LiveActivityDispatchPoisonHandler` (DeadLetter + LogError + ack) +
    `LiveActivityJanitorTimerHandler` (daily 04:00 UTC; reclaims order-scoped rows older than 24 h,
    cross-tenant `IgnoreQueryFilters`; push-to-start rows never swept). Three trigger shells in
    `Cleansia.Functions/Functions/`; all three handlers registered in
    `FunctionsProcessingRegistration.AddFunctionsProcessing()` (the `FunctionsHostStartupGuardTests` now
    auto-cover them — green). Repo query methods added to `ILiveActivityTokenRepository` (by-order,
    push-to-start, stale-order-scoped). New repo methods only — no schema/DbContext change.
  - **Bicep** — `apnsSecretProvisioned` param (default false) + `apnsSettings` gate on the Functions host
    (`functionApp` extraAppSettings), the `fcmSecretProvisioned` precedent verbatim. Default false emits
    `{}` (union no-op) → dev Functions host **byte-unchanged**, app binds `Enabled=false` by C# default →
    channel INERT. True → KV refs `Apns--KeyId`/`Apns--TeamId`/`Apns--PrivateKeyPem` + literal
    `CustomerBundleId=cz.cleansia.customer` + `UseSandbox` (dev true / prod false) + `APNS__Enabled=true`.
    Machine-checked: compiled ARM diff (dependsOn-set-order ignored) is exactly 3 leaves — the new param,
    the functionApp union expr, the templateHash; removing the single APNS union arg yields byte-identical
    to the pre-change functionApp settings.
  - **DEVIATION (recorded for the reviewer):** (a) the ADR D2 shape has the producer NOT check tokens and
    the consumer no-op on zero rows; LA-2's landed producer already gates on `HasTokensForOrderAsync`
    (its own recorded deviation), so this consumer ALSO no-ops on zero rows (belt-and-suspenders, per ADR).
    (b) **Bicep single-gate:** the ADR Dependencies §1 describes a two-step owner flow (bind the `APNS:*`
    keys with `Enabled=false`, enable later). App Service bicep REPLACES the whole appSettings array on
    every deploy, so a manually-flipped `APNS__Enabled` would be wiped on the next `az deployment` — the
    enable MUST be param-driven. With the single `apnsSecretProvisioned` gate the task scoped, provisioning
    and enabling collapse into that one deliberate flip (dev stays false → inert; the owner flips it in the
    `.bicepparam` only once the three secrets exist AND iOS LA-5 ships). The ADR's inert intent holds — dev
    is never enabled — but the bind-then-enable interim is not separately reachable via this gate.
  - **MANUAL_STEP (owner):** seed the three KV secrets `Apns--KeyId`/`Apns--TeamId`/`Apns--PrivateKeyPem`
    from the **SAME .p8 already in backend custody** for ordinary push (no new key), then flip
    `apnsSecretProvisioned=true` in the `.bicepparam` when LA-4 + LA-5 are both deployed. Still gated:
    iOS LA-5 widget + the owner's `CleansiaCustomer/project.yml` widget-target step + customer-mobile spec
    regen + the `LiveActivityTokens` migration fold (from LA-1).
- 2026-07-20 — **LA-3/LA-4 review fixes applied (5).** (1) Deleted the dead
  `LiveActivityJanitorPolicy.IsStale` (zero production callers) + its predicate tests; added a REAL
  repository test that runs `GetStaleOrderScopedTokensAsync` against an actual SQLite `CleansiaDbContext`
  (push-to-start + past-cutoff + recent rows → returns ONLY the order-scoped past-cutoff row), pinning the
  `OrderId != null` exclusion on the path the janitor actually executes. (2) An unparseable APNS key (an
  unresolved `@Microsoft.KeyVault(SecretUri=…)` literal that clears the non-empty guard) now degrades to
  **Skipped** via a new `IApnsJwtProvider.HasUsableKey()` the client checks in its inert guard — no
  `FormatException` out of `GetToken()`, so no `live-activity-dispatch-poison` storm; one S6-safe Warning,
  no socket (test added, real provider). (3) Recorded the kept producer-gate deviation + two more
  refinements as **ADR-0029 Amendment A1 (2026-07-20)** (bounded append, architect-ratified). (4)
  Corrected `SendLiveActivityUpdateHandler`'s catch comment to state at-most-once-after-the-marker
  honestly (mirrors `SendPushNotificationHandler`) — no behaviour change. (5) Added a
  `LiveActivityProducerTests` case exercising the gate's `OrderId == null` push-to-start branch on the
  real repo. `dotnet build src/Cleansia.Api.sln` clean; `dotnet test src/Cleansia.Tests` green. Not
  committed (owner commits).

## LA-5 — iOS widget: OWNER HANDOFF (2026-07-20)

The backend (LA-1…LA-4) is done and ships **inert**. The iOS widget target can't be created by Claude
(it lives in `CleansiaCustomer/project.yml`, which carries the owner-local Stripe key). So: **owner adds
the target once**, then Claude writes + verifies the ActivityKit Swift against it. Three owner steps:

### Step 1 — add the Widget Extension target (paste into `CleansiaCustomer/project.yml` under `targets:`)
```yaml
  CleansiaCustomerLiveActivity:
    type: app-extension
    platform: iOS
    deploymentTarget: "16.1"          # Live Activities need 16.1; the APP floor stays 16.0
    sources:
      - path: LiveActivity            # Claude adds the widget Swift here after this target exists
    info:
      path: LiveActivity/Info.plist
      properties:
        CFBundleDisplayName: Cleansia
        NSExtension:
          NSExtensionPointIdentifier: com.apple.widgetkit-extension
    settings:
      base:
        PRODUCT_BUNDLE_IDENTIFIER: cz.cleansia.customer.widgets
        GENERATE_INFOPLIST_FILE: NO
        SKIP_INSTALL: NO
    dependencies:
      - package: CleansiaCore
```
Then, on the **CleansiaCustomer app target**: add `NSSupportsLiveActivities: YES` to its
`info.properties`, and add `- target: CleansiaCustomerLiveActivity` to its `dependencies:` (xcodegen
embeds the extension automatically). Run `xcodegen generate` in `CleansiaCustomer/`.
NOTE the APNs topic the backend already uses is `cz.cleansia.customer.push-type.liveactivity` — the
**app** bundle id (`APNS:CustomerBundleId = cz.cleansia.customer`), NOT the widget's `.widgets` id. Correct as authored.

### Step 2 — fold the entity into the Initial migration
`LiveActivityToken` (+ its config, table `LiveActivityTokens`, the unique `(UserId, DeviceId, OrderId)`
NULLS-NOT-DISTINCT index) compiles but isn't in the migration. Regenerate the single Initial migration
(startup host `Cleansia.Web.Partner`) so the table ships — the established pre-prod pattern.

### Step 3 — seed APNS + go live (only when LA-5 ships)
Seed KV secrets `Apns--KeyId` / `Apns--TeamId` / `Apns--PrivateKeyPem` from the **same .p8 already in
backend custody** (no new key), then set `apnsSecretProvisioned = true` in the prod/dev bicepparam. That
single flip wires the six `APNS__*` settings AND enables the channel (App Service replaces appSettings
every deploy, so enable must be param-driven — see the LA-4 bicep note). Until then the whole pipeline
is INERT (`APNS:Enabled=false`).

### Then Claude writes LA-5 (after Step 1)
`CleanOrderAttributes` (ActivityAttributes + ContentState mirroring the backend `LiveActivityContentState`
`{v, status, orderNumber, scheduledStart, scheduledEnd}`), the Live Activity + Dynamic Island widget UI,
and a `LiveActivityCoordinator` that: starts the activity on the order-tracking screen, registers the
push-to-start token + the per-order update token via `POST /api/LiveActivity/Register`, and ends it via
the DELETE endpoint / the backend `end` push. Plus the 5-locale key `live_activity.order_not_active`
(the register endpoint's edge error) in the customer error catalog. Gated on Step 1.
