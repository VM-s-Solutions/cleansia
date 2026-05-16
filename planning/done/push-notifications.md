# Push Notifications — Customer Android + Backend

> Plan only. No code lands from this document — it's the spec a future
> `/execute` (or hand implementation) runs against.

## 1. Decisions up front

### Provider: **FCM HTTP v1 directly via Admin SDK**
- **Not** OneSignal / Pusher / Airship — adding a 4th vendor SDK to mobile (Sentry, Mapbox, Stripe already there) and routing transactional notifications through a third-party black box is unjustified for a 12-trigger MVP. Vendors shine for marketing CRM features we don't need yet.
- **Not** raw HTTP/2 to FCM — Google's Admin SDK handles auth-token rotation, batching, and retry quirks. ~5 lines of glue vs maintaining a custom HTTP client.
- **Yes** Firebase Cloud Messaging via `FirebaseAdmin` NuGet (server) + `firebase-messaging-ktx` (client). Free tier covers the entire foreseeable load.

### Topology: queue + Function (mirrors existing receipt/invoice pattern)
- Status-change handler enqueues a `SendPushNotificationMessage` to `notifications-dispatch` queue.
- New `SendPushNotificationFunction` (in `Cleansia.Functions/Functions/`) consumes the queue, looks up active `Device` rows for the user, calls `IPushDispatcher.SendAsync` (Admin SDK wrapper).
- **Why queue not in-process:** dispatch should not block the order-status request, must survive transient FCM 5xx, and benefits from Azure Functions retry+poison handling already used by receipts. Same operational story.

### Device storage: extend existing `Device` entity (don't create `UserDeviceToken`)
- `Device` already has `UserId`, `Platform`, `DeviceToken`, `DeviceId`, `LastActiveAt`. Currently unused — perfect FCM-token holder.
- Add ONE column: `NotificationsEnabled` (bool, default true) — the system-level kill switch when the user revokes notification permission at the OS level.
- Per-event preferences live on a NEW `UserNotificationPreferences` entity (not `Device`) because preferences follow the user, not the handset.

### Domain-event hook: extend `Order.AddOrderStatus`, not MediatR INotification
- 8 distinct sites already call `OrderStatusTrack.Create(...) + order.AddOrderStatus(...)`. The single funnel is `Order.AddOrderStatus(track)` on the entity itself.
- Add a domain-side `INotificationDispatcher` injected into the `CreateOrder.Handler` / `TakeOrder.Handler` / etc. AFTER `unitOfWork.CommitAsync()` succeeds (post-commit dispatch is non-negotiable — never enqueue inside a transaction that might roll back).
- Cleaner: each handler that calls `AddOrderStatus` also enqueues. 8 call-site touches but explicit + traceable. **No INotification framework added.**
- For non-order events (membership expiry, recurring scheduled, tier upgrade, dispute reply), enqueue from each respective handler (4 additional sites).

### Notification logging: **NO** `NotificationLog` entity in MVP
- FCM Admin SDK returns success/failure synchronously. Failures (token invalid → 410) prune the `Device` row. Permanent log is over-engineering for v1.
- Sentry breadcrumbs carry the dispatch trail for crash forensics.
- Revisit IF support tickets need "did the user receive notification X" — at that point add `NotificationLog` as a separate spec.

### i18n strategy: **Resource files (mobile-side localization)**
- 12 events × 5 locales × 2 fields = 120 strings. Tracking these in DB risks inconsistency with the rest of the app (which uses .json files for web, strings.xml for Android).
- Backend sends an EVENT KEY ("order.confirmed") + structured args ("orderNumber=#A123", "cleanerFirstName=Jana"). Mobile resolves the key against `strings.xml` in the device's preferred language at display time.
- Backend NEVER ships pre-formatted body strings. Single source of truth for translations is mobile resource files; web app + backend reuse the same keys via the existing i18n pipeline if/when iOS / web push lands.

### Privacy: data-only payloads
- Notification payload format: `data: { event_key, ...args }` — NO `notification` field.
- `MessagingService.onMessageReceived` builds the local notification, looking up title/body in `strings.xml`. Lock-screen text is generic when sensitive; OS shows whatever WE choose to show, not what FCM injects.
- This also fixes the iOS-future story: data-only payloads route through `UNNotificationServiceExtension` for mutation, same shape.

---

## 2. Architecture

