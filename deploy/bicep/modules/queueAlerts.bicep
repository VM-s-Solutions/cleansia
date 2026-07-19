// Poison-queue alerting (T-0360) — the half alerts.bicep deliberately deferred: per-queue signals
// are not plain metrics, they need DIAGNOSTIC SETTINGS on the queue service (log rows into Log
// Analytics) plus a SCHEDULED-QUERY rule over those logs. Attaches to the Action Group alerts.bicep
// exports for exactly this. Deployed by main.bicep under the same gate as alerts.bicep
// (!empty(alertEmail)) and mirrors its naming (alert-*-cleansia-<region>-<env>) and severity
// convention (prod = paging severity + tight window, dev = inbox severity + wide window).

@description('Deployment stage: dev | prod. Drives severity and evaluation cadence (mirrors alerts.bicep).')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Expansion-seam region token threaded into every resource name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Azure location of the scheduled-query rule — must be the Log Analytics workspace region.')
param location string

@description('Deploy-time resource name of the Storage Account whose queue service is monitored (mirrors modules/storage.bicep naming).')
param storageAccountName string

@description('Resource id of the Log Analytics workspace the queue logs flow to and the alert query runs against.')
param logAnalyticsWorkspaceId string

@description('The Action Group resource id exported by alerts.bicep — the alert fans into it.')
param actionGroupId string

@description('Resource tags applied to every alert resource.')
param tags object = {}

var isProd = env == 'prod'
var windowSize = isProd ? 'PT5M' : 'PT15M'
var evaluationFrequency = isProd ? 'PT5M' : 'PT15M'
var severity = isProd ? 1 : 3

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2024-01-01' existing = {
  parent: storageAccount
  name: 'default'
}

// StorageWrite carries PutMessage (the poison move IS a PutMessage on the -poison queue);
// StorageDelete carries the poison consumer's dead-letter drain for forensics. StorageRead is
// deliberately OFF: the always-on Functions host polls all 12 queues continuously, and logging every
// GetMessages poll would flood the workspace (dev runs a 1 GB/day cap) for zero alerting value.
resource queueDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  scope: queueService
  name: 'queue-logs-cleansia-${region}-${env}'
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logAnalyticsDestinationType: 'Dedicated'
    logs: [
      {
        category: 'StorageWrite'
        enabled: true
      }
      {
        category: 'StorageDelete'
        enabled: true
      }
    ]
  }
}

// A successful PutMessage into any *-poison queue means the Functions runtime gave up on a message
// after maxDequeueCount — durable work (receipt/invoice/email/push/pay) failed repeatedly. The alert
// fires on the EVENT rather than on queue depth: the poison consumer drains the queue within
// seconds, so a depth sample can already read 0 while the failure is real; the PutMessage log row is
// the durable signal ("depth > 0" style without the race).
resource poisonQueueAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: 'alert-poison-queue-cleansia-${region}-${env}'
  location: location
  tags: tags
  kind: 'LogAlert'
  properties: {
    displayName: 'alert-poison-queue-cleansia-${region}-${env}'
    description: 'One or more messages were moved to a *-poison queue on ${storageAccountName} within ${windowSize} — a queue consumer is failing past maxDequeueCount.'
    severity: severity
    enabled: true
    scopes: [logAnalyticsWorkspaceId]
    evaluationFrequency: evaluationFrequency
    windowSize: windowSize
    criteria: {
      allOf: [
        {
          query: 'StorageQueueLogs | where OperationName == "PutMessage" | where StatusText == "Success" | where ObjectKey contains "-poison"'
          timeAggregation: 'Count'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    // On a fresh provision the StorageQueueLogs table does not exist until the diagnostic setting
    // above has shipped its first rows — without this, rule creation fails query validation and the
    // whole first deployment with it.
    skipQueryValidation: true
    actions: {
      actionGroups: [actionGroupId]
    }
  }
  dependsOn: [queueDiagnostics]
}
