# Azure PROD reliability posture (T-0359) — authored, owner-applied

> The prod seams the dev Bicep deliberately leaves off, authored as **env-switched parameters** on the
> same module set (ADR-0015 D1: prod is dev's topology at a different scale). Every knob defaults to
> the dev value, so a dev deploy with the unchanged `weu.dev.bicepparam` is behavior-identical;
> [`weu.prod.bicepparam`](bicep/weu.prod.bicepparam) flips them. **Authored, NOT deployed** — applying
> any of this is the owner's step (a `Deploy to PRO` dispatch, runbook §11), same rule as ADR-0015.

## The knobs at a glance

| # | Seam | main.bicep param(s) | dev default | prod param file | Overridable? |
|---|---|---|---|---|---|
| 1 | Deployment slots + swap | `deploymentSlotsEnabled` | `false` | `true` | yes |
| 2 | Autoscale | `autoscaleEnabled`, `autoscaleMinInstances`, `autoscaleMaxInstances` | `false`, 1, 3 | `true`, 1, 3 | yes |
| 3 | Postgres HA + geo-backup | `postgresHighAvailabilityMode`, `postgresGeoRedundantBackup`, `postgresBackupRetentionDays` | `Disabled`, `Disabled`, 7 | `ZoneRedundant`, `Enabled`, 35 | yes (geo-backup only at first provision) |
| 4 | ACR image retention | `acrImageRetentionEnabled`, `acrImageRetentionDays` | `false`, 30 | `true`, 30 | yes |
| 5 | App Insights sampling + ingestion cap | module-internal env switch (`modules/appInsights.bicep`: `samplingPercentage`, `dailyCapMb`) | 10%, 500 MB/day | 50%, 5000 MB/day | yes (module params) |
| 6 | Private database + Key Vault (Q-INFRA-03, E-3) | `privateNetworkingEnabled` | `false` | `true` | yes, see §6 |
| 7 | Storage on managed identity; least-privilege database login (E-4) | `storageManagedIdentityEnabled`, `postgresAppLoginEnabled` | `false`, `false` | `true`, `true` | yes, see §7 |

**Always On is NOT env-switched** — `alwaysOn: true` on all six web hosts in every stage (it was
`env == 'prod'`). Always On costs nothing on a plan already billed by the hour, and without it App
Service unloads an idle dev host after ~20 minutes, so a demo opens on a cold start. Dev's B2
(2 vCPU / 3.5 GB) now holds 7 resident processes (5 APIs + SSR + Functions, no slots); watch for
memory-driven recycling there the same way §1's per-instance math says to on prod's S1.

## 1. Deployment slots + swap (`deploymentSlotsEnabled`)

Each of the six web hosts (5 APIs + the customer SSR) gets a **`staging` slot** mirroring the parent's
config 1:1, with its own system-assigned managed identity that receives the same Key Vault
Secrets User + Storage data grants (via `roleAssignments.bicep`) — a slot that cannot resolve its Key
Vault references would swap a broken instance into production.

- **No stop/start deploy pattern anywhere** — the swap replaces it: deploy to the slot, warm it, swap;
  the production site is never stopped.
- **The Functions host deliberately gets NO slot**: a warm staging Functions container would compete
  with production for the same Storage Queue messages (double-consumption). Functions deploys stay
  a container set — setting the image restarts the site, so there is no separate restart step.
- S1 supports 5 slots per app; B-series rejects slot creation, which is why dev stays `false`.
- **Slots are not coming to dev, and "upgrade dev to Standard so it can have them" is the wrong fix —
  it is both the expensive option and the worse one.** The intuitive cure for a cold dev host is a
  staging slot, so this is written down rather than left to be re-derived. Standard is the lowest tier
  that accepts a slot, and the step from dev's **B2 (2 vCPU / 3.5 GB)** to **S1 (1 vCPU / 1.75 GB)**
  *halves both the RAM and the CPU* of the plan that already holds 7 always-on processes — on the very
  hosts whose cold start prompted the question. Paying more for less memory to fix a memory-sensitive
  symptom is a net loss before the invoice is even opened. (The monthly delta is a pricing-table lookup
  and is deliberately not quoted here; the SKU arithmetic above is the decisive part and does not go
  stale.) Always On plus the post-deploy warm probe already close the dev gap at **€0** — see the Always
  On note above, the *Warm the site* step each DEV leg of `deploy-api` runs through
  `.github/scripts/warm-site.sh`, and the `warm-dev-sites` job that warms the SSR after them.
- **Slots are NOT Always On** (hardcoded `alwaysOn: false` on the slot resource): Always On is on
  Azure's not-swapped (slot-sticky) settings list, so a warm slot buys zero swap benefit — the CI
  workflow warms the slot explicitly before swapping. Mirroring the parent's prod `alwaysOn: true`
  would keep 6 idle staging processes permanently resident between deploys for nothing.
- **Per-instance memory math (read before first provision):** with slots, one plan instance hosts
  every site — 5 API hosts + SSR + the Functions container are Always On in prod (7 resident
  processes; the 6 slots idle-unload since they are not Always On). On the authored **S1
  (1 vCPU / 1.75 GB)** that is tight at real load (EF + connection pools across 5 .NET APIs), and the
  autoscale rule is CPU-based — it will not relieve MEMORY pressure (scaling out replicates all sites
  per instance). If prod shows memory-driven recycling (502/503 + worker restarts in App Insights),
  step the plan SKU to **S2 or P0v3** rather than tuning processes.

**Workflow step (authored):** the six web-host deploys in `.github/workflows/deploy-azure.yml` — the
five legs of the `deploy-api` matrix job and `deploy-customer-ssr` — run the full slot flow whenever
`inputs.env == 'prod'`: deploy the artifact to the `staging` slot
(`slot-name: staging` on `azure/webapps-deploy@v3`), **warm it** (curl the slot — `/health` for the
five APIs, `/` for the SSR — retrying up to 5 minutes and FAILING the job rather than swapping a
cold/broken slot), then
`az webapp deployment slot swap … --slot staging --target-slot production`. The SSR's startup command
is not a deploy step: Bicep sets `appCommandLine` to `node server/server.mjs` on the site and on its
staging slot (`modules/appService.bicep`), so a deploy no longer restarts the SSR to set it, and the
command travels with the slot on a swap. Dev keeps deploying straight to the production site
(B-series has no slots — path unchanged), and the Functions host keeps its slotless deploy, now a
container set only (the queue double-consumption rule above).

**Prod deploys keep the full five-way fan-out.** `deploy-api` runs at `max-parallel: 5` in prod, so
the five slot swaps land together and the environment approvals arrive at once; DEV runs two legs at a
time, each warming its own site before the next starts (2026-10-01, ADR-0015 D5(d)'s fallback).
Throttling prod as well is an owner option — one value in that `max-parallel` expression — and is not
taken here.

The SSR warm probe hits `/` rather than its `/health` route **on purpose**, and that is a different
question from Azure's own probe. `/health` (registered in `apps/cleansia.app/server.ts` ahead of the
Angular catch-all) proves only that the Node process is listening; a slot can pass it while the
Angular engine manifest fails to load and every real request 500s. `/` forces an actual SSR render, so
the swap gate tests the thing the site exists to do and warms the render path at the same time.
Azure's continuous `healthCheckPath` wants the opposite trade — cheap, render-free, once a minute —
so `main.bicep` points it at `/health` for all six web hosts, SSR included.

## 2. Autoscale (`autoscaleEnabled`, bounds)

One CPU-driven `autoscalesettings` on the shared plan (`modules/appServicePlan.bicep`): 1..3
instances, **+1 above 70% average CPU over 10 min, -1 below 30%**, 10-minute cooldowns. Deliberately
one-signal and symmetric so it cannot flap. Scaling the plan scales **every** site on it (5 APIs +
SSR + Functions). Chosen defaults (all overridable): floor 1 (cost-lean; raise to 2 for instance
redundancy), ceiling 3 (S1 allows up to 10).

## 3. Postgres HA + backup (`postgresHighAvailabilityMode`, `postgresGeoRedundantBackup`, `postgresBackupRetentionDays`)

- HA `ZoneRedundant` needs a GeneralPurpose/MemoryOptimized tier — the prod `Standard_D2s_v3` (already
  in the prod param) qualifies; the dev Burstable SKU rejects HA, hence env-switched. HA roughly
  doubles the server cost (a standby replica) — `SameZone` is the cheaper middle if zone redundancy
  is not required.
- **`geoRedundantBackup` is IMMUTABLE after server create**: it must be `Enabled` on the FIRST prod
  provision or never (flipping later forces a server replacement). It is in the prod param file now so
  the first provision gets it.
- Retention: prod 35 days (the Azure maximum), dev stays 7.

## 4. ACR image retention (`acrImageRetentionEnabled`, `acrImageRetentionDays`)

CI pushes one sha-tagged `cleansia-functions` image per deploy and nothing ever deletes them. The
built-in ACR `retentionPolicy` **cannot** fix this — it is Premium-only AND only deletes *untagged*
manifests. Instead: a **scheduled ACR Task** (runs on the Basic SKU) executes
`acr purge --filter '.*:.*' --ago 30d --keep 10 --untagged` nightly at 03:00 UTC — tags older than 30
days go, the newest 10 per repo always survive as rollback targets, orphaned untagged manifests are
swept. **Dev may flip this on too** (add `param acrImageRetentionEnabled = true` to
`weu.dev.bicepparam`) — the accumulation actually bites the dev registry first; kept default-off only
to honor the byte-unchanged dev rule.

## 5. App Insights sampling + ingestion cap (module-internal env switch)

`modules/appInsights.bicep`, following its existing pattern of env-keyed internals. **Ingestion is the
entire bill here** — dev measured 27.29 GB/month, all Analytics-tier, and that is where the ~€49/month
line came from. Retention is not a lever (see below).

- **`samplingPercentage`** — dev **10**, prod **50**. Dev was 100, i.e. no sampling at all; 10 puts dev
  ingestion inside the 5 GB/month pay-as-you-go free grant. Prod stays 50 deliberately: prod is
  authored, not deployed, so it is no part of the measured bill, and 50 is the rate at which the one
  rule that reads this component still resolves (below). Re-derive prod from real traffic, not from
  dev's number.
- **`dailyCapMb`** — dev **500**, prod **5000**. The unit is MB because Bicep has no float type and the
  useful dev value is fractional. `0` = uncapped. **The cap is a runaway-cost breaker, not a budget**:
  when hit, ingestion stops until the next UTC day and every alert over the workspace goes blind — if
  it trips in normal operation, raise it rather than live with it. Dev's previous 1 GB cap sat *above*
  the 0.88 GB/day dev was actually running, which is precisely why a year of drift produced no signal;
  500 MB is ~4× the new steady state and below the level a reverted sampling knob would restore.
- **`retentionInDays` is NOT a cost knob** — dev 30, prod 90, both unchanged and both deliberately so.
  31 days of analytics retention are included in the ingestion price, and Application Insights
  (`App*`) tables are kept **90 days at no charge** on top of that. Both values sit inside those
  allowances, so lowering either saves exactly €0 and only shortens the investigable window.

**Two couplings a prod go-live must not tune blind:**

- `alerts.bicep`'s exceptions spike reads `exceptions/count`, which is a **log-based** metric:
  `AppExceptions | summarize sum(itemCount)`. Sampling leaves the count unbiased but resolvable only
  in steps of `1/samplingPercentage` — steps of 2 against prod's threshold of 10, steps of 10 against
  dev's 25. Lowering prod sampling without lowering what that threshold claims turns a paging rule
  into a coin flip.
- `Cleansia.Functions/host.json` excludes `Exception` from the worker's own adaptive sampling, which
  means those exceptions reach the ingestion endpoint **unsampled** and are therefore sampled *here*.
  `samplingPercentage` is the only sampler that host's exceptions ever meet.

**The Basic table plan is not the lever it looks like.** All 27.29 GB is Analytics at $2.99/GB against
Basic's $0.65, but per Microsoft's current table-feature matrix `AppDependencies`, `AppRequests`,
`AppExceptions`, `AppMetrics` and `AppPerformanceCounters` **do not support the Basic plan at all** —
only `AppTraces` does (and `StorageQueueLogs`). Moving `AppTraces` would also give up the ability to
purge personal data from it, which is the wrong trade one week after `e84aed25` found a live reset
token on a `LogError` path. Revisit only if prod ingestion turns out to be `AppTraces`-dominated.

**One real prod-only retention cost, recorded rather than fixed here:** the workspace's 90-day setting
also applies to `StorageQueueLogs`, which is a resource-log table and so gets the 31-day allowance,
not the 90-day one — prod pays retention on days 32-90 of it. The fix, if it ever matters, is a
per-table retention override (a `Microsoft.OperationalInsights/workspaces/tables` child resource), not
a change to the workspace default.

## 6. Q-INFRA-03 — private database and Key Vault (`privateNetworkingEnabled`)

`weu.prod.bicepparam` sets the flag `true` (E-3), so production is private from its first provision
and its database is never public. Dev keeps `false` and is unchanged. What the flag does:

1. Deploys `vnet-cleansia-<region>-<env>` with `snet-apps` (delegated to `Microsoft.Web/serverFarms`)
   and `snet-privatelink`; private DNS zones + links for `privatelink.postgres.database.azure.com`,
   `privatelink.{blob,queue,table}.core.windows.net` and `privatelink.vaultcore.azure.net`; private
   endpoints `pe-{pg,blob,queue,table,kv}-…`. (Table is included because the Functions runtime store
   can touch it — stranding it public-only could brick the host.)
2. VNet-integrates all six web hosts, their staging slots, and the Functions host (`snet-apps`,
   `vnetRouteAllEnabled`), so the existing FQDN-based connection strings and the
   `@Microsoft.KeyVault(...)` app settings resolve to private IPs — no config change.
3. Postgres `publicNetworkAccess` → `Disabled`; **the `0.0.0.0` allow-Azure-services rule and the
   admin-IP rule are not created** (they only exist while public access is on). That `0.0.0.0` rule is
   why dev's posture is dev-only: it admits every source address inside Azure, other customers'
   subscriptions included, so from there the password is the only barrier. The private-endpoint model
   was chosen over VNet injection on purpose: a flexible server's network model is immutable after
   create — VNet injection would force replacing the server, a PE attaches to the existing one.
4. Key Vault public network access → `Disabled`, default action `Deny` (the `AzureServices` bypass
   stays).
5. **Storage is not made private.** Its network ACL default stays `Allow` in every stage; the hosts
   still reach it through the private endpoints of item 1. The order-photo, profile-photo and
   dispute-evidence links are SAS URLs on the account's public blob endpoint, loaded straight by the
   browsers and the mobile apps, and a `Deny` ACL refuses such a request (403 `AuthorizationFailure`)
   before the SAS is checked — the `AzureServices` bypass admits trusted Azure services, not browsers.
   What guards the public endpoint is authentication: no anonymous blob access, and in prod only an
   Entra identity or a user-delegation SAS (§7). Storage can go `Deny` only once blob reads reach
   clients through a path inside the network boundary, a proxy or a CDN; `main.bicep` passes `Allow`
   until then.

### CI: a temporary public window per run

A GitHub-hosted runner is outside the VNet, so `deploy-azure.yml` opens the database and the vault to
the runner's own IP for as long as it needs them and closes them again. Both jobs read the posture from
the param file Bicep deploys (step *Resolve the stage's network and database posture*), so the workflow cannot
disagree with the infrastructure; on dev none of the window steps run.

- **`provision`**, after Bicep: *Open the Key Vault to the runner* adds an IP rule for the runner, then
  turns public access on with the default action still `Deny`; the secret push runs; *Close the Key
  Vault to the public* (runs even on failure) turns public access off and removes the rule.
- **`migrate-database`**: *Open the private database for the migration* turns Postgres public access on
  and waits for `Enabled` + `Ready` (minutes); the per-run `ci-migrate-<run id>` firewall rule admits
  the runner; the vault opens the same way for the connection-string read, unless the stage uses the
  least-privilege login (§7), whose migration reads no vault secret; the migration runs; the
  rule is removed; *Close the private database and Key Vault to the public* (runs even on failure or
  cancellation) turns both off and waits until Postgres reports `Disabled`.
- While a window is open, the database takes TLS connections from the runner's IP alone and the vault
  answers the runner's IP alone. A self-hosted runner or jumpbox inside the VNet removes the window
  altogether; it is the step to take once deploys are frequent.
- **If a close fails**, the job fails with `… may still accept public traffic`. Close it by hand, and
  delete any `ci-migrate-*` firewall rule the run left:

  ```bash
  az postgres flexible-server update -g rg-cleansia-weu-prod -n pg-cleansia-weu-prod --public-access Disabled
  az keyvault update -n kv-cleansia-weu-prod -g rg-cleansia-weu-prod --public-network-access Disabled --default-action Deny
  ```

  The next Bicep provision also sets both back to `Disabled` and drops the vault's IP rules.

### Admin `psql`: the same window, by hand

Never while a `Deploy to PRO` run is in flight — its close would cut your session, and yours would cut
its migration. From your laptop (not Cloud Shell — its IP changes):

```bash
RG=rg-cleansia-weu-prod; PG=pg-cleansia-weu-prod
MY_IP=<your laptop's public IPv4>
az postgres flexible-server update -g $RG -n $PG --public-access Enabled     # waits; takes minutes
az postgres flexible-server firewall-rule create -g $RG -s $PG -n admin-psql --start-ip-address $MY_IP --end-ip-address $MY_IP
psql "host=$PG.postgres.database.azure.com port=5432 dbname=Cleansia user=cleansia_admin sslmode=require"

# when done — always, even if the session failed
az postgres flexible-server firewall-rule delete -g $RG -s $PG -n admin-psql --yes
az postgres flexible-server update -g $RG -n $PG --public-access Disabled
```

The password is the prod `POSTGRES_ADMIN_PASSWORD`; the Key Vault is not needed for this. To read a
vault secret by hand, open the vault the same way and close it after:

```bash
KV=kv-cleansia-weu-prod
az keyvault network-rule add -n $KV -g $RG --ip-address $MY_IP/32
az keyvault update -n $KV -g $RG --public-network-access Enabled --default-action Deny
# ... az keyvault secret show ...
az keyvault update -n $KV -g $RG --public-network-access Disabled --default-action Deny
az keyvault network-rule remove -n $KV -g $RG --ip-address $MY_IP/32
```

Postgres-**MI auth** (the other half of Q-INFRA-03) is NOT part of this seam — it needs Npgsql token
plumbing (an app code change) and stays an open owner question. §7 takes the smaller step: the hosts
no longer connect as the administrator.

## 7. E-4 — storage on managed identity, least-privilege database login

`weu.prod.bicepparam` sets both flags `true`, so production never has a long-lived storage key and its
hosts never hold the database administrator. Dev keeps both `false` and is unchanged: its hosts still
read `Storage--ConnectionString` and `ConnectionStrings--cleansia-db` as the admin, both written by
`derivedSecrets`.

### Storage (`storageManagedIdentityEnabled`)

- Every API host and the Functions app reach blobs and queues with their system-assigned managed
  identity — the Storage Blob/Queue Data Contributor grants `roleAssignments.bicep` already makes, the
  staging slots included. The app settings carry no secret: `BlobContainerConfiguration__AccountUrl`
  (the blob endpoint) and `QueueStorageConnectionString__queueServiceUri` +
  `QueueStorageConnectionString__credential=managedidentity` (the queue endpoint).
- The queue setting is the Functions **identity-based connection** named by every
  `[QueueTrigger(Connection = "QueueStorageConnectionString")]`, so the triggers, the Functions worker's
  sender and the API hosts' senders all move with one setting. The `ConnectionStrings__…` storage
  settings are absent, not empty — the Functions host prefers them when they exist.
- The order-photo, profile-photo and dispute-evidence links become **user-delegation SAS**: signed with
  a key the host asks Storage for under its identity, valid for the link's own lifetime (15 min / 1 h),
  never with the account key.
- The account sets `allowSharedKeyAccess: false`: a request signed with an account key or an account or
  service SAS is refused. `derivedSecrets` writes no `Storage--ConnectionString`; there is no storage
  secret anywhere.
- The Functions health probe lists queues instead of reading the queue service properties, which need a
  management role the identities do not have.
- **People need data roles too.** With shared keys off, the portal's storage browser and Storage
  Explorer authenticate as you: give yourself *Storage Blob Data Reader* (or Contributor) on
  `stcleansiaweuprod` and switch the portal blade's authentication method to *Microsoft Entra user
  account*. `az storage … --auth-mode login` works the same way; `--account-key` and connection strings
  do not.
- Turning this on for an existing account (not the prod plan — prod starts with it): deploy, confirm
  uploads, photo links and every queue drain, then delete the stale `Storage--ConnectionString` secret by
  hand; Bicep does not delete a secret it stops writing.

### Database (`postgresAppLoginEnabled`)

- The hosts connect as **`cleansia_app`**, which may `SELECT`, `INSERT`, `UPDATE` and `DELETE` rows in
  `public` and use its sequences — and nothing else: no `CREATE`, `ALTER`, `DROP`, `TRUNCATE`, no new
  roles or schemas. A SQL injection or a leaked host secret cannot change or drop the schema. The server
  administrator (`cleansia_admin`) is used by the migration alone.
- **Every connection verifies the server certificate** (`Ssl Mode=VerifyFull`, in place of
  `Require;Trust Server Certificate=true`), against the system trust store of the host and the runner.
- `deploy/db/grant-app-login.sql` creates the login and its grants. `deploy-azure.yml` runs it in
  `migrate-database` after every migration, inside the same temporary window (§6); it is idempotent, sets
  the login's password on every run, and its default privileges extend the grants to tables a later
  migration adds.
- Key Vault's `ConnectionStrings--cleansia-db` holds the **application login's** connection string. The
  provision job's secret push writes it on every deploy from the **`POSTGRES_APP_PASSWORD`** secret of
  `prod-weu` — never Bicep, because the password is external and the Bicep step is skipped when nothing
  in `deploy/bicep` changed. The migration builds the administrator's connection string from
  `POSTGRES_ADMIN_PASSWORD` itself, so **the administrator credential never enters Key Vault**, and the
  migrate job no longer opens the vault.
- `POSTGRES_APP_PASSWORD`: at least 24 letters and digits, nothing else (a `;` or `=` breaks the
  connection string). The provision job refuses a deploy without it before anything is provisioned.
- **Rotation**: change `POSTGRES_APP_PASSWORD` in `prod-weu` and run a deploy. The push writes the new
  secret, the migrate job sets the login's password, and every host restarts onto it in its deploy job.
  Open connections survive; a host that opens a new one between the migrate job and its own restart is
  refused, so rotate at a quiet hour.
- An admin `psql` (§6) still signs in as `cleansia_admin`; to check what the application can do, sign in
  as `cleansia_app` with the password from `POSTGRES_APP_PASSWORD`.

## Verification state

Authored and compile-verified with Bicep CLI 0.45.15 (`bicep build` on `main.bicep` + every module,
`bicep build-params` on both param files) — no cloud call was made and nothing was deployed. Dev
invariance was checked on the compiled template: the dev parameter values are byte-identical, every
new resource is condition-gated off by default, and every touched property evaluates to its previous
value under the dev defaults.

§6 as E-3 built it was compile-verified the same way with Bicep CLI 0.47.16: the compiled dev
parameters are byte-identical to before, the prod parameters differ only in
`privateNetworkingEnabled`, and the vault's `allowPublicNetworkAccess` compiles to
`not(privateNetworkingEnabled)`; the storage account's `networkDefaultAction` compiles to the literal
`Allow`, independent of the flag. The workflow's window steps were exercised locally against a stubbed
`az`; nothing was deployed.

§7 as E-4 built it was compile-verified with Bicep CLI 0.47.16: the compiled dev parameters are
byte-identical to before, and the prod parameters differ only in `storageManagedIdentityEnabled` and
`postgresAppLoginEnabled`. `grant-app-login.sql` was run twice against a local `postgres:16` by a
non-superuser administrator shaped like Azure's (`CREATEROLE`, a member of the database's owning role):
the login reads, writes, locks rows, takes advisory locks and inserts through serial and identity
columns, is refused `DROP`, `TRUNCATE`, `ALTER`, `CREATE TABLE`/`SCHEMA`/`INDEX`/`ROLE`/`DATABASE`,
reaches a table created after the grant, and the second run moved it to the new password. Nothing was
deployed and no Azure command was run. The backend CI now repeats the core of that run on every build:
`AppLoginGrantScriptTests` (integration suite) migrates a Testcontainers Postgres as that administrator,
runs the script twice through psql with the deploy step's variables, and fails if the login cannot
write the rows the hosts write (an EF commit, the outbox claim, the fiscal counter upsert), can create,
alter, drop or truncate a table, or cannot insert through a serial table that existed before the grant
or was created after it; it also pins that the deploy runs the script after the migration.