```
┌──────────────────────────────────────────────────────────────────────────┐
│ Customer API (Cleansia.Web.Customer)                                     │
│                                                                          │
│   PaymentController                                                      │
│   OrderController                                                        │
│   DeviceController         POST /api/Device/Register   ──┐               │
│                            POST /api/Device/Unregister   │ NEW endpoints │
│   NotificationPrefsCtrl    GET  /api/NotificationPrefs   │               │
│                            PUT  /api/NotificationPrefs ──┘               │
└──────────────────────┬───────────────────────────────────────────────────┘
                       │
                       ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ Cleansia.Core.AppServices                                                │
│                                                                          │
│   CreateOrder.Handler ─┐                                                 │
│   TakeOrder.Handler    │                                                 │
│   StartOrder.Handler   │ post-commit:                                    │
│   CompleteOrder.Handler│ queueClient.SendAsync(NotificationsDispatch,    │
│   CancelOrder.Handler  │   new SendPushNotificationMessage(...))         │
│   AddDisputeMessage.H. │                                                 │
│   ...                ──┘                                                 │
└──────────────────────┬───────────────────────────────────────────────────┘
                       │ Azure Storage Queue: "notifications-dispatch"
                       ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ Cleansia.Functions / SendPushNotificationFunction                        │
│   1. Deserialize SendPushNotificationMessage                             │
│   2. Look up user's active Device rows where NotificationsEnabled=true   │
│   3. Filter by UserNotificationPreferences for this event category       │
│   4. Build data-only payload {event_key, args}                           │
│   5. IPushDispatcher.SendAsync(tokens, payload)                          │
│   6. On 410 NotRegistered → delete that Device row                       │
└──────────────────────┬───────────────────────────────────────────────────┘
                       │ FirebaseAdmin SDK / FCM HTTP v1
                       ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ Customer Android device                                                  │
│   CleansiaFirebaseMessagingService.onMessageReceived(data)               │
│     1. Lookup title/body in strings.xml by event_key + args              │
│     2. Build NotificationCompat with deep-link Intent                    │
│     3. Show local notification                                           │
│   onNewToken(token):                                                     │
│     POST /api/Device/Register                                            │
│   MainActivity.onNewIntent(intent): parse deep-link → navController      │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## 3. Task Specs

### Phase 1: Backend foundations

```yaml
task: 'Add NotificationsEnabled to Device, plus NotificationPreferences entity'
id: PUSH-001
type: feature
priority: high
specialist: backend
estimated_complexity: small
recommended_model: sonnet
context: |
  Existing Device entity (src/Cleansia.Core.Domain/Devices/Device.cs)
  already has UserId, Platform, DeviceToken, DeviceId, LastActiveAt — needs
  one extra bool. UserNotificationPreferences is a new entity per user
  carrying 12 boolean toggles (one per event category; categories list
  below in PUSH-007 i18n table).
files_to_modify:
  - path: 'src/Cleansia.Core.Domain/Devices/Device.cs'
    line_range: '6-10'
    change: 'Add `public bool NotificationsEnabled { get; private set; } = true;` and an `UpdateNotificationsEnabled(bool)` setter.'
  - path: 'src/Cleansia.Infra.Database/EntityConfigurations/DeviceEntityConfiguration.cs'
    change: 'Add property mapping for NotificationsEnabled with default true.'
files_to_create:
  - 'src/Cleansia.Core.Domain/Notifications/UserNotificationPreferences.cs'
  - 'src/Cleansia.Core.Domain/Notifications/NotificationCategory.cs (enum: OrderUpdates, CleanerOnTheWay, OrderCompleted, OrderCancelled, RefundIssued, MembershipExpiring, MembershipCancelled, TierUpgrade, Promo, DisputeReply, RecurringScheduled — 11 categories; Order"taken" lumps with OrderUpdates)'
  - 'src/Cleansia.Infra.Database/EntityConfigurations/UserNotificationPreferencesEntityConfiguration.cs'
  - 'src/Cleansia.Core.Domain/Repositories/IUserNotificationPreferencesRepository.cs'
  - 'src/Cleansia.Infra.Database/Repositories/UserNotificationPreferencesRepository.cs'
dependencies: []
verification:
  - 'dotnet build src/Cleansia.Api.sln'
manual_steps_required:
  - 'EF migration after PUSH-001'
