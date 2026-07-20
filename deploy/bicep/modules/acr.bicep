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

@description('Enable the scheduled image purge task (T-0359 prod posture — CI pushes one sha-tagged image per deploy, and nothing ever deletes them). An ACR Task running `acr purge` because the built-in retentionPolicy cannot do this job: it is Premium-only AND only deletes UNTAGGED manifests, while every CI image is tagged with its commit sha. ACR Tasks run on every SKU including this Basic registry.')
param imageRetentionEnabled bool = false

@description('Tags older than this many days are purged (the newest imageRetentionKeepCount per repository always survive).')
param imageRetentionDays int = 30

@description('The newest N tags per repository that always survive the purge, regardless of age.')
param imageRetentionKeepCount int = 10

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

// Nightly (03:00 UTC) purge across every repository: tags older than the cutoff go, the newest N per
// repo always survive (a rollback target is always present), and orphaned untagged manifests are
// swept too. --keep guarantees the currently-deployed image can never be purged by age alone as long
// as fewer than N newer deploys exist; the Functions host references its image by sha tag, so the
// keep-floor is the safety net.
var purgeCommand = 'acr purge --filter \'.*:.*\' --ago ${imageRetentionDays}d --keep ${imageRetentionKeepCount} --untagged'

resource purgeTask 'Microsoft.ContainerRegistry/registries/tasks@2019-06-01-preview' = if (imageRetentionEnabled) {
  parent: registry
  name: 'purge-old-images'
  location: location
  tags: tags
  properties: {
    status: 'Enabled'
    platform: {
      os: 'Linux'
      architecture: 'amd64'
    }
    agentConfiguration: {
      cpu: 2
    }
    timeout: 3600
    step: {
      type: 'EncodedTask'
      encodedTaskContent: base64('version: v1.1.0\nsteps:\n  - cmd: ${purgeCommand}\n    disableWorkingDirectoryOverride: true\n    timeout: 3600\n')
    }
    trigger: {
      timerTriggers: [
        {
          name: 'nightly'
          schedule: '0 3 * * *'
          status: 'Enabled'
        }
      ]
    }
  }
}

@description('Registry resource id (for the AcrPull role assignment in roleAssignments.bicep).')
output registryId string = registry.id

@description('Registry name (consumed by CI az acr build and the Functions image reference).')
output registryName string = registry.name

@description('Login server, e.g. acrcleansiaweudev.azurecr.io — the Functions container image prefix.')
output loginServer string = registry.properties.loginServer
