// PostgreSQL Flexible Server (Burstable B1ms for dev) for Cleansia.
// ADR-0015 D2/D3: Burstable B1ms, public endpoint + firewall (Azure services + admin IP),
// TLS-required, one `Cleansia` database. ADR-0017: the `region` token is in every name from day
// one so a second region is a new parameter value, not a rename of a live resource.
//
// The admin password is NEVER inline here. The orchestrator (main.bicep) reads it from Key Vault
// (a `getSecret` reference on an existing Key Vault) and passes it into the @secure() parameter,
// so no secret value is committed in Bicep, the param file, or any output.

@description('Short region token threaded through every name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Azure location the server is created in. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Deployment stage suffix (dev | prod).')
param stage string

@description('PostgreSQL server SKU name (Burstable B1ms for dev).')
param skuName string = 'Standard_B1ms'

@description('PostgreSQL server SKU tier.')
@allowed([
  'Burstable'
  'GeneralPurpose'
  'MemoryOptimized'
])
param skuTier string = 'Burstable'

@description('Allocated storage in GB.')
param storageSizeGB int = 32

@description('PostgreSQL major version.')
param postgresVersion string = '16'

@description('Administrator login name. NOT a secret (the password is).')
param administratorLogin string

@description('Administrator password. Sourced from Key Vault by the orchestrator — never inline.')
@secure()
param administratorPassword string

@description('The application database name created on the server.')
param databaseName string = 'Cleansia'

@description('Owner/admin public IP allowed through the firewall (for the EF-bundle apply + manual access).')
param adminIpAddress string

@description('Resource tags applied to the server.')
param tags object = {}

var postgresServerName = 'pg-cleansia-${region}-${stage}'

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: postgresServerName
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuTier
  }
  properties: {
    version: postgresVersion
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorPassword
    storage: {
      storageSizeGB: storageSizeGB
      autoGrow: 'Enabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
    authConfig: {
      passwordAuth: 'Enabled'
      activeDirectoryAuth: 'Disabled'
    }
  }
}

// require_secure_transport=on — TLS is mandatory for every client connection (ADR-0015 D3).
resource requireSecureTransport 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgres
  name: 'require_secure_transport'
  properties: {
    value: 'on'
    source: 'user-override'
  }
}

// Firewall: allow other Azure services (the App Services + Functions reach the server). The
// 0.0.0.0 sentinel rule is Azure's "allow Azure-internal traffic" switch, NOT an open-to-internet
// rule — it does not expose the server to arbitrary public IPs.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAllAzureServicesAndResources'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Firewall: the owner/admin public IP, for the EF-bundle migration apply + manual psql access.
resource allowAdminIp 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = {
  parent: postgres
  name: 'AllowAdminIp'
  properties: {
    startIpAddress: adminIpAddress
    endIpAddress: adminIpAddress
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
}

@description('The PostgreSQL Flexible Server resource name.')
output serverName string = postgres.name

@description('The fully-qualified domain name of the server (host for the connection string).')
output fullyQualifiedDomainName string = postgres.properties.fullyQualifiedDomainName

@description('The application database name.')
output applicationDatabaseName string = database.name
