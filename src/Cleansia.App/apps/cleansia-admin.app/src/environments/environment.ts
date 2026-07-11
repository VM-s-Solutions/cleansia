export const environment = {
  apiHost: 'localhost',
  apiPort: '5001',
  // Empty on purpose: /api is same-origin, served by the dev-server proxy
  // (proxy.conf.json → local Admin API :5001; --configuration=devremote →
  // the deployed dev API). The auth cookie is SameSite=Strict, so the
  // browser must see one origin — never put an absolute URL back here.
  apiBaseUrl: '',
  apiProtocol: 'http',
  isDevelopment: true,
  blobStorageUrl: 'http://127.0.0.1:10000/devstoreaccount1',
  googleClientId:
    '354682423254-boe1nlnb1dbd3m6a013d3nkpo2e9bgiq.apps.googleusercontent.com',
  betaGateEnabled: false,
  betaGateUrl: 'http://localhost:5000/gate',
  sentryDsn: '',
  bugReportUrl: '',
};
