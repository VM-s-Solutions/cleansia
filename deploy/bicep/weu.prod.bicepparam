// PROD parameter file for the Cleansia Azure footprint (ADR-0015 D1/D2 + ADR-0017 region seam).
//
// AUTHORED, NOT DEPLOYED. The deploy-dev workflow never references this file; a prod apply is a
// separate, owner-approved, protected (`prod-weu`) GitHub Environment dispatch. It is the SAME module
// set as dev at prod SKUs — the proof that prod is dev's topology at a different scale (ADR-0015 D1).
//
// Region seam (ADR-0017): region='weu' threads the `weu` token into every resource/RG/Key-Vault name
// (`api-cleansia-<audience>-weu-prod`, `pg-cleansia-weu-prod`, `kv-cleansia-weu-prod`, …). A second
// region is a new param value + a matrix entry, never a rename of a live resource.
//
// SECRET DISCIPLINE (ADR-0015 D4, the standing rule): NO real secret value lives in this file. The
// Postgres administrator PASSWORD is NOT assigned here — it is a @secure() param the prod CI/owner
// supplies at deploy time on the command line:
//     --parameters postgresAdministratorPassword=$POSTGRES_ADMIN_PASSWORD
// (a CLI --parameters value satisfies a param this file leaves unset), sourced from the protected
// `prod-weu` GitHub Environment secret. It never appears in source, the compiled template, or a log.
//
// adminIpAddress + ciPrincipalId are PROD PLACEHOLDERS the owner replaces at prod-provision time
// (a real owner egress IP / the prod CI principal object id). They are config, not secrets.

using './main.bicep'

// ---------------------------------------------------------------------------------------------------
// Region + stage (ADR-0017 region seam).
// ---------------------------------------------------------------------------------------------------

param region = 'weu'
param env = 'prod'

// ---------------------------------------------------------------------------------------------------
// PROD SKUs (ADR-0015 D1/D2 — prod is dev's topology at a different scale).
// ---------------------------------------------------------------------------------------------------

param appServicePlanSku = 'S1'
param staticWebAppSku = 'Standard'
param postgresSkuName = 'Standard_D2s_v3'
param postgresSkuTier = 'GeneralPurpose'
param storageSku = 'Standard_ZRS'

// ---------------------------------------------------------------------------------------------------
// Prod reliability posture (T-0359) — the seams the dev Bicep deliberately leaves off. Every value is
// overridable here; rationale + the flip sequences live in deploy/AZURE-PROD-POSTURE.md.
// ---------------------------------------------------------------------------------------------------

// Swap-based zero-downtime deploys: a "staging" slot on each web host (S1 supports slots; B2 does
// not). The prod deploy workflow must target the slot + swap — see AZURE-PROD-POSTURE.md §1.
param deploymentSlotsEnabled = true

// CPU-driven scale on the shared S1 plan: 1..3 instances, +1 above 70% avg CPU / -1 below 30%.
param autoscaleEnabled = true
param autoscaleMinInstances = 1
param autoscaleMaxInstances = 3

// Postgres resilience. geoRedundantBackup is IMMUTABLE after server create — it must be Enabled on
// the FIRST prod provision or never (flipping later replaces the server).
param postgresHighAvailabilityMode = 'ZoneRedundant'
param postgresGeoRedundantBackup = 'Enabled'
param postgresBackupRetentionDays = 35

// Nightly ACR purge: sha-tagged images older than 30 days go, the newest 10 per repo survive.
param acrImageRetentionEnabled = true
param acrImageRetentionDays = 30

// Q-INFRA-03 (VNet + private endpoints for Postgres/Storage) is DELIBERATELY NOT flipped here — it
// is the authored-but-owner-gated flag: enabling it cuts the CI migration path (the GitHub runner's
// temporary firewall rule needs public access) and direct admin psql until the owner provides a
// private path. Prerequisites + sequence: deploy/AZURE-PROD-POSTURE.md §6.
// param privateNetworkingEnabled = true

// ---------------------------------------------------------------------------------------------------
// Postgres admin LOGIN (non-secret). The PASSWORD is supplied on the CLI at deploy time (see header).
// ---------------------------------------------------------------------------------------------------

param postgresAdministratorLogin = 'cleansia_admin'

// ---------------------------------------------------------------------------------------------------
// Owner-supplied PROD placeholders — replaced at provision time (config, not secrets).
// ---------------------------------------------------------------------------------------------------

// REPLACE with the owner/admin egress public IP allowed through the Postgres firewall for the
// EF-bundle apply + manual access at prod-provision time.
param adminIpAddress = '0.0.0.0'

// REPLACE with the prod CI/provisioning principal object id (granted Key Vault Secrets Officer).
// Empty string skips the grant — the owner may grant it out of band instead.
param ciPrincipalId = ''

// ---------------------------------------------------------------------------------------------------
// Custom domains (deployed-web same-site enabler) — OFF until the owner creates the prod DNS records.
// This is the shape the committed prod config ALREADY assumes (appsettings.Production.json CorsOrigins
// + environment.prod.ts apiBaseUrl): frontends + APIs same-site under cleansia.cz, SameSite=Strict
// untouched. Uncomment ONLY AFTER the DNS records exist — subdomains need CNAME + asuid TXT; the apex
// (cleansia.cz) needs an A record + asuid TXT: deploy/AZURE-DEV-RUNBOOK.md §12. The mobile API hosts
// are body-token (no cookies/CORS) and need no custom domain.
// ---------------------------------------------------------------------------------------------------

// param customDomains = {
//   ssr: 'cleansia.cz'
//   'ssr-www': 'www.cleansia.cz'
//   'swa-partner': 'partner.cleansia.cz'
//   'swa-admin': 'admin.cleansia.cz'
//   'api-partner': 'api.cleansia.cz'
//   'api-admin': 'api-admin.cleansia.cz'
//   'api-customer': 'api-customer.cleansia.cz'
// }

// ---------------------------------------------------------------------------------------------------
// Resource tags.
// ---------------------------------------------------------------------------------------------------

param tags = {
  project: 'cleansia'
  env: 'prod'
  region: 'weu'
  costCenter: 'platform'
  managedBy: 'bicep'
}

