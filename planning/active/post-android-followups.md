# Post-Android follow-ups — open items

Slim active tracker. The full archive of shipped work (Waves 0–5, LOY-001/002/003/005, REC-001/002, MOB-C-001 through 008/011/012, BE-001/003/004/005, WEB-P-002, etc.) lives at [`planning/done/post-android-followups-shipped.md`](../done/post-android-followups-shipped.md).

---

## Owner-gated (code wired, blocked on external setup)

### Google OAuth wiring (MOB-C-009 / WEB-C-004 / MOB-C-009b)
**Blocker:** Google Cloud Console project + OAuth client IDs (web + Android).

**State of the code:**
- Customer mobile: Credential Manager flow wired in `SignInScreen.kt` / `SignUpScreen.kt`, `GoogleSignInController.kt`, `AuthViewModel.signInWithGoogle()`. `GOOGLE_WEB_CLIENT_ID` BuildConfig field reads from `~/.gradle/gradle.properties`.
- Customer web: Google Identity SDK + `#googleBtn` template ref shipped in `login.component.ts` / `register.component.ts`. Reads `environment.googleClientId`.
- Customer mobile forgot-password: fully wired (`requestPasswordChange` + `changePassword` on `AuthApi`/`AuthRepository`/`AuthViewModel`; `ForgotPasswordScreen` UI wired).

**Owner actions when ready:**
1. Provision Cloud Console project + OAuth web client + Android client (SHA-1 fingerprint from debug keystore for dev, release keystore for prod).
2. Drop the web client ID into:
   - `~/.gradle/gradle.properties`: `GOOGLE_WEB_CLIENT_ID=...` (customer Android).
   - `apps/cleansia.app/src/environments/environment*.ts`: `googleClientId: '...'` (customer web — also partner web if you want them sharing the project).
3. Test sign-in on each surface.

### Csrf:Enabled flip
See [`httponly-cookie-auth-migration.md`](httponly-cookie-auth-migration.md) Step 6.

---

## Open engineering work

### MOB-P-NOTIF — Partner push notifications + in-app notifications feed [CODE DONE, owner-gated on Firebase config + backend dispatch]

**Status (2026-05-29):** All app code shipped and building (`:partner-app:assembleDebug` green). What remains is owner provisioning + a backend dispatch gap — see "Remaining" at the bottom of this entry.

