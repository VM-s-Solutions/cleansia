@description('Deployment region token threaded into every name (ADR-0017 region seam; default West Europe).')
param region string = 'weu'

@description('Deployment stage suffix (dev | prod).')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Azure location to create the registry in.')
param location string

@description('ACR SKU. Basic for both dev and prod per ADR-0015 D2.')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param sku string = 'Basic'

@description('Resource tags.')
param tags object = {}

// ACR names are globally unique, alphanumeric only (no hyphens), 5-50 chars, lowercase.
// The region/env tokens are folded in without separators: acrcleansia<region><env>.
var registryName = toLower('acrcleansia${region}${env}')

resource registry 'Microsoft.ContainerRegistry/registries@2023-11-01-preview' = {
  name: registryName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // Admin user disabled: the Functions host pulls via its managed identity (AcrPull),
    // and CI pushes via OIDC — no shared admin credential exists to leak (ADR-0015 D4).
    adminUserEnabled: false
    publicNetworkAccess: 'Enabled'
    anonymousPullEnabled: false
    dataEndpointEnabled: false
    zoneRedundancy: 'Disabled'
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      retentionPolicy: {
        status: 'disabled'
        days: 7
      }
    }
  }
}

@description('Registry resource id (for the AcrPull role assignment in roleAssignments.bicep).')
output registryId string = registry.id

@description('Registry name (consumed by CI az acr build and the Functions image reference).')
output registryName string = registry.name

@description('Login server, e.g. acrcleansiaweudev.azurecr.io — the Functions container image prefix.')
output loginServer string = registry.properties.loginServer