```

```yaml
task: 'Push-notification dispatcher abstraction + FCM impl'
id: PUSH-002
type: feature
priority: high
specialist: backend
estimated_complexity: medium
recommended_model: sonnet
context: |
  IPushDispatcher.SendAsync(tokens, eventKey, args) returns
  PushDispatchResult { Success, InvalidTokens }. The Function uses
  InvalidTokens to prune dead Device rows. FirebaseAdmin SDK reads
  service-account JSON from `FCM_SERVICE_ACCOUNT_JSON` config secret.
  Localized: NO — backend just ships event_key + args; mobile localizes.
files_to_create:
  - 'src/Cleansia.Core.AppServices.Abstractions/Notifications/IPushDispatcher.cs'
  - 'src/Cleansia.Core.AppServices.Abstractions/Notifications/PushDispatchResult.cs'
  - 'src/Cleansia.Infra.Services/Notifications/FcmPushDispatcher.cs (uses FirebaseAdmin.Messaging)'
  - 'src/Cleansia.Infra.Services/Notifications/FcmConfig.cs (binds FCM:ServiceAccountJson + FCM:ProjectId)'
files_to_modify:
  - path: 'src/Cleansia.Infra.Services/Cleansia.Infra.Services.csproj'
    change: 'Add <PackageReference Include="FirebaseAdmin" Version="..." />'
  - path: 'src/Cleansia.Infra.Services/ServiceCollectionExtensions.cs'
    change: 'Register IPushDispatcher → FcmPushDispatcher as singleton; bind FcmConfig from config section "FCM"'
dependencies: ['PUSH-001']
verification:
  - 'dotnet build'
  - 'Unit test: FcmPushDispatcher with InvalidArgument exception → returns InvalidTokens'
```

```yaml
task: 'Queue message + Function consumer for push dispatch'
id: PUSH-003
type: feature
priority: high
specialist: backend
estimated_complexity: medium
recommended_model: sonnet
context: |
  Mirrors GenerateReceiptFunction exactly. Message carries UserId +
  EventKey + args (Dictionary<string,string>). Function loads the user's
  active Device rows where NotificationsEnabled = true AND the
  UserNotificationPreferences row allows the event's category, calls
  IPushDispatcher.SendAsync, prunes 410-marked tokens.
files_to_create:
  - 'src/Cleansia.Core.Queue.Abstractions/Messages/SendPushNotificationMessage.cs (record with UserId, EventKey, Args, optional TenantId for cross-tenant lookup)'
  - 'src/Cleansia.Functions/Functions/SendPushNotificationFunction.cs'
files_to_modify:
  - path: 'src/Cleansia.Core.Queue.Abstractions/QueueNames.cs'
    change: 'Add `public const string NotificationsDispatch = "notifications-dispatch";`'
dependencies: ['PUSH-001', 'PUSH-002']
verification:
  - 'dotnet build'
  - 'Unit test: function deserializes message + filters by user prefs'
```

```yaml
task: 'RegisterDevice + UnregisterDevice CQRS commands (extend existing Devices feature)'
id: PUSH-004
type: feature
priority: high
specialist: backend
estimated_complexity: small
recommended_model: sonnet
context: |
  src/Cleansia.Core.AppServices/Features/Devices/RegisterDevice.cs already
  exists. Verify it idempotently upserts on (UserId, DeviceId) and stores
  the FCM token in DeviceToken. UnregisterDevice must delete the row
  on logout — not just mark inactive — so a re-installed app with new
  device_id starts fresh.
files_to_modify:
  - path: 'src/Cleansia.Core.AppServices/Features/Devices/RegisterDevice.cs'
    change: 'Verify upsert semantics on (UserId, DeviceId). If matches, .UpdateToken(token). If not, create. Set NotificationsEnabled=true on register (user just granted permission).'
  - path: 'src/Cleansia.Web.Customer/Controllers/DeviceController.cs (verify exists; if not, create)'
    change: 'Wire POST /api/Device/Register → mediator → RegisterDevice.Command'
dependencies: ['PUSH-001']
verification:
  - 'dotnet build'
  - 'curl POST /api/Device/Register with mocked JWT → 200 + Device row in DB'
```

```yaml
task: 'GetMyNotificationPreferences + UpdateNotificationPreferences commands'
id: PUSH-005
type: feature
priority: high
specialist: backend
estimated_complexity: small
recommended_model: sonnet
context: |
  Standard CQRS — GetMine reads or creates default-true row; Update
  patches the categories the client supplied. Categories are a flag-bool
  per NotificationCategory enum value.
