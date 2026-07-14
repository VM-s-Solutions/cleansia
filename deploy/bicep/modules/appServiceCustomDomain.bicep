// Custom domain for ONE App Service host (the same-site enabler for deployed web cookie auth) —
// hostname binding + App Service MANAGED certificate + the SNI flip, sequenced inside one deployment.
// Instantiated by main.bicep once per configured `customDomains` entry that targets an App Service
// (the SWAs bind their domains themselves — staticWebApp.bicep).
//
// The well-known two-phase reality this module sequences:
//   1. the hostname binding must exist BEFORE the managed certificate can issue (App Service validates
//      issuance against the bound hostname), so the binding is created first WITHOUT SSL;
//   2. the binding can only be flipped to SniEnabled AFTER the certificate exists — its thumbprint IS
//      the flip. ARM forbids declaring the same resource twice in one deployment, so the flip lives in
//      the nested appServiceSniBinding module: a nested module is its own ARM deployment, which makes
//      the second write to the binding legal. One `az deployment group create` therefore completes all
//      three phases.
//
// DNS is the true precondition (owner, BEFORE deploying an entry): the CNAME (subdomain) or A record
// (apex) to the host plus the `asuid.<hostname>` TXT verification record — deploy/AZURE-DEV-RUNBOOK.md
// §12. Managed certificates are free and auto-renew, but only while that DNS stays in place; they never
// cover wildcards (one hostname = one binding = one cert).
//
// RE-DEPLOY TRANSIENT: ARM PUT is full-replace, so every re-deploy re-PUTs the phase-1 binding without
// SSL and phase 3 restores SniEnabled moments later — a brief HTTPS blip on the CUSTOM hostname per
// deploy (dev auto-deploys on every merge). The *.azurewebsites.net default hostname is unaffected;
// this is the accepted cost of completing the two-phase sequence in one declarative deployment.

@description('Name of the EXISTING App Service the hostname binds to (e.g. web-cleansia-customer-weu-dev). The site must already be deployed — main.bicep passes an explicit dependsOn.')
param siteName string

@description('The custom hostname to bind — a bare hostname, no scheme (e.g. dev.cleansia.cz). Never empty: fail here with a clear message instead of deep in ARM with an opaque one.')
@minLength(1)
param hostname string

@description('Resource id of the App Service Plan the managed certificate is issued into. Must be the plan hosting the site.')
param appServicePlanId string

@description('Azure location — must match the App Service Plan location.')
param location string

@description('Resource tags applied to the certificate (bindings do not carry tags).')
param tags object = {}

resource site 'Microsoft.Web/sites@2023-12-01' existing = {
  name: siteName
}

// Phase 1 — bind the hostname WITHOUT SSL. Azure verifies domain ownership here against the
// pre-created DNS (CNAME/A + asuid TXT); a missing record fails the deployment at this resource —
// fix DNS and simply re-run (the whole sequence is idempotent).
resource hostBinding 'Microsoft.Web/sites/hostNameBindings@2023-12-01' = {
  parent: site
  name: hostname
  properties: {
    siteName: siteName
    hostNameType: 'Verified'
  }
}

// Phase 2 — the free App Service managed certificate; issuance validates against the binding above.
resource managedCertificate 'Microsoft.Web/certificates@2023-12-01' = {
  name: 'cert-${hostname}'
  location: location
  tags: tags
  properties: {
    canonicalName: hostname
    serverFarmId: appServicePlanId
  }
  dependsOn: [
    hostBinding
  ]
}

// Phase 3 — flip the binding to SniEnabled with the issued certificate's thumbprint (see header for
// why this must be a nested module).
module sniBinding 'appServiceSniBinding.bicep' = {
  name: 'sni-${hostname}'
  params: {
    siteName: siteName
    hostname: hostname
    certificateThumbprint: managedCertificate.properties.thumbprint
  }
}

@description('The bound custom hostname.')
output hostname string = hostname

@description('Thumbprint of the issued managed certificate.')
output certificateThumbprint string = managedCertificate.properties.thumbprint
