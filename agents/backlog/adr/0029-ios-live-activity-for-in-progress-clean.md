# ADR-0029 — iOS Live Activity for the in-progress clean: a direct-APNs `liveactivity` channel beside FCM, driven by the order-status seam (OnTheWay→start, InProgress→update, terminal→end), with a dedicated per-order `LiveActivityToken` registration (not `Device`) and a customer-app-only v1

- **Status:** **proposed (ratification-ready)** — panel consensus 2026-07-17 (see Verdict) +
  code-drift re-verification round 2026-07-19 (see "Re-verification round"). The former hard gate is
  now **substantially resolved**: the owner's APNs `.p8` is uploaded, delivered as a backend secret,
  and **confirmed working for ordinary push against the live dev environment** — the same team-scoped
  key signs `liveactivity` sends, so no new key and no new custody *decision* remain, only the
  mechanical `APNS:*` config binding (Dependencies §1). What still holds this at `proposed`:
  owner ratification of the product choices (timing floors, v1 exclusions — "Owner ratification
  questions" Q2/Q3, each with a recommended default). Once accepted it becomes immutable — supersede,
  never edit.
- **Date:** 2026-07-17 (drafted) · 2026-07-19 (hardened against the shipped producer-seam/feed code)
- **Supersedes:** — (extends the push architecture without touching it: ADR-0002's dispatch contract,
  ADR-0023's consumer boundary, and ADR-0025's FCM alert wire shape are **byte-unchanged**; this ADR
  adds a *parallel* channel, queue, consumer, and registration surface)
- **Superseded by:** —
- **Backs / extends:** ADR-0002 (outbox/`IPendingDispatch` — the producer seam and the
  transient/permanent retry vocabulary the new consumer reuses), ADR-0005 (integration resilience —
  the new APNs client codes against its pooled-client + failure-taxonomy contract), ADR-0014 (iOS 16.0
  deployment floor — **kept**; ActivityKit is availability-gated, never a floor change), ADR-0016
  (Apple-review quality bar — a Live Activity is a high-visibility review surface), ADR-0025 (the FCM
  alert path this channel sits beside without entangling), ADR-0026 (device revocation — activity
  tokens join the session-hygiene cleanup)
- **Applies to:** backend (`Cleansia.Infra.Clients` new `Apns/`, `Cleansia.Core.Domain` new entity,
  customer mobile host endpoint, Functions new consumer) | ios (customer app + **new Widget Extension
  target**) — partner app, Android, and all web hosts untouched
- **Ticket:** T-0427 (`architect, ios, backend`, size L) · **depends_on:** T-0342 (**substantially
  resolved 2026-07-19** — `.p8` uploaded, in backend secret custody, ordinary push confirmed working
  on live dev; residual = `APNS:*` config binding + widget provisioning, see Dependencies), T-0403 ✅
  (FCM tokens — done), T-0404 ✅ (alert display — done; the alert channel stays the source-of-truth
  notification path)

> **One decision:** *how the customer iOS app gets a Wolt/Uber-style lock-screen + Dynamic Island
> live status for an in-progress cleaning order, updated remotely while the app is not running.*
> Four facets of that one decision (transport, triggers, token contract, scope) are decided together
> below as D1–D4 because none is separately shippable. The chosen shape: a **direct-APNs
> token-authenticated (`.p8`/ES256) `liveactivity` channel** — a new small client beside FCM, never
> through it — driven from the **existing order-status producer seam** onto a **new
> `live-activity-dispatch` queue** with its own Mode-A consumer; per-order ActivityKit update tokens
> (plus the per-device push-to-start token, iOS 17.2+) live in a **new `LiveActivityToken` entity**
> registered via the customer mobile host — **not** as columns on `Device`. v1 is customer-app-only,
> one activity per active order, no live map/ETA.

---

## Context

### What exists (verified in the working tree 2026-07-17; re-verified + corrected 2026-07-19)

- **The alert push path is complete in code AND delivering.** Producers call the **shared notify
  seam `INotificationProducer.NotifyAsync`** (`NotificationProducer.cs` — one call atomically
  records the in-app feed row for feed-scoped events *and* enqueues the
  `QueueEnvelope<SendPushNotificationMessage>` on `notifications-dispatch` via the
  `IPendingDispatch` the producer holds; constructing the message anywhere else is forbidden by a
  raw-file tripwire test, `SendPushNotificationSeamTripwireTests.cs`). The status handlers hold
  `INotificationProducer`, **not** `IPendingDispatch` (`NotifyOnTheWay.cs:86,100`,
  `StartOrder.cs:115,154`, `CompleteOrder.cs:237`) — the 2026-07-17 draft predated this
  consolidation; D2's producer-seam shape is corrected accordingly (a **sibling seam**, not a bare
  enqueue). The Functions consumer (`SendPushNotificationHandler.cs`) claims the D2.1 key (Mode A,
  claim-first), gates on preferences, fans out via the only `IPushDispatcher`
  (`FcmPushDispatcher`), and prunes FCM-rejected tokens. ADR-0025's `FcmMessageFactory` builds the
  per-platform APNs **alert** block (loc-keys) inside that FCM message. **Delivery is live:** the
  owner's `.p8` is uploaded, the backend secret is provisioned, and ordinary push is confirmed
  working on the owner's iPhone against the dev environment.
- **The order-status seam is single and clean.** Every transition appends through
  `Order.AddOrderStatus` (`Order.cs:344` — "the single append seam", with persisted `CurrentStatus`
  and a deterministic `OrderStatusTrack.Sequence`). The handlers that append the in-service
  transitions: `NotifyOnTheWay` (OnTheWay), `StartOrder` (InProgress), `CompleteOrder` (Completed),
  `CancelOrder` / `AdminCancelOrder` (Cancelled), and `AdminOverrideOrderStatus` (any **strict
  forward** lifecycle move — `AdminOverrideOrderStatus.cs:96-105` rejects same-state, backward, and
  off-lifecycle targets, so **Cancelled is unreachable through the override** and a same-status
  revisit is impossible at this seam). Two 2026-07-19 corrections to the draft's "the exact sites
  that already enqueue the alert push" claim: (a) `AdminOverrideOrderStatus` produces **no**
  notification today (it appends status + audit only) — for this feature it gains its *first*
  producer call, because a customer's lock-screen card must not miss an admin-driven
  Completed/InProgress; (b) `CancelOrder`/`AdminCancelOrder` notify the customer only on the
  refund branch (`order.refunded`, conditional) — the activity **end** enqueue therefore sits
  unconditionally beside `AddOrderStatus`, never inside the refund conditional. Lifecycle:
  `New(0) → Pending(1) → Confirmed(2) → OnTheWay(3) → InProgress(4) → Completed(5)`, `Cancelled(6)`.
- **The registration surface is `Device`** (`Device.cs`): `(UserId, DeviceId)` unique across
  active+inactive rows, soft-delete tombstone reclaim, token-less registration allowed (T-0398),
  `NotificationsEnabled` kill switch, and — critically — it is a **shared partner+customer surface**
  wired into ADR-0026's revocation directory. `RegisterDevice` rides both mobile hosts'
  `DeviceController`s.
- **`Cleansia.Infra.Clients`** holds the three outbound adapters (Fcm, SendGrid, Stripe), all under
  ADR-0005's pooled-`IHttpClientFactory` + failure-classification contract.

### What ActivityKit requires (platform facts the decision is built on)

