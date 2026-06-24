// Application Insights (workspace-backed) + its Log Analytics workspace — the platform/infra
// telemetry + alerting layer across all five APIs + the SSR + Functions (ADR-0015 D2/D3). The
// connection string output is wired into each host as APPLICATIONINSIGHTS_CONNECTION_STRING by
// main.bicep. ADR-0017: the `region` token is in both names from day one.

@description('Azure region the resources are deployed to.')
param location string

@description('Expansion-seam region token threaded into every resource name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Deployment stage suffix (dev | prod). Drives names and retention.')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Tags applied to every resource.')
param tags object = {}

var workspaceName = 'log-cleansia-${region}-${env}'
var appInsightsName = 'appi-cleansia-${region}-${env}'
var retentionInDays = env == 'prod' ? 90 : 30

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    workspaceCapping: env == 'prod' ? {} : {
      dailyQuotaGb: 1
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: false
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

@description('Application Insights connection string — wired into each host as APPLICATIONINSIGHTS_CONNECTION_STRING by main.bicep.')
output connectionString string = appInsights.properties.ConnectionString

@description('Application Insights instrumentation key (legacy SDK fallback).')
output instrumentationKey string = appInsights.properties.InstrumentationKey

output appInsightsId string = appInsights.id
output appInsightsName string = appInsights.name
output logAnalyticsId string = logAnalytics.id
output logAnalyticsName string = logAnalytics.name