files_to_create:
  - 'src/Cleansia.Core.AppServices/Features/Notifications/GetMyNotificationPreferences.cs'
  - 'src/Cleansia.Core.AppServices/Features/Notifications/UpdateNotificationPreferences.cs'
  - 'src/Cleansia.Core.AppServices/Features/Notifications/DTOs/NotificationPreferencesDto.cs'
  - 'src/Cleansia.Web.Customer/Controllers/NotificationPreferencesController.cs'
dependencies: ['PUSH-001']
verification:
  - 'dotnet build'
  - 'GET returns defaults on first call; PUT persists changes'
```

```yaml
task: 'Hook order-status changes to enqueue push notification'
id: PUSH-006
type: feature
priority: high
specialist: backend
estimated_complexity: medium
recommended_model: sonnet
context: |
  8 call sites for OrderStatusTrack.Create. Of those, 5 are user-facing
  events worth pushing (the 3 system cleanup ones are NOT user actions).
  Inject IQueueClient into each handler that doesn't already have it
  (most do — used for receipts). Enqueue AFTER unitOfWork.CommitAsync —
  if the commit fails, the notification must not fire.
files_to_modify:
  - path: 'src/Cleansia.Core.AppServices/Features/Orders/TakeOrder.cs'
    line_range: '~173'
    change: 'After commit, queueClient.SendAsync(NotificationsDispatch, new SendPushNotificationMessage(order.UserId, "order.confirmed", { orderId=order.Id, orderNumber=order.DisplayOrderNumber }))'
  - path: 'src/Cleansia.Core.AppServices/Features/Orders/StartOrder.cs'
    line_range: '~115'
    change: 'event_key="order.in_progress"'
  - path: 'src/Cleansia.Core.AppServices/Features/Orders/CompleteOrder.cs'
    line_range: '~166'
    change: 'event_key="order.completed" — payload includes orderId so Mobile deep-links to review CTA'
  - path: 'src/Cleansia.Core.AppServices/Features/Orders/CancelOrder.cs'
    line_range: '~122'
    change: 'event_key="order.cancelled" — only when CancelledByCleaner=true'
  - path: 'src/Cleansia.Core.AppServices/Features/Payments/HandlePaymentNotification.cs'
    line_range: '~367, ~386'
    change: 'event_key="order.confirmed" (line 367) and "order.refunded" (line 386 if refund webhook)'
  - path: 'src/Cleansia.Core.AppServices/Features/Disputes/AddDisputeMessage.cs'
    change: 'When sender is Support and recipient is Customer, enqueue event_key="dispute.reply" with disputeId'
  - path: 'src/Cleansia.Functions/Functions/MaterializeRecurringBookingsFunction.cs'
    change: 'When new booking is materialized 24h ahead, enqueue event_key="recurring.scheduled"'
dependencies: ['PUSH-003']
verification:
  - 'dotnet build'
  - 'Manual: trigger Take order in dev, observe queue message, observe Function log'
```

```yaml
task: 'i18n keys for 12 notification events × 5 locales (Android resources)'
id: PUSH-007
type: feature
priority: high
specialist: mobile
estimated_complexity: small
recommended_model: haiku
context: |
  120 strings total. ALL added as <string> entries in 5 strings.xml
  files. Use string-format placeholders for args: %1$s for orderNumber etc.
  Backend never sends pre-formatted text — it sends event_key + args.
  Naming: notification_<event_key>_title, notification_<event_key>_body.
