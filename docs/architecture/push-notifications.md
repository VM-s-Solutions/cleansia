# Push Notifications — Dev Setup Runbook

Gotchas the next developer will hit unless documented.

## 0. Azure (deployed envs) — provisioning the FCM credential

The Functions host is the only dispatcher. Bicep wires `FCM__ServiceAccountJson` as a Key
Vault reference to secret **`Fcm--ServiceAccountJson`** (main.bicep `fcmSettings`), gated by
the `fcmSecretProvisioned` param — which the deploy pipeline sets automatically from the
GitHub secret's presence. **The whole flow is driven by one GitHub secret; no manual Azure or
Bicep step.** Owner steps:

1. Firebase Console → Project settings → Service accounts → **Generate new private key**.
   If it fails: the GCP org policy `iam.disableServiceAccountKeyCreation` blocks key
   creation — add an org-policy exception for the `firebase-adminsdk` service account first.
2. Add the key's JSON as the GitHub Environment secret **`FIREBASE_SERVICE_ACCOUNT_JSON`**
   (same place as `SENDGRID_API_KEY` etc. — per Environment: `dev`, later `prod`). Paste the
   raw JSON; the dispatcher auto-detects raw-or-base64.
3. That's it. On the next deploy, the pipeline (`deploy-azure.yml`) resolves
   `fcmSecretProvisioned=true` because the secret is set, writes `Fcm--ServiceAccountJson` to
   Key Vault in the same run, and Bicep wires the reference — so the KV reference is never
   created before the secret exists (an unresolvable reference would dead-letter every push).
   When the secret is absent, the param stays `false` and the dispatcher runs its clean
   disabled no-op.
4. Verify end to end on **BOTH platforms**: change an order status → the push arrives on a
   registered Android device **and** on iOS. Verifying on Android alone is exactly what lets an
   APNs-key fault ship unnoticed — Android never touches the APNs credential, so it stays green
   while every iOS push fails (see §0b). On iOS specifically, verify on a **TestFlight** build,
   not only an Xcode-installed one: they use different APNs environments and a key can be valid
   for one and not the other. Note a Firebase-console test push CAN display on iOS and gives a
   false "works"; the real events are data-only.

## 0b. FCM answers 401 / 403 — "push notifications stopped arriving"

Symptom in App Insights: `POST https://fcm.googleapis.com/v1/projects/<project>/messages:send`
returns **401**, and the queue message ends in `notifications-dispatch-poison`.

> **TWO different credentials sit on this path and both surface as an HTTP 401 from
> `fcm.googleapis.com`. Only the FCM error code separates them. Read the code before you touch
> anything** — this exact ambiguity once cost a full evening of investigating Key Vault, GCP IAM and
> the deploy pipeline while the actual fault was an APNs key in Firebase.

| Log says | Who refused us | Where the fix lives |
|---|---|---|
| `ThirdPartyAuthError`, HTTP 401 | **APPLE.** FCM authenticated to Google fine; APNs then rejected the APNs auth key **Firebase** holds. | Apple Developer portal + Firebase console. Nothing in Azure, Key Vault or GCP is involved and no redeploy can affect it. See §0b-1. |
| **No** FCM error code, HTTP 401/403 | **GOOGLE.** Our service-account credential was refused before FCM's own taxonomy applied. | GCP / the `FIREBASE_SERVICE_ACCOUNT_JSON` secret. See §0b-2. |

`oauth2.googleapis.com` returning 200 while `fcm.googleapis.com` 401s **does not narrow this down** —
the OAuth mint succeeds in both cases. It only proves the service-account key itself is alive.

**Read the answer instead of guessing** — the boundary logs Google's literal text:

```kusto
traces | where message has "FCM rejected token" | where timestamp > ago(2d)
```

That line carries `{ErrorCode}/{TransportErrorCode} HTTP {HttpStatus} — {Detail}`. `host.json`
excludes `Exception` from App Insights sampling so it survives.

### §0b-1 — `ThirdPartyAuthError`: Apple refused the APNs key

