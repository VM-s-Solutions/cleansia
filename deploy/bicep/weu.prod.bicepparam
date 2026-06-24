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
// Postgres administrator PASSWORD is sourced at deploy time from a BOOTSTRAP Key Vault the owner
// created out of band (`kv-cleansia-bootstrap-weu-prod`) via `getSecret` — Bicep resolves the value
// from Key Vault during the deployment and never writes it to source, a parameter output, or a log.
// The application's own Key Vault (`kv-cleansia-weu-prod`) is created BY this deployment, so it cannot
// be the source of a deploy-time param (chicken-and-egg); the bootstrap vault breaks that cycle. The
// owner may instead supply the password as a CI secure parameter — in neither case is it a literal.
//
// adminIpAddress + ciPrincipalId are PROD PLACEHOLDERS the owner replaces at prod-provision time
// (a real owner egress IP / the prod CI principal object id). They are config, not secrets.

using './main.bicep'

// ---------------------------------------------------------------------------------------------------
// Bootstrap Key Vault holding ONLY the deploy-time Postgres admin password. Owner-created out of band
// before any prod apply; referenced read-only here so `getSecret` can resolve the value without ever
// committing it. This is NOT the application secret vault (that one is provisioned by main.bicep).
// ---------------------------------------------------------------------------------------------------

resource bootstrapKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  // Owner replaces this resource-group with the prod bootstrap RG at provision time.
  scope: resourceGroup('rg-cleansia-bootstrap-weu-prod')
  name: 'kv-cleansia-bootstrap-weu-prod'
}

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
// Postgres admin identity. The login NAME is not a secret; the PASSWORD is resolved from the bootstrap
// Key Vault via getSecret — never a literal (ADR-0015 D4).
// ---------------------------------------------------------------------------------------------------

param postgresAdministratorLogin = 'cleansia_admin'
param postgresAdministratorPassword = bootstrapKeyVault.getSecret('Postgres--AdministratorPassword')

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
