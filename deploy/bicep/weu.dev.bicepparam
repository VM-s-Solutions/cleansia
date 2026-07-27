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

// ── Custom domains (deployed-web same-site enabler) ─────────────────────────────────────────────────
// Deployed web cookie auth needs the frontends + APIs on ONE registrable domain: the auth cookie is
// SameSite=Strict and host-only, and the Azure default hostnames are on the Public Suffix List, so a
// frontend on *.dev.cleansia.cz calling *.azurewebsites.net is cross-SITE and the cookie is neither
// stored nor sent.
//
// Every key here is independently contains()-guarded in main.bicep, so any subset works — but a key may
// only be added once its CNAME and `asuid.<hostname>` TXT records exist, or hostNameBindings fails and
// takes the WHOLE release with it (every deploy job declares `needs: provision`).
//
// TWO DIFFERENT JOBS, easy to conflate:
//   * frontend keys (ssr / ssr-www / swa-*) drive CORS — frontendCustomDomainKeys reads ONLY these, so
//     they are what puts an origin into the platform allow-list and the CorsOrigins__0..n app settings
//     that override the committed Production JSON.
//   * api-* keys drive nothing in CORS. They bind the API hostnames, which is what makes the cookie
//     same-site, i.e. what makes LOGIN work.
// Omitting `ssr` while the customer web app was already being served from customer.dev.cleansia.cz is
// exactly why partner and admin logins worked and the customer one failed preflight with no
// Access-Control-Allow-Origin.
//
// Naming follows what actually exists in DNS (<audience>.dev / <audience>-api.dev), NOT the prod shape
// this file originally guessed at. All six hostnames verified live: each resolves to its App Service or
// Static Web App and carries the asuid TXT verification record.
//
// Adding `ssr` also moves customerWebBaseUrl (main.bicep) off the azurewebsites default onto the custom
// hostname, which is what SendGrid links and Stripe success/cancel returns are built from. That is
// wanted: a customer who authenticates on customer.dev.cleansia.cz and returns from Stripe to
// *.azurewebsites.net lands PSL-separated and appears logged out.
//
// The mobile API hosts are body-token (no cookies, no CORS) and need no custom domain.
// ssr-www is prod-only. There is no PROD environment yet.
param customDomains = {
  ssr: 'customer.dev.cleansia.cz'
  'swa-partner': 'partner.dev.cleansia.cz'
  'swa-admin': 'admin.dev.cleansia.cz'
  'api-partner': 'partner-api.dev.cleansia.cz'
  'api-admin': 'admin-api.dev.cleansia.cz'
  'api-customer': 'customer-api.dev.cleansia.cz'
}

// ── Tags applied to every resource (commonTags in main.bicep adds project/region/env/managedBy) ──────
param tags = {
  costCenter: 'cleansia-dev'
  environment: 'dev'
}

