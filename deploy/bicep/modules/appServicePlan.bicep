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

@description('Enable the CPU-driven autoscale rule on the plan (T-0359 prod posture). Autoscale needs a Standard+ SKU (S1) — dev B2 keeps the default false and stays a fixed single instance. Scaling the plan scales every site on it (the 5 APIs + SSR + Functions).')
param autoscaleEnabled bool = false

@description('Autoscale instance floor (also the default count). 1 keeps prod cost-lean; raise to 2 for instance redundancy.')
param autoscaleMinInstances int = 1

@description('Autoscale instance ceiling. S1 allows up to 10.')
param autoscaleMaxInstances int = 3

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

// CPU-driven scale-out for the shared plan: +1 instance above 70% average CPU over 10 minutes,
// -1 below 30%, 10-minute cooldowns. Deliberately conservative (one signal, symmetric hysteresis)
// so the rule can never flap; the bounds are param-overridable per environment.
resource autoscale 'Microsoft.Insights/autoscalesettings@2022-10-01' = if (autoscaleEnabled) {
  name: 'autoscale-${planName}'
  location: location
  tags: tags
  properties: {
    enabled: true
    targetResourceUri: appServicePlan.id
    profiles: [
      {
        name: 'cpu-based'
        capacity: {
          minimum: string(autoscaleMinInstances)
          maximum: string(autoscaleMaxInstances)
          default: string(autoscaleMinInstances)
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 70
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT10M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT10M'
            }
          }
        ]
      }
    ]
  }
}

@description('Resource id of the plan — consumed by every appService/SSR instantiation.')
output id string = appServicePlan.id

@description('Name of the plan.')
output name string = appServicePlan.name
