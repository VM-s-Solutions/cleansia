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
// Resource tags.
// ---------------------------------------------------------------------------------------------------

param tags = {
  project: 'cleansia'
  env: 'prod'
  region: 'weu'
  costCenter: 'platform'
  managedBy: 'bicep'
}
