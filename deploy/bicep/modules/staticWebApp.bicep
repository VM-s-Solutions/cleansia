// Static Web App module (ADR-0015 D2). Instantiated twice by main.bicep: the partner SPA and the
// admin SPA. Free tier for dev; Standard for prod (env-switched in the param file). Carries the
// region token in its name (ADR-0017 D4).
//
// The SWA deployment token is a CI/provisioning secret that lives in a GitHub Environment secret
// (AZURE_STATIC_WEB_APPS_API_TOKEN_*, ADR-0015 D4 tier 3) and is read by the deploy workflow — it is
// NEVER emitted by this module. The SPAs are HTTPS by default on Static Web Apps and reach their API
// hosts via the per-host CORS configured on each App Service.

@description('Full Static Web App name, already region/stage-tokenized by the orchestrator (e.g. swa-cleansia-partner-weu-dev).')
param name string

@description('Azure location for the Static Web App (a supported SWA region; resolved by the orchestrator).')
param location string

@description('Static Web App SKU. Dev = Free; prod = Standard.')
@allowed([
  'Free'
  'Standard'
])
param skuName string = 'Free'

@description('Resource tags applied to the Static Web App.')
param tags object = {}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    // App content is pushed from CI via the deploy token (no repo build wiring here — ADR-0015 D5).
    allowConfigFileUpdates: true
  }
}

@description('Resource id of the Static Web App.')
output id string = staticWebApp.id

@description('Static Web App name.')
output name string = staticWebApp.name

@description('Default hostname (the stable *.azurestaticapps.net origin used in each API host CORS list).')
output defaultHostName string = staticWebApp.properties.defaultHostname