1. A Live Activity is rendered by a **Widget Extension** (new app-extension target) from a
   `Codable` `ContentState`; the app (or a push) supplies state — the widget has no logic beyond
   rendering what it is handed. iOS **16.1+**.
2. Remote updates address a **per-activity APNs push token** (obtained from
   `activity.pushTokenUpdates` after the activity starts, **rotates mid-activity**) — sent with
   `apns-push-type: liveactivity`, topic `<bundle-id>.push-type.liveactivity`, and a **mandatory
   `timestamp`** the system uses to **discard out-of-order updates**.
3. **These tokens are not FCM registration tokens.** `FcmPushDispatcher`'s multicast-per-user model
   cannot address them; the FirebaseAdmin .NET SDK the platform dispatches through exposes no
   live-activity member on its `ApnsConfig` (the REST surface FCM grew for its iOS-SDK path would
   have to be hand-rolled — see Option D1-B).
4. **Remote start** (`event: start` carrying `attributes-type` + `attributes`) needs the
   **push-to-start token** — per app install, from `Activity.pushToStartTokenUpdates`, iOS
   **17.2+**. Below 17.2 an activity can only be started **locally by the running app**.
5. Lifetime bounds: the system force-ends an activity after **~8 h**; it may linger on the lock
   screen up to 4 more hours unless a `dismissal-date` says otherwise; `stale-date` flips the
   widget's `isStale` rendering.
6. `NSSupportsLiveActivities` in the app's Info.plist (xcodegen `project.yml` — the plist itself is
   generated; see MANUAL_STEP notes).

---

## D1 — Update transport: **direct APNs, token-based (`.p8`/ES256) auth — a new small `Apns` adapter beside FCM**

### Options

- **D1-A — Direct APNs (CHOSEN).** A new `ApnsLiveActivityClient` in
  `Cleansia.Infra.Clients/Apns/`: HTTP/2 `POST https://api{.sandbox}.push.apple.com/3/device/{activityToken}`,
  `authorization: bearer <ES256 JWT>` minted from the **same team-scoped `.p8` auth key T-0342
  provisions** (an APNs auth key is team-wide and push-type-agnostic — the key that unlocks alert
  push via the Firebase console upload also signs direct `liveactivity` sends; what is NEW is
  **delivering the key material to backend config**, see Dependencies). JWT cached and re-minted
  every ~50 min (Apple's 20–60 min window). Named `IHttpClientFactory` client with pooled
  `SocketsHttpHandler` and `HttpVersion.Version20`, per the ADR-0005 contract. Failure taxonomy
  (ADR-0005 vocabulary): **permanent** → `410 Unregistered` / `400 BadDeviceToken` — prune the
  token row; **auth** → `403 ExpiredProviderToken/InvalidProviderToken` — re-mint the JWT once,
  then classify transient; **transient** → 429/5xx/network — throw, queue redelivers.
  `Apns:Enabled=false` → the dispatcher reports **Skipped** and the consumer **acks** (the exact
  dev/CI no-op semantics `SendPushNotificationHandler` already implements for unconfigured FCM).
- **D1-B — Route through FCM's live-activity relay. Rejected.** FCM's HTTP v1 REST surface has
  grown a live-activity token field for its iOS-SDK path, but the FirebaseAdmin .NET SDK in use
  does not expose it — using it means hand-rolling a Google-OAuth-authenticated REST call, i.e.
  **building a bespoke client anyway**, with an extra relay hop, second-party error mapping in
  front of APNs' first-party semantics (`410 Unregistered` is the prune signal ADR-0025's
  dead-token model depends on), and a dependency on FCM feature lag for an Apple-only surface. It
  would also tempt entangling the send into `FcmPushDispatcher`, whose per-user token-multicast
  shape does not fit a per-order single-token send.
- **D1-C — Polling. Rejected.** A Live Activity cannot poll: the widget renders only what it is
  handed, background app refresh is discretionary and terminated apps get nothing. Fails the
  feature's core premise (updates while the app is not running).
- **D1-D — Silent push (`content-available`) → app updates the activity locally. Rejected.**
  Reproduces ADR-0025 Option C's rejection exactly: background pushes are throttled, best-effort,
  and **not delivered to user-terminated apps** — precisely the state a lock-screen activity
  exists for. Apple's sanctioned remote-update path *is* the ActivityKit token.
- **D1-E — iOS 18 broadcast channels. Rejected for v1.** 1:many channel infrastructure for a
  1:1 per-order surface, on an 18.0 floor. Out of scope; the seam (a second push-type on the same
  client) stays open.

### Decision

D1-A. One new adapter (`ApnsLiveActivityClient` behind an `ILiveActivityPushClient` port in
`Cleansia.Core.Clients.Abstractions/Apns/` — the exact placement `IPushDispatcher` has in
`.../Fcm/`), one new config block, zero change to the FCM path. Whether the HTTP/2+ES256 plumbing
is hand-rolled or wrapped by a maintained library (e.g. dotAPNS) is an **implementation choice
inside this contract** — the port, taxonomy, pooling, and config shape below are what this ADR pins
(challenger CH-D1-2).

**Config contract (pinned 2026-07-19, platform conventions applied).** `IApnsConfig` in
`Cleansia.Infra.Common/Configuration/Interfaces/` + `ApnsConfig : AutoBindConfig(configuration,
"APNS")` in `Cleansia.Infra.Common/Configuration/`, registered in
`Cleansia.Config/Configurations/ConfigurationExtensions.AddConfigurationBindings` — byte-for-byte
the `IFcmConfig`/`FcmConfig`("FCM") archetype. The exact keys (host + Functions app settings;
binder is case-insensitive):

