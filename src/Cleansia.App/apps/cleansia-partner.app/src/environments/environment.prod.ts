export const environment = {
  apiHost: 'api.cleansia.cz',
  apiPort: '443',
  apiProtocol: 'https',
  apiBaseUrl: 'https://api.cleansia.cz',
  blobStorageUrl: '',
  googleClientId: '',
  betaGateEnabled: false,
  betaGateUrl: '',
  isDevelopment: false,
  sentryDsn: '',
  bugReportUrl: '',
  // token-free "is geocoding configured" flag only — the real
  // Mapbox token lives server-side on the partner API proxy, never in this
  // browser bundle. Set to any non-empty value (e.g. 'enabled') to show the UI.
  // MANUAL_STEP (rotate-mapbox-token): rotate the exposed token and provision the
  // new value server-side; do NOT paste a token here.
  mapboxToken: '',
};
