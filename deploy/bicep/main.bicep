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
    tags: commonTags
  }
}

module acr 'modules/acr.bicep' = {
  name: 'acr'
  params: {
    location: location
    region: region
    env: env
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
    administratorLogin: postgresAdministratorLogin
    administratorPassword: postgresAdministratorPassword
    adminIpAddress: adminIpAddress
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

// Browser host (SPA/SSR) CORS origins — the dev SWA + SSR default hostnames, not localhost (D3).
var browserCorsOrigins = [
  'https://${staticWebApps[0].outputs.defaultHostName}'
  'https://${staticWebApps[1].outputs.defaultHostName}'
  'https://${ssr.outputs.defaultHostName}'
]

// App settings shared by every API host: DB + Storage + JWT + SendGrid + Sentry + Mapbox Key Vault
// references, plus the (non-secret) App Insights connection string. The `__` -> `:` App Service
// mapping means the app reads its existing config keys with no code change (ADR-0015 D4).
// The customer SSR host — the base for customer-facing links in emails/Stripe redirects (reset
// password, order status, checkout success/cancel). Dev: the SSR default hostname.
var customerWebBaseUrl = 'https://${ssr.outputs.defaultHostName}'

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
  Mapbox__GeocodingAccessToken: kvRef(keyVaultUri, 'Mapbox--GeocodingAccessToken')
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
  // ADR-0003 D3: the app refuses to boot in non-Development unless it's told which proxy network to
  // trust for X-Forwarded-For. Behind App Service the only hop is the App Service front end, which
  // forwards from its internal range — 100.64.0.0/10 (carrier-grade NAT, what App Service uses). The /10
  // satisfies the "no /0–/8 supernet" guard: it trusts the App Service front end, not the public net.
  ForwardedHeaders__KnownNetworks: '100.64.0.0/10'
  ForwardedHeaders__ForwardLimit: '1'
}, sendGridSettings)

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
// are empty placeholders wired for completeness — the section binds without gaps and enabling fiscal
// later is a matter of supplying the values (the sensitive ones via KV/CI at that point). No live
// fiscal secret is committed.
var fiscalSettings = {
  Fiscal__CzechEet2__Enabled: 'false'
  Fiscal__CzechEet2__ApiUrl: ''
  Fiscal__CzechEet2__ApiKey: ''
  Fiscal__CzechEet2__CertificatePath: ''
  Fiscal__CzechEet2__CertificatePassword: ''
  Fiscal__CzechEet2__TaxpayerIdentifier: ''
  Fiscal__CzechEet2__BusinessPremiseId: ''
  Fiscal__CzechEet2__CashRegisterId: ''
}

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
      appSettings: host.needsStripe ? union(apiBaseSettings, fiscalSettings, stripeSettings) : union(apiBaseSettings, fiscalSettings)
      corsAllowedOrigins: host.browserFacing ? browserCorsOrigins : []
      httpsOnly: true
      alwaysOn: false
      tags: commonTags
    }
  }
]

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
      APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
    }
    corsAllowedOrigins: []
    httpsOnly: true
    alwaysOn: false
    // The Angular SSR (Node) host has no /health endpoint — disable the probe so Azure doesn't recycle it.
    healthCheckPath: ''
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
    extraAppSettings: union(sendGridSettings, fiscalSettings, {
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
    ciPrincipalId: ciPrincipalId
  }
}

// Bicep-DERIVABLE Key Vault secrets — values computed from resources this deployment creates (the
// storage key, the Postgres FQDN + password param, deterministic JWT issuer/audience), so the owner
// no longer hand-populates them. The 6 EXTERNAL secrets (Jwt--Key, Stripe--*, SendGrid, Sentry, Mapbox)
// are NOT here — a CI step pushes those from the dev-weu GitHub-Environment secrets (ADR-0015 D4).
module derivedSecrets 'modules/derivedSecrets.bicep' = {
  name: 'derivedSecrets'
  params: {
    keyVaultName: keyVault.outputs.keyVaultName
    storageAccountName: storage.outputs.storageAccountName
    postgresFqdn: postgres.outputs.fullyQualifiedDomainName
    postgresAdministratorLogin: postgresAdministratorLogin
    postgresAdministratorPassword: postgresAdministratorPassword
    jwtIssuer: 'https://${apiAppServices[0].outputs.defaultHostName}'
  }
}

// ---------------------------------------------------------------------------------------------------
// Alerting (ADR-0015 D2/D3) — Action Group + metric alerts over the six web hosts, the App Insights
// component, and Postgres. Deployed only when an alert email is supplied (dev today; prod wires its
// own address when it goes live). Scopes are passed as deploy-time NAMES — the module rebuilds the
// resource ids itself, because module outputs cannot feed its per-site for-loop (BCP182) — hence the
// explicit dependsOn so the alerts never race the resources they watch. The postgres/appInsights
// names MIRROR the owning modules' naming (postgres.bicep / appInsights.bicep).
// ---------------------------------------------------------------------------------------------------

module alerts 'modules/alerts.bicep' = if (!empty(alertEmail)) {
  name: 'alerts'
  params: {
    env: env
    region: region
    alertEmail: alertEmail
    siteNames: concat(apiSiteNames, [ssrSiteName])
    postgresServerName: 'pg-cleansia-${region}-${env}'
    appInsightsName: 'appi-cleansia-${region}-${env}'
    tags: commonTags
  }
  dependsOn: [
    apiAppServices
    ssr
    postgres
    appInsights
  ]
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
