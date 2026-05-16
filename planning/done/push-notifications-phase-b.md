# Push Notifications — Phase B+ event scaffolding

> Phase A (Confirmed / OnTheWay / InProgress / Completed / Cancelled / Refunded
> / DisputeReply) shipped on `feat/customer-android-app`. The catalog +
> dispatcher + Function consumer are all in place, so adding new events is a
> follow-the-pattern exercise.

## Status — Phase B SHIPPED 2026-05-15

All 4 Phase B events now in production. See
"`promo.new_sitewide` — SHIPPED 2026-05-15" section at the bottom for the
fan-out architecture of the final event.

### Original status snapshot (2026-05-12)

- ✅ `loyalty.tier_upgrade` — shipped. `LoyaltyService.GrantForCompletedOrderAsync`
  snapshots `previousTier` before the grant and enqueues the push when
  `CurrentTier > previousTier`. Tier name comes through as the enum identifier
  (`SilverMopper` etc.) and the mobile `resolveTierLabel` maps to localized
  `loyalty_tier_*` strings.
- ✅ `membership.expiring_soon` — shipped. Sweep handler
  `SendMembershipLifecycleNotifications` queries Active subs whose
  `CurrentPeriodEnd` falls in [now+2d, now+4d] and the per-period
  `RenewalReminderSentAt` is null. Stamp resets when Stripe webhooks roll
  the period forward (`UpdateFromStripeWebhook`) or on plan swap.
- ✅ `membership.cancellation_effective` — shipped via the same sweep.
  Queries Active subs with `CancelledAt != null` and `CurrentPeriodEnd` in
  [now, now+2d] and `CancellationReminderSentAt` null. Cleared on plan swap
  so a retracted cancellation re-arms.
