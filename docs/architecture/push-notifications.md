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
4. Verify end to end: change an order status → the push arrives on a registered **Android**
   device (iOS display is gated on the T-0404 APNs alert work — a Firebase-console test push
   CAN display on iOS and gives a false "works"; the real events are data-only).

## 0b. FCM answers 401 / 403 — "push notifications stopped arriving"

Symptom in App Insights: `POST https://fcm.googleapis.com/v1/projects/<project>/messages:send`
returns **401**, and the queue message ends in `notifications-dispatch-poison`.

**The project id in that URL is not configured anywhere.** It is read out of the
service-account JSON itself (`FcmPushDispatcher` passes no `ProjectId` override when
`FCM:ServiceAccountJson` is set), so a "wrong project in Azure" mismatch is structurally
impossible — the project in the URL *is* the credential's own project. Likewise, the OAuth
token mint succeeding proves the key is neither revoked nor mangled. That leaves:

| # | Cause | Tell-tale |
|---|---|---|
| 1 | Service-account key **disabled or deleted** in GCP (this org enforces `iam.disableServiceAccountKeyCreation`, so keys get clawed back) | `401 Unauthenticated` |
| 2 | **FCM API not enabled** on the project | `403` naming `fcm.googleapis.com` |
| 3 | Service account **missing the Firebase Cloud Messaging API Admin role** | `403 PermissionDenied` |

The credential is now explicitly scoped to `https://www.googleapis.com/auth/firebase.messaging`
(`CreateScoped` in `EnsureInitialized`), which both removes the "inherited broad default scopes"
failure mode and stops us minting a `cloud-platform` token just to send a push.

**Read the answer instead of guessing** — Google's literal error text is logged at the boundary:

```kusto
traces | where message has "FCM rejected token" | where timestamp > ago(2d)
```

That line carries `{ErrorCode}/{TransportErrorCode} HTTP {HttpStatus} — {Detail}`, which names the
cause outright. `host.json` excludes `Exception` from App Insights sampling so it survives.

**Owner fix:** re-issue the service-account key in Firebase Console → Project settings → Service
accounts → *Generate new private key*, update the `FIREBASE_SERVICE_ACCOUNT_JSON` GitHub
Environment secret, and re-run the deploy (the KV push only happens inside a deploy). Then verify
with `gcloud services list --enabled --project <project> | grep fcm`.

**What the code now does with a 401:** it classifies it `AuthConfig` and ACKs with one alertable
`LogError` instead of throwing. A credential fault is host-wide — every push is failing
identically — so redelivery was amplification, not recovery: ~15 FCM rejections plus 15-25 OAuth
mints per notification, all landing in the poison queue with the real cause discarded. Device rows
are never pruned on a 401; the tokens are innocent, and pruning would delete every `Device` row.

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
