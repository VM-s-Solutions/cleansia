export const environment = {
  apiHost: 'api-customer.cleansia.cz',
  apiPort: '443',
  apiProtocol: 'https',
  apiBaseUrl: 'https://api-customer.cleansia.cz',
  blobStorageUrl: '',
  // Google Identity Services client id. Public by design (Google authorises by
  // page origin, not by secrecy) but per-deployment. Empty = Google sign-in is
  // switched off and the button is hidden; email/password is unaffected.
  // MANUAL_STEP (google-oauth-prod-client): create/choose the production OAuth
  // client, add the production origin under Google Cloud Console → Credentials
  // → Authorized JavaScript origins, then paste the client id here. Filling
  // this in before the origin is authorised shows a button that 403s.
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