**Shipped:**
- `core/notifications/`: `CleansiaFirebaseMessagingService` (data-only payloads → local notifications + Room persistence), `NotificationDeepLink` (→ `NavRoute.OrderDetails`), `NotificationChannels`, `PushTokenRepository`, `DeviceApi` wrapper, Hilt module.
- `core/notifications/db/`: Room `NotificationRecord` + DAO + DB for the feed + unread count.
- `features/notifications/`: `NotificationsScreen` + VM (newest-first list, tap → order, mark-read on open, MascotEmptyState empty state).
- Token register on login + email-confirm, unregister on logout (`AuthRepository`, all `runCatching`-wrapped).
- Bell wired (`DashboardScreen` → `NavRoute.Notifications`) with an unread dot from the Room flow.
- `MainActivity` resolves deep links on cold start + `onNewIntent`.
- `build.gradle.kts`: google-services plugin + firebase-messaging BOM + Room deps. `google-services.json` gitignored; placeholder `google-services.sample.json` auto-copied so the build never blocks (real file, when dropped in, is NOT overwritten).
- Wired event_keys (cleaner's POV, all carry orderId/orderNumber): `order.confirmed`, `order.in_progress`, `order.completed`, `order.cancelled`, `dispute.reply`.

**Remaining (owner / backend — push does NOT work end-to-end until done):**
1. Drop a real `google-services.json` (Firebase console, project `cleansia` / number 640229436651; register `cz.cleansia.partner` + `cz.cleansia.partner.debug` + SHA-1s) into `partner-app/`.
2. Confirm the backend FCM dispatcher targets the partner audience (same Firebase project + service account as customer, so likely no new secret).
3. **Backend dispatch gap:** today every `NotificationEventCatalog` event fans out to `order.UserId` (the customer). Partners receive nothing. Add partner-targeted dispatch sites for the keys the feed expects — at minimum `order.available` (new job) / `order.assigned`, and `invoice.generated` / `payperiod.invoice_generated`. The mobile side already handles these keys; only the backend send is missing.

(Original plan preserved below for reference.)

#### Original plan

**Goal:** make the dashboard bell (already wired to a no-op `onNotificationClick`) open a real notifications feed, and deliver push to partners.

**Blocker / why it's not done yet:** the **partner app has zero Firebase/FCM infrastructure** — no `firebase-messaging` dependency, no `google-services` plugin, no `google-services.json`, no messaging service, no token registration. The push-notifications MVP shipped customer-app only. So "capture incoming FCM locally" is impossible until the FCM stack exists.

**Reference implementation (customer app, copy this):**
- `customer-app/.../core/notifications/CleansiaFirebaseMessagingService.kt`
- `customer-app/.../core/notifications/NotificationDeepLink.kt`
- Customer `build.gradle.kts` (google-services plugin + firebase-messaging dep) + `customer-app/google-services.json`.

**Work breakdown:**
1. **Owner manual step:** provision a Firebase project entry for `cz.cleansia.partner`, download `google-services.json` into `partner-app/`. Provision the FCM service-account secret for the backend Functions host (same as customer).
2. Partner `build.gradle.kts`: add `com.google.gms.google-services` plugin + `libs.firebase.messaging`.
3. Port `CleansiaFirebaseMessagingService` + `NotificationDeepLink` into `partner-app/.../core/notifications/`, mapping deep links to partner `NavRoute.X` types (e.g. `OrderDetails(orderId)`).
4. Token register/unregister on auth (login → register device, logout → unregister) via the existing `DeviceApi` pattern. Reuse `core/network/NetworkCall.kt` for the repo, not bare try/catch.
5. AndroidManifest: declare the messaging service + `POST_NOTIFICATIONS` (already present) runtime permission request.
6. Backend: confirm partner audience is covered by the push dispatch (the Functions `IHostAudienceProvider` / FCM pipeline). Partner-relevant events: order assigned/available, dispute reply, pay-period/invoice generated.
7. **Notifications feed UI:** Room table (`partner-app` has no Room yet — add `androidx.room` deps) capturing each received `RemoteMessage`; a `NotificationsScreen` listing them (tap → deep-link to order), unread dot on the bell. Wire `onNotificationClick` → `NavRoute.Notifications`.
   - Alternative to local capture: a backend notifications-history endpoint (survives reinstalls, syncs devices) — separate BE task + NSwag regen.

**Decision recorded (2026-05-29):** owner chose "stand up the full partner FCM stack first" over a UI-only stub. Sequenced as its own task because step 1 is an external provisioning gate that blocks everything downstream.

### WEB-P-001 — Mapbox autocomplete in partner address forms [DONE]

Shipped this session (env files + provider + profile-personal-info wiring). Promoting to closed; mention here only for the audit trail.

### INFRA-001 — Bicep IaC [DEFERRED — see decisions-infra-arch.md]

Trigger: next time provisioning a second env from scratch. 3–5 days when triggered.

### ARCH-001 — Android `:core` shared module [DEFERRED — see decisions-infra-arch.md]

Trigger: next time adding the same primitive to both apps. 2–3 days.

### BE-002 — `HandlePaymentNotification` refactor [DONE]

Subscription branch extracted to `StripeSubscriptionWebhookHandler`, `IsOrderEvent`/`IsSubscriptionEvent` moved to `Constants.StripeEventType`, `ExtractOrderId` helper extracted. File dropped from 453 → 296 LOC. Promoted to closed.

---

## Deferred with documented triggers

These have explicit reopen conditions; no action until triggered:

| Item | Trigger |
|---|---|
| MOB-C-002 (tier mascots) | Design assets land |
| MOB-C-008 / MOB-P-001 (>700 LOC file splits) | Next feature work touching those files |
| MOB-C-010 / MOB-P-002 (snackbar→VM injection, 9 sites) | Incremental during feature work |
| WEB-C-001 (perf regression) | Reopen with concrete Lighthouse data |
| WEB-C-002 (duplication) | When a duplication candidate actually surfaces |
| LOY-004 (Plus discount didn't apply) | Retest with LOY-001/002 chips live in user retest |

---

## Cross-references

- HttpOnly cookie auth → [`httponly-cookie-auth-migration.md`](httponly-cookie-auth-migration.md)
- Frontend cleanup waves → [`frontend-cleanup-plan.md`](frontend-cleanup-plan.md)
- Push notifications Phase B → [`push-notifications-phase-b.md`](push-notifications-phase-b.md)
- Mobile theming + locales → [`mobile-theming-i18n.md`](mobile-theming-i18n.md)
- Infra + Android arch decisions → [`decisions-infra-arch.md`](decisions-infra-arch.md)
