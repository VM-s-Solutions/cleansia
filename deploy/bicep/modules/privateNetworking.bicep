// The Q-INFRA-03 prod hardening seam: VNet + private endpoints for Postgres and Storage, deployed
// ONLY when main.bicep's privateNetworkingEnabled flag is true (default false — dev keeps the
// public-endpoint + firewall posture, byte-unchanged). One VNet, two subnets: `snet-apps` (delegated
// to Microsoft.Web/serverFarms — the regional VNet-integration subnet every App Service/Functions
// host joins) and `snet-privatelink` (the private endpoints). Private DNS zones make the existing
// FQDNs (pg-cleansia-*.postgres.database.azure.com, stcleansia*.blob/queue/table.core.windows.net)
// resolve to the private IPs from inside the VNet, so no connection string changes.
//
// The Postgres side uses the PRIVATE ENDPOINT model, not VNet injection, on purpose: a flexible
// server's network model is immutable after create — VNet injection would force replacing the live
// server, while a private endpoint attaches to the existing one and only publicNetworkAccess flips.
//
// Flipping this on has operational prerequisites (CI migration path, admin psql access) — the
// owner's decision record and sequence live in deploy/AZURE-PROD-POSTURE.md.

@description('Expansion-seam region token threaded into every resource name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Deployment stage suffix (dev | prod).')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Azure location the network resources are created in.')
param location string

@description('VNet address space. Overridable if it ever needs to peer with another network.')
param vnetAddressPrefix string = '10.60.0.0/16'

@description('Address prefix of the App Service VNet-integration subnet (delegated to Microsoft.Web/serverFarms).')
param appSubnetPrefix string = '10.60.1.0/24'

@description('Address prefix of the private-endpoint subnet.')
param privateEndpointSubnetPrefix string = '10.60.2.0/24'

@description('Resource id of the PostgreSQL Flexible Server the private endpoint targets.')
param postgresServerId string

@description('Resource id of the Storage Account the blob/queue/table private endpoints target.')
param storageAccountId string

@description('Resource tags applied to every network resource.')
param tags object = {}

var vnetName = 'vnet-cleansia-${region}-${env}'
var appSubnetName = 'snet-apps'
var privateEndpointSubnetName = 'snet-privatelink'

resource vnet 'Microsoft.Network/virtualNetworks@2024-05-01' = {
  name: vnetName
  location: location
  tags: tags
  properties: {
    addressSpace: {
      addressPrefixes: [vnetAddressPrefix]
    }
    subnets: [
      {
        name: appSubnetName
        properties: {
          addressPrefix: appSubnetPrefix
          delegations: [
            {
              name: 'appservice'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
        }
      }
      {
        name: privateEndpointSubnetName
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
    ]
  }
}

// Zone names are fixed by Azure Private Link — the FQDN suffix of each service. Table is included
// alongside blob/queue because the Functions runtime store (AzureWebJobsStorage) can touch all three;
// leaving table public-only would strand the host mid-flip.
var privateDnsZoneNames = [
  'privatelink.postgres.database.azure.com'
  'privatelink.blob.${environment().suffixes.storage}'
  'privatelink.queue.${environment().suffixes.storage}'
  'privatelink.table.${environment().suffixes.storage}'
]

resource privateDnsZones 'Microsoft.Network/privateDnsZones@2020-06-01' = [
  for zoneName in privateDnsZoneNames: {
    name: zoneName
    location: 'global'
    tags: tags
  }
]

resource privateDnsZoneLinks 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = [
  for (zoneName, i) in privateDnsZoneNames: {
    parent: privateDnsZones[i]
    name: '${vnetName}-link'
    location: 'global'
    tags: tags
    properties: {
      registrationEnabled: false
      virtualNetwork: {
        id: vnet.id
      }
    }
  }
]

// One private endpoint per service, each paired with the matching DNS zone by index above.
var privateEndpointDefinitions = [
  {
    name: 'pe-pg-cleansia-${region}-${env}'
    targetId: postgresServerId
    groupId: 'postgresqlServer'
    zoneIndex: 0
  }
  {
    name: 'pe-blob-cleansia-${region}-${env}'
    targetId: storageAccountId
    groupId: 'blob'
    zoneIndex: 1
  }
  {
    name: 'pe-queue-cleansia-${region}-${env}'
    targetId: storageAccountId
    groupId: 'queue'
    zoneIndex: 2
  }
  {
    name: 'pe-table-cleansia-${region}-${env}'
    targetId: storageAccountId
    groupId: 'table'
    zoneIndex: 3
  }
]

resource privateEndpoints 'Microsoft.Network/privateEndpoints@2024-05-01' = [
  for pe in privateEndpointDefinitions: {
    name: pe.name
    location: location
    tags: tags
    properties: {
      subnet: {
        id: '${vnet.id}/subnets/${privateEndpointSubnetName}'
      }
      privateLinkServiceConnections: [
        {
          name: pe.name
          properties: {
            privateLinkServiceId: pe.targetId
            groupIds: [pe.groupId]
          }
        }
      ]
    }
  }
]

resource privateEndpointDnsGroups 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2024-05-01' = [
  for (pe, i) in privateEndpointDefinitions: {
    parent: privateEndpoints[i]
    name: 'default'
    properties: {
      privateDnsZoneConfigs: [
        {
          name: 'zone'
          properties: {
            privateDnsZoneId: privateDnsZones[pe.zoneIndex].id
          }
        }
      ]
    }
  }
]

@description('VNet resource id.')
output vnetId string = vnet.id

@description('The delegated App Service integration subnet id — passed to every appService/functionApp instantiation as virtualNetworkSubnetId.')
output appSubnetId string = '${vnet.id}/subnets/${appSubnetName}'
