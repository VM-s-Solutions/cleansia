# Push Notifications — Dev Setup Runbook

Gotchas the next developer will hit unless documented.

## 0. Azure (deployed envs) — provisioning the FCM credential

The Functions host is the only dispatcher. Bicep wires
`FCM__ServiceAccountJson` as a Key Vault reference to secret **`Fcm--ServiceAccountJson`**
(main.bicep `fcmSettings`). Owner steps, **in this order**:

1. Firebase Console → Project settings → Service accounts → **Generate new private key**.
   If it fails: the GCP org policy `iam.disableServiceAccountKeyCreation` blocks key
   creation — add an org-policy exception for the `firebase-adminsdk` service account first.
2. Put the key into Key Vault (raw JSON or base64 — the dispatcher auto-detects):
   `az keyvault secret set --vault-name <kv> --name Fcm--ServiceAccountJson --file firebase-key.json`
3. Only then flip `param fcmSecretProvisioned = true` in `deploy/bicep/weu.dev.bicepparam`
   (and later `weu.prod.bicepparam`) and merge — the setting is param-gated DEFAULT-OFF because
   **an unresolvable KV reference is worse than no setting**: the app receives the literal
   `@Microsoft.KeyVault(...)` string, init fails as TRANSIENT, and every push dead-letters to
   the poison queue instead of the clean disabled no-op.
4. Verify end to end: change an order status → the push arrives on a registered **Android**
   device (iOS display is gated on the T-0404 APNs alert work — a Firebase-console test push
   CAN display on iOS and gives a false "works"; the real events are data-only).

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
