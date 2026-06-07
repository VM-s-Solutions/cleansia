export const environment = {
  apiHost: 'localhost',
  apiPort: '5003',
  apiBaseUrl: 'http://localhost:5003',
  apiProtocol: 'http',
  isDevelopment: true,
  blobStorageUrl: 'http://127.0.0.1:10000/devstoreaccount1',
  googleClientId:
    '354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com',
  sentryDsn: '',
  bugReportUrl: '',
  // The Mapbox token must NEVER ship in the browser bundle.
  // It now lives server-side and is injected by the same-origin proxy
  // (server.ts, reads process.env.MAPBOX_TOKEN). This is only a token-free
  // "is geocoding configured" flag that toggles the UI.
  mapboxToken: 'enabled',
};
