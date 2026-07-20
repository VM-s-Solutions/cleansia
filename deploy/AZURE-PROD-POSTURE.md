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
| 5 | App Insights sampling + ingestion cap | module-internal env switch (`modules/appInsights.bicep`: `samplingPercentage`, `dailyCapGb`) | 100 (off), 1 GB/day | 50%, 5 GB/day | yes (module params) |
| 6 | VNet + private endpoints (Q-INFRA-03) | `privateNetworkingEnabled` | `false` | **`false` — the documented flag** | yes, see §6 |

Also env-switched in `main.bicep` (not a param): **Always On** — `alwaysOn: env == 'prod'` on the six
web hosts. S1 keeps prod instances warm; dev B2 keeps the cost posture.

## 1. Deployment slots + swap (`deploymentSlotsEnabled`)

Each of the six web hosts (5 APIs + the customer SSR) gets a **`staging` slot** mirroring the parent's
config 1:1, with its own system-assigned managed identity that receives the same Key Vault
Secrets User + Storage data grants (via `roleAssignments.bicep`) — a slot that cannot resolve its Key
Vault references would swap a broken instance into production.

- **No stop/start deploy pattern anywhere** — the swap replaces it: deploy to the slot, warm it, swap;
  the production site is never stopped.
- **The Functions host deliberately gets NO slot**: a warm staging Functions container would compete
  with production for the same Storage Queue messages (double-consumption). Functions deploys stay
  the container-set + restart they are today.
- S1 supports 5 slots per app; B-series rejects slot creation, which is why dev stays `false`.
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

**Workflow step (authored):** the six web-host deploy jobs in `.github/workflows/deploy-azure.yml`
now run the full slot flow whenever `inputs.env == 'prod'`: deploy the artifact to the `staging` slot
(`slot-name: staging` on `azure/webapps-deploy@v3`), **warm it** (curl the slot's health endpoint —
`/health` for the five APIs, `/` for the SSR since the Node host has no health probe — retrying up to
5 minutes and FAILING the job rather than swapping a cold/broken slot), then
`az webapp deployment slot swap … --slot staging --target-slot production`. The SSR's startup command
is set on the staging slot for prod (`appCommandLine` swaps with the slot). Dev keeps deploying
straight to the production site (B-series has no slots — path unchanged), and the Functions host keeps
its slotless container-set + restart deploy (the queue double-consumption rule above).

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

`modules/appInsights.bicep`, following its existing pattern of env-keyed internals (retention was
already `env == 'prod' ? 90 : 30`):

- `samplingPercentage` — prod 50, dev 100 (= off; the property is omitted, preserving the dev shape).
  SDK adaptive sampling layers on top.
- `dailyCapGb` — prod **5 GB/day** (was uncapped `{}`), dev keeps its historical 1 GB. `0` = uncapped.
  **The cap is a runaway-cost breaker, not a budget**: when hit, ingestion stops until the next UTC
  day and alerts go blind — if it trips in normal operation, raise it rather than live with it.

## 6. Q-INFRA-03 — VNet + private endpoints for Postgres + Storage (`privateNetworkingEnabled`)

The full seam is **authored and compiling** (`modules/privateNetworking.bicep`) but the flag stays
`false` even in the prod param file — this is the "full private networking may stay a documented
flag" half of Q-INFRA-03. What flipping it to `true` does, atomically:

1. Deploys `vnet-cleansia-<region>-<env>` with `snet-apps` (delegated to `Microsoft.Web/serverFarms`)
   and `snet-privatelink`; private DNS zones + links for `privatelink.postgres.database.azure.com`
   and `privatelink.{blob,queue,table}.core.windows.net`; private endpoints `pe-{pg,blob,queue,table}-…`.
   (Table is included because the Functions runtime store can touch it — stranding it public-only
   could brick the host mid-flip. Key Vault is NOT in scope: it keeps its own existing
   `allowPublicNetworkAccess` module param.)
2. VNet-integrates all six web hosts, their staging slots, and the Functions host (`snet-apps`,
   `vnetRouteAllEnabled`), so the existing FQDN-based connection strings resolve to private IPs — no
   config change.
3. Postgres `publicNetworkAccess` → `Disabled`; **the dev-accepted `0.0.0.0` allow-Azure-services rule
   and the admin-IP firewall rule disappear with it** (they only exist while public access is on).
   The private-endpoint model was chosen over VNet injection on purpose: a flexible server's network
   model is immutable after create — VNet injection would force replacing the live server, a PE
   attaches to the existing one.
4. Storage network ACL default → `Deny` (public endpoint stays on with the `AzureServices` bypass, so
   trusted platform services and ARM control-plane operations — `listKeys` for `derivedSecrets`,
   diagnostic settings, metric alerts — keep working).

**Hard prerequisites before the owner flips it** (why it is not defaulted on):

- **CI migrations break**: `migrate-database` opens a temporary public firewall rule for the GitHub
  runner — impossible with `publicNetworkAccess: Disabled`. The owner must first provide a private
  path (a self-hosted runner in `snet-apps`'s VNet, or accept a temporary public-enable window per
  migration).
- **Direct admin `psql` breaks** the same way (VPN/Bastion/jumpbox into the VNet, or temporary
  public-enable).
- Postgres-**MI auth** (the other half of Q-INFRA-03) is NOT part of this seam — it needs Npgsql token
  plumbing (an app code change) and stays an open owner question.

## Verification state

Authored and compile-verified with Bicep CLI 0.45.15 (`bicep build` on `main.bicep` + every module,
`bicep build-params` on both param files) — no cloud call was made and nothing was deployed. Dev
invariance was checked on the compiled template: the dev parameter values are byte-identical, every
new resource is condition-gated off by default, and every touched property evaluates to its previous
value under the dev defaults.