event_keys_and_copy:
  - event_key: 'order.confirmed'
    title_en: "Cleaner found! 🎉"
    body_en: "%1$s has been confirmed for your booking #%2$s."
  - event_key: 'order.on_the_way'
    title_en: "Your cleaner is on the way"
    body_en: "%1$s is heading to your address now."
  - event_key: 'order.in_progress'
    title_en: "Cleaning in progress"
    body_en: "%1$s has started cleaning. We'll let you know when it's done."
  - event_key: 'order.completed'
    title_en: "All done! ✨"
    body_en: "Your booking #%1$s is complete. Tap to leave a review."
  - event_key: 'order.cancelled'
    title_en: "Booking cancelled"
    body_en: "Your booking #%1$s was cancelled. We're matching you with another cleaner."
  - event_key: 'order.refunded'
    title_en: "Refund issued"
    body_en: "Your refund for booking #%1$s has been processed."
  - event_key: 'membership.expiring_soon'
    title_en: "Cleansia Plus renews in 3 days"
    body_en: "Tap to manage your subscription."
  - event_key: 'membership.cancellation_effective'
    title_en: "Cleansia Plus ends tomorrow"
    body_en: "We'll miss you! Tap to resubscribe and keep your perks."
  - event_key: 'loyalty.tier_upgrade'
    title_en: "You've reached %1$s tier!"
    body_en: "New rewards unlocked. Tap to see what's new."
  - event_key: 'promo.new_sitewide'
    title_en: "New offer: %1$s"
    body_en: "%2$s — tap to see details."
  - event_key: 'dispute.reply'
    title_en: "Support replied"
    body_en: "You have a new message about your dispute. Tap to read."
  - event_key: 'recurring.scheduled'
    title_en: "Cleaning scheduled tomorrow"
    body_en: "Your recurring booking #%1$s is confirmed for %2$s."
files_to_modify:
  - path: 'src/cleansia_customer_android/app/src/main/res/values/strings.xml'
    change: 'Add 24 entries (12 keys × 2 fields).'
  - path: 'src/cleansia_customer_android/app/src/main/res/values-cs/strings.xml'
    change: 'Add cs translations of all 24 entries.'
  - path: 'src/cleansia_customer_android/app/src/main/res/values-sk/strings.xml'
    change: 'sk translations.'
  - path: 'src/cleansia_customer_android/app/src/main/res/values-uk/strings.xml'
    change: 'uk translations.'
  - path: 'src/cleansia_customer_android/app/src/main/res/values-ru/strings.xml'
    change: 'ru translations.'
dependencies: []
verification:
  - "./gradlew :app:compileDebugKotlin"
  - 'No locale parity drift (each strings.xml has same key set)'
```

```yaml
task: 'Add Firebase SDK + AndroidManifest service registration'
id: PUSH-008
type: feature
priority: high
specialist: mobile
estimated_complexity: small
recommended_model: sonnet
context: |
  Standard Firebase setup. google-services.json drops into app/.
  AndroidManifest gains the messaging service + notification permission
  (POST_NOTIFICATIONS for Android 13+).
files_to_modify:
  - path: 'src/cleansia_customer_android/build.gradle.kts'
    change: 'Add classpath "com.google.gms:google-services:4.4.2" to top-level build.gradle.kts (root project).'
  - path: 'src/cleansia_customer_android/app/build.gradle.kts'
    change: 'Apply plugin id("com.google.gms.google-services"). Add dependencies: implementation(platform("com.google.firebase:firebase-bom:33.x")), implementation("com.google.firebase:firebase-messaging-ktx").'
  - path: 'src/cleansia_customer_android/gradle/libs.versions.toml'
    change: 'Add firebase-bom + firebase-messaging-ktx aliases.'
  - path: 'src/cleansia_customer_android/app/src/main/AndroidManifest.xml'
    change: |
      Add <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
      Add inside <application>:
        <service android:name=".core.notifications.CleansiaFirebaseMessagingService"
                 android:exported="false">
          <intent-filter>
            <action android:name="com.google.firebase.MESSAGING_EVENT" />
          </intent-filter>
        </service>
      Add launchMode="singleTop" to MainActivity so onNewIntent fires for
      tap events when app is already running.
dependencies: []
manual_steps_required:
  - 'Owner provisions Firebase project, downloads google-services.json,
    drops into app/. NOT checked into git — gitignore .json blob, ship
    sample at app/google-services.sample.json.'
verification:
  - "./gradlew :app:assembleDebug"
```

```yaml
task: 'CleansiaFirebaseMessagingService — token rotation + receive + display'
id: PUSH-009
type: feature
priority: high
specialist: mobile
estimated_complexity: medium
recommended_model: sonnet
context: |
  Hilt @AndroidEntryPoint service. onNewToken: register with backend
  (POST /api/Device/Register). onMessageReceived(data): lookup
  strings.xml by event_key + args, build NotificationCompat with deep
  link Intent(MainActivity).putExtra("deep_link_route", payload). Use
  per-category NotificationChannel (Android 8+) so user can mute one
  category from system settings.
