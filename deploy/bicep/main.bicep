// Cleansia Azure footprint — the RG-scoped orchestration that wires the already-authored modules into
// one declarative deployment (ADR-0015). Deployed with:
//   az deployment group create --resource-group rg-cleansia-<region>-<env> \
//     --template-file deploy/bicep/main.bicep --parameters deploy/bicep/<region>.<env>.bicepparam
//
// It instantiates the ONE reusable appService module six times (the five API hosts + the customer
// SSR) — the customer-mobile host the old four-host YAML omitted is included so the iOS customer app
// has a stable dev URL (ADR-0015 D2/D6). Config flows as App Service settings that are Key Vault
// REFERENCE strings resolved by each host's system-assigned managed identity (ADR-0015 D4); NO secret
// value is committed here — only Key Vault secret NAMES and reference URIs.
//
// Region seam (ADR-0017): the `region` token (default weu) is threaded into every module that emits a
// name or location, so a second region is a new param value, not a rename/recreate of a live resource.
// Region is INFRA/config only — it is never a clause in the tenancy filter and never branched on in a
// handler.

targetScope = 'resourceGroup'

// ---------------------------------------------------------------------------------------------------
// Parameters
// ---------------------------------------------------------------------------------------------------

@description('Expansion-seam region token (ADR-0017) threaded into every name and resolved to an Azure location. Default West Europe.')
param region string = 'weu'

@description('Deployment stage: dev | prod. Drives names, SKUs, and the dev/prod parameter file.')
@allowed([
  'dev'
  'prod'
])
param env string

@description('App Service Plan SKU. Dev = B2 (ADR-0015 D2 owner override); prod = S1.')
param appServicePlanSku string = 'B2'

@description('Static Web App tier. Dev = Free; prod = Standard.')
@allowed([
  'Free'
  'Standard'
])
param staticWebAppSku string = 'Free'

@description('PostgreSQL Flexible Server SKU name. Dev = Standard_B1ms (Burstable); prod = a GeneralPurpose sku.')
param postgresSkuName string = 'Standard_B1ms'

@description('PostgreSQL Flexible Server SKU tier.')
@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param postgresSkuTier string = 'Burstable'

@description('Storage redundancy SKU. Dev = Standard_LRS.')
@allowed([
  'Standard_LRS'
  'Standard_ZRS'
  'Standard_GRS'
])
param storageSku string = 'Standard_LRS'

@description('PostgreSQL administrator login name. NOT a secret (the password is).')
param postgresAdministratorLogin string

@description('PostgreSQL administrator password. Supplied at deploy time on the CLI (--parameters postgresAdministratorPassword=$POSTGRES_ADMIN_PASSWORD) from the GitHub-Environment secret — NEVER a literal in source. The empty default lets the .bicepparam omit it (the CLI override always provides the real value).')
@secure()
param postgresAdministratorPassword string = ''

@description('Owner/admin public IP allowed through the Postgres firewall for the EF-bundle apply + manual access.')
param adminIpAddress string

@description('Object id of the CI/provisioning principal granted Key Vault Secrets Officer. Empty string skips it (owner may grant out of band).')
param ciPrincipalId string = ''

@description('Ops email the alerts Action Group notifies (ADR-0015 D3). Empty string SKIPS the alerts module entirely (prod supplies its own address when it goes live).')
param alertEmail string = ''

@description('''
Custom hostnames under the ONE registrable domain (cleansia.cz), keyed by host token. Default {} =
no custom domains — zero behavior change. This is the same-site enabler for deployed web cookie auth:
the HttpOnly SameSite=Strict auth cookies only flow when frontend + API share a registrable
domain, and the Azure default hostnames (*.azurewebsites.net / *.azurestaticapps.net) are
PSL-separated sites, so a deployed web URL cannot authenticate without these.

Recognized keys (any subset; values are bare hostnames, no scheme):
  api-partner | api-admin | api-customer | api-partner-mobile | api-customer-mobile   (the API hosts)
  ssr | ssr-www                                                    (the customer SSR; www = prod only)
  swa-partner | swa-admin                                                             (the two SPAs)

Setting any frontend key (ssr/ssr-www/swa-*) also folds that origin into the browser-host CORS (both
the App Service platform CORS and the app-level CorsOrigins app settings) and, for `ssr`, moves
customerWebBaseUrl (SendGrid links + Stripe redirects) onto the custom domain.

DNS is the hard precondition: the CNAME (or apex A) + asuid TXT records must exist BEFORE a deploy
that sets an entry, or the deployment fails at the binding/validation step — the records, the
recommended per-env hostnames, and the owner sequence live in deploy/AZURE-DEV-RUNBOOK.md §12.
''')
param customDomains object = {}

@description('''Browser origins to allow through CORS, WITHOUT binding anything. Use this when the
hostnames are already bound and certified outside Bicep (portal / az CLI), which is the normal way a
domain gets cut over under time pressure.

Why this exists separately from `customDomains`: that parameter conflated two unrelated jobs — telling
the APIs which origins to trust, and creating hostname bindings plus managed certificates. Only the
first is ever needed once the domains exist. Worse, the second cannot be made idempotent against
out-of-band work: Azure permits ONE certificate per canonicalName per App Service PLAN, and that
certificate resource is named after whichever SITE created it, which is not derivable — a certificate
for the customer web host was found under the name
`customer.dev.cleansia.cz-api-cleansia-customer-mobile-weu-dev`. Any name Bicep picks therefore risks a
Conflict that fails the entire release, because every deploy job declares `needs: provision`.

Full origins including scheme, e.g. [ 'https://admin.dev.cleansia.cz' ].''')
param extraCorsOrigins string[] = []

@description('''Google OAuth **web** client id for the customer web API's token verification.

Per-environment on purpose. GoogleTokenVerifier pins a single audience, so this must be the client id
that THIS environment's browser bundle uses (apps/cleansia.app/src/environments/environment.*.ts) —
any drift rejects every web Google sign-in with auth.invalid_google_token. It is also a DIFFERENT
client from the mobile host's native one, whose tokens must not be accepted here.

Public identifier, not a secret: it ships inside the browser bundle either way, so it belongs in the
parameter file rather than Key Vault. Empty disables web Google sign-in (fails closed).''')
param googleWebClientId string = ''

