# Partner "new jobs available" notifications — targeted + throttled

**Created:** 2026-05-29
**Status:** PLANNED (not started)
**Depends on:** MOB-P-NOTIF (partner FCM stack — code shipped, owner-gated on `google-services.json` + Firebase console). This feature is the *backend dispatch* half that MOB-P-NOTIF's report flagged as missing ("backend has no partner-targeted dispatch").

## Problem

Cleaners should learn about new available jobs via push, but:
- **Targeting:** only cleaners who could plausibly take the order (right jurisdiction, approved, not already busy) — not every cleaner.
- **Throttling:** batched, not one push per order. A burst of 5 orders ⇒ 1 "5 new jobs" push, at most one digest per cleaner per interval.

## Decisions (owner, 2026-05-29)

- **Mechanism:** periodic **digest** (not instant-per-order). Best fit for the existing `TimerTrigger Function → background service` pattern; avoids needing a per-event "last notified" dedup store on the hot path.
- **Cadence:** every **30 minutes**, push only to cleaners with ≥1 newly-eligible order since their last digest. No new jobs ⇒ no push.
- **Targeting v1:** work country + approved/active + not-already-busy. **Proximity deferred to v2** (no Haversine util exists; lat/long nullable on old rows).
- **Opt-out:** new `NotificationCategory.NewJobsAvailable` + bool column on existing `UserNotificationPreferences`; digest honors `IsAllowed`. Mobile prefs UI is a later task.

## Why digest over instant (recorded rationale)

The pipeline is single-recipient, customer-only by construction: `SendPushNotificationMessage` carries one `UserId`; every `order.*` enqueue hard-codes `order.UserId`. Instant per-order cleaner push would need a new "last-notified-at" store to rate-limit (greenfield — nothing tracks this today). The digest sidesteps that: the 30-min sweep IS the rate limit, and a per-cleaner watermark ("last digest sent at") is the only new state needed.

## Existing building blocks (reuse, don't reinvent)