files_to_create:
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/CleansiaFirebaseMessagingService.kt'
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/NotificationChannels.kt (registers 11 channels at app startup; matches NotificationCategory enum)'
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/NotificationDeepLink.kt (parses event_key + args → Routes.X)'
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/PushTokenRepository.kt (Hilt singleton; calls DeviceApi.register, persists last-registered token in DataStore so we don onNewToken-storm on every launch)'
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/DeviceApi.kt (Retrofit interface for /api/Device/Register + /Unregister)'
files_to_modify:
  - path: 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/CleansiaApp.kt'
    change: 'In onCreate, call NotificationChannels.registerAll(context). Idempotent — Android dedupes by channelId.'
  - path: 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/MainActivity.kt'
    change: |
      Override onNewIntent(intent: Intent). Read intent.getStringExtra("deep_link_route"); if present, parse via NotificationDeepLink and call navController.navigate(typedRoute). Wire navController as Activity-scoped via a small bridge so onNewIntent can reach it (currently scoped inside setContent).
dependencies: ['PUSH-007', 'PUSH-008']
verification:
  - "./gradlew :app:testDebugUnitTest"
  - 'Manual: send test notification from FCM console with data {event_key: "order.confirmed", orderNumber: "A123", orderId: "01JXX..."} — observe local notification, tap → navigates to Routes.OrderDetail(orderId)'
```

```yaml
task: 'Hook AuthRepository: register token on sign-in, unregister on sign-out'
id: PUSH-010
type: feature
priority: high
specialist: mobile
estimated_complexity: small
recommended_model: sonnet
context: |
  Token stored locally must follow the user's session. On sign-in we
  register token → backend; on sign-out (including ForcedSignOut) we
  unregister so the device stops getting notifications for the previous
  user. Don't FetchToken on every launch — onNewToken handles rotation.
files_to_modify:
  - path: 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/AuthRepository.kt'
    change: |
      After successful login (line ~where current token-store-save is), call
      pushTokenRepository.registerCurrentToken().
      In logout(), call pushTokenRepository.unregisterDevice() BEFORE clearing
      tokens (the API call needs the JWT).
  - path: 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/AuthAuthenticator.kt'
    change: 'No change — unregister already happens via SessionEvent.ForcedSignOut listener wiring (see PUSH-011).'
files_to_create:
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/auth/PushSessionListener.kt (observes SessionEvent.ForcedSignOut, calls pushTokenRepository.unregisterDevice in a fire-and-forget coroutine)'
dependencies: ['PUSH-009']
verification:
  - "./gradlew :app:testDebugUnitTest"
  - 'Manual: log in, verify Device row created. Log out, verify Device row deleted.'
```

```yaml
task: 'Notifications screen wires to backend prefs (replaces local state)'
id: PUSH-011
type: feature
priority: medium
specialist: mobile
estimated_complexity: small
recommended_model: sonnet
context: |
  Existing NotificationsScreen.kt:56-62 keeps state in `var X by remember`
  — pure local. Wire to a new NotificationPreferencesViewModel that reads
  GET /api/NotificationPreferences and writes diffs to PUT.
files_to_create:
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/profile/NotificationPreferencesViewModel.kt'
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/NotificationPreferencesApi.kt'
  - 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/core/notifications/NotificationPreferencesRepository.kt'
files_to_modify:
  - path: 'src/cleansia_customer_android/app/src/main/java/cz/cleansia/customer/features/profile/NotificationsScreen.kt'
    line_range: '54-100'
    change: 'Replace local toggle state with viewModel.preferences StateFlow. Each toggle calls viewModel.updateCategory(NotificationCategory.X, enabled). Debounce updates server-side (300ms) so rapid toggling does not spam PUTs.'
dependencies: ['PUSH-005', 'PUSH-009']
verification:
  - "./gradlew :app:testDebugUnitTest"
```

```yaml
task: 'Membership-expiring + cancellation-effective + tier-upgrade timer notifications'
id: PUSH-012
type: feature
priority: medium
specialist: backend
estimated_complexity: medium
recommended_model: sonnet
context: |
  Add a timer Function that runs once daily, queries memberships
  expiring in 3d / cancellation effective tomorrow, enqueues messages.
  Tier-upgrade is event-based — fires from inside loyalty service when
  thresholds cross, not on a timer.
files_to_create:
  - 'src/Cleansia.Functions/Functions/MembershipReminderTimerFunction.cs (CRON 0 9 * * * — 09:00 UTC daily)'
