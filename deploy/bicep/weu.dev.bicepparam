// DEV parameter file for the West-Europe (weu) Cleansia footprint (ADR-0015 + ADR-0017).
// Param-file naming is <region>.<stage>.bicepparam (ADR-0017): this is region=weu, stage=dev.
//
// Applied by the owner / CI with (the password comes from the CLI, never this file):
//   az deployment group create --resource-group rg-cleansia-weu-dev \
//     --template-file deploy/bicep/main.bicep --parameters deploy/bicep/weu.dev.bicepparam \
//     --parameters postgresAdministratorPassword=$POSTGRES_ADMIN_PASSWORD adminIpAddress=$ADMIN_IP_ADDRESS
//
// ───────────────────────────────────────────────────────────────────────────────────────────────────
// SECRETS RULE (ADR-0015 D4): NO real secret value is committed here. Only non-secret config (region,
// env, SKUs, the admin LOGIN name, tags) is literal. The Postgres admin PASSWORD is NOT assigned in this
// file at all — it is a @secure() param the CI/owner supplies at deploy time on the command line:
//     --parameters postgresAdministratorPassword=$POSTGRES_ADMIN_PASSWORD
// (a CLI --parameters value satisfies a param that this file leaves unset). It never appears in source,
// the compiled template, or any output. Likewise adminIpAddress + ciPrincipalId are supplied by CI (or
// the placeholders below are overridden on the CLI).
// ───────────────────────────────────────────────────────────────────────────────────────────────────

using './main.bicep'

// ── Region / stage seam (ADR-0017) ──────────────────────────────────────────────────────────────────
param region = 'weu'
param env = 'dev'

// ── Dev SKUs (ADR-0015 D2) ──────────────────────────────────────────────────────────────────────────
param appServicePlanSku = 'B2'
param staticWebAppSku = 'Free'
param postgresSkuName = 'Standard_B1ms'
param postgresSkuTier = 'Burstable'
param storageSku = 'Standard_LRS'

// ── Postgres admin LOGIN (non-secret). The PASSWORD is supplied on the CLI (see header) — not here. ──
param postgresAdministratorLogin = 'cleansia_admin'

// ── Networking / RBAC placeholders the owner/CI supplies on the CLI ─────────────────────────────────
// adminIpAddress: owner/admin public IP allowed through the Postgres firewall (single /32, no CIDR).
// CI overrides via --parameters adminIpAddress=$ADMIN_IP_ADDRESS from the dev-weu Environment.
param adminIpAddress = '0.0.0.0'

// ciPrincipalId: object id of the CI principal granted Key Vault Secrets Officer. '' SKIPS that grant
// (owner may grant out of band). CI may override via --parameters ciPrincipalId=$CI_PRINCIPAL_ID.
param ciPrincipalId = ''

// ── Alerting (ADR-0015 D3) — the ops email the dev Action Group notifies (not a secret) ─────────────
param alertEmail = 'cmisa695@gmail.com'

// ── Custom domains (deployed-web same-site enabler) — OFF until the owner creates the DNS records ───
// Deployed web cookie auth needs the frontends + APIs on ONE registrable domain (SameSite=Strict;
// the Azure default hostnames are PSL-separated sites). Uncomment — any subset works — ONLY AFTER the
// DNS records (CNAME + asuid TXT per hostname) exist: deploy/AZURE-DEV-RUNBOOK.md §12. The dev set
// below mirrors the prod shape (appsettings.Production.json / environment.prod.ts) under the dev zone.
// The mobile API hosts are body-token (no cookies/CORS) and need no custom domain.
// STEP 1 (this commit) — the two FRONTEND hostnames only. These already resolve (the SPAs are being
// served from them), so this subset deploys today with no new DNS. It is what fixes CORS:
// frontendCustomDomainKeys reads ONLY the swa-*/ssr keys, so adding these appends both origins to the
// platform CORS list AND emits the CorsOrigins__0..n app settings that override the committed
// Production JSON (whose origins are the PROD apex, not the dev zone).
//
// STEP 2 (next commit) — the api-* hostnames. Those do NOT fix CORS; they fix LOGIN. The auth cookie
// is SameSite=Strict and host-only, and azurewebsites.net is on the Public Suffix List, so a SPA on
// *.dev.cleansia.cz calling *.azurewebsites.net is cross-SITE: the cookie is never sent and the login
// Set-Cookie is refused. They require CNAME + `asuid.<host>` TXT records to exist FIRST or the
// hostNameBindings deployment fails.
//
// ssr / ssr-www / api-customer stay commented: no PROD environment exists yet and no DNS is created
// for dev.cleansia.cz, so binding them would fail the deploy for hostnames nothing is asking for.
param customDomains = {
  'swa-partner': 'partner.dev.cleansia.cz'
  'swa-admin': 'admin.dev.cleansia.cz'
}

// ── Tags applied to every resource (commonTags in main.bicep adds project/region/env/managedBy) ──────
param tags = {
  costCenter: 'cleansia-dev'
  environment: 'dev'
}