- **Fan-out template:** [`SendSitewidePromoFanoutFunction.cs`](../../src/Cleansia.Functions/Functions/SendSitewidePromoFanoutFunction.cs) — pages opted-in users (PageSize 200), enqueues one `SendPushNotificationMessage` per recipient on `notifications-dispatch`. The digest is structurally identical with a different recipient query.
- **TimerTrigger pattern:** `PeriodReminderTimerFunction` + [`PeriodReminderBackgroundService.cs`](../../src/Cleansia.Core.AppServices/Services/PeriodReminderBackgroundService.cs) — sweep employees, dispatch per-recipient. Mirror this.
- **Queue + dispatch:** `notifications-dispatch` queue, `SendPushNotificationMessage(UserId, EventKey, Args, TenantId?)`, [`SendPushNotificationFunction`](../../src/Cleansia.Functions/Functions/SendPushNotificationFunction.cs) consumer (checks prefs → loads devices → multicasts), [`FcmPushDispatcher`](../../src/Cleansia.Infra.Clients/Fcm/FcmPushDispatcher.cs) (multicast already supported).
- **Available-orders predicate:** [`DashboardSpecifications.CreateAvailableOrdersSpec`](../../src/Cleansia.Core.AppServices/Features/Dashboard/DashboardSpecifications.cs) — status ∈ {Pending, Confirmed}, has free spots, not-already-assigned-to-me. (Does NOT scope by work-country today — digest adds that.)
- **Not-busy check:** `IOrderRepository.HasOverlappingOrderAsync(employeeId, cleaningDateTime, estimatedTime)` ([`OrderRepository.cs:149`](../../src/Cleansia.Infra.Database/Repositories/OrderRepository.cs#L149)).
- **Prefs table:** [`UserNotificationPreferences.cs`](../../src/Cleansia.Core.Domain/Notifications/UserNotificationPreferences.cs) — employees are Users, so reusable.
- **Mobile side:** MOB-P-NOTIF already handles arbitrary `event_key`s and persists to the Room feed. Add one new key + its strings.

## Tasks

### NJN-1 — Notification category + prefs (backend) [S]
- Append `NewJobsAvailable` to [`NotificationCategory.cs`](../../src/Cleansia.Core.Domain/Notifications/NotificationCategory.cs) (append only — enum forbids renumbering).
- Add bool column to `UserNotificationPreferences` (default **true** for cleaners). Map in the entity + `IsAllowed`/`Set`.
- **MANUAL_STEP (owner):** EF migration `AddNewJobsAvailablePreference`.

### NJN-2 — Event key + catalog [XS]
- Add `order.new_available` (or `jobs.new_available`) to [`NotificationEventCatalog`](../../src/Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs) → `NewJobsAvailable` category.
- The digest is a *count* notification ("N new jobs available near you"), so `Args` carries `count` (+ optional `topPay`). Body localized client-side.

### NJN-3 — Per-cleaner digest watermark [S]
- Add `LastNewJobsDigestAt: DateTimeOffset?` to `Employee` (the only new throttle state needed). Updated when a digest is sent to that cleaner.
- **MANUAL_STEP (owner):** EF migration `AddEmployeeLastNewJobsDigestAt`.
- *(Alt considered: separate `NotificationWatermark` table — rejected as overkill for one timestamp.)*

### NJN-4 — Eligible-cleaners + new-jobs query (backend) [M]
- New query/service: for the sweep, find cleaners who are `ContractStatus ∈ {Approved, Active}` AND have `WorkCountryId` set, joined against orders that:
  - match the available-orders spec (Pending/Confirmed, free spots, not assigned to them),
  - are in the cleaner's `WorkCountryId` (derive order country from `Order.CustomerAddress.CountryId`),
  - became available **after** the cleaner's `LastNewJobsDigestAt`,
  - exclude orders that overlap an existing in-progress order of theirs (`HasOverlappingOrderAsync`).
- Returns per-cleaner: count of new eligible orders (+ maybe max pay). Cleaners with 0 are skipped.
- **Index:** add `ContractStatus` index on Employee (currently only `WorkCountryId` is indexed) — the sweep filters on it.
- **MANUAL_STEP (owner):** migration for the index (can fold into NJN-3's migration).

### NJN-5 — Digest TimerTrigger + background service [M]
- `SendNewJobsDigestTimerFunction` (TimerTrigger, every 30 min — interval in appsettings per "configurable" note; default `0 */30 * * * *`).
- `NewJobsDigestService`: run NJN-4 query → for each eligible cleaner, check `UserNotificationPreferences.IsAllowed(NewJobsAvailable)` → enqueue `SendPushNotificationMessage(cleaner.UserId, "order.new_available", {count}, tenantId)` → set `LastNewJobsDigestAt = now`. Page recipients (PageSize 200) like the promo fanout.
- Tenant-aware: use `GetQueryableIgnoringTenant()` + set tenant override on each message, mirroring `PeriodReminderBackgroundService`.

### NJN-6 — Mobile strings + feed handling [XS]
- Partner `CleansiaFirebaseMessagingService`: handle `order.new_available` → title/body from `strings.xml` ("New jobs available", "%d new jobs near you"), category channel, deep-link to the Orders tab (Available pane) rather than a single order.
- Add the 2 strings × 5 locales (key parity).
- Add `order.new_available` to the partner notification template map + `NotificationDeepLink` (→ Orders/Available, not OrderDetails).

### NJN-7 — Verify [S]
- `dotnet build src/Cleansia.Api.sln` green (file-lock errors from running hosts OK).
- `:partner-app:compileDebugKotlin` green.
- Manual: with a seeded approved cleaner + a fresh Confirmed order in their work country, the 30-min sweep produces exactly one queued push; running it again with no new orders produces none; muting the category produces none.

## Out of scope (v2+)
- **Proximity / radius targeting** — needs a Haversine helper + null-lat/long fallback + per-cleaner compute. Fast-follow once the digest is proven.
- **Instant (non-digest) push** for high-value/urgent jobs.
- **Mobile notification-preferences UI** — the category exists server-side; a toggle screen is separate.
- **Quiet hours.**

## Sequencing
NJN-1 + NJN-2 + NJN-3 (schema/enum, one migration batch, owner-applied) → NJN-4 (query) → NJN-5 (timer/service) → NJN-6 (mobile) → NJN-7 (verify). NJN-6 can proceed in parallel after NJN-2 defines the event key.

## Manual steps the owner owns
- EF migrations (NJN-1 prefs column, NJN-3 watermark + NJN-4 index — can be one migration).
- No NSwag/OpenAPI regen needed (no new client-facing DTO; digest is server→push only).
- Firebase config from MOB-P-NOTIF still required for any of this to deliver.
