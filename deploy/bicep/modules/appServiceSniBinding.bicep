// The SNI flip for appServiceCustomDomain — re-declares the hostname binding WITH the managed
// certificate's thumbprint (sslState SniEnabled). A separate module solely because ARM forbids
// declaring the same resource twice in one deployment; as a nested deployment this second write to
// the binding is legal. Never instantiated directly by main.bicep — only by appServiceCustomDomain,
// after the certificate has issued.

@description('Name of the EXISTING App Service the hostname is already bound to.')
param siteName string

@description('The custom hostname whose binding is flipped to SNI SSL. Never empty: fail here with a clear message instead of deep in ARM with an opaque one.')
@minLength(1)
param hostname string

@description('Thumbprint of the issued App Service managed certificate.')
param certificateThumbprint string

resource site 'Microsoft.Web/sites@2023-12-01' existing = {
  name: siteName
}

resource sniBinding 'Microsoft.Web/sites/hostNameBindings@2023-12-01' = {
  parent: site
  name: hostname
  properties: {
    siteName: siteName
    hostNameType: 'Verified'
    sslState: 'SniEnabled'
    thumbprint: certificateThumbprint
  }
}
