export const environment = {
  apiHost: 'api-cleansia-partner-dev.azurewebsites.net',
  apiPort: '443',
  apiProtocol: 'https',
  apiBaseUrl: 'https://api-cleansia-partner-dev.azurewebsites.net',
  blobStorageUrl: '',
  googleClientId: '',
  betaGateEnabled: false,
  betaGateUrl: '',
  isDevelopment: false,
  sentryDsn: '',
  bugReportUrl:
    'https://docs.google.com/spreadsheets/d/1k4IbmrKPkZo79D4pDzukjUjQqY-ipnXqkUSSFoHtfFg/edit?usp=sharing',
  // token-free "is geocoding configured" flag only — the real
  // Mapbox token lives server-side on the partner API proxy, never in this
  // browser bundle. Set to any non-empty value (e.g. 'enabled') to show the UI.
  // MANUAL_STEP (rotate-mapbox-token): rotate the exposed token and provision the
  // new value server-side; do NOT paste a token here.
  mapboxToken: '',
};
