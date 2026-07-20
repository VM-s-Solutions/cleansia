// Alerting (ADR-0015 D2/D3) — the Action Group + plain ARM metric alerts that make the telemetry
// actually page someone: per-site Http5xx + latency over the six web hosts, an App Insights
// exceptions spike (covers the APIs + Functions), and the Postgres health trio. Scopes are built
// from resource NAMES passed by main.bicep — deploy-time strings, never module outputs, because
// the per-site for-loop needs a deploy-time array (BCP182); main.bicep carries the explicit
// dependsOn so the alerts never race the resources they watch.
//
// Env gating: dev = severity 3 + wide windows (owner-inbox noise floor); prod = severity 1-2 +
// tight windows (paging). Poison-queue depth is NOT here — queue signals need diagnostic settings
// + a scheduled-query alert, which live in modules/queueAlerts.bicep attached to this module's
// exported actionGroupId.

@description('Deployment stage: dev | prod. Drives severities, thresholds, and windows.')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Expansion-seam region token threaded into every resource name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('The single ops email the Action Group notifies.')
param alertEmail string

@description('Deploy-time resource NAMES of the web hosts to alert on (the five API App Services + the customer SSR).')
param siteNames array

@description('Deploy-time resource NAME of the queue/timer Function App — watched by its own HealthCheckStatus alert (the 2026-07-18 outage was a silent Functions-host failure).')
param functionsSiteName string

@description('Deploy-time resource name of the PostgreSQL Flexible Server (mirrors modules/postgres.bicep naming).')
param postgresServerName string

@description('Deploy-time resource name of the Application Insights component (mirrors modules/appInsights.bicep naming).')
param appInsightsName string

@description('Resource tags applied to every alert resource.')
param tags object = {}

var isProd = env == 'prod'

// Shared evaluation cadence — prod tight (page fast), dev wide (fewer, batched signals).
var windowSize = isProd ? 'PT5M' : 'PT15M'
var evaluationFrequency = isProd ? 'PT1M' : 'PT5M'

var http5xxSeverity = isProd ? 1 : 3
var http5xxThreshold = isProd ? 5 : 25
var latencySeverity = isProd ? 2 : 3
var responseTimeThresholdSeconds = 2
var exceptionsSeverity = isProd ? 2 : 3
var exceptionsThreshold = isProd ? 10 : 25

// ---------------------------------------------------------------------------------------------------
// Action Group — the one email receiver every alert below fans into. Location is 'global' by design
// (action groups are not regional resources).
// ---------------------------------------------------------------------------------------------------

resource actionGroup 'Microsoft.Insights/actionGroups@2022-06-01' = {
  name: 'ag-cleansia-${region}-${env}'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'cleansia' // 12-char Azure limit — keep it env-agnostic
    enabled: true
    emailReceivers: [
      {
        name: 'ops-email'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

// ---------------------------------------------------------------------------------------------------
// Per-site alerts — Http5xx count + average response time over each of the six web hosts. The site
// names already carry the region/env tokens, so the alert names reuse them verbatim.
// ---------------------------------------------------------------------------------------------------

resource http5xxAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for siteName in siteNames: {
    name: 'alert-http5xx-${siteName}'
    location: 'global'
    tags: tags
    properties: {
      description: 'HTTP 5xx responses on ${siteName} exceeded ${http5xxThreshold} in ${windowSize}.'
      severity: http5xxSeverity
      enabled: true
      scopes: [resourceId('Microsoft.Web/sites', siteName)]
      evaluationFrequency: evaluationFrequency
      windowSize: windowSize
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: 'Http5xx'
            metricNamespace: 'Microsoft.Web/sites'
            metricName: 'Http5xx'
            operator: 'GreaterThan'
            threshold: http5xxThreshold
            timeAggregation: 'Total'
          }
        ]
      }
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]

// The Functions host has no request traffic (queue/timer only) so Http5xx/latency don't apply — instead
// watch the HealthCheckStatus metric fed by the /api/health probe (healthCheckPath on functionApp.bicep).
// It reports the % of healthy instances; < 100 means an instance is failing its probe (DB/queue down, or
// the worker crash-looping — exactly the 2026-07-18 silent outage). Sev = the http5xx tier: a dead
// background host means no emails/receipts/invoices/push go out. windowSize smooths deploy-time blips.
resource functionsHealthAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-functions-health-${functionsSiteName}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Functions host ${functionsSiteName} is failing its /api/health probe (unhealthy instance) over ${windowSize}.'
    severity: http5xxSeverity
    enabled: true
    scopes: [resourceId('Microsoft.Web/sites', functionsSiteName)]
    evaluationFrequency: evaluationFrequency
    windowSize: windowSize
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'HealthCheckStatus'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'HealthCheckStatus'
          operator: 'LessThan'
          threshold: 100
          timeAggregation: 'Average'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

resource latencyAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for siteName in siteNames: {
    name: 'alert-latency-${siteName}'
    location: 'global'
    tags: tags
    properties: {
      description: 'Average HTTP response time on ${siteName} exceeded ${responseTimeThresholdSeconds}s over ${windowSize}.'
      severity: latencySeverity
      enabled: true
      scopes: [resourceId('Microsoft.Web/sites', siteName)]
      evaluationFrequency: evaluationFrequency
      windowSize: windowSize
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: 'HttpResponseTime'
            metricNamespace: 'Microsoft.Web/sites'
            metricName: 'HttpResponseTime'
            operator: 'GreaterThan'
            threshold: responseTimeThresholdSeconds
            timeAggregation: 'Average'
          }
        ]
      }
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]

