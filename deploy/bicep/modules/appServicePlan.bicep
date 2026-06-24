// Linux App Service Plan that hosts the five API App Services + the customer SSR App Service.
// Reusable across environments: the SKU is a parameter (B2 for dev per ADR-0015 D2, S1 for prod).
// The region token (ADR-0017 D4) is carried in the name so a second region is a param value, not a
// rename of a live resource.

@description('Deployment region token threaded through every name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Deployment stage token (ADR-0015): dev | prod. Drives the name suffix.')
@allowed([
  'dev'
  'prod'
])
param stage string

@description('Azure location for the plan. Resolved from the region token by the orchestrator.')
param location string

@description('App Service Plan SKU. Dev = B2 (ADR-0015 D2 owner override of B1); prod = S1.')
param skuName string = 'B2'

@description('Resource tags applied to the plan.')
param tags object = {}

var planName = 'plan-cleansia-${region}-${stage}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: skuName
  }
  properties: {
    reserved: true
  }
}

@description('Resource id of the plan — consumed by every appService/SSR instantiation.')
output id string = appServicePlan.id

@description('Name of the plan.')
output name string = appServicePlan.name