Everything below lives in the **Firebase console and the Apple portal**. Nothing in Azure, Key Vault
or GCP participates in this path — `IApnsConfig` (`Apns--KeyId` / `Apns--TeamId` /
`Apns--PrivateKeyPem`) is read ONLY by `ApnsLiveActivityClient` / `ApnsJwtProvider`, which is the
direct-APNs Live Activity channel, not FCM. Restarting the Functions host or re-pulling a Key Vault
reference cannot affect this error.

Check in this order:

1. **Which SLOT holds the key — the one that caused the 2026-07-25 outage.** Firebase → Project
   settings → Cloud Messaging → *Apple app configuration* → the card for this bundle id. Each card
   has **TWO** independent auth-key slots: *Development APNs auth key* and *Production APNs auth key*.
   The console will happily sit with one filled and the other reading *"No production APNs auth key"*.
   A **TestFlight or App Store build sends a PRODUCTION token**, so FCM looks in the production slot;
   with only the development slot filled, every push from those builds fails `APNS_AUTH_ERROR` while
   Xcode-installed builds keep working perfectly. That asymmetry is the signature —
   *"it worked from Xcode and broke the moment I shipped to TestFlight."*
   A single *Sandbox & Production* key is valid in **both** slots: upload the same `.p8` twice.
   Below the auth keys sits a separate legacy **APNs Certificates** section with its own
   development/production pair — leave it empty unless deliberately using `.p12` certificates, since
   a stale one there produces this same error code.
2. **Which app entry holds it.** The key is stored **per iOS app**, not per project, and this project
   has two (`cz.cleansia.customer` and `cz.cleansia.partner`). Uploading to one leaves the other
   unchanged.
3. **Key ID and Team ID as stored.** Firebase does **not** validate the Key ID against the uploaded
   `.p8` — it stores whatever was typed, so a paste with stray whitespace or a stale Key ID fails
   exactly like a bad key. Retype rather than paste when in doubt.
4. **The key's APNs ENVIRONMENT scope.** A key scoped to *Sandbox* cannot authenticate a Production
   token at all. Note the `development` value committed in `CleansiaCustomer.entitlements` tells you
   **nothing** about what a TestFlight build sent — Xcode substitutes `aps-environment = production`
   at distribution signing, so do not "verify" from it.
   A key's environment **cannot be changed after creation** — Apple's *Edit* flow covers only the name
   and the enabled services. Fixing it means creating a REPLACEMENT key, and the environment choice
   lives behind the **Configure** button next to the APNs checkbox, which is easy to click straight
   past: doing so yields another Sandbox-only key that fails identically. Create it as
   **Team Scoped (All Topics)** + **Sandbox & Production**, then re-open the Keys list and READ THE
   ROW BACK to confirm it actually says `Sandbox & Production`.
   Do **not** revoke the old key until the new one is verified working: `.p8` keys never expire,
   revocation is immediate and irreversible, and the old key may also be the one seeded into
   `Apns--KeyId` / `Apns--PrivateKeyPem` for the Live Activity client — revoking it breaks that
   channel too, silently.
5. The key's **topic scope** covers this bundle id, and the key is not revoked.

The FCM path needs **no redeploy** after fixing this — Google holds the key server-side and picks it
up on the next send. Test **both** a TestFlight token and an Xcode-installed token: replacing rather
than adding a key can fix one and break the other.

**Isolating Apple from Firebase.** Every signal above is filtered through FCM, which collapses *every*
Apple-side refusal into the same `ThirdPartyAuthError`. To ask Apple directly, sign a provider JWT
with the `.p8` and POST to `api.push.apple.com` (and `api.sandbox.push.apple.com`) yourself. Even
with a bogus device token the answer separates the cases: `403 InvalidProviderToken` = the key or
identity is wrong; `400 BadDeviceToken` = **the key is fine** and the problem is elsewhere.

**Not to be confused with `APNS__UseSandbox` in `main.bicep`** — that steers the direct-APNs
**Live Activity** client, a separate path that never involves FCM.

### §0b-2 — no FCM error code: Google refused the service account

The project id in the URL is not configured anywhere. It is read out of the service-account JSON
itself (`FcmPushDispatcher` passes no `ProjectId` override when `FCM:ServiceAccountJson` is set), so
a "wrong project in Azure" mismatch is structurally impossible — the project in the URL *is* the
credential's own project. That leaves:

