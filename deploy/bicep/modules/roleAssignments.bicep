// The managed-identity role grants that wire the platform's hosts to Key Vault, Storage, and ACR
// (ADR-0015 D4). App + Functions MIs -> Key Vault Secrets User + Storage Blob/Queue Data Contributor;
// the Functions MI also -> AcrPull (it pulls the QuestPDF container image); the CI principal ->
// Key Vault Secrets Officer (so a guarded CI step can write secret values). Least privilege: the app
// hosts get Secrets *User* (read), never Officer. Role assignment names are deterministic guids so a
// re-provision is idempotent (no duplicate assignments).

@description('Resource id of the Key Vault the app identities read secrets from.')
param keyVaultId string

@description('Resource id of the Storage Account the app identities use for blob + queue.')
param storageAccountId string

@description('Resource id of the Container Registry the Functions host pulls its image from.')
param acrId string

@description('System-assigned managed-identity principal ids of the app hosts (5 APIs + SSR) that read Key Vault + Storage.')
param appPrincipalIds array

@description('System-assigned managed-identity principal id of the Functions host (Key Vault + Storage + ACR Pull). Empty string skips it.')
param functionsPrincipalId string = ''

@description('Object id of the CI/provisioning principal granted Key Vault Secrets Officer. Empty string skips it (owner may grant out of band).')
param ciPrincipalId string = ''

// Built-in role definition ids (stable, tenant-independent).
var roleIds = {
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  keyVaultSecretsOfficer: 'b86a8fe4-44ce-4948-aff5-fbb1a28f9e2c'
  storageBlobDataContributor: 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
  storageQueueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  acrPull: '7f951dda-4ed3-4680-a7ca-43fe172d538d'
}

// Every host MI that consumes Key Vault + Storage. The Functions MI is appended when supplied so it
// gets the same Secrets-User + Storage data roles as the web hosts (plus AcrPull below).
var storageAndVaultPrincipals = empty(functionsPrincipalId) ? appPrincipalIds : concat(appPrincipalIds, [
  functionsPrincipalId
])

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: last(split(keyVaultId, '/'))
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: last(split(storageAccountId, '/'))
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' existing = {
  name: last(split(acrId, '/'))
}

// App + Functions identities -> Key Vault Secrets User (read secret values, not manage them).
resource kvSecretsUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in storageAndVaultPrincipals: {
    name: guid(keyVault.id, principalId, roleIds.keyVaultSecretsUser)
    scope: keyVault
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultSecretsUser)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// App + Functions identities -> Storage Blob Data Contributor (the MI path the app's DefaultAzureCredential prefers).
resource blobDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in storageAndVaultPrincipals: {
    name: guid(storageAccount.id, principalId, roleIds.storageBlobDataContributor)
    scope: storageAccount
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageBlobDataContributor)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// App + Functions identities -> Storage Queue Data Contributor (the queue -> Functions pipeline).
resource queueDataContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in storageAndVaultPrincipals: {
    name: guid(storageAccount.id, principalId, roleIds.storageQueueDataContributor)
    scope: storageAccount
    properties: {
      roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.storageQueueDataContributor)
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// Functions identity -> AcrPull (pull the QuestPDF container image from ACR).
resource functionsAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(functionsPrincipalId)) {
  name: guid(acr.id, functionsPrincipalId, roleIds.acrPull)
  scope: acr
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.acrPull)
    principalId: functionsPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// CI/provisioning principal -> Key Vault Secrets Officer (write secret values during provisioning).
resource ciSecretsOfficer 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(ciPrincipalId)) {
  name: guid(keyVault.id, ciPrincipalId, roleIds.keyVaultSecretsOfficer)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleIds.keyVaultSecretsOfficer)
    principalId: ciPrincipalId
    principalType: 'ServicePrincipal'
  }
}
