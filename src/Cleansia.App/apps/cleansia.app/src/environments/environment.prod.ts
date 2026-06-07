export const environment = {
  apiHost: 'api-customer.cleansia.cz',
  apiPort: '443',
  apiProtocol: 'https',
  apiBaseUrl: 'https://api-customer.cleansia.cz',
  blobStorageUrl: '',
  googleClientId: '',
  isDevelopment: false,
  sentryDsn: '',
  bugReportUrl: '',
  // The Mapbox token must NEVER ship in the browser bundle.
  // The real token lives server-side (process.env.MAPBOX_TOKEN, injected by the
  // same-origin proxy in server.ts). This is only a token-free "is geocoding
  // configured" flag — set it to any non-empty value (e.g. 'enabled') once the
  // server-side MAPBOX_TOKEN is provisioned to show the autocomplete UI.
  // MANUAL_STEP (rotate-mapbox-token): rotate the previously-exposed Mapbox token
  // in the Mapbox account and provision the new value as the server-side
  // process.env.MAPBOX_TOKEN — do NOT paste any token into this browser bundle.
  mapboxToken: '',
};
