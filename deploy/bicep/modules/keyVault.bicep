// RBAC-mode Key Vault holding the platform's secret NAMES only (ADR-0015 D4 inventory). NO real
// secret value is ever committed: the owner (or a Secrets-Officer CI step) populates values out of
// band. App + Functions hosts read via their managed identity (Key Vault Secrets User, granted in
// roleAssignments.bicep); access policies are disabled in favour of `enableRbacAuthorization`.
// ADR-0017: the `region` token is in the vault name from day one (kv-cleansia-weu-dev).

@description('Azure region the resources are deployed to.')
param location string

@description('Expansion-seam region token threaded into every resource name (ADR-0017). Default West Europe.')
param region string = 'weu'

@description('Deployment stage suffix (dev | prod). Drives the vault name.')
@allowed([
  'dev'
  'prod'
])
param env string

@description('Tenant id of the subscription the vault lives in.')
param tenantId string = subscription().tenantId

@description('Tags applied to every resource.')
param tags object = {}

@description('Allow Azure trusted services + public network for dev; prod tightens via main.bicep flag.')
param allowPublicNetworkAccess bool = true

@description('''
Pre-create each secret as an EMPTY placeholder so App Service Key-Vault references resolve before the
owner populates values. OFF by default: a re-run would clobber owner-set values back to empty, so the
idempotent path is owner-creates-the-secret and Bicep only emits the vault + RBAC + the name list.
''')
param createEmptySecretPlaceholders bool = false

// Secret NAMES the platform reads (ADR-0015 D4 inventory). Values are owner-populated post-deploy
// via the portal / `az keyvault secret set` or a Secrets-Officer CI step. NO value is ever committed.
// Jwt--Issuer / Jwt--Audience are NOT in this inventory: no app setting references them — the hosts
// validate with the code-side issuer fallback + constant audiences (see modules/derivedSecrets.bicep).
var secretNames = [
  'ConnectionStrings--cleansia-db'
  'Jwt--Key'
  'Csrf--Secret'
  'Stripe--SecretKey'
  'Stripe--WebhookSecret'
  'Stripe--PublishableKey'
  'SendGrid--ApiKey'
  'SendGrid--ResetPasswordTemplateId'
  'SendGrid--OrderReceiptTemplateId'
  'SendGrid--EmailConfirmationTemplateId'
  'SendGrid--PeriodClosedTemplateId'
  'SendGrid--PeriodEndReminderTemplateId'
  'SendGrid--OrderStatusUpdateTemplateId'
  'Sentry--Dsn'
  'Storage--ConnectionString'
  'Mapbox--GeocodingAccessToken'
]

var keyVaultName = 'kv-cleansia-${region}-${env}'

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: env == 'prod' ? true : null
    publicNetworkAccess: allowPublicNetworkAccess ? 'Enabled' : 'Disabled'
    networkAcls: {
      bypass: 'AzureServices'
      defaultAction: allowPublicNetworkAccess ? 'Allow' : 'Deny'
    }
  }
}

// Optional empty placeholders (default OFF). When enabled, only the FIRST provision should run with
// this true — a later re-run with it true overwrites owner-populated values with empty. No real
// secret material is ever in source: the value is the empty string, the owner sets the real value.
resource secrets 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = [
  for name in (createEmptySecretPlaceholders ? secretNames : []): {
    parent: keyVault
    name: name
    properties: {
      value: ''
      attributes: {
        enabled: true
      }
    }
  }
]

output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri

@description('Secret names declared in the vault — for main.bicep to compose Key Vault reference URIs.')
output secretNames array = secretNames