@description('''Sign in with Apple **Services ID** for the customer web API's token verification.

This is the WEB audience. Never put the native bundle id here: the customer web API issues HttpOnly
session cookies, so accepting the native audience would let a captured iOS (identityToken, rawNonce)
pair POSTed to it mint a browser session. The mobile host carries the bundle id and this host does
not — AppleAudienceIsolationConfigPinTests pins both directions.

Per-environment, because a Services ID is registered against specific domains and return URLs. Public
identifier, same reasoning as googleWebClientId. Empty disables web Apple sign-in (fails closed),
which is the correct state until the Services ID is provisioned.''')
param appleWebServicesId string = ''

@description('''Base URL for customer-facing links — SendGrid emails and Stripe success/cancel returns.
Set this when the customer web app is served from a custom hostname that Bicep does not bind. Leaving
it empty derives the value from `customDomains.ssr`, falling back to the SSR default hostname.

It matters that this tracks the hostname users actually authenticate on: a customer who signs in at
customer.dev.cleansia.cz and returns from Stripe to *.azurewebsites.net is on a Public-Suffix-separated
site, so the auth cookie is not sent and they appear logged out.''')
param customerWebBaseUrlOverride string = ''

@description('Set true ONLY after the Key Vault secret Fcm--ServiceAccountJson exists — it wires FCM__ServiceAccountJson onto the Functions host. Default false keeps the push dispatcher in its clean disabled no-op instead of dead-lettering on an unresolvable KV reference.')
param fcmSecretProvisioned bool = false

@description('Set true ONLY after the Key Vault secrets Fiscal--CzechEet2--ApiKey and Fiscal--CzechEet2--CertificatePassword exist — it swaps the two empty fiscal placeholder app settings for Key Vault references (same shape as fcmSecretProvisioned). Default false keeps the settings as literal empty strings: a KV reference to a missing secret does NOT degrade to empty — the app receives the raw reference string as the config value.')
param fiscalSecretProvisioned bool = false

@description('Set true ONLY after the Key Vault secrets Apns--KeyId, Apns--TeamId, and Apns--PrivateKeyPem exist (seeded from the SAME team-scoped .p8 already in backend custody for ordinary push — no new key) — it wires the six APNS__* live-activity app settings (ADR-0029 D1) onto the Functions host as KV references + the literal bundle id/sandbox and flips APNS__Enabled on. Default false emits NOTHING (union with an empty object) so the dev Functions host is byte-unchanged and the channel binds Enabled=false by C# default — the whole live-activity pipeline ships INERT. Same order-matters gate as fcmSecretProvisioned: an unresolvable KV reference hands the app the raw reference string, so keep this false until the three secrets exist AND the iOS lane (LA-5) ships.')
param apnsSecretProvisioned bool = false

// ---------------------------------------------------------------------------------------------------
// Prod reliability posture (T-0359). Every knob defaults to the dev value, so a dev deploy with the
// unchanged weu.dev.bicepparam is behavior-identical; weu.prod.bicepparam flips them. Full rationale,
// override guidance, and the owner flip sequence: deploy/AZURE-PROD-POSTURE.md.
// ---------------------------------------------------------------------------------------------------

@description('Deploy a "staging" slot on each web host (the 5 APIs + the SSR) for swap-based zero-downtime deploys. Needs a Standard+ plan SKU (prod S1) — B-series rejects slot creation, so dev stays false. The Functions host deliberately gets NO slot: a warm staging Functions container would compete for the same queue messages as production.')
param deploymentSlotsEnabled bool = false

@description('Enable the CPU-driven autoscale rule on the shared plan. Needs Standard+ (prod S1); dev B2 stays a fixed single instance.')
param autoscaleEnabled bool = false

@description('Autoscale instance floor. 1 keeps prod cost-lean; raise to 2 for instance redundancy.')
param autoscaleMinInstances int = 1

@description('Autoscale instance ceiling. S1 allows up to 10.')
param autoscaleMaxInstances int = 3

@description('Postgres high availability. Dev = Disabled; prod = ZoneRedundant (requires the GeneralPurpose tier — Burstable rejects HA).')
@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param postgresHighAvailabilityMode string = 'Disabled'

@description('Postgres geo-redundant backup. IMMUTABLE after server create — prod must set it at provision time; flipping it later forces a server replacement.')
@allowed([
  'Disabled'
  'Enabled'
])
param postgresGeoRedundantBackup string = 'Disabled'

@description('Postgres backup retention in days (7-35). Dev = 7; prod = 35.')
@minValue(7)
@maxValue(35)
param postgresBackupRetentionDays int = 7

@description('Enable the nightly ACR purge task (CI pushes one sha-tagged image per deploy and nothing ever deletes them). Dev may flip this on too — the accumulation bites the Basic registry in dev first.')
param acrImageRetentionEnabled bool = false

@description('ACR purge age cutoff in days (the newest 10 tags per repo always survive as rollback targets).')
param acrImageRetentionDays int = 30

@description('''
The Q-INFRA-03 hardening seam: VNet + private endpoints for Postgres and Storage. When true it
deploys modules/privateNetworking.bicep, VNet-integrates every App Service/Functions host, flips
Postgres publicNetworkAccess to Disabled (the dev-accepted 0.0.0.0 allow-Azure-services rule and the
admin-IP rule disappear with it), and sets the Storage network ACL default to Deny.

DELIBERATELY LEFT false EVEN IN THE PROD PARAM FILE — a documented flag, not a default: flipping it
breaks the CI migration path (the GitHub runner's temporary firewall rule needs public access) and
direct admin psql until the owner provides a private path. Prerequisites + sequence:
deploy/AZURE-PROD-POSTURE.md.
''')
param privateNetworkingEnabled bool = false

@description('Resource tags applied to every resource.')
param tags object = {}

// ---------------------------------------------------------------------------------------------------
// Region -> Azure location resolution (ADR-0017). A second region adds a map entry, not a rewrite.
// ---------------------------------------------------------------------------------------------------

var regionToLocation = {
  weu: 'westeurope'
}
var location = regionToLocation[region]

// Static Web Apps are available in a subset of regions; West Europe is supported and co-located here.
var staticWebAppLocation = location

var commonTags = union(tags, {
  project: 'cleansia'
  region: region
  env: env
  managedBy: 'bicep'
})