- ✅ `promo.new_sitewide` — **shipped 2026-05-15**. Admin "Marketing →
  Sitewide Push" form composes 5-locale title+body; backend
  `SendSitewidePromo` command enqueues a single `SendSitewidePromoMessage`
  on the new `sitewide-promo-fanout` queue; `SendSitewidePromoFanoutFunction`
  pages opted-in users (`Promo == true`) joined with `User.PreferredLanguageCode`
  and emits one `SendPushNotificationMessage` per recipient on the existing
  `notifications-dispatch` queue with locale-matched title+body in `Args`.
  Mobile `CleansiaFirebaseMessagingService` reads title+body straight from
  the FCM data payload for this one event (the only Phase B event whose
  body isn't a fixed mobile-side template).

### Manual steps owner owes

- **EF migration** — `UserMembership` gained two nullable timestamps
  (`RenewalReminderSentAt`, `CancellationReminderSentAt`). Generate +
  apply migration before deploying.
- **AppHost queue** — `notifications-dispatch` already declared (Phase A);
  no new queue needed.
- **Functions host registration** — `SendMembershipLifecycleNotificationsFunction`
  picks up automatically (no manual DI wiring needed under the Functions
  worker host), but verify it shows in the function list at startup.

### Verification

- `dotnet build src/Cleansia.Core.AppServices/Cleansia.Core.AppServices.csproj` — 0 errors.
- `./gradlew :app:compileDebugKotlin :app:testDebugUnitTest` — 55 tests green.

## `promo.new_sitewide` — SHIPPED 2026-05-15

How each of the four pre-ship blockers was resolved:

1. **Admin UI** ✅ — New `@cleansia/admin-features/marketing` lib with a
   single `SitewidePushFormComponent`. Top-level "Marketing" sidebar entry
   with "Sitewide Push" child. The form has 5 title inputs + 5 textarea
   bodies and a confirm dialog before submit. Currently uses raw
   `HttpClient.post` against `/api/AdminMarketing/send-sitewide-promo`;
   swap to `adminClient.adminMarketingClient.sendSitewidePromo(command)`
   after NSwag regen.
2. **Marketing-category opt-in** ✅ — Fan-out Function filters
   `UserNotificationPreferences.Promo == true` before enqueueing.
   `UserNotificationPreferences.Promo` already defaults to `false`
   (opt-in) per the entity's original design.
3. **Throttling** ✅ — Fan-out lives in
   `SendSitewidePromoFanoutFunction` (queue-triggered). Pages 200 users
   per round-trip, enqueues per-user `SendPushNotificationMessage`
   inline. Synchronous request returns immediately after enqueueing the
   single fan-out message. Per-user enqueue failures are logged and
   skipped (one bad row doesn't poison-message the whole campaign).
4. **Body i18n** ✅ — Backend `SendSitewidePromo.Command` accepts 5
   locale variants of (title, body). Fan-out Function picks the matching
   locale per recipient from `User.PreferredLanguageCode` (`en`/`cs`/`sk`/`uk`/`ru`,
   `en` fallback). Selected title+body land in the per-user dispatch
   message's `Args` dictionary as keys `title` and `body`. Mobile
   `CleansiaFirebaseMessagingService.onMessageReceived` branches on
   `event_key == "promo.new_sitewide"` to read those keys directly,
   bypassing the local `templateFor`/`strings.xml` lookup the other
   events use.

### Manual steps owed by owner

- **NSwag regen for admin client.** Run
  `npm run generate-admin-client` from `src/Cleansia.App/`. After regen,
  swap the raw `HttpClient.post` in
  `sitewide-push-form.component.ts:send()` to
  `adminClient.adminMarketingClient.sendSitewidePromo(...)`.
- No EF migration — no new schema.
- No new queue declaration in Aspire `AppHost/Program.cs` — emulated
  Azure Storage auto-creates `sitewide-promo-fanout` on first send.
- After deploy: smoke-test with a low-fanout campaign (single opt-in
  admin user) before broadcasting.


## Pattern

For each new event:

1. **Add event key constant** to `Cleansia.Core.Domain/Notifications/NotificationEventCatalog.cs`
2. **Add category mapping** in the same file's `GetCategoryFor` switch
3. **Add category enum value** to `NotificationCategory.cs` if new category
4. **Add per-user preference toggle** to `UserNotificationPreferences.cs` (field + IsAllowed/SetAllowed branches)
5. **Enqueue from the triggering handler** — `queueClient.SendAsync(QueueNames.NotificationsDispatch, new SendPushNotificationMessage(...))`
6. **Customer mobile `templateFor`** entry in `CleansiaFirebaseMessagingService.kt` mapping event_key → (titleRes, bodyRes, category)
7. **5 i18n strings** × 2 fields (title + body) added to `values{,-cs,-sk,-uk,-ru}/strings.xml`
8. **Customer mobile `NotificationDeepLink.resolve`** entry mapping event_key → typed Compose route (if it should deep-link somewhere)

## Phase B events to wire

| Event key | Trigger site | Deep link target |
|---|---|---|
| `membership.expiring_soon` | Background job (3 days before renewal/expiry) | `Routes.SubscribePlus` |
| `membership.cancellation_effective` | Background job (1 day before cancel takes effect) | `Routes.SubscribePlus` |
| `loyalty.tier_upgrade` | `LoyaltyService.GrantForCompletedOrderAsync` after tier promotion detected | `Routes.RewardsActivity` |
| `recurring.scheduled` | `RecurringBookingBackgroundService` when next instance auto-scheduled (24h ahead) | `Routes.OrderDetail(newOrderId)` |
| `promo.new_sitewide` | Admin "send sitewide promo" action (admin UI not built yet) | Home (no deep link) |

## Suggested order

1. `loyalty.tier_upgrade` — easiest, single trigger site, one notification
2. `membership.expiring_soon` + `membership.cancellation_effective` — share infra
3. `recurring.scheduled` — needs a new background job
4. `promo.new_sitewide` — admin UI work + opt-in marketing-category logic

## Out of scope for follow-up

- iOS push (no iOS app)
- Web push (browser Push API)
- SMS / email fallbacks (already handled by existing email templates)
