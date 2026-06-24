// DEV parameter file for the West-Europe (weu) Cleansia footprint (ADR-0015 + ADR-0017).
// Param-file naming is <region>.<stage>.bicepparam (ADR-0017): this is region=weu, stage=dev.
//
// Applied by the owner / CI with:
//   az deployment group create --resource-group rg-cleansia-weu-dev \
//     --template-file deploy/bicep/main.bicep --parameters deploy/bicep/weu.dev.bicepparam
//
// ───────────────────────────────────────────────────────────────────────────────────────────────────
// SECRETS RULE (ADR-0015 D4, the standing law): NO real secret value is committed here — not the
// Postgres password, not a connection string, not a key. Only non-secret config (region, env, SKUs,
// the admin LOGIN name, tags) is literal. The Postgres admin PASSWORD is sourced at deploy time, two
// supported ways (pick one; the file ships the getSecret form, the CI-param form is the documented
// fallback):
//
//   (1) getSecret from a bootstrap Key Vault  ── the default below.
//       A small owner-provisioned BOOTSTRAP Key Vault (separate from the kv-cleansia-weu-dev that
//       main.bicep CREATES — that one does not exist yet on the first apply, and its values are
//       owner-populated post-deploy) holds the deploy-time Postgres admin password as a secret named
//       `postgres-admin-password`. The owner sets it once with `az keyvault secret set`. This file reads
//       it via `getSecret` on an `existing` reference — Bicep passes the secret straight into the
//       @secure() param at deploy time and it NEVER appears in source, the compiled template, or any
//       output. Replace the three bootstrapKeyVault coordinates below with the owner's real values
//       (they are resource identifiers, NOT secrets) before the apply.
//
//   (2) CI passes it as a parameter  ── the documented fallback (uncomment the override, delete the
//       getSecret assignment). The CI reads the GitHub-Environment `dev-weu` secret
//       (e.g. POSTGRES_ADMIN_PASSWORD) and appends it on the command line WITHOUT writing a literal to
//       the repo:
//         az deployment group create ... --parameters deploy/bicep/weu.dev.bicepparam \
//             postgresAdministratorPassword=$POSTGRES_ADMIN_PASSWORD
//       A command-line --parameters value overrides the same-named assignment in this file, so leaving
//       the getSecret form in place and ALSO passing it on the CLI is safe — the CLI wins.
// ───────────────────────────────────────────────────────────────────────────────────────────────────

using './main.bicep'

// ── Bootstrap Key Vault that holds the deploy-time Postgres admin password (option 1 above). ──────────
// These are resource COORDINATES, not secrets. The owner sets them to the real bootstrap vault. They
// are intentionally placeholders the owner edits out-of-band; CI may also override via the option-2
// CLI parameter, in which case this reference is unused.
var bootstrapKeyVaultSubscriptionId = '00000000-0000-0000-0000-000000000000' // owner: bootstrap vault subscription id
var bootstrapKeyVaultResourceGroup = 'rg-cleansia-weu-dev'                    // owner: bootstrap vault resource group
var bootstrapKeyVaultName = 'kv-cleansia-weu-dev-bootstrap'                   // owner: bootstrap vault name

resource bootstrapKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  scope: resourceGroup(bootstrapKeyVaultSubscriptionId, bootstrapKeyVaultResourceGroup)
  name: bootstrapKeyVaultName
}

// ── Region / stage seam (ADR-0017) ──────────────────────────────────────────────────────────────────
param region = 'weu'
param env = 'dev'

// ── Dev SKUs (ADR-0015 D2) ──────────────────────────────────────────────────────────────────────────
param appServicePlanSku = 'B2'
param staticWebAppSku = 'Free'
param postgresSkuName = 'Standard_B1ms'
param postgresSkuTier = 'Burstable'
param storageSku = 'Standard_LRS'

// ── Postgres admin credentials ──────────────────────────────────────────────────────────────────────
// The LOGIN is a non-secret config value. The PASSWORD is sourced from the bootstrap Key Vault via
// getSecret (option 1) — never a literal. For option 2 (CI passes it), comment the getSecret line and
// let the CLI --parameters override supply it; see the header.
param postgresAdministratorLogin = 'cleansia_admin'
param postgresAdministratorPassword = bootstrapKeyVault.getSecret('postgres-admin-password')

// ── Networking / RBAC placeholders the owner supplies ──────────────────────────────────────────────
// adminIpAddress: the owner/admin public IP allowed through the Postgres firewall for the EF-bundle
// apply + manual psql access. Placeholder — the owner sets their real IP, or CI overrides it via
// --parameters adminIpAddress=$ADMIN_IP from the dev-weu Environment. (No CIDR; a single /32 IP.)
param adminIpAddress = '0.0.0.0'

// ciPrincipalId: object id of the CI/provisioning principal granted Key Vault Secrets Officer. Empty
// string SKIPS that grant — the owner may grant it out of band (ADR-0015 D4). Leave '' unless the owner
// wires the CI principal id in (or CI overrides via --parameters ciPrincipalId=$CI_PRINCIPAL_ID).
param ciPrincipalId = ''

// ── Tags applied to every resource (commonTags in main.bicep adds project/region/env/managedBy) ──────
param tags = {
  costCenter: 'cleansia-dev'
  environment: 'dev'
}