// ---------------------------------------------------------------------------------------------------
// Host topology (ADR-0015 D2). The five API hosts + the SSR — the reusable appService instantiated
// once per entry. `browserFacing` hosts get the dev SPA/SSR origins in CORS; the two mobile hosts are
// body-token (no cookies/CSRF) so their CORS stays closed (an empty array). `needsStripe` adds the
// Stripe Key Vault references (the customer + customer-mobile hosts initiate payments).
// ---------------------------------------------------------------------------------------------------

var apiHosts = [
  {
    audience: 'partner'
    browserFacing: true
    needsStripe: false
  }
  {
    audience: 'admin'
    browserFacing: true
    needsStripe: false
  }
  {
    audience: 'customer'
    browserFacing: true
    needsStripe: true
    // Web channel: Stripe Checkout Sessions (OrderPaymentDispatcher mints a session only on Web),
    // plus the membership/subscription and chargeback events. Its own Stripe endpoint => its own
    // signing secret, so rotating one channel cannot silently break the other.
    webhookSecretName: 'Stripe--WebhookSecret'
  }
  {
    audience: 'partner-mobile'
    browserFacing: false
    needsStripe: false
  }
  {
    audience: 'customer-mobile'
    browserFacing: false
    needsStripe: true
    // Mobile channel: Stripe PaymentSheet PaymentIntents (payment_intent.*), delivered to a SEPARATE
    // Stripe endpoint so web and mobile traffic are traceable apart — hence a separate signing secret.
    webhookSecretName: 'Stripe--WebhookSecretMobile'
  }
]

var apiLinuxFxVersion = 'DOTNETCORE|10.0'
var ssrLinuxFxVersion = 'NODE|20-lts'

// Deploy-time site NAMES — the single source both the appService instantiations and the alerts
// module consume. Alert scopes must be deploy-time strings (module outputs cannot be collected for
// the alerts for-loop: BCP182), so the names live here rather than being read back from the modules.
var apiSiteNames = [for host in apiHosts: 'api-cleansia-${host.audience}-${region}-${env}']
var ssrSiteName = 'web-cleansia-customer-${region}-${env}'

// ---------------------------------------------------------------------------------------------------
// Foundation: observability, secret store, storage, registry, database.
//
// appInsights.bicep ships the component AND its Log Analytics workspace because there is no shape in
// which it ships one of them: Application Insights is workspace-based only (classic components are
// retired, and a component declared without WorkspaceResourceId gets a workspace auto-provisioned
// beside it), so "keep App Insights, drop the workspace" is not a configuration that exists. The cost
// lever is that module's sampling + daily-cap knobs, not the resource list. Error telemetry does not
// depend on any of it: Sentry (Sentry__Dsn) covers the five API hosts and, since ac2243d2, the
// Functions worker, on its own unsampled path. The SSR (Node) host ships to neither — filed, not fixed.
// ---------------------------------------------------------------------------------------------------

module appInsights 'modules/appInsights.bicep' = {
  name: 'appInsights'
  params: {
    location: location
    region: region
    env: env
    tags: commonTags
  }
}

module keyVault 'modules/keyVault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    region: region
    env: env
    allowPublicNetworkAccess: true
    tags: commonTags
  }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    region: region
    stage: env
    skuName: storageSku
    networkDefaultAction: privateNetworkingEnabled ? 'Deny' : 'Allow'
    tags: commonTags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    location: location
    region: region
    env: env
    imageRetentionEnabled: acrImageRetentionEnabled
    imageRetentionDays: acrImageRetentionDays
    tags: commonTags
  }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: {
    location: location
    region: region
    stage: env
    skuName: postgresSkuName
    skuTier: postgresSkuTier
    highAvailabilityMode: postgresHighAvailabilityMode
    geoRedundantBackup: postgresGeoRedundantBackup
    backupRetentionDays: postgresBackupRetentionDays
    publicNetworkAccess: privateNetworkingEnabled ? 'Disabled' : 'Enabled'
    administratorLogin: postgresAdministratorLogin
    administratorPassword: postgresAdministratorPassword
    adminIpAddress: adminIpAddress
    tags: commonTags
  }
}

// The Q-INFRA-03 seam, materialized only when the flag is on: VNet + private endpoints + private DNS
// for Postgres/Storage. The app subnet id it outputs is what VNet-integrates every host below.
module privateNetworking 'modules/privateNetworking.bicep' = if (privateNetworkingEnabled) {
  name: 'privateNetworking'
  params: {
    location: location
    region: region
    env: env
    postgresServerId: postgres.outputs.serverId
    storageAccountId: storage.outputs.storageAccountId
    tags: commonTags
  }
}

// ---------------------------------------------------------------------------------------------------
// Key Vault reference helpers — secret NAMES only; the secret VALUES are owner/CI-populated in Key
// Vault. Each App Service resolves the @Microsoft.KeyVault(...) reference via its managed identity.
// ---------------------------------------------------------------------------------------------------

var keyVaultUri = keyVault.outputs.keyVaultUri

func kvRef(vaultUri string, secretName string) string =>
  '@Microsoft.KeyVault(SecretUri=${vaultUri}/secrets/${secretName})'

// ONE definition of the Mapbox geocoding token reference, consumed by BOTH host families below.
// The .NET API hosts bind it as `Mapbox__GeocodingAccessToken` (App Service maps `__` -> `:`), while
// the Node SSR host binds the SAME secret as `MAPBOX_TOKEN` — server.ts reads `process.env.MAPBOX_TOKEN`
// verbatim and Node performs no `__` translation, so the API-host key name would be invisible to it.
// Same secret, two binding names; never add a second Key Vault secret for this.
var mapboxTokenKvRef = kvRef(keyVaultUri, 'Mapbox--GeocodingAccessToken')

// Browser host (SPA/SSR) CORS origins — the dev SWA + SSR default hostnames, not localhost (D3).
// When customDomains adds same-site frontend hostnames they are APPENDED — the default hostnames keep
// serving during the cut-over, nothing is removed. Every map lookup is defensively contains()-guarded
// so an absent key can never surface as a lookup failure.
var frontendCustomDomainKeys = [
  'ssr'
  'ssr-www'
  'swa-partner'
  'swa-admin'
]
var customFrontendOriginCandidates = [
  for key in frontendCustomDomainKeys: contains(customDomains, key) ? 'https://${customDomains[key]}' : ''
]
var customFrontendOrigins = union(filter(customFrontendOriginCandidates, origin => !empty(origin)), extraCorsOrigins)
var browserCorsOrigins = concat([
  'https://${staticWebApps[0].outputs.defaultHostName}'
  'https://${staticWebApps[1].outputs.defaultHostName}'
  'https://${ssr.outputs.defaultHostName}'
], customFrontendOrigins)

