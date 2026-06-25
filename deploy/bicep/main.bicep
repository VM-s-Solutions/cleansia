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
var apiBaseSettings = {
  ConnectionStrings__ConnectionString: kvRef(keyVaultUri, 'ConnectionStrings--cleansia-db')
  ConnectionStrings__BlobContainerConfigurationConnectionString: kvRef(keyVaultUri, 'Storage--ConnectionString')
  ConnectionStrings__QueueStorageConnectionString: kvRef(keyVaultUri, 'Storage--ConnectionString')
  JwtSettings__Secret: kvRef(keyVaultUri, 'Jwt--Key')
  SendGrid__ApiKey: kvRef(keyVaultUri, 'SendGrid--ApiKey')
  Sentry__Dsn: kvRef(keyVaultUri, 'Sentry--Dsn')
  Mapbox__GeocodingAccessToken: kvRef(keyVaultUri, 'Mapbox--GeocodingAccessToken')
  APPLICATIONINSIGHTS_CONNECTION_STRING: appInsights.outputs.connectionString
}

var stripeSettings = {
  Stripe__SecretKey: kvRef(keyVaultUri, 'Stripe--SecretKey')
  Stripe__WebhookSecret: kvRef(keyVaultUri, 'Stripe--WebhookSecret')
}

// ---------------------------------------------------------------------------------------------------
// The five API App Services — the reusable appService module, once per host (ADR-0015 D1/D2).
// ---------------------------------------------------------------------------------------------------

module apiAppServices 'modules/appService.bicep' = [
  for host in apiHosts: {
    name: 'api-${host.audience}'
    params: {
      name: 'api-cleansia-${host.audience}-${region}-${env}'
      location: location
      appServicePlanId: appServicePlan.outputs.id
      linuxFxVersion: apiLinuxFxVersion
      appSettings: host.needsStripe ? union(apiBaseSettings, stripeSettings) : apiBaseSettings
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
    name: 'web-cleansia-customer-${region}-${env}'
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
// Functions — container pulled from ACR (QuestPDF native deps; ADR-0015 D2). The module composes its
// own Key Vault references internally; it receives the vault URI + ACR login server + storage + AI.
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