files_to_modify:
  - path: 'src/Cleansia.Core.AppServices/Services/LoyaltyService.cs (or equivalent — search for tier-progression code)'
    change: 'After tier change is persisted, enqueue event_key="loyalty.tier_upgrade" with newTier=string.'
dependencies: ['PUSH-003']
verification:
  - 'dotnet build'
  - 'Local Function test: timer triggers at next-cron, query selects expected memberships'
```

```yaml
task: 'Promo-code sitewide marketing trigger (admin-fired, opt-in only)'
id: PUSH-013
type: feature
priority: low
specialist: backend
estimated_complexity: small
recommended_model: sonnet
context: |
  Marketing pushes are NOT transactional — they require a fan-out
  ("send to all users where promo_notifications_enabled=true"). Admin
  triggers manually from the admin app. Out of scope for MVP — this is
  Phase 2. Spec preserved here so it does not get lost.
files_to_create:
  - 'src/Cleansia.Core.AppServices/Features/Notifications/Admin/SendSitewidePromoNotification.cs'
  - 'src/Cleansia.Web.Admin/Controllers/NotificationsController.cs'
dependencies: ['PUSH-003', 'PUSH-006']
verification:
  - 'manual via admin app'
```

---

## 4. Phased rollout

### MVP (Phase A) — 3 events
Order confirmed (cleaner taken), order completed, dispute reply.

**Why these three:**
- Order confirmed: highest user anxiety moment (booking → silence).
- Order completed: drives review submission (loyalty growth flywheel).
- Dispute reply: support response time visibility.

**Tasks:** PUSH-001, PUSH-002, PUSH-003, PUSH-004, PUSH-005, PUSH-006 (subset: TakeOrder + CompleteOrder + AddDisputeMessage), PUSH-007 (subset: 6 strings × 5 locales = 30), PUSH-008, PUSH-009, PUSH-010, PUSH-011.

### Phase B — order lifecycle complete
Add: cleaner-on-the-way, order in progress, order cancelled, refund.
**Tasks:** PUSH-006 remaining sites, PUSH-007 remaining strings.

### Phase C — membership + loyalty
Add: membership expiring, membership cancellation effective, tier upgrade, recurring scheduled.
**Tasks:** PUSH-012.

### Phase D — marketing
Add: promo sitewide.
**Tasks:** PUSH-013.

---

## 5. Manual steps (owner does these)

```yaml
manual_steps:
  - type: 'firebase_project_setup'
    description: |
      Create Firebase project at console.firebase.google.com.
      Add Android app with package cz.cleansia.customer (and .debug variant).
      Download google-services.json, drop into src/cleansia_customer_android/app/.
      Add app/google-services.json to .gitignore (already true for app/.gitignore — verify).
      Generate service account key: Project Settings → Service accounts →
      Generate new private key → store as FCM_SERVICE_ACCOUNT_JSON secret in
      Aspire user-secrets and CI.
    after_phase: 0
    before_phase: 1
  - type: 'migration'
    description: |
      Generate EF migration AddNotificationsAndDeviceFlags for:
        - Device.NotificationsEnabled column (default true)
        - new UserNotificationPreferences table
      Run dotnet ef migrations add AddNotificationsAndDeviceFlags
        --project src/Cleansia.Infra.Database
        --startup-project src/Cleansia.Web.Customer
      Review the migration, then dotnet ef database update.
    after_phase: 1
    before_phase: 2
  - type: 'nswag_regeneration'
    description: |
      DeviceController + NotificationPreferencesController add new endpoints.
      Regenerate the customer NSwag client. Mobile uses Retrofit interfaces
      hand-written so no regen needed there.
      Run: npm run generate-customer-client
    after_phase: 2
    before_phase: 3
  - type: 'fcm_test_send'
    description: |
      Before shipping: from Firebase console → Cloud Messaging → Send test
      message to the device's registration token. Confirm onMessageReceived
      fires + local notification displays + tap opens MainActivity at the
      right route.
    after_phase: 3