// App-level CORS must AGREE with the platform CORS above: the hosts read `CorsOrigins` from config,
// and a deployed host runs the Production JSON whose origins are the PROD cleansia.cz set — a dev
// custom-domain frontend would pass platform CORS yet be refused by the app. When (and only when)
// customDomains adds frontend origins, the FULL origin list is emitted as indexed app settings
// (CorsOrigins__0..n), which override the JSON entries by index; the emitted list (>= 3 entries) is
// always at least as long as the committed arrays (<= 2), so no stale JSON tail can leak through the
// index merge (.NET merges JSON config arrays BY INDEX). Empty customDomains emits nothing — zero
// behavior change.
var corsOriginsAppSettings = empty(customFrontendOrigins) ? {} : toObject(range(0, length(browserCorsOrigins)), i => 'CorsOrigins__${i}', i => browserCorsOrigins[i])

// Social sign-in audiences for the CUSTOMER WEB api only — the host that issues browser session
// cookies. Each key is omitted entirely when its parameter is empty, so an unset provider keeps the
// blank value committed in appsettings.json and the verifier fails closed, rather than being handed
// an empty app setting. Note the deliberate asymmetry with the mobile host: this one must never
// receive Apple__BundleId (see the appleWebServicesId parameter).
var customerWebAuthSettings = union(
  empty(googleWebClientId) ? {} : { Google__ClientId: googleWebClientId },
  empty(appleWebServicesId) ? {} : { Apple__WebServicesId: appleWebServicesId }
)

// App settings shared by every API host: DB + Storage + JWT + SendGrid + Sentry + Mapbox Key Vault
// references, plus the (non-secret) App Insights connection string. The `__` -> `:` App Service
// mapping means the app reads its existing config keys with no code change (ADR-0015 D4).
// The customer SSR host — the base for customer-facing links in emails/Stripe redirects (reset
// password, order status, checkout success/cancel). The SSR default hostname until the `ssr` custom
// domain is configured — then the links move onto it.
var ssrCustomHost = contains(customDomains, 'ssr') ? customDomains.ssr : ''
var derivedCustomerWebBaseUrl = empty(ssrCustomHost) ? 'https://${ssr.outputs.defaultHostName}' : 'https://${ssrCustomHost}'
var customerWebBaseUrl = empty(customerWebBaseUrlOverride) ? derivedCustomerWebBaseUrl : customerWebBaseUrlOverride

// SendGrid email config — ONE shared definition consumed by BOTH the API hosts and the Functions app
// (the Functions container ships an appsettings.json with only cron schedules, so it receives ZERO
// application config unless set here — a missing template id throws ArgumentNullException at send time).
// All 6 dynamic-template ids are supplied via GitHub secrets → Key Vault → these references (set the 6
// SENDGRID_*_TEMPLATE_ID GitHub secrets; the CI push writes them to KV). Each email uses its OWN
// dedicated template — never a substitute.
// LINK COMPOSITION (EmailService concatenates): resetLink = ClientDomainUrl + ResetPasswordUrl, and the
// receipt track-order link = ClientDomainUrl + '/track-order'. So ClientDomainUrl is the customer web
// BASE URL and ResetPasswordUrl is a PATH — setting ResetPasswordUrl to a full URL would double the host.
var sendGridSettings = {
  SendGrid__ApiKey: kvRef(keyVaultUri, 'SendGrid--ApiKey')
  SendGrid__ResetPasswordTemplateId: kvRef(keyVaultUri, 'SendGrid--ResetPasswordTemplateId')
  SendGrid__OrderReceiptTemplateId: kvRef(keyVaultUri, 'SendGrid--OrderReceiptTemplateId')
  SendGrid__EmailConfirmationTemplateId: kvRef(keyVaultUri, 'SendGrid--EmailConfirmationTemplateId')
  SendGrid__PeriodClosedTemplateId: kvRef(keyVaultUri, 'SendGrid--PeriodClosedTemplateId')
  SendGrid__PeriodEndReminderTemplateId: kvRef(keyVaultUri, 'SendGrid--PeriodEndReminderTemplateId')
  SendGrid__OrderStatusUpdateTemplateId: kvRef(keyVaultUri, 'SendGrid--OrderStatusUpdateTemplateId')
  SendGrid__AddressFrom: 'it@cleansia.cz'
  SendGrid__ClientDomainUrl: customerWebBaseUrl
  SendGrid__ResetPasswordUrl: '/forgot-password'
}

// Timer schedules for every function that declares `[TimerTrigger("%SomeCron%")]` rather than a
// literal cron. THESE ARE NOT DECORATION AND THEY ARE NOT A DUPLICATE OF appsettings.json.
//
// The `%token%` is expanded by the Functions HOST at indexing time, from PLATFORM APPLICATION SETTINGS.
// `src/Cleansia.Functions/appsettings.json` is loaded by Program.cs into the ISOLATED WORKER's
// IConfiguration — a different process and a different configuration object, which the host never reads.
// With no setting here the token does not resolve, the timer listener is never created, and the function
// simply never runs: no error, no invocation, no telemetry. Exactly the failure mode the SendEmail
// templateId comment above describes, on a different key.
//
// Measured 2026-08-22: SendNewJobsDigest, MaterializeRecurringBookings, SendPreCleaningReminders,
// SendRecurringOrderReminders, SendMembershipLifecycleNotifications and ExpireStaleReferrals had never
// fired in DEV. The timers carrying literal crons — OutboxDrainer, CleanupStalePendingOrders and the
// rest — were unaffected, which is what made the split visible. The map below carries eight keys, not
// six: SendCleanerJobReminders and SendTomorrowJobDigest were added by the same change and would have
// landed in exactly the same hole.
//
// Values are copied verbatim from src/Cleansia.Functions/appsettings.json so this changes nothing except
// whether the schedules exist at all. TimerCronSettingsAreDeployedTests fails the build if a tokenized
// timer is added without a matching key here.
var cronSettings = {
  MaterializeRecurringBookingsCron: '0 0 2 * * *'
  SendRecurringOrderRemindersCron: '0 30 2 * * *'
  SendPreCleaningRemindersCron: '0 */5 * * * *'
  SendCleanerJobRemindersCron: '0 */5 * * * *'
  SendTomorrowJobDigestCron: '0 0 * * * *'
  SendMembershipLifecycleNotificationsCron: '0 0 3 * * *'
  SendNewJobsDigestCron: '0 0 * * * *'
  ExpireStaleReferralsCron: '0 30 3 * * *'
}

