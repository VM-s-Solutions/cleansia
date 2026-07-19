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

@description('High availability mode (T-0359 prod posture). Dev = Disabled. ZoneRedundant/SameZone require a GeneralPurpose or MemoryOptimized tier — the Burstable dev SKU rejects HA.')
@allowed([
  'Disabled'
  'SameZone'
  'ZoneRedundant'
])
param highAvailabilityMode string = 'Disabled'

@description('Geo-redundant backup (T-0359 prod posture). Dev = Disabled. IMMUTABLE after server create — flipping it on an existing server forces a replacement, so prod must set it at provision time.')
@allowed([
  'Disabled'
  'Enabled'
])
param geoRedundantBackup string = 'Disabled'

@description('Backup retention in days (7-35). Dev = 7; prod raises it.')
@minValue(7)
@maxValue(35)
param backupRetentionDays int = 7

@description('Public network access (the Q-INFRA-03 seam). Enabled = the dev posture (firewall rules below apply). Disabled = private-endpoint-only — the firewall rules are skipped, and the CI migration path + admin psql access need a private path (see deploy/AZURE-PROD-POSTURE.md).')
@allowed([
  'Enabled'
  'Disabled'
])
param publicNetworkAccess string = 'Enabled'

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
      backupRetentionDays: backupRetentionDays
      geoRedundantBackup: geoRedundantBackup
    }
    highAvailability: {
      mode: highAvailabilityMode
    }
    network: {
      publicNetworkAccess: publicNetworkAccess
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

// Azure Database for PostgreSQL Flexible Server blocks CREATE EXTENSION unless the extension is
// allow-listed in the `azure.extensions` server parameter. The first migration runs
// `CREATE EXTENSION citext` + `pg_trgm` (CleansiaDbContext.HasPostgresExtension), so both must be listed
// here or migrate fails with "extension ... is not allow-listed". (depends-on requireSecureTransport so
// the two config writes serialize — Postgres applies parameter changes one at a time.)
resource allowedExtensions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: postgres
  name: 'azure.extensions'
  dependsOn: [requireSecureTransport]
  properties: {
    value: 'CITEXT,PG_TRGM'
    source: 'user-override'
  }
}

// Firewall: allow other Azure services (the App Services + Functions reach the server). The
// 0.0.0.0 sentinel rule is Azure's "allow Azure-internal traffic" switch, NOT an open-to-internet
// rule — it does not expose the server to arbitrary public IPs. Dev-accepted only (Q-INFRA-03):
// when publicNetworkAccess is Disabled the server takes no firewall rules at all — traffic arrives
// via the private endpoint (modules/privateNetworking.bicep).
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (publicNetworkAccess == 'Enabled') {
  parent: postgres
  name: 'AllowAllAzureServicesAndResources'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Firewall: the owner/admin public IP, for the EF-bundle migration apply + manual psql access.
resource allowAdminIp 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2024-08-01' = if (publicNetworkAccess == 'Enabled') {
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

@description('The PostgreSQL Flexible Server resource id (the private-endpoint target when the Q-INFRA-03 seam is enabled).')
output serverId string = postgres.id

@description('The fully-qualified domain name of the server (host for the connection string).')
output fullyQualifiedDomainName string = postgres.properties.fullyQualifiedDomainName

@description('The application database name.')
output applicationDatabaseName string = database.name