```

---

## 6. Test strategy

### Unit tests
- `FcmPushDispatcherTests` — mock `FirebaseMessaging` instance, assert InvalidArgumentException returns InvalidTokens.
- `SendPushNotificationFunctionTests` — mock IPushDispatcher + repos; assert filters out users with category disabled; asserts pruning of 410'd tokens.
- `NotificationPreferencesViewModelTests` (mobile) — debounce + diff-on-toggle behavior.
- `CleansiaFirebaseMessagingServiceTests` (mobile, Robolectric) — onMessageReceived builds correct NotificationCompat with deep-link Intent extras.

### Integration tests
- `RegisterDeviceIntegrationTest` — POST `/api/Device/Register`, verify Device row exists with correct token + UserId.
- `OrderTakenEnqueuesPushTest` — TakeOrder.Handler with stub queue, verify SendPushNotificationMessage enqueued post-commit, NOT pre-commit.

### Manual E2E checklist
1. Fresh install → permission prompt appears (Android 13+) → grant.
2. Sign in → backend Device row exists with non-empty DeviceToken.
3. Have admin user place an order → take it → push arrives within 5s → tap → opens OrderDetail with right orderId.
4. Open Notifications screen → toggle "Order updates" off → take another order → no push received.
5. Force-quit app (swipe from recents) → repeat (3) → push still arrives → tap → cold-start → lands on OrderDetail (NOT Home).
6. Sign out → backend Device row deleted → from another logged-in user's actions, this device gets nothing.
7. Reinstall app → onNewToken fires → new Device row → flow resumes.
8. Lock screen visible: notification body shows generic "Your booking has an update" — not the customer name or address.

---

## 7. Effort estimate

- **Phase A (MVP)**: ~5 working days (1 day backend foundations, 1.5 days FCM dispatch + Function, 1 day mobile FCM + service, 1 day prefs UI + i18n, 0.5 day E2E)
- **Phase B**: ~1 day (additional hook sites + 30 i18n strings)
- **Phase C**: ~2 days (membership timer Function + loyalty hook)
- **Phase D**: ~2 days (admin UI + fan-out)

Total to ship full set: ~10 working days. MVP alone unlocks the highest-value triggers in 5.

---

## 8. Out of scope (do not expand here)

- iOS push (no iOS app yet). When it lands: data-only payload pattern carries over verbatim, plus add APNs cert in Firebase project + UNNotificationServiceExtension on the iOS side.
- Partner app push (separate plan when partner needs job-offered notifications).
- Web push (browser Push API). Deferred until customer web app needs it; data-only pattern still applies.
- SMS / email fallback. Email already exists; SMS cost-prohibitive for non-transactional.
- Notification grouping / inline-reply / rich media (image, action buttons). MVP uses single-line generic notifications. Revisit after launch metrics.
- A/B testing notification copy. Add when ≥50 daily push events to be statistically meaningful.

---

## 9. Execution Plan summary

```
### Phase 1: Backend foundations + queue infra (specialist: backend)
- PUSH-001: Device flags + UserNotificationPreferences entity
- PUSH-002: IPushDispatcher + FCM impl
- PUSH-003: Queue + Function

>> MANUAL STEP (owner): Firebase project + google-services.json
>> MANUAL STEP (owner): EF migration AddNotificationsAndDeviceFlags

### Phase 2: Backend commands + hooks (specialist: backend, mostly parallelizable)
- PUSH-004: RegisterDevice / UnregisterDevice command verification
- PUSH-005: NotificationPreferences GET/PUT
- PUSH-006: Hook order-status state changes (8 sites)

>> MANUAL STEP (owner): NSwag regenerate customer client

### Phase 3: Mobile (specialist: mobile, parallelizable)
- PUSH-007: i18n keys × 5 locales
- PUSH-008: Firebase SDK + manifest
- PUSH-009: MessagingService + deep-link plumbing
- PUSH-010: Auth wiring
- PUSH-011: Notifications screen → backend prefs

### Phase 4 (later): membership timer + tier upgrades
- PUSH-012

### Phase 5 (later): marketing fan-out
- PUSH-013

### Verification
- dotnet build src/Cleansia.Api.sln
- ./gradlew :app:assembleDebug :app:testDebugUnitTest
- Manual E2E checklist (section 6)

### Model Recommendations
- Phase 1–2 backend: sonnet
- Phase 3 mobile: sonnet (PUSH-007 i18n table only: haiku)
- Phase 4–5: sonnet

### Token Estimate (per phase, execute model only)
- Phase 1: ~25k (3 backend tasks, fresh files mostly)
- Phase 2: ~30k (8 hook sites + 2 controllers)
- Phase 3: ~40k (5 mobile files new + 2 modified, plus i18n × 5)
- Phase 4: ~15k
- Phase 5: ~15k
- **Total MVP (Phases 1–3): ~95k tokens**
```
