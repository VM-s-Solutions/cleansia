// Storage Account (Standard LRS) for Cleansia — the single account that backs three distinct uses
// (ADR-0015 D2, mandatory): the blob containers (order photos, employee documents, generated
// receipt/invoice PDFs, customer files), the Storage Queues that drive the Functions pipeline
// (each with its `-poison` companion), and the Functions runtime's own AzureWebJobsStorage.
//
// ADR-0017: the `region` token is in the name from day one. Azure Storage account names are
// globally unique, lowercase, alphanumeric, and <=24 chars, so the dashed convention used by the
// other resources is collapsed to `stcleansia<region><stage>` (e.g. `stcleansiaweudev`).
//
// No secret value is committed: the access keys are read at deploy time only to write the
// `Storage--ConnectionString` Key Vault secret (done by the orchestrator/keyVault module, not
// here), and the preferred runtime path is managed identity (Blob/Queue Data Contributor) granted
// by the roleAssignments module. This module emits structure only.

@description('Short region token threaded through the name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Azure location the account is created in. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Deployment stage suffix (dev | prod).')
param stage string

@description('Storage redundancy SKU (Standard_LRS for dev).')
@allowed([
  'Standard_LRS'
  'Standard_ZRS'
  'Standard_GRS'
])
param skuName string = 'Standard_LRS'

@description('Resource tags applied to the account.')
param tags object = {}

// Blob containers (ADR-0015 D2 / infrastructure.md). All private — no anonymous blob access.
var blobContainers = [
  'generated-receipts'
  'generated-invoices'
  'user-files'
  'employee-documents'
  'order-photos'
  'dispute-evidence'
]

// The runtime queue set (QueueNames.cs). Each carries a `-poison` companion (ADR-0002 D3): the
// Functions runtime moves a message to `<queue>-poison` after maxDequeueCount, and a poison
// consumer durably dead-letters it. Provisioning both halves up front means the pipeline is live
// end to end the moment the Functions container starts.
var queueBaseNames = [
  'generate-receipt'
  'generate-invoice'
  'notifications-dispatch'
  'sitewide-promo-fanout'
  'calculate-order-pay'
  'send-email'
]
var poisonQueueNames = [for q in queueBaseNames: '${q}-poison']
var allQueueNames = concat(queueBaseNames, poisonQueueNames)

var storageAccountResourceName = 'stcleansia${region}${stage}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' = {
  name: storageAccountResourceName
  location: location
  tags: tags
  sku: {
    name: skuName
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2024-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource containers 'Microsoft.Storage/storageAccounts/blobServices/containers@2024-01-01' = [
  for name in blobContainers: {
    parent: blobService
    name: name
    properties: {
      publicAccess: 'None'
    }
  }
]

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2024-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource queues 'Microsoft.Storage/storageAccounts/queueServices/queues@2024-01-01' = [
  for name in allQueueNames: {
    parent: queueService
    name: name
  }
]

@description('The Storage Account resource name (used by roleAssignments + the Functions runtime store).')
output storageAccountName string = storageAccount.name

@description('The Storage Account resource id (for MI role assignments).')
output storageAccountId string = storageAccount.id

@description('The blob endpoint (for managed-identity BlobServiceClient construction).')
output blobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('The queue endpoint (for managed-identity QueueClient construction).')
output queueEndpoint string = storageAccount.properties.primaryEndpoints.queue

@description('The provisioned blob container names.')
output blobContainerNames array = blobContainers

@description('The provisioned queue names (base + poison).')
output queueNames array = allQueueNames