var apiBaseSettings = union({
  ConnectionStrings__ConnectionString: kvRef(keyVaultUri, 'ConnectionStrings--cleansia-db')
  ConnectionStrings__BlobContainerConfigurationConnectionString: kvRef(keyVaultUri, 'Storage--ConnectionString')
  ConnectionStrings__QueueStorageConnectionString: kvRef(keyVaultUri, 'Storage--ConnectionString')
  JwtSettings__Secret: kvRef(keyVaultUri, 'Jwt--Key')
  // CSRF secret — a true secret pushed by CI (like the other externals). The app throws on an empty
  // secret only when Csrf:Enabled=true; dev runs disabled, but we still supply it so enabling CSRF is
  // a one-flag flip.
  Csrf__Secret: kvRef(keyVaultUri, 'Csrf--Secret')
  Sentry__Dsn: kvRef(keyVaultUri, 'Sentry--Dsn')
  Mapbox__GeocodingAccessToken: mapboxTokenKvRef
  // Read by Cleansia.ServiceDefaults `AddTelemetryExporters`, which registers the Azure Monitor OTel
  // exporter only when this is non-empty. It was inert on every API host until T-0500: the exporter
  // existed but hung off an AddServiceDefaults overload no host calls, so the value arrived and nothing
  // read it. Deleting it silently disables the App Insights export — the registration is skipped, not
  // failed — leaving Sentry as these hosts' sole error path. It is not decoration.
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
  // ADR-0003 D3: the app refuses to boot in non-Development unless it's told which proxy network to
  // trust for X-Forwarded-For. Behind App Service the only hop is the App Service front end, which
  // forwards from its internal range — 100.64.0.0/10 (carrier-grade NAT, what App Service uses). The /10
  // satisfies the "no /0–/8 supernet" guard: it trusts the App Service front end, not the public net.
  ForwardedHeaders__KnownNetworks: '100.64.0.0/10'
  ForwardedHeaders__ForwardLimit: '1'
  // No ASPNETCORE_ENVIRONMENT is set anywhere in this template or the workflows, so every deployed host
  // — dev included — runs as Production and binds appsettings.Production.json, which pins
  // Logging:LogLevel:Default to Warning. That silently withheld every Information-level line the
  // background sweeps emit, including NewJobsDigest's own "sweep complete" summary, which is the single
  // most useful triage line in the system.
  //
  // Host.CreateDefaultBuilder layers environment variables AFTER the JSON files and App Service surfaces
  // every app setting as one, so this wins without a code change or a new appsettings file.
  //
  // SCOPED TO THE `Cleansia` CATEGORY ON PURPOSE, never Default. Lowering Default would also admit
  // RequestLoggingMiddleware's request/response body slices and the caller PII they can carry —
  // /architecture/request-logging documents that as load-bearing in both directions. Prod stays at
  // Warning regardless.
  Logging__LogLevel__Cleansia: env == 'prod' ? 'Warning' : 'Information'
}, sendGridSettings)

// FCM push dispatch — the Functions queue consumer is the ONLY dispatcher; FcmPushDispatcher is a
// deliberate no-op while this is unset, so pushes are silently ACKed until the secret exists. Value:
// the Firebase service-account JSON (raw or base64 — the dispatcher auto-detects), owner-populated in
// Key Vault (Firebase Console → Project settings → Service accounts → Generate new private key; note
// the GCP org policy iam.disableServiceAccountKeyCreation must allow key creation for that SA).
// Param-gated DEFAULT-OFF because ORDER MATTERS: an unresolvable KV reference hands the app the
// literal reference string, which fails init as TRANSIENT and dead-letters every push instead of the
// clean disabled no-op. Flip fcmSecretProvisioned in the .bicepparam only AFTER the Key Vault secret
// Fcm--ServiceAccountJson exists.
var fcmSettings = fcmSecretProvisioned
  ? {
      FCM__ServiceAccountJson: kvRef(keyVaultUri, 'Fcm--ServiceAccountJson')
    }
  : {}

// Direct-APNs live-activity channel (ADR-0029 D1) — the Functions dispatch consumer is the ONLY
// sender; ApnsLiveActivityClient is a deliberate Skipped-ack no-op while APNS:Enabled is false. Same
// param-gated DEFAULT-OFF shape as fcmSettings (empty object → union no-op → dev byte-unchanged, and
// the app binds APNS:Enabled=false by C# default). Flipping apnsSecretProvisioned binds the three
// secrets as KV references + the literal bundle id / sandbox switch AND enables the channel — flip it
// only after the secrets exist and the iOS widget (LA-5) ships. UseSandbox tracks env (dev builds get
// sandbox activity tokens; prod is production APNs). The owner seeds Apns--KeyId/TeamId/PrivateKeyPem
// from the SAME .p8 already in backend custody for ordinary push — no new key, no new custody surface.
var apnsSettings = apnsSecretProvisioned
  ? {
      APNS__Enabled: 'true'
      APNS__KeyId: kvRef(keyVaultUri, 'Apns--KeyId')
      APNS__TeamId: kvRef(keyVaultUri, 'Apns--TeamId')
      APNS__PrivateKeyPem: kvRef(keyVaultUri, 'Apns--PrivateKeyPem')
      APNS__CustomerBundleId: 'cz.cleansia.customer'
      APNS__UseSandbox: env == 'prod' ? 'false' : 'true'
    }
  : {}

