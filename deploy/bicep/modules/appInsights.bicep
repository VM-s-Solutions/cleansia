// Application Insights (workspace-backed) + its Log Analytics workspace — the platform/infra
// telemetry + alerting layer across all five APIs + the SSR + Functions (ADR-0015 D2/D3). The
// connection string output is wired into each host as APPLICATIONINSIGHTS_CONNECTION_STRING by
// main.bicep. ADR-0017: the `region` token is in both names from day one.
//
// The workspace is also what makes a LOG alert possible at all: modules/queueAlerts.bicep ships the
// queue diagnostic logs here and runs the poison-queue scheduled query against them. Deleting this
// module deletes that alert, and Azure Storage publishes no per-queue metric to rebuild it from.
//
// COST: this pair was the largest single line on the Azure bill (~EUR 49/month, 27.29 GB/month, all
// Analytics-tier) until the knobs below were set. The bill is ingestion, not retention — see
// retentionInDays.

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

@description('''Ingestion sampling percentage on the component — the server-side sampler at the
Application Insights endpoint, which is what makes it a COST knob rather than an SDK setting (it can
be changed without redeploying an app).

Dev = 10. Dev measured 27.29 GB/month at 100 (no sampling at all), which is the whole of the ~EUR 49
line; 10 puts ingestion inside the 5 GB/month pay-as-you-go free grant with headroom, so the line is
~EUR 0 rather than merely smaller. What that costs is bounded and stated: sampling is deterministic on
operation id, so a retained request keeps its own dependencies/traces/exceptions and a kept trace is
still a whole trace, and log-based metrics restore true counts via sum(itemCount) — but they restore
them in steps of 1/sampling, so alerts.bicep's dev exceptions threshold of 25 effectively resolves in
steps of 10. That is acceptable in dev because the exceptions rule is severity 3 into an inbox and
first-occurrence error reporting is Sentry's job on all five APIs and, since ac2243d2, on the
Functions worker too — none of which is sampled.

Prod = 50, deliberately unchanged: prod is authored, not deployed, so it is no part of the measured
bill, and 50 is where the prod exceptions threshold of 10 still resolves in steps of 2. The prod job
App Insights does that Sentry cannot — retrieving the one successful-but-wrong request behind a
customer complaint — succeeds at exactly this rate, so it is a fidelity knob there, not a cost one.

The .NET SDK layers on top: telemetry a host has ALREADY sampled bypasses this (the endpoint honours
the incoming rate), while telemetry a host excludes from its own sampling arrives unsampled and is
therefore sampled HERE. Cleansia.Functions/host.json excludes Exception, so this value is the only
sampler that host's exceptions ever meet.''')
@minValue(1)
@maxValue(100)
param samplingPercentage int = env == 'prod' ? 50 : 10

@description('''Log Analytics daily ingestion cap, in MEGABYTES. 0 = uncapped. When the cap is hit,
ingestion STOPS until the next UTC day — every alert over this workspace goes blind — so it is a
runaway-cost breaker, not a budget.

The unit is MB because Bicep has no float type and the useful dev value is fractional.

Dev = 500. The previous 1 GB cap is why the drift was never found: dev ran at ~0.88 GB/day against
it, so the breaker was never within 12% of tripping and twelve months of growth raised no signal.
At the sampling above, dev steady state is ~0.12 GB/day, so 500 MB is ~4x headroom — wide enough to
survive a heavy debugging day, tight enough to trip on the specific regression that matters: reverting
samplingPercentage to 100 puts dev straight back to ~0.88 GB/day, which the old cap sat above and this
one sits below. A breaker that cannot catch its own removal is decoration.

Prod = 5000, deliberately unchanged: prod has no ingestion baseline to derive a cap from, and guessing
one for an environment with no traffic is the failure being fixed, not repeated. Re-derive it from the
first week of real prod ingestion — deploy/AZURE-PROD-POSTURE.md §5.''')
@minValue(0)
param dailyCapMb int = env == 'prod' ? 5000 : 500

@description('Tags applied to every resource.')
param tags object = {}

var workspaceName = 'log-cleansia-${region}-${env}'
var appInsightsName = 'appi-cleansia-${region}-${env}'

// NOT a cost knob, and the reason is worth pinning because it looks like one: 31 days of analytics
// retention are included in the ingestion price, and Application Insights (App*) tables are retained
// 90 days at no charge on top of that. Both values below sit inside those allowances, so lowering
// either saves exactly nothing and only shortens how far back a bug can be investigated. The one
// thing that is NOT free at 90 is StorageQueueLogs — a resource-log table, so its allowance is the
// 31-day one and prod pays retention on days 32-90 of it.
var retentionInDays = env == 'prod' ? 90 : 30

// dailyQuotaGb is a decimal and Bicep has no float literal, so the MB param is recomposed into one
// through json(): 500 -> '0.500', 5000 -> '5.000'.
var dailyQuotaGb = json('${dailyCapMb / 1000}.${padLeft(dailyCapMb % 1000, 3, '0')}')

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
    workspaceCapping: dailyCapMb > 0 ? {
      dailyQuotaGb: dailyQuotaGb
    } : {}
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
    SamplingPercentage: samplingPercentage == 100 ? null : samplingPercentage
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