| Key | Meaning | Default |
|---|---|---|
| `APNS:Enabled` | Master switch. `false` → the client reports **Skipped** and the consumer **acks** (the exact `result.Skipped` inert-ship semantics `FcmPushDispatcher`/`SendPushNotificationHandler` implement). Lets the already-provisioned key sit in config with the channel off until the iOS lane ships. | `false` |
| `APNS:KeyId` | The 10-char Apple Key ID of the `.p8` (from T-0342's key-creation step — the owner noted it at creation). | empty |
| `APNS:TeamId` | The Apple Developer Team ID (paid team, enrolled 2026-07-13). | empty |
| `APNS:PrivateKeyPem` | The `.p8` content. Raw PEM or base64-wrapped — same dual-accept convention as `FCM:ServiceAccountJson`. **The same key material already delivered as a backend secret for ordinary push** — a config binding, not a new custody surface. Missing/empty while `Enabled=true` → treated as Skipped + a startup `LogWarning`, never a crash (the FCM "deliberately unconfigured" pattern). | empty |
| `APNS:CustomerBundleId` | `cz.cleansia.customer` — the `apns-topic` is derived as `{CustomerBundleId}.push-type.liveactivity`, never hardcoded. | empty |
| `APNS:UseSandbox` | `true` → `api.sandbox.push.apple.com` (dev — the owner's iPhone runs dev builds, which get sandbox activity tokens); `false` → `api.push.apple.com`. | `true` in dev, `false` in prod |

---

## D2 — Trigger points: **OnTheWay starts, InProgress updates, terminal ends; stale-date guards long/stuck cleans; same producer seam, new queue, own Mode-A consumer**

### The transition map

| Order transition | Activity event | Payload notes |
|---|---|---|
| `Confirmed` | **none** (no activity) | days may separate booking from service; the ~8 h ActivityKit budget makes an at-confirmation activity structurally wrong (CH-D2-1) |
| `OnTheWay` (`NotifyOnTheWay`) | **start** — `event: start` to the push-to-start token (iOS 17.2+); 16.1–17.1 fall back to **local start on next app foreground** while status ∈ {OnTheWay, InProgress} | initial `content-state: onTheWay` |
| `InProgress` (`StartOrder`) | **update** to each per-order update token | `content-state: inProgress` |
| `Completed` (`CompleteOrder`) | **end** — final `content-state: completed`, `dismissal-date = now + 30 min` (final state stays glanceable, then leaves) | owner-ratified window |
| `Cancelled` (CancelOrder / AdminCancelOrder — **corrected 2026-07-19:** `AdminOverrideOrderStatus` is strict-forward-only and rejects Cancelled, `AdminOverrideOrderStatus.cs:96-103`) | **end** — `content-state: cancelled`, `dismissal-date = now` (immediate dismissal; a dead order must not linger). Enqueued **unconditionally** beside `AddOrderStatus` — the existing customer alert on cancel is refund-conditional and must not gate the end-push | pre-service cancels no-op (no token rows exist yet) |
| Any forward move via `AdminOverrideOrderStatus` (OnTheWay / InProgress / Completed) | the same event the organic handler would send (start / update / end) | **added 2026-07-19:** this handler produces no alert push today — the activity enqueue is its *first* producer call, so the lock-screen card tracks admin-driven transitions too |

- **Staleness (revised per CH-D2-3):** every start/update sets
  `stale-date = max(now + 4 h, scheduledEnd + 1 h)` — a 6-hour booked clean never renders stale
  mid-service; a genuinely stuck activity (cleaner never completed) flips to the widget's explicit
  `isStale` presentation ("status may be outdated — open the app") instead of lying, and the OS 8 h
  cap ends it. **No server-side watchdog end-push in v1** — the accepted residual is a stale-styled
  card for a few hours in the forgotten-order case, which the alert channel and the app remain
  authoritative over.
- **Ordering:** the APNs payload's mandatory `timestamp` is set to the transition's
  `OrderStatusTrack.CreatedOn` — ActivityKit discards updates older than the last applied, so
  queue-redelivery or cross-transition races cannot regress the lock screen (CH-D2-4).
- **Producer seam (reshaped 2026-07-19 to the shipped consolidation):** the status handlers no
  longer hold `IPendingDispatch` — they hold the shared notify seam `INotificationProducer`
  (tripwire-pinned). The live-activity channel gets the **sibling seam, same archetype**: a small
  `ILiveActivityProducer` (`Cleansia.Core.AppServices/Services/`, holding `IPendingDispatch`) whose
  one method `NotifyOrderTransitionAsync(order, eventKey, transitionAtUtc, ct)` builds and enqueues
  the `QueueEnvelope<SendLiveActivityUpdateMessage>` (UserId, OrderId, EventKey, orderNumber +
  schedule display args, transition timestamp, TenantId) on the **new queue
  `live-activity-dispatch`** (`QueueNames.LiveActivityDispatch`). Six producer sites — the five
  notify-seam handlers plus `AdminOverrideOrderStatus` — each gain **one collaborator + one call**
  (2 → 3-ish collaborators; nowhere near the 8-service smell). It is deliberately **not** a second
  method on `INotificationProducer`: that seam's contract is "feed row + push, atomically, gated by
  notification preferences" — an activity is none of those (D4), and entangling them re-creates the
  channel coupling ADR-0023 exists to forbid. Constructing `SendLiveActivityUpdateMessage` outside
  `LiveActivityProducer` is forbidden by a **second raw-file tripwire test** (the
  `SendPushNotificationSeamTripwireTests` pattern verbatim). Producers do **not** check whether
  tokens exist (they cannot, cheaply); the consumer no-ops on zero rows, exactly as the push
  consumer no-ops on zero devices.
- **Consumer:** a new Functions handler `SendLiveActivityUpdateHandler` (`Cleansia.Functions.Core/
  Handlers/`, thin `[QueueTrigger]` shell in `Cleansia.Functions/Functions/`) — **Mode A
  claim-first** (ADR-0002 D2.2) on a new deterministic key
  `MessageKeys.LiveActivity(orderId, eventKey, sequence)` →
  `liveactivity:{orderId}:{eventKey}:{sequence}` (an **added** frozen D2.1 formula in
  `MessageKeys.cs` — additions are this ADR's job; *changes* stay superseding-ADR-only).
  **Corrected rationale 2026-07-19:** `AdminOverrideOrderStatus` rejects same-status revisits, so
  no code path can currently produce the same `(orderId, eventKey)` twice — the `Sequence` segment
  is *defensive* (a pure-function key that stays collision-free if any future handler ever
  re-appends a status), not load-bearing today (CH-D2-4 stands, weakened honestly). The consumer
  resolves the order's `LiveActivityToken` rows; builds the ActivityKit payload; sends per token
  via the D1 client; prunes 410s; deletes the order's rows after a **successful terminal send**. It
  is a **sibling** of `SendPushNotificationHandler`, never a modification of it (ADR-0023's
  boundary logic applies by construction: separate queue, separate claim keyspace, separate failure
  domain — an FCM outage cannot retry-storm APNs, and vice versa). Per ADR-0002 D3 (F3) the queue
  gets its own poison pair: `live-activity-dispatch-poison` + `LiveActivityDispatchPoisonHandler`
  (DeadLetter row + LogError + ack — the `NotificationsDispatchPoisonHandler` archetype).
- **Lost-update residual (CH-D2-2, accepted):** Mode A is at-most-once-after-the-marker; a crash
  between claim and send loses that one activity update. A lost mid-flight update is healed by the
  next transition; a lost **terminal** update leaves a zombie card bounded by `stale-date` styling,
  the OS 8 h hard cap, and the token janitor (D3). Accepted for a glanceable surface — the alert
  push (own claim key, own queue) still tells the user the order completed.

### Alternatives rejected

- **Start at `Confirmed`** — the 8 h budget kills it structurally (a Tuesday booking for Friday
  ends Tuesday night); Wolt/Uber parity is the *active-service* window, not the reservation.
- **Piggyback `notifications-dispatch` / extend `SendPushNotificationHandler`** — entangles two
  channels' retry, idempotency, and preference semantics (an activity update is not a notification:
  it must **ignore** `NotificationsEnabled`/category mutes? No — see D4 — but its *failure*
  handling differs), and violates the spirit of ADR-0023's byte-untouched consumer. Two queues,
  two small consumers, zero coupling.
- **Domain events / a new orchestration layer on `AddOrderStatus`** — the platform's established
  producer pattern is explicit enqueue at the handler (ADR-0002); introducing an implicit
  event-fanout seam for one feature is a bigger architecture change than the feature.

---

## D3 — Token registration: **a new `LiveActivityToken` entity + customer-mobile-host endpoint — `Device` is not overloaded**

### The contract

- **Entity `LiveActivityToken`** (`Cleansia.Core.Domain/LiveActivities/`), `Auditable`,
  **`ITenantEntity`** (global filter — the tenancy seam, never hand-rolled):
  `UserId`, `DeviceId` (the same client-generated id `Device` rows carry — correlation, not FK),
  `OrderId` (**nullable**: `null` = the per-install **push-to-start** token; non-null = a
  **per-activity update** token for that order), `Token` (APNs hex), `LastUpdatedAt`.
  Unique `(UserId, DeviceId, OrderId)`; registration **upserts** (ActivityKit rotates update tokens
  mid-activity and rotates push-to-start tokens across installs — last write wins).
- **Endpoint — customer mobile host only** (`Cleansia.Web.Mobile.Customer`, a new
  `LiveActivityController`): `POST /LiveActivity/Register` `{ deviceId, token, orderId? }` →
  `RegisterLiveActivityToken` command. Validation (FluentValidation, not the handler): device id +
  token non-empty; when `orderId` present — the order **belongs to the caller** (S1) and
  `CurrentStatus ∈ {Confirmed, OnTheWay, InProgress}`. `DELETE /LiveActivity/{orderId}` for the
  client-side end path (user dismissed the activity → `pushTokenUpdates` ends). **MANUAL_STEP:**
  EF migration (owner; pre-prod = fold into the Initial migration) + customer-mobile spec regen.
- **iOS obligations:** observe `Activity.pushToStartTokenUpdates` (17.2+, at launch) → register
  with `orderId = null`; observe `activityUpdates` (an activity remotely started launches the app
  in background) and each activity's `pushTokenUpdates` → register with its `orderId`; before a
  local start, check `Activity<CleanOrderAttributes>.activities` for the order — **one activity
  per order** enforced client-side.
- **Cleanup (four paths, all cheap):** (1) the consumer deletes an order's rows after a successful
  terminal send; (2) APNs `410`/`400 BadDeviceToken` prunes the row (D1 taxonomy); (3) a janitor
  sweep (existing Functions timer host) deletes order-scoped rows older than 24 h — the
  orphaned-row backstop for the lost-terminal-update residual; (4) **logout/`RevokeDevice`
  deletes the device's rows** (push-to-start included) — session hygiene consistent with the S11
  wipe rule and ADR-0026's revocation intent: a revoked device must not keep receiving lock-screen
  order state.

### Alternatives rejected

- **D3-B — columns on `Device`** (the obvious cheap path — attacked and rejected):
  1. **Cardinality:** update tokens are per *(device × order)* — a recurring customer with two
     bookings in flight needs N rows; `Device` is one row per install.
  2. **Lifecycle:** `Device` rows live for the install and are tombstone-reclaimed across logins;
     activity tokens die in hours and rotate mid-activity. Grafting hours-lived state onto an
     install-lived aggregate makes every `Device` write path reason about activity semantics.
  3. **Shared surface:** `Device` is partner+customer and load-bearing for ADR-0026's revocation
     directory and the dispatcher's eligibility filter. Customer-only Live-Activity columns fatten
     the platform's most security-coupled registration entity for one app's feature.
  4. The `Device` CRC boundary ("does NOT know push content semantics") survives: `Device` still
     answers *"where do alerts go"*; `LiveActivityToken` answers *"where does THIS order's live
     card go"*.
- **D3-C — reuse `RegisterDevice` with a token-kind discriminator** — pollutes a stable, shared,
  spec-published contract (both mobile apps + tombstone semantics) with per-order fields that are
  meaningless for alert tokens; separate command keeps both validators honest.
- **D3-D — two entities (PushToStartToken + OrderActivityToken)** — two janitors, two endpoints,
  two EF configs for a one-nullable-column difference. Rejected for v1; **revisit trigger:** if
  push-to-start ever grows per-widget-type attributes, split then (the lead's condition, CH-D3-2).

---

## D4 — Scope guard: **customer app only; one activity per active order; explicit v1 exclusions; the content-state is an S6-allowlisted, versioned cross-platform contract**

- **Customer app only.** The partner has no lock-screen-status use case today (they *are* the
  status source). The endpoint lives on the customer host only — per-audience host separation is
  the seam, not an accident; a partner v2 adds its own endpoint + widget, no shared-code rewrite.
- **One activity per active order** (client-enforced, D3). Overlapping orders each get their own
  activity; the OS stacks them — **no custom multi-order UI in v1**.
- **Preference gating:** activity sends respect `NotificationsEnabled` = false on the target
  device's `Device` row? **No** — deliberately **not** consulted: ActivityKit has its own
  user-facing consent (the user can disable Live Activities per-app in Settings, and dismissing
  the card ends the token stream). Coupling the activity channel to the alert kill switch would
  make "mute notifications" also kill a surface the OS governs separately. The per-category
  notification preferences likewise do not apply (an activity is not a notification). Recorded
  explicitly so a reviewer doesn't "fix" it into the dispatcher's filter.
- **Content-state = a versioned wire contract** (the loc-key lesson, ADR-0025 D4.1 analog):
  `{ v: 1, status: string, orderNumber, scheduledStart, scheduledEnd }` — the widget decodes
  `status` as a **string** and maps unknown values to a generic in-service presentation (schema
  evolution can never fail decoding into a dropped update). **S6 allowlist for lock-screen-visible
  values: `status`, `orderNumber`, the two schedule timestamps. Forbidden permanently: names
  (customer *and* cleaner), addresses, free text, internal ids** (`orderId` stays app-side).
  Stricter than Wolt (courier name) by design — same S6 line as ADR-0025 D3, pinned by test.
  Additive-only evolution; a shared JSON fixture is asserted by a backend unit test **and** an iOS
  decoding test (the cross-platform pin).
- **v1 exclusions (explicit):** partner-app activities; Android "Live Updates"/promoted ongoing
  notifications (own ticket if the product wants parity); live map / cleaner ETA (**no
  cleaner-location stream exists** — building one is a feature an order of magnitude larger than
  this ADR and a consent/privacy decision of its own); iOS 18 broadcast channels; the
  frequent-updates entitlement (transition cadence is ~4 events per order — not needed); iOS
  < 16.1 (silent no-op — availability-gated, the ADR-0014 16.0 floor is untouched); marketing/promo
  content in activities (S6 + the T-0412 promo channel is separate).
- **New iOS target:** `CleansiaCustomerWidgets` (Widget Extension, xcodegen `type: app-extension`,
  `NSExtensionPointIdentifier: com.apple.widgetkit-extension`, bundle id
  `cz.cleansia.customer.widgets`, deploymentTarget 16.1 — an embedded extension may floor higher
  than its 16.0 host app; on a 16.0 device it simply never loads) + `NSSupportsLiveActivities: true`
  on the **app** target's Info properties. **MANUAL_STEP (refined 2026-07-19):**
  `CleansiaCustomer/project.yml` is **owner-modified working tree** (it carries the owner's Stripe
  publishable key; standing rule: agents never checkout/stage it) — the implementation ticket specs
  the exact `project.yml` diff **verbatim** (T-0427 slice LA-5) and **the owner applies it**, then
  re-runs xcodegen (standing post-pull rule). Extension provisioning rides Dependencies §1.

---

## Dependencies (stated explicitly — the gate structure)

1. **T-0342 (owner) — SUBSTANTIALLY RESOLVED 2026-07-19.** The APNs `.p8` is created, uploaded,
   **delivered as a backend secret, and confirmed working for ordinary push** (dev environment
   live; the owner's iPhone runs against DEV). An APNs auth key is team-scoped and
   push-type-agnostic — the **same key signs `liveactivity` sends; no new key, no new custody
   decision**. What remains is mechanical + provisioning, not gating architecture:
   - **(owner, config)** bind the existing key material into the D1 config keys — `APNS:KeyId`,
     `APNS:TeamId`, `APNS:PrivateKeyPem`, `APNS:CustomerBundleId=cz.cleansia.customer`,
     `APNS:UseSandbox=true` (dev), leaving `APNS:Enabled=false` until the iOS lane ships
     (**MANUAL_STEP**, minutes — the secret already exists in backend custody).
   - **(owner, Apple)** Widget-Extension provisioning: the `cz.cleansia.customer.widgets` bundle id
     signs under the paid team (automatic signing typically auto-registers it) — riding the
     standing post-pull xcodegen session, plus applying the `project.yml` diff (see D4
     MANUAL_STEP).
2. **T-0403 ✅ / T-0404 ✅ (code)** — the alert channel this sits beside; the alert path stays the
   authoritative notification, the activity is glanceable state. No ordering constraint between
   the two channels' deploys (they share no wire, queue, or consumer).
3. **Backend-first is safe:** the entity + endpoint + queue + consumer with `Apns:Enabled=false`
   ship inert (Skipped-ack semantics, D1). The iOS lane (widget + token observers + registration)
   can land behind the 16.1/17.2 availability gates in the same or a later release train.

---

## Consequences

**Cheaper / safer:**
- The FCM path, ADR-0025's wire shape, `SendPushNotificationHandler`, and `Device` are all
  **byte-unchanged** — the new channel is additive end to end (new adapter, new queue, new
  consumer, new entity, new controller, new iOS target).
- The APNs adapter is the platform's fourth outbound client under the *same* ADR-0005 contract —
  no new resilience vocabulary; and it is the natural home for any future direct-APNs need
  (broadcast channels, partner activities, VoIP-class pushes) — the seam earns its keep beyond v1.
- The producer cost is one `ILiveActivityProducer` collaborator + one call per status handler (six
  sites), mirroring the shipped `INotificationProducer` archetype — no handler-count smell, and the
  envelope/key construction stays in exactly one tripwire-pinned file per channel.
- Zero coupling between channels' failure domains; dev/CI stays no-op by config exactly like FCM.

**More expensive / accepted residuals:**
- A second push secret-management surface (JWT minting, key rotation, sandbox-vs-prod hosts) the
  owner and ops must now carry — priced as the unavoidable cost of a surface FCM cannot serve.
- Sub-17.2 devices (16.1–17.1) get **no remote start** — the activity appears only if the app is
  foregrounded during the service window. Honest degradation; shrinks monotonically with OS
  adoption; the alert pushes remain complete on those devices.
- The zombie-card residual (lost terminal update) bounded by stale-date + 8 h cap + janitor —
  never a wrong *status*, at worst a stale-styled card.
- One more cross-platform contract to keep in sync (content-state schema) — pinned by the shared
  fixture on both sides, additive-only.
- A new EF entity + migration (**MANUAL_STEP**, owner) and a customer-mobile spec regen
  (**MANUAL_STEP**).

---

## How a reviewer verifies compliance

**Mechanical:**
1. `FcmPushDispatcher`, `IPushDispatcher`, `SendPushNotificationHandler`, `Device`,
   `RegisterDevice`, **`INotificationProducer`/`NotificationProducer` and its tripwire test** — all
   byte-unchanged (grep/diff). The new consumer and the new producer seam are sibling files, not
   edits.
2. The APNs client lives in `Cleansia.Infra.Clients/Apns/`, is registered via a named
   `IHttpClientFactory` client (pooled handler, HTTP/2), and no other site constructs an APNs call.
   Its port is `Cleansia.Core.Clients.Abstractions/Apns/ILiveActivityPushClient.cs`; its config
   binds only the six `APNS:*` keys pinned in D1.
3. Producers: exactly the six handlers named in D2 (`NotifyOnTheWay`, `StartOrder`,
   `CompleteOrder`, `CancelOrder`, `AdminCancelOrder`, `AdminOverrideOrderStatus`) gain one
   `ILiveActivityProducer` call; the cancel-path call sits **outside** the refund conditional; no
   handler branches on country, platform, or token existence; `SendLiveActivityUpdateMessage` is
   constructed only in `LiveActivityProducer` (raw-file tripwire, the
   `SendPushNotificationSeamTripwireTests` pattern).
4. `LiveActivityToken` implements `ITenantEntity` and is covered by the global query filter; the
   register command validates order ownership in the **validator**, not the handler.
5. The content-state builder's allowlist: no name, address, free-text, or internal-id field can
   reach the payload (test-pinned, see TC-LA-6).
6. iOS: all ActivityKit call sites are `#available(iOS 16.1/16.2/17.2)`-gated; the deployment
   target is still 16.0; the widget decodes `status` as a string with a fallback presentation;
   `Localizable` strings for the widget live in the **extension's own** catalog ×5 locales.

**Test contract (red-first, `TC-LA-*`):**
7. **TC-LA-0** — payload per event: start carries `attributes-type`/`attributes` + state
   `onTheWay`; update carries `content-state` + `timestamp` = transition time; completed end
   carries `dismissal-date ≈ now+30 min`; cancelled end carries `dismissal-date = now`.
8. **TC-LA-1** — stale-date rule: `max(now + 4 h, scheduledEnd + 1 h)` at boundary values.
9. **TC-LA-2** — idempotency: same `(orderId, eventKey, sequence)` claims once (redelivery
   short-circuits); distinct sequences produce distinct keys (the defensive segment — no current
   code path revisits a status, `AdminOverrideOrderStatus.cs:96-103`, but the key must stay
   collision-free if one ever does).
10. **TC-LA-3** — taxonomy: 410/BadDeviceToken prunes the row and acks; 403 re-mints once then
    throws; 429/5xx throw (redelivery); `Apns:Enabled=false` → Skipped-ack, rows untouched.
11. **TC-LA-4** — terminal send success deletes the order's token rows; failure leaves them (the
    janitor's job); janitor deletes only order-scoped rows older than 24 h.
12. **TC-LA-5** — registration: upsert on rotation; foreign order → rejected; terminal-status
    order → rejected; logout/RevokeDevice cascades the device's rows.
13. **TC-LA-6** — the S6 pin: the union of content-state fields ⊆
    `{v, status, orderNumber, scheduledStart, scheduledEnd}` — asserted over the builder itself.
14. **TC-LA-7 (iOS)** — the shared JSON fixture decodes into `ContentState`; an unknown `status`
    string decodes and renders the fallback presentation.
15. **QA device matrix (manual):** 17.2+ device, app terminated: OnTheWay push materializes the
    activity on the lock screen; InProgress updates it; Completed shows the final state then
    dismisses (~30 min); a cancelled order dismisses immediately; Dynamic Island renders on
    island hardware; a 16.x device shows the activity after app-foreground during the window.

---

## Roles affected (files created when this ADR is `accepted` — deliberation protocol §parallel documentation)

- **NEW** `agents/knowledge/roles/apns-live-activity-client.md` — signs and delivers one
  ActivityKit payload to one activity token; owns the JWT cache + failure taxonomy; does NOT know
  orders, tenants, or which transition it is carrying.
- **NEW** `agents/knowledge/roles/live-activity-token.md` — remembers where an order's live card
  (and a device's remote-start ability) is addressable; does NOT know alert tokens, notification
  preferences, or payload content.
- **NEW** `agents/knowledge/roles/send-live-activity-update-handler.md` — translates one order
  transition into per-token ActivityKit sends with Mode-A idempotency; does NOT know FCM, alert
  display, or how activities render.
- **UPDATED** `agents/architecture/decisions/push-notifications.md` — gains the two-channel
  picture (FCM alerts | direct-APNs activities) + this trade-off space.

---

## Owner ratification questions (why this stays `proposed`)

1. **~~The second channel + second secret location~~ — RESOLVED BY CONDUCT 2026-07-19.** The owner
   already delivered the `.p8` as a backend secret and it is confirmed working for ordinary push
   against live dev. The same team-scoped key signs `liveactivity` sends; nothing new to approve —
   only the mechanical `APNS:*` binding (Dependencies §1 MANUAL_STEP).
2. **Product choices — OPEN, with recommended defaults (ratify or one-line-change each):**
   start at OnTheWay, not Confirmed (*recommended: keep* — the ~8 h ActivityKit budget makes a
   Confirmed-time start structurally dead for next-day bookings, CH-D2-1); completed card lingers
   **30 min** (*recommended: keep* — glanceable receipt without lock-screen squatting); cancelled
   dismisses **immediately** (*recommended: keep* — a dead order must not linger); remote start
   requires **iOS 17.2+**, 16.1–17.1 get the open-app-during-service-window fallback (*recommended:
   accept* — platform floor, not a choice we control; shrinks with OS adoption).
3. **v1 exclusions — OPEN, recommended: stand.** No partner activities, no Android
   Live-Updates parity, no live map/ETA (each is its own future ticket; the map/ETA additionally
   requires a cleaner-location stream that does not exist and is a consent feature of its own).

---

## Challenge

*(Architect panel, challenger mode — attacks per decision; every point checked against source or
platform documentation, not the draft's own claims.)*

### CH-D1-1 — Why a second channel at all? FCM grew live-activity support.
The platform just finished consolidating ALL push through one dispatcher (ADR-0025 explicitly
prides itself on "one multicast, no platform branch"). Now the author adds a parallel APNs client,
a second secret, and a second failure taxonomy. If FCM's v1 API can carry a live-activity token,
the marginal cost of staying on one channel may be lower than a new adapter the team maintains
forever. Price it against the real SDK, not the ideal.

### CH-D1-2 — Hand-rolled ES256/HTTP/2 is new cryptographic surface.
JWT minting, key caching, HTTP/2 connection management against Apple's picky endpoints — this is
the kind of plumbing that looks small in an ADR and bleeds for a quarter. Why does the ADR not
mandate a maintained library, or at least not pretend the choice is free?

### CH-D1-3 — "The T-0342 p8 serves" is asserted, not delivered.
T-0342's definition of done is *upload to the Firebase console*. The backend never sees the key.
If this ADR ships assuming the key is "already provisioned," implementation stalls on day one with
a missing secret nobody owns. The dependency line must name the new custody step explicitly or the
gate is fiction.

### CH-D2-1 — Starting at OnTheWay wastes the anticipation window.
Wolt shows the order card from acceptance. Why not start at Confirmed — "your clean is tomorrow at
9:00" is glanceable value. The author should prove the exclusion, not assume it.

### CH-D2-2 — Mode A + a terminal update = a zombie card on the lock screen at midnight.
The push consumer's at-most-once is fine for alerts (the next event heals). An activity has no
"next event" after Completed is lost. A customer staring at "in progress" at 23:00 for a clean
that ended at 14:00 is a trust-destroying surface. What bounds it?

### CH-D2-3 — A flat `stale-date = now + 4 h` marks a booked 6-hour clean stale mid-service.
The draft's own table says cleans can run long. A stale badge on a *correct* status is the same
trust damage as CH-D2-2 in the other direction.

### CH-D2-4 — Queue redelivery + two transitions in flight can regress the card (InProgress →
OnTheWay), and `MessageKeys.LiveActivity(orderId, eventKey)` deadlocks admin-override revisits
(the same status twice = second send silently swallowed by the claim).

### CH-D3-1 — The obvious design is a `Device.ActivityToken` column; the author must defeat it,
not skip it. One entity fewer, one endpoint fewer, the janitor is `Device`-lifecycle for free.
Overloading `Device` is named in the ticket as the thing to attack — attack it properly:
what *breaks*?

### CH-D3-2 — The nullable-`OrderId` dual-purpose table is a modeling smell.
Push-to-start tokens (install-lived, one per device) and update tokens (activity-lived, N per
device) in one table with a null discriminator is the classic "two concepts, one entity" shortcut
this panel usually rejects.

### CH-D4-1 — Without live location/ETA, is this feature worth an L ticket?
The alert pushes (T-0404) already tell the customer every transition on the lock screen. A Live
Activity that shows the same four statuses without the map/ETA that makes Wolt's compelling may be
cost without differentiation. The scope guard should say why v1 is still worth shipping — or this
escalates to the owner as a value question, not an architecture one.

### CH-D4-2 — Push-to-start at 17.2 quietly forks the experience under ADR-0014's 16.0 floor.
The floor was an owner decision for old-device reach; a headline feature that old devices only get
in degraded form must be recorded as such, not discovered in QA.

### Checked and found sound (named explicitly — silence is not assent)
- **The producer seam:** all in-service transitions flow through `Order.AddOrderStatus` and the
  named handlers already hold `IPendingDispatch` — one added enqueue is real, no new collaborator
  (verified `NotifyOnTheWay.cs:85-103`, `StartOrder.cs:138-167`).
- **Consumer separation:** a sibling handler on a new queue genuinely cannot entangle FCM retries
  — separate claim keyspace, separate poison queue. ADR-0023's boundary holds by construction.
- **Tenancy:** `LiveActivityToken : ITenantEntity` + global filter — seam respected, no
  hand-rolled scoping anywhere in the contract.
- **S6:** the content-state allowlist is *stricter* than the industry norm (no courier name) and
  test-pinned like ADR-0025 D3. Sound.
- **ADR-0014:** availability gates, floor untouched — verified against the ADR's status block.

## Defense

### CH-D1-1 — REBUT (with one concession folded).
The "one channel" property ADR-0025 protects is *the alert fan-out* — one multicast per user to
`Device` tokens. Live-Activity tokens are a different address space with different cardinality
(per order, not per user), a different lifetime (hours), and a different payload grammar
(`content-state`/`event`/`dismissal-date` — no loc-keys, no `Data`). There is no consolidation to
preserve: FCM's relay would still be a *second send path* inside the codebase — and with the
FirebaseAdmin .NET SDK exposing no live-activity member, it is a hand-rolled Google-OAuth REST
call: a bespoke client either way, with a relay hop and second-hand error semantics added. The
concession folded into D1-B: the rejection now argues against the real SDK surface, not "FCM
cannot" folklore — the owner's ticket premise ("FCM cannot address activity tokens") is true *for
the platform's dispatch stack*, and that is the claim D1 records.

### CH-D1-2 — CONCEDE + REVISE.
D1 now states the library-vs-hand-rolled choice is open at implementation *inside* the pinned
contract (port, taxonomy, pooling, config), and prices the custody/rotation cost as a named
residual in Consequences. What the ADR refuses to do is pin a specific NuGet package — that is an
implementation detail with its own review, not an architectural commitment.

### CH-D1-3 — CONCEDE + REVISE.
Correct and important. The Dependencies section now names the commissioned owner step in bold:
same `.p8`, **second delivery location** (backend secrets: PEM + KeyId + TeamId), plus the
Widget-Extension provisioning rider on the T-0342 session. Ratification question 1 puts it in
front of the owner explicitly.

### CH-D2-1 — REBUT.
ActivityKit force-ends an activity ~8 h after it starts. A Confirmed-time start for a next-day
booking is dead the same evening — the card would *vanish before the service it advertises*, which
is worse than absent. Wolt starts at acceptance because delivery is a 30-minute lifecycle; the
platform's equivalent of "acceptance to door" is OnTheWay→Completed, which is what D2 maps. The
anticipation window belongs to the alert channel (`order.confirmed` push + the app) — a surface
without an 8 h fuse.

### CH-D2-2 — REBUT (bounded, priced).
Three independent bounds, all in the draft: `stale-date` flips the card to an explicit
"may be outdated" presentation (never a confident lie), the OS ends the activity at the 8 h cap,
and the 24 h janitor clears the token rows. Upgrading the terminal send to Mode B
(act-then-mark) buys nothing here — the failure mode is APNs delivery, not the marker. The
residual is a stale-*styled* card for some hours in a crash-window case, and the completed alert
push (independent queue and claim) still fires. Priced in Consequences; accepted.

### CH-D2-3 — CONCEDE + REVISE.
Folded: `stale-date = max(now + 4 h, scheduledEnd + 1 h)` (D2, TC-LA-1). A booked-long clean can
no longer render stale mid-service.

### CH-D2-4 — CONCEDE + REVISE.
Both points folded: the APNs `timestamp` (= `OrderStatusTrack.CreatedOn`) makes regressions
impossible at the device (ActivityKit discards older-than-applied), and the claim key gains the
transition `Sequence` — `MessageKeys.LiveActivity(orderId, eventKey, sequence)` — so an
admin-override revisit is a fresh key, never swallowed (TC-LA-2).

### CH-D3-1 — REBUT (the attack the ticket ordered, answered on the merits).
A `Device` column breaks on **cardinality** before taste enters: update tokens are per
*(device × order)* — a recurring customer with overlapping bookings needs N tokens per device;
a column caps at one and silently drops the second order's card. Fixing that with a
`Device`-child collection just rebuilds D3's entity *inside* the platform's most security-coupled
aggregate — the one ADR-0026's revocation directory and the dispatcher's eligibility filter hang
off — coupling an hours-lived, customer-only concern to every partner-app and revocation code
path that touches `Device`. The lifecycle mismatch (install-lived tombstone reclaim vs
activity-lived rotation) means every `MarkRegistered`/reclaim path would need activity-token
reasoning. Cheap today, expensive at every future `Device` change — exactly the trade this panel
exists to refuse.

### CH-D3-2 — CONCEDE the smell, REBUT the split (lead's condition recorded).
Real smell, honestly carried: D3-D prices the alternative (two entities = two janitors, two
endpoints, two configs for one nullable column) and records the **revisit trigger** — if
push-to-start grows per-widget-type attributes, split then. The `Kind` is derivable
(`OrderId is null`), the cleanup paths are shared, and the table is small and short-lived by
construction. One entity stands for v1.

### CH-D4-1 — REBUT on architecture; the value question is *already* the owner's.
The differentiation over alert pushes is real and mechanical: an alert is a moment (dismissible,
buried under later notifications); the activity is *persistent glanceable state* + Dynamic Island
presence for the whole service window — the exact surface the owner's remark asked for by name
("Wolt/Uber-style"). ETA/map would deepen it but requires a cleaner-location stream that does not
exist and is a privacy/consent feature of its own. The ADR ships the owner's stated ask at its
minimal honest scope; the "is it worth an L" call is the owner's ticket-priority call, and this
ADR's ratification questions put the v1 shape in front of them. No escalation beyond the
ratification already built into the status.

### CH-D4-2 — CONCEDE + REVISE.
The 17.2 remote-start floor is now a named Consequence, a QA-matrix line (16.x foreground
fallback), and owner-ratification question 2. The ADR-0014 floor itself is untouched — old
devices lose nothing they have today.

## Verdict

*(Architect panel, lead mode — 2026-07-17.)* **CONSENSUS REACHED — zero blocking challenges
remain. Status: `draft — awaiting owner ratification` (deliberately NOT `accepted`: ratification
questions 1–3 + the T-0342 gate; the panel's consensus means implementation may be *planned*
against this shape, and the ADR flips to `accepted` the day the owner ratifies + provisions).**

| Challenge | Ruling |
|---|---|
| CH-D1-1 (second channel) | **DEFENDED** — different address space, cardinality, and payload grammar; the FCM relay is a bespoke client anyway on the real SDK. D1-B re-argued against the real surface. Resolved. |
| CH-D1-2 (crypto plumbing) | **CONCEDED + REVISED** — library choice open inside the pinned contract; custody cost priced. Resolved. |
| CH-D1-3 (p8 custody) | **CONCEDED + REVISED** — the commissioned owner step is now explicit in Dependencies + ratification Q1. Resolved. |
| CH-D2-1 (start at Confirmed) | **DEFENDED** — the 8 h ActivityKit cap makes it structurally wrong; anticipation belongs to the alert channel. Resolved. |
| CH-D2-2 (zombie card) | **DEFENDED** — triple-bounded (stale styling / 8 h cap / janitor), priced as an accepted residual. Resolved. |
| CH-D2-3 (long-clean staleness) | **CONCEDED + REVISED** — `max(now+4h, scheduledEnd+1h)`. Resolved. |
| CH-D2-4 (ordering/revisits) | **CONCEDED + REVISED** — APNs timestamp + Sequence in the claim key. Resolved. |
| CH-D3-1 (overload Device) | **DEFENDED** — fails on cardinality; couples an hours-lived customer concern to the revocation-coupled shared aggregate. `LiveActivityToken` stands. Resolved. |
| CH-D3-2 (nullable-OrderId smell) | **CONCEDED as smell, split REJECTED for v1** — revisit trigger recorded (push-to-start attributes). Resolved with condition. |
| CH-D4-1 (value without ETA) | **DEFENDED on architecture; value routed to the owner** via the ratification questions this draft status exists for. Resolved. |
| CH-D4-2 (17.2 fork) | **CONCEDED + REVISED** — named consequence + QA line + ratification Q2. Resolved. |
| Producer seam, consumer separation, tenancy, S6 allowlist, ADR-0014 floor | **Checked by the challenger, found sound — no challenge raised.** |

**On acceptance (not before):** create the three role files + update
`agents/architecture/decisions/push-notifications.md` (§Roles affected); PM slices T-0427 into the
backend lane (entity/migration MANUAL_STEP, endpoint, queue+consumer, APNs client, TC-LA-0..6) and
the iOS lane (widget target + xcodegen MANUAL_STEP, token observers, registration, TC-LA-7, QA
matrix) — backend deployable inert (`APNS:Enabled=false`) ahead of the iOS train. *(2026-07-19: the
slice specs are drafted and appended to T-0427 — implementation still starts only on acceptance.)*

---

## Re-verification round (2026-07-19 — code drift since the 2026-07-17 draft)

*(Second panel round, convened because two things moved under the draft: the notifications
feed + producer-seam consolidation (`INotificationProducer` + tripwire) shipped, and the owner's
`.p8` was delivered as a backend secret and confirmed working for ordinary push on live dev. Author
re-grounded every named seam in the working tree; challenges steelmanned below; lead ruled.)*

**Drift corrected in the body (author, evidence at file:line):**
- **Producer mechanism** — handlers hold `INotificationProducer`, not `IPendingDispatch`
  (`NotifyOnTheWay.cs:86`, `StartOrder.cs:115`); D2's "one added `pending.Enqueue`, no new
  collaborator" was stale → reshaped to the sibling `ILiveActivityProducer` seam + second tripwire.
- **`AdminOverrideOrderStatus`** is strict-forward-only and **rejects Cancelled**
  (`AdminOverrideOrderStatus.cs:96-105`) and produces **no notification today** → the Cancelled row
  names CancelOrder/AdminCancelOrder only; the override gains its *first* producer call for
  forward moves; the claim-key `Sequence` rationale downgraded from load-bearing to defensive.
- **Cancel alerts are refund-conditional** (`CancelOrder.cs:139-152`) → the activity end-enqueue is
  pinned *outside* the conditional.
- **The `.p8` gate** — T-0342's remaining substance collapsed from "commission a new custody step"
  to "bind six `APNS:*` config keys" (now enumerated exactly, D1) + extension provisioning.
- **Port/config placement made exact** — `Cleansia.Core.Clients.Abstractions/Apns/`,
  `ApnsConfig : AutoBindConfig("APNS")` in Infra.Common, registered in `ConfigurationExtensions`.
- **Poison pair added** — `live-activity-dispatch-poison` + handler (ADR-0002 D3/F3 requires it;
  the draft omitted it; every shipped queue has one, `Program.cs:85-90`).

### RV-1 (challenger) — A second producer seam re-fragments what the consolidation just unified.
The platform just collapsed ALL push production into one tripwire-pinned seam so no producer can
ship a push without its feed row. Adding `ILiveActivityProducer` puts a second enqueue seam beside
it — the next developer must know which of two seams to call. Why not a second method on
`INotificationProducer` so there is still exactly one producer-facing surface?

**Defense — REBUT.** The consolidation's invariant is *"feed row + alert push, atomically, gated by
notification preferences"* — an activity update has **none** of those parts: no feed row (it is not
an inbox item), no preference gating (D4, deliberate), a different queue, key, and failure domain.
Folding it into `INotificationProducer` would make that interface's contract disjunctive ("does the
atomic feed+push thing, OR silently something entirely different"), force the notify seam to know
ActivityKit vocabulary, and re-couple the two channels' retry semantics at the exact boundary
ADR-0023 keeps separate. What the consolidation actually killed was *N ad-hoc construction sites
per message type* — and this ADR applies that same cure to the new channel: **one construction site
per channel, each tripwire-pinned**. Two seams for two channels is the pattern, not a regression.

### RV-2 (challenger) — The override handler gains a producer call the alert channel doesn't have:
now an admin override moves the lock-screen card but sends no push — an inconsistency shipped by
design. Either both channels fire there or neither; picking one smells arbitrary.

**Defense — REBUT (asymmetry is the point).** An alert is an *event*; a Live Activity is *state*.
Missing an event notification for an admin override is a product gap (real, pre-existing, not
commissioned here — the PM can ticket it independently); a stateful card left saying "in progress"
after the order was overridden to Completed is a **lie pinned to the lock screen** — the exact
CH-D2-2 trust surface, now self-inflicted. State must converge on every transition source; events
may be curated. Cost: one collaborator in a handler that has three.

### RV-3 (challenger) — `APNS:Enabled` invents a second disable idiom; FCM disables on empty
credential. Two conventions for the same concept is how config drifts.

**Defense — REBUT with the changed fact.** FCM's empty-credential idiom worked because the secret
and the feature arrived together. Here the secret **already exists in backend custody today**
(delivered for ordinary push) while the iOS lane is unbuilt — under the FCM idiom, binding the
`APNS:*` keys would light the channel the moment ops copies the secret, with zero client code to
receive it. `Enabled=false` is the deliberate inert-ship lever the backend-first deploy depends on
(Dependencies §3); the empty-credential no-op is *retained* as the second guard (both map to the
same Skipped-ack semantics). Divergence acknowledged and priced: one boolean, documented in the D1
table, mirrored nowhere else.

### RV-4 (challenger) — The claim key carries a `Sequence` segment whose justifying scenario was
just proven impossible (`targetRank <= currentRank` rejects revisits). Frozen formulas should not
carry dead segments.

**Defense — CONCEDE the rationale, KEEP the segment.** The 07-17 rationale ("admin-override
revisit") was wrong and the body now says so. The segment stays because the formula is **frozen on
ship** (MessageKeys doc: changing a formula is a superseding ADR) — a defensive segment costs
nothing now; removing it and later needing it costs an ADR + a dual-read migration. Pure-function
property intact (`Sequence` is deterministic domain state, not a timestamp).

### Checked again and found sound (named — silence is not assent)
- `Order.AddOrderStatus` still the single append seam at `Order.cs:344`; `OrderStatusTrack.Sequence`
  deterministic (`OrderStatusTrack.cs:17`).
- `SendPushNotificationHandler.cs` matches the draft's Mode-A/claim-first/Skipped-ack description
  verbatim (claim at :90, Skipped-ack at :149, prune at :176) — the sibling-consumer blueprint holds.
- `Cleansia.Infra.Clients` holds exactly Fcm/SendGrid/Stripe — `Apns/` is genuinely new; the
  ADR-0005 contract reference is current.
- `Device`/`DeviceController` (customer mobile host) unchanged as the alert registration surface —
  D3's "don't overload `Device`" analysis unaffected by the drift.
- The `AutoBindConfig` + `ConfigurationExtensions` config archetype is as assumed (verified
  `FcmConfig.cs`, `ConfigurationExtensions.cs:17`).

### Lead ruling (2026-07-19)
RV-1/RV-2/RV-3 **DEFENDED** with evidence; RV-4 **CONCEDED + REVISED** (rationale corrected in
place, segment retained with the honest justification). Zero blocking challenges. The 2026-07-17
consensus **stands on the corrected body**. Status advances `draft` → **`proposed
(ratification-ready)`**: ratification Q1 is resolved by the owner's own conduct (key delivered +
working); Q2/Q3 remain the owner's product calls, each carrying a recommended default so
ratification is a yes/no, not a design session. Implementation remains gated on acceptance.

---

## Status log

- 2026-07-17 — drafted; panel round 1 (11 challenges: 5 defended, 5 conceded+revised, 1
  conceded-with-condition); consensus; status `draft — awaiting owner ratification`.
- 2026-07-19 — **hardened against the shipped codebase + re-verification panel round.** Corrected
  drift: producer seam reshaped to a sibling `ILiveActivityProducer` beside the shipped
  `INotificationProducer` consolidation (+ second tripwire); `AdminOverrideOrderStatus` reality
  (forward-only, no Cancelled, no existing notify call) folded into D2's map and the claim-key
  rationale; cancel-path enqueue pinned outside the refund conditional; poison queue pair added
  (ADR-0002 D3/F3); exact `APNS:*` config keys + `IApnsConfig` placement pinned. Resolved the `.p8`
  gate: the key is uploaded, in backend secret custody, and confirmed working for ordinary push on
  live dev — same key serves `liveactivity`; ratification Q1 closed by conduct. Q2/Q3 stay OPEN
  with recommended defaults. Implementation plan drafted as slices LA-1…LA-5 appended to T-0427
  (build starts only on acceptance). Status → **`proposed (ratification-ready)`** — the owner
  ratifies.