// Shared Stripe settings. Stripe__WebhookSecret is a DEFAULT: each host overrides it with its own
// endpoint's signing secret at the call site (see host.webhookSecretName), because a Stripe webhook
// secret is per-ENDPOINT and web/mobile now have separate endpoints.
var stripeSettings = {
  Stripe__SecretKey: kvRef(keyVaultUri, 'Stripe--SecretKey')
  Stripe__WebhookSecret: kvRef(keyVaultUri, 'Stripe--WebhookSecret')
  // The app also binds Stripe:PublishableKey — publishable keys are client-safe (not a true secret),
  // but we route it through Key Vault + CI push for uniformity. And the checkout redirect bases, which
  // default to localhost in appsettings — point them at the dev customer SSR host.
  Stripe__PublishableKey: kvRef(keyVaultUri, 'Stripe--PublishableKey')
  Stripe__SuccessUrlBase: '${customerWebBaseUrl}/checkout/success'
  Stripe__CancelUrlBase: '${customerWebBaseUrl}/checkout/cancel'
}

// Fiscal (Czech EET) config the app binds. Disabled in dev (Fiscal:CzechEet2:Enabled=false), so these
// are empty placeholders wired for completeness — the section binds without gaps. The two SECRET
// fields (ApiKey, CertificatePassword) are param-gated exactly like fcmSettings: while
// fiscalSecretProvisioned is false they stay the literal empty strings (unchanged dev behavior);
// once the Key Vault secrets exist and the flag flips, they become KV references — so enabling
// fiscal can never route a live secret through a Bicep literal. The gate matters because an
// unresolvable KV reference hands the app the raw reference string, not an empty value.
var fiscalSettings = union(
  {
    Fiscal__CzechEet2__Enabled: 'false'
    Fiscal__CzechEet2__ApiUrl: ''
    Fiscal__CzechEet2__CertificatePath: ''
    Fiscal__CzechEet2__TaxpayerIdentifier: ''
    Fiscal__CzechEet2__BusinessPremiseId: ''
    Fiscal__CzechEet2__CashRegisterId: ''
  },
  fiscalSecretProvisioned
    ? {
        Fiscal__CzechEet2__ApiKey: kvRef(keyVaultUri, 'Fiscal--CzechEet2--ApiKey')
        Fiscal__CzechEet2__CertificatePassword: kvRef(keyVaultUri, 'Fiscal--CzechEet2--CertificatePassword')
      }
    : {
        Fiscal__CzechEet2__ApiKey: ''
        Fiscal__CzechEet2__CertificatePassword: ''
      }
)

// ---------------------------------------------------------------------------------------------------
// The five API App Services — the reusable appService module, once per host (ADR-0015 D1/D2).
// ---------------------------------------------------------------------------------------------------

module apiAppServices 'modules/appService.bicep' = [
  for (host, i) in apiHosts: {
    name: 'api-${host.audience}'
    params: {
      name: apiSiteNames[i]
      location: location
      appServicePlanId: appServicePlan.outputs.id
      linuxFxVersion: apiLinuxFxVersion
      // union() with {} is a no-op, so each host receives exactly the blocks its flags select: Stripe
      // for the payment initiators, the app-level CORS override for browser hosts (empty until
      // customDomains adds frontend origins — see corsOriginsAppSettings).
      appSettings: union(apiBaseSettings, fiscalSettings, host.needsStripe ? union(stripeSettings, { Stripe__WebhookSecret: kvRef(keyVaultUri, host.webhookSecretName) }) : {}, host.browserFacing ? corsOriginsAppSettings : {}, host.audience == 'customer' ? customerWebAuthSettings : {})
      corsAllowedOrigins: host.browserFacing ? browserCorsOrigins : []
      httpsOnly: true
      // Always On in EVERY stage. It costs nothing on a plan that is already paid for by the hour,
      // and without it App Service unloads an idle dev host after ~20 minutes — so the first request
      // of a demo pays a full cold start (white screen) rather than the deploy pipeline paying it.
      alwaysOn: true
      stagingSlotEnabled: deploymentSlotsEnabled
      virtualNetworkSubnetId: privateNetworkingEnabled ? privateNetworking!.outputs.appSubnetId : ''
      tags: commonTags
    }
  }
]

// ---------------------------------------------------------------------------------------------------
// Custom domains (the same-site enabler for deployed web cookie auth) — hostname binding + managed
// certificate + SNI flip per configured customDomains entry, layered onto the hosts above.
// Default-off: an absent key deploys NOTHING for that host. DNS (CNAME/A + asuid TXT) must exist
// before an entry is set — the records + owner sequence live in deploy/AZURE-DEV-RUNBOOK.md §12. The
// SWA domains are NOT here — each staticWebApp module binds its own (SWA validates via the CNAME and
// manages its own TLS).
// ---------------------------------------------------------------------------------------------------

module apiCustomDomains 'modules/appServiceCustomDomain.bicep' = [
  for (host, i) in apiHosts: if (contains(customDomains, 'api-${host.audience}')) {
    name: 'domain-api-${host.audience}'
    params: {
      siteName: apiSiteNames[i]
      hostname: contains(customDomains, 'api-${host.audience}') ? customDomains['api-${host.audience}'] : ''
      appServicePlanId: appServicePlan.outputs.id
      location: location
      tags: commonTags
    }
    dependsOn: [
      apiAppServices[i]
    ]
  }
]

module ssrCustomDomain 'modules/appServiceCustomDomain.bicep' = if (contains(customDomains, 'ssr')) {
  name: 'domain-ssr'
  params: {
    siteName: ssrSiteName
    hostname: ssrCustomHost
    appServicePlanId: appServicePlan.outputs.id
    location: location
    tags: commonTags
  }
  dependsOn: [
    ssr
  ]
}

// www (prod: www.cleansia.cz) binds the SAME site as `ssr`, so it chains after it — App Service 409s
// concurrent host-level writes to one site. The dependsOn on a condition-false module is ignored by
// ARM, so ssr-www also deploys standalone.
module ssrWwwCustomDomain 'modules/appServiceCustomDomain.bicep' = if (contains(customDomains, 'ssr-www')) {
  name: 'domain-ssr-www'
  params: {
    siteName: ssrSiteName
    hostname: contains(customDomains, 'ssr-www') ? customDomains['ssr-www'] : ''
    appServicePlanId: appServicePlan.outputs.id
    location: location
    tags: commonTags
  }
  dependsOn: [
    ssr
    ssrCustomDomain
  ]
}

