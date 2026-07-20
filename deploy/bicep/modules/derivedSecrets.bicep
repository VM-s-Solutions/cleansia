// Writes the Key Vault secrets that Bicep can DERIVE from resources it creates (no external input):
//   - Storage--ConnectionString  : built from the storage account's access key (listKeys)
//   - ConnectionStrings--cleansia-db : built from the Postgres FQDN + admin login + the @secure() password
//
// The remaining secrets (Jwt--Key, Stripe--*, SendGrid--ApiKey, Sentry--Dsn, Mapbox--*) are EXTERNAL —
// Bicep cannot know them — so they are NOT written here; a CI step pushes those from GitHub-Environment
// secrets. (ADR-0015 D4: no external secret value is ever in source; derivable values are computed, not
// committed — the storage key is read at deploy time via listKeys, never written to source or an output.)
//
// JWT issuer/audience are deliberately NOT written. No app setting references them — every host
// validates with the code-side values (config "JwtSettings:Issuer" is unset on the deployed hosts, so
// the `?? "cleansia"` fallback applies, and each host passes its audience as a constant). This module
// used to write Jwt--Issuer (the partner API URL) + Jwt--Audience as dead secrets; wiring them into
// app settings would have swapped the live issuer from "cleansia" to that URL and invalidated every
// outstanding JWT on the next deploy — zero benefit, real outage. Issuer/audience stay code-side.

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