| # | Cause | Tell-tale |
|---|---|---|
| 1 | Service-account key **disabled or deleted** in GCP (this org enforces `iam.disableServiceAccountKeyCreation`, so keys get clawed back) | `401 Unauthenticated` |
| 2 | **FCM API not enabled** on the project | `403` naming `fcm.googleapis.com` |
| 3 | Service account **missing the Firebase Cloud Messaging API Admin role** | `403 PermissionDenied` |

The credential is explicitly scoped to `https://www.googleapis.com/auth/firebase.messaging`
(`CreateScoped` in `EnsureInitialized`), which removes the "inherited broad default scopes" failure
mode and stops us minting a `cloud-platform` token just to send a push.

**Fix:** re-issue the service-account key in Firebase Console → Project settings → Service accounts
→ *Generate new private key*, update the `FIREBASE_SERVICE_ACCOUNT_JSON` GitHub Environment secret,
and re-run the deploy (the Key Vault push only happens inside a deploy). Then verify with
`gcloud services list --enabled --project <project> | grep fcm`.

### What the code does with either

It classifies the failure `AuthConfig` and ACKs with one alertable `LogError` instead of throwing.
The fault is host-wide — every push is failing identically — so redelivery was amplification, not
recovery: ~15 FCM rejections plus 15-25 OAuth mints per notification, all landing in the poison
queue with the real cause discarded. Device rows are **never** pruned on a 401; the tokens are
innocent, and pruning would delete every `Device` row.

⚠️ **Because it now acks, a broken credential produces SILENCE rather than a poison pile.** That
`LogError` is the only operational signal — alert on it (`Provider="Fcm"`, `FailureClass=AuthConfig`).

