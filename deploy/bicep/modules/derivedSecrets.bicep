// Writes the Key Vault secrets that Bicep can DERIVE from resources it creates (no external input):
//   - Storage--ConnectionString  : built from the storage account's access key (listKeys)
//   - ConnectionStrings--cleansia-db : built from the Postgres FQDN + admin login + the @secure() password
//   - Jwt--Issuer / Jwt--Audience : deterministic config values
//
// The remaining secrets (Jwt--Key, Stripe--*, SendGrid--ApiKey, Sentry--Dsn, Mapbox--*) are EXTERNAL —
// Bicep cannot know them — so they are NOT written here; a CI step pushes those from GitHub-Environment
// secrets. (ADR-0015 D4: no external secret value is ever in source; derivable values are computed, not
// committed — the storage key is read at deploy time via listKeys, never written to source or an output.)

@description('Name of the Key Vault these secrets are written into.')
param keyVaultName string

@description('Storage account name whose key builds the Storage connection string.')
param storageAccountName string

@description('PostgreSQL fully-qualified domain name (the DB connection-string host).')
param postgresFqdn string

@description('PostgreSQL admin login (non-secret).')
param postgresAdministratorLogin string

@description('PostgreSQL admin password (@secure() — supplied at deploy time, never committed).')
@secure()
param postgresAdministratorPassword string

@description('The application database name.')
param databaseName string = 'Cleansia'

@description('JWT issuer — the partner API host base URL (deterministic from region/env).')
param jwtIssuer string

@description('JWT audience — a constant config value.')
param jwtAudience string = 'cleansia'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2024-01-01' existing = {
  name: storageAccountName
}

// Storage connection string from the account's primary key (read at deploy time; never persisted to source).
var storageConnectionString = 'DefaultEndpointsProtocol=https;AccountName=${storageAccountName};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'

// Npgsql connection string. The password is alphanumeric-only (runbook §1) so no escaping is needed.
var dbConnectionString = 'Host=${postgresFqdn};Database=${databaseName};Username=${postgresAdministratorLogin};Password=${postgresAdministratorPassword};Ssl Mode=Require;Trust Server Certificate=true'

resource storageConnSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Storage--ConnectionString'
  properties: {
    value: storageConnectionString
  }
}

resource dbConnSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'ConnectionStrings--cleansia-db'
  properties: {
    value: dbConnectionString
  }
}

resource jwtIssuerSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Jwt--Issuer'
  properties: {
    value: jwtIssuer
  }
}

resource jwtAudienceSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'Jwt--Audience'
  properties: {
    value: jwtAudience
  }
}