// ---------------------------------------------------------------------------------------------------
// App Insights exceptions spike — ONE alert over the shared component, so it covers server-side
// exceptions from all five APIs, the SSR, and the Functions host in a single signal.
// ---------------------------------------------------------------------------------------------------

resource exceptionsAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'alert-exceptions-cleansia-${region}-${env}'
  location: 'global'
  tags: tags
  properties: {
    description: 'Server exceptions across the APIs/SSR/Functions exceeded ${exceptionsThreshold} in ${windowSize}.'
    severity: exceptionsSeverity
    enabled: true
    scopes: [resourceId('Microsoft.Insights/components', appInsightsName)]
    evaluationFrequency: evaluationFrequency
    windowSize: windowSize
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          criterionType: 'StaticThresholdCriterion'
          name: 'ExceptionsCount'
          metricNamespace: 'microsoft.insights/components'
          metricName: 'exceptions/count'
          operator: 'GreaterThan'
          threshold: exceptionsThreshold
          timeAggregation: 'Count'
        }
      ]
    }
    actions: [
      {
        actionGroupId: actionGroup.id
      }
    ]
  }
}

// ---------------------------------------------------------------------------------------------------
// Postgres Flexible Server health — failed connections (any failure pages in prod), CPU saturation,
// and storage headroom (auto-grow still needs a human before the ceiling).
// ---------------------------------------------------------------------------------------------------

var postgresAlertRules = [
  {
    shortName: 'connfailed'
    metricName: 'connections_failed'
    timeAggregation: 'Total'
    threshold: isProd ? 0 : 10
    severity: isProd ? 1 : 3
    description: 'Failed connections on the PostgreSQL server ${postgresServerName}.'
  }
  {
    shortName: 'cpu'
    metricName: 'cpu_percent'
    timeAggregation: 'Average'
    threshold: 90
    severity: isProd ? 2 : 3
    description: 'CPU above 90% on the PostgreSQL server ${postgresServerName}.'
  }
  {
    shortName: 'storage'
    metricName: 'storage_percent'
    timeAggregation: 'Average'
    threshold: 85
    severity: isProd ? 2 : 3
    description: 'Storage above 85% on the PostgreSQL server ${postgresServerName}.'
  }
]

resource postgresAlerts 'Microsoft.Insights/metricAlerts@2018-03-01' = [
  for rule in postgresAlertRules: {
    name: 'alert-pg-${rule.shortName}-cleansia-${region}-${env}'
    location: 'global'
    tags: tags
    properties: {
      description: rule.description
      severity: rule.severity
      enabled: true
      scopes: [resourceId('Microsoft.DBforPostgreSQL/flexibleServers', postgresServerName)]
      evaluationFrequency: evaluationFrequency
      windowSize: windowSize
      criteria: {
        'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
        allOf: [
          {
            criterionType: 'StaticThresholdCriterion'
            name: rule.metricName
            metricNamespace: 'Microsoft.DBforPostgreSQL/flexibleServers'
            metricName: rule.metricName
            operator: 'GreaterThan'
            threshold: rule.threshold
            timeAggregation: rule.timeAggregation
          }
        ]
      }
      actions: [
        {
          actionGroupId: actionGroup.id
        }
      ]
    }
  }
]

@description('The Action Group resource id — future alert modules (e.g. the poison-queue scheduled query) attach to it.')
output actionGroupId string = actionGroup.id
