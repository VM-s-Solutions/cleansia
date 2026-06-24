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

@description('Resource tags.')
param tags object = {}

var functionAppName = 'func-cleansia-${region}-${env}'

// QuestPDF needs native libfontconfig1/libfreetype6 baked into the image — the Functions host is a
// CONTAINER pulled from ACR, never a code/zip deploy (ADR-0015 D2). DOCKER|<server>/<repo>:<tag>.
var linuxFxVersion = 'DOCKER|${acrLoginServer}/${imageRepository}:${imageTag}'

// Key Vault references — secret NAMES only; values are owner/CI-populated in Key Vault (ADR-0015 D4).
var dbConnSecretUri = '${keyVaultUri}/secrets/ConnectionStrings--cleansia-db'
var sendGridSecretUri = '${keyVaultUri}/secrets/SendGrid--ApiKey'
var sentrySecretUri = '${keyVaultUri}/secrets/Sentry--Dsn'
var storageSecretUri = '${keyVaultUri}/secrets/Storage--ConnectionString'

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
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      alwaysOn: false
      // Pull the image from ACR using the Function App's managed identity (AcrPull granted in
      // roleAssignments.bicep). No registry admin user, no registry password in config.
      acrUseManagedIdentityCreds: true
      appSettings: [
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
        {
          name: 'SendGrid__ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${sendGridSecretUri})'
        }
        {
          name: 'Sentry__Dsn'
          value: '@Microsoft.KeyVault(SecretUri=${sentrySecretUri})'
        }
      ]
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