// ---------------------------------------------------------------------------------------------------
// Compute plan + the customer SSR App Service (Node) — the sixth appService instantiation.
// ---------------------------------------------------------------------------------------------------

module appServicePlan 'modules/appServicePlan.bicep' = {
  name: 'appServicePlan'
  params: {
    location: location
    region: region
    stage: env
    skuName: appServicePlanSku
    autoscaleEnabled: autoscaleEnabled
    autoscaleMinInstances: autoscaleMinInstances
    autoscaleMaxInstances: autoscaleMaxInstances
    tags: commonTags
  }
}

module ssr 'modules/appService.bicep' = {
  name: 'ssr-customer'
  params: {
    name: ssrSiteName
    location: location
    appServicePlanId: appServicePlan.outputs.id
    linuxFxVersion: ssrLinuxFxVersion
    appSettings: {
      // DEAD CONFIG on this host, deliberately kept — unlike the five .NET APIs, nothing here reads it.
      // The SSR app is Node and ships no telemetry client, and the App Service Node auto-instrumentation
      // agent is off (it needs ApplicationInsightsAgent_EXTENSION_VERSION, which no host sets). So this
      // site is the one place the setting still means nothing, and it costs no ingestion for the same
      // reason. Turning it on is an owner decision, not a template one: either add the
      // `applicationinsights` npm package to apps/cleansia.app/server.ts or set the agent app setting
      // here (T-0500).
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
      // server.ts fronts Mapbox forward-geocoding at /api/mapbox/geocode so the token never reaches
      // the browser (the Mapbox REST API authenticates ONLY via an `access_token` query parameter,
      // which would otherwise leak into history/referrer/CDN+APM logs). The proxy reads
      // process.env.MAPBOX_TOKEN and, when it is empty, answers 503 {"features":[]} — which is what
      // every environment did, because this site previously received exactly ONE app setting.
      MAPBOX_TOKEN: mapboxTokenKvRef
    }
    corsAllowedOrigins: []
    httpsOnly: true
    // Always On in every stage — same reasoning as the API hosts above, and it matters most here:
    // this is the browser-facing site, so an unloaded dev SSR host is the white screen a visitor sees.
    alwaysOn: true
    stagingSlotEnabled: deploymentSlotsEnabled
    virtualNetworkSubnetId: privateNetworkingEnabled ? privateNetworking!.outputs.appSubnetId : ''
    // The SSR host DOES expose /health: apps/cleansia.app/server.ts registers it on the Express app
    // ahead of the static handler and the Angular engine catch-all, so it answers 200 JSON without
    // ever entering a render. (It was previously disabled on the false premise that the Node host had
    // no such route, which left the one browser-facing site as the only host Azure never probed.)
    //
    // Kept at /health, and explicitly, because this host has no /alive — that endpoint comes from the
    // .NET MapDefaultEndpoints the API hosts share. The distinction the API hosts now draw (Azure
    // recycles on LIVENESS, never on a slow shared dependency) does not apply here: server.ts's /health
    // touches no database and no storage, so it is already a liveness probe by construction.
    healthCheckPath: '/health'
    tags: commonTags
  }
}

// ---------------------------------------------------------------------------------------------------
// The two SPAs — partner + admin Static Web Apps.
// ---------------------------------------------------------------------------------------------------

var spaAudiences = [
  'partner'
  'admin'
]

module staticWebApps 'modules/staticWebApp.bicep' = [
  for audience in spaAudiences: {
    name: 'swa-${audience}'
    params: {
      name: 'swa-cleansia-${audience}-${region}-${env}'
      location: staticWebAppLocation
      skuName: staticWebAppSku
      // Same-site enabler — '' (key absent, the default) binds no custom domain.
      customDomain: contains(customDomains, 'swa-${audience}') ? customDomains['swa-${audience}'] : ''
      tags: commonTags
    }
  }
]

// ---------------------------------------------------------------------------------------------------
// Functions — container pulled from ACR (QuestPDF native deps; ADR-0015 D2). The module owns only the
// runtime/storage/db wiring; ALL application config (SendGrid, Sentry, fiscal) is passed in via
// extraAppSettings from the SAME shared objects the API hosts use. The Functions container ships an
// appsettings.json with only cron schedules — anything not set here simply does not exist at runtime
// (this is exactly how the SendEmail templateId ArgumentNullException happened).
// ---------------------------------------------------------------------------------------------------

module functionApp 'modules/functionApp.bicep' = {
  name: 'functionApp'
  params: {
    location: location
    region: region
    env: env
    appServicePlanId: appServicePlan.outputs.id
    acrLoginServer: acr.outputs.loginServer
    keyVaultUri: keyVaultUri
    storageAccountName: storage.outputs.storageAccountName
    storageAccountId: storage.outputs.storageAccountId
    appInsightsConnectionString: appInsights.outputs.connectionString
    virtualNetworkSubnetId: privateNetworkingEnabled ? privateNetworking!.outputs.appSubnetId : ''
    // Sentry__Dsn IS read (ac2243d2): Program.cs calls ConfigureLogging(AddSentryMonitoring), so an
    // ILogger.LogError in this worker becomes a Sentry event — which is what PoisonHandlerBase step 2
    // and FunctionInvocationErrorMiddleware depend on. It is a SECOND, unsampled path alongside the
    // App Insights export above, not a substitute for it. A blank DSN disables Sentry silently rather
    // than throwing, so an unpopulated secret costs the per-error detail and the first-occurrence mail
    // while leaving only threshold alerts. Populate it.
    extraAppSettings: union(sendGridSettings, fiscalSettings, fcmSettings, apnsSettings, cronSettings, {
      Sentry__Dsn: kvRef(keyVaultUri, 'Sentry--Dsn')
    })
    tags: commonTags
  }
}

// ---------------------------------------------------------------------------------------------------
// MI role grants — the app hosts (5 APIs + SSR) + Functions -> Key Vault Secrets User + Storage data
// roles; Functions -> AcrPull; CI -> Key Vault Secrets Officer (ADR-0015 D4). Runs last: it consumes
// every host's managed-identity principal id.
// ---------------------------------------------------------------------------------------------------

