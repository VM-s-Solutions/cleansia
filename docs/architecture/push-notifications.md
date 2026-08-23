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

## 4. EF tenant filter — null/null case

`null = null` in SQL is `NULL` (not `true`), which would hide every row in
single-tenant deployments and queue/webhook contexts. The global query
filter at `CleansiaDbContext.ApplyTenantQueryFilters` has an explicit
`(currentTenantId == null && e.TenantId == null)` branch to make
single-tenant mode work.

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

### Why the cleaner-assigned event is not the confirmed event {#assigned-vs-confirmed}

`OrderConfirmed` is [overloaded](/domain/order-lifecycle#confirmed-is-deliberately-overloaded) — it
means *money settled* **or** *cleaner assigned*. Two of its producers, the Stripe webhook and the
recurring cash confirmation, have no cleaner at all.

Widening that key to carry "a cleaner is committed to your booking" would repeat the overloading one
layer up, in the thing that writes to a customer's lock screen.

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

The three reminders are non-mutable **on the owner's ruling**, on the same reasoning as the two above and
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