Reference: [FCM v1 error codes](https://firebase.google.com/docs/cloud-messaging/error-codes).

## 1. Encoding the FCM service-account JSON

`FCM:ServiceAccountJson` must be **base64-encoded**. The dispatcher accepts
raw JSON too (auto-detected via leading `{`) but base64 sidesteps the
JSON-inside-JSON escape pain.

```powershell
$bytes = [IO.File]::ReadAllBytes("path\to\firebase-key.json")
[Convert]::ToBase64String($bytes) | Set-Clipboard
```

Then paste as the secret value:

```powershell
dotnet user-secrets set "FCM:ServiceAccountJson" "PASTE_BASE64_HERE"
```

## 2. JSON `:` vs env-var `__` separator

In secrets.json / appsettings.json: use **`:`** or nested objects. Never `__`.

```json
{ "FCM": { "ServiceAccountJson": "..." } }
```

In `local.settings.json` `Values` (Azure Functions only — that section is
treated as env vars): **`__`** is correct.

```json
{ "Values": { "SendGrid__ApiKey": "..." } }
```

## 3. Functions host needs an `IHostAudienceProvider` sentinel

MediatR's assembly scan over `Cleansia.Core.AppServices` registers the Auth
handlers (Login, GoogleAuth, etc.) which depend on `IHostAudienceProvider`.
The Functions host never issues tokens but DI still validates the constructor
at startup. Without a binding the worker process aborts.

`Cleansia.Functions/Program.cs` registers a sentinel:

```csharp
services.AddSingleton<IHostAudienceProvider>(new HostAudienceProvider("cleansia.functions"));
```

## 4. EF tenant filter — a consumer with no tenant reads nothing

The Functions host has no JWT, so `GetCurrentTenantId()` is `null` until a consumer sets the override
from the envelope it is processing (`SendEmailHandler`, the push producers). Since ADR-0061 every
stamped row carries a non-null `TenantId`, so the filter's `(currentTenantId == null && e.TenantId ==
null)` branch matches nothing here: a consumer that forgets its override reads an **empty** set — a
user with no device, an order with no rows — and a producer that writes under no tenant fails `23502`.
Set the override from the envelope's tenant before the first tenanted read; the envelope carries it
because the producer passed `tenantProvider.GetCurrentTenantId()` (or the row's own tenant) when it
enqueued. → [Cross-cutting concerns — tenancy](/flows/cross-cutting#tenancy)

## 5. Emulator setup

The customer Android app's emulator must run a **Google Play** system image
(not "Google APIs"). FCM requires Google Play Services.

```powershell
.\adb shell pm list packages | Select-String "google.android.gms"
```

If this returns nothing, recreate the AVD with a Google Play image.

## 6. Test pushes via Firebase Console

Quickest way to isolate "is the device reachable at all":

1. Find the device token: `SELECT "DeviceToken" FROM "Devices" WHERE "UserId" = '...' LIMIT 1`
2. Firebase Console → Cloud Messaging → "Send your first message" → "Test on device"
3. Paste token, send. If it arrives, the device is fine and the issue is
   upstream (dispatcher, token freshness). If not, the issue is the
   emulator/token/Play Services.

## 7. Standalone Functions debug in Visual Studio

VS 2022's bundled Functions toolset (4.126.0) doesn't yet support .NET 10
isolated workers, so F5 on `Cleansia.Functions` fails with "no Functions
runtime available." Use **Debug → Attach to Process** instead:

1. Run Aspire (`Cleansia.AppHost`) — it launches the Functions worker.
2. In VS: Debug → Attach to Process → search `Cleansia.Functions` → attach.
3. Trigger an action that enqueues a push. Breakpoints in
   `SendPushNotificationFunction.Run` hit.

When VS ships a toolset with net10 support, switch to F5 launch instead.

## The event catalogue {#event-catalogue}

`NotificationEventCatalog` maps every event key — the strings that flow on the queue and into the FCM
payload — to the per-user opt-in category. The same keys are looked up in the Android apps' string
resources, so the two must stay in step.

Several keys exist as separate keys for reasons that are easy to undo by "simplifying" them.

### The admin audience: a feed and an e-mail, never a push {#admin-audience}

The in-app feed has **three** audiences, and the host controller sets which one a request serves —
`NotificationFeedAudience { Customer, Partner, Admin }` — so a dual-role user's console can never read,
count or mark-read a row of their partner-app feed. The customer and partner keysets are lists that
trail their clients' templates (a key belongs in a keyset only once the audience's clients render it,
or the badge counts a row the app drops unrendered). **The admin keyset is the catalogue by
construction** — `NotificationFeedEventKeys.Admin = AdminNotificationEventCatalog.All`, the nine
`admin.*` keys ([ADR-0065](/decisions/adr-0065)) — because the console is built to render every key of
its catalogue, and a spec walks the C# file so a key added on the server fails the admin build without
its five-locale sentence.

Three things separate this audience from the other two:

| | Customer / partner | Admin |
|---|---|---|
| Writer | `NotificationProducer.NotifyAsync` — the feed row **and** an unconditional push | `AdminNotifier.NotifyAsync` — the feed row and an outbox **e-mail**; no push, ever. `IsFeedEvent` does not know the admin keys: the push seam cannot write them |
| Recipient | one user, resolved from the persisted row | every eligible administrator of the **named** company, one row each, read by argument past the tenant filter |
| Category / mute | `GetCategoryFor(key)` → a mutable category, or non-mutable | every `admin.*` key maps to **null**: no category, no preference, nothing to mute — an administrator who does not want order e-mails is a company that sets the mailbox |

**Why no push.** `NotificationProducer` enqueues a `SendPushNotificationMessage` on every call, an
administrator may hold a partner-app device row, and a push would reach an app that cannot render an
`admin.*` key — the admin console is a browser, and it polls `unread-count` once a minute while the tab
is visible instead. **The e-mail rides the same `send-email` queue** as every other message, as a second
message shape the consumer tells apart by a `messageType` discriminator (`admin-notification`), the way
the guest cancellation e-mail already did: one `EmailType.AdminNotification`, one embedded template
(`admin-notification.html`), and per-event subject and body copy in five locales keyed
`{eventKey}.Subject` / `.Body` (the crew-lost event adds `.BodyUnderWay` and two `.Cause.*` phrases),
layered under the admin e-mail-template page's rows like the wind-down notices. Its message key is
`admin-email:{eventKey}:{subject}:{hash(address)}` — the address hashed so no recipient appears in a
key or a log line — which is why an event's **subject must be unique per logical event across
requests**: the outbox index fails the second commit rather than collapsing it. **Args are never PII**,
the same rule as the two client feeds: ids, numbers, enum names, dates and money, and the catalogue
entry declares the exact set a site may pass, so a site cannot smuggle a name in. Rows fall under
`retention.notifications.days` like every other feed row.

**The customer keyset is unchanged by the walk-back**, and one consequence is named rather than hidden:
when a `Confirmed` order loses its last cleaner and goes back to `New` ([ADR-0067](/decisions/adr-0067)),
no customer push, e-mail or feed row is written — but the next take is a `New → Confirmed` transition
again, so the customer receives a **second** "your order is confirmed" e-mail on top of the assignment
push. A new cleaner is a new confirmation; the e-mail is true when it is sent (default O-D2-1).
→ [Business rules — administrators are told](/product/business-rules#admin-notifications),
[Admin notifier](/domain/roles/admin-notifier)

### Why the cleaner-assigned event is not the confirmed event {#assigned-vs-confirmed}

`order.payment_confirmed` records the payment-side confirmation. The Stripe webhook settles a card
payment; recurring cash confirmation accepts the occurrence before onsite collection. Neither means
a cleaner accepted the job, and since [ADR-0057](/decisions/adr-0057) neither writes a fulfilment status.
The key therefore says “confirmed”, rather than claiming cash has already been received.
→ [the order lifecycle](/domain/order-lifecycle)

`order.payment_confirmed` is the one key for this event. The earlier `order.confirmed` is gone from
`NotificationEventCatalog`, from the customer feed keyset and from the web, Android and iOS clients:
nothing produces it, no client carries a template for it, and a feed row that still names it sits
outside the keyset, so the feed neither lists nor counts it. Existing rows and idempotency keys are
not rewritten.

A rename like this one still ships clients first. An older binary cannot resolve an Android template
or APNs localization key it does not bundle, and nothing on the server updates a device's strings —
[ADR-0025's version-skew constraint](/decisions/adr-0025).

Widening that key to carry "a cleaner is committed to your booking" would repeat the overloading one
layer up, in the thing that writes to a customer's lock screen.

### A recurring schedule pauses once per paid lapse {#recurring-paused}

When materialization skips a recurring template because paid Plus is inactive, `recurring.paused`
tells the account owner to renew. The template stays saved and existing occurrences stay booked.
The notification belongs to the account's operator, even when a template serves another market.
It uses the existing recurring-notification preference; a muted push still leaves the feed item.

The membership stores the notice timestamp and a permanent sequence. The sequence participates in
the dispatch identity, so feed retention and a later recovery cannot make an earlier delivery eligible
again. The notice, outbox intent and membership latch commit together; PostgreSQL concurrency checking
allows only one competing template or sweep to acquire that latch.

An authoritative Stripe `active` observation for a live, non-trial period establishes paid-period
proof. The account's latest proven-paid membership owns its lapse; an unsuccessful new subscription
cannot replace it with a fresh latch. Paid and unpaid observations retain their provider chronology,
so a delayed genuine recovery can rearm the next lapse while an older replay cannot. Equal or missing
event chronology does not rearm. Existing entitlement reconciliation is unchanged.

No payment history is inferred for an unmarked membership: it continues to follow existing scheduling
rules but receives this notice only after an authoritative paid observation establishes proof. There
is no historical-data backfill. Both mobile apps carry the five-locale display copy; the customer's
feed and tap route lead to membership renewal.

### Why the preferred-offer-closed message is one sentence {#one-sentence}

When a customer's chosen cleaner does not take the booking, the customer is told the offer ended and is
offered a second choice. **They are never told that a named person refused, and never told that a named
person did not answer.**

One sentence covers both outcomes and that *is* the guarantee. Two per-path strings would reintroduce
exactly the disclosure the neutral line exists to prevent — and the question of which lawful basis
covers telling a third party what a worker did is still open.

The same reticence runs the other way: no surface ever says an order is held for someone else, and no
cleaner ever learns they were passed over.

### Which events a user may silence {#mutability}

Most order events sit under the existing `OrderUpdates` category rather than getting one of their own.
A new category is a boolean **column** plus a toggle in every client, and someone who silenced order
updates has already answered the question.

Five are deliberately **non-mutable**, and every one of them is aimed at a **cleaner**, about a job they
have already accepted. That is the line: a customer may silence anything, because the consequence of a
missed message is theirs. A cleaner not turning up is somebody else's morning.

| Event | Key | Why it cannot be silenced |
|---|---|---|
| Admin assigned you a job | `order.admin_assigned` | A cleaner must not be able to silence a job appearing on their own schedule and then not turn up |
| Admin took you off a job | `order.admin_unassigned` | Losing a booked day is not an optional notice |
| You have N jobs tomorrow | `order.reminder_tomorrow` | The day-ahead plan. A cleaner who silenced it would be planning tomorrow off memory |
| Your job starts in about two hours | `order.reminder_soon` | The last point at which a cleaner can still travel, or tell us they cannot |
| Your job starts soon and you have not set off | `order.reminder_not_started` | The platform's last chance to prevent a no-show. Suppressed for a cleaner already out on **another** job |

The three reminders are non-mutable **on the owner's ruling** (2026-09-15, Q-PUSH-01 — the evening
digest included; it was the one the ADR had escalated), on the same reasoning as the two above and
recorded in ADR-0054: they are not marketing, they carry no offer, and each one is about work the cleaner
already agreed to do.

Both per-job reminders come off **one** query that selects only orders still in `Confirmed`, so marking
yourself on the way switches off whichever of the two has not already been sent. In practice that only
ever silences the nudge — nobody is on the way two hours early — but the gate is the same gate, not two.

The nudge carries one further condition, and it is about the **cleaner** rather than the order: it is
suppressed for anyone already `OnTheWay` or `InProgress` on *any* assignment. Back-to-back jobs put the
second job's nudge window inside the first, so without it the platform asks a cleaner holding a mop in
someone else's kitchen whether they have set off. The two-hour notice is deliberately **not** suppressed
that way — knowing what is next while finishing this one is useful.

**All three go only to cleaners who may actually work**: `ContractStatus.Approved` exactly, on an active
account. Rejecting a cleaner does not take them off their live orders, so without one shared predicate
the sweep would tell somebody the platform has just barred from working that their job starts in two
hours — for work `StartOrder` would then refuse to let them start.

And the admin-unassigned copy is deliberately **not** the assignment-cancelled copy: here the job goes
ahead with somebody else, and a cleaner repeating "cancelled" to the customer would be telling them
their booking was gone.

### Why the day-ahead digest runs hourly {#digest-hourly}

`SendTomorrowJobDigest` fires **every hour**, not once a day, and each tick sends to almost nobody. That
is not waste — it is the only way a UTC cron can deliver at 18:00 *local*.

A timer trigger has no timezone. A once-a-day tick would land at one instant worldwide and be the right
evening for exactly one country. So the sweep runs hourly, resolves each cleaner's zone from the
`WorkCountryId` they were assigned at registration, and picks only those inside a **local** send window
that opens at 18:00. A cleaner in Prague and a cleaner in Warsaw get the same message an hour apart in
UTC and at the same moment on their own kitchen clock.

**A window, not the hour itself, and that distinction is load-bearing.** An hour *equality* gives a whole
timezone exactly one attempt per day: a cleaner who takes tomorrow's job at 18:30 is never told, because
at 19:00 the test is already false — no failure required, that is simply what an equality does. Any tick
that throws before its group commits loses that evening outright. The window is bounded at **three
hours** rather than left open: these keys are non-mutable and the platform has no quiet hours, so an
unbounded catch-up would push at 23:00 to someone who took a job at 22:50.

Two consequences worth knowing before changing it:

- **The suppression watermark is compared in the cleaner's zone too.** Comparing it in UTC would let a
  cleaner east of Greenwich be told about tomorrow twice on one local evening, when UTC midnight falls
  inside their evening.
- **A zero-job evening sends nothing and records nothing.** No digest saying "0" — that message would
  teach a cleaner to ignore the one that says 2 — and no watermark either, so a job taken later the same
  evening still earns a digest on the next tick.

A cleaner with no work country is skipped rather than defaulted to UTC: defaulting would send at the
wrong hour while looking like it worked.

### One claim the copy still overstates {#near-you}

The new-jobs digest says *"N new jobs near you"*, and the server now means it — the count is narrowed by
the cleaner's own job radius around their home address.

**It is still not true for everyone.** A cleaner who has set no radius, and one whose home never
geocoded, both keep the country-wide board by design, and the payload carries no way for the client to
tell those apart. Making the wording follow the reality needs a second loc arg or a second event key,
which means new strings in both apps.
