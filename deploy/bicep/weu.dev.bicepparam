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

// ── Custom domains ──────────────────────────────────────────────────────────────────────────────────
// EMPTY ON PURPOSE. Every hostname below is already bound and certified in Azure, created out-of-band
// through the portal. Bicep must not try to own them.
//
// The reason is not preference, it is that Bicep CANNOT own them idempotently. Azure permits one
// certificate per canonicalName per App Service PLAN, and the certificate resource is named after
// whichever SITE created it. That name is not derivable: the certificate covering the customer web
// host exists as
//     customer.dev.cleansia.cz-api-cleansia-customer-mobile-weu-dev
// i.e. named after customer-mobile, a site that does not even serve that hostname. Any name Bicep
// guesses collides, returns Conflict 51021, and fails the ENTIRE release — every deploy job declares
// `needs: provision`, so one certificate conflict blocks the migration, all five APIs, Functions, the
// SSR host and both SPAs.
//
// So the two jobs this parameter used to conflate are now split: extraCorsOrigins tells the APIs which
// origins to trust (the only part that was ever needed once the domains exist), and
// customerWebBaseUrlOverride sets the customer-facing link base. Neither creates a resource.
//
// Populate customDomains ONLY for a hostname Bicep should create from nothing — a fresh environment
// where no binding or certificate exists yet. Adding one that already exists will fail the deploy.
param customDomains = {}

// ── Browser origins allowed through CORS (no binding, no certificate) ───────────────────────────────
// These three are where the frontends are actually served from. The API hostnames are deliberately
// absent: an API is not a browser origin.
param extraCorsOrigins = [
  'https://customer.dev.cleansia.cz'
  'https://partner.dev.cleansia.cz'
  'https://admin.dev.cleansia.cz'
]

// ── Social sign-in audiences for the customer web API ────────────────────────────────────────────────
// Both are public identifiers (they ship in the browser bundle), and both live here rather than in
// appsettings.json so DEV and PROD cannot share an audience and so a hand-set portal value cannot be
// wiped: the appSettings collection is replaced wholesale on every provision.
//
// This must stay equal to googleClientId in apps/cleansia.app/src/environments/environment.staging.ts
// (the nx configuration DEV builds) — GoogleTokenVerifier pins one audience, so drift rejects every
// web Google sign-in with auth.invalid_google_token.
param googleWebClientId = '354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com'

// Must equal appleClientId in environment.staging.ts — AppleTokenVerifier accepts only the audiences
// its host lists, so drift rejects every web sign-in with auth.invalid_apple_token. The Services ID
// is grouped under the primary App ID cz.cleansia.customer; that grouping is what keeps `sub` stable,
// so an iOS user reaches the same account on web rather than a freshly created one.
param appleWebServicesId = 'cz.cleansia.customer.web'

// ── Base for customer-facing links (SendGrid emails, Stripe success/cancel returns) ──────────────────
// Must be the hostname customers authenticate on. Returning from Stripe to *.azurewebsites.net would be
// Public-Suffix-separated from customer.dev.cleansia.cz, so the auth cookie would not be sent and the
// user would come back apparently logged out.
param customerWebBaseUrlOverride = 'https://customer.dev.cleansia.cz'

// ── Tags applied to every resource (commonTags in main.bicep adds project/region/env/managedBy) ──────
param tags = {
  costCenter: 'cleansia-dev'
  environment: 'dev'
}

