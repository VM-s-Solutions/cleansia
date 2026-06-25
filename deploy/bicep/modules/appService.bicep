// The ONE reusable App Service module (ADR-0015 D1). Instantiated six times by main.bicep: the five
// API hosts (partner, admin, customer, partner-mobile, customer-mobile) + the customer SSR host.
// Parameterized by name/runtime/appSettings/CORS so each host keeps its own config — the per-audience
// seam (a change to one host's settings touches only that instance). Carries the region token in its
// name (ADR-0017 D4).
//
// Secrets discipline (ADR-0015 D4): this module NEVER embeds a secret value. `appSettings` arrives as
// a name->value map whose secret entries are Key Vault REFERENCE strings
// (@Microsoft.KeyVault(SecretUri=...)) composed by the orchestrator; the host's system-assigned
// managed identity (provisioned here) resolves them at runtime once granted Key Vault Secrets User.

@description('Full App Service name, already region/stage-tokenized by the orchestrator (e.g. api-cleansia-partner-weu-dev).')
param name string

@description('Azure location. Resolved from the region token by the orchestrator.')
param location string

@description('Resource id of the shared Linux App Service Plan.')
param appServicePlanId string

@description('Linux runtime stack for this host. APIs = DOTNETCORE|10.0; the customer SSR = NODE|20-lts.')
param linuxFxVersion string

@description('App settings as a name->value map. Secret values MUST be Key Vault reference strings; this module stores them verbatim and emits no literal secret.')
param appSettings object = {}

@description('CORS allowed origins for this host. Browser hosts get the dev SPA/SSR origins; mobile (body-token) hosts stay closed — pass an empty array.')
param corsAllowedOrigins array = []

@description('Enforce HTTPS-only (ADR-0015 D3). Always true for our hosts.')
param httpsOnly bool = true

@description('Always-On. True keeps the host warm; off (default) suits dev cost on B2.')
param alwaysOn bool = false

@description('Health-check path Azure pings to gauge instance health. The .NET hosts expose /health (all checks) + /alive (liveness only) via MapDefaultEndpoints. Empty string disables the probe (the SSR/Node host passes "").')
param healthCheckPath string = '/health'

@description('Resource tags applied to the host.')
param tags object = {}

var appSettingsArray = [
  for setting in items(appSettings): {
    name: setting.key
    value: setting.value
  }
]

resource appService 'Microsoft.Web/sites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlanId
    httpsOnly: httpsOnly
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      alwaysOn: alwaysOn
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: empty(healthCheckPath) ? null : healthCheckPath
      appSettings: appSettingsArray
      cors: {
        allowedOrigins: corsAllowedOrigins
        supportCredentials: !empty(corsAllowedOrigins)
      }
    }
  }
}

@description('Resource id of the host.')
output id string = appService.id

@description('Host name.')
output name string = appService.name

@description('Default hostname (the stable *.azurewebsites.net the iOS/web clients point at — ADR-0015 D6).')
output defaultHostName string = appService.properties.defaultHostName

@description('System-assigned managed identity principal id — consumed by roleAssignments to grant Key Vault Secrets User + Storage data roles.')
output principalId string = appService.identity.principalId
