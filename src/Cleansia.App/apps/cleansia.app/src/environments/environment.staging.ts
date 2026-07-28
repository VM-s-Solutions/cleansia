// apiBaseUrl here is also the target of proxy.devremote.conf.json — keep the
// two in sync when the deployed dev host changes.
export const environment = {
  apiHost: 'api-cleansia-customer-weu-dev.azurewebsites.net',
  apiPort: '443',
  apiProtocol: 'https',
  apiBaseUrl: 'https://customer-api.dev.cleansia.cz',
  blobStorageUrl: '',
  // Google Identity Services client id. Public by design — Google authorises by
  // page origin, not by secrecy — so it belongs in the bundle. Same client as
  // local dev on purpose: dev and localhost share one OAuth client.
  // MANUAL_STEP (google-oauth-origins): the deployed dev origin must be listed
  // under Google Cloud Console → Credentials → this client → Authorized
  // JavaScript origins, otherwise GSI answers 403 "origin not allowed".
  googleClientId:
    '354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com',
  // Sign in with Apple Services ID — the WEB audience, never the iOS bundle id.
  // MANUAL_STEP (apple-services-id-dev): the Services ID must be created under
  // the primary App ID `cz.cleansia.customer` (an ungrouped one issues a
  // different `sub` and locks every existing iOS user out of web sign-in), with
  // `customer.dev.cleansia.cz` registered as a domain and
  // `https://customer.dev.cleansia.cz/auth/apple/callback` as a return URL.
  // Must stay equal to appleWebServicesId in deploy/bicep/weu.dev.bicepparam,
  // which is the audience the API accepts — WebSocialAudienceConfigPinTests
  // pins the pair. Blank it to hide the button again.
  appleClientId: 'cz.cleansia.customer.web',
  isDevelopment: false,
  sentryDsn: '',
  bugReportUrl:
    'https://docs.google.com/spreadsheets/d/1k4IbmrKPkZo79D4pDzukjUjQqY-ipnXqkUSSFoHtfFg/edit?usp=sharing',
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
