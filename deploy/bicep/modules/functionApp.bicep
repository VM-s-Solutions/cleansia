@description('Deployment region token threaded into every name (ADR-0017 region seam; default West Europe).')
param region string = 'weu'

@description('Deployment stage suffix (dev | prod).')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Azure location to create the Function App in.')
param location string

@description('Resource id of the App Service Plan (Linux) the Function App runs on.')
param appServicePlanId string

@description('ACR login server, e.g. acrcleansiaweudev.azurecr.io.')
param acrLoginServer string

@description('Container image repository name in ACR (without the registry prefix).')
param imageRepository string = 'cleansia-functions'

@description('Container image tag. CI overrides per deploy with the commit sha; latest is the placeholder default.')
param imageTag string = 'latest'

@description('Key Vault URI used to build @Microsoft.KeyVault(SecretUri=...) references. e.g. https://kv-cleansia-weu-dev.vault.azure.net')
param keyVaultUri string

@description('Storage account name for the Functions runtime store (AzureWebJobsStorage via managed identity).')
param storageAccountName string

@description('Resource id of the storage account (for the AzureWebJobsStorage MI binding).')
param storageAccountId string

@description('Application Insights connection string (non-secret; instrumentation only).')
param appInsightsConnectionString string

@description('Subnet id for regional VNet integration (the Q-INFRA-03 seam). Empty (default) = no VNet integration. MUST be set whenever Postgres/Storage go private — an unintegrated Functions host would lose the DB and every queue at once.')
param virtualNetworkSubnetId string = ''

@description('Resource tags.')
param tags object = {}

@description('''
Extra app settings composed by the orchestrator (main.bicep) — the SHARED application config the
Functions app must receive (SendGrid email config, Sentry, fiscal placeholders). CRITICAL: unlike the
API hosts, the Functions container ships an appsettings.json with ONLY cron schedules — every piece of
application config (template ids, from-address, link base URLs, …) must arrive via these app settings
or the email/receipt handlers throw at runtime (the SendEmail templateId ArgumentNullException). One
shared definition in main.bicep feeds both the API hosts and this app so the two can never drift.
''')
param extraAppSettings object = {}

var functionAppName = 'func-cleansia-${region}-${env}'

// QuestPDF needs native libfontconfig1/libfreetype6 baked into the image — the Functions host is a
// CONTAINER pulled from ACR, never a code/zip deploy (ADR-0015 D2). DOCKER|<server>/<repo>:<tag>.
var linuxFxVersion = 'DOCKER|${acrLoginServer}/${imageRepository}:${imageTag}'

// Key Vault references — secret NAMES only; values are owner/CI-populated in Key Vault (ADR-0015 D4).
var dbConnSecretUri = '${keyVaultUri}/secrets/ConnectionStrings--cleansia-db'
var storageSecretUri = '${keyVaultUri}/secrets/Storage--ConnectionString'

// The orchestrator-supplied object → the {name, value} array shape App Service wants.
var extraAppSettingsArray = [
  for setting in items(extraAppSettings): {
    name: setting.key
    value: setting.value
  }
]

// Functions-runtime + storage/db wiring owned by this module; application config (SendGrid/Sentry/
// fiscal) arrives via extraAppSettings so it has ONE home in main.bicep shared with the API hosts.
var baseAppSettings = [
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  {
    name: 'DOCKER_REGISTRY_SERVER_URL'
    value: 'https://${acrLoginServer}'
  }
  {
    name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
    value: 'false'
  }
  // Functions runtime store — managed-identity binding to the Storage Account
  // (no AzureWebJobsStorage connection string; the __accountName/__credential form).
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccountName
  }
  {
    name: 'AzureWebJobsStorage__credential'
    value: 'managedidentity'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
  // App config via Key Vault references (double-underscore -> colon mapping; no app code change).
  {
    name: 'ConnectionStrings__ConnectionString'
    value: '@Microsoft.KeyVault(SecretUri=${dbConnSecretUri})'
  }
  {
    name: 'ConnectionStrings__BlobContainerConfigurationConnectionString'
    value: '@Microsoft.KeyVault(SecretUri=${storageSecretUri})'
  }
  {
    name: 'ConnectionStrings__QueueStorageConnectionString'
    value: '@Microsoft.KeyVault(SecretUri=${storageSecretUri})'
  }
]

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: tags
  kind: 'functionapp,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: true
    reserved: true
    virtualNetworkSubnetId: empty(virtualNetworkSubnetId) ? null : virtualNetworkSubnetId
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      vnetRouteAllEnabled: empty(virtualNetworkSubnetId) ? null : true
      // MUST be true on a Dedicated (App Service) plan: with alwaysOn=false the Functions host unloads
      // when idle and stops polling the Storage Queues (send-email, generate-receipt, generate-invoice,
      // notifications) and firing timers until something wakes it — messages sit invisible past their
      // visibility timeout and come back with DequeueCount > 1. Only Consumption plans get an external
      // scale controller; on this shared B2 the warm host IS the queue scaler.
      alwaysOn: true
      // The GET /api/health probe (HealthFunction). App Service pings it and RECYCLES an instance that
      // returns non-200 — self-healing for the class of failure behind the 2026-07-18 outage (host up but
      // a dependency down), and it exposes the HealthCheckStatus metric the alerts module watches. Points
      // at the app's only HTTP route; timers/queues are unaffected.
      healthCheckPath: '/api/health'
      // Pull the image from ACR using the Function App's managed identity (AcrPull granted in
      // roleAssignments.bicep). No registry admin user, no registry password in config.
      acrUseManagedIdentityCreds: true
      appSettings: concat(baseAppSettings, extraAppSettingsArray)
    }
  }
}

@description('Function App resource id.')
output functionAppId string = functionApp.id

@description('Function App default host name.')
output defaultHostName string = functionApp.properties.defaultHostName

@description('System-assigned managed identity principal id (for AcrPull / KV Secrets User / Storage role grants).')
output principalId string = functionApp.identity.principalId

@description('Storage account id this app binds AzureWebJobsStorage to (passthrough for the Storage role grant).')
output storageAccountId string = storageAccountId