// Module OUTPUTS (.outputs.principalId) are only known AFTER deployment, so they can NOT be collected
// into a `var` (BCP182: var for-bodies must compute at deployment start). A for-expression IS allowed as
// a DIRECT module property value, so the 5 API principal ids are passed inline; the SSR + Functions ids
// go as their own params (the module concats them) — no for-expression nested inside a function call.
module roleAssignments 'modules/roleAssignments.bicep' = {
  name: 'roleAssignments'
  params: {
    keyVaultId: keyVault.outputs.keyVaultId
    storageAccountId: storage.outputs.storageAccountId
    acrId: acr.outputs.registryId
    appPrincipalIds: [for i in range(0, length(apiHosts)): apiAppServices[i].outputs.principalId]
    ssrPrincipalId: ssr.outputs.principalId
    functionsPrincipalId: functionApp.outputs.principalId
    // Staging-slot MIs (empty strings when slots are off, filtered in the module) — a slot must hold
    // the same Key Vault/Storage grants BEFORE its first swap or it would swap in unable to resolve
    // its config.
    apiSlotPrincipalIds: [for i in range(0, length(apiHosts)): apiAppServices[i].outputs.stagingSlotPrincipalId]
    ssrSlotPrincipalId: ssr.outputs.stagingSlotPrincipalId
    ciPrincipalId: ciPrincipalId
  }
}

// Bicep-DERIVABLE Key Vault secrets — values computed from resources this deployment creates (the
// storage key, the Postgres FQDN + password param), so the owner no longer hand-populates them. The
// EXTERNAL secrets (Jwt--Key, Stripe--*, SendGrid, Sentry, Mapbox) are NOT here — a CI step pushes
// those from the dev-weu GitHub-Environment secrets (ADR-0015 D4). JWT issuer/audience are
// deliberately NOT written either — see the rationale in modules/derivedSecrets.bicep.
module derivedSecrets 'modules/derivedSecrets.bicep' = {
  name: 'derivedSecrets'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    storageAccountName: storage.outputs.storageAccountName
    postgresFqdn: postgres.outputs.fullyQualifiedDomainName
    postgresAdministratorLogin: postgresAdministratorLogin
    postgresAdministratorPassword: postgresAdministratorPassword
  }
}

// ---------------------------------------------------------------------------------------------------
// Alerting (ADR-0015 D2/D3) — Action Group + metric alerts over the six web hosts, the Functions
// host's health probe, the App Insights component, and Postgres. Deployed only when an alert email is
// supplied (dev today; prod wires its own address when it goes live). Scopes are passed as deploy-time
// NAMES — the module rebuilds the resource ids itself, because module outputs cannot feed its per-site
// for-loop (BCP182) — hence the explicit dependsOn so the alerts never race the resources they watch.
// The postgres/appInsights names MIRROR the owning modules' naming (postgres.bicep / appInsights.bicep).
// ---------------------------------------------------------------------------------------------------

module alerts 'modules/alerts.bicep' = if (!empty(alertEmail)) {
  name: 'alerts'
  params: {
    env: env
    region: region
    alertEmail: alertEmail
    siteNames: concat(apiSiteNames, [ssrSiteName])
    functionsSiteName: 'func-cleansia-${region}-${env}'
    postgresServerName: 'pg-cleansia-${region}-${env}'
    appInsightsName: 'appi-cleansia-${region}-${env}'
    tags: commonTags
  }
  dependsOn: [
    apiAppServices
    ssr
    postgres
    appInsights
    functionApp
  ]
}

// Poison-queue alerting (T-0360) — the deferred half of alerts.bicep: queue diagnostic settings into
// the workspace + the scheduled-query rule over them, attached to the exported Action Group. Same
// gate as alerts (no alert email = no alerting at all). It is the reason the workspace is not
// negotiable independently of the component: a *-poison PutMessage has no metric form to alert on
// (QueueMessageCount is account-wide, undimensioned and hourly), so without this the only trace of a
// message the runtime gave up on is the durable DeadLetter row — a pull, not a page.
module queueAlerts 'modules/queueAlerts.bicep' = if (!empty(alertEmail)) {
  name: 'queueAlerts'
  params: {
    env: env
    region: region
    location: location
    storageAccountName: storage.outputs.storageAccountName
    logAnalyticsWorkspaceId: appInsights.outputs.logAnalyticsId
    actionGroupId: alerts!.outputs.actionGroupId
    tags: commonTags
  }
}

// ---------------------------------------------------------------------------------------------------
// Outputs — the stable default hostnames the iOS base URLs + the dev smoke consume (ADR-0015 D6).
// ---------------------------------------------------------------------------------------------------

// A for-expression is only valid as the DIRECT value of an output (BCP138 fires if it's wrapped in a
// function like toObject(...)), so emit the {audience, host} pairs as an ARRAY rather than a keyed map —
// the consumer (smoke / iOS config) reads it by the audience field.
@description('The five API hosts as {audience, host} pairs (audience = partner|admin|customer|partner-mobile|customer-mobile).')
output apiHostList array = [
  for i in range(0, length(apiHosts)): {
    audience: apiHosts[i].audience
    host: apiAppServices[i].outputs.defaultHostName
  }
]

@description('iOS partner app base host (Cleansia.Web.Mobile.Partner) — ADR-0015 D6.')
output partnerMobileApiHostName string = apiAppServices[3].outputs.defaultHostName

@description('iOS customer app base host (Cleansia.Web.Mobile.Customer — the host the old YAML omitted) — ADR-0015 D6.')
output customerMobileApiHostName string = apiAppServices[4].outputs.defaultHostName

@description('Customer SSR App Service default hostname.')
output ssrHostName string = ssr.outputs.defaultHostName

@description('Partner SPA Static Web App default hostname.')
output partnerSpaHostName string = staticWebApps[0].outputs.defaultHostName

@description('Admin SPA Static Web App default hostname.')
output adminSpaHostName string = staticWebApps[1].outputs.defaultHostName

@description('Functions host default hostname.')
output functionAppHostName string = functionApp.outputs.defaultHostName

@description('Key Vault name — the owner populates the secret values into this vault post-deploy.')
output keyVaultName string = keyVault.outputs.keyVaultName

@description('ACR name + login server — CI az acr build pushes the Functions image here.')
output acrLoginServer string = acr.outputs.loginServer

@description('PostgreSQL fully-qualified domain name — the host for the connection string.')
output postgresFqdn string = postgres.outputs.fullyQualifiedDomainName
